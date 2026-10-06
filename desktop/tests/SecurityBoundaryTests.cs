using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Gaode.Station01.Desktop.Tests;

[TestClass]
public class SecurityBoundaryTests
{
    [TestMethod]
    public void HostConfigurationDoesNotExposeDeviceOrDatabaseFields()
    {
        var names = typeof(HostConfiguration).GetProperties().Select(x => x.Name).ToArray();
        CollectionAssert.DoesNotContain(names, "PlcAddress");
        CollectionAssert.DoesNotContain(names, "DatabaseConnectionString");
    }
}
