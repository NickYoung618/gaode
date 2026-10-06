using Gaode.Application.Capabilities;
using Gaode.Application.Configuration;
using Gaode.Contracts.Tests.Support;
using Gaode.Domain.Configuration;
using Xunit;

namespace Gaode.Contracts.Tests.Configuration;

public sealed class Station01RunConfigurationTests
{
    [Fact]
    public void UnknownControlCapabilityBlocksButAlgorithmReadinessRemainsSeparate()
    {
        var (config, budget, simulation) = TestConfiguration.Normal();
        var validator = new PublicConfigurationValidator(Station01Policies.Create());
        var badMotion = config with
        {
            Motion = config.Motion with { Capability = new CapabilityRef("motion.unknown", "9") }
        };
        var algorithmUnavailable = config with
        {
            Algorithms = config.Algorithms with
            {
                TrayPose = new(new CapabilityRef("pose.not-ready", "1"), "declared-pose", "test/1")
            }
        };

        var blocked = validator.Validate(badMotion, budget, simulation, fullSimulation: true);
        var algorithmIssue = validator.Validate(algorithmUnavailable, budget, simulation, fullSimulation: true);

        Assert.Contains("MotionCapabilityInvalid", blocked.BlockingControlErrors);
        Assert.True(algorithmIssue.CanStart);
        Assert.Contains("TrayPoseNotConfigured", algorithmIssue.AlgorithmIssues);
        Assert.DoesNotContain("MotionCapabilityInvalid", algorithmIssue.BlockingControlErrors);
    }

    [Fact]
    public void FrozenSnapshotKeepsVersionsCapabilitiesPurposeAndClampBudget()
    {
        var loader = TestConfiguration.Loader();
        var publicConfig = loader.LoadPublic(new("s01-public-dev", "1.0.0"));
        var budget = loader.LoadBudget(new("s01-budget-dev", "3.0.0"));
        var simulation = loader.LoadSimulation(new("s01-sim-normal", "3.0.0"));
        var capabilities = new Dictionary<string, string>(Station01Policies.Create().Versions);

        var frozen = ConfigurationFreezer.Freeze(publicConfig, budget, simulation, capabilities);
        capabilities["xyz.fixed"] = "changed-after-freeze";

        Assert.Equal("1.0.0", frozen.Public.Version);
        Assert.Equal("3.0.0", frozen.Budget.Version);
        Assert.Equal("2.0", frozen.Budget.SchemaVersion);
        Assert.Equal(10000, frozen.Budget.BusinessMs.RecipeApplication);
        Assert.Equal("Test", frozen.Public.Purpose);
        Assert.Equal(budget.Value.BusinessMs.ClampCompletion,
            frozen.Budget.BusinessMs.ClampCompletion);
        Assert.True(frozen.Budget.BusinessMs.ClampCompletion > 0);
        Assert.Equal("1.0", frozen.CapabilityVersions["xyz.fixed"]);
        Assert.Equal(64, frozen.SnapshotId.Length);
    }

    [Fact]
    public void ProductionCannotHideASimulatedBindingFallback()
    {
        var (config, budget, simulation) = TestConfiguration.Normal();
        var production = config with { Purpose = "Production" };
        var productionBudget = budget with { Purpose = "Production" };
        var validator = new PublicConfigurationValidator(Station01Policies.Create());

        var result = validator.Validate(production, productionBudget, simulation,
            fullSimulation: false, externalVirtualPlc: false);

        Assert.False(result.CanStart);
        Assert.Contains("ProductionSimulationFallbackForbidden", result.BlockingControlErrors);
    }
}
