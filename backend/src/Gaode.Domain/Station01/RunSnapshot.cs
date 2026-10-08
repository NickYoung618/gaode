namespace Gaode.Domain.Station01;

public sealed record RunSnapshot(
    Guid RunId, string RequestId, string SubjectId, RunState State, long ObservedRevision,
    long PersistedRevision, TerminalOutcome FinalOutcome, bool CancelRequested,
    ActionState Action, CaptureState Capture, AlgorithmState Algorithm, SaveState Save,
    HandoffState Handoff, string? PublicVersion, string? BudgetVersion,
    string? SimulationVersion, IReadOnlyList<string> Events, string? ErrorCode = null,
    WorkflowIdentity? Identity = null, bool PhysicalAlarmRaised = false,
    bool AutomaticContinuationAllowed = false, string? PhysicalRestrictionCode = null,
    string RecipeState = "Unmatched", string QualityState = "NotEvaluated",
    string SortingState = "NotStarted", string WholeTaskState = "NotCompleted",
    string? PlanRevision = null, string? RecipeBindingReference = null,
    Guid? HandoffId = null, Guid? WholeTrayCompletionId = null,
    Guid? ReadyForUnlockSourceMatrixId = null, Guid? UnlockObservedEventId = null,
    StartupDiagnostic? StartupDiagnostic = null)
{
    public Guid? ReadyForRemovalSourceMatrixId { get; init; }
    public Guid? ManualRemovalAllowedEventId { get; init; }
    public RecipeSelectionProjection? RecipeSelection { get; init; }
    public RecipeExecutionProjection? RecipeExecution { get; init; }
    public ExecutionPhaseProjection? ExecutionPhase { get; init; }
    public IReadOnlyList<AxisObservationProjection> AxisObservations { get; init; } = [];
    public IReadOnlyList<SlotStateProjection> SlotStates { get; init; } = [];
    public IReadOnlyList<int>? AbnormalPhysicalSlotIndices { get; init; }
    public ObservationCoverageProjection ObservationCoverage { get; init; } = new("NotObserved", null);
    public IReadOnlyList<string> AllowedActions { get; init; } = [];
    public ManualFlipProjection? WaitingManualFlip { get; init; }
    public FailedCommandRecoveryProjection? FailedCommandRecovery { get; init; }
    public IReadOnlyList<RunResultProjection> Results { get; init; } = [];
    public string ExecutionState => State.ToString();
    public string DeviceSchemaVersion { get; init; } = "device-semantics/1";
    public string ResultSchemaVersion { get; init; } = "station01-result-display/1.0";
    public string? ResultRevision { get; init; }
    public ResultContextProjection? ResultContext { get; init; }
    public FaultRestartProjection? FaultRestart { get; init; }
    public CommissioningRecoveryProjection? CommissioningRecovery { get; init; }
    public IReadOnlyList<RunMovementProjection> Movements { get; init; } = [];
    public TrayAnomalyDecisionProjection? TrayAnomalyDecision { get; init; }
    public string? TrayEndReason { get; init; }
    public bool? InspectionCompleted { get; init; }

    public RunSnapshot Next(RunState next)
    {
        if (RunStateRules.IsTerminal(State) && next != State)
            throw new InvalidOperationException("最终运行状态不可复活");
        return this with { State = next, ObservedRevision = ObservedRevision + 1 };
    }

    public RunSnapshot RestrictPhysical(string code) => this with
    {
        State = RunState.Restricted,
        PhysicalAlarmRaised = true,
        AutomaticContinuationAllowed = false,
        PhysicalRestrictionCode = code,
        ErrorCode = code,
        ObservedRevision = ObservedRevision + 1,
        Events = [..Events, "PhysicalRestricted:" + code]
    };
}

public sealed record CommissioningRecoveryProjection(Guid RecoveryWriteId, Guid ResetId, string Status);

public sealed record TrayAnomalyItem(int PhysicalSlotIndex, string CellId, string Region,
    int Row, int Column, string Type, string Reason);
public sealed record TrayAnomalyDecisionProjection(Guid DecisionId, Guid RunId, Guid ObservationId,
    int CheckRound, DateTimeOffset CreatedUtc, DateTimeOffset DeadlineUtc,
    IReadOnlyList<TrayAnomalyItem> Items, string State, string? Choice = null,
    string? ChoiceSource = null, string? OperatorId = null, string? EvidenceReference = null);

public sealed record RecipeSelectionProjection(string RecipeId, string Version,
    string CatalogDigest, string ScenarioId, string Purpose);

public sealed record RecipeExecutionProjection(string RecipeId, string Version,
    string CatalogDigest, string PlanRevision, string ScenarioId, string? Route)
{
    public string RecipeVersion => Version;
    public string? DefinitionDigest { get; init; }
    public string? Model { get; init; }
    public string? FCode { get; init; }
    public string? SnapshotRef { get; init; }
    public string? Stage { get; init; }
    public ExecutionPhaseProjection? ExecutionPhase { get; init; }
}

public sealed record ExecutionPhaseProjection(string Kind, string State, int? StepSequence = null,
    Guid? TransitionId = null, string? EntityId = null, int? PhysicalSlotIndex = null,
    int? LocalFace = null, string? ScanPoseId = null, string? ObservationRef = null,
    string? EvidenceRef = null)
{
    public string? StageId { get; init; }
    public string? CellId { get; init; }
}
public sealed record SlotStateProjection(int PhysicalSlotIndex, string Presence, string PoseState,
    string Participation, string? ObservationRef, IReadOnlyList<string> EntityRefs, IReadOnlyList<string> ReasonCodes)
{
    public string? DetectionState { get; init; }
    public string? PhysicalDisposition { get; init; }
    public string? SortingEvidenceRef { get; init; }
    public string? CellId { get; init; }
    public string? Region { get; init; }
    public int? Row { get; init; }
    public int? Column { get; init; }
    public int? RegionOrdinal { get; init; }
}
public sealed record ObservationCoverageProjection(string State, string? LastObservationRef);
public sealed record AxisObservationProjection(string Axis, double? Position, string? Unit,
    string Reliability, DateTimeOffset? ObservedAt, long? ConnectionEpoch, string? EvidenceRef);

public sealed record RunResultProjection(string Kind, string Id, string? Disposition,
    string? ResultReference, string PlanRevision, Guid? CommittedEventId,
    string? Source, string? Quality)
{
    public string? ParentId { get; init; }
    public string? StageId { get; init; }
    public int? LocalFace { get; init; }
    public string Completeness { get; init; } = "Unknown";
    public string Availability { get; init; } = "Committed";
    public string SaveState { get; init; } = "Committed";
    public long? CommittedRevision { get; init; }
    public IReadOnlyList<string> ReasonCodes { get; init; } = [];
    public IReadOnlyList<string>? RequiredTargetRefs { get; init; }
    public IReadOnlyList<string>? CompletedTargetRefs { get; init; }
    public string? DispositionState { get; init; }
    public int? PhysicalSlotIndex { get; init; }
    public string? PoseState { get; init; }
    public string? Participation { get; init; }
    public IReadOnlyList<InspectionResultProjection> Inspections { get; init; } = [];
}

public sealed record RunMovementProjection(string EntityId, int PhysicalSlotIndex,
    string SourcePointRef, string TargetPointRef, string State, Guid OperationId,
    Guid CommittedEventId)
{
    public bool IsPosePending { get; init; }
    public string? DetectionState { get; init; }
    public string? PhysicalDisposition { get; init; }
    public string? Reason { get; init; }
    public string? TransferPurpose { get; init; }
    public string? OriginalCellId { get; init; }
    public bool? SafeConfirmed { get; init; }
}

public sealed record StartupDiagnostic(
    IReadOnlyList<string> ReasonCodes, string SafetyAssessment, string StopStage,
    string Disposition, long? ConnectionEpoch, DateTimeOffset? ObservedAtUtc,
    ExecutionOrigin ExecutionOrigin, DeviceObservation? SemanticObservation = null,
    DiagnosticEvidenceReference? DiagnosticEvidenceReference = null);

public sealed record ManualFlipProjection(string EntityId, int StepSequence,
    int TargetFace, DateTimeOffset DeadlineUtc, string FaceSource);
public sealed record FailedCommandRecoveryProjection(Guid OperationId, Guid ActionId,
    long FailedEpoch, string Role, DateTimeOffset DeadlineUtc, long? ResetEpoch, Guid? CheckId);

public sealed record ResultContextProjection(string Kind, string Id, int? LocalFace, int? StepSequence);
public sealed record InspectionParameterProjection(string Name, object Value, string Kind, string Reference, string? Unit = null);
public sealed record DetailAvailabilityProjection(string Parameters, string Defects, string Confidence, string Measurement);
public sealed record InspectionResultProjection(string InspectionId, int? StepSequence, int? LocalFace,
    string? BusinessCamera, Guid? CaptureId, Guid? CallId, IReadOnlyList<Guid> MediaIds,
    string? ItemId, string? ItemName, string? Rule, string? TechnicalState, string? Disposition,
    IReadOnlyList<string> ReasonCodes, IReadOnlyList<InspectionParameterProjection> Parameters,
    DetailAvailabilityProjection DetailAvailability, string ResultReference, Guid CommittedEventId,
    long CommittedRevision, string Source, string Quality)
{
    public string? StageId { get; init; }
    public IReadOnlyList<Guid> InputCallIds { get; init; } = [];
    public object? MeasuredValue { get; init; }
    public string? Unit { get; init; }
    public object? Defects { get; init; }
    public double? Confidence { get; init; }
    public string? ConfidenceUnit { get; init; }
}

public sealed record FaultRestartProjection(Guid FaultRunId, Guid? ResetId, Guid? InitialCheckId,
    string Status, Guid? NewRunId, IReadOnlyList<string> BlockedReasons, long CommittedRevision);
