using Gaode.Domain.Station01;

namespace Gaode.Application.Station01;

public static class ContinuationPolicy
{
    public static bool CanContinueIndependentStep(AlgorithmState state, bool physicalSafe,
        bool saveCommitted) => physicalSafe && saveCommitted &&
        AlgorithmOutcomePolicy.Decide(state).ContinueIndependentStep;

    public static bool CanCompleteWithLimitations(AlgorithmState state, bool physicalSafe,
        bool saveCommitted) => physicalSafe && saveCommitted &&
        AlgorithmOutcomePolicy.Decide(state).CanCompleteWithLimitations;

    public static string Explain(AlgorithmState state) => AlgorithmOutcomePolicy.Decide(state).Reason;
}
