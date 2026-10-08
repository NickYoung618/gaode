using System.Buffers.Binary;
using System.Text.Json;
using Gaode.Application.Ports;

namespace Gaode.Infrastructure.Devices.Cameras;

public sealed record CameraBinding(string Role, string Kind, string Serial, string ExpectedNicMac);
public sealed record CameraWireMessage(string Kind, Guid SessionId, Guid RequestId)
{
    public int Version { get; init; } = 2;
    public CameraImagingSettings? Settings { get; init; }
    public ActualCameraSettings? ActualSettings { get; init; }
    public string? SettingsDigest { get; init; }
    public CameraBinding? Binding { get; init; }
    public CaptureFrameMetadata? Metadata { get; init; }
    public long MaxBytes { get; init; }
    public string? Format { get; init; }
    public string? ContentType { get; init; }
    public string? Error { get; init; }
}

public static class CameraWorkerProtocol
{
    public const long MaxPayloadBytes = 768L * 1024 * 1024;
    public const int MaxHeaderBytes = 1024 * 1024;
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void ValidateResponse(CameraWireMessage request, CameraWireMessage response, string expectedKind)
    {
        if (response.Version != 2 || response.SessionId != request.SessionId || response.RequestId != request.RequestId)
            throw new InvalidDataException("CameraOldSessionOrRequestRejected");
        if (response.Kind == "error") throw new IOException("CameraSdkError:" + response.Error);
        if (response.Kind != expectedKind) throw new InvalidDataException("CameraUnexpectedMessage:" + response.Kind);
    }

    public static async Task WriteAsync(Stream stream, CameraWireMessage header,
        ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(header, Json);
        if (json.Length > MaxHeaderBytes || data.Length > MaxPayloadBytes) throw new InvalidDataException("CameraMessageTooLarge");
        var prefix = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(prefix, json.Length);
        await stream.WriteAsync(prefix, ct); await stream.WriteAsync(json, ct);
        var length = new byte[8]; BinaryPrimitives.WriteInt64LittleEndian(length, data.Length);
        await stream.WriteAsync(length, ct); await stream.WriteAsync(data, ct); await stream.FlushAsync(ct);
    }

    public static async Task<(CameraWireMessage Header, byte[] Data)> ReadAsync(Stream stream,
        long maxBytes, CancellationToken ct)
    {
        var prefix = new byte[4]; await stream.ReadExactlyAsync(prefix, ct);
        var count = BinaryPrimitives.ReadInt32LittleEndian(prefix);
        if (count is <= 0 or > MaxHeaderBytes) throw new InvalidDataException("CameraHeaderLengthInvalid");
        var json = new byte[count]; await stream.ReadExactlyAsync(json, ct);
        var header = JsonSerializer.Deserialize<CameraWireMessage>(json, Json) ?? throw new InvalidDataException("CameraHeaderMissing");
        if (header.Version != 2) throw new InvalidDataException("CameraProtocolVersionMismatch");
        var length = new byte[8]; await stream.ReadExactlyAsync(length, ct);
        var size = BinaryPrimitives.ReadInt64LittleEndian(length);
        if (size < 0 || size > Math.Min(maxBytes, MaxPayloadBytes) || (header.Kind != "frame" && size != 0))
            throw new InvalidDataException("CameraPayloadLengthInvalid");
        var data = new byte[checked((int)size)]; await stream.ReadExactlyAsync(data, ct);
        return (header, data);
    }
}
