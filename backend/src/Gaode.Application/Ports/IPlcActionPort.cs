namespace Gaode.Application.Ports;

public interface IPlcActionPort
{
    ValueTask RequestStartAsync(PortEnvelope envelope, Guid actionId, Guid intentWriteId,
        Action<DeviceEvent> onEvent, CancellationToken cancellationToken);
    ValueTask RequestStopAsync(PortEnvelope envelope, Action<DeviceEvent> onEvent,
        CancellationToken cancellationToken);

}
