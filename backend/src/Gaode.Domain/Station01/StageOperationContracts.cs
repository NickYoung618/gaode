namespace Gaode.Domain.Station01;

public enum IdempotencyDecision { New, Replay, Conflict }
public enum TrayEndReason { NormalCompletion, ManualIntervention, EmptyTray }

public sealed record StageOperationIdentity(
    Guid RunId,
    Guid TrayId,
    Guid StationId,
    Guid LineId,
    string Stage,
    Guid OperationId,
    int Attempt,
    string PayloadDigest)
{
    public bool IsValid => RunId != Guid.Empty && TrayId != Guid.Empty &&
        StationId != Guid.Empty && LineId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(Stage) && OperationId != Guid.Empty &&
        Attempt >= 1 && !string.IsNullOrWhiteSpace(PayloadDigest);
}

public sealed class StageIdempotencyRegistry
{
    private readonly object gate = new();
    private readonly Dictionary<string, string> fingerprints = new(StringComparer.Ordinal);

    public IdempotencyDecision Register(string idempotencyKey, string payloadDigest)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key is required.", nameof(idempotencyKey));
        if (string.IsNullOrWhiteSpace(payloadDigest))
            throw new ArgumentException("Payload digest is required.", nameof(payloadDigest));
        lock (gate)
        {
            if (!fingerprints.TryGetValue(idempotencyKey, out var previous))
            {
                fingerprints[idempotencyKey] = payloadDigest;
                return IdempotencyDecision.New;
            }
            return previous == payloadDigest ? IdempotencyDecision.Replay : IdempotencyDecision.Conflict;
        }
    }
}

public sealed record WholeTrayCompletionReference(
    Guid CompletionId,
    Guid RunId,
    Guid TrayId,
    Guid StationId,
    Guid LineId,
    string PlanRevision,
    Guid DetectionCompletedEventId,
    Guid SortingCompletedEventId,
    Guid UnloadPreparationCompletedEventId)
{
    public TrayEndReason EndReason { get; init; } = TrayEndReason.NormalCompletion;
    public bool? InspectionCompleted { get; init; }
    public string? RecipePlanRevision { get; init; }
    public string? EndBasisReference { get; init; }
    public bool IsValid => CompletionId != Guid.Empty && RunId != Guid.Empty &&
        TrayId != Guid.Empty && StationId != Guid.Empty && LineId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(PlanRevision) &&
        Enum.IsDefined(EndReason) && (EndReason == TrayEndReason.NormalCompletion
            ? DetectionCompletedEventId != Guid.Empty && SortingCompletedEventId != Guid.Empty
            : InspectionCompleted == false && !string.IsNullOrWhiteSpace(EndBasisReference)) &&
        UnloadPreparationCompletedEventId != Guid.Empty;
}

public sealed record FinalUnloadCompletion(
    Guid FinalCompletionId,
    WholeTrayCompletionReference WholeTray,
    Guid ManualRemovalAllowedEventId,
    string OperatorId,
    DateTimeOffset ConfirmedAtUtc,
    string ConfirmationStage,
    bool Confirmed,
    string Reason,
    Guid FinalSourceMatrixId)
{
    // Only populated when reading an older final record; never grants current execution.
    public Guid? UnlockObservedEventId { get; init; }
    public bool IsValid => FinalCompletionId != Guid.Empty && WholeTray.IsValid &&
        ManualRemovalAllowedEventId != Guid.Empty && !string.IsNullOrWhiteSpace(OperatorId) &&
        ConfirmedAtUtc > DateTimeOffset.MinValue &&
        ConfirmationStage == "ManualTrayRemovalConfirmation" && Confirmed &&
        !string.IsNullOrWhiteSpace(Reason) && FinalSourceMatrixId != Guid.Empty;
}
