using Gaode.Application.Ports;
using Gaode.Diagnostics;

namespace Gaode.Infrastructure.Devices.Cameras;

/// <summary>
/// Converts one SDK trigger into the application capture evidence sequence. It does not
/// interpret images, persist files, or retry an uncertain trigger.
/// </summary>
public sealed class CameraCaptureAdapter(ICameraSdkGateway camera, ILightGateway lights) : ICapturePort
{
    private int _threeD, _f;
    public string MediaSource => "Unknown";
    public Gaode.Domain.Station01.ComponentExecutionOrigin CameraOrigin => Gaode.Domain.Station01.ComponentExecutionOrigin.Unknown;
    public Gaode.Domain.Station01.ComponentExecutionOrigin LightOrigin => Gaode.Domain.Station01.ComponentExecutionOrigin.Unknown;
    public long ConnectionEpoch => camera.ConnectionEpoch;
    public int TriggerCount(CaptureRole role) => role == CaptureRole.ThreeD
        ? Volatile.Read(ref _threeD) : Volatile.Read(ref _f);

    public async ValueTask RequestCaptureAsync(CaptureRequest request,
        Action<CaptureEvent> onEvent, CancellationToken cancellationToken)
    {
        if (!request.Envelope.IsValid || request.CaptureId == Guid.Empty ||
            request.IntentWriteId == Guid.Empty || request.MaxBytes <= 0 ||
            string.IsNullOrWhiteSpace(request.CameraBindingId) ||
            string.IsNullOrWhiteSpace(request.LightBindingId))
            throw new ArgumentException("相机采集请求无效");
        if (request.Role == CaptureRole.F && Interlocked.Increment(ref _f) != 1)
            throw new InvalidOperationException("F已触发，不允许重拍");
        if (request.Role == CaptureRole.ThreeD) Interlocked.Increment(ref _threeD);
        var epoch = ConnectionEpoch;
        var phase = "CameraOpen";
        try
        {
            await camera.OpenAsync(request.CameraBindingId, cancellationToken);
            if (request.Role is CaptureRole.Detection or CaptureRole.E && request.DetectionSettings is { } settings)
            {
                phase = "CameraConfigure";
                await camera.ConfigureAsync(request.CameraBindingId, settings.ExposureUs, settings.Gain, cancellationToken);
                phase = "LightConfigure";
                await lights.SetBrightnessAsync(request.LightBindingId, settings.LightChannel,
                    settings.BrightnessPercent, cancellationToken);
                RuntimeDiagnostics.Record("CameraSdk", "SettingsRequested", request.Envelope.RunId,
                    new { request.CaptureId, request.CameraBindingId, settings.ExposureUs, settings.Gain,
                        settings.BrightnessPercent, applicationState = "Unknown" });
            }
            phase = "LightOn";
            await lights.SetAsync(request.LightBindingId, true, cancellationToken);
            phase = "CameraTrigger";
            onEvent(new(request, CaptureEventKind.Accepted, epoch));
            onEvent(new(request, CaptureEventKind.Capturing, epoch));
            var frame = await camera.TriggerAsync(request.CameraBindingId,
                request.PointVersion, cancellationToken);
            if (frame.ConnectionEpoch != epoch || frame.Bytes.LongLength > request.MaxBytes)
            {
                onEvent(new(request, CaptureEventKind.Failed, frame.ConnectionEpoch,
                    ErrorCode: frame.Bytes.LongLength > request.MaxBytes ? "MediaOverLimit" : "CameraEpochChanged"));
                return;
            }
            onEvent(new(request, CaptureEventKind.Ended, frame.ConnectionEpoch));
            onEvent(new(request, CaptureEventKind.MediaTaken, frame.ConnectionEpoch,
                frame.Bytes, request.Role == CaptureRole.ThreeD ? "bin" : "img")
            {
                Fact = new(request.Envelope.RunId, request.CaptureId, request.Envelope.OperationId,
                    frame.ConnectionEpoch, AcquisitionContract.RequestedSettingsDigest(request), MediaSource,
                    CameraOrigin, LightOrigin, CaptureApplicationState.Unknown, null, false, [])
            });
        }
        catch (OperationCanceledException error) when (cancellationToken.IsCancellationRequested)
        {
            RuntimeDiagnostics.Record("CameraSdk", "Cancelled", request.Envelope.RunId,
                new { request.CaptureId, request.Envelope.OperationId, epoch, phase }, error);
            onEvent(new(request, CaptureEventKind.Unknown, epoch, ErrorCode: "CaptureCancelledOrUnknown"));
        }
        catch (Exception error)
        {
            RuntimeDiagnostics.Record("CameraSdk", "Failed", request.Envelope.RunId,
                new { request.CaptureId, request.Envelope.OperationId, epoch, phase,
                    request.CameraBindingId, request.LightBindingId }, error);
            onEvent(new(request, CaptureEventKind.Unknown, epoch,
                ErrorCode: "CameraFailure:" + error.GetType().Name));
        }
        finally
        {
            try { await lights.SetAsync(request.LightBindingId, false, CancellationToken.None); }
            catch (Exception error)
            {
                RuntimeDiagnostics.Record("LightCleanup", "Failed", request.Envelope.RunId,
                    new { request.CaptureId, request.Envelope.OperationId, request.LightBindingId,
                        disposition = "LightOffNotConfirmed" }, error);
            }
        }
    }
}
