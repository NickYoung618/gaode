using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Domain.Configuration;

namespace Gaode.Application.Configuration;

public sealed record FrozenConfiguration(PublicConfiguration Public, BusinessBudget Budget,
    SimulationProfile Simulation, string PublicJson, string BudgetJson, string SimulationJson,
    string PublicDigest, string BudgetDigest, string SimulationDigest,
    IReadOnlyDictionary<string, string> CapabilityVersions, string SnapshotId);

public static class ConfigurationFreezer
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static FrozenConfiguration Freeze(LoadedConfiguration<PublicConfiguration> p,
        LoadedConfiguration<BusinessBudget> b, LoadedConfiguration<SimulationProfile> s,
        IReadOnlyDictionary<string, string> capabilityVersions)
    {
        static T Copy<T>(string json) => JsonSerializer.Deserialize<T>(json, Options)!;
        var idMaterial = p.Digest + b.Digest + s.Digest + string.Join(";", capabilityVersions.OrderBy(x => x.Key).Select(x => x.Key + "=" + x.Value));
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
            Copy<SimulationProfile>(s.CanonicalJson), p.CanonicalJson, b.CanonicalJson, s.CanonicalJson,
            p.Digest, b.Digest, s.Digest, new Dictionary<string, string>(capabilityVersions), snapshotId);
    }
}
