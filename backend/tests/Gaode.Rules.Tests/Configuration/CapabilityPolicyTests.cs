using Gaode.Application.Capabilities;
using Gaode.Domain.Configuration;
using Xunit;

namespace Gaode.Rules.Tests.Configuration;

public sealed class CapabilityPolicyTests
{
    [Fact]
    public void OnlyRegisteredMatchingCategoryAndPurposeCanBeReferenced()
    {
        var registry = Station01Policies.Create();
        Assert.True(registry.IsCompatible(new("xy.fixed", "1.0"), "Test", "Motion"));
        Assert.False(registry.IsCompatible(new("xy.fixed", "1.0"), "Test", "Height"));
        Assert.False(registry.IsCompatible(new("xy.script", "1.0"), "Test", "Motion"));
        Assert.False(registry.IsCompatible(new("code.test-tray-format", "1.0"), "Production", "Parser"));
        Assert.Throws<InvalidOperationException>(() => registry.Register(
            new FixedCapabilityPolicy("xy.fixed", "1.0", "Motion", new HashSet<string> { "Test" })));
    }
}
