namespace Gaode.Application.Ports;

public interface IMotionPort
{
    ValueTask RequestMoveAsync(MoveRequest request, Action<DeviceEvent> onEvent,
        CancellationToken cancellationToken);
}
