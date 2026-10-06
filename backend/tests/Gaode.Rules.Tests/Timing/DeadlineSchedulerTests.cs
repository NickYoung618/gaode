using Gaode.Application.Timing;
using Gaode.Domain.Station01;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Gaode.Rules.Tests.Timing;

public sealed class DeadlineSchedulerTests
{
    [Fact]
    public async Task EarlyTimerWakeupRearmsRemainingBudgetWithoutChangingDeadline()
    {
        var clock = new EarlyWakeClock();
        var scheduler = new DeadlineScheduler(clock, "early-wakeup");
        var window = scheduler.Register(new(Guid.NewGuid(), Guid.NewGuid(), 1, OperationPhase.Result), 50);
        clock.Tick = 49;
        clock.Timer.Fire();
        Assert.False(window.Completion.IsCompleted);
        Assert.Equal(TimeSpan.FromMilliseconds(1), clock.Timer.LastDue);
        Assert.Equal(50, window.DueTick);
        clock.Tick = 50;
        clock.Timer.Fire();
        Assert.Equal(IngressOutcome.TimedOut, (await window.Completion).Outcome);
    }

    private sealed class EarlyWakeClock : TimeProvider
    {
        public long Tick;
        public override long TimestampFrequency => 1000;
        public override long GetTimestamp() => Tick;
        public ProbeTimer Timer { get; private set; } = null!;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            Timer = new(callback, state);
        public sealed class ProbeTimer(TimerCallback callback, object? state) : ITimer
        {
            public TimeSpan LastDue;
            public bool Change(TimeSpan dueTime, TimeSpan period) { LastDue = dueTime; return true; }
            public void Fire() => callback(state);
            public void Dispose() { }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task FakeClockDrivesRealBusinessTimerRatherThanReturningTimeoutString()
    {
        var clock = new FakeTimeProvider();
        var scheduler = new DeadlineScheduler(clock, "clock-test");
        var key = new OperationKey(Guid.NewGuid(), Guid.NewGuid(), 1, OperationPhase.Completion);
        var window = scheduler.Register(key, 50);
        Assert.False(window.Completion.IsCompleted);
        clock.Advance(TimeSpan.FromMilliseconds(49));
        Assert.False(window.Completion.IsCompleted);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal(IngressOutcome.TimedOut, (await window.Completion).Outcome);
        Assert.Equal("clock-test", window.ClockId);
    }

    [Fact]
    public async Task ClosedWindowsUseBoundedRetentionAndRetainedTimeoutStillClassifiesLate()
    {
        var clock = new FakeTimeProvider();
        var scheduler = new DeadlineScheduler(clock, "bounded-clock", closedRetentionLimit: 8);
        var ingress = new OperationIngress(scheduler);
        var keys = new List<OperationKey>();

        for (var index = 0; index < 100; index++)
        {
            var key = new OperationKey(Guid.NewGuid(), Guid.NewGuid(), 1, OperationPhase.Result);
            keys.Add(key);
            var window = scheduler.Register(key, 1);
            clock.Advance(TimeSpan.FromMilliseconds(1));
            Assert.Equal(IngressOutcome.TimedOut, (await window.Completion).Outcome);
        }

        Assert.Null(scheduler.Get(keys[0]));
        Assert.NotNull(scheduler.Get(keys[^1]));
        Assert.Empty(scheduler.PendingDueTicks);
        Assert.Equal(IngressOutcome.Late, ingress.Receive(keys[^1], "late-result").Outcome);
        Assert.Equal(IngressOutcome.Unmatched, ingress.Receive(keys[0], "expired-tombstone").Outcome);
    }
}
