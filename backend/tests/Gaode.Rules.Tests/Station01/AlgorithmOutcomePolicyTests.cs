using Gaode.Application.Station01;
using Gaode.Domain.Station01;
using Xunit;

namespace Gaode.Rules.Tests.Station01;

public sealed class AlgorithmOutcomePolicyTests
{
    [Theory]
    [InlineData(AlgorithmState.NotConfigured)]
    [InlineData(AlgorithmState.NotIntegrated)]
    [InlineData(AlgorithmState.NotReady)]
    [InlineData(AlgorithmState.Error)]
    [InlineData(AlgorithmState.TimedOut)]
    [InlineData(AlgorithmState.NoResult)]
    [InlineData(AlgorithmState.InvalidResult)]
    [InlineData(AlgorithmState.DependencyFailed)]
    public void FiniteAlgorithmFailureDoesNotBlockAnIndependentStep(AlgorithmState state)
    {
        var decision = AlgorithmOutcomePolicy.Decide(state);
        Assert.True(decision.IsFinite);
        Assert.True(decision.ContinueIndependentStep);
        Assert.True(ContinuationPolicy.CanContinueIndependentStep(state, true, true));
        Assert.True(ContinuationPolicy.CanCompleteWithLimitations(state, true, true));
    }

    [Theory]
    [InlineData(AlgorithmState.Queued)]
    [InlineData(AlgorithmState.Running)]
    [InlineData(AlgorithmState.Cancelled)]
    public void NonFiniteOrCancelledOutcomeCannotBeUsedAsCompletion(AlgorithmState state)
    {
        var decision = AlgorithmOutcomePolicy.Decide(state);
        Assert.False(decision.CanCompleteWithLimitations);
        Assert.False(ContinuationPolicy.CanCompleteWithLimitations(state, true, true));
    }
}
