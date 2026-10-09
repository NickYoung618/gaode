using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using System.Text.Json.Serialization;

namespace Gaode.Application.Ports;

[JsonConverter(typeof(JsonStringEnumConverter<AcquisitionState>))]
public enum AcquisitionState { Requested, CaptureAllowed, WorkCommitted, Released, Blocked, HeldUnknown }

public sealed record CaptureWindowRequest(ActionCorrelation Correlation, CaptureRole Role,
    FixedPoint Target, PositionReachedEvidence PositionEvidence, ActionWindow Window);
public sealed record AcquisitionSession(Guid SessionId, CaptureWindowRequest Request,
    AcquisitionState State, DeviceActionEvidence Evidence);
public sealed record CaptureWorkCommit(CaptureCompletionEvidence Completion)
{
    public bool IsFor(AcquisitionSession session) => Completion.IsFor(session);
}
public sealed record CaptureCycleResult(AcquisitionSession Session, AcquisitionState State,
    DeviceActionEvidence? Evidence, string? FailureReason);

public interface IAcquisitionCyclePort
{
    ValueTask<AcquisitionSession> OpenCaptureWindowAsync(CaptureWindowRequest request,
        CancellationToken cancellationToken);
    Task<CaptureCycleResult> FinishCaptureWindowAsync(AcquisitionSession session,
        CaptureWorkCommit work, ActionWindow releaseWindow, CancellationToken cancellationToken);
    // Only the existing public-3D failure cleanup may use this. It never authorizes another action.
    Task<CaptureCycleResult> CloseFailedCaptureWindowAsync(AcquisitionSession session,
        string failureReason, ActionWindow cleanupWindow, CancellationToken cancellationToken);
}
