using Gaode.Contracts.Tests.Support;
using Gaode.Domain.Configuration;
using Gaode.Infrastructure.Simulation;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Gaode.Contracts.Tests.Simulation;

public sealed class ResponsePolicyTests
{
    [Fact]
    public async Task StrategiesAreExplicitAndScheduledCapacityIsBounded()
    {
        var noResponse = new SimStage(1, "NoResponse", "Success", null, [], false);
        var failure = noResponse with { Strategy = "Fail", FailureCode = "Injected" };
        Assert.False(ResponsePolicy.ShouldRespond(noResponse));
        Assert.Equal("Injected", ResponsePolicy.FailureCode(failure));
        var clock = new FakeTimeProvider();
        var scheduler = new SimulationEventScheduler(clock, 1);
        var delivered = false;
        scheduler.Schedule(10, () => delivered = true);
        Assert.Throws<InvalidOperationException>(() => scheduler.Schedule(10, () => { }));
        clock.Advance(TimeSpan.FromMilliseconds(10));
        for (var i = 0; i < 4; i++) await Task.Yield();
        Assert.True(delivered);
        Assert.Equal(0, scheduler.Pending);
    }

    [Fact]
    public async Task ZeroDelayIsTrackedToCompletionAndCancellationSuppressesCallback()
    {
        var clock = new FakeTimeProvider();
        var scheduler = new SimulationEventScheduler(clock, 4);
        var called = false;
        var zero = scheduler.ScheduleTracked(0, () => called = true);
        await zero;
        Assert.True(called);
        Assert.Equal(0, scheduler.Pending);

        called = false;
        using var cancellation = new CancellationTokenSource();
        var delayed = scheduler.ScheduleTracked(10, () => called = true, cancellation.Token);
        await cancellation.CancelAsync();
        clock.Advance(TimeSpan.FromMilliseconds(10));
        await delayed;
        Assert.False(called);
    }
}
