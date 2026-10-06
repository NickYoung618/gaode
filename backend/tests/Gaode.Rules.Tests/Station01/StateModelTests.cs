using Gaode.Domain.Station01;
using Xunit;

namespace Gaode.Rules.Tests.Station01;

public sealed class StateModelTests
{
    [Theory]
    [InlineData(RunState.Completed, TerminalOutcome.Completed, true)]
    [InlineData(RunState.CompletedWithExceptions, TerminalOutcome.CompletedWithExceptions, true)]
    [InlineData(RunState.Cancelled, TerminalOutcome.Cancelled, true)]
    [InlineData(RunState.Completed, TerminalOutcome.None, false)]
    [InlineData(RunState.RunningF, TerminalOutcome.Completed, false)]
    public void TerminalStateMustAgreeWithDurableOutcome(RunState state, TerminalOutcome outcome, bool expected) =>
        Assert.Equal(expected, RunStateRules.IsCompatible(state, outcome));

    [Fact]
    public void StageIdentityNeverPretendsToHaveRecipeOrQualityResult()
    {
        var run = new RunSnapshot(Guid.NewGuid(), "request", "test:Operator", RunState.Created,
            0, 0, TerminalOutcome.None, false, ActionState.NotRequested,
            CaptureState.NotRequested, AlgorithmState.NotRequested, SaveState.NotQueued,
            HandoffState.NotReady, null, null, null, []);
        Assert.Equal("Unmatched", run.RecipeState);
        Assert.Equal("NotEvaluated", run.QualityState);
        Assert.Equal("NotStarted", run.SortingState);
        Assert.Equal("NotCompleted", run.WholeTaskState);
    }
}
