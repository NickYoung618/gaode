using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gaode.Application.Ports;
using Gaode.Application.Configuration;
using Gaode.Domain.Configuration;

namespace Gaode.Infrastructure.Configuration;

public sealed class ConfigurationLoader(string configurationRoot, string schemaRoot) : IPublicConfiguration
{
    private readonly object saveGate = new();
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict
    };

    public LoadedConfiguration<PublicConfiguration> LoadPublic(ConfigReference reference) => Load<PublicConfiguration>(reference, "public-config.schema.json");
    public LoadedConfiguration<BusinessBudget> LoadBudget(ConfigReference reference) => Load<BusinessBudget>(reference, "budget.schema.json");
    public LoadedConfiguration<SimulationProfile> LoadSimulation(ConfigReference reference) => Load<SimulationProfile>(reference, "simulation.schema.json");

    public LoadedConfiguration<PublicConfiguration> SavePublicPositions(ConfigReference reference,
        FixedPoint threeD, FixedPoint manualLoading, string expectedDigest)
    {
        lock (saveGate)
        {
            var current = LoadPublic(reference);
            if (current.Digest != expectedDigest)
                throw new ConfigurationException("PublicPositionRevisionConflict", "公共位置已修改，请重新读取");
            var motion = current.Value.Motion;
            bool Valid(FixedPoint point) => point.Unit == motion.Unit && point.Frame == motion.Frame &&
                !string.IsNullOrWhiteSpace(point.Id) && !string.IsNullOrWhiteSpace(point.Version) &&
                double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(point.Z) &&
                point.X >= motion.Limits.XMin && point.X <= motion.Limits.XMax &&
                point.Y >= motion.Limits.YMin && point.Y <= motion.Limits.YMax &&
                point.Z >= motion.Limits.ZMin && point.Z <= motion.Limits.ZMax;
            if (!Valid(threeD) || !Valid(manualLoading) || threeD.Id != motion.Points.ThreeD.Id ||
                motion.Points.Unload is { } existing && manualLoading.Id != existing.Id)
                throw new ConfigurationException("PublicPositionInvalid", "公共位置不符合已确认坐标合同");
            var revision = Guid.NewGuid().ToString("N");
            // Preserve the complete validated document, including absent historical optional fields.
            var updated = JsonNode.Parse(current.CanonicalJson)!.AsObject();
            var updatedMotion = updated["motion"]!.AsObject();
            var points = updatedMotion["points"]!.AsObject();
            points["threeD"] = JsonSerializer.SerializeToNode(threeD with { Version = revision }, Options);
            points["unload"] = JsonSerializer.SerializeToNode(manualLoading with { Version = revision }, Options);
            updatedMotion["coordinateSource"] = "OperatorConfirmedPublicPositions:" + revision;
            updatedMotion["coordinateDigest"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(points.ToJsonString())));
            var json = updated.ToJsonString();
            using var document = JsonDocument.Parse(json);
            using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(schemaRoot, "public-config.schema.json")));
            JsonSchemaSubset.Validate(document.RootElement, schema.RootElement);
            var path = Path.Combine(Path.GetFullPath(configurationRoot), current.SourceFile);
            var temporary = path + "." + revision + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(Encoding.UTF8.GetBytes(json)); stream.Flush(flushToDisk: true); }
            File.Move(temporary, path, true);
            return LoadPublic(reference);
        }
    }

    private LoadedConfiguration<T> Load<T>(ConfigReference reference, string schemaName)
    {
        if (!IsSafeId(reference.Id) || !IsSafeId(reference.Version))
            throw new ConfigurationException("ConfigurationReferenceInvalid", "配置引用只能使用受控ID与版本");
        var root = Path.GetFullPath(configurationRoot);
        var schemaPath = Path.Combine(Path.GetFullPath(schemaRoot), schemaName);
        if (!File.Exists(schemaPath)) throw new ConfigurationException("SchemaMissing", "配置结构文件不存在");
        using var schema = JsonDocument.Parse(File.ReadAllText(schemaPath));
        LoadedConfiguration<T>? found = null;
        foreach (var path in Directory.EnumerateFiles(root, "*.json", SearchOption.TopDirectoryOnly).Take(129))
        {
            if (found is not null && !File.Exists(path)) continue;
            if (new FileInfo(path).Length > 1_048_576)
                throw new ConfigurationException("ConfigurationOversized", "配置文件超过开发限制");
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var value = doc.RootElement;
            if (!value.TryGetProperty("id", out var id) || id.GetString() != reference.Id ||
                !value.TryGetProperty("version", out var version) || version.GetString() != reference.Version)
                continue;
            JsonSchemaSubset.Validate(value, schema.RootElement);
            var parsed = value.Deserialize<T>(Options) ?? throw new ConfigurationException("ConfigurationEmpty", "配置反序列化为空");
            var canonical = Canonical(value);
            var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
            var current = new LoadedConfiguration<T>(parsed, canonical, digest, Path.GetFileName(path));
            if (found is not null && found.Digest != digest)
                throw new ConfigurationException("ConfigurationVersionConflict", "同ID版本存在不同配置内容");
            found = current;
        }
        return found ?? throw new ConfigurationException("ConfigurationNotFound", "未找到指定版本的公共或模拟配置");
    }

    private static bool IsSafeId(string? id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 128 &&
        id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');

    private static string Canonical(JsonElement element)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer)) WriteCanonical(writer, element);
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray()) WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            default: element.WriteTo(writer); break;
        }
    }
}

internal static class JsonSchemaSubset
{
    public static void Validate(JsonElement value, JsonElement schema)
    {
        if (schema.TryGetProperty("anyOf", out var any))
        {
            foreach (var branch in any.EnumerateArray())
            {
                try { Validate(value, branch); return; }
                catch (ConfigurationException) { }
            }
            throw Invalid("anyOf");
        }
        if (schema.TryGetProperty("type", out var type))
        {
            bool valid = type.GetString() switch
            {
                "object" => value.ValueKind == JsonValueKind.Object,
                "array" => value.ValueKind == JsonValueKind.Array,
                "string" => value.ValueKind == JsonValueKind.String,
                "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _),
                "number" => value.ValueKind == JsonValueKind.Number && double.IsFinite(value.GetDouble()),
                "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                "null" => value.ValueKind == JsonValueKind.Null,
                _ => false
            };
            if (!valid) throw Invalid("type");
        }
        if (schema.TryGetProperty("const", out var constant) && !JsonElement.DeepEquals(value, constant))
            throw Invalid("const");
        if (schema.TryGetProperty("enum", out var enumeration) &&
            !enumeration.EnumerateArray().Any(e => JsonElement.DeepEquals(e, value))) throw Invalid("enum");
        if (value.ValueKind == JsonValueKind.Object)
        {
            if (schema.TryGetProperty("required", out var required))
                foreach (var name in required.EnumerateArray())
                    if (!value.TryGetProperty(name.GetString()!, out _)) throw Invalid("required:" + name.GetString());
            if (schema.TryGetProperty("properties", out var properties))
                foreach (var property in value.EnumerateObject())
                {
                    if (properties.TryGetProperty(property.Name, out var member)) Validate(property.Value, member);
                    else if (schema.TryGetProperty("additionalProperties", out var extra) && extra.ValueKind == JsonValueKind.False)
                        throw Invalid("additionalProperties:" + property.Name);
                }
        }
        if (value.ValueKind == JsonValueKind.Array)
        {
            var length = value.GetArrayLength();
            if (schema.TryGetProperty("minItems", out var minItems) && length < minItems.GetInt32()) throw Invalid("minItems");
            if (schema.TryGetProperty("maxItems", out var maxItems) && length > maxItems.GetInt32()) throw Invalid("maxItems");
            if (schema.TryGetProperty("items", out var itemSchema))
                foreach (var item in value.EnumerateArray()) Validate(item, itemSchema);
        }
        if (value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString()!;
            if (schema.TryGetProperty("minLength", out var minLength) && text.Length < minLength.GetInt32()) throw Invalid("minLength");
            if (schema.TryGetProperty("maxLength", out var maxLength) && text.Length > maxLength.GetInt32()) throw Invalid("maxLength");
            if (schema.TryGetProperty("format", out var format) && format.GetString() == "date-time" &&
                !DateTimeOffset.TryParse(text, out _)) throw Invalid("date-time");
        }
        if (value.ValueKind == JsonValueKind.Number)
        {
            var number = value.GetDouble();
            if (schema.TryGetProperty("minimum", out var minimum) && number < minimum.GetDouble()) throw Invalid("minimum");
            if (schema.TryGetProperty("maximum", out var maximum) && number > maximum.GetDouble()) throw Invalid("maximum");
        }
    }
    private static ConfigurationException Invalid(string rule) => new("SchemaInvalid", "配置结构不符合" + rule);
}
