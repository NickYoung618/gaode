using Gaode.Application.Capabilities;
using Gaode.Application.Configuration;
using Gaode.Contracts.Tests.Support;
using Gaode.Infrastructure.Configuration;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Gaode.Contracts.Tests.Configuration;

public sealed class ConfigurationValidationTests
{
    [Fact]
    public void NonFiniteInternalBudgetCannotEnterTypedBusinessModel()
    {
        var (config, budget, simulation) = TestConfiguration.Normal();
        Assert.Equal(typeof(int?), budget.BusinessMs.GetType().GetProperty("RecipeApplication")!.PropertyType);
        var rejected = new List<string>();
        foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            // Exercise the actual production member conversion, without JSON or
            // a test-side exception. This integer model has no nonfinite state.
            Assert.Throws<Microsoft.CSharp.RuntimeBinder.RuntimeBinderException>(() =>
                budget.BusinessMs with { RecipeApplication = (dynamic)value });
            rejected.Add(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        var legal = budget with { BusinessMs = budget.BusinessMs with { RecipeApplication = (dynamic)10000 } };
        Assert.True(new PublicConfigurationValidator(Station01Policies.Create())
            .Validate(config, legal, simulation, fullSimulation: true).CanStart);
        var root = Environment.GetEnvironmentVariable("GAODE_009_EVIDENCE_ROOT")
            ?? throw new InvalidOperationException("009EvidenceRootRequired");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "BA07-config-nonfinite-internal-" + Guid.NewGuid().ToString("N") + ".json"),
            JsonSerializer.Serialize(new { caseId = "BA07-config/nonfinite-internal", rejected,
                memberType = "System.Nullable<System.Int32>", legal = legal.BusinessMs.RecipeApplication,
                scope = "ProductionModelTypeBoundaryAndSemanticValidator;NoTcpClaim" }));
    }
    [Theory]
    [InlineData("BUDGET-COMPONENT/valid-test", null)]
    [InlineData("BUDGET-COMPONENT/missing", "SchemaInvalid")]
    [InlineData("BUDGET-COMPONENT/null", "SchemaInvalid")]
    [InlineData("BUDGET-COMPONENT/zero", "SchemaInvalid")]
    [InlineData("BUDGET-COMPONENT/negative", "SchemaInvalid")]
    [InlineData("BUDGET-COMPONENT/fraction", "SchemaInvalid")]
    [InlineData("BUDGET-COMPONENT/nonfinite-json", "JsonInvalid")]
    [InlineData("BUDGET-COMPONENT/overflow", "SchemaInvalid")]
    [InlineData("BUDGET-COMPONENT/missing-version", "ConfigurationNotFound")]
    [InlineData("BUDGET-COMPONENT/schema-version", "SchemaInvalid")]
    [InlineData("BUDGET-COMPONENT/version-conflict", "ConfigurationVersionConflict")]
    [InlineData("BUDGET-COMPONENT/purpose", "SchemaInvalid")]
    [InlineData("BUDGET-COMPONENT/source", "RecipeApplicationBudgetInvalid")]
    [InlineData("BUDGET-COMPONENT/freeze", "FrozenBudgetIdentityInvalid")]
    [InlineData("BUDGET-COMPONENT/production-unapproved", "RecipeApplicationProductionBudgetUnapproved")]
    public void RecipeApplicationUsesActualLoaderValidatorAndFrozenBudget(string caseId, string? reason)
    {
        var original = TestConfiguration.Loader();
        var budget = original.LoadBudget(new("s01-budget-dev", "3.0.0"));
        var publicConfig = original.LoadPublic(new("s01-public-dev", "1.0.0"));
        var simulation = original.LoadSimulation(new("s01-sim-normal", "3.0.0"));
        var document = JsonNode.Parse(budget.CanonicalJson)!;
        var scenario = caseId.Split('/')[1];
        var durations = document["businessMs"]!.AsObject();
        switch (scenario)
        {
            case "missing": durations.Remove("recipeApplication"); break;
            case "null": durations["recipeApplication"] = null; break;
            case "zero": durations["recipeApplication"] = 0; break;
            case "negative": durations["recipeApplication"] = -1; break;
            case "fraction": durations["recipeApplication"] = 1.5; break;
            case "overflow": durations["recipeApplication"] = 2147483648L; break;
            case "missing-version": document.AsObject().Remove("version"); break;
            case "schema-version": document["schemaVersion"] = "1.0"; break;
            case "purpose": document["purpose"] = "UnapprovedPurpose"; break;
            case "production-unapproved": document["purpose"] = "Production"; break;
            case "source": document["source"] = " "; break;
        }
        var directory = Path.Combine(Gaode.Testing.ApprovedTestRoot.Resolve(TestConfiguration.Workspace()),
            "009-budget-components", scenario + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var json = document.ToJsonString();
        if (scenario == "nonfinite-json") json = json.Replace("\"recipeApplication\":10000", "\"recipeApplication\":NaN", StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(directory, "budget.json"), json);
        if (scenario == "version-conflict")
        {
            document["source"] = "different-content-same-version";
            File.WriteAllText(Path.Combine(directory, "conflict.json"), document.ToJsonString());
        }
        var loader = new ConfigurationLoader(directory,
            Path.Combine(TestConfiguration.Workspace(), "specs/001-station01-public-preparation/contracts"));
        FrozenConfiguration? frozen = null;
        string? actual = null;
        try
        {
            var loaded = loader.LoadBudget(new("s01-budget-dev", "3.0.0"));
            var decision = new PublicConfigurationValidator(Station01Policies.Create())
                .Validate(publicConfig.Value, loaded.Value, simulation.Value, fullSimulation: true);
            if (decision.BlockingControlErrors.Count > 0)
            {
                Assert.NotNull(reason);
                Assert.Contains(reason!, decision.BlockingControlErrors);
                actual = reason;
            }
            else frozen = ConfigurationFreezer.Freeze(publicConfig,
                scenario == "freeze" ? loaded with { Digest = "tampered" } : loaded,
                simulation, Station01Policies.Create().Versions);
        }
        catch (ConfigurationException error) { actual = error.Code; }
        catch (JsonException) { actual = "JsonInvalid"; }
        Assert.Equal(reason, actual);
        if (reason is null)
        {
            Assert.NotNull(frozen);
            Assert.Equal(10000, frozen.Budget.BusinessMs.RecipeApplication);
            Assert.Equal("Test", frozen.Budget.Purpose);
            Assert.Equal(budget.CanonicalJson, frozen.BudgetJson);
            // Editing/reloading latest configuration cannot change the active immutable snapshot.
            document["businessMs"]!["recipeApplication"] = 20000;
            File.WriteAllText(Path.Combine(directory, "budget.json"), document.ToJsonString());
            Assert.Equal(20000, loader.LoadBudget(new("s01-budget-dev", "3.0.0")).Value.BusinessMs.RecipeApplication);
            Assert.Equal(10000, frozen.Budget.BusinessMs.RecipeApplication);
            Assert.Equal(budget.Digest, frozen.BudgetDigest);
        }
        else Assert.Null(frozen);
        File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new {
            caseId, actual, scope = "ConfigurationComponentOnly;HostAndApiDispatchAdmissionStillRequired",
            frozenBudget = frozen?.Budget, frozen?.BudgetDigest, frozen?.SnapshotId }));
    }
    [Fact]
    public void MissingFPointBlocksStartButMissingAlgorithmDoesNot()
    {
        var (config, budget, simulation) = TestConfiguration.Normal();
        var validator = new PublicConfigurationValidator(Station01Policies.Create());
        Assert.True(validator.Validate(config, budget, simulation, true).CanStart);
        var badF = config with { Motion = config.Motion with
        { Points = config.Motion.Points with { F = config.Motion.Points.F with { X = -1 } } } };
        Assert.False(validator.Validate(badF, budget, simulation, true).CanStart);
        var noPose = config with { Algorithms = config.Algorithms with
        { TrayPose = new(null, null, null) } };
        var decision = validator.Validate(noPose, budget, simulation, true);
        Assert.True(decision.CanStart);
        Assert.Contains("TrayPoseNotConfigured", decision.AlgorithmIssues);
    }

    [Fact]
    public void InvalidRangeAndPurposeMismatchBlockButAlgorithmBudgetIssueStaysSeparate()
    {
        var (config, budget, simulation) = TestConfiguration.Normal();
        var validator = new PublicConfigurationValidator(Station01Policies.Create());
        var invalidRange = config with { Capture3d = config.Capture3d with
        { Scope = config.Capture3d.Scope with { Bounds = new(10, 1, 0, 1) } } };
        Assert.False(validator.Validate(invalidRange, budget, simulation, true).CanStart);
        Assert.False(validator.Validate(config, budget with { Purpose = "Production" },
            simulation, true).CanStart);
        var missingAlgorithmBudget = budget with { BusinessMs = budget.BusinessMs with
        { TrayPoseAlgorithm = null } };
        var result = validator.Validate(config, missingAlgorithmBudget, simulation, true);
        Assert.True(result.CanStart);
        Assert.Contains(result.AlgorithmIssues, x => x.Contains("TrayPose", StringComparison.Ordinal));
    }

    [Fact]
    public void SimulationReferenceMismatchBlocksBeforeDeviceAction()
    {
        var (config, budget, simulation) = TestConfiguration.Normal();
        var mismatched = simulation with { PublicConfigRef = simulation.PublicConfigRef with { Version = "not-installed-version" } };
        var decision = new PublicConfigurationValidator(Station01Policies.Create())
            .Validate(config, budget, mismatched, fullSimulation: true);

        Assert.False(decision.CanStart);
        Assert.Contains("SimulationPurposeOrReferenceInvalid", decision.BlockingControlErrors);
    }

    [Fact]
    public void FrozenSnapshotDoesNotChangeWhenLoadedMutableArraysAreEdited()
    {
        var loader = TestConfiguration.Loader();
        var p = loader.LoadPublic(new("s01-public-dev", "1.0.0"));
        var b = loader.LoadBudget(new("s01-budget-dev", "3.0.0"));
        var s = loader.LoadSimulation(new("s01-sim-normal", "3.0.0"));
        var frozen = ConfigurationFreezer.Freeze(p, b, s,
            new Dictionary<string, string> { ["xy.fixed"] = "1.0" });
        p.Value.Bindings[0] = p.Value.Bindings[0] with { Provider = "Real" };
        Assert.All(frozen.Public.Bindings, binding => Assert.Equal("Simulated", binding.Provider));
        Assert.Equal("Test", frozen.Public.Purpose);
    }
}
