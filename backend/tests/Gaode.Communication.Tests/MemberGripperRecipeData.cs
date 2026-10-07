using Gaode.Application.Recipes;
using Gaode.Domain.Configuration;

namespace Gaode.Communication.Tests.Devices;

internal static class Recipe011Data
{
    internal static RecipeDefinition ForSlots(int faces, params int[] slots)
    {
        var basis = Candidate(faces, extraE: false);
        var recipe = basis with
        {
            Capacity = slots.Max(),
            Positions = Enumerable.Range(1, slots.Max()).Select(index => new RecipePosition($"s{index}", $"{{TrayRunId}}/s{index}",
                [new("part", "{UnitId}/part", "individual")]) { PhysicalSlotIndex = index,
                    CellId = RecipeTrayLayout.CellIdentity(1, index + 3) }).ToArray(),
            TrayLayout = new(10, 10, Enumerable.Range(1, slots.Max()).Select(index =>
                new RecipeTrayCell(RecipeTrayLayout.CellIdentity(1, index + 3), 1, index + 3, RecipeTrayRegion.OK))
                .Concat([new("r1:c1", 1, 1, RecipeTrayRegion.NG), new("r1:c10", 1, 10, RecipeTrayRegion.Pending)]).ToArray()),
            TraySlotMapping = new("component-map", "test-1", "Test:explicit Recipe011Data physical mapping",
                Enumerable.Range(1, slots.Max()).Select(index => new RecipeCellPhysicalBinding(
                    RecipeTrayLayout.CellIdentity(1, index + 3), index)).ToArray()),
            ExecutionPositions = Enumerable.Range(1, slots.Max()).ToDictionary(index => $"s{index}", index =>
            {
                var input = basis.ExecutionPositions["s1"].PhysicalEntity;
                return new SlotExecutionInputs($"s{index}", input with
                {
                    Source = input.Source! with { Point = input.Source.Point with { Id = $"s{index}" } },
                    Coordinates = input.Coordinates.Select(c => c with { SlotId = $"s{index}", PhysicalSlotIndex = index }).ToArray()
                }, new Dictionary<string, ObjectExecutionInputs>());
            }),
            Approval = basis.Approval with { AllowedSlots = slots.Select(i => $"s{i}").ToArray() },
            RecipeId = "component-recipe", Version = "component-version", CatalogDigest = "component-catalog"
        };
        return recipe with { DefinitionDigest = RecipeDefinitionIdentity.ComputeDefinitionDigest(recipe) };
    }

    internal static RecipeDefinition Candidate(int faces = 4, bool extraE = true)
    {
        var stages = Enumerable.Range(1, faces).Select(n => new RecipeStage(n,
            n == 1 ? "none" : "flipAffectedMembersOneByOne", null,
            [new("part", n, n == 2 ? "AB" : "CD", "detect", "defect")])).ToArray();
        var points = new Dictionary<string, RecipePurposePoint>(StringComparer.Ordinal)
        {
            ["pick"] = Purpose(RecipePointPurpose.FlipPick),
            ["put"] = Purpose(RecipePointPurpose.FlipPutBack),
            ["scan"] = Purpose(RecipePointPurpose.EScan)
        };
        var coordinates = stages.SelectMany(s => s.Targets.SelectMany(t => t.CameraPair.Select(c =>
            new CoordinateDefinition($"f{t.LocalFace}-{c}", new($"detect-{c}", "test-1", 10, 20, "mm", "test-frame"),
                "{UnitId}/part", "s1", 1, t.LocalFace, c.ToString(), $"stage:{s.Number}",
                "test-1", "Test:explicit-config", new(3, "mm", "test-frame", "Test-only", "test-1")) { CaptureProfile = "detect" }))).ToArray();
        var flip = new RecipeFlipInputs(stages.Skip(1).ToDictionary(s => $"stage:{s.Number}",
            s => new RecipeFlipTransition(new("motion", "test-1", $"face-{s.Number}"), "pick", "put")));
        var objectInputs = new ObjectExecutionInputs(new(new("source", "test-1", 1, 2, "mm", "test-frame", 3), "Test:source"),
            coordinates, faces > 1 ? flip : null, null,
            new Dictionary<string, HandlingPoint>(), points) {
                SortingCellIds = new Dictionary<string, string> { ["NG"] = "r1:c1", ["Pending"] = "r1:c10" } };
        return new RecipeDefinition("", "", "draft", "component-scenario", "CaseSensitiveModel", "tray-raw", "independentPart", "part",
            "layout", 1, [new("s1", "{TrayRunId}/s1", [new("part", "{UnitId}/part", "individual")]) { PhysicalSlotIndex = 1, CellId = "r1:c4" }],
            [new("part", Enumerable.Range(1, faces).ToArray())], "ordinaryBatch", stages,
            new(extraE, extraE ? "part" : null, extraE ? "physicalEntity" : null, extraE, null) {
                ExtraPose = extraE ? new("mark-view", new("motion", "test-1", "mark-access"), "scan", "pick", "put", "detect", "ecode") : null },
            new("originalSlot", "NG", "Pending", "singlePart"), "motion", "quality",
            new Dictionary<string, CaptureProfile> { ["detect"] = new("detect", "test-1", new("detect", 10, 1, [0, 0, 2, 2], "light", 1, 0)) },
            new Dictionary<string, SlotExecutionInputs> { ["s1"] = new("s1", objectInputs, new Dictionary<string, ObjectExecutionInputs>()) }, "")
        {
            SchemaVersion = RecipeDefinitionSerialization.CurrentSchema, SortingGripperId = 1,
            InspectionKind = RecipeInspectionKind.Ordinary, NgCapacity = 1, PendingCapacity = 1,
            TrayLayout = new(10, 10, [new("r1:c1", 1, 1, RecipeTrayRegion.NG), new("r1:c4", 1, 4, RecipeTrayRegion.OK),
                new("r1:c10", 1, 10, RecipeTrayRegion.Pending)]),
            TraySlotMapping = new("component-map", "test-1", "Test:explicit component mapping", [new("r1:c4", 1)]),
            SortingTargets = new Dictionary<string, HandlingPoint> {
                ["r1:c1"] = new(new("ng", "test-1", 30, 40, "mm", "test-frame", 3), "Test:ng"),
                ["r1:c10"] = new(new("pending", "test-1", 50, 60, "mm", "test-frame", 3), "Test:pending") },
            DefinitionDigest = "",
            Approval = new("test-scope", "test-1", "test-digest", "Test", ["s1"], "Test-only"),
            AlgorithmRequirements = new Dictionary<string, AlgorithmRequirement> {
                ["defect"] = new("defect", "test-1", AlgorithmPurpose.SingleDetection, 1, "test-defect/1"),
                ["defect/fusion"] = new("defect/fusion", "test-1", AlgorithmPurpose.FaceFusion, 2, "test-fusion/1"),
                ["ecode"] = new("ecode", "test-1", AlgorithmPurpose.EntityCode, 1, "test-code/1") }
        };
    }

    private static RecipePurposePoint Purpose(RecipePointPurpose purpose) => new(purpose,
        new(purpose.ToString(), "test-1", 10, 20, "mm", "test-frame"),
        new(3, "mm", "test-frame", "Test-only", "test-1"), "Test:explicit-purpose")
        { CaptureProfile = purpose == RecipePointPurpose.EScan ? "detect" : null };
}
