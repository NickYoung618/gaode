using Gaode.Domain.Configuration;

namespace Gaode.Infrastructure.Simulation;

public sealed class SimulationEventScheduler(TimeProvider clock, int maxPending)
{
    private int _pending;
    public TimeProvider Clock => clock;
    public int Pending => Volatile.Read(ref _pending);
    public event Action<long, Task>? DeliveryScheduled;

    public void Schedule(int delayMs, Action callback, CancellationToken cancellationToken = default)
        => _ = ScheduleTracked(delayMs, callback, cancellationToken);

    public Task ScheduleTracked(int delayMs, Action callback, CancellationToken cancellationToken = default)
    {
        if (delayMs < 0) throw new ArgumentOutOfRangeException(nameof(delayMs));
        if (Interlocked.Increment(ref _pending) > maxPending)
        {
            Interlocked.Decrement(ref _pending);
            throw new InvalidOperationException("模拟定时事件容量不足");
        }
        var due = clock.GetTimestamp() + (long)Math.Ceiling(delayMs * clock.TimestampFrequency / 1000d);
        var delivery = DeliverAsync(delayMs, callback, cancellationToken);
        DeliveryScheduled?.Invoke(due, delivery);
        return delivery;
    }

    public void Respond(SimStage stage, Action action, CancellationToken cancellationToken = default)
    {
        if (!ResponsePolicy.ShouldRespond(stage)) return;
        Schedule(stage.DelayMs, action, stage.IgnoreCancel ? CancellationToken.None : cancellationToken);
    }

    public Task RespondTracked(SimStage stage, Action action, CancellationToken cancellationToken = default)
    {
        if (!ResponsePolicy.ShouldRespond(stage)) return Task.CompletedTask;
        return ScheduleTracked(stage.DelayMs, action,
            stage.IgnoreCancel ? CancellationToken.None : cancellationToken);
    }

    public Task RespondTrackedAsync(SimStage stage, Func<Task> action, CancellationToken cancellationToken = default)
    {
        if (!ResponsePolicy.ShouldRespond(stage)) return Task.CompletedTask;
        if (stage.DelayMs < 0) throw new ArgumentOutOfRangeException(nameof(stage));
        if (Interlocked.Increment(ref _pending) > maxPending)
        { Interlocked.Decrement(ref _pending); throw new InvalidOperationException("模拟定时事件容量不足"); }
        var due = clock.GetTimestamp() + (long)Math.Ceiling(stage.DelayMs * clock.TimestampFrequency / 1000d);
        var delivery = DeliverWork();
        DeliveryScheduled?.Invoke(due, delivery);
        return delivery;
        async Task DeliverWork()
        {
            try
            {
                if (stage.DelayMs == 0) await Task.Yield();
                else await Task.Delay(TimeSpan.FromMilliseconds(stage.DelayMs), clock,
                    stage.IgnoreCancel ? CancellationToken.None : cancellationToken).ConfigureAwait(false);
                await action();
            }
            catch (OperationCanceledException) { }
            finally { Interlocked.Decrement(ref _pending); }
        }
    }

    public void Duplicates(SimStage stage, Action action)
    {
        foreach (var offset in stage.DuplicateOffsetsMs.Take(4))
            Schedule(checked(stage.DelayMs + offset), action);
    }

    private async Task DeliverAsync(int delayMs, Action callback, CancellationToken cancellationToken)
    {
        try
        {
            // Register positive-delay timers immediately. Zero delay still crosses an async boundary.
            if (delayMs == 0) await Task.Yield();
            else await Task.Delay(TimeSpan.FromMilliseconds(delayMs), clock, cancellationToken).ConfigureAwait(false);
            callback();
        }
        catch (OperationCanceledException) { }
        finally { Interlocked.Decrement(ref _pending); }
    }
}
