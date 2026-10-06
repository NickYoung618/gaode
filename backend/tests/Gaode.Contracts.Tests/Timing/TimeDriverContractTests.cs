using Gaode.Application.Timing;
using Gaode.Contracts.Tests.Support;
using Gaode.Domain.Station01;
using Xunit;

namespace Gaode.Contracts.Tests.Timing;

public sealed class TimeDriverContractTests
{
    [Fact]
    public async Task AdvancesOnlyRequestedInstantAndOrdersDifferentTimers()
    {
        var driver = new ControlledTimeDriver();
        var scheduler = new DeadlineScheduler(driver.Clock, "virtual-contract");
        var first = scheduler.Register(Key(), 10);
        var second = scheduler.Register(Key(), 20);
        await driver.AdvanceAndDrainAsync(TimeSpan.FromMilliseconds(10));
        Assert.True(first.Completion.IsCompleted);
        Assert.False(second.Completion.IsCompleted);
        await driver.AdvanceAndDrainAsync(TimeSpan.FromMilliseconds(10));
        Assert.True(second.Completion.IsCompleted);
    }

    [Fact]
    public async Task SameInstantTimersExpireTogetherAndEarlyResponseWins()
    {
        var driver = new ControlledTimeDriver();
        var scheduler = new DeadlineScheduler(driver.Clock, "virtual-contract");
        var ingress = new OperationIngress(scheduler);
        var acceptedKey = Key();
        var accepted = ingress.Register(acceptedKey, 10);
        var expired = ingress.Register(Key(), 10);
        Assert.Equal(IngressOutcome.Accepted, ingress.Receive(acceptedKey, "early").Outcome);
        await driver.AdvanceAndDrainAsync(TimeSpan.FromMilliseconds(10));
        Assert.Equal(IngressOutcome.Accepted, (await accepted.Completion).Outcome);
        Assert.Equal(IngressOutcome.TimedOut, (await expired.Completion).Outcome);
    }

    [Fact]
    public async Task DrainDoesNotAdvanceTimeOrInventExternalIoCompletion()
    {
        var driver = new ControlledTimeDriver();
        var before = driver.Clock.GetTimestamp();
        var externalIo = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var query = Task.FromResult("Ready");
        await driver.DrainReadyAsync();
        Assert.Equal("Ready", await query);
        Assert.False(externalIo.Task.IsCompleted);
        Assert.Equal(before, driver.Clock.GetTimestamp());
        Assert.NotEqual(driver.Clock.GetUtcNow(), DateTimeOffset.UtcNow);
    }

    private static OperationKey Key() =>
        new(Guid.NewGuid(), Guid.NewGuid(), 1, OperationPhase.Result);
}
