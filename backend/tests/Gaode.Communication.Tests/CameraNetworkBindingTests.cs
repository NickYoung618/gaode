using Gaode.Infrastructure.Devices.Cameras;
using Xunit;
namespace Gaode.Communication.Tests;
public sealed class CameraNetworkBindingTests
{
    private static readonly CameraHostInterface Bound = new("A0146D01363A", "192.168.1.20", "255.255.255.0", 13);
    [Fact]
    public void UniqueConfiguredNicAndRouteProduceMeasuredLocalAddress()
    {
        var actual = CameraNetworkBinding.Resolve("A0-14-6D-01-36-3A", "192.168.1.88",
            [new("001122334455", "127.0.0.1", "255.0.0.0", 1), Bound], 13);
        Assert.Equal("192.168.1.20", actual.Address); Assert.Equal(13, actual.InterfaceIndex);
    }
    [Theory]
    [InlineData("wrong-mac")]
    [InlineData("wrong-route")]
    [InlineData("duplicate-ip")]
    [InlineData("wrong-subnet")]
    public void UnprovenBindingNeverSubstitutesAnAddress(string cause)
    {
        var mac = cause == "wrong-mac" ? "001122334455" : Bound.Mac;
        var addresses = cause == "duplicate-ip" ? new[] { Bound, Bound with { Address = "192.168.1.21" } } : new[] { Bound };
        Assert.Throws<InvalidDataException>(() => CameraNetworkBinding.Resolve(mac,
            cause == "wrong-subnet" ? "192.168.9.88" : "192.168.1.88", addresses, cause == "wrong-route" ? 1 : 13));
    }
}
