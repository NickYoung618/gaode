using Gaode.Application.Ports;

namespace Gaode.Application.Acquisition;

public sealed class CaptureEvidenceGate
{
    private readonly object _gate = new();
    private bool _ended, _taken;
    private byte[]? _buffer;
    private string? _format;
    private CorrelatedCaptureFact? _fact;

    public bool Observe(CaptureEvent e)
    {
        lock (_gate)
        {
            if (e.Fact is { } fact && !AcquisitionContract.MatchesFact(fact, e.Request, e.ConnectionEpoch)) return false;
            if (e.Kind == CaptureEventKind.Ended) _ended = true;
            if (e.Kind == CaptureEventKind.MediaTaken && e.Buffer is { Length: > 0 })
            {
                if (_taken) return false;
                _taken = true;
                _buffer = e.Buffer;
                _format = e.Format;
                _fact = e.Fact;
            }
            return _ended && _taken;
        }
    }

    public CorrelatedCaptureFact TakeFact(CaptureRequest request, long epoch)
    {
        lock (_gate)
        {
            if (!_ended || !_taken || _fact is null || !AcquisitionContract.MatchesFact(_fact, request, epoch))
                throw new InvalidOperationException("CurrentCaptureFactMissingOrMismatched");
            if (request.Envelope.Purpose == Gaode.Domain.Configuration.RuntimePurposes.RealDeviceCommissioning)
            {
                var actual = _fact.ActualCameraSettings;
                var detection = request.DetectionSettings; var publicSettings = request.PublicSettings;
                var skipped = request.LightExecution?.IsSimulated == true;
                var invalid = request.LightExecution != _fact.LightExecution ||
                    request.LightExecution is not null && !request.LightExecution.IsValid || detection is not null && publicSettings is not null || detection is null && publicSettings is null ||
                    _fact.CameraApplicationState != CaptureApplicationState.Applied || actual is null ||
                    !_fact.CameraOrigin.IsKnown || _fact.ActualPublicSettings != publicSettings ||
                    detection is not null && (_fact.ActualSettings is not { } applied ||
                        applied.ProfileId != detection.ProfileId || applied.LightChannel != detection.LightChannel ||
                        applied.BrightnessPercent != detection.BrightnessPercent || applied.SettleMs != detection.SettleMs) ||
                    actual.ExposureUs != (detection?.ExposureUs ?? publicSettings!.ExposureUs) ||
                    detection is not null && (actual.Gain != detection.Gain ||
                        !detection.RoiPixels.SequenceEqual(new[] { actual.OffsetX, actual.OffsetY, actual.Width, actual.Height })) ||
                    (skipped ? _fact.LightApplicationState != CaptureApplicationState.NotApplied || _fact.PhysicalLightApplied
                        : _fact.LightApplicationState is not (CaptureApplicationState.Applied or CaptureApplicationState.ConfiguredOnly)) ||
                    request.LightExecution?.Mode == "Real" &&
                        (_fact.LightOrigin.Source != Gaode.Domain.Station01.ComponentEvidenceSource.Real || !_fact.PhysicalLightApplied) ||
                    !_fact.LightOrigin.IsKnown ||
                    _fact.LightOrigin.Source == Gaode.Domain.Station01.ComponentEvidenceSource.Simulated && _fact.PhysicalLightApplied;
                if (invalid)
                {
                    Gaode.Diagnostics.RuntimeDiagnostics.Record("CaptureEvidence", "SettingsEvidenceRejected", request.Envelope.RunId,
                        new { request.CaptureId, request.Envelope.OperationId, request.IntentWriteId,
                            _fact.CameraApplicationState, _fact.LightApplicationState, _fact.RequestedSettingsDigest }, warning: true);
                    throw new InvalidOperationException("CaptureSettingsEvidenceMissingOrMismatched");
                }
            }
            return _fact;
        }
    }

    public (byte[] Buffer, string Format) Take()
    {
        lock (_gate)
        {
            if (!_ended || !_taken || _buffer is null || _format is null)
                throw new InvalidOperationException("采集结束与媒体接管依据尚未齐全");
            return (_buffer, _format);
        }
    }
}
