namespace Gaode.Infrastructure.Devices.Cameras;

public sealed record CameraFrame(byte[] Bytes, string Format, string ContentType, long ConnectionEpoch);

/// <summary>Vendor SDK boundary. Implementations must invoke callbacks only for their session.</summary>
public interface ICameraSdkGateway : IAsyncDisposable
{
    long ConnectionEpoch { get; }
    Task OpenAsync(string cameraBindingId, CancellationToken cancellationToken = default);
    // The gateway owns the vendor API and supported range; business camera IDs are not SDK encodings.
    Task ConfigureAsync(string cameraBindingId, int exposureUs, double gain,
        CancellationToken cancellationToken = default);
    Task<CameraFrame> TriggerAsync(string cameraBindingId, string pointVersion,
        CancellationToken cancellationToken = default);
}
