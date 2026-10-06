using Gaode.Application.Capabilities;
using Gaode.Application.Configuration;
using Gaode.Application.Recipes;
using Gaode.Contracts.Tests.Support;
using Gaode.Infrastructure.Configuration;
using Gaode.Infrastructure.Recipes;
using Xunit;

namespace Gaode.Contracts.Tests.Recipes;

// Input preparation only. Does not save recipes or execute a representative run.
public sealed class JointInputDefinitionTests
{
    [Fact]
    public void CurrentJointInputsUseUniqueSharedDefinitionsAndLoadExplicitPoseBudgets()
    {
        var root = TestConfiguration.Workspace();
        var input = Path.Combine(root, "specs/011-plc-interaction-update/examples/joint");
        var snapshot = new JsonRecipeCatalog(Path.Combine(input, "catalog.json")).GetSnapshot();
        Assert.Equal(2, snapshot.Definitions.Count);
        Assert.Equal(new[] { "011-TEST-TRAY-1", "011-TEST-TRAY-2" }, snapshot.Definitions.Select(d => d.FCode));
        foreach (var definition in snapshot.Definitions)
        {
            var others = RecipeCatalogSnapshots.Create("input-preparation", snapshot.Definitions.Where(d => d.FCode != definition.FCode));
            var result = RecipeDefinitionValidator.ValidateForSave(definition, others);
            Assert.True(result.Valid, string.Join(";", result.Issues.Select(i => i.Code + ":" + i.FieldPath)));
            Assert.Equal("Test", definition.Approval.Purpose);
            Assert.Empty(definition.RecipeId); // No server save/version is invented by preparation.
            Assert.Equal("originalSlot", definition.Disposition.Ok);
        }
        var loader = new ConfigurationLoader(Path.Combine(input, "config"),
            Path.Combine(root, "specs/001-station01-public-preparation/contracts"));
        var configuration = loader.LoadPublic(new("s01-public-011-joint", "1")).Value;
        var budget = loader.LoadBudget(new("s01-budget-011-joint", "2")).Value;
        var simulation = loader.LoadSimulation(new("s01-sim-011-joint", "2")).Value;
        var capabilities = Station01Policies.Create();
        capabilities.Register(new FixedCapabilityPolicy("code.test-tray-format", "1.0", "Parser", new HashSet<string> { "Test" }));
        var validation = new PublicConfigurationValidator(capabilities).Validate(configuration, budget,
            simulation, fullSimulation: false, externalVirtualPlc: true);
        Assert.Empty(validation.BlockingControlErrors);
        Assert.Empty(validation.AlgorithmIssues);
        Assert.Null(configuration.Algorithms.Height);
        Assert.Equal(0, budget.BusinessMs.ClampCompletion); // Historical field does not gate current readiness.
        Assert.Equal(10000, budget.BusinessMs.TrayPoseAlgorithm);
        Assert.Equal(10000, budget.BusinessMs.FlipCompletion);
        Assert.Equal(10000, budget.BusinessMs.PutBackCompletion);
    }
}
