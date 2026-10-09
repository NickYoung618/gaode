using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Domain.Configuration;

namespace Gaode.Application.Configuration;

public sealed record FrozenConfiguration(PublicConfiguration Public, BusinessBudget Budget,
    SimulationProfile? Simulation, string PublicJson, string BudgetJson, string SimulationJson,
    string PublicDigest, string BudgetDigest, string SimulationDigest,
    IReadOnlyDictionary<string, string> CapabilityVersions, string SnapshotId)
{
    public string? CommissioningJson { get; init; }
    public string? CommissioningDigest { get; init; }
    public string? CommissioningSourceFile { get; init; }
    public string? RealAlgorithmJson { get; init; }
    public string? RealAlgorithmDigest { get; init; }
    public string? RealAlgorithmSourceFile { get; init; }
    public RealAlgorithmConfiguration? RealAlgorithm { get; init; }
}

public static class ConfigurationFreezer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
    public static FrozenConfiguration RestoreAudit(JsonElement audit, string evidenceReference)
    {
        LoadedConfiguration<T> Read<T>(string field)
        {
            var json = audit.GetProperty(field + "Json").GetString()!;
            var digest = audit.GetProperty(field + "Digest").GetString()!;
            if (!StringComparer.OrdinalIgnoreCase.Equals(digest, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)))))
                throw new InvalidOperationException("FrozenBindingConfigurationDigestMismatch");
            return new(JsonSerializer.Deserialize<T>(json, Options) ?? throw new InvalidDataException("FrozenConfigurationMissing"), json, digest, evidenceReference);
        }
        var p = Read<PublicConfiguration>("public");
        var commissioning = p.Value.Purpose == RuntimePurposes.RealDeviceCommissioning;
        var real = commissioning && audit.TryGetProperty("realAlgorithmJson", out var value) && value.ValueKind == JsonValueKind.String
            ? Read<RealAlgorithmConfiguration>("realAlgorithm") with { SourceFile = audit.GetProperty("realAlgorithmSourceFile").GetString()! } : null;
        var frozen = Freeze(p, Read<BusinessBudget>("budget"), commissioning ? null : Read<SimulationProfile>("simulation"),
            audit.GetProperty("capabilityVersions").Deserialize<Dictionary<string,string>>(Options)!,
            commissioning && real is null ? Read<CommissioningConfiguration>("commissioning") : null, real);
        if (frozen.SnapshotId != audit.GetProperty("snapshotId").GetString()) throw new InvalidOperationException("FrozenBindingSnapshotMismatch");
        return frozen;
    }

    public static FrozenConfiguration Freeze(LoadedConfiguration<PublicConfiguration> p,
        LoadedConfiguration<BusinessBudget> b, LoadedConfiguration<SimulationProfile>? s,
        IReadOnlyDictionary<string, string> capabilityVersions,
        LoadedConfiguration<CommissioningConfiguration>? commissioning = null,
        LoadedConfiguration<RealAlgorithmConfiguration>? realAlgorithm = null)
    {
        static T Copy<T>(string json) => JsonSerializer.Deserialize<T>(json, Options)!;
        if (p.Value.Purpose == RuntimePurposes.RealDeviceCommissioning)
        {
            if (realAlgorithm is not null)
            {
                if (s is not null || commissioning is not null || realAlgorithm.Value.Purpose != p.Value.Purpose ||
                    realAlgorithm.Value.PublicRef != new ConfigReference(p.Value.Id,p.Value.Version) ||
                    realAlgorithm.Value.BudgetRef != new ConfigReference(b.Value.Id,b.Value.Version) ||
                    !StringComparer.OrdinalIgnoreCase.Equals(realAlgorithm.Digest, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(realAlgorithm.CanonicalJson)))))
                    throw new ConfigurationException("RealAlgorithmFrozenReferenceInvalid", "真实算法冻结来源不符");
            }
            else
            {
            if (s is not null || commissioning is null ||
                !string.Equals(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(commissioning.CanonicalJson))), commissioning.Digest, StringComparison.OrdinalIgnoreCase))
                throw new ConfigurationException("CommissioningConfigurationMissingOrDigestInvalid", "联调配置来源或摘要不符");
            var input = Copy<CommissioningConfiguration>(commissioning.CanonicalJson);
            if (input.Purpose != p.Value.Purpose || input.PublicConfigRef != new ConfigReference(p.Value.Id, p.Value.Version) ||
                input.BudgetRef != new ConfigReference(b.Value.Id, b.Value.Version))
                throw new ConfigurationException("CommissioningConfigurationReferenceMismatch", "联调配置引用不符");
            }
        }
        else if (s is null || commissioning is not null || realAlgorithm is not null)
            throw new ConfigurationException("SimulationConfigurationMismatch", "运行模式与配置不符");
        var idMaterial = p.Digest + b.Digest + (s?.Digest ?? "") + (commissioning?.Digest ?? "") +
            (realAlgorithm is null ? "" : realAlgorithm.Digest + realAlgorithm.SourceFile) + string.Join(";", capabilityVersions.OrderBy(x => x.Key).Select(x => x.Key + "=" + x.Value));
        var snapshotId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(idMaterial)));
        var frozenBudget = Copy<BusinessBudget>(b.CanonicalJson);
        var actualBudgetDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(b.CanonicalJson)));
        if (!StringComparer.OrdinalIgnoreCase.Equals(actualBudgetDigest, b.Digest) ||
            frozenBudget.Id != b.Value.Id || frozenBudget.Version != b.Value.Version || frozenBudget.Purpose != p.Value.Purpose)
            throw new ConfigurationException("FrozenBudgetIdentityInvalid", "预算快照与来源不一致");
        try
        {
            Recipes.RecipeApplicationCoordinator.RequireBudget(frozenBudget.Id, frozenBudget.Version, frozenBudget.SchemaVersion,
                frozenBudget.Purpose, frozenBudget.Source, actualBudgetDigest, snapshotId, frozenBudget.BusinessMs.RecipeApplication);
        }
        catch (InvalidOperationException error)
        {
            // Preserve the finite business rejection code through the actual Host
            // configuration path; callers must not see only an exception type.
            throw new ConfigurationException(error.Message, "配方应用预算无法冻结：" + error.Message);
        }
        return new(Copy<PublicConfiguration>(p.CanonicalJson), frozenBudget,
            s is null ? null : Copy<SimulationProfile>(s.CanonicalJson), p.CanonicalJson, b.CanonicalJson, s?.CanonicalJson ?? "",
            p.Digest, b.Digest, s?.Digest ?? "", new Dictionary<string, string>(capabilityVersions), snapshotId)
            { CommissioningJson = commissioning?.CanonicalJson, CommissioningDigest = commissioning?.Digest,
                CommissioningSourceFile = commissioning?.SourceFile, RealAlgorithmJson = realAlgorithm?.CanonicalJson,
                RealAlgorithmDigest = realAlgorithm?.Digest, RealAlgorithmSourceFile = realAlgorithm?.SourceFile,
                RealAlgorithm = realAlgorithm is null ? null : Copy<RealAlgorithmConfiguration>(realAlgorithm.CanonicalJson) };
    }
}
