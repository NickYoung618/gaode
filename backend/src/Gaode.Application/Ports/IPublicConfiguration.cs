using Gaode.Domain.Configuration;

namespace Gaode.Application.Ports;

public sealed record LoadedConfiguration<T>(T Value, string CanonicalJson, string Digest,
    string SourceFile);

public interface IPublicConfiguration
{
    LoadedConfiguration<PublicConfiguration> LoadPublic(ConfigReference reference);
    LoadedConfiguration<PublicConfiguration> SavePublicPositions(ConfigReference reference,
        FixedPoint threeD, FixedPoint manualLoading, string expectedDigest);
    LoadedConfiguration<BusinessBudget> LoadBudget(ConfigReference reference);
    LoadedConfiguration<SimulationProfile> LoadSimulation(ConfigReference reference);
}
