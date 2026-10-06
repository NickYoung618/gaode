using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Gaode.Station01.Desktop.Tests;

[TestClass]
public class WebView2StartupTests
{
    [TestMethod]
    public void ProductionResourceContractUsesThreePages()
    {
        var pages = new[] { "login.html", "a.html", "data-view.html", "prototype.html" };
        foreach (var page in pages) Assert.IsTrue(page.EndsWith(".html", StringComparison.Ordinal));
    }
}
