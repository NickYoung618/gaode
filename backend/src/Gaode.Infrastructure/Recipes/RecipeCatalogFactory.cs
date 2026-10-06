using Gaode.Application.Recipes;

namespace Gaode.Infrastructure.Recipes;

// Explicit offline input adapters only. Host registration uses SqliteRecipeStore.
public sealed class RecipeCatalogOptions
{
    public string Provider { get; set; } = "";
    public string? CatalogPath { get; set; }
}

public static class RecipeCatalogFactory
{
    public static IRecipeCatalog Create(RecipeCatalogOptions options, string contentRoot)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.CatalogPath))
            throw new InvalidOperationException("RecipeInputPathRequired");
        var path = Path.GetFullPath(options.CatalogPath, contentRoot);
        return options.Provider switch
        {
            "File" => new JsonRecipeCatalog(path),
            "Semantic" => new SemanticRecipeInputProvider(path),
            _ => throw new InvalidOperationException("RecipeInputProviderUnsupported")
        };
    }
}

