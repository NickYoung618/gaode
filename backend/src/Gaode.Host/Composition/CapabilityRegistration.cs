using Gaode.Application.Capabilities;
using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Infrastructure.Recipes;
using Gaode.Application.Configuration;
using Gaode.Domain.Configuration;

namespace Gaode.Host.Composition;

public static class CapabilityRegistration
{
    public static CapabilityRegistry RegisterStation01(IAlgorithmPort provider, string approvedPurpose,
        CommissioningConfiguration? commissioning = null, RealAlgorithmConfiguration? realAlgorithm = null)
    {
        var registry = Station01Policies.Create();
        registry.Register(new FixedCapabilityPolicy("code.test-tray-format", "1.0", "Parser",
            new HashSet<string>(StringComparer.Ordinal) { "Test" }));
        registry.RegisterDecoder("code.test-tray-format", "1.0", RecipeEnvironmentDecoder.DecodeTrayCode);
        if (approvedPurpose == RuntimePurposes.RealDeviceCommissioning)
        {
            var rule = realAlgorithm?.CodeRule ?? commissioning?.CodeRule;
            if ((realAlgorithm?.Purpose ?? commissioning?.Purpose) != approvedPurpose ||
                rule is not { Id: "decoded-content-exact", Version: "1.0" } || string.IsNullOrWhiteSpace(rule.Source))
                throw new InvalidOperationException("CommissioningCodeRuleSourceRequired");
            registry.Register(new FixedCapabilityPolicy(rule.Id, rule.Version, "Parser", new HashSet<string> { approvedPurpose }));
            registry.RegisterDecoder(rule.Id, rule.Version, RecipeEnvironmentDecoder.DecodeTrayCode);
        }
        if (provider is IAlgorithmCapabilityProvider implementation && provider.Origin.IsKnown)
        {
            var version = provider.Origin.VersionRef!;
            var purpose = approvedPurpose;
            foreach (var capability in implementation.AlgorithmCapabilities)
                registry.RegisterAlgorithm(capability.Purpose, capability.CapabilityId, capability.CapabilityVersion,
                    capability.ResultContract, capability.InputCount, implementation.ImplementationReference,
                    version, purpose, "host-capability-registration/1");
        }
        return registry;
    }
}
