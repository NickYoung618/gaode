using Gaode.Domain.Station01;
using Xunit;

namespace Gaode.Rules.Tests.Station01;

public sealed class FCodePolicyTests
{
    [Fact]
    public void OrdinalDuplicatesPreserveRawAndYieldOneCandidate()
    {
        var result = FCodePolicy.Evaluate(true, ["TEST-TRAY-0001", "TEST-TRAY-0001"],
            _ => (true, "TEST-TRAY-0001"));
        Assert.Equal(2, result.RawCandidates!.Count);
        Assert.Single(result.DistinctRawValues);
        Assert.Equal("TEST-TRAY-0001", result.PrimaryCode);
        Assert.Equal(FParseState.Parsed, result.ParseState);
    }

    [Fact]
    public void DifferentCodesConflictWithoutChoosingPrimary()
    {
        var result = FCodePolicy.Evaluate(true, ["a", "A"]);
        Assert.Equal(FRecognitionState.Conflict, result.RecognitionState);
        Assert.Null(result.PrimaryCode);
    }

    [Fact]
    public void NoResponseAndEmptyResponseRemainDistinct()
    {
        Assert.Equal(FRecognitionState.NoResponse, FCodePolicy.Evaluate(false, null).RecognitionState);
        Assert.Equal(FRecognitionState.NoCode, FCodePolicy.Evaluate(true, []).RecognitionState);
        Assert.Equal(FParseState.NotDefined, FCodePolicy.Evaluate(true, ["raw"]).ParseState);
    }
}
