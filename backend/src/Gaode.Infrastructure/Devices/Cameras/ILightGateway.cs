namespace Gaode.Infrastructure.Devices.Cameras;

public interface ILightGateway
{
    Task SetBrightnessAsync(string lightBindingId, string configuredChannel, int brightnessPercent,
        CancellationToken cancellationToken = default);
    Task SetAsync(string lightBindingId, bool enabled, CancellationToken cancellationToken = default);
}
