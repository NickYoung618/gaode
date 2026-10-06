using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Gaode.Domain.Configuration;

namespace Gaode.Application.Recipes;

/// <summary>The single current recipe body format; historical payloads use their own readers.</summary>
public static class RecipeDefinitionSerialization
{
    public const string CurrentSchema = "recipe-definition/5";
    public const string LayoutHistoricalSchema = "recipe-definition/4";
    public const string PreviousSchema = "recipe-definition/3";
    public const string HistoricalSchema = "recipe-definition/2";
    private static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(info =>
        {
            // FixedPoint also serves historical readers where Z was optional. In a
            // current recipe every configured coordinate must be explicitly supplied.
            if (info.Type == typeof(FixedPoint))
                foreach (var property in info.Properties.Where(p => p.Name is "x" or "y" or "z"))
                    property.IsRequired = true;
        });
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = false,
            RespectRequiredConstructorParameters = true,
            RespectNullableAnnotations = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            TypeInfoResolver = resolver
        };
        options.Converters.Add(new PurposeConverter());
        options.Converters.Add(new InspectionKindConverter());
        options.Converters.Add(new TrayRegionConverter());
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        return options;
    }

    public static string Serialize(RecipeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.SchemaVersion is not (CurrentSchema or LayoutHistoricalSchema or PreviousSchema or HistoricalSchema))
            throw new JsonException("RecipeDefinitionSchemaUnsupported");
        if (definition.SchemaVersion == CurrentSchema) definition = RecipeCatalogSnapshots.Freeze(definition);
        return JsonSerializer.Serialize(definition, Options);
    }

    public static RecipeDefinition Deserialize(string json)
    {
        if (JsonNode.Parse(json) is not JsonObject body)
            throw new JsonException("RecipeDefinitionMissing");
        if (body["schemaVersion"] is not JsonValue schema ||
            !schema.TryGetValue<string>(out var version) || version is not (CurrentSchema or LayoutHistoricalSchema or PreviousSchema or HistoricalSchema))
            throw new JsonException("RecipeDefinitionSchemaUnsupported");
        // These are server-produced metadata. A candidate without them is still unsaved.
        foreach (var name in new[] { "recipeId", "version", "definitionDigest", "catalogDigest" })
            if (!body.ContainsKey(name)) body[name] = "";
        return RecipeCatalogSnapshots.Freeze(body.Deserialize<RecipeDefinition>(Options) ?? throw new JsonException("RecipeDefinitionMissing"));
    }

    public sealed class InspectionKindConverter : JsonConverter<RecipeInspectionKind>
    {
        public override RecipeInspectionKind Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.String ? reader.GetString() switch
            {
                "ordinary" => RecipeInspectionKind.Ordinary,
                "specialRotation" => RecipeInspectionKind.SpecialRotation,
                _ => throw new JsonException("RecipeInspectionKindInvalid")
            } : throw new JsonException("RecipeInspectionKindMustBeString");
        public override void Write(Utf8JsonWriter writer, RecipeInspectionKind value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value switch {
                RecipeInspectionKind.Ordinary => "ordinary", RecipeInspectionKind.SpecialRotation => "specialRotation",
                _ => throw new JsonException("RecipeInspectionKindInvalid") });
    }

    public sealed class TrayRegionConverter : JsonConverter<RecipeTrayRegion>
    {
        public override RecipeTrayRegion Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.String ? reader.GetString() switch
            {
                "NG" => RecipeTrayRegion.NG, "OK" => RecipeTrayRegion.OK, "Pending" => RecipeTrayRegion.Pending,
                _ => throw new JsonException("RecipeTrayRegionInvalid")
            } : throw new JsonException("RecipeTrayRegionMustBeString");
        public override void Write(Utf8JsonWriter writer, RecipeTrayRegion value, JsonSerializerOptions options)
        {
            if (!Enum.IsDefined(value)) throw new JsonException("RecipeTrayRegionInvalid");
            writer.WriteStringValue(value.ToString());
        }
    }

    private sealed class PurposeConverter : JsonConverter<RecipePointPurpose>
    {
        public override RecipePointPurpose Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            reader.TokenType == JsonTokenType.String ? reader.GetString() switch
            {
                "FlipPick" => RecipePointPurpose.FlipPick,
                "FlipPutBack" => RecipePointPurpose.FlipPutBack,
                "EScan" => RecipePointPurpose.EScan,
                _ => throw new JsonException("RecipePointPurposeInvalid")
            } : throw new JsonException("RecipePointPurposeMustBeString");

        public override void Write(Utf8JsonWriter writer, RecipePointPurpose value, JsonSerializerOptions options)
        {
            if (!Enum.IsDefined(value)) throw new JsonException("RecipePointPurposeInvalid");
            writer.WriteStringValue(value.ToString());
        }
    }
}
