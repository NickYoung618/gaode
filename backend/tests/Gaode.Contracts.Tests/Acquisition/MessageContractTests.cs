using Gaode.Application.Acquisition;
using Gaode.Application.Ports;
using Xunit;

namespace Gaode.Contracts.Tests.Acquisition;

public sealed class MessageContractTests
{
    [Fact]
    public void RequiresEndedAndMediaAndRejectsCrossCaptureAssociation()
    {
        var request = Request(Guid.NewGuid());
        var gate = new CaptureEvidenceGate();
        Assert.False(gate.Observe(new(request, CaptureEventKind.MediaTaken, 7, [1], "img")));
        Assert.Throws<InvalidOperationException>(() => gate.Take());
        Assert.True(gate.Observe(new(request, CaptureEventKind.Ended, 7)));
        var other = Request(Guid.NewGuid());
        Assert.False(AcquisitionContract.Matches(new(other, CaptureEventKind.Ended, 7), request, 7));
        Assert.True(AcquisitionContract.Matches(new(request, CaptureEventKind.Ended, 7), request, 7));
    }

    private static CaptureRequest Request(Guid captureId)
    {
        var envelope = new PortEnvelope(Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(),
            "snapshot", "1.0.0", "Test", 10, 20, "clock");
        return new(envelope, captureId, CaptureRole.F, "F", "1", null, null,
            "camera", "light", Guid.NewGuid(), 1024);
    }
}
