using Gaode.Domain.Station01;
using System.Collections.Concurrent;

namespace Gaode.Application.Timing;

public sealed record IngressEvidenceSnapshot(IReadOnlyList<IngressDecision> LateDetails,
    int LateOverflow, int DuplicateCount, bool DuplicateSummaryPresent);

public sealed class OperationIngress
{
    private readonly DeadlineScheduler scheduler;
    private readonly int lateDetailLimit;
    private readonly int duplicateSummaryLimit;
    private readonly int operationLimit;
    private readonly ConcurrentDictionary<OperationKey, EvidenceBucket> evidence = new();
    private readonly Queue<OperationKey> order = new();
    private readonly object evidenceGate = new();

    public OperationIngress(DeadlineScheduler scheduler, int lateDetailLimit = 16,
        int duplicateSummaryLimit = 1, int operationLimit = 256)
    {
        if (lateDetailLimit < 0 || duplicateSummaryLimit < 0 || operationLimit <= 0)
            throw new ArgumentOutOfRangeException(nameof(lateDetailLimit));
        this.scheduler = scheduler;
        this.lateDetailLimit = lateDetailLimit;
        this.duplicateSummaryLimit = duplicateSummaryLimit;
        this.operationLimit = operationLimit;
    }

    public bool ReceiveIfExpired(DeadlineWindow window)
    {
        var now = scheduler.Clock.GetTimestamp();
        if (now < window.DueTick) return false;
        window.Timeout(now);
        return true;
    }

    public IngressDecision Receive(OperationKey key, string? reason = null)
    {
        var received = scheduler.Clock.GetTimestamp();
        var window = scheduler.Get(key);
        var decision = window is null
            ? new(key, IngressOutcome.Unmatched, received, 0, reason)
            : window.Receive(received, reason);
        if (window is not null && decision.Outcome is IngressOutcome.Late or IngressOutcome.Duplicate)
            Track(decision);
        return decision;
    }

    public DeadlineWindow Register(OperationKey key, int budgetMs) => scheduler.Register(key, budgetMs);

    public IngressEvidenceSnapshot GetEvidence(OperationKey key)
    {
        if (!evidence.TryGetValue(key, out var value)) return new([], 0, 0, false);
        lock (value.Gate) return new(value.Late.ToArray(), value.LateOverflow,
            value.DuplicateCount, duplicateSummaryLimit > 0 && value.DuplicateCount > 0);
    }

    public int TrackedEvidenceOperations => evidence.Count;

    private void Track(IngressDecision decision)
    {
        EvidenceBucket value;
        lock (evidenceGate)
        {
            if (!evidence.TryGetValue(decision.Key, out value!))
            {
                value = new();
                evidence[decision.Key] = value;
                order.Enqueue(decision.Key);
                while (order.Count > operationLimit)
                    evidence.TryRemove(order.Dequeue(), out _);
            }
        }
        lock (value.Gate)
        {
            if (decision.Outcome == IngressOutcome.Late)
            {
                if (value.Late.Count < lateDetailLimit) value.Late.Add(decision);
                else value.LateOverflow++;
            }
            else value.DuplicateCount++;
        }
    }

    private sealed class EvidenceBucket
    {
        public object Gate { get; } = new();
        public List<IngressDecision> Late { get; } = [];
        public int LateOverflow { get; set; }
        public int DuplicateCount { get; set; }
    }
}
