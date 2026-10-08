using System.Text.Json;
using Gaode.Application.Recipes;
using Gaode.Communication.Tests;

internal sealed partial class OfflineFixture
{
    private async Task VerifyRecipeEditAsync()
    {
        var a = recipe;
        try
        {
            RecipeDefinition? b = null;
            await new CameraBusinessRegressionTests().ExecuteSavedCommissioningRecipe(a, output + ".run-a.json", async () => {
                File.WriteAllText(Path.Combine(inputs.Root, "recipe-a-frozen.json"), RecipeDefinitionSerialization.Serialize(a));
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
                do {
                    var current = App.Services.GetRequiredService<IRecipeCatalog>().GetSnapshot().Definitions.Single(x => x.RecipeId == a.RecipeId);
                    if (current.Version != a.Version) { b = current; break; }
                    await Task.Delay(50, deadline.Token);
                } while (true);
            });
            if (b is null) throw new InvalidOperationException("PageDidNotSaveVersionB");
            await new CameraBusinessRegressionTests().ExecuteSavedCommissioningRecipe(b, output + ".run-b.json");
            File.WriteAllText(output + ".recipe-edit.json", JsonSerializer.Serialize(new {
                scope = "OFFLINE:page-saved-version-after-A-freeze;formal-executor-declared-ports;not-full-machine",
                state = "Passed", before = JsonDocument.Parse(RecipeDefinitionSerialization.Serialize(a)).RootElement.Clone(),
                after = JsonDocument.Parse(RecipeDefinitionSerialization.Serialize(b)).RootElement.Clone()
            }, Json));
        }
        catch (Exception error)
        { File.WriteAllText(output + ".recipe-edit.json", JsonSerializer.Serialize(new { state = "Failed", errorType = error.GetType().Name, error = error.Message }, Json)); }
    }
}
