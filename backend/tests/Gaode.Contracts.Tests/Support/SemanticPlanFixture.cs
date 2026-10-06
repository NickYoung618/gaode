using Gaode.Application.Capabilities;
using Gaode.Application.Recipes;

namespace Gaode.Contracts.Tests.Support;

// Explicit UpperIsolation inputs; these do not prove a shared main-chain run.
internal static class SemanticPlanFixture
{
    public static ApprovalScope Approval => new("unit-approval", "1", "unit-digest", "Test", ["P01", "P02"], "unit-approval");
    public static SlotExecutionInputs Sorting(string slot, string ng = "P14") => new(slot,
        new(null, [], null, null, new Dictionary<string, HandlingPoint>
        {
            ["NG"] = new(new(ng, "test-v1", 400, 300, "mm", "SIM_MACHINE", 150), "unit://" + ng),
            ["Pending"] = new(new("P15", "test-v1", 500, 300, "mm", "SIM_MACHINE", 150), "unit://P15")
        }, new Dictionary<string, RecipePurposePoint>()), new Dictionary<string, ObjectExecutionInputs>());

    public static CapabilityRegistry Capabilities()
    {
        var registry = Station01Policies.Create();
        registry.Register(new FixedCapabilityPolicy("code.test-tray-format", "1.0", "Parser", new HashSet<string> { "Test" }));
        registry.RegisterDecoder("code.test-tray-format", "1.0", Gaode.Infrastructure.Recipes.RecipeEnvironmentDecoder.DecodeTrayCode);
        return registry;
    }
}
