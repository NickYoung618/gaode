using Gaode.Application.Configuration;
using Gaode.Application.Recipes;
using Gaode.Domain.Configuration;

namespace Gaode.Application.Ports;

public interface ICommissioningRunInputs
{
    void FreezeRun(Guid runId, Guid trayId, string scenarioId, PublicConfiguration configuration, Gaode.Application.Station01.ExpectedRecipeRef? selection = null, Gaode.Domain.Station01.FLocation? fLocation = null);
    void BindRecipe(Guid runId, FrozenExecutionInputs inputs);
    void ReleaseRun(Guid runId);
}

public sealed record AlgorithmCapabilityDeclaration(AlgorithmPurpose Purpose, string CapabilityId,
    string CapabilityVersion, string ResultContract, int InputCount, string ParametersVersion);

public interface IAlgorithmCapabilityProvider
{
    string ImplementationReference { get; }
    IReadOnlyList<AlgorithmCapabilityDeclaration> AlgorithmCapabilities { get; }
}
