using Gaode.Application.Timing;
using Gaode.Domain.Station01;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Gaode.Rules.Tests.Timing;

public sealed class OperationIngressTests
{
    [Theory]
    [InlineData(-1, IngressOutcome.Accepted)]
    [InlineData(0, IngressOutcome.Late)]
    [InlineData(1, IngressOutcome.Late)]
    public async Task ArrivalAtDeadlineIsTimeoutRegardlessOfTimerOrder(int offsetMs, IngressOutcome expected)
    {
        var clock = new FakeTimeProvider();
        var ingress = new OperationIngress(new DeadlineScheduler(clock, "controlled"));
        var key = new OperationKey(Guid.NewGuid(), Guid.NewGuid(), 1, OperationPhase.Result);
        var window = ingress.Register(key, 100);
        clock.Advance(TimeSpan.FromMilliseconds(100 + offsetMs));
        var decision = ingress.Receive(key, "response");
        Assert.Equal(expected, decision.Outcome);
        Assert.Equal(offsetMs < 0 ? IngressOutcome.Accepted : IngressOutcome.TimedOut,
            (await window.Completion).Outcome);
    }

    [Fact]
    public void WrongIdentityIsUnmatchedAndSecondResultIsDuplicate()
    {
        var clock = new FakeTimeProvider();
        var ingress = new OperationIngress(new DeadlineScheduler(clock, "controlled"));
        var key = new OperationKey(Guid.NewGuid(), Guid.NewGuid(), 1, OperationPhase.Result);
        ingress.Register(key, 100);
        Assert.Equal(IngressOutcome.Unmatched,
            ingress.Receive(key with { Attempt = 2 }, "wrong-attempt").Outcome);
        Assert.Equal(IngressOutcome.Accepted, ingress.Receive(key, "first").Outcome);
        Assert.Equal(IngressOutcome.Duplicate, ingress.Receive(key, "duplicate").Outcome);
    }

    [Fact]
    public void LateDetailsDuplicateSummaryAndTrackedOperationsRemainBounded()
    {
        var clock = new FakeTimeProvider();
        var ingress = new OperationIngress(new DeadlineScheduler(clock, "bounded"),
            lateDetailLimit: 2, duplicateSummaryLimit: 1, operationLimit: 3);
        var acceptedKey = new OperationKey(Guid.NewGuid(), Guid.NewGuid(), 1, OperationPhase.Result);
        ingress.Register(acceptedKey, 10);
        Assert.Equal(IngressOutcome.Accepted, ingress.Receive(acceptedKey, "first").Outcome);
        for (var i = 0; i < 5; i++) ingress.Receive(acceptedKey, "duplicate-" + i);
        var duplicate = ingress.GetEvidence(acceptedKey);
        Assert.Equal(5, duplicate.DuplicateCount);
        Assert.True(duplicate.DuplicateSummaryPresent);

        OperationKey? last = null;
        for (var operation = 0; operation < 5; operation++)
        {
            last = new(Guid.NewGuid(), Guid.NewGuid(), 1, OperationPhase.Result);
            ingress.Register(last, 1);
            clock.Advance(TimeSpan.FromMilliseconds(1));
            for (var late = 0; late < 4; late++) ingress.Receive(last, "late-" + late);
        }
        var retained = ingress.GetEvidence(last!);
        Assert.Equal(2, retained.LateDetails.Count);
        Assert.Equal(2, retained.LateOverflow);
        Assert.True(ingress.TrackedEvidenceOperations <= 3);
    }
}
