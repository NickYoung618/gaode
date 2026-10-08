namespace Gaode.Application.Ports;

public sealed record FramePayload(string Name, long ElementCount, int ElementBytes,
    long ByteLength, string Sha256);

public sealed record CaptureFrameMetadata(string Schema, string Role, string Serial,
    string NicMac, string CameraIp, string HostIp, Guid WorkerSessionId,
    ulong FrameId, double DeviceTimestamp, long TriggerSequence,
    DateTimeOffset TriggeredUtc, DateTimeOffset ReceivedUtc, int Width, int Height,
    string PixelFormat, long PayloadBytes, IReadOnlyDictionary<string, string> ActualParameters,
    IReadOnlyList<FramePayload> Payloads);

public sealed record ReceivedCapture(byte[] Bytes, string Format, CorrelatedCaptureFact Fact);

public interface ICameraCaptureJournal
{
    Task RecordIntentAsync(CaptureRequest request, CancellationToken cancellationToken);
    Task CommitAsync(CaptureRequest request, MediaRef media, CorrelatedCaptureFact fact,
        CancellationToken cancellationToken);
    Task RecordFailureAsync(CaptureRequest request, string reason, CancellationToken cancellationToken);
}
