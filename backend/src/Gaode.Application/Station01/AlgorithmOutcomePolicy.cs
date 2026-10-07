using Gaode.Domain.Station01;

namespace Gaode.Application.Station01;

public sealed record AlgorithmOutcomeDecision(AlgorithmState State, bool IsFinite,
    bool ContinueIndependentStep, bool CanCompleteWithLimitations, string Reason);

/// <summary>
/// Single mapping from technical algorithm outcomes to business continuation semantics.
/// Algorithm availability is never promoted to success and never blocks an independent
/// physical step by itself.
/// </summary>
public static class AlgorithmOutcomePolicy
{
    public static AlgorithmOutcomeDecision Decide(AlgorithmState state) => state switch
    {
        AlgorithmState.Success => new(state, true, true, true, "Success"),
        AlgorithmState.Error => new(state, true, true, true, "AlgorithmError"),
        AlgorithmState.TimedOut => new(state, true, true, true, "AlgorithmTimedOut"),
        AlgorithmState.NotConfigured => new(state, true, true, true, "NotConfigured"),
        AlgorithmState.NotIntegrated => new(state, true, true, true, "NotIntegrated"),
        AlgorithmState.NotReady => new(state, true, true, true, "NotReady"),
        AlgorithmState.NoResult => new(state, true, true, true, "NoResult"),
        AlgorithmState.InvalidResult => new(state, true, true, true, "InvalidResult"),
        AlgorithmState.DependencyFailed => new(state, true, true, true, "DependencyFailed"),
        AlgorithmState.Cancelled => new(state, true, false, false, "Cancelled"),
        AlgorithmState.Queued or AlgorithmState.Running =>
            new(state, false, false, false, "NonTerminal"),
        _ => new(state, false, false, false, "UnknownState")
    };

    public static void RequireFinite(AlgorithmState state)
    {
        if (!Decide(state).IsFinite)
            throw new InvalidOperationException("算法尚未形成有限终态：" + state);
    }
}
