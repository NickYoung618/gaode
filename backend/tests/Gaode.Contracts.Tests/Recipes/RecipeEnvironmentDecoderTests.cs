using System.Text.Json;
using System.Text.Json.Nodes;
using Gaode.Application.Recipes;
using Gaode.Infrastructure.Recipes;
using Xunit;

namespace Gaode.Contracts.Tests.Recipes;

public sealed class RecipeEnvironmentDecoderTests
{
    [Theory]
    [InlineData("TEST-TRAY-0001")]
    [InlineData("TEST-TRAY-0042")]
    [InlineData(" Raw tray 甲 ")]
    public void FDecodedContentIsPreservedWithoutFixtureAliases(string raw)
    {
        var result = RecipeEnvironmentDecoder.DecodeTrayCode(raw);
        Assert.NotNull(result);
        Assert.Equal(raw, result.RawCode);
        Assert.Equal(raw, result.ParsedCode);
    }

    [Fact]
    public void CurrentDefinitionUsesCommonSerializationWithoutDroppingPurposeFields()
    {
        var text = RecipeDefinitionSerialization.Serialize(Recipe011Data.Candidate());
        using var body = JsonDocument.Parse(text);
        var read = RecipeEnvironmentDecoder.Decode(body.RootElement);
        Assert.Equal(text, RecipeDefinitionSerialization.Serialize(read));
    }

    [Fact]
    public void OldCoordinateFieldsAreNotSilentlyUpgraded()
    {
        var body = JsonNode.Parse(RecipeDefinitionSerialization.Serialize(Recipe011Data.Candidate()))!;
        body["stages"]![0]!["coordinateRule"] = "initial3D";
        using var document = JsonDocument.Parse(body.ToJsonString());
        Assert.Throws<JsonException>(() => RecipeEnvironmentDecoder.Decode(document.RootElement));
    }

    [Theory]
    [InlineData("duplicate-face", "RecipeRequiredTargetsIncompleteOrDuplicate")]
    [InlineData("wrong-material", "RecipeTargetInvalid")]
    [InlineData("capture-parameters", "RecipeCaptureProfileInvalid")]
    [InlineData("algorithm-inputs", "RecipeAlgorithmRequirementInvalid")]
    public void DirectSemanticDefinitionsCannotBypassCommonRules(string mutation, string expectedProblem)
    {
        var recipe = Recipe011Data.Candidate(1, false);
        var stage = recipe.Stages[0];
        var profile = recipe.CaptureProfiles.First();
        var algorithm = recipe.AlgorithmRequirements.First();
        var changed = mutation switch
        {
            "duplicate-face" => recipe with { Stages = [stage with { Targets = [stage.Targets[0], stage.Targets[0]] }] },
            "wrong-material" => recipe with { Stages = [stage with { Targets = [stage.Targets[0] with { Material = "Absent" }] }] },
            "capture-parameters" => recipe with { CaptureProfiles = recipe.CaptureProfiles.ToDictionary(x => x.Key,
                x => x.Key == profile.Key ? x.Value with { Settings = x.Value.Settings with { ExposureUs = 0 } } : x.Value) },
            _ => recipe with { AlgorithmRequirements = recipe.AlgorithmRequirements.ToDictionary(x => x.Key,
                x => x.Key == algorithm.Key ? x.Value with { InputCount = 7 } : x.Value) }
        };
        // The common validator owns these retained obligations. No decoder-level process rules.
        var result = RecipeDefinitionValidator.ValidateForSave(changed,
            new(RecipeCatalogSnapshot.CurrentSchema, "", []));
        Assert.False(result.Valid);
        Assert.Contains(result.Issues, issue => issue.Code == expectedProblem);
    }
}
