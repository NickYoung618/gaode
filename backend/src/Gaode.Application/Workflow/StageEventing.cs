using Gaode.Application.Ports;

namespace Gaode.Application.Workflow;

public enum StageEventType
{
    IntentRecorded,
    Started,
    Accepted,
    Executing,
    AttemptFailed,
    RetryScheduled,
    PendingRecorded,
    Completed,
    Failed,
    TimedOut,
    Disconnected,
    MappingFailed,
    ManualReviewRequested,
    ManualReviewConfirmed,
    RecoveryResumed,
    WholeTrayCompleted,
    UnlockRequested,
    ObservedUnlocked,
    ManualTrayRemovalConfirmed,
    FinalUnloadCompleted,
    UnknownHeld,
    ManualRemovalAllowed,
    AlgorithmLifecycleRecorded
}

public enum StageProjectionStatus
{
    NotStarted,
    Started,
    Accepted,
    Executing,
    Completed,
    Failed,
    TimedOut,
    Disconnected,
    UnknownHeld,
    PausedForManualReview
}

public enum StageEventCommitState { Committed, Replay, Conflict }

// Commit outcome and delivery of a usable receipt are different facts.
public sealed class StageEventCommitException(Guid eventId, ActualCommitState actualCommit, Exception cause)
    : Exception("StageEventCommitUnconfirmed", cause)
{
    public Guid EventId { get; } = eventId;
    public ActualCommitState ActualCommit { get; } = actualCommit;
}

public sealed record StageEvent(
    Guid EventId,
    Guid RunId,
    Guid TrayId,
    string StationId,
    string LineId,
    WholeTrayWorkflowStage Stage,
    Guid OperationId,
    int Attempt,
    long ConnectionEpoch,
    StageEventType EventType,
    DateTimeOffset OccurredAt,
    DateTimeOffset PersistedAt,
    ResultSource Source,
    ResultQuality Quality,
    string? ErrorCode,
    string PayloadDigest,
    string PayloadJson,
    string IdempotencyKey,
    long Sequence,
    DateTimeOffset RetainUntil,
    string PlanRevision = "",
    DateTimeOffset? StageStartedAtUtc = null,
    DateTimeOffset? StageDeadlineAtUtc = null);

public sealed record StageAttemptEvidence(
    int Attempt,
    DateTimeOffset StageStartedAt,
    DateTimeOffset StageDeadlineAt,
    DateTimeOffset? PlannedAt,
    string? ErrorCode,
    Guid OperationId,
    IReadOnlyList<string> EvidenceReferences)
{
    public bool IsValid => Attempt >= 1 && StageStartedAt < StageDeadlineAt &&
        (PlannedAt is null || PlannedAt < StageDeadlineAt) && OperationId != Guid.Empty &&
        EvidenceReferences.All(x => !string.IsNullOrWhiteSpace(x));
}

public sealed record StageProjection(
    Guid RunId,
    Guid TrayId,
    string StationId,
    string LineId,
    WholeTrayWorkflowStage Stage,
    long Revision,
    StageProjectionStatus Status,
    Guid? CurrentOperationId,
    long ConnectionEpoch,
    bool DeviceHeld,
    bool NeedsManualReview,
    Guid? LastEventId,
    DateTimeOffset UpdatedAt,
    DateTimeOffset RetainUntil)
{
    public bool IsTerminal => Status is StageProjectionStatus.Completed or StageProjectionStatus.Failed or
        StageProjectionStatus.TimedOut or StageProjectionStatus.Disconnected or StageProjectionStatus.UnknownHeld;
    public bool AutomaticRetryAllowed => !DeviceHeld && Status != StageProjectionStatus.UnknownHeld;
}

public sealed record StageEventAppendRequest(
    Guid EventId,
    Guid RunId,
    Guid TrayId,
    string StationId,
    string LineId,
    WholeTrayWorkflowStage Stage,
    Guid OperationId,
    int Attempt,
    long ConnectionEpoch,
    StageEventType EventType,
    DateTimeOffset OccurredAt,
    ResultSource Source,
    ResultQuality Quality,
    string? ErrorCode,
    string PayloadDigest,
    string PayloadJson,
    string IdempotencyKey,
    string PlanRevision = "",
    DateTimeOffset? StageStartedAtUtc = null,
    DateTimeOffset? StageDeadlineAtUtc = null)
{
    public bool IsValid => EventId != Guid.Empty && RunId != Guid.Empty && TrayId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(StationId) && !string.IsNullOrWhiteSpace(LineId) &&
        OperationId != Guid.Empty && Attempt >= 1 && ConnectionEpoch >= 0 &&
        !string.IsNullOrWhiteSpace(PayloadDigest) && PayloadJson is not null &&
        !string.IsNullOrWhiteSpace(IdempotencyKey);
}

public sealed record StageEventAppendResult(
    StageEventCommitState State,
    StageEvent Event,
    StageProjection Projection,
    string? ErrorCode)
{
    public bool IsCommitted => State is StageEventCommitState.Committed or StageEventCommitState.Replay;
}

public interface IStageEventStore
{
    Task<StageEventAppendResult> AppendAsync(StageEventAppendRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StageEvent>> ReadAsync(Guid runId, Guid trayId,
        WholeTrayWorkflowStage stage, CancellationToken cancellationToken = default);

    Task<StageProjection?> GetProjectionAsync(Guid runId, Guid trayId,
        WholeTrayWorkflowStage stage, CancellationToken cancellationToken = default);

    Task<StageProjection> RecoverAsync(Guid runId, Guid trayId, WholeTrayWorkflowStage stage,
        CancellationToken cancellationToken = default);
}

public sealed class StageDispatchGate(IStageEventStore store)
{
    public async Task<StageEventAppendResult> CommitIntentThenDispatchAsync(
        StageEventAppendRequest intent, Func<CancellationToken, Task> dispatch,
        CancellationToken cancellationToken = default)
    {
        var result = await store.AppendAsync(intent, cancellationToken);
        if (result.State == StageEventCommitState.Conflict)
            throw new InvalidOperationException("IdempotencyConflict");
        if (result.State == StageEventCommitState.Committed)
            await dispatch(cancellationToken);
        return result;
    }
}

public static class StageEventProjection
{
    public static StageProjection Initial(StageEvent e) => new(e.RunId, e.TrayId, e.StationId,
        e.LineId, e.Stage, 0, StageProjectionStatus.NotStarted, null,
        e.EventType == StageEventType.AlgorithmLifecycleRecorded ? 0 : e.ConnectionEpoch,
        false, false, e.EventType == StageEventType.AlgorithmLifecycleRecorded ? null : e.EventId,
        e.EventType == StageEventType.AlgorithmLifecycleRecorded ? default : e.PersistedAt, e.RetainUntil);

    public static StageProjection Apply(StageProjection current, StageEvent e)
    {
        if (e.Sequence <= current.Revision) return current;
        if (e.EventType == StageEventType.AlgorithmLifecycleRecorded)
            return current with { Revision = e.Sequence,
                RetainUntil = current.RetainUntil > e.RetainUntil ? current.RetainUntil : e.RetainUntil };
        var status = e.EventType switch
        {
            StageEventType.Started => StageProjectionStatus.Started,
            StageEventType.Accepted => StageProjectionStatus.Accepted,
            StageEventType.Executing => StageProjectionStatus.Executing,
            StageEventType.Completed => StageProjectionStatus.Completed,
            StageEventType.Failed => StageProjectionStatus.Failed,
            StageEventType.TimedOut => StageProjectionStatus.TimedOut,
            StageEventType.Disconnected => StageProjectionStatus.Disconnected,
            StageEventType.UnknownHeld => StageProjectionStatus.UnknownHeld,
            StageEventType.MappingFailed or StageEventType.ManualReviewRequested =>
                StageProjectionStatus.PausedForManualReview,
            StageEventType.ManualReviewConfirmed or StageEventType.RecoveryResumed =>
                StageProjectionStatus.Started,
            StageEventType.UnlockRequested => StageProjectionStatus.Started,
            StageEventType.ObservedUnlocked or StageEventType.ManualRemovalAllowed or StageEventType.FinalUnloadCompleted =>
                StageProjectionStatus.Completed,
            _ => current.Status
        };
        // An ordinary failure cannot erase an earlier unknown physical occupation.
        if (current.DeviceHeld && current.Status == StageProjectionStatus.UnknownHeld &&
            e.EventType is StageEventType.Failed or StageEventType.TimedOut or StageEventType.Disconnected)
            status = StageProjectionStatus.UnknownHeld;
        var held = status == StageProjectionStatus.UnknownHeld ||
            (status is not StageProjectionStatus.Completed and not StageProjectionStatus.Failed and
             not StageProjectionStatus.TimedOut and not StageProjectionStatus.Disconnected && current.DeviceHeld);
        var review = status == StageProjectionStatus.PausedForManualReview || current.NeedsManualReview;
        if (e.EventType == StageEventType.ManualReviewConfirmed) review = false;
        return current with
        {
            Revision = e.Sequence,
            Status = status,
            CurrentOperationId = e.OperationId,
            ConnectionEpoch = e.ConnectionEpoch,
            DeviceHeld = held,
            NeedsManualReview = review,
            LastEventId = e.EventId,
            UpdatedAt = e.PersistedAt,
            RetainUntil = e.RetainUntil
        };
    }

    public static bool IsTerminal(StageEventType type) => type is StageEventType.Completed or
        StageEventType.Failed or StageEventType.TimedOut or StageEventType.Disconnected or
        StageEventType.UnknownHeld or StageEventType.ObservedUnlocked or StageEventType.ManualRemovalAllowed or
        StageEventType.FinalUnloadCompleted;
}

public static class StageEventRetentionPolicy
{
    public const int MinimumYears = 7;
    public static DateTimeOffset RetainUntil(DateTimeOffset persistedAt) => persistedAt.AddYears(MinimumYears);
}
