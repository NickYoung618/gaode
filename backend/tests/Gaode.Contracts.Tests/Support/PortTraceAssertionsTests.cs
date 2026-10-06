using Xunit;

namespace Gaode.Contracts.Tests.Support;

public sealed class PortTraceAssertionsTests
{
    [Fact]
    public void RejectsMinimalOutOfScopeTrace()
    {
        var trace = new Station01PortTrace(1, 0, 0, 0, 0, "NotEvaluated", []);
        var error = Assert.Throws<InvalidOperationException>(() => PortTraceAssertions.AssertM1Boundary(trace));
        Assert.Contains("配方", error.Message);
    }

    [Fact]
    public void AcceptsNormalPublicPreparationBoundary()
    {
        PortTraceAssertions.AssertM1Boundary(new(0, 0, 1, 1, 2, "NotEvaluated",
            ["Start", "Move3D", "Capture3D", "Height", "MoveF", "CaptureF", "DecodeF"]));
    }
}
