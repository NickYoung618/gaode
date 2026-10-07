using System.Text.Json;
using Gaode.Application.Recipes;

namespace Gaode.Infrastructure.Recipes;

/// <summary>Format boundary only; current bodies use the unique common serializer.</summary>
public static class RecipeEnvironmentDecoder
{
    public static RecipeDefinition Decode(JsonElement definition) =>
        RecipeDefinitionSerialization.Deserialize(definition.GetRawText());

    // F's decoded content is already the tray number. No fixture aliases or code maps.
    public static DecodedTrayCode? DecodeTrayCode(string raw) =>
        string.IsNullOrWhiteSpace(raw) ? null : new(raw, raw, "tray-code/1", "F:decoded-content");
}
