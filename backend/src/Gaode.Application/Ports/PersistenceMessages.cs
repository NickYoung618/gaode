using Gaode.Domain.Station01;
using System.Text.Json.Serialization;

namespace Gaode.Application.Ports;

public enum WriteKind { RunCreated, StartIntent, ActionIntent, ActionFact, CaptureIntent,
    CaptureFact, Media, AlgorithmIntent, AlgorithmFact, HandoffV2, Complete, Cancel, Audit }
public enum CommitState { Queued, Committed, Failed, CommitUnknown, ConditionRejected }

[JsonConverter(typeof(JsonStringEnumConverter<ActualCommitState>))]
public enum ActualCommitState { Unknown, ConfirmedRolledBack, Committed }
[JsonConverter(typeof(JsonStringEnumConverter<ReceiptValidity>))]
public enum ReceiptValidity { None, Invalid, ValidCurrent }
[JsonConverter(typeof(JsonStringEnumConverter<BusinessCommitRecordKind>))]
public enum BusinessCommitRecordKind { RunWrite, StageEvent, CommunicationEvidence }
public sealed record RequiredCommitEvidence(Guid WriteId, ActionCorrelation Correlation, ActualCommitState ActualCommit,
    ReceiptValidity Validity, long? PersistedRevision, DateTimeOffset? CommittedUtc,
    long? ReceivedTick, string? FailureReason)
{
    public BusinessCommitRecordKind RecordKind { get; init; }
    public string? SavePurpose { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<PhysicalPickState>))]
public enum PhysicalPickState { Unconfirmed, Observed }
[JsonConverter(typeof(JsonStringEnumConverter<PickCommitState>))]
public enum PickCommitState { Unknown, Rejected, Committed }

public sealed record PickCompletionEvidence(ActionCorrelation Correlation, string ObjectId,
    int PhysicalSlotIndex, string ReservationReference, string AssignmentDigest,
    FixedPointIdentity SourcePoint, FixedPointIdentity TargetPoint,
    PositionReachedEvidence SourcePositionReached, DateTimeOffset PickObservedUtc,
    ObservationIdentity Observation, ExecutionOrigin ExecutionOrigin,
    IReadOnlyList<DiagnosticEvidenceReference> DiagnosticEvidenceReferences, ActionWindow Window);
public sealed record FixedPointIdentity(string Id, string Version, string Digest);
public sealed record PickCommitReceipt(PickCommitState State, ActualCommitState ActualCommit,
    ReceiptValidity Validity, Guid? EventId, Guid? WriteId, ActionCorrelation Correlation,
    string ReservationReference, string AssignmentDigest,
    IReadOnlyList<DiagnosticEvidenceReference> DiagnosticEvidenceReferences,
    DateTimeOffset? CommittedUtc, long? PersistedRevision, DateTimeOffset ReceivedUtc,
    long ReceivedTick, ActionWindow Window, string? FailureReason)
{
    public bool MayAuthorizePlace(PickCompletionEvidence evidence, long now) =>
        State == PickCommitState.Committed && ActualCommit == ActualCommitState.Committed &&
        Validity == ReceiptValidity.ValidCurrent && EventId is { } eventId && eventId != Guid.Empty &&
        WriteId is { } writeId && writeId != Guid.Empty && CommittedUtc is not null && PersistedRevision is >= 0 &&
        Correlation == evidence.Correlation && ReservationReference == evidence.ReservationReference &&
        AssignmentDigest == evidence.AssignmentDigest && Window.Contains(ReceivedTick) && Window.Contains(now) &&
        DiagnosticEvidenceReferences.Count > 0 && DiagnosticEvidenceReferences.SequenceEqual(evidence.DiagnosticEvidenceReferences);
}

public sealed record PickEvidenceFailureNotice(ActionCorrelation Correlation, string ReservationReference,
    PhysicalPickState PhysicalPick, PositionReachedEvidence? SourcePositionReached,
    DateTimeOffset? PickObservedUtc, ObservationIdentity? Observation, ExecutionOrigin ExecutionOrigin,
    ActualCommitState ActualCommit, ReceiptValidity ReceiptValidity, string Reason)
{
    public bool HoldsDevice => true;
    public bool CanRetry => false;
}

public interface IPickCommitPort
{
    Task<PickCommitReceipt> CommitPickAsync(PickCompletionEvidence evidence, CancellationToken cancellationToken);
    Task<RequiredCommitEvidence> ReportPickEvidenceFailureAsync(PickEvidenceFailureNotice notice,
        CancellationToken cancellationToken);
}

public sealed record WriteBatch(Guid WriteId, Guid RunId, long ExpectedRevision,
    WriteKind Kind, string PayloadJson, string PayloadDigest, RunState? StateAfter = null,
    TerminalOutcome ExpectedTerminal = TerminalOutcome.None,
    TerminalOutcome CandidateTerminal = TerminalOutcome.None,
    string? HandoffJson = null, string? RequestId = null, string? SubjectId = null,
    string? ContextJson = null, Guid? HandoffId = null,
    Guid? TrayId = null, string? HandoffV2Json = null);

public sealed record CommitReceipt(Guid WriteId, Guid RunId, CommitState State,
    long? CommittedRevision, TerminalOutcome ActualTerminal, string? ErrorCode, DateTimeOffset? CommittedUtc = null)
{
    public BusinessCommitRecordKind RecordKind { get; init; }
}
public sealed record QueuedWrite(CommitReceipt QueuedReceipt, Task<CommitReceipt> Completion);

public sealed record PersistedRun(Guid RunId, string RequestId, string SubjectId,
    string ContextJson, RunState State, long Revision, TerminalOutcome Terminal,
    long? TerminalRevision, bool CancelRequested);
public sealed record PersistedWrite(Guid WriteId, Guid RunId, long Revision,
    WriteKind Kind, string PayloadJson, string PayloadDigest, CommitState State);
public sealed record PersistedHandoff(Guid HandoffId, Guid RunId, string Json,
    long Revision, TerminalOutcome Terminal, Guid WriteId);

public sealed record AlgorithmIntentPayload(Guid CallId, Guid OperationId, int Attempt,
    Guid RunId, Guid CaptureId, IReadOnlyList<Guid> InputMediaIds,
    string PublicVersion, string ScopeVersion, string ParametersVersion,
    string CapabilityId, string CapabilityVersion, string ExpectedComponentVersion,
    Guid SessionId, string ClockId, long StartTick, long DueTick,
    int BudgetMs, string InvocationBasis);

public sealed record RunCreatedPayload(Guid CommandId, string RequestId,
    string SubjectId, string ContextJson, string PublicJson, string BudgetJson,
    string SimulationJson, string SnapshotId, string PublicDigest,
    string BudgetDigest, string SimulationDigest)
{
    public string? CanonicalRequest { get; init; }
}

public sealed record OperationIntentPayload(Guid OperationId, string Kind,
    int Attempt, Guid? ActionId, Guid? CaptureId, string TargetOrScope,
    string SnapshotId, Guid IntentWriteId);

public sealed record AlgorithmFactPayload(Guid CallId, AlgorithmState State,
    bool ResponseReceived, string RawResultJson, string Decision,
    string DispatchEvidence = "NotDispatched", long DispatchTick = 0,
    long? AcceptedTick = null, Guid? WorkerSessionId = null)
{
    public Gaode.Application.Recipes.DecodedTrayCode? DecodedCode { get; init; }
    public ComponentExecutionOrigin Origin { get; init; } = ComponentExecutionOrigin.Unknown;
    public Guid CaptureId { get; init; }
    public Guid RunId { get; init; }
}

public static class PersistenceContract
{
    public static bool IsValid(WriteBatch batch)
    {
        if (batch.WriteId == Guid.Empty || batch.RunId == Guid.Empty || batch.ExpectedRevision < 0 ||
            string.IsNullOrWhiteSpace(batch.PayloadJson) || string.IsNullOrWhiteSpace(batch.PayloadDigest))
            return false;
        if (batch.Kind == WriteKind.Complete)
            return batch.CandidateTerminal is TerminalOutcome.Completed or TerminalOutcome.CompletedWithExceptions &&
                batch.HandoffId is not null && !string.IsNullOrWhiteSpace(batch.HandoffJson);
        if (batch.Kind == WriteKind.HandoffV2)
            return batch.CandidateTerminal == TerminalOutcome.None && batch.HandoffJson is null &&
                batch.HandoffId is not null && batch.TrayId is not null &&
                !string.IsNullOrWhiteSpace(batch.HandoffV2Json);
        if (batch.Kind == WriteKind.Cancel)
            return batch.CandidateTerminal == TerminalOutcome.Cancelled && batch.HandoffJson is null;
        return batch.CandidateTerminal == TerminalOutcome.None && batch.HandoffJson is null &&
            batch.HandoffV2Json is null;
    }
}
