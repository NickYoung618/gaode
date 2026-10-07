using System.Text.Json.Serialization;

namespace Gaode.Application.Recipes;

[JsonConverter(typeof(RecipeDefinitionSerialization.InspectionKindConverter))]
public enum RecipeInspectionKind { Ordinary, SpecialRotation }
[JsonConverter(typeof(RecipeDefinitionSerialization.TrayRegionConverter))]
public enum RecipeTrayRegion { NG, OK, Pending }
public sealed record RecipeTrayCell(string CellId, int Row, int Column, RecipeTrayRegion Region);
public sealed record RecipeTrayLayout(int Rows, int Columns, IReadOnlyList<RecipeTrayCell> Cells)
{
    public IEnumerable<RecipeTrayCell> Ordered(RecipeTrayRegion region) =>
        Cells.Where(c => c.Region == region).OrderBy(c => c.Row).ThenBy(c => c.Column);
    public static string CellIdentity(int row, int column) => $"r{row}:c{column}";
}
public sealed record RecipeCellPhysicalBinding(string CellId, int PhysicalSlotIndex);
public sealed record RecipeTraySlotMapping(string Id, string Version, string EvidenceReference,
    IReadOnlyList<RecipeCellPhysicalBinding> Bindings);
public sealed record RotationWorkstationInputs(HandlingPoint Place, HandlingPoint Pick);
public sealed record OriginalSlotReference(Guid TrayId, string CellId, int Row, int Column,
    string SlotId, int PhysicalSlotIndex, string EntityId);

public sealed record RecipeMember(string Material, string MemberPattern, string Handling)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CellId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PhysicalSlotIndex { get; init; }
}
public sealed record RecipePosition(string SlotId, string UnitPattern, IReadOnlyList<RecipeMember> Members)
{
    public required int? PhysicalSlotIndex { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CellId { get; init; }
}
public sealed record RecipeMaterial(string Material, IReadOnlyList<int> LocalFaces)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SortingGripperId { get; init; }
}
public sealed record RecipeTarget(string Material, int LocalFace, string CameraPair,
    string CaptureProfile, string AlgorithmProfile);
public sealed record RecipeStage(int Number, string Action,
    double? AngleDeg, IReadOnlyList<RecipeTarget> Targets);
public sealed record RecipeExtraScanPose(string PoseId, RecipeTargetPose TargetPose,
    string ScanPointRef, string? PickPointRef, string? PutBackPointRef,
    string CaptureProfile, string AlgorithmProfile);
public sealed record RecipeCodeRule(bool Enabled, string? RepresentativeMaterial,
    string? BindTo, bool RequiredForOk, string? ReadAt)
{
    public string? CaptureProfile { get; init; }
    public string? AlgorithmProfile { get; init; }
    public string? ScanPointRef { get; init; }
    public RecipeExtraScanPose? ExtraPose { get; init; }
}
public sealed record RecipeDisposition(string Ok, string Ng, string Pending,
    string PhysicalUnit);
public sealed record RecipeDefinition(string RecipeId, string Version, string ReleaseStatus,
    string ScenarioId, string Model, string FCode, string UnitKind, string PrimaryMaterial,
    string LayoutProfile, int Capacity, IReadOnlyList<RecipePosition> Positions,
    IReadOnlyList<RecipeMaterial> Composition, string Route,
    IReadOnlyList<RecipeStage> Stages, RecipeCodeRule ECode, RecipeDisposition Disposition,
    string MotionProfile, string QualityProfile,
    IReadOnlyDictionary<string, CaptureProfile> CaptureProfiles,
    IReadOnlyDictionary<string, SlotExecutionInputs> ExecutionPositions,
    string CatalogDigest,
    int? PlcRecipeId = null, int? NgCapacity = null, int? PendingCapacity = null)
{
    public required string SchemaVersion { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SortingGripperId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RecipeTrayLayout? TrayLayout { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RecipeTraySlotMapping? TraySlotMapping { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RecipeInspectionKind? InspectionKind { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? RotationLoadingGripperId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RotationWorkstationInputs? RotationWorkstation { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, HandlingPoint>? SortingTargets { get; init; }
    public required string DefinitionDigest { get; init; }
    public required ApprovalScope Approval { get; init; }
    public required IReadOnlyDictionary<string, AlgorithmRequirement> AlgorithmRequirements { get; init; }
}

public sealed record RecipeCatalogItem(string RecipeId, string Version, string Model,
    string ScenarioId, string Route, string Purpose, string CatalogDigest,
    string Availability, string? Restriction)
{
    public RecipeInspectionKind? InspectionKind { get; init; }
    public string? UnitKind { get; init; }
}

public interface IRecipeCatalog
{
    RecipeCatalogSnapshot GetSnapshot();
}

public sealed record RecipeCatalogSnapshot(string SchemaVersion, string CatalogDigest,
    IReadOnlyList<RecipeDefinition> Definitions)
{
    public const string CurrentSchema = "recipe-catalog-snapshot/1";
}

public interface IRecipeStore
{
    Task<RecipeSaveResult> SaveAsync(RecipeSaveRequest request, CancellationToken cancellationToken);
}

public sealed record RecipeSaveRequest(RecipeDefinition Candidate, string? TargetRecipeId,
    string? ExpectedVersion, string RequestId);
public sealed record RecipeValidationIssue(string Code, string FieldPath, string Message,
    string? ObjectReference = null);
public sealed record RecipeValidationResult(IReadOnlyList<RecipeValidationIssue> Issues)
{
    public bool Valid => Issues.Count == 0;
}

public enum RecipeSaveStatus { Saved, ValidationFailed, VersionConflict, SaveFailed, CommitUnknown }
public sealed record RecipeSaveResult(RecipeSaveStatus Status, RecipeDefinition? Definition = null,
    string? CatalogDigest = null, IReadOnlyList<RecipeValidationIssue>? Issues = null, string? Reason = null)
{
    public string? RecipeId => Status == RecipeSaveStatus.Saved ? Definition?.RecipeId : null;
    public string? Version => Status == RecipeSaveStatus.Saved ? Definition?.Version : null;
    public string? DefinitionDigest => Status == RecipeSaveStatus.Saved ? Definition?.DefinitionDigest : null;
}

public sealed record RecipeSelectionIntent(string RecipeId, string? ObservedVersion = null,
    string? ObservedCatalogDigest = null);
public enum RecipeMatchStatus { Matched, Unmatched, Ambiguous, IdentityMismatch, Restricted }
public sealed record RecipeMatchResult(RecipeMatchStatus Status, RecipeDefinition? Definition,
    string CatalogDigest, string? Reason = null)
{
    public string? RecipeId => Definition?.RecipeId;
    public string? Version => Definition?.Version;
    public string? DefinitionDigest => Definition?.DefinitionDigest;
}

public enum RecipeStepKind
{
    FlipMember, RescanWholeTray, TransferToRotation, Rotate, PositionForCapture,
    Capture, ReadECode, DecideUnit, SortUnit, ReturnUnit, UnloadTray
}

public sealed record RecipeStep(int Sequence, RecipeStepKind Kind, string UnitId,
    string? MemberId, string? SlotId, string? Material, int? LocalFace,
    string? Camera, double? AngleDeg, int CoordinateEpoch, string When = "Always",
    string? CaptureProfile = null, string? AlgorithmProfile = null)
{
    public string OwnerStage { get; init; } = "Detection";
    public string? PhysicalEntityId { get; init; }
    public int? PhysicalSlotIndex { get; init; }
    public string? PointRef { get; init; }
    public string? StageId { get; init; }
    public string? ScanPoseId { get; init; }
    public RecipeTargetPose? TargetPose { get; init; }
}
public sealed record RecipeRunPlan(string TrayRunId, string ScenarioId, string FCode,
    string RecipeId, string RecipeVersion, string CatalogDigest, string ReleaseStatus,
    string UnitKind, int InitialCoordinateEpoch, string? ECodeBindTo,
    bool ECodeRequiredForOk, RecipeDisposition Disposition,
    string MotionProfile, string QualityProfile,
    IReadOnlyDictionary<string, CaptureProfile> CaptureProfiles,
    IReadOnlyDictionary<string, SlotExecutionInputs> ExecutionPositions,
    IReadOnlyList<string> MissingSlots,
    IReadOnlyList<RecipeStep> Steps,
    int? PlcRecipeId, int? NgCapacity = null, int? PendingCapacity = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, int>? SortingGrippersByMaterial { get; init; }
    public required string Model { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SortingGripperId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RecipeTrayLayout? TrayLayout { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RecipeTraySlotMapping? TraySlotMapping { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RecipeInspectionKind? InspectionKind { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? RotationLoadingGripperId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RotationWorkstationInputs? RotationWorkstation { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, OriginalSlotReference>? OriginalSlots { get; init; }
    public required string DefinitionDigest { get; init; }
    public required RecipeCodeRule ECode { get; init; }
    public required ApprovalScope Approval { get; init; }
    public required IReadOnlyDictionary<string, AlgorithmRequirement> AlgorithmRequirements { get; init; }
}



