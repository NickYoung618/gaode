using Gaode.Contracts.Tests.Support;
using Gaode.Domain.Configuration;
using Gaode.Infrastructure.Configuration;
using Gaode.Application.Configuration;
using Xunit;

namespace Gaode.Contracts.Tests.Configuration;

public sealed class SchemaInputTests
{
    [Theory]
    [InlineData("s01-sim-normal")]
    [InlineData("s01-sim-controlled-normal")]
    [InlineData("s01-sim-algorithm-timeouts")]
    [InlineData("s01-sim-motion-timeout")]
    [InlineData("s01-sim-no-response")]
    [InlineData("s01-sim-duplicate-late")]
    public void EveryVersionedSimulationExampleLoadsIndependently(string id)
    {
        var value = TestConfiguration.Loader().LoadSimulation(new(id, "2.0.0"));
        Assert.Equal(id, value.Value.Id);
        Assert.Equal("Test", value.Value.Purpose);
        Assert.Equal("s01-public-dev", value.Value.PublicConfigRef.Id);
        Assert.Equal("s01-budget-dev", value.Value.BudgetRef.Id);
        Assert.Equal("2.0.0", value.Value.BudgetRef.Version);
    }

    [Fact]
    public void AllCheckedInExamplesLoadThroughOneSchemaCheckedLoader()
    {
        var loader = TestConfiguration.Loader();
        var p = loader.LoadPublic(new("s01-public-dev", "1.0.0"));
        var b = loader.LoadBudget(new("s01-budget-dev", "3.0.0"));
        var real = loader.LoadSimulation(new("s01-sim-normal", "3.0.0"));
        Assert.Equal("Test", p.Value.Purpose);
        Assert.Equal("Test", b.Value.Purpose);
        Assert.Equal("RealElapsed", real.Value.ClockMode);
        Assert.Equal(64, p.Digest.Length);
        Assert.Throws<ConfigurationException>(() => loader.LoadPublic(new("../escape", "1.0.0")));
    }

    [Fact]
    public void UnknownMembersAndNonFiniteNumbersAreRejectedBySchemaBoundary()
    {
        var workspace = TestConfiguration.Workspace();
        var source = Path.Combine(workspace, "specs", "001-station01-public-preparation", "examples", "public.test.json");
        var schema = Path.Combine(workspace, "specs", "001-station01-public-preparation", "contracts");
        var root = Path.Combine(Path.GetTempPath(), "gaode-config-contract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var json = File.ReadAllText(source).Replace("\"qualityState\": \"NotEvaluated\"",
            "\"qualityState\": \"NotEvaluated\", \"arbitraryScript\": \"do-anything()\"");
        File.WriteAllText(Path.Combine(root, "bad.json"), json);
        var loader = new ConfigurationLoader(root, schema);
        Assert.Throws<ConfigurationException>(() => loader.LoadPublic(new("s01-public-dev", "1.0.0")));
    }
}
