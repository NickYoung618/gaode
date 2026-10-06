using Gaode.Domain.Station01;

namespace Gaode.Application.Timing;

public sealed record IngressDecision(OperationKey Key, IngressOutcome Outcome,
    long ReceivedTick, long DueTick, string? Reason = null)
{
    public bool IsSuccessful => Outcome == IngressOutcome.Accepted;
}
