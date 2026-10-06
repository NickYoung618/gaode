using System.Security.Cryptography;
using System.Text.Json;
using Gaode.Application.Recipes;

namespace Gaode.Infrastructure.Recipes;

/// <summary>Explicit current-format input adapter. Never the Host's persisted recipe source.</summary>
public sealed class JsonRecipeCatalog : IRecipeCatalog
{
    private readonly string[] bodies;
    private readonly string digest;

    public JsonRecipeCatalog(string path)
    {
        var bytes = File.ReadAllBytes(path);
        digest = Convert.ToHexString(SHA256.HashData(bytes));
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetString() != RecipeCatalogSnapshot.CurrentSchema)
            throw new InvalidDataException("RecipeCatalogSchemaUnsupported");
        bodies = root.GetProperty("definitions").EnumerateArray()
            .Select(item => RecipeDefinitionSerialization.Serialize(RecipeEnvironmentDecoder.Decode(item))).ToArray();
        // No old-field upgrade, synthetic approval or manufacturing data substitution.
    }

    public RecipeCatalogSnapshot GetSnapshot() => RecipeCatalogSnapshots.Create(digest,
        bodies.Select(RecipeDefinitionSerialization.Deserialize).ToArray());
}
