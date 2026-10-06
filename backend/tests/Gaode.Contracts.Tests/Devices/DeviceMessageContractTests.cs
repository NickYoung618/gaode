using Gaode.Application.Ports;
using Xunit;

namespace Gaode.Contracts.Tests.Devices;

public sealed class DeviceMessageContractTests
{
    [Fact]
    public void RejectsMissingVersionOldGenerationAndTreatsAckAsNonCompletion()
    {
        var envelope = ValidEnvelope();
        Assert.True(envelope.IsValid);
        Assert.False((envelope with { ConfigVersion = "" }).IsValid);
        var accepted = new DeviceEvent(envelope, DeviceEventKind.Accepted, Guid.NewGuid(), 2);
        Assert.True(DeviceContract.Matches(accepted, envelope, 2));
        Assert.False(DeviceContract.Matches(accepted, envelope, 3));
        Assert.NotEqual(DeviceEventKind.Completed, accepted.Kind);
    }

    private static PortEnvelope ValidEnvelope() => new(Guid.NewGuid(), Guid.NewGuid(), 1,
        Guid.NewGuid(), "snapshot", "1.0.0", "Test", 10, 20, "clock");
}
