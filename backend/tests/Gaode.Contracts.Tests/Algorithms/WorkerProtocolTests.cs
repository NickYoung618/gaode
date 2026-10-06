using System.Text;
using Gaode.Infrastructure.Algorithms;
using Xunit;

namespace Gaode.Contracts.Tests.Algorithms;

public sealed class WorkerProtocolTests
{
    [Fact]
    public void ExecuteRoundTripsWithoutEmbeddingAnAbsolutePath()
    {
        var session = Guid.NewGuid();
        var input = new WorkerMediaInput("lease-1", Guid.NewGuid(), Guid.NewGuid(),
            "media/run/capture.bin", "application/octet-stream", 12, new string('A', 64));
        var message = WorkerProtocolCodec.Execute(session, new WorkerExecutionInput(
            Guid.NewGuid(), 1, "TrayPose", "lease-1", [input], "params/1", "tray.observation", "1.0"));
        var decoded = WorkerProtocolCodec.Decode(Encoding.UTF8.GetBytes(WorkerProtocolCodec.Encode(message)));
        Assert.Equal("Execute", decoded.Type);
        Assert.Equal(session, decoded.WorkerSessionId);
        Assert.Equal("media/run/capture.bin", Assert.Single(decoded.Inputs!).InputKey);
    }

    [Fact]
    public void AbsoluteOrTraversalInputIsRejected()
    {
        var message = new WorkerMessage("Execute", WorkerProtocolCodec.ContractVersion,
            Guid.NewGuid(), Guid.NewGuid(), 1, "TrayPose", "lease", Inputs:
            [new WorkerMediaInput("lease", Guid.NewGuid(), Guid.NewGuid(),
                "..\\outside.bin", "application/octet-stream", 12, new string('A', 64))]);
        Assert.Throws<InvalidDataException>(() => WorkerProtocolCodec.Encode(message));
    }

    [Fact]
    public void OversizedLinesAreRejected()
    {
        var message = new WorkerMessage("Health", WorkerProtocolCodec.ContractVersion,
            Guid.NewGuid(), Guid.NewGuid(), 1, Reason: new string('x', WorkerProtocolCodec.MaxLineBytes));
        Assert.Throws<InvalidDataException>(() => WorkerProtocolCodec.Encode(message));
    }
}
