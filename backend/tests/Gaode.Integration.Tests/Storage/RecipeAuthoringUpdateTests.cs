using Gaode.Application.Recipes;
using Gaode.Infrastructure.Recipes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Data.Sqlite;
using Xunit;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Gaode.Integration.Tests.Support;

namespace Gaode.Integration.Tests.Storage;

public sealed class RecipeAuthoringUpdateTests
{
    [Theory]
    [InlineData(4, true)]
    [InlineData(6, false)]
    public async Task ConfiguredFacesPurposePointsAndIndependentEArePreservedInRealStore(int faces, bool extraE)
    {
        var (allowed, root) = RecipeAuthoringTestRoots.New(); RecipeStoreSchema.Prepare(allowed, root);
        var options = new RecipeStoreOptions { DatabasePath = Path.Combine(root, "recipes.db"), ReadWriteTimeoutMs = 10000, DbLockTimeoutSeconds = 1 };
        using var store = new SqliteRecipeStore(options, allowed, NullLogger<SqliteRecipeStore>.Instance, () => "component-author");
        var saved = await store.SaveAsync(new(RecipeAuthoringTestInputs.RichCandidate(faces, extraE), null, null, "rich-content"), CancellationToken.None);
        Assert.Equal(RecipeSaveStatus.Saved, saved.Status);
        var content = Assert.Single(store.GetSnapshot().Definitions);
        Assert.Equal(RecipeDefinitionSerialization.Serialize(saved.Definition!), RecipeDefinitionSerialization.Serialize(content));
        Assert.Equal(faces, content.Stages.Count);
        Assert.Equal(extraE, content.ECode.ExtraPose is not null);
        Assert.Equal(3, content.ExecutionPositions["slot-one"].PhysicalEntity.PurposePoints.Count);
        Assert.Equal("originalSlot", content.Disposition.Ok);
    }

    [Fact]
    public async Task HttpUpdateUsesTheOriginalReadVersionAndRequiresCompleteRereadAfterConflict()
    {
        await using var host = await Station01HostFixture.CreateAsync();
        using var author = host.ClientForRole("ProcessEngineer");
        static JsonObject Body(RecipeDefinition definition, string requestId) => new()
        { ["requestId"] = requestId, ["definition"] = JsonNode.Parse(RecipeDefinitionSerialization.Serialize(definition)) };
        using var created = await author.PostAsJsonAsync("/api/v1/recipes", Body(RecipeAuthoringTestInputs.Candidate(), "api-version-new"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var saved = RecipeDefinitionSerialization.Deserialize(JsonNode.Parse(await created.Content.ReadAsStringAsync())!["definition"]!.ToJsonString());
        var url = "/api/v1/recipes/" + saved.RecipeId;
        using var original = await author.GetAsync(url);
        var tag = original.Headers.ETag!;
        var edited = saved with { Model = "edited-through-http" };
        using var missing = await author.PutAsJsonAsync(url, Body(edited, "api-version-missing"));
        Assert.Equal((HttpStatusCode)428, missing.StatusCode);
        async Task<HttpResponseMessage> Update(string requestId)
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, url) { Content = JsonContent.Create(Body(edited, requestId)) };
            request.Headers.IfMatch.Add(tag); return await author.SendAsync(request);
        }
        using var changed = await Update("api-version-update"); Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        using var conflict = await Update("api-version-stale"); Assert.Equal(HttpStatusCode.PreconditionFailed, conflict.StatusCode);
        using var reread = await author.GetAsync(url); Assert.NotEqual(tag, reread.Headers.ETag);
        var actual = RecipeDefinitionSerialization.Deserialize(JsonNode.Parse(await reread.Content.ReadAsStringAsync())!["definition"]!.ToJsonString());
        Assert.Equal("edited-through-http", actual.Model);
        Assert.Equal(saved.RecipeId, actual.RecipeId); Assert.NotEqual(saved.Version, actual.Version);
        Assert.Equal(RecipeDefinitionSerialization.Serialize(edited with { Version = actual.Version, DefinitionDigest = actual.DefinitionDigest, CatalogDigest = actual.CatalogDigest }),
            RecipeDefinitionSerialization.Serialize(actual));
    }

    [Fact]
    public async Task CompleteBodiesSurviveUpdateConflictAndStoreReopenWithoutMutatingAnOldSnapshot()
    {
        var (allowed, root) = RecipeAuthoringTestRoots.New();
        RecipeStoreSchema.Prepare(allowed, root);
        var options = new RecipeStoreOptions { DatabasePath = Path.Combine(root, "recipes.db"), ReadWriteTimeoutMs = 30000, DbLockTimeoutSeconds = 2 };
        RecipeDefinition original, edited;
        using (var store = new SqliteRecipeStore(options, allowed, NullLogger<SqliteRecipeStore>.Instance, () => "component-author"))
        {
            var candidate = RecipeAuthoringTestInputs.Candidate();
            var first = await store.SaveAsync(new(candidate, null, null, "create"), CancellationToken.None);
            Assert.Equal(RecipeSaveStatus.Saved, first.Status); original = first.Definition!;
            Assert.False(string.IsNullOrEmpty(original.RecipeId)); Assert.False(string.IsNullOrEmpty(original.Version));
            Assert.Equal(RecipeDefinitionIdentity.ComputeDefinitionDigest(original), original.DefinitionDigest);
            Assert.False(RecipeAdmission.Evaluate(original, ["slot-one"], "Production").Eligible);
            var old = store.GetSnapshot();
            var changed = original with { Model = "changed-model", FCode = " tray edited " };
            var second = await store.SaveAsync(new(changed, original.RecipeId, original.Version, "edit"), CancellationToken.None);
            Assert.Equal(RecipeSaveStatus.Saved, second.Status); edited = second.Definition!;
            Assert.NotEqual(original.Version, edited.Version);
            Assert.Equal(original.RecipeId, edited.RecipeId);
            Assert.Equal(RecipeDefinitionSerialization.Serialize(original), RecipeDefinitionSerialization.Serialize(Assert.Single(old.Definitions)));
            Assert.Equal(RecipeDefinitionSerialization.Serialize(edited), RecipeDefinitionSerialization.Serialize(Assert.Single(store.GetSnapshot().Definitions)));
            var stale = await store.SaveAsync(new(original, original.RecipeId, original.Version, "stale"), CancellationToken.None);
            Assert.Equal(RecipeSaveStatus.VersionConflict, stale.Status);
            Assert.Equal(edited.Version, Assert.Single(store.GetSnapshot().Definitions).Version);
            var duplicate = await store.SaveAsync(new(candidate with { FCode = edited.FCode }, null, null, "duplicate"), CancellationToken.None);
            Assert.Equal(RecipeSaveStatus.ValidationFailed, duplicate.Status);
            Assert.Contains(duplicate.Issues!, i => i.Code == "RecipeFCodeOccupied");
        }
        using var reopened = new SqliteRecipeStore(options, allowed, NullLogger<SqliteRecipeStore>.Instance, () => "component-author");
        var actual = Assert.Single(reopened.GetSnapshot().Definitions);
        Assert.Equal(RecipeDefinitionSerialization.Serialize(edited), RecipeDefinitionSerialization.Serialize(actual));
        Assert.Equal("edit", reopened.GetCurrentContent(actual.RecipeId)!.RequestId);
    }

    [Fact]
    public async Task CommonRejectionAndActualSqliteFailuresDoNotReportSavedOrReturnAFallback()
    {
        var (allowed, root) = RecipeAuthoringTestRoots.New(); RecipeStoreSchema.Prepare(allowed, root);
        var options = new RecipeStoreOptions { DatabasePath = Path.Combine(root, "recipes.db"), ReadWriteTimeoutMs = 10000, DbLockTimeoutSeconds = 1 };
        using var store = new SqliteRecipeStore(options, allowed, NullLogger<SqliteRecipeStore>.Instance, () => "component-author");
        var candidate = RecipeAuthoringTestInputs.Candidate();
        var invalid = candidate with { ExecutionPositions = new Dictionary<string, SlotExecutionInputs>() };
        var rejected = await store.SaveAsync(new(invalid, null, null, "missing-point"), CancellationToken.None);
        Assert.Equal(RecipeSaveStatus.ValidationFailed, rejected.Status); Assert.Empty(store.GetSnapshot().Definitions);
        var saved = await store.SaveAsync(new(candidate, null, null, "first"), CancellationToken.None);
        Assert.Equal(RecipeSaveStatus.Saved, saved.Status);
        using (var blocker = new SqliteConnection($"Data Source={options.DatabasePath};Mode=ReadWrite;Pooling=False"))
        {
            blocker.Open(); using var transaction = blocker.BeginTransaction(deferred: false);
            var failed = await store.SaveAsync(new(saved.Definition! with { Model = "must-not-commit" }, saved.RecipeId, saved.Version, "write-locked"), CancellationToken.None);
            Assert.Equal(RecipeSaveStatus.SaveFailed, failed.Status); Assert.Null(failed.Definition);
        }
        Assert.Equal(saved.Version, Assert.Single(store.GetSnapshot().Definitions).Version);
        using (var damaged = new SqliteConnection($"Data Source={options.DatabasePath};Mode=ReadWrite;Pooling=False"))
        {
            damaged.Open(); using var command = damaged.CreateCommand();
            command.CommandText = "DROP INDEX IX_RecipeHead_FCode"; command.ExecuteNonQuery();
        }
        Assert.Throws<InvalidOperationException>(() => store.GetSnapshot());
        Assert.Throws<InvalidOperationException>(() => store.GetCurrentContent(saved.RecipeId!));
    }
}

// Explicit component-only business inputs; never a fixture-code privilege or joint-chain source.
internal static class RecipeAuthoringTestInputs
{
    internal static RecipeDefinition RichCandidate(int faces, bool extraE)
    {
        var recipe = Candidate();
        var original = recipe.ExecutionPositions["slot-one"].PhysicalEntity;
        var stages = Enumerable.Range(1, faces).Select(number => new RecipeStage(number,
            number == 1 ? "none" : "flipAffectedMembersOneByOne", null,
            [new("part", number, number == 2 ? "AB" : "CD", "capture", "inspect")])).ToArray();
        var coordinates = stages.SelectMany(stage => stage.Targets[0].CameraPair.Select(camera => original.Coordinates[0] with
        {
            PointRef = $"detect-{stage.Number}-{camera}", LocalFace = stage.Number, Camera = camera.ToString(),
            StageId = RecipeStageIdentity.Create(stage.Number)
        })).ToArray();
        RecipePurposePoint Point(RecipePointPurpose purpose) => new(purpose, original.Coordinates[0].Point,
            original.Coordinates[0].Fixed, "Component:explicit-purpose") { CaptureProfile = purpose == RecipePointPurpose.EScan ? "capture" : null };
        var inputs = original with { Coordinates = coordinates,
            PurposePoints = new Dictionary<string, RecipePurposePoint>
            { ["pick"] = Point(RecipePointPurpose.FlipPick), ["put"] = Point(RecipePointPurpose.FlipPutBack), ["scan"] = Point(RecipePointPurpose.EScan) },
            Flip = new(stages.Skip(1).ToDictionary(stage => RecipeStageIdentity.Create(stage.Number), stage =>
                new RecipeFlipTransition(new(recipe.MotionProfile, "component-1", "pose-" + stage.Number), "pick", "put"))) };
        return recipe with { Composition = [new("part", Enumerable.Range(1, faces).ToArray())], Stages = stages,
            ExecutionPositions = new Dictionary<string, SlotExecutionInputs> { ["slot-one"] = new("slot-one", inputs, new Dictionary<string, ObjectExecutionInputs>()) },
            ECode = new(extraE, extraE ? "part" : null, extraE ? "physicalEntity" : null, extraE, null)
            { ExtraPose = extraE ? new("separate-mark", new(recipe.MotionProfile, "component-1", "mark-pose"), "scan", "pick", "put", "capture", "ecode") : null },
            AlgorithmRequirements = recipe.AlgorithmRequirements.Concat(new Dictionary<string, AlgorithmRequirement>
            { ["ecode"] = new("ecode", "component-1", AlgorithmPurpose.EntityCode, 1, "entity-code/1") }).ToDictionary(pair => pair.Key, pair => pair.Value) };
    }
    internal static RecipeDefinition Candidate()
    {
        var coordinates = new[] { "A", "B" }.Select(camera => new CoordinateDefinition("detect-" + camera,
            new("point-" + camera, "component-1", 12.5, 23.5, "mm", "component-frame"),
            "{UnitId}/part", "slot-one", 1, 1, camera, RecipeStageIdentity.Create(1), "component-1", "Component:explicit-input",
            new(8.5, "mm", "component-frame", "Component-only", "component-1")) { CaptureProfile = "capture" }).ToArray();
        HandlingPoint Point(string id, double x) => new(new(id, "component-1", x, 40, "mm", "component-frame", 9), "Component:explicit-input");
        var inputs = new ObjectExecutionInputs(Point("source", 30), coordinates, null, null,
            new Dictionary<string, HandlingPoint>(), new Dictionary<string, RecipePurposePoint>())
            {SortingCellIds=new Dictionary<string,string>{["NG"]="r1:c1",["Pending"]="r1:c10"}};
        return new("", "", "draft", "authoring-component", "input-model", " raw-tray ", "independentPart", "part", "layout-reference", 1,
            [new("slot-one", "{TrayRunId}/slot-one", [new("part", "{UnitId}/part", "individual")]) { PhysicalSlotIndex = 1,CellId="r1:c4" }],
            [new("part", [1])], "ordinaryBatch", [new(1, "none", null, [new("part", 1, "AB", "capture", "inspect")])],
            new(false, null, null, false, null), new("originalSlot", "NG", "Pending", "singlePart"), "motion-reference", "quality-reference",
            new Dictionary<string, CaptureProfile> { ["capture"] = new("capture", "component-1", new("capture", 100, 1, [0, 0, 8, 8], "light", 20, 5)) },
            new Dictionary<string, SlotExecutionInputs> { ["slot-one"] = new("slot-one", inputs, new Dictionary<string, ObjectExecutionInputs>()) }, "")
        {
            SchemaVersion = RecipeDefinitionSerialization.CurrentSchema, SortingGripperId = 1, DefinitionDigest = "",
            InspectionKind=RecipeInspectionKind.Ordinary,NgCapacity=1,PendingCapacity=1,
            TrayLayout=new(10,10,[new("r1:c1",1,1,RecipeTrayRegion.NG),new("r1:c4",1,4,RecipeTrayRegion.OK),new("r1:c10",1,10,RecipeTrayRegion.Pending)]),
            TraySlotMapping=new("component-map","component-1","Declared current storage Test input, not historical inference",[new("r1:c4",1)]),
            SortingTargets=new Dictionary<string,HandlingPoint>{["r1:c1"]=Point("ng",50),["r1:c10"]=Point("pending",70)},
            Approval = new("", "", "", "", [], ""),
            AlgorithmRequirements = new Dictionary<string, AlgorithmRequirement>
            {
                ["inspect"] = new("inspect", "component-1", AlgorithmPurpose.SingleDetection, 1, "image-quality/1"),
                ["inspect/fusion"] = new("inspect/fusion", "component-1", AlgorithmPurpose.FaceFusion, 2, "face-quality/1")
            }
        };
    }
}
