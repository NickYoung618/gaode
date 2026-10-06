using Gaode.Application.Ports;

namespace Gaode.Infrastructure.Integrations;

public sealed class NotIntegratedCapture : ICapturePort
{
    private int _threeD;
    private int _f;
    public long ConnectionEpoch => 0;
    public int TriggerCount(CaptureRole role) => role == CaptureRole.ThreeD ? Volatile.Read(ref _threeD) : Volatile.Read(ref _f);

    public ValueTask RequestCaptureAsync(CaptureRequest request, Action<CaptureEvent> onEvent,
        CancellationToken cancellationToken)
    {
        if (request.Role == CaptureRole.ThreeD) Interlocked.Increment(ref _threeD);
        else Interlocked.Increment(ref _f);
        onEvent(new CaptureEvent(request, CaptureEventKind.Unknown, ConnectionEpoch,
            ErrorCode: "NotIntegrated"));
        return ValueTask.CompletedTask;
    }
}
