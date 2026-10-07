using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Gaode.Application.Recipes;

public static class RecipeDefinitionIdentity
{
    public static string CreateRecipeId() => Guid.NewGuid().ToString("N");
    public static string CreateVersion() => Guid.NewGuid().ToString("N");

    public static string ComputeDefinitionDigest(RecipeDefinition definition)
    {
        var body = JsonNode.Parse(RecipeDefinitionSerialization.Serialize(definition))!.AsObject();
        foreach (var name in new[] { "version", "definitionDigest", "catalogDigest" }) body.Remove(name);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer)) WriteCanonical(writer, body);
        return Convert.ToHexString(SHA256.HashData(buffer.ToArray()));
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                writer.WriteStartObject();
                foreach (var property in obj.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Key);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonArray array:
                writer.WriteStartArray();
                foreach (var item in array) WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            case null: writer.WriteNullValue(); break;
            default: node.WriteTo(writer); break;
        }
    }
}
