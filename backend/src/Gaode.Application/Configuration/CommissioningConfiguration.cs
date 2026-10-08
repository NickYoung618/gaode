using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;

namespace Gaode.Application.Configuration;

public sealed record CommissioningRecipeBinding(string RecipeId, string Version, string DefinitionDigest,
    string ScenarioId, string Model, string FCode);
public sealed record CommissioningCodeRule(string Id, string Version, string Source);
public sealed record CommissioningResultScope(string SlotId, string Material, string StageId,
    int LocalFace, int HeightRound, string Camera);
public sealed record CommissioningAlgorithmResult(string Id, string Version, string Source,
    AlgorithmPurpose Purpose, CommissioningResultScope? Scope, string? Disposition);
public sealed record CommissioningConfiguration(string SchemaVersion, string Id, string Version,
    string Purpose, string Source, ConfigReference PublicConfigRef, ConfigReference BudgetRef,
    CommissioningRecipeBinding ExpectedRecipe, CommissioningCodeRule CodeRule,
    IReadOnlyDictionary<string, string> PublicLightChannels,
    IReadOnlyList<AlgorithmCapabilityDeclaration> Algorithms,
    IReadOnlyList<TraySlotObservation> Slots, FLocation? FLocation, string MappingSourceReference,
    IReadOnlyList<string> RawCodes, IReadOnlyList<CommissioningAlgorithmResult> Results)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<CommissioningRecipeInputs>? RecipeInputs { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<CommissioningEntityCodeResult>? EntityCodes { get; init; }
}

public sealed record CommissioningEntityCodeResult(string Id, string Version, string Source,
    CommissioningResultScope Scope, IReadOnlyList<string> Codes);
public sealed record CommissioningRecipeInputs(string Id, string Version, string Source,
    CommissioningRecipeBinding ExpectedRecipe, IReadOnlyList<TraySlotObservation> Slots, FLocation? FLocation,
    string MappingSourceReference, IReadOnlyList<string> RawCodes, IReadOnlyList<CommissioningAlgorithmResult> Results,
    IReadOnlyList<CommissioningEntityCodeResult>? EntityCodes = null);

