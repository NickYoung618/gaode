using System.Text.Json;
using System.Text.Json.Nodes;
using Gaode.Application.Recipes;
using Xunit;

namespace Gaode.Contracts.Tests.Recipes;

public sealed class RecipeDefinitionSerializationTests
{
    [Fact]
    public void CurrentInspectionKindUsesTheExactSharedWireName()
    {
        var recipe = Recipe011Data.Candidate() with { InspectionKind = RecipeInspectionKind.SpecialRotation };
        var json = RecipeDefinitionSerialization.Serialize(recipe);
        Assert.Contains("\"inspectionKind\":\"specialRotation\"", json);
        Assert.Equal(RecipeInspectionKind.SpecialRotation, RecipeDefinitionSerialization.Deserialize(json).InspectionKind);
        Assert.Throws<System.Text.Json.JsonException>(() => RecipeDefinitionSerialization.Deserialize(json.Replace("specialRotation", "SpecialRotation")));
    }

    [Fact]
    public void CurrentLayoutRoundTripsWithoutInventingPhysicalMapping()
    {
        var recipe = Recipe011Data.Candidate(1, false);
        recipe = recipe with { TraySlotMapping = null,
            Positions = recipe.Positions.Select(p => p with { PhysicalSlotIndex = null }).ToArray(),
            ExecutionPositions = recipe.ExecutionPositions.ToDictionary(p => p.Key, p => p.Value with {
                PhysicalEntity = p.Value.PhysicalEntity with { Coordinates = p.Value.PhysicalEntity.Coordinates
                    .Select(c => c with { PhysicalSlotIndex = null }).ToArray() } }) };
        var read = RecipeDefinitionSerialization.Deserialize(RecipeDefinitionSerialization.Serialize(recipe));
        Assert.True(RecipeDefinitionValidator.ValidateForSave(read, RecipeCatalogSnapshots.Create("empty", [])).Valid);
        Assert.Null(read.Positions[0].PhysicalSlotIndex);
        Assert.Null(read.TraySlotMapping);
        Assert.Equal("ProductPhysicalSlotMappingMissing", RecipeDefinitionValidator.ExecutionProblem(read, ["s1"]));
        Assert.Equal("r1:c4", read.Positions[0].CellId);
    }

    [Fact]
    public void CurrentLayoutRejectsCountLinksAndGripperDefaults()
    {
        var recipe = Recipe011Data.Candidate(1, false);
        var snapshot = RecipeCatalogSnapshots.Create("empty", []);
        Assert.Contains(RecipeDefinitionValidator.ValidateForSave(recipe with { Capacity = 2 }, snapshot).Issues,
            i => i.Code == "RecipeTrayCountsMismatch");
        Assert.Contains(RecipeDefinitionValidator.ValidateForSave(recipe with { SortingGripperId = null }, snapshot).Issues,
            i => i.Code == "RecipeSortingGripperRequired");
        Assert.Contains(RecipeDefinitionValidator.ValidateForSave(recipe with {
            Positions = [recipe.Positions[0] with { CellId = "r1:c1" }] }, snapshot).Issues,
            i => i.Code == "RecipeTrayPositionLinkInvalid");
    }

    [Theory]
    [InlineData("recipe-definition/2")]
    [InlineData("recipe-definition/3")]
    public void HistoricalBodyReadsExactlyButCannotBecomeANewWrite(string schema)
    {
        var current = Recipe011Data.Candidate(1, false);
        var old = current with { SchemaVersion = schema, InspectionKind = null, TrayLayout = null,
            TraySlotMapping = null, SortingTargets = null,
            ExecutionPositions = current.ExecutionPositions.ToDictionary(p => p.Key, p => p.Value with {
                PhysicalEntity = p.Value.PhysicalEntity with { SortingCellIds = null,
                    Sorting = new Dictionary<string, HandlingPoint> { ["NG"] = current.SortingTargets!["r1:c1"],
                        ["Pending"] = current.SortingTargets!["r1:c10"] } } }),
            Positions = current.Positions.Select(p => p with { CellId = null }).ToArray() };
        var json = RecipeDefinitionSerialization.Serialize(old);
        var read = RecipeDefinitionSerialization.Deserialize(json);
        Assert.Equal(json, RecipeDefinitionSerialization.Serialize(read));
        Assert.Null(read.TrayLayout);
        Assert.Contains(RecipeDefinitionValidator.ValidateForSave(read, RecipeCatalogSnapshots.Create("empty", [])).Issues,
            i => i.Code == "RecipeCurrentSchemaRequiredForSave");
    }

    [Fact]
    public void FrozenLayoutAndMappingDoNotFollowCallerCollectionEdits()
    {
        var recipe = Recipe011Data.Candidate(1, false);
        var cells = recipe.TrayLayout!.Cells.ToList();
        var bindings = recipe.TraySlotMapping!.Bindings.ToList();
        var frozen = RecipeCatalogSnapshots.Freeze(recipe with {
            TrayLayout = recipe.TrayLayout with { Cells = cells }, TraySlotMapping = recipe.TraySlotMapping with { Bindings = bindings } });
        var digest = RecipeDefinitionIdentity.ComputeDefinitionDigest(frozen);
        cells.Clear(); bindings.Clear();
        Assert.Equal(3, frozen.TrayLayout!.Cells.Count);
        Assert.Single(frozen.TraySlotMapping!.Bindings);
        Assert.Equal(digest, RecipeDefinitionIdentity.ComputeDefinitionDigest(frozen));
    }
    [Fact]
    public void PurposePointsAndIndependentPoseRoundTripWithoutProtocolNumbers()
    {
        var recipe = Recipe011Data.Candidate();
        var text = RecipeDefinitionSerialization.Serialize(recipe);
        var read = RecipeDefinitionSerialization.Deserialize(text);
        Assert.Equal("mark-view", read.ECode.ExtraPose!.PoseId);
        Assert.Equal("mark-access", read.ECode.ExtraPose.TargetPose.PoseKey);
        Assert.Equal(RecipePointPurpose.FlipPutBack,
            read.ExecutionPositions["s1"].PhysicalEntity.PurposePoints["put"].Purpose);
        Assert.Equal(text, RecipeDefinitionSerialization.Serialize(read));
        Assert.DoesNotContain("coordinateRule", text);
        Assert.DoesNotContain("pointRefs", text);
    }

    [Fact]
    public void MissingCoordinateDoesNotBecomeZero()
    {
        var json = Body();
        Point(json)["point"]!.AsObject().Remove("x");
        Assert.Throws<JsonException>(() => RecipeDefinitionSerialization.Deserialize(json.ToJsonString()));
    }

    [Fact]
    public void NumericPurposeIsRejected()
    {
        var json = Body();
        Point(json)["purpose"] = 2;
        Assert.Throws<JsonException>(() => RecipeDefinitionSerialization.Deserialize(json.ToJsonString()));
    }

    [Fact]
    public void OldStageFieldsAreNotAnExecutionCompatibilityPath()
    {
        var json = Body();
        json["stages"]![0]!["coordinateRule"] = "initial3D";
        Assert.Throws<JsonException>(() => RecipeDefinitionSerialization.Deserialize(json.ToJsonString()));
    }

    [Fact]
    public void OmittedServerIdentityDoesNotCreateASavedVersion()
    {
        var json = Body();
        foreach (var name in new[] { "recipeId", "version", "definitionDigest", "catalogDigest" })
            json.Remove(name);
        var read = RecipeDefinitionSerialization.Deserialize(json.ToJsonString());
        Assert.Equal("", read.RecipeId);
        Assert.Equal("", read.Version);
        Assert.Equal("", read.DefinitionDigest);
    }

    private static JsonObject Body() => JsonNode.Parse(
        RecipeDefinitionSerialization.Serialize(Recipe011Data.Candidate()))!.AsObject();
    private static JsonObject Point(JsonObject json) => json["executionPositions"]!["s1"]!
        ["physicalEntity"]!["purposePoints"]!["pick"]!.AsObject();
}

// Component input only: these explicit Test values do not approve physical parameters.
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

    internal static RecipeRunPlan Plan(Guid trayId, int faces = 1, params int[] slots)
    {
        var selected = slots.Length == 0 ? new[] { 1 } : slots;
        var recipe = ForSlots(faces, selected);
        return RecipeRunPlanner.BuildExecutable(recipe, trayId.ToString("D"), selected.Select(i => $"s{i}").ToArray());
    }

    internal static Gaode.Domain.Configuration.PublicConfiguration Motion()
    {
        var source = Gaode.Contracts.Tests.Support.TestConfiguration.Normal().Public;
        return source with { Motion = source.Motion with { Frame = "test-frame",
            CoordinateSource = "Test:Recipe011Data explicit component coordinates" } };
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
