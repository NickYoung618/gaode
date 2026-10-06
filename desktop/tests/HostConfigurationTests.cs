using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Gaode.Station01.Desktop.Tests;

[TestClass]
public class HostConfigurationTests
{
    [TestMethod]
    public void ApprovedConfigurationIsAccepted()
    {
        var config = new HostConfiguration("https://host.example", "https://host.example/hubs/station01", "Test", "v1", "3DC791C1F8AB5EEDFA037F5DBAE450B2D20522FED654F86EA700C0284945E1E0");
        config.Validate();
    }

    [TestMethod]
    public void DeviceAddressIsRejected()
    {
        var config = new HostConfiguration("plc://device", "https://host.example/hub", "Test", "v1", "3DC791C1F8AB5EEDFA037F5DBAE450B2D20522FED654F86EA700C0284945E1E0");
        Assert.ThrowsException<InvalidOperationException>(() => config.Validate());
    }
}
