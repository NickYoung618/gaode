namespace Gaode.Infrastructure.Devices.Cameras;

public interface ILightGateway
{
    Gaode.Domain.Station01.ComponentExecutionOrigin Origin { get; }
    Task SetBrightnessAsync(string lightBindingId, string configuredChannel, int brightnessPercent,
        CancellationToken cancellationToken = default);
    Task SetAsync(string lightBindingId, bool enabled, CancellationToken cancellationToken = default);
}
