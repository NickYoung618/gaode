using Gaode.Domain.Configuration;
using Gaode.Application.Recipes;

namespace Gaode.Application.Capabilities;

public sealed class CapabilityRegistry
{
    private readonly Dictionary<(string, string), ICapabilityPolicy> _policies = [];
    private readonly Dictionary<(string, string), Func<string, DecodedTrayCode?>> _decoders = [];
    private readonly Dictionary<AlgorithmPurpose, (string Id, string Version, string Contract,
        int Inputs, string Provider, string ProviderVersion, string Purpose, string Approval)> _algorithms = [];

    public void RegisterDecoder(string id, string version, Func<string, DecodedTrayCode?> decode) =>
        _decoders.Add((id, version), decode);

    public DecodedTrayCode? Decode(CapabilityRef reference, string purpose, string raw)
    {
        if (!IsCompatible(reference, purpose, "Parser") || !_decoders.TryGetValue((reference.Id, reference.ContractVersion), out var decode))
            throw new InvalidOperationException("TrayCodeDecoderNotBound");
        return decode(raw);
    }

    public void RegisterAlgorithm(AlgorithmPurpose purpose, string id, string version, string contract,
        int inputs, string provider, string providerVersion, string approvedPurpose, string approval) =>
        _algorithms.Add(purpose, (id, version, contract, inputs, provider, providerVersion, approvedPurpose, approval));

    public BoundCapability Bind(AlgorithmRequirement requirement, string runPurpose)
    {
        if (!_algorithms.TryGetValue(requirement.Purpose, out var implementation) ||
            implementation.Contract != requirement.ResultContract || implementation.Inputs != requirement.InputCount ||
            implementation.Purpose != runPurpose || string.IsNullOrWhiteSpace(requirement.ParametersVersion) ||
            string.IsNullOrWhiteSpace(implementation.ProviderVersion))
            throw new InvalidOperationException("AlgorithmRequirementNotBound:" + requirement.Id);
        return new(requirement, implementation.Id, implementation.Version, implementation.Provider,
            implementation.ProviderVersion, implementation.Approval);
    }

    public void Register(ICapabilityPolicy policy)
    {
        if (!_policies.TryAdd((policy.Id, policy.ContractVersion), policy))
            throw new InvalidOperationException("重复注册能力版本");
    }

    public bool IsCompatible(CapabilityRef? reference, string purpose, string category)
    {
        if (reference is null) return false;
        return _policies.TryGetValue((reference.Id, reference.ContractVersion), out var policy) &&
            policy.Category == category && policy.SupportsPurpose(purpose) &&
            policy.ValidateReference(reference) is null;
    }

    public IReadOnlyDictionary<string, string> Versions => _policies.Values.ToDictionary(
        p => p.Id, p => p.ContractVersion, StringComparer.Ordinal);
}
