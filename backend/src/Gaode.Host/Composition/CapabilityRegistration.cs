using Gaode.Application.Capabilities;
using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Infrastructure.Recipes;

namespace Gaode.Host.Composition;

public static class CapabilityRegistration
{
    public static CapabilityRegistry RegisterStation01(IAlgorithmPort provider, string approvedPurpose)
    {
        var registry = Station01Policies.Create();
        registry.Register(new FixedCapabilityPolicy("code.test-tray-format", "1.0", "Parser",
            new HashSet<string>(StringComparer.Ordinal) { "Test" }));
        registry.RegisterDecoder("code.test-tray-format", "1.0", RecipeEnvironmentDecoder.DecodeTrayCode);
        if (provider is Gaode.Infrastructure.Algorithms.PythonWorkerAdapter worker && provider.Origin.IsKnown)
        {
            var version = provider.Origin.VersionRef!;
            var purpose = approvedPurpose;
            foreach (var (kind, id, count, contract) in new[] {
                (AlgorithmPurpose.SingleDetection, "detection.single", 1, "image-quality/1"),
                (AlgorithmPurpose.FaceFusion, "detection.fusion", 2, "face-quality/1"),
                (AlgorithmPurpose.EntityCode, "code.raw-candidates", 1, "decoded-code/1"),
                (AlgorithmPurpose.TrayPose, "tray.observation", 1, "tray-observation/2") })
                registry.RegisterAlgorithm(kind, id, "1.0", contract, count, worker.ImplementationReference,
                    version, purpose, "host-capability-registration/1");
        }
        return registry;
    }
}
