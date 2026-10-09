using Gaode.Domain.Configuration;

namespace Gaode.Application.Configuration;

// Immutable Host descriptor data; invocation/SDK/IPC interpretation stays in adapters.
public sealed record AlgorithmFileReference(string Path, string Version, string Sha256);
public sealed record AlgorithmProviderReference(string ImplementationId, string AlgorithmVersion, string DeliveryKind,
    AlgorithmFileReference Artifact, string EntryPoint, IReadOnlyList<string> Arguments, IReadOnlyList<AlgorithmFileReference> Dependencies);
public sealed record AlgorithmModuleReference(string Module, string BindingId, string CapabilityId, string CapabilityVersion,
    string ResultContract, int InputCount, string ModelId, string ModelVersion, string ParametersVersion,
    IReadOnlyList<AlgorithmFileReference> ModelFiles, AlgorithmFileReference ParametersFile);
public sealed record TrayPosePngReference(bool? Required, string? Plane, int? BitDepth);
public sealed record AlgorithmInputFormats(IReadOnlyList<int> PngBitDepths, IReadOnlyList<string> PngColorTypes,
    string? PlyEncoding, bool? PlyRgbRequired, TrayPosePngReference TrayPosePng);
public sealed record RealAlgorithmConfiguration(string SchemaVersion, string Id, string Version, string Purpose, string Source,
    ConfigReference CommissioningRef, ConfigReference PublicRef, ConfigReference BudgetRef, CommissioningCodeRule CodeRule,
    AlgorithmProviderReference Provider, IReadOnlyList<AlgorithmModuleReference> Modules, AlgorithmInputFormats Inputs,
    AlgorithmFileReference? LayoutFile, AlgorithmFileReference? CalibrationFile);
