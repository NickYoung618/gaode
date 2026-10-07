using System.Collections.Concurrent;
using Gaode.Domain.Station01;

namespace Gaode.Application.Timing;

public sealed class DeadlineScheduler
{
    public const int DefaultClosedRetentionLimit = 256;

    private readonly ConcurrentDictionary<OperationKey, DeadlineWindow> _windows = new();
    private readonly Queue<DeadlineWindow> _closed = new();
    private readonly object _retentionGate = new();
    private readonly int _closedRetentionLimit;

    public DeadlineScheduler(TimeProvider timeProvider, string clockId,
        int closedRetentionLimit = DefaultClosedRetentionLimit)
    {
        if (closedRetentionLimit <= 0) throw new ArgumentOutOfRangeException(nameof(closedRetentionLimit));
        Clock = timeProvider;
        ClockId = clockId;
        _closedRetentionLimit = closedRetentionLimit;
    }

    public TimeProvider Clock { get; }
    public string ClockId { get; }

    public DeadlineWindow Register(OperationKey key, int budgetMs)
    {
        if (!key.IsValid || budgetMs <= 0) throw new ArgumentException("期限身份或预算无效");
        var start = Clock.GetTimestamp();
        var ticks = checked((long)Math.Ceiling(Clock.TimestampFrequency * budgetMs / 1000.0));
        var window = new DeadlineWindow(key, start, checked(start + ticks), ClockId, RetainClosed);
        if (!_windows.TryAdd(key, window)) throw new InvalidOperationException("重复期限身份");
        var timer = Clock.CreateTimer(_ => window.Arm(Clock), null,
            Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        window.Attach(timer);
        window.Arm(Clock);
        return window;
    }

    public DeadlineWindow? Get(OperationKey key) => _windows.TryGetValue(key, out var window) ? window : null;
    public IReadOnlyList<long> PendingDueTicks => _windows.Values.Where(w => !w.Completion.IsCompleted)
        .Select(w => w.DueTick).Order().ToArray();

    private void RetainClosed(DeadlineWindow window)
    {
        lock (_retentionGate)
        {
            _closed.Enqueue(window);
            while (_closed.Count > _closedRetentionLimit)
            {
                var expired = _closed.Dequeue();
                if (_windows.TryGetValue(expired.Key, out var tracked) && ReferenceEquals(tracked, expired))
                    _windows.TryRemove(expired.Key, out _);
            }
        }
    }
}
