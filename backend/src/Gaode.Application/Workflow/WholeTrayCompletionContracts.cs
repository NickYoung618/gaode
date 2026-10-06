using Gaode.Application.Ports;
using Gaode.Domain.Station01;

namespace Gaode.Application.Workflow;

/// <summary>
/// Immutable, persisted completion evidence.  It is deliberately separate from a
/// projection: a manual-removal allowance must reference this record, never a Boolean or a
/// plan that happens to have finished executing.
/// </summary>
public sealed record WholeTrayCompletionRecord(
    WholeTrayCompletionReference Reference,
    ComponentEvidenceMatrix SourceMatrix,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset PersistedAtUtc,
    string RecordDigest,
    DateTimeOffset RetainUntilUtc)
{
    public bool IsValid => Reference.IsValid && SourceMatrix.IsComplete &&
        SourceMatrix.Milestone == EvidenceMilestone.ReadyForRemoval &&
        !string.IsNullOrWhiteSpace(RecordDigest) &&
        PersistedAtUtc >= CreatedAtUtc && RetainUntilUtc >= PersistedAtUtc.AddYears(7);
}

public sealed record WholeTrayCompletionCreateRequest(
    Guid CompletionId,
    Guid RunId,
    Guid TrayId,
    Guid StationId,
    Guid LineId,
    string PlanRevision,
    ComponentEvidenceMatrix SourceMatrix,
    DateTimeOffset CreatedAtUtc,
    string IdempotencyKey)
{
    public TrayEndReason EndReason { get; init; } = TrayEndReason.NormalCompletion;
    public bool InspectionCompleted { get; init; } = true;
    public string? RecipePlanRevision { get; init; }
    public string? EndBasisReference { get; init; }
    public bool IsValid => CompletionId != Guid.Empty && RunId != Guid.Empty && TrayId != Guid.Empty &&
        StationId != Guid.Empty && LineId != Guid.Empty && !string.IsNullOrWhiteSpace(PlanRevision) &&
        SourceMatrix is not null && SourceMatrix.RunId == RunId && SourceMatrix.TrayId == TrayId &&
        SourceMatrix.PlanRevision == PlanRevision &&
        SourceMatrix.Milestone == EvidenceMilestone.ReadyForRemoval &&
        CreatedAtUtc > DateTimeOffset.MinValue && !string.IsNullOrWhiteSpace(IdempotencyKey) &&
        Enum.IsDefined(EndReason) && (EndReason == TrayEndReason.NormalCompletion ||
            !InspectionCompleted && !string.IsNullOrWhiteSpace(EndBasisReference));
}

public sealed record ManualTrayRemovalConfirmationRequest(
    Guid FinalCompletionId,
    WholeTrayCompletionReference WholeTray,
    Guid ManualRemovalAllowedEventId,
    string OperatorId,
    DateTimeOffset ConfirmedAtUtc,
    string Reason,
    string IdempotencyKey,
    ComponentEvidence ManualActorEvidence)
{
    public bool IsValid => FinalCompletionId != Guid.Empty && WholeTray.IsValid &&
        ManualRemovalAllowedEventId != Guid.Empty && !string.IsNullOrWhiteSpace(OperatorId) &&
        ConfirmedAtUtc > DateTimeOffset.MinValue && !string.IsNullOrWhiteSpace(Reason) &&
        !string.IsNullOrWhiteSpace(IdempotencyKey) && ManualActorEvidence is
        { Component: ComponentKind.ManualActor, Source: ComponentEvidenceSource.AuthenticatedHuman or
            ComponentEvidenceSource.Test } &&
        ManualActorEvidence.IsVerifiable;
}

public interface IWholeTrayCompletionStore
{
    Task<WholeTrayCompletionRecord> CreateAsync(WholeTrayCompletionCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<WholeTrayCompletionRecord?> GetAsync(WholeTrayCompletionReference reference,
        CancellationToken cancellationToken = default);

    Task<WholeTrayCompletionRecord?> GetByRunAsync(Guid runId,
        CancellationToken cancellationToken = default);

    Task<FinalUnloadCompletion> ConfirmManualRemovalAsync(ManualTrayRemovalConfirmationRequest request,
        CancellationToken cancellationToken = default);
}
