using Gaode.Application.Recipes;
using Xunit;

namespace Gaode.Contracts.Tests.Recipes;

public sealed class RecipeCommonFoundationTests
{
    [Fact]
    public void SaveAllowsUnapprovedDraftButEnforcesUniqueRawTrayCode()
    {
        var draft = Recipe011Data.Candidate() with { Approval = new("", "", "", "Production", [], "") };
        Assert.True(RecipeDefinitionValidator.ValidateForSave(draft, Snapshot()).Valid);
        var saved = draft with { RecipeId = "other", ScenarioId = "other-scenario" };
        var result = RecipeDefinitionValidator.ValidateForSave(draft, Snapshot(saved));
        Assert.Contains(result.Issues, i => i.Code == "RecipeFCodeOccupied" && i.FieldPath == "fCode");
        Assert.True(RecipeDefinitionValidator.ValidateForSave(draft with { FCode = "tray-raw " }, Snapshot(saved)).Valid);
    }

    [Fact]
    public void FourFaceCameraRuleDoesNotLimitMoreFaces()
    {
        var recipe = Recipe011Data.Candidate();
        var reversed = recipe with { Stages = recipe.Stages.Select(s => s with {
            Targets = s.Targets.Select(t => t with { CameraPair = t.CameraPair == "AB" ? "CD" : "AB" }).ToArray() }).ToArray() };
        Assert.Contains(RecipeDefinitionValidator.ValidateForSave(reversed, Snapshot()).Issues,
            i => i.Code == "FourFaceRequiresOneABThreeCD");
        Assert.True(RecipeDefinitionValidator.ValidateForSave(Recipe011Data.Candidate(6, false), Snapshot()).Valid);
    }

    [Fact]
    public void ExtraPoseAndFlipRequireTheirOwnPurposeReferences()
    {
        var recipe = Recipe011Data.Candidate();
        recipe = recipe with { ECode = recipe.ECode with { ExtraPose = recipe.ECode.ExtraPose! with { ScanPointRef = "pick" } } };
        Assert.Contains(RecipeDefinitionValidator.ValidateForSave(recipe, Snapshot()).Issues,
            i => i.Code == "EntityCodePointInvalid");
        Assert.False(RecipeDefinitionValidator.ValidateForSave(Recipe011Data.Candidate(6, true), Snapshot()).Valid);
    }

    [Fact]
    public void DigestPreservesBusinessContentAndIgnoresCommitMetadata()
    {
        var recipe = Recipe011Data.Candidate() with { RecipeId = "id-1" };
        var digest = RecipeDefinitionIdentity.ComputeDefinitionDigest(recipe);
        Assert.Equal(digest, RecipeDefinitionIdentity.ComputeDefinitionDigest(recipe with {
            Version = "new-commit", CatalogDigest = "new-catalog", DefinitionDigest = "old-digest",
            AlgorithmRequirements = recipe.AlgorithmRequirements.Reverse().ToDictionary(p => p.Key, p => p.Value) }));
        Assert.NotEqual(digest, RecipeDefinitionIdentity.ComputeDefinitionDigest(recipe with { Model = "different-model" }));
        Assert.NotEqual(digest, RecipeDefinitionIdentity.ComputeDefinitionDigest(recipe with { RecipeId = "id-2" }));
        Assert.NotEqual(digest, RecipeDefinitionIdentity.ComputeDefinitionDigest(recipe with { FCode = recipe.FCode + " " }));
    }

    [Fact]
    public void MatchingUsesExactGlobalCodeAndDoesNotLockObservedVersion()
    {
        var recipe = Saved();
        var snapshot = Snapshot(recipe);
        Assert.Equal(RecipeMatchStatus.Unmatched, RecipeMatcher.Match(snapshot, recipe.FCode + " ", null, recipe.ScenarioId, "Test").Status);
        var matched = RecipeMatcher.Match(snapshot, recipe.FCode, new(recipe.RecipeId, "old", "old"), recipe.ScenarioId, "Test");
        Assert.Equal(RecipeMatchStatus.Matched, matched.Status);
        Assert.Equal(recipe.Version, matched.Version);
        Assert.Equal(RecipeMatchStatus.IdentityMismatch, RecipeMatcher.Match(snapshot, recipe.FCode,
            new("different-id"), recipe.ScenarioId, "Test").Status);
        Assert.Equal(RecipeMatchStatus.Ambiguous, RecipeMatcher.Match(Snapshot(recipe, recipe with {
            RecipeId = "duplicate", ScenarioId = "elsewhere" }), recipe.FCode, null, recipe.ScenarioId, "Test").Status);
    }

    [Fact]
    public void SnapshotOwnsDeepCollectionsAndRejectsCallerMutation()
    {
        var recipe = Saved();
        var snapshot = RecipeCatalogSnapshots.Create("catalog-1", [recipe]);
        ((RecipeStage[])recipe.Stages)[0] = recipe.Stages[0] with { Action = "changed" };
        recipe.CaptureProfiles["detect"].Settings.RoiPixels[2] = 99;
        Assert.Equal("none", snapshot.Definitions[0].Stages[0].Action);
        Assert.Equal(2, snapshot.Definitions[0].CaptureProfiles["detect"].Settings.RoiPixels[2]);
        Assert.Throws<NotSupportedException>(() => ((IList<RecipeDefinition>)snapshot.Definitions).Clear());
        Assert.Throws<NotSupportedException>(() => ((IDictionary<string, SlotExecutionInputs>)snapshot.Definitions[0].ExecutionPositions).Clear());
        var exposed = snapshot.Definitions[0].CaptureProfiles["detect"].Settings.RoiPixels;
        exposed[2] = 88;
        Assert.Equal(2, snapshot.Definitions[0].CaptureProfiles["detect"].Settings.RoiPixels[2]);
    }

    private static RecipeDefinition Saved()
    {
        var recipe = Recipe011Data.Candidate() with { RecipeId = RecipeDefinitionIdentity.CreateRecipeId(), Version = RecipeDefinitionIdentity.CreateVersion() };
        return recipe with { DefinitionDigest = RecipeDefinitionIdentity.ComputeDefinitionDigest(recipe) };
    }
    private static RecipeCatalogSnapshot Snapshot(params RecipeDefinition[] definitions) =>
        new(RecipeCatalogSnapshot.CurrentSchema, "snapshot-current", definitions);
}
