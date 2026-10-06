using Gaode.Application.Station01;
using Gaode.Domain.Station01;
using Xunit;

namespace Gaode.Contracts.Tests.Station01;

public sealed class ThreeDAndFRecipeGateTests
{
    [Theory]
    [InlineData(false, true, AlgorithmState.Success, FRecognitionState.Unique, FParseState.Parsed)]
    [InlineData(true, false, AlgorithmState.Success, FRecognitionState.Unique, FParseState.Parsed)]
    [InlineData(true, true, AlgorithmState.Error, FRecognitionState.Unique, FParseState.Parsed)]
    [InlineData(true, true, AlgorithmState.Success, FRecognitionState.NoCode, FParseState.NotAttempted)]
    [InlineData(true, true, AlgorithmState.Success, FRecognitionState.Conflict, FParseState.NotAttempted)]
    [InlineData(true, true, AlgorithmState.Success, FRecognitionState.Unique, FParseState.InvalidFormat)]
    public void FailureLocksAndStopsBeforePlanBindOrHandoff(bool threeD, bool f,
        AlgorithmState state, FRecognitionState recognition, FParseState parse)
    {
        var code = Code(recognition, parse);
        var calls = 0;
        var decision = ThreeDAndFRecipeGate.Evaluate(threeD, f, state, code);

        Assert.False(decision.CanLoadAndBind);
        Assert.True(decision.MustLockAndStop);
        Assert.Throws<InvalidOperationException>(() =>
            ThreeDAndFRecipeGate.ExecuteAfterGate(decision, () => ++calls));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void ExactlyOneParsedFCodeAfterBothHandshakesAllowsPlanThenBind()
    {
        var decision = ThreeDAndFRecipeGate.Evaluate(true, true, AlgorithmState.Success,
            Code(FRecognitionState.Unique, FParseState.Parsed));
        var sequence = new List<string>();

        var result = ThreeDAndFRecipeGate.ExecuteAfterGate(decision, () =>
        {
            sequence.Add("Plan");
            sequence.Add("Bind");
            return "bound";
        });

        Assert.True(decision.CanLoadAndBind);
        Assert.False(decision.MustLockAndStop);
        Assert.Equal("bound", result);
        Assert.Equal(["Plan", "Bind"], sequence);
    }

    private static FCodeResult Code(FRecognitionState recognition, FParseState parse) =>
        new(true, ["F001"], recognition == FRecognitionState.Unique ? ["F001"] : ["F001", "F002"],
            recognition, recognition == FRecognitionState.Unique ? "F001" : null, parse,
            parse == FParseState.Parsed ? "F001" : null);
}
