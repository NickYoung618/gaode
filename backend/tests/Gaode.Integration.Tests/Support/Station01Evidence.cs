namespace Gaode.Integration.Tests.Support;

public static partial class Station01Evidence
{
    public static void AssertM1Boundary(int fTriggers, int threeDTriggers, int moveCommands,
        string recipeState, string qualityState, IEnumerable<string> writeKinds)
    {
        if (fTriggers is < 0 or > 1 || threeDTriggers is < 0 or > 1 || moveCommands is < 0 or > 2)
            throw new InvalidOperationException("M1动作或采集计数越界");
        if (recipeState != "Bound" || qualityState != "NotEvaluated")
            throw new InvalidOperationException("第一工位公共准备配方绑定或质量边界错误");
        if (writeKinds.Any(x => x.Contains("Recipe", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("M1产生配方操作证据");
    }

}
