using System.Text;
using Gaode.Application.Recipes;
using Gaode.Infrastructure.Recipes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Gaode.Communication.Tests;

public sealed class ImportedCommissioningRecipeTests
{
    [Fact]
    public async Task LocalFlipSourceSavesReloadsAndExecutesFormalDetectionChain()
    {
        var source = Path.Combine(ControlledCommissioningTests.Inputs.RepoRoot(),
            "configuration", "commissioning", "single-flip-source.json");
        var candidate = RecipeDefinitionSerialization.Deserialize(File.ReadAllText(source, Encoding.UTF8));
        var root = Path.Combine(Path.GetTempPath(), "gaode-imported-recipe-OFFLINE-" + Guid.NewGuid().ToString("N"));
        var recipes = Path.Combine(root, "recipes");
        RecipeStoreSchema.Prepare(root, recipes);
        var options = new RecipeStoreOptions { DatabasePath=Path.Combine(recipes,"recipes.db"),
            ReadWriteTimeoutMs=10000, DbLockTimeoutSeconds=5 };
        string savedId;
        using(var store = new SqliteRecipeStore(options,root,NullLogger<SqliteRecipeStore>.Instance,()=>"OFFLINE:source-import"))
        {
            var result = await store.SaveAsync(new(candidate,null,null,Guid.NewGuid().ToString()),default);
            Assert.Equal(RecipeSaveStatus.Saved,result.Status);
            savedId = result.Definition!.RecipeId;
        }
        RecipeDefinition saved;
        using(var reader = new SqliteRecipeStore(options,root,NullLogger<SqliteRecipeStore>.Instance,()=>"OFFLINE:fresh-reader"))
            saved = Assert.Single(reader.GetSnapshot().Definitions,d=>d.RecipeId==savedId);
        Assert.Equal(candidate.FCode,saved.FCode);
        Assert.Equal(new CommissioningFPosition("commissioning-f-position/1",65,50),saved.CommissioningFPosition);
        Assert.Equal(new[]{"AB","CD"},saved.Stages.SelectMany(s=>s.Targets).Select(t=>t.CameraPair));
        Assert.Equal("Simulated",saved.LightExecution!.Mode);
        await new CameraBusinessRegressionTests().ExecuteSavedCommissioningRecipe(saved,
            ControlledCommissioningTests.Inputs.EvidencePath("imported-recipe-execution.json"));
    }
}
