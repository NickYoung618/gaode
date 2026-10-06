using Gaode.Contracts.Tests.Support;
using Gaode.Infrastructure.Simulation;
using Gaode.Application.Ports;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Gaode.Contracts.Tests.Simulation;

public sealed class DeliveryIsolationTests
{
    [Fact]
    public async Task DeviceTimerDeliveryDoesNotDependOnCallerSynchronizationContext()
    {
        var clock = new FakeTimeProvider();
        var scheduler = new SimulationEventScheduler(clock, 8);
        var context = new UnpumpedContext();
        var previous = SynchronizationContext.Current;
        Task delivery;
        var delivered = false;
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            delivery = scheduler.ScheduleTracked(10, () => delivered = true);
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
        clock.Advance(TimeSpan.FromMilliseconds(10));
        await delivery.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(delivered);
        Assert.Equal(0, context.Posts);
    }

    [Fact]
    public async Task StartDoesNotRequireButtonInsideAcceptanceCallback()
    {
        var (_, budget, profile) = TestConfiguration.Normal();
        var clock = new FakeTimeProvider();
        using var plc = new SimulatedPlc(profile, new(clock, budget.Limits.MaxPendingTimerEvents));
        var accepted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await plc.RequestStartAsync(new(Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(),
            "snapshot", "1", "Test", 1, 999999, "clock"), Guid.NewGuid(), Guid.NewGuid(), e =>
        {
            if (e.Kind != DeviceEventKind.Accepted) return;
            accepted.TrySetResult();
        }, CancellationToken.None);
        clock.Advance(TimeSpan.FromMilliseconds(profile.Stages.PlcAcceptance.DelayMs));
        await accepted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(Gaode.Domain.Station01.ClampState.Released, plc.Observe().Clamp);
    }

    private sealed class UnpumpedContext : SynchronizationContext
    {
        public int Posts;
        public override void Post(SendOrPostCallback callback, object? state) => Interlocked.Increment(ref Posts);
    }
}
