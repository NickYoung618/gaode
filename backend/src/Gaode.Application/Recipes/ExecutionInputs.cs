using Gaode.Application.Ports;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;

namespace Gaode.Application.Recipes;

// These are business inputs, not file documents. Decoders must preserve invalid
// identities for the common validator to reject; they cannot pre-plan operations.
public enum AlgorithmPurpose { Height, TrayCode, SingleDetection, FaceFusion, EntityCode, TrayPose }
public enum CoordinateResolutionKind { Unknown, MeasurementOffset, ApprovedFixed }

public sealed record CaptureProfile(string Id, string Version, DetectionCaptureSettings Settings)
{
    // CaptureSettings serves device adapters too and contains an array. Keep its
    // public shape while preventing recipe readers from changing frozen settings.
    private DetectionCaptureSettings settings = Copy(Settings);
    public DetectionCaptureSettings Settings { get => Copy(settings); init => settings = Copy(value); }
    private static DetectionCaptureSettings Copy(DetectionCaptureSettings value) =>
        value with { RoiPixels = (int[])value.RoiPixels.Clone() };
}
public sealed record AlgorithmRequirement(string Id, string ParametersVersion,
    AlgorithmPurpose Purpose, int InputCount, string ResultContract);
public sealed record BoundCapability(AlgorithmRequirement Requirement,
    string CapabilityId, string CapabilityVersion, string ProviderIdentity,
    string ProviderVersion, string ApprovalReference);

public sealed record ApprovedFixedBasis(double Z, string Unit, string Datum,
    string ApprovalReference, string ConfigurationVersion);
public sealed record PlanarPoint(string Id, string Version, double X, double Y, string Unit, string Frame);
public sealed record CoordinateDefinition(string PointRef, PlanarPoint Point,
    string ObjectPattern, string SlotId, int? PhysicalSlotIndex, int LocalFace,
    string Camera, string StageId, string ConfigurationVersion,
    string SourceFactReference, ApprovedFixedBasis Fixed)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? CaptureProfile { get; init; }
}

public sealed record HandlingPoint(FixedPoint Point, string CoordinateEvidenceReference);
public sealed record RecipeTargetPose(string ProfileId, string ProfileVersion, string PoseKey);
public enum RecipePointPurpose { FlipPick, FlipPutBack, EScan }
public sealed record RecipePurposePoint(RecipePointPurpose Purpose, PlanarPoint Point,
    ApprovedFixedBasis Fixed, string CoordinateEvidenceReference)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? CaptureProfile { get; init; }
}
public sealed record RecipeFlipTransition(RecipeTargetPose TargetPose,
    string PickPointRef, string PutBackPointRef);
public sealed record RecipeFlipInputs(IReadOnlyDictionary<string, RecipeFlipTransition> Stages);
public sealed record AuxiliaryTarget(HandlingPoint Target, int ActionTimeoutMs);
public sealed record RotationTargets(AuxiliaryTarget Entry,
    IReadOnlyDictionary<int, AuxiliaryTarget> Poses,
    IReadOnlyDictionary<string, AuxiliaryTarget> Exits);
public sealed record ObjectExecutionInputs(HandlingPoint? Source,
    IReadOnlyList<CoordinateDefinition> Coordinates, RecipeFlipInputs? Flip,
    RotationTargets? Rotation, IReadOnlyDictionary<string, HandlingPoint> Sorting,
    IReadOnlyDictionary<string, RecipePurposePoint> PurposePoints)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public HandlingPoint? OriginPutBack { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? SortingCellIds { get; init; }
}
public sealed record SlotExecutionInputs(string SlotId, ObjectExecutionInputs PhysicalEntity,
    IReadOnlyDictionary<string, ObjectExecutionInputs> Members)
{
    public ObjectExecutionInputs ForDetection(string? material) => material is not null &&
        Members.TryGetValue(material, out var member) ? member : PhysicalEntity;
    public ObjectExecutionInputs ForObject(string unitKind, string? material) =>
        unitKind == "looseGroup" && material is not null
            ? Members.TryGetValue(material, out var member) ? member :
                throw new InvalidOperationException("ProductMemberTargetMissing")
            : PhysicalEntity;
}

public sealed record ApprovalScope(string Id, string Version, string Digest, string Purpose,
    IReadOnlyList<string> AllowedSlots, string EvidenceReference);
public sealed record AdmissionDecision(bool Eligible, string? Reason,
    string ApprovalReference, string InputDigest);
public sealed record DecodedTrayCode(string RawCode, string ParsedCode,
    string FormatVersion, string EvidenceReference);

// Device-specific I/O accounting belongs to the budget/configuration boundary.
// These approved allowances retain the original absolute lifecycle and amounts.
public sealed record ExecutionCostProfile(string Id, string Version, string Purpose,
    string Source, string BudgetReference, string BudgetDigest,
    int CaptureMs, int AlgorithmMs, int AcquisitionReleaseMs,
    long OrdinarySortDeviceAllowanceMs, long UnloadDeviceAllowanceMs)
{
    public required int CaptureWaitMs { get; init; }
    public required int AlgorithmWaitMs { get; init; }
    public required int InputReleaseWaitMs { get; init; }
}

public sealed record FrozenExecutionInputs(string SchemaVersion, Guid RunId, Guid TrayId,
    string PlanRevision, RecipeRunPlan Plan,
    IReadOnlyDictionary<string, BoundCapability> Capabilities,
    ExecutionCostProfile CostProfile, string SemanticDigest)
{
    public const string CurrentSchema = "execution-inputs/3";
    public const string HistoricalSchema = "execution-inputs/2";
    public BoundCapability? TrayPoseCapability { get; init; }
    public string ComputeDigest() => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
        System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(this with { SemanticDigest = "" },
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))));
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsValid => SchemaVersion is CurrentSchema or HistoricalSchema && RunId != Guid.Empty && TrayId != Guid.Empty &&
        Plan.TrayRunId == TrayId.ToString("D") && PlanRevision == RecipePlanRevision.Compute(Plan) &&
        SemanticDigest == ComputeDigest();
}

public sealed record CommittedAlgorithmSource(Guid RunId, Guid CaptureId, Guid CallId,
    ComponentExecutionOrigin Origin, Guid WriteId, long CommittedRevision);
