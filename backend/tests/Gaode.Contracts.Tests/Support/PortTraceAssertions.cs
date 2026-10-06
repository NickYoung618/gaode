using Gaode.Domain.Station01;

namespace Gaode.Contracts.Tests.Support;

public sealed record Station01PortTrace(int RecipeOperations, int LaterStationStarts,
    int FTriggers, int ThreeDTriggers, int MoveCommands, string QualityState,
    IReadOnlyList<string> OperationKinds);

public static class PortTraceAssertions
{
    public static void AssertM1Boundary(Station01PortTrace trace)
    {
        if (trace.RecipeOperations != 0 || trace.LaterStationStarts != 0)
            throw new InvalidOperationException("M1不得执行配方操作或启动后续工位");
        if (trace.FTriggers is < 0 or > 1)
            throw new InvalidOperationException("F步骤最多触发一次");
        if (trace.ThreeDTriggers is < 0 or > 1 || trace.MoveCommands is < 0 or > 2)
            throw new InvalidOperationException("M1包含范围外采集或运动");
        if (trace.QualityState != "NotEvaluated")
            throw new InvalidOperationException("公共准备不得给出质量结论");
        var allowed = new[] { "Start", "Move3D", "Capture3D", "Height", "MoveF", "CaptureF", "DecodeF" };
        if (trace.OperationKinds.Except(allowed, StringComparer.Ordinal).Any())
            throw new InvalidOperationException("M1包含范围外工艺动作");
    }
}
