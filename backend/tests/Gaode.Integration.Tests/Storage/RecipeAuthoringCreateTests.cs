using Gaode.Infrastructure.Recipes;
using Microsoft.Data.Sqlite;
using Xunit;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Gaode.Application.Recipes;
using Gaode.Integration.Tests.Support;

namespace Gaode.Integration.Tests.Storage;

public sealed class RecipeAuthoringCreateTests
{
    [Fact]
    public async Task ManualSelectionOrderKeepsOnlyTheSourcesExplicitTargetsAndClearsChangedCellLocally()
    {
        await using var host=await Station01HostFixture.CreateAsync();using var author=host.ClientForRole("ProcessEngineer");
        var source=RecipeAuthoringTestInputs.Candidate();
        using var save=await author.PostAsJsonAsync("/api/v1/recipes",new JsonObject{["requestId"]="manual-source",["definition"]=JsonNode.Parse(RecipeDefinitionSerialization.Serialize(source))});
        Assert.Equal(HttpStatusCode.Created,save.StatusCode);
        var saved=JsonNode.Parse(await save.Content.ReadAsStringAsync())!["definition"]!;
        using var draft=await author.PostAsJsonAsync("/api/v1/recipes/editor-draft",new{source.Model,source.ScenarioId,source.UnitKind,InspectionKind="ordinary",SourceRecipeId=saved["recipeId"]!.GetValue<string>()});
        Assert.Equal(HttpStatusCode.OK,draft.StatusCode);var body=JsonNode.Parse(await draft.Content.ReadAsStringAsync())!["definition"]!;
        var selected=new List<RecipeTrayCell>();
        foreach(var cell in new[]{new RecipeTrayCell("r1:c10",1,10,RecipeTrayRegion.Pending),new("r1:c4",1,4,RecipeTrayRegion.OK),new("r1:c1",1,1,RecipeTrayRegion.NG)})
        {
            selected.Add(cell);
            using var map=await author.PostAsJsonAsync("/api/v1/recipes/editor-layout",new{definition=body,sourceRecipeId=saved["recipeId"]!.GetValue<string>(),sourceVersion=saved["version"]!.GetValue<string>(),trayLayout=new RecipeTrayLayout(10,10,selected.ToArray())});
            Assert.Equal(HttpStatusCode.OK,map.StatusCode);body=JsonNode.Parse(await map.Content.ReadAsStringAsync())!["definition"]!;
        }
        var slot=body["positions"]![0]!["slotId"]!.GetValue<string>();var objectInput=body["executionPositions"]![slot]!["physicalEntity"]!;
        Assert.Equal("r1:c1",objectInput["sortingCellIds"]!["NG"]!.GetValue<string>());
        Assert.Equal("r1:c10",objectInput["sortingCellIds"]!["Pending"]!.GetValue<string>());
        objectInput["coordinates"]![0]!["point"]!["x"]=123.5;
        using var changed=await author.PostAsJsonAsync("/api/v1/recipes/editor-layout",new{definition=body,sourceRecipeId=saved["recipeId"]!.GetValue<string>(),sourceVersion=saved["version"]!.GetValue<string>(),trayLayout=new RecipeTrayLayout(10,10,selected.Select(c=>c.CellId=="r1:c1"?c with{Region=RecipeTrayRegion.Pending}:c).ToArray())});
        Assert.Equal(HttpStatusCode.OK,changed.StatusCode);var after=JsonNode.Parse(await changed.Content.ReadAsStringAsync())!["definition"]!;
        Assert.Equal(123.5,after["executionPositions"]![slot]!["physicalEntity"]!["coordinates"]![0]!["point"]!["x"]!.GetValue<double>());
        Assert.Null(after["sortingTargets"]!["r1:c1"]!["point"]!["x"]);
        Assert.Equal(slot,after["positions"]![0]!["slotId"]!.GetValue<string>());
    }

    [Fact]
    public async Task EditorDraftKeepsHiddenConfigurationButBlanksEachNewPhotoWithoutChangingStoredSource()
    {
        await using var host = await Station01HostFixture.CreateAsync();
        using var author = host.ClientForRole("ProcessEngineer");
        var source = RecipePerCaptureTests.PerCapture(RecipeAuthoringTestInputs.RichCandidate(4, true));
        var workspace = new DirectoryInfo(AppContext.BaseDirectory);
        while (workspace is not null && !File.Exists(Path.Combine(workspace.FullName, "global.json"))) workspace = workspace.Parent;
        var evidence = Path.Combine(workspace!.FullName, "artifacts", "recipe-ui-fix-012");
        Directory.CreateDirectory(evidence);
        await File.WriteAllTextAsync(Path.Combine(evidence, "declared-api-input.json"), RecipeDefinitionSerialization.Serialize(source));
        using var created = await author.PostAsJsonAsync("/api/v1/recipes", new JsonObject
            { ["requestId"] = "editor-source", ["definition"] = JsonNode.Parse(RecipeDefinitionSerialization.Serialize(source)) });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var saved = JsonNode.Parse(await created.Content.ReadAsStringAsync())!["definition"]!;
        using var draft = await author.PostAsJsonAsync("/api/v1/recipes/editor-draft", new { source.Model, source.UnitKind,source.ScenarioId,InspectionKind="ordinary",SourceRecipeId=saved["recipeId"]!.GetValue<string>() });
        Assert.Equal(HttpStatusCode.OK, draft.StatusCode);
        var body = JsonNode.Parse(await draft.Content.ReadAsStringAsync())!["definition"]!;
        Assert.Null(body["sortingGripperId"]);
        Assert.Empty(body["positions"]!.AsArray());
        using var layout=await author.PostAsJsonAsync("/api/v1/recipes/editor-layout",new JsonObject{
            ["definition"]=body.DeepClone(),["sourceRecipeId"]=saved["recipeId"]!.DeepClone(),["sourceVersion"]=saved["version"]!.DeepClone(),
            ["trayLayout"]=saved["trayLayout"]!.DeepClone()});
        Assert.Equal(HttpStatusCode.OK,layout.StatusCode);
        body=JsonNode.Parse(await layout.Content.ReadAsStringAsync())!["definition"]!;
        var slotId=body["positions"]![0]!["slotId"]!.GetValue<string>();
        var input = body["executionPositions"]![slotId]!["physicalEntity"]!;
        var points = input["coordinates"]!.AsArray().Concat(new[] { input["purposePoints"]!["scan"] }).ToArray();
        Assert.Equal(9, points.Select(p => p!["captureProfile"]!.GetValue<string>()).Distinct().Count());
        foreach (var point in points)
        {
            var settings = body["captureProfiles"]![point!["captureProfile"]!.GetValue<string>()]!["settings"]!;
            Assert.Null(settings["exposureUs"]); Assert.Null(settings["gain"]); Assert.Null(settings["brightnessPercent"]);
            Assert.Equal("light", settings["lightChannel"]!.GetValue<string>());
            Assert.Null(point["point"]!["x"]);
        }
        using var reread = await author.GetAsync("/api/v1/recipes/" + saved["recipeId"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(saved, JsonNode.Parse(await reread.Content.ReadAsStringAsync())!["definition"]));
    }
    [Fact]
    public async Task GroupDraftScopesActualMembersAndFaceChangesWithoutBlankingUnusedBackground()
    {
        await using var host = await Station01HostFixture.CreateAsync();
        using var author = host.ClientForRole("ProcessEngineer");
        var basis = RecipeAuthoringTestInputs.RichCandidate(2, false);
        var input = basis.ExecutionPositions["slot-one"].PhysicalEntity;
        var second = input with { Coordinates = input.Coordinates.Select(c => c with { ObjectPattern = "{UnitId}/second" }).ToArray() };
        var group = basis with
        {
            UnitKind = "looseGroup", FCode = "group-editor", Composition = [new("part", [1, 2]), new("second", [1, 2])],
            Positions = [basis.Positions[0] with { Members = [new("part", "{UnitId}/part", "individual"), new("second", "{UnitId}/second", "individual")] }],
            Stages = basis.Stages.Select(s => s with { Targets = s.Targets.Concat(s.Targets.Select(t => t with { Material = "second" })).ToArray() }).ToArray(),
            Disposition = basis.Disposition with { PhysicalUnit = "allGroupMembersIndividuallyToSameReservedGroupCell" },
            ExecutionPositions = new Dictionary<string, SlotExecutionInputs>
                { ["slot-one"] = new("slot-one", input, new Dictionary<string, ObjectExecutionInputs> { ["part"] = input, ["second"] = second }) }
        };
        group = RecipePerCaptureTests.PerCapture(group);
        using var saved = await author.PostAsJsonAsync("/api/v1/recipes", new JsonObject
            { ["requestId"] = "group-source", ["definition"] = JsonNode.Parse(RecipeDefinitionSerialization.Serialize(group)) });
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        var persisted = RecipeDefinitionSerialization.Deserialize(JsonNode.Parse(await saved.Content.ReadAsStringAsync())!["definition"]!.ToJsonString());
        var frozen = RecipeRunPlanner.BuildExecutable(persisted with { Approval = new("component", "1", "component-only", "Test", ["slot-one"], "DeclaredComponentOnly") },
            Guid.NewGuid().ToString("D"), ["slot-one"]);
        var captures = frozen.Steps.Where(s => s.Kind == RecipeStepKind.Capture).ToArray();
        Assert.Equal(8, captures.Length); Assert.Equal(8, captures.Select(c => c.CaptureProfile).Distinct().Count());
        using var draft = await author.PostAsJsonAsync("/api/v1/recipes/editor-draft", new { group.Model, group.UnitKind,group.ScenarioId,InspectionKind="ordinary",SourceRecipeId=persisted.RecipeId });
        Assert.Equal(HttpStatusCode.OK, draft.StatusCode);
        var body = JsonNode.Parse(await draft.Content.ReadAsStringAsync())!["definition"]!;
        Assert.Empty(body["positions"]!.AsArray());
        using var layout=await author.PostAsJsonAsync("/api/v1/recipes/editor-layout",new JsonObject{
            ["definition"]=body.DeepClone(),["sourceRecipeId"]=persisted.RecipeId,["sourceVersion"]=persisted.Version,
            ["trayLayout"]=JsonNode.Parse(RecipeDefinitionSerialization.Serialize(persisted))!["trayLayout"]!.DeepClone()});
        Assert.Equal(HttpStatusCode.OK,layout.StatusCode);
        body=JsonNode.Parse(await layout.Content.ReadAsStringAsync())!["definition"]!;
        var slotId=body["positions"]![0]!["slotId"]!.GetValue<string>();
        var slot = body["executionPositions"]![slotId]!;
        Assert.NotNull(slot["physicalEntity"]!["coordinates"]![0]!["point"]!["x"]);
        Assert.Null(slot["members"]!["part"]!["coordinates"]![0]!["point"]!["x"]);
        Assert.Null(slot["members"]!["second"]!["coordinates"]![0]!["point"]!["x"]);
        using var changed = await author.PostAsJsonAsync("/api/v1/recipes/editor-layout", new JsonObject
            { ["definition"] = body.DeepClone(), ["sourceRecipeId"]=persisted.RecipeId,["sourceVersion"]=persisted.Version, ["material"] = "part", ["faces"] = 1 });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var actual = JsonNode.Parse(await changed.Content.ReadAsStringAsync())!["definition"]!["executionPositions"]![slotId]!["members"]!;
        Assert.Equal(2, actual["part"]!["coordinates"]!.AsArray().Count);
        Assert.Equal(4, actual["second"]!["coordinates"]!.AsArray().Count);
    }
    [Fact]
    public async Task RealHttpSaveReturnsCompleteBodyAndPreservesWriteAndCommonValidationGates()
    {
        // Shared fixture must consume the delivered real recipe-store preparation/configuration.
        // It does not replace IRecipeStore/IRecipeCatalog or return preconfigured save responses.
        await using var host = await Station01HostFixture.CreateAsync();
        using var author = host.ClientForRole("ProcessEngineer");
        var candidate = RecipeAuthoringTestInputs.Candidate();
        static JsonObject Request(RecipeDefinition definition, string id) => new()
        {
            ["requestId"] = id, ["definition"] = JsonNode.Parse(RecipeDefinitionSerialization.Serialize(definition))
        };
        using var created = await author.PostAsJsonAsync("/api/v1/recipes", Request(candidate, "api-new"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var envelope = JsonNode.Parse(await created.Content.ReadAsStringAsync())!;
        var saved = RecipeDefinitionSerialization.Deserialize(envelope["definition"]!.ToJsonString());
        using var read = await author.GetAsync("/api/v1/recipes/" + saved.RecipeId);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(created.Headers.ETag, read.Headers.ETag);
        var actual = JsonNode.Parse(await read.Content.ReadAsStringAsync())!;
        Assert.Equal(RecipeDefinitionSerialization.Serialize(saved), RecipeDefinitionSerialization.Serialize(
            RecipeDefinitionSerialization.Deserialize(actual["definition"]!.ToJsonString())));
        Assert.False(RecipeAdmission.Evaluate(saved, ["slot-one"], "Production").Eligible);
        using var duplicate = await author.PostAsJsonAsync("/api/v1/recipes", Request(candidate, "api-duplicate"));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var denied = await host.Client.PostAsJsonAsync("/api/v1/recipes", Request(candidate, "api-operator"));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var invalid = RecipeAuthoringTestInputs.RichCandidate(4, true);
        invalid = invalid with { FCode = "invalid-four-face-only", Stages = invalid.Stages.Select(s => s with
            { Targets = s.Targets.Select(t => t with { CameraPair = "AB" }).ToArray() }).ToArray() };
        using var rejected = await author.PostAsJsonAsync("/api/v1/recipes", Request(invalid, "api-invalid-four-face"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
        Assert.Contains("FourFaceRequiresOneABThreeCD", await rejected.Content.ReadAsStringAsync());
    }

    [Fact]
    public void RecipeSchemaIsPreparedSeparatelyAndNeverOverwritesAnExistingStore()
    {
        var (allowed, root) = RecipeAuthoringTestRoots.New();
        RecipeStoreSchema.Prepare(allowed, root);
        RecipeStoreSchema.Inspect(allowed, root);
        var database = Path.Combine(root, "recipes.db");
        var before = File.ReadAllBytes(database);
        Assert.Throws<InvalidOperationException>(() => RecipeStoreSchema.Prepare(allowed, root));
        Assert.Equal(before, File.ReadAllBytes(database));
        Assert.False(File.Exists(Path.Combine(root, "station01.test.db")));
    }

    [Fact]
    public void MissingOrChangedRecipeSchemaIsRejectedWithoutCreatingOrRepairingIt()
    {
        var (allowed, root) = RecipeAuthoringTestRoots.New();
        Assert.Throws<InvalidOperationException>(() => RecipeStoreSchema.Inspect(allowed, root));
        Assert.False(Directory.Exists(root));
        RecipeStoreSchema.Prepare(allowed, root);
        using (var connection = new SqliteConnection($"Data Source={Path.Combine(root, "recipes.db")};Mode=ReadWrite;Pooling=False"))
        {
            connection.Open(); using var command = connection.CreateCommand();
            command.CommandText = "DROP INDEX IX_RecipeHead_FCode"; command.ExecuteNonQuery();
        }
        Assert.Throws<InvalidOperationException>(() => RecipeStoreSchema.Inspect(allowed, root));
    }
}

internal static class RecipeAuthoringTestRoots
{
    internal static (string Allowed, string Root) New()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json"))) directory = directory.Parent;
        if (directory is null) throw new InvalidOperationException("Independent workspace is required");
        var allowed = Path.Combine(directory.FullName, "artifacts", "recipe-authoring-012", "test-stores");
        return (allowed, Path.Combine(allowed, Guid.NewGuid().ToString("N")));
    }
}
