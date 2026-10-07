using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Application.Recipes;

namespace Gaode.Application.Ports;

public sealed record PortEnvelope(Guid RunId, Guid OperationId, int Attempt, Guid SessionId,
    string SnapshotId, string ConfigVersion, string Purpose, long StartTick,
    long DueTick, string ClockId)
{
    public bool IsValid => RunId != Guid.Empty && OperationId != Guid.Empty &&
        Attempt >= 1 && SessionId != Guid.Empty && !string.IsNullOrWhiteSpace(SnapshotId) &&
        !string.IsNullOrWhiteSpace(ConfigVersion) && !string.IsNullOrWhiteSpace(ClockId) &&
        Purpose is "Test" or "Production" && DueTick > StartTick;
}

public enum DeviceEventKind { Accepted, Executing, Completed, Failed, UnknownHeld, ButtonPressed, ClampStarted, ClampCompleted, Stopped }
public sealed record DeviceEvent(PortEnvelope Envelope, DeviceEventKind Kind,
    Guid ActionId, long ConnectionEpoch, string? ErrorCode = null,
    DateTimeOffset? DeviceObservedUtc = null, DeviceActionEvidence? Evidence = null,
    ExecutionOrigin? ExecutionOrigin = null);

public sealed record MoveRequest(PortEnvelope Envelope, Guid ActionId, FixedPoint Target,
    Guid IntentWriteId, string BindingId, string Role = "3D")
{
    public FlipMovePreparation? FlipPreparation { get; init; }
}

public sealed record FlipMovePreparation(Guid TransitionId, string Model, RecipeTargetPose TargetPose,
    string PhysicalEntityId, int PhysicalSlotIndex);

public sealed record FlipRequest(ActionCorrelation Correlation, Guid TransitionId, string Model, RecipeTargetPose TargetPose,
    PositionReachedEvidence PositionEvidence, ActionWindow Window, Guid IntentWriteId);
public sealed record PutBackRequest(ActionCorrelation Correlation, Guid TransitionId,
    PositionReachedEvidence PositionEvidence, ActionWindow Window, Guid IntentWriteId);
public interface IPhysicalHandlingPort
{
    Task<DeviceActionEvidence> FlipAsync(FlipRequest request, CancellationToken cancellationToken);
    Task<DeviceActionEvidence> PutBackAsync(PutBackRequest request, CancellationToken cancellationToken);
}

public static class DeviceContract
{
    public static bool Matches(DeviceEvent e, PortEnvelope expected, long epoch) =>
        e.Envelope.IsValid && e.Envelope.RunId == expected.RunId &&
        e.Envelope.OperationId == expected.OperationId && e.Envelope.Attempt == expected.Attempt &&
        e.Envelope.SessionId == expected.SessionId && e.Envelope.SnapshotId == expected.SnapshotId &&
        e.ConnectionEpoch == epoch;
}
