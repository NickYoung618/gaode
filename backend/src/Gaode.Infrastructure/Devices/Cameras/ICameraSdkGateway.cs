namespace Gaode.Infrastructure.Devices.Cameras;

public sealed record CameraFrame(byte[] Bytes, string Format, string ContentType, long ConnectionEpoch)
{
    public Gaode.Application.Ports.CaptureFrameMetadata? Metadata { get; init; }
    public Gaode.Application.Ports.ActualCameraSettings? ActualSettings { get; init; }
}

/// <summary>Vendor SDK boundary. Implementations must invoke callbacks only for their session.</summary>
public interface ICameraSdkGateway : IAsyncDisposable
{
    long ConnectionEpoch { get; }
    long GetConnectionEpoch(string binding) => ConnectionEpoch;
    long GetMaxCaptureBytes(string binding, long fallback) => fallback;
    Task OpenAsync(string cameraBindingId, CancellationToken cancellationToken = default);
    // The gateway owns the vendor API and supported range; business camera IDs are not SDK encodings.
    Task ConfigureAsync(string cameraBindingId, int exposureUs, double gain,
        CancellationToken cancellationToken = default);
    Task<CameraFrame> TriggerAsync(string cameraBindingId, string pointVersion,
        CancellationToken cancellationToken = default);
    Task<CameraFrame> CaptureConfiguredAsync(string cameraBindingId, string pointVersion,
        Gaode.Application.Ports.CameraImagingSettings settings, string settingsDigest,
        CancellationToken cancellationToken = default);
}
