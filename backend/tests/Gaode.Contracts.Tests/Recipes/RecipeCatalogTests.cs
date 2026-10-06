using System.Text.Json;
using System.Text.Json.Nodes;
using Gaode.Application.Recipes;
using Gaode.Infrastructure.Recipes;
using Gaode.Contracts.Tests.Support;
using Xunit;

namespace Gaode.Contracts.Tests.Recipes;

// Explicit input-adapter contracts. Formal Host recipes come only from SqliteRecipeStore.
// Recipe011Data is component input, never a production or joint-chain substitute.
public sealed class RecipeCatalogTests
{
    [Fact]
    public void ExplicitCurrentInputPreservesEveryFieldAndCommonMatchingUsesExactF()
    {
        var saved = Saved();
        var path = Write(saved);
        var adapter = RecipeCatalogFactory.Create(new() { Provider = "File", CatalogPath = path }, TestConfiguration.Workspace());
        var snapshot = adapter.GetSnapshot();
        Assert.Equal(RecipeCatalogSnapshot.CurrentSchema, snapshot.SchemaVersion);
        Assert.Equal(RecipeDefinitionSerialization.Serialize(saved),
            RecipeDefinitionSerialization.Serialize(Assert.Single(snapshot.Definitions)));
        var match = RecipeMatcher.Match(snapshot, saved.FCode, new(saved.RecipeId, "old-observed-version", "old-source"), saved.ScenarioId, "Test");
        Assert.Equal(RecipeMatchStatus.Matched, match.Status);
        Assert.Equal(saved.Version, match.Version);
        Assert.Equal(RecipeMatchStatus.Unmatched,
            RecipeMatcher.Match(snapshot, saved.FCode + " ", null, saved.ScenarioId, "Test").Status);
        Assert.Null(saved.PlcRecipeId); // No automatic PLC-number assignment or product-name branch.
    }

    [Theory]
    [InlineData("missing-camera", "ProductTargetMissingOrDuplicate")]
    [InlineData("wrong-slot", "ProductTargetIdentityMismatch")]
    [InlineData("cross-stage", "ProductTargetMissingOrDuplicate")]
    public void ImportedCurrentBodyCannotBypassCommonPointAssociation(string change, string expected)
    {
        var saved = Saved();
        var slot = saved.ExecutionPositions["s1"];
        var coordinates = slot.PhysicalEntity.Coordinates.ToArray();
        coordinates = change switch
        {
            "missing-camera" => coordinates.Skip(1).ToArray(),
            "wrong-slot" => coordinates.Select((c, i) => i == 0 ? c with { SlotId = "unrelated" } : c).ToArray(),
            _ => coordinates.Select((c, i) => i == 0 ? c with { StageId = RecipeStageIdentity.Create(2) } : c).ToArray()
        };
        var invalid = saved with { ExecutionPositions = new Dictionary<string, SlotExecutionInputs>
        {
            ["s1"] = slot with { PhysicalEntity = slot.PhysicalEntity with { Coordinates = coordinates } }
        } };
        var snapshot = new JsonRecipeCatalog(Write(invalid)).GetSnapshot();
        var result = RecipeDefinitionValidator.ValidateForSave(Assert.Single(snapshot.Definitions), snapshot);
        Assert.False(result.Valid);
        Assert.Contains(result.Issues, issue => issue.Code == expected);
    }

    [Fact]
    public void ConfigurationDifferencesRemainVisibleAndUnapprovedSlotsStayRestricted()
    {
        var first = Saved();
        var changed = first with { CaptureProfiles = first.CaptureProfiles.ToDictionary(p => p.Key,
            p => p.Value with { Settings = p.Value.Settings with { ExposureUs = 125 } }) };
        changed = changed with { DefinitionDigest = RecipeDefinitionIdentity.ComputeDefinitionDigest(changed) };
        var a = new JsonRecipeCatalog(Write(first)).GetSnapshot();
        var b = new JsonRecipeCatalog(Write(changed)).GetSnapshot();
        Assert.NotEqual(a.CatalogDigest, b.CatalogDigest);
        Assert.NotEqual(a.Definitions[0].DefinitionDigest, b.Definitions[0].DefinitionDigest);
        Assert.Equal(125, b.Definitions[0].CaptureProfiles["detect"].Settings.ExposureUs);
        Assert.Equal("RecipeSlotNotApproved", RecipeAdmission.Evaluate(b.Definitions[0], ["unapproved-slot"], "Test").Reason);
        var noApproval = changed with { Approval = new("", "", "", "", [], "") };
        Assert.True(RecipeDefinitionValidator.ValidateForSave(noApproval, RecipeCatalogSnapshots.Create("", [])).Valid);
        Assert.False(RecipeAdmission.Evaluate(noApproval, ["s1"], "Test").Eligible);
    }

    [Fact]
    public void HistoricalSourceRemainsReadableButCannotBecomeAnActiveCurrentDirectory()
    {
        var path = Path.Combine(TestConfiguration.Workspace(), "specs", "008-recipe-driven-inspection", "fixtures", "recipes-q07.json");
        var bytes = File.ReadAllBytes(path);
        using var archived = JsonDocument.Parse(bytes);
        Assert.True(archived.RootElement.TryGetProperty("recipes", out _));
        Assert.Throws<InvalidDataException>(() => new JsonRecipeCatalog(path));
        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Throws<InvalidOperationException>(() => RecipeCatalogFactory.Create(new(), TestConfiguration.Workspace()));
        Assert.Throws<InvalidOperationException>(() => RecipeCatalogFactory.Create(
            new() { Provider = "Review", CatalogPath = path }, TestConfiguration.Workspace()));
        // Versioned run/history readers remain 011-owned; this test does not assert a new execution route.
    }

    private static RecipeDefinition Saved()
    {
        var recipe = Recipe011Data.Candidate(1, false) with
        {
            RecipeId = RecipeDefinitionIdentity.CreateRecipeId(), Version = RecipeDefinitionIdentity.CreateVersion()
        };
        return recipe with { DefinitionDigest = RecipeDefinitionIdentity.ComputeDefinitionDigest(recipe) };
    }

    private static string Write(RecipeDefinition recipe)
    {
        var root = Path.Combine(TestConfiguration.Workspace(), "artifacts", "recipe-authoring-012", "component-inputs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var body = new JsonObject
        {
            ["schemaVersion"] = RecipeCatalogSnapshot.CurrentSchema,
            ["definitions"] = new JsonArray(JsonNode.Parse(RecipeDefinitionSerialization.Serialize(recipe)))
        };
        var path = Path.Combine(root, "current-input.json");
        File.WriteAllText(path, body.ToJsonString());
        return path;
    }
}

