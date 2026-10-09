namespace Gaode.Application.Ports;

// Internal durable ownership facts, never an engine response or a motion completion.
public sealed record AlgorithmResourceState(Guid RunId, Guid TrayId, Guid CallId, Guid OperationId,
    string ClockId, string SnapshotId, IReadOnlyList<MediaRef> Inputs, int ReleaseBudgetMs)
{
    public string SchemaVersion { get; init; } = "pipeline/1";
    public int Revision { get; init; } = 1;
    public AlgorithmRequest? Request { get; init; }
    public Guid? WorkerSessionId { get; init; }
    public string Dispatch { get; init; } = "Registered";
    public string? TechnicalTerminal { get; init; }
    public bool BusinessEnded { get; init; }
    public bool InputsReleased { get; init; }
    public bool ExecutionEnded { get; init; }
    public bool DispatchReturned { get; init; }
    public bool CancelCallbacksEnded { get; init; } = true;
    public DateTimeOffset? ReleaseStartUtc { get; init; }
    public DateTimeOffset? ReleaseDueUtc { get; init; }
    public long? ReleaseStartTick { get; init; }
    public long? ReleaseDueTick { get; init; }
    public string? ReleaseTrigger { get; init; }
    public bool ObservationExpired { get; init; }
    public DateTimeOffset? HostShutdownStartUtc { get; init; }
    public DateTimeOffset? HostShutdownDueUtc { get; init; }
    public bool Reclaimed => BusinessEnded && InputsReleased && ExecutionEnded && DispatchReturned && CancelCallbacksEnded;
    public bool IsValid => SchemaVersion == "pipeline/1" && RunId != Guid.Empty && TrayId != Guid.Empty &&
        CallId != Guid.Empty && OperationId != Guid.Empty && Revision > 0 && ReleaseBudgetMs > 0 &&
        !string.IsNullOrWhiteSpace(ClockId) && !string.IsNullOrWhiteSpace(SnapshotId) && Inputs.Count > 0 &&
        Inputs.All(x => x.RunId == RunId && x.MediaId != Guid.Empty && x.CaptureId != Guid.Empty) &&
        (Request is null || Request.Envelope.IsValid && Request.CallId == CallId && Request.Envelope.RunId == RunId &&
            Request.Envelope.OperationId == OperationId && Request.Envelope.ClockId == ClockId && Request.Envelope.SnapshotId == SnapshotId &&
            Request.Inputs.SequenceEqual(Inputs)) &&
        ((ReleaseStartUtc is null && ReleaseDueUtc is null && ReleaseStartTick is null && ReleaseDueTick is null && ReleaseTrigger is null) ||
         (ReleaseStartUtc.HasValue && ReleaseDueUtc == ReleaseStartUtc.Value.AddMilliseconds(ReleaseBudgetMs) &&
          ReleaseStartTick.HasValue && ReleaseDueTick > ReleaseStartTick && !string.IsNullOrWhiteSpace(ReleaseTrigger)));
}

public interface IAlgorithmResourceStore
{
    Task AppendResourceAsync(AlgorithmResourceState state, CancellationToken token);
    Task<IReadOnlyList<AlgorithmResourceState>> GetUnreclaimedResourcesAsync(int offset, int limit, CancellationToken token);
}
