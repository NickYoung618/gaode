using System.Collections.Concurrent;
using Gaode.Application.Ports;
using Gaode.Domain.Station01;
using Gaode.Diagnostics;

namespace Gaode.Infrastructure.Devices.Cameras;

/// <summary>Applies capture settings and records each component's source separately.</summary>
public sealed class CameraCaptureAdapter(ICameraSdkGateway camera, ILightGateway? light = null,
    IReadOnlyDictionary<string, string>? publicLightChannels = null) : ICapturePort
{
    private readonly ConcurrentDictionary<CaptureRole, int> _counts = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.Ordinal);
    public string MediaSource => "RealCamera";
    public ComponentExecutionOrigin CameraOrigin => new(ComponentEvidenceSource.Real,
        typeof(CameraCaptureAdapter).Assembly.FullName!, "PersistentCamera/2");
    public ComponentExecutionOrigin LightOrigin => light?.Origin ?? ComponentExecutionOrigin.Unknown;
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
        var gate = _gates.GetOrAdd(request.CameraBindingId, _ => new(1, 1));
        var entered = false;
        var lightEnabled = false;
        var skipLight = request.LightExecution?.IsSimulated == true;
        var lightOrigin = skipLight ? new ComponentExecutionOrigin(ComponentEvidenceSource.Simulated, "light-execution/1", "SkippedExternalLight/1") : LightOrigin;
        try
        {
            var detection = request.DetectionSettings; var publicSettings = request.PublicSettings;
            if (detection is not null && publicSettings is not null ||
                publicSettings is not null && request.Role is not (CaptureRole.ThreeD or CaptureRole.F))
                throw new ArgumentException("CaptureSettingsRoleMismatch");
            var configured = detection is not null || publicSettings is not null;
            if (request.LightExecution is not null && !request.LightExecution.IsValid)
                throw new InvalidOperationException("CaptureLightModeInvalid");
            if (request.LightExecution?.Mode == "Real" && LightOrigin.Source != ComponentEvidenceSource.Real)
                throw new InvalidOperationException("RealLightAdapterNotConfigured");
            string? channel = detection?.LightChannel;
            if (publicSettings is not null)
            {
                if (string.IsNullOrWhiteSpace(publicSettings.ConfigurationId) || string.IsNullOrWhiteSpace(publicSettings.ConfigurationVersion))
                    throw new ArgumentException("PublicCaptureConfigurationIdentityMissing");
                channel = request.LightBindingId is { } binding ? publicLightChannels?.GetValueOrDefault(binding) : null;
            }
            if (configured && !skipLight && (light is null || string.IsNullOrWhiteSpace(request.LightBindingId) || string.IsNullOrWhiteSpace(channel)))
                throw new InvalidOperationException("CaptureLightConfigurationMissing");
            if (detection is { SettleMs: < 0 }) throw new ArgumentException("CaptureSettleInvalid");
            await gate.WaitAsync(ct); entered = true;
            onEvent(new(request, CaptureEventKind.Accepted, epoch));
            onEvent(new(request, CaptureEventKind.Capturing, epoch));
            var digest = AcquisitionContract.RequestedSettingsDigest(request);
            RuntimeDiagnostics.Record("CameraSdk", "CaptureSettingsAccepted", request.Envelope.RunId,
                new { request.CaptureId, request.Envelope.OperationId, request.CameraBindingId, epoch, settingsDigest = digest, CameraOrigin, LightOrigin });
            if (configured && !skipLight)
            {
                await light!.SetBrightnessAsync(request.LightBindingId!, channel!, detection?.BrightnessPercent ?? publicSettings?.LightLevel ?? throw new InvalidOperationException("CaptureLightBrightnessMissing"), ct);
                await light.SetAsync(request.LightBindingId!, true, ct);
                lightEnabled = true;
                RuntimeDiagnostics.Record("CameraLight", "LightSettingsConsumed", request.Envelope.RunId,
                    new { request.CaptureId, request.Envelope.OperationId, request.LightBindingId, channel, settingsDigest = digest,
                        LightOrigin, PhysicalLightApplied = LightOrigin.Source == ComponentEvidenceSource.Real });
                if (detection is { SettleMs: > 0 }) await Task.Delay(detection.SettleMs!.Value, ct);
            }
            if (configured && skipLight)
                RuntimeDiagnostics.Record("CameraLight", "ExternalLightSkipped", request.Envelope.RunId,
                    new { request.CaptureId, request.Envelope.OperationId, request.LightExecution, PhysicalLightApplied = false });
            var frame = configured ? await camera.CaptureConfiguredAsync(request.CameraBindingId, request.PointVersion,
                new(detection?.ExposureUs ?? publicSettings!.ExposureUs, detection?.Gain, detection?.RoiPixels), digest, ct)
                : await camera.TriggerAsync(request.CameraBindingId, request.PointVersion, ct);
            if (frame.ConnectionEpoch != epoch || frame.Metadata is null || frame.Bytes.LongLength > request.MaxBytes)
                throw new InvalidDataException("CameraFrameEpochMetadataOrCapacityMismatch");
            _counts.AddOrUpdate(request.Role, 1, (_, count) => count + 1);
            RuntimeDiagnostics.Record("CameraSdk", "CurrentFrameAndSettingsConfirmed", request.Envelope.RunId,
                new { request.CaptureId, request.Envelope.OperationId, request.CameraBindingId, epoch,
                    settingsDigest = digest, frame.Metadata.WorkerSessionId, frame.Metadata.FrameId,
                    frame.Metadata.TriggerSequence, frame.ActualSettings, CameraOrigin, LightOrigin });
            var fact = new CorrelatedCaptureFact(request.Envelope.RunId, request.CaptureId,
                request.Envelope.OperationId, epoch, AcquisitionContract.RequestedSettingsDigest(request),
                MediaSource, CameraOrigin, configured ? lightOrigin : ComponentExecutionOrigin.Unknown,
                configured ? CaptureApplicationState.ConfiguredOnly : CaptureApplicationState.NotApplied, detection, false,
                [$"worker-session:{frame.Metadata.WorkerSessionId}", $"trigger:{frame.Metadata.TriggerSequence}", $"frame:{frame.Metadata.FrameId}"])
                { FrameMetadata = frame.Metadata, ActualCameraSettings = frame.ActualSettings,
                    ActualPublicSettings = publicSettings,
                    CameraApplicationState = configured ? CaptureApplicationState.Applied : CaptureApplicationState.NotApplied,
                    LightExecution = request.LightExecution,
                    LightApplicationState = skipLight ? CaptureApplicationState.NotApplied : configured ? LightOrigin.Source == ComponentEvidenceSource.Real
                        ? CaptureApplicationState.Applied : CaptureApplicationState.ConfiguredOnly : CaptureApplicationState.Unknown,
                    PhysicalLightApplied = configured && !skipLight && LightOrigin.Source == ComponentEvidenceSource.Real };
            if (lightEnabled) { await light!.SetAsync(request.LightBindingId!, false, ct); lightEnabled = false; }
            onEvent(new(request, CaptureEventKind.Ended, epoch));
            onEvent(new(request, CaptureEventKind.MediaTaken, epoch, frame.Bytes, frame.Format) { Fact = fact });
        }
        catch (Exception e)
        {
            RuntimeDiagnostics.Record("CameraSdk", "CaptureUnknown_NoAutomaticReplay", request.Envelope.RunId,
                new { request.CaptureId, request.Envelope.OperationId, request.CameraBindingId, epoch }, e);
            onEvent(new(request, CaptureEventKind.Unknown, epoch, ErrorCode: e.Message));
        }
        finally
        {
            try
            {
                if (lightEnabled) await light!.SetAsync(request.LightBindingId!, false, CancellationToken.None);
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.Record("CameraLight", "LightOffFailed", request.Envelope.RunId,
                    new { request.CaptureId, request.Envelope.OperationId, request.LightBindingId }, error);
            }
            finally { if (entered) gate.Release(); }
        }
    }
}
