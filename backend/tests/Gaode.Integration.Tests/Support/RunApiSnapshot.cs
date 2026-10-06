using Gaode.Domain.Station01;
using Gaode.Host.Api;

namespace Gaode.Integration.Tests.Support;

// Consumer of the published run API. The versioned startup observation is the
// actual API type, not the different internal DeviceObservation record. No
// conversion reconstructs identities, raw values, sources or authorization.
public sealed record RunApiSnapshot(
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
    StartupDiagnosticApi? StartupDiagnostic = null)
{
    public Guid? ReadyForRemovalSourceMatrixId { get; init; }
    public Guid? ManualRemovalAllowedEventId { get; init; }

    public RecipeSelectionProjection? RecipeSelection { get; init; }
    public RecipeExecutionProjection? RecipeExecution { get; init; }
    public IReadOnlyList<string> AllowedActions { get; init; } = [];
    public ManualFlipProjection? WaitingManualFlip { get; init; }
    public FailedCommandRecoveryProjection? FailedCommandRecovery { get; init; }
    public IReadOnlyList<RunResultProjection> Results { get; init; } = [];
    public string ExecutionState { get; init; } = "";
    public string DeviceSchemaVersion { get; init; } = "device-semantics/1";
    public string ResultSchemaVersion { get; init; } = "station01-result-display/1.0";
    public string? ResultRevision { get; init; }
    public ResultContextProjection? ResultContext { get; init; }
    public FaultRestartProjection? FaultRestart { get; init; }
    public IReadOnlyList<RunMovementProjection> Movements { get; init; } = [];

}
