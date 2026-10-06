using Gaode.Application.Ports;
using Gaode.Application.Workflow;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Gaode.Contracts.Tests.Workflow;

[Trait("EvidenceLevel", "UpperIsolation")]
public sealed class StageRetryPolicyTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 23, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DetectionCommunicationHasFourTotalAttemptsAndOneTwoFourBackoff()
    {
        var clock = new FakeTimeProvider(Start);
        var deadline = StageRetryPolicy.FreezeDeadline(clock.GetUtcNow());

        Assert.Equal(TimeSpan.FromSeconds(1), Decide(clock, deadline, StageFailureClass.DetectionCommunication, 1).Delay);
        Assert.Equal(TimeSpan.FromSeconds(2), Decide(clock, deadline, StageFailureClass.DetectionCommunication, 2).Delay);
        Assert.Equal(TimeSpan.FromSeconds(4), Decide(clock, deadline, StageFailureClass.DetectionCommunication, 3).Delay);
        Assert.Equal(StageRetryDisposition.Pending,
            Decide(clock, deadline, StageFailureClass.DetectionCommunication, 4).Disposition);
    }

    [Fact]
    public void AlgorithmTimeoutHasThreeTotalAttemptsAndTwoFiveBackoff()
    {
        var clock = new FakeTimeProvider(Start);
        var deadline = StageRetryPolicy.FreezeDeadline(clock.GetUtcNow());

        Assert.Equal(TimeSpan.FromSeconds(2), Decide(clock, deadline, StageFailureClass.DetectionAlgorithmTimeout, 1).Delay);
        Assert.Equal(TimeSpan.FromSeconds(5), Decide(clock, deadline, StageFailureClass.DetectionAlgorithmTimeout, 2).Delay);
        Assert.Equal(StageRetryDisposition.Pending,
            Decide(clock, deadline, StageFailureClass.DetectionAlgorithmTimeout, 3).Disposition);
    }

    [Fact]
    public void SharedDeadlineWinsAndDetectionBecomesPending()
    {
        var clock = new FakeTimeProvider(Start);
        var deadline = StageRetryPolicy.FreezeDeadline(Start);
        clock.Advance(TimeSpan.FromSeconds(119));

        var decision = Decide(clock, deadline, StageFailureClass.DetectionCommunication, 1);

        Assert.Equal(StageRetryDisposition.Pending, decision.Disposition);
        Assert.Equal("StageDeadlineWouldBeExceeded", decision.ErrorCode);
        Assert.Null(decision.Delay);
    }

    [Fact]
    public void RestartUsesPersistedDeadlineInsteadOfResettingIt()
    {
        var persisted = Start.AddSeconds(120);
        var restartedAt = Start.AddSeconds(75);

        Assert.Equal(persisted, StageRetryPolicy.FreezeDeadline(restartedAt, persisted));
    }

    [Fact]
    public void PlcPreDispatchFailureCanRetryButExhaustionIsFailed()
    {
        var clock = new FakeTimeProvider(Start);
        var deadline = StageRetryPolicy.FreezeDeadline(Start);

        Assert.Equal(StageRetryDisposition.Retry,
            Decide(clock, deadline, StageFailureClass.PlcPreDispatchCommunication, 1).Disposition);
        Assert.Equal(StageRetryDisposition.Failed,
            Decide(clock, deadline, StageFailureClass.PlcPreDispatchCommunication, 4).Disposition);
    }

    [Fact]
    public void PossiblyDispatchedPlcActionNeverRetriesAndIsUnknownHeld()
    {
        var clock = new FakeTimeProvider(Start);
        var decision = Decide(clock, StageRetryPolicy.FreezeDeadline(Start),
            StageFailureClass.PlcPhysicalDispatchUnknown, 1);

        Assert.Equal(StageRetryDisposition.UnknownHeld, decision.Disposition);
        Assert.Null(decision.Delay);
        Assert.False(decision.AutomaticRetryAllowed);
    }

    private static StageRetryDecision Decide(FakeTimeProvider clock, DateTimeOffset deadline,
        StageFailureClass failure, int attempt) => StageRetryPolicy.Decide(new(
            failure, attempt, Start, deadline, clock.GetUtcNow(), "error-1", Guid.NewGuid()));
}
