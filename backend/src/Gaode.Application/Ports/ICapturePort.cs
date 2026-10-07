namespace Gaode.Application.Ports;

public interface ICapturePort
{
    Gaode.Domain.Station01.ComponentExecutionOrigin CameraOrigin => Gaode.Domain.Station01.ComponentExecutionOrigin.Unknown;
    Gaode.Domain.Station01.ComponentExecutionOrigin LightOrigin => Gaode.Domain.Station01.ComponentExecutionOrigin.Unknown;
    string MediaSource => "Unknown";
    long ConnectionEpoch { get; }
    ValueTask RequestCaptureAsync(CaptureRequest request, Action<CaptureEvent> onEvent,
        CancellationToken cancellationToken);
    int TriggerCount(CaptureRole role);
}
