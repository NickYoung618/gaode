using Gaode.Domain.Station01;

namespace Gaode.Application.Timing;

public sealed class DeadlineWindow(OperationKey key, long startTick, long dueTick, string clockId,
    Action<DeadlineWindow>? onClosed = null)
{
    private readonly object _gate = new();
    private readonly TaskCompletionSource<IngressDecision> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _closed;
    private IngressOutcome? _closedOutcome;
    private ITimer? _timer;

    public OperationKey Key { get; } = key;
    public long StartTick { get; } = startTick;
    public long DueTick { get; } = dueTick;
    public string ClockId { get; } = clockId;
    public Task<IngressDecision> Completion => _completion.Task;

    internal void Attach(ITimer timer) => _timer = timer;

    internal void Arm(TimeProvider clock)
    {
        lock (_gate)
        {
            if (_closed) return;
            var now = clock.GetTimestamp();
            if (now >= DueTick) { Timeout(now); return; }
            // Timer resolution can wake before the monotonic deadline. Re-arm the
            // remaining interval, rounded up, rather than silently losing the timeout.
            var remainingMs = Math.Ceiling((DueTick - now) * 1000d / clock.TimestampFrequency);
            _timer!.Change(TimeSpan.FromMilliseconds(remainingMs), System.Threading.Timeout.InfiniteTimeSpan);
        }
    }

    public IngressDecision Receive(long receivedTick, string? reason = null)
    {
        lock (_gate)
        {
            if (_closed) return new(Key, _closedOutcome == IngressOutcome.TimedOut
                ? IngressOutcome.Late : IngressOutcome.Duplicate, receivedTick, DueTick, reason);
            if (receivedTick >= DueTick)
            {
                Close(new(Key, IngressOutcome.TimedOut, receivedTick, DueTick, "ResponseAtOrAfterDeadline"));
                return new(Key, IngressOutcome.Late, receivedTick, DueTick, reason);
            }
            var decision = new IngressDecision(Key, IngressOutcome.Accepted, receivedTick, DueTick, reason);
            Close(decision);
            return decision;
        }
    }

    public void Timeout(long observedTick)
    {
        lock (_gate)
        {
            if (_closed || observedTick < DueTick) return;
            Close(new(Key, IngressOutcome.TimedOut, observedTick, DueTick));
        }
    }

    private void Close(IngressDecision decision)
    {
        _closed = true;
        _closedOutcome = decision.Outcome;
        _timer?.Dispose();
        onClosed?.Invoke(this);
        _completion.TrySetResult(decision);
    }
}
