using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Gaode.Station01.Desktop.Tests;

[TestClass]
public class HostRuntimeTests
{
    [TestMethod]
    public void VirtualHostIsFixedToApprovedOrigin() => Assert.AreEqual("appassets.local", HostRuntime.VirtualHost);
}
