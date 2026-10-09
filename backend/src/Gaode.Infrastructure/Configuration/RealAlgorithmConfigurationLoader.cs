using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Configuration;
using Gaode.Application.Ports;
using Gaode.Domain.Configuration;

namespace Gaode.Infrastructure.Configuration;

public sealed record AlgorithmComponentCheck(string Path, string Version, string Sha256, string State);
public sealed record RealAlgorithmLoad(LoadedConfiguration<RealAlgorithmConfiguration> Configuration,
    IReadOnlyList<AlgorithmComponentCheck> Components)
{
    public bool IsReady => false; // Protocol/provider/model application have not been delivered or integrated.
    public string Readiness => Components.Any(x => x.State == "ComponentMissing") ? "ComponentMissing" : "NotIntegrated";
}

public static class RealAlgorithmConfigurationLoader
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
      NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict };
    public static RealAlgorithmLoad Load(string path, string digest, ConfigReference commissioning,
        ConfigReference publicRef, ConfigReference budgetRef)
    {
        if (!System.IO.Path.IsPathFullyQualified(path) || !IsDigest(digest) || !File.Exists(path))
            throw new ConfigurationException("AlgorithmConfigurationInvalid", "独立算法配置路径或摘要无效/文件缺失");
        CheckNoLinks(path);
        if (new FileInfo(path).Length > 1_048_576) throw new ConfigurationException("AlgorithmConfigurationInvalid", "独立算法配置超限");
        var bytes = File.ReadAllBytes(path);
        var actual = Convert.ToHexString(SHA256.HashData(bytes));
        if (!StringComparer.OrdinalIgnoreCase.Equals(digest, actual)) throw new ConfigurationException("AlgorithmConfigurationInvalid", "独立算法配置摘要不符");
        var json = new UTF8Encoding(false, true).GetString(bytes);
        using var document = JsonDocument.Parse(json);
        RejectDuplicates(document.RootElement);
        using var schemaStream = typeof(RealAlgorithmConfigurationLoader).Assembly.GetManifestResourceStream("Gaode.RealAlgorithmHostSchema")
            ?? throw new ConfigurationException("SchemaMissing", "独立算法结构契约未随源码装配");
        using var schema = JsonDocument.Parse(schemaStream);
        JsonSchemaSubset.Validate(document.RootElement, schema.RootElement);
        var value = document.RootElement.Deserialize<RealAlgorithmConfiguration>(Json)
            ?? throw new ConfigurationException("AlgorithmConfigurationInvalid", "独立算法配置为空");
        if (value.Purpose != RuntimePurposes.RealDeviceCommissioning || value.CommissioningRef != commissioning ||
            value.PublicRef != publicRef || value.BudgetRef != budgetRef || value.CodeRule.Id != "decoded-content-exact" ||
            value.CodeRule.Version != "1.0" || value.Modules.Count == 0 ||
            value.Modules.Select(x => x.Module).Distinct().Count() != value.Modules.Count ||
            value.Modules.Select(x => x.BindingId).Distinct().Count() != value.Modules.Count ||
            value.Modules.Any(x => x.InputCount != (x.Module == "DefectFusion" ? 2 : 1)))
            throw new ConfigurationException("AlgorithmConfigurationInvalid", "独立算法用途、引用、模块或输入数量不符");
        var references = value.Provider.Dependencies.Prepend(value.Provider.Artifact)
            .Concat(value.Modules.SelectMany(x => x.ModelFiles.Append(x.ParametersFile)))
            .Concat(new[] { value.LayoutFile, value.CalibrationFile }.OfType<AlgorithmFileReference>());
        var components = references.Select(reference =>
        {
            if (!IsDigest(reference.Sha256)) throw new ConfigurationException("AlgorithmConfigurationInvalid", "组件摘要格式非法");
            var full = System.IO.Path.GetFullPath(reference.Path, System.IO.Path.GetDirectoryName(path)!);
            CheckNoLinks(full);
            // Fix the absolute identity, read only. Missing delivery never becomes readiness.
            var state = "ComponentMissing";
            if (File.Exists(full))
            {
                using var input = File.OpenRead(full);
                if (!StringComparer.OrdinalIgnoreCase.Equals(Convert.ToHexString(SHA256.HashData(input)), reference.Sha256))
                    throw new ConfigurationException("AlgorithmConfigurationInvalid", "已交付文件摘要与声明不符");
                state = "FileVerified;ModelApplicationUnconfirmed";
            }
            return new AlgorithmComponentCheck(full, reference.Version, reference.Sha256, state);
        }).ToArray();
        return new(new(value, json, actual, System.IO.Path.GetFullPath(path)), components);
    }
    public static bool IsDigest(string text) => text is { Length: 64 } && text.All(Uri.IsHexDigit);
    private static void CheckNoLinks(string path)
    {
        for (var current = System.IO.Path.GetFullPath(path); current is not null; current = System.IO.Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new ConfigurationException("AlgorithmConfigurationInvalid", "算法来源路径不允许链接");
    }
    private static void RejectDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in element.EnumerateObject())
            { if (!names.Add(field.Name)) throw new ConfigurationException("AlgorithmConfigurationInvalid", "重复配置字段"); RejectDuplicates(field.Value); }
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var value in element.EnumerateArray()) RejectDuplicates(value);
    }
}
