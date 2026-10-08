using System.Buffers.Binary;
using Gaode.Infrastructure.Devices.Cameras;
using Xunit;

namespace Gaode.Communication.Tests;

public sealed class CameraProtocolTests
{
    [Fact]
    public async Task WireV2CarriesSettingsAndRejectsV1InsteadOfDowngrading()
    {
        using var stream = new MemoryStream();
        var request = new CameraWireMessage("capture-configured", Guid.NewGuid(), Guid.NewGuid())
            { Settings = new(15000), SettingsDigest = "public-settings" };
        await CameraWorkerProtocol.WriteAsync(stream, request, ReadOnlyMemory<byte>.Empty, default);
        stream.Position = 0;
        var read = (await CameraWorkerProtocol.ReadAsync(stream, 0, default)).Header;
        Assert.Equal(2, read.Version); Assert.Equal(15000, read.Settings!.ExposureUs); Assert.Null(read.Settings.Gain);
        using var old = new MemoryStream();
        await CameraWorkerProtocol.WriteAsync(old, request with { Version = 1 }, ReadOnlyMemory<byte>.Empty, default);
        old.Position = 0;
        await Assert.ThrowsAsync<InvalidDataException>(() => CameraWorkerProtocol.ReadAsync(old, 0, default));
    }
    [Fact]
    public void OldSessionOrOldRequestCannotSatisfyCurrentCapture()
    {
        var current = new CameraWireMessage("capture", Guid.NewGuid(), Guid.NewGuid());
        Assert.Throws<InvalidDataException>(() => CameraWorkerProtocol.ValidateResponse(current,
            new("frame", Guid.NewGuid(), current.RequestId), "frame"));
        Assert.Throws<InvalidDataException>(() => CameraWorkerProtocol.ValidateResponse(current,
            new("frame", current.SessionId, Guid.NewGuid()), "frame"));
        CameraWorkerProtocol.ValidateResponse(current, new("frame", current.SessionId, current.RequestId), "frame");
    }
    [Fact]
    public async Task PersistentStreamDeliversConsecutiveBinaryFramesWithoutBase64()
    {
        using var stream = new MemoryStream();
        var session = Guid.NewGuid(); var first = Guid.NewGuid(); var second = Guid.NewGuid();
        var bytes = new byte[5 * 1024 * 1024]; Random.Shared.NextBytes(bytes);
        await CameraWorkerProtocol.WriteAsync(stream, new("frame", session, first), bytes, default);
        await CameraWorkerProtocol.WriteAsync(stream, new("frame", session, second), new byte[] { 7, 8 }, default);
        Assert.True(stream.Length < bytes.Length + 2048);
        stream.Position = 0;
        var a = await CameraWorkerProtocol.ReadAsync(stream, bytes.Length, default);
        var b = await CameraWorkerProtocol.ReadAsync(stream, bytes.Length, default);
        Assert.Equal(first, a.Header.RequestId); Assert.Equal(session, a.Header.SessionId);
        Assert.Equal(bytes, a.Data); Assert.Equal(second, b.Header.RequestId); Assert.Equal(new byte[] { 7, 8 }, b.Data);
    }

    [Fact]
    public async Task OversizedOrTruncatedTransferCannotProduceAFrame()
    {
        using var oversized = new MemoryStream();
        var prefix = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(prefix, CameraWorkerProtocol.MaxHeaderBytes + 1);
        oversized.Write(prefix); oversized.Position = 0;
        await Assert.ThrowsAsync<InvalidDataException>(() => CameraWorkerProtocol.ReadAsync(oversized, 20, default));
        using var truncated = new MemoryStream();
        await CameraWorkerProtocol.WriteAsync(truncated, new("frame", Guid.NewGuid(), Guid.NewGuid()), new byte[] { 1, 2, 3 }, default);
        truncated.SetLength(truncated.Length - 1); truncated.Position = 0;
        await Assert.ThrowsAsync<EndOfStreamException>(() => CameraWorkerProtocol.ReadAsync(truncated, 20, default));
    }
}
