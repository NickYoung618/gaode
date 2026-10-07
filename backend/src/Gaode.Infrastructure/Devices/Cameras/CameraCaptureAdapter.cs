using System.Collections.Concurrent;
using Gaode.Application.Ports;
using Gaode.Domain.Station01;
using Gaode.Diagnostics;

namespace Gaode.Infrastructure.Devices.Cameras;

/// <summary>Translates the persistent camera gateway into application evidence; no light, PLC or algorithm calls.</summary>
public sealed class CameraCaptureAdapter(ICameraSdkGateway camera) : ICapturePort
{
    private readonly ConcurrentDictionary<CaptureRole, int> _counts = new();
    public string MediaSource => "RealCamera";
    public ComponentExecutionOrigin CameraOrigin => new(ComponentEvidenceSource.Real,
        typeof(CameraCaptureAdapter).Assembly.FullName!, "RawCapture");
    public ComponentExecutionOrigin LightOrigin => ComponentExecutionOrigin.Unknown;
    public long ConnectionEpoch => camera.ConnectionEpoch;
    public long GetConnectionEpoch(string binding) => camera.GetConnectionEpoch(binding);
    public long GetMaxCaptureBytes(string binding, long fallback) => camera.GetMaxCaptureBytes(binding, fallback);
    public int TriggerCount(CaptureRole role) => _counts.GetValueOrDefault(role);

    public async ValueTask RequestCaptureAsync(CaptureRequest request, Action<CaptureEvent> onEvent, CancellationToken ct)
    {
        if (!request.Envelope.IsValid || request.CaptureId == Guid.Empty || request.IntentWriteId == Guid.Empty ||
            request.MaxBytes <= 0 || string.IsNullOrWhiteSpace(request.CameraBindingId))
            throw new ArgumentException("CameraCaptureRequestInvalid");
        var epoch = GetConnectionEpoch(request.CameraBindingId);
        try
        {
            onEvent(new(request, CaptureEventKind.Accepted, epoch));
            onEvent(new(request, CaptureEventKind.Capturing, epoch));
            _counts.AddOrUpdate(request.Role, 1, (_, count) => count + 1);
            var frame = await camera.TriggerAsync(request.CameraBindingId, request.PointVersion, ct);
            if (frame.ConnectionEpoch != epoch || frame.Metadata is null || frame.Bytes.LongLength > request.MaxBytes)
                throw new InvalidDataException("CameraFrameEpochMetadataOrCapacityMismatch");
            var fact = new CorrelatedCaptureFact(request.Envelope.RunId, request.CaptureId,
                request.Envelope.OperationId, epoch, AcquisitionContract.RequestedSettingsDigest(request),
                MediaSource, CameraOrigin, LightOrigin, CaptureApplicationState.NotApplied, null, false,
                [$"worker-session:{frame.Metadata.WorkerSessionId}", $"trigger:{frame.Metadata.TriggerSequence}", $"frame:{frame.Metadata.FrameId}"])
                { FrameMetadata = frame.Metadata };
            onEvent(new(request, CaptureEventKind.Ended, epoch));
            onEvent(new(request, CaptureEventKind.MediaTaken, epoch, frame.Bytes, frame.Format) { Fact = fact });
        }
        catch (Exception e)
        {
            RuntimeDiagnostics.Record("CameraSdk", "CaptureUnknown_NoAutomaticReplay", request.Envelope.RunId,
                new { request.CaptureId, request.Envelope.OperationId, request.CameraBindingId, epoch }, e);
            onEvent(new(request, CaptureEventKind.Unknown, epoch, ErrorCode: e.Message));
        }
    }
}
