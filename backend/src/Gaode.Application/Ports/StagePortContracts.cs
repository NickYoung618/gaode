using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Application.Recipes;

namespace Gaode.Application.Ports;

public enum DetectionResultKind { Accepted, Executing, Completed, Failed, TimedOut, Disconnected, UnknownHeld }
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<DetectionEvidenceBasis>))]
public enum DetectionEvidenceBasis { Unspecified, ProductInspection, InitialPoseExclusion }
public enum StageActionKind { Accepted, Executing, Completed, Failed, TimedOut, Disconnected, UnknownHeld }
public enum WholeTrayWorkflowStage
{
    Detection,
    Sorting,
    UnloadPreparation,
    UnlockObservation,
    ManualTrayRemovalConfirmation,
    RecipeApplication,
    ManualRemovalAdmission
}
public enum PlcWorkflowStage { Sorting, UnloadPreparation, UnlockObservation, TransferToRotation, Rotate }
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter<TransferPurpose>))]
public enum TransferPurpose { RotationLoading, Sorting, ReturnToOrigin }
public sealed record DetectionExecutionScope(string UnitId, string SlotId)
{
    public bool Includes(RecipeStep step) => step.UnitId == UnitId && step.SlotId == SlotId;
}
public sealed record RotationExecutionBasis(double AngleToleranceDeg, string Purpose, string SourceReference);
public sealed record RotationExecutionConfiguration(RotationExecutionBasis? Basis);
public sealed record RotationTarget(double AngleDeg, string StageId, double AngleToleranceDeg,
    string MechanicalEvidenceReference);
public enum ResultSource { Real, Virtual, Simulated, Fallback, Test, HostDerived }
public enum ResultQuality { Measured, Derived, Degraded, Unknown }
public enum StageFailureClass
{
    DetectionCommunication,
    DetectionAlgorithmTimeout,
    PlcPreDispatchCommunication,
    PlcPhysicalDispatchUnknown
}

public sealed record DetectionObjectExpectation(
    string ObjectId,
    FixedPoint Position,
    string ExpectedClassification)
{
    public bool IsValid => !string.IsNullOrWhiteSpace(ObjectId) && Position is not null &&
        !string.IsNullOrWhiteSpace(Position.Id) &&
        !string.IsNullOrWhiteSpace(ExpectedClassification);

    public bool IsApprovedPosition => IsValid &&
        !string.IsNullOrWhiteSpace(Position.Version) &&
        Position.Frame != "frozen-plan" &&
        !Position.Version.StartsWith("plan:", StringComparison.Ordinal) &&
        double.IsFinite(Position.X) && double.IsFinite(Position.Y) &&
        double.IsFinite(Position.Z);
}

public sealed record DetectionStepTarget(int StepSequence, string ObjectId, string SlotId,
    int PhysicalSlotIndex, int? LocalFace, int CoordinateEpoch, string Camera,
    string PointRef, FixedPoint Point, string Source, string ZBasis)
{
    public string? StageId { get; init; }
    public string? ScanPoseId { get; init; }
    public CoordinateResolutionKind ResolutionKind { get; init; }
    public bool IsValid => StepSequence > 0 && !string.IsNullOrWhiteSpace(ObjectId) &&
        !string.IsNullOrWhiteSpace(SlotId) && PhysicalSlotIndex > 0 &&
        (LocalFace > 0 || Camera == "E" && !string.IsNullOrWhiteSpace(ScanPoseId)) &&
        CoordinateEpoch > 0 && Camera is "A" or "B" or "C" or "D" or "E" &&
        !string.IsNullOrWhiteSpace(PointRef) && Point is not null &&
        !string.IsNullOrWhiteSpace(Point.Id) && !string.IsNullOrWhiteSpace(Point.Version) &&
        double.IsFinite(Point.X) && double.IsFinite(Point.Y) && double.IsFinite(Point.Z) &&
        !string.IsNullOrWhiteSpace(Source) && ResolutionKind != CoordinateResolutionKind.Unknown &&
        !string.IsNullOrWhiteSpace(ZBasis);
}

public sealed record DetectionRequest(
    Guid RunId,
    Guid TrayId,
    Guid StationId,
    Guid LineId,
    WholeTrayWorkflowStage Stage,
    Guid OperationId,
    string PlanRevision,
    long ConnectionEpoch,
    DateTimeOffset DeadlineUtc,
    IReadOnlyList<string> InputMediaReferences,
    string Purpose,
    string IdempotencyKey,
    int Attempt = 1,
    DateTimeOffset? StageStartedAtUtc = null,
    IReadOnlyList<string>? ComponentEvidenceReferences = null,
    IReadOnlyList<DetectionObjectExpectation>? ExpectedObjects = null,
    RecipeRunPlan? Plan = null,
    PublicConfiguration? MotionConfiguration = null,
    IReadOnlyList<DetectionStepTarget>? Targets = null)
{
    public FrozenExecutionInputs? Inputs { get; init; }
    public Gaode.Application.Configuration.RealAlgorithmConfiguration? AlgorithmConfiguration { get; init; }
    public string? AlgorithmConfigurationDigest { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public DetectionExecutionScope? Scope { get; init; }
    public Guid SessionId { get; init; }
    public string SnapshotId { get; init; } = "";
    public string ClockId { get; init; } = "";
    // Frozen business save allowance; the original stage deadline remains the outer limit.
    public int CriticalSaveBudgetMs { get; init; }
    public BusinessDurations? FrozenBusinessDurations { get; init; }
    public TrayObservation? InitialObservation { get; init; }
    public Guid? InitialObservationWriteId { get; init; }
    public bool IsValid =>
        RunId != Guid.Empty && TrayId != Guid.Empty && StationId != Guid.Empty &&
        LineId != Guid.Empty && OperationId != Guid.Empty &&
        Stage == WholeTrayWorkflowStage.Detection &&
        !string.IsNullOrWhiteSpace(PlanRevision) && ConnectionEpoch > 0 &&
        DeadlineUtc > DateTimeOffset.MinValue &&
        InputMediaReferences is { Count: > 0 } &&
        InputMediaReferences.All(x => !string.IsNullOrWhiteSpace(x)) &&
        !string.IsNullOrWhiteSpace(Purpose) &&
        !string.IsNullOrWhiteSpace(IdempotencyKey) && Attempt >= 1 &&
        (StageStartedAtUtc is null || StageStartedAtUtc < DeadlineUtc) &&
        (ComponentEvidenceReferences is null ||
            ComponentEvidenceReferences.All(x => !string.IsNullOrWhiteSpace(x))) &&
        (ExpectedObjects is null || ExpectedObjects.All(x => x.IsValid)) &&
        ExpectedObjects is { Count: > 0 } && ExpectedObjects.All(x => x.IsApprovedPosition);
}

public sealed record DetectionObjectResult(
    string ObjectId,
    FixedPoint Position,
    string Classification,
    string Disposition = "NG",
    IReadOnlyList<string>? EvidenceReferences = null)
{
    public bool SpecialHandlingCompleted { get; init; }
    public string? HandlingEvidenceReference { get; init; }
    public bool IsValid => !string.IsNullOrWhiteSpace(ObjectId) &&
        Position is not null && Position.Id.Length > 0 &&
        double.IsFinite(Position.X) && double.IsFinite(Position.Y) &&
        double.IsFinite(Position.Z) && !string.IsNullOrWhiteSpace(Classification) &&
        Disposition is "OK" or "NG" or "Pending" &&
        (EvidenceReferences is null || EvidenceReferences.All(x => !string.IsNullOrWhiteSpace(x)));
}

public sealed record DetectionPortResult(
    DetectionRequest Request,
    DetectionResultKind Kind,
    string? ResultReference,
    IReadOnlyList<DetectionObjectResult> Objects,
    ResultSource Source,
    ResultQuality Quality,
    string? ErrorCode,
    DateTimeOffset ObservedAtUtc,
    IReadOnlyList<string>? EvidenceReferences = null)
{
    // Adapter assertion: no capture, algorithm call or mechanical action was dispatched.
    public bool NoWorkStarted { get; init; }
    public string? EndBasisReference { get; init; }
    public IReadOnlyList<PosePendingHandling> PosePending { get; init; } = [];
    public DetectionEvidenceBasis EvidenceBasis { get; init; }
    public IReadOnlyList<CorrelatedCaptureFact> CaptureFacts { get; init; } = [];
    public ComponentExecutionOrigin AlgorithmOrigin { get; init; } = ComponentExecutionOrigin.Unknown;
    public TrayObservation? LastObservation { get; init; }
    public IReadOnlyDictionary<int, SlotParticipation> SlotParticipation { get; init; } =
        new Dictionary<int, SlotParticipation>();
    public bool IsCorrelated => Request.IsValid && Request.OperationId != Guid.Empty;
    public bool IsValid => IsCorrelated && Kind switch
    {
        DetectionResultKind.Accepted or DetectionResultKind.Executing => true,
        DetectionResultKind.Completed => !string.IsNullOrWhiteSpace(ResultReference) &&
            Objects is not null && Objects.All(x => x.IsValid) &&
            (EvidenceReferences is null || EvidenceReferences.All(x => !string.IsNullOrWhiteSpace(x))),
        DetectionResultKind.Failed or DetectionResultKind.TimedOut or DetectionResultKind.Disconnected or DetectionResultKind.UnknownHeld =>
            !string.IsNullOrWhiteSpace(ErrorCode),
        _ => false
    };
    public bool IsRealAcceptance => Kind == DetectionResultKind.Completed &&
        Source == ResultSource.Real && Quality == ResultQuality.Measured;
}

public sealed record PosePendingHandling(string ObjectId, int PhysicalSlotIndex,
    Guid ObservationId, string ObservationReference, string DetectionState, string Reason);

public interface IDetectionPort
{
    ValueTask<DetectionPortResult> ExecuteAsync(
        DetectionRequest request, CancellationToken cancellationToken);
}

public sealed record PlcStageActionRequest(
    ActionCorrelation Correlation,
    Guid StationId,
    Guid LineId,
    PlcWorkflowStage Stage,
    string ActionParametersDigest,
    ActionWindow Window,
    string IdempotencyKey,
    Guid? WholeTrayCompletionId = null,
    int? PhysicalSlotIndex = null,
    FixedPoint? UnloadTarget = null,
    double PositionTolerance = 0,
    string? TargetPurpose = null,
    FixedPoint? SortingSource = null,
    FixedPoint? SortingTarget = null,
    string? ReservationReference = null)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public DetectionExecutionScope? Scope { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public TransferPurpose? TransferPurpose { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? RequestedGripperId { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public FixedPoint? SafeTarget { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public RotationTarget? RotationTarget { get; init; }
    public Guid RunId => Correlation.RunId;
    public Guid TrayId => Correlation.TrayId ?? Guid.Empty;
    public Guid OperationId => Correlation.OperationId;
    public int Attempt => Correlation.Attempt;
    public string PlanRevision => Correlation.PlanRevision ?? "";
    public long ConnectionEpoch => Correlation.ConnectionEpoch;
    public string ConfigSnapshotId => Correlation.SnapshotId;
    public DateTimeOffset DeadlineUtc => Window.DeadlineUtc;
    public bool IsValid => Correlation.IsValid && TrayId != Guid.Empty && StationId != Guid.Empty &&
        LineId != Guid.Empty && Window.IsValid && !string.IsNullOrWhiteSpace(PlanRevision) &&
        !string.IsNullOrWhiteSpace(ActionParametersDigest) && !string.IsNullOrWhiteSpace(IdempotencyKey) &&
        double.IsFinite(PositionTolerance) && PositionTolerance >= 0 &&
        (TransferPurpose is null || RequestedGripperId is 1 or 2 &&
            Stage is PlcWorkflowStage.Sorting or PlcWorkflowStage.TransferToRotation) &&
        (Stage != PlcWorkflowStage.TransferToRotation || TransferPurpose == Gaode.Application.Ports.TransferPurpose.RotationLoading &&
            PhysicalSlotIndex is > 0 && SortingSource is not null && SortingTarget is not null &&
            !string.IsNullOrWhiteSpace(ReservationReference)) &&
        (Stage != PlcWorkflowStage.Rotate || RotationTarget is { } rotation && double.IsFinite(rotation.AngleDeg) &&
            double.IsFinite(rotation.AngleToleranceDeg) && rotation.AngleToleranceDeg >= 0 &&
            !string.IsNullOrWhiteSpace(rotation.StageId) && !string.IsNullOrWhiteSpace(rotation.MechanicalEvidenceReference)) &&
        (Stage != PlcWorkflowStage.UnlockObservation || WholeTrayCompletionId is { } id && id != Guid.Empty) &&
        (Stage != PlcWorkflowStage.Sorting || PhysicalSlotIndex is > 0 &&
            PhysicalSlotIndex == Correlation.PhysicalSlotIndex && SortingSource is not null && SortingTarget is not null &&
            !string.IsNullOrWhiteSpace(ReservationReference));
}

public sealed record PlcStageActionResult(
    PlcStageActionRequest Request,
    StageActionKind Kind,
    Guid ActionId,
    long ConnectionEpoch,
    string? ErrorCode,
    bool HoldsDevice,
    bool CanRetry,
    DateTimeOffset ObservedAtUtc,
    ExecutionOrigin ExecutionOrigin,
    DeviceActionEvidence? Evidence)
{
    public bool IsCorrelated => Request.IsValid && ActionId == Request.Correlation.ActionId &&
        ConnectionEpoch == Request.ConnectionEpoch &&
        (Evidence is null || Evidence.Correlation == Request.Correlation);
    public bool IsCompleted => Kind == StageActionKind.Completed && IsCorrelated && !HoldsDevice &&
        Evidence is { IsCorrelated: true } && HasRequiredPositions && Evidence.Meaning == (Request.Stage switch
        {
            PlcWorkflowStage.Sorting => DeviceCompletionMeaning.MaterialTransferred,
            PlcWorkflowStage.TransferToRotation => DeviceCompletionMeaning.MaterialTransferred,
            PlcWorkflowStage.Rotate => DeviceCompletionMeaning.PositionReached,
            PlcWorkflowStage.UnloadPreparation => DeviceCompletionMeaning.UnloadPrepared,
            PlcWorkflowStage.UnlockObservation => DeviceCompletionMeaning.Unlocked,
            _ => throw new InvalidOperationException("Unknown business stage")
        });
    private bool HasRequiredPositions => Request.Stage switch
    {
        PlcWorkflowStage.Sorting or PlcWorkflowStage.TransferToRotation => Evidence!.Positions.Count >= 2 &&
            HasPosition(Request.SortingSource) && HasPosition(Request.SortingTarget) &&
            (Request.TransferPurpose is null || Evidence.SafeReached is { Matched: true } &&
                (Request.SafeTarget is null || Evidence.SafeReached.Target == Request.SafeTarget)),
        PlcWorkflowStage.Rotate => Request.RotationTarget is { } target && Evidence!.AngleReached is { Matched: true } angle &&
            angle.TargetAngleDeg == target.AngleDeg && angle.ToleranceDeg <= target.AngleToleranceDeg,
        PlcWorkflowStage.UnloadPreparation => HasPosition(Request.UnloadTarget),
        PlcWorkflowStage.UnlockObservation => true,
        _ => false
    };
    private bool HasPosition(FixedPoint? target) => target is not null && Evidence!.Positions.Any(p =>
        p.Target == target && p.Tolerance <= Request.PositionTolerance);
    public bool IsUnknownHeld => Kind == StageActionKind.UnknownHeld && HoldsDevice && !CanRetry;
    public bool HasComponentEvidence => Evidence is { IsCorrelated: true } &&
        ExecutionOrigin.Provider != DeviceProvider.Unavailable &&
        !string.IsNullOrWhiteSpace(ExecutionOrigin.ComponentVersion);
}

public interface IPlcStageActionPort
{
    ValueTask<PlcStageActionResult> ExecuteAsync(
        PlcStageActionRequest request, CancellationToken cancellationToken);
}

public static class DeviceStageContract
{
    public static bool IsKnownStage(PlcWorkflowStage stage) =>
        stage is PlcWorkflowStage.Sorting or PlcWorkflowStage.UnloadPreparation or PlcWorkflowStage.TransferToRotation or PlcWorkflowStage.Rotate;
}

public sealed record MappingFailed(
    Guid RunId,
    Guid TrayId,
    string PlanRevision,
    IReadOnlyList<string> MissingObjectIds,
    IReadOnlyList<string> DuplicateObjectIds,
    IReadOnlyList<string> AmbiguousObjectIds,
    string Reason)
{
    public bool IsValid => RunId != Guid.Empty && TrayId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(PlanRevision) &&
        (MissingObjectIds.Count > 0 || DuplicateObjectIds.Count > 0 ||
         AmbiguousObjectIds.Count > 0) && !string.IsNullOrWhiteSpace(Reason);
    public bool AllowsPartialDispatch => false;
}

public sealed record UnknownHeld(
    Guid RunId,
    Guid TrayId,
    PlcWorkflowStage Stage,
    Guid OperationId,
    long ConnectionEpoch,
    string ErrorCode,
    bool HoldsDevice,
    bool AutomaticRetryAllowed)
{
    public bool IsValid => RunId != Guid.Empty && TrayId != Guid.Empty &&
        OperationId != Guid.Empty && ConnectionEpoch > 0 &&
        !string.IsNullOrWhiteSpace(ErrorCode) && HoldsDevice && !AutomaticRetryAllowed;
}
