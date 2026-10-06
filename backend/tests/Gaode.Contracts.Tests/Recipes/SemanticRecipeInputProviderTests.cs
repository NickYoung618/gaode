using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gaode.Application.Recipes;
using Gaode.Infrastructure.Recipes;
using Gaode.Contracts.Tests.Support;
using Xunit;

namespace Gaode.Contracts.Tests.Recipes;

public sealed class SemanticRecipeInputProviderTests
{
    [Fact]
    public void AlternativeFixedCoordinatesKeepConfiguredIdentityAndPurposeInputs()
    {
        // Component input only; no production approval or joint-run claim.
        var root = Path.Combine(TestConfiguration.Workspace(), "artifacts", "recipe-authoring-012",
            "component-inputs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var candidate = Recipe011Data.Candidate(1, false);
        var csv = SemanticRecipeInputProvider.CoordinateColumns + "\n" + string.Join("\n",
            candidate.ExecutionPositions["s1"].PhysicalEntity.Coordinates.Select(c =>
                $"s1,{c.ObjectPattern},1,1,{c.Camera},stage:1,{c.PointRef},configured-{c.Camera},external-2,30,40,7,mm,test-frame,external-2,Test:table,Test-only")) + "\n";
        File.WriteAllText(Path.Combine(root, "coordinates.csv"), csv, new UTF8Encoding(false));
        var input = new JsonObject
        {
            ["schemaVersion"] = SemanticRecipeInputProvider.InputSchema,
            ["definitions"] = new JsonArray(new JsonObject
            {
                ["definition"] = JsonNode.Parse(RecipeDefinitionSerialization.Serialize(candidate)),
                ["coordinatesFile"] = "coordinates.csv",
                ["coordinatesSha256"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(csv)))
            })
        };
        var path = Path.Combine(root, "input.json");
        File.WriteAllText(path, input.ToJsonString());
        var provider = new SemanticRecipeInputProvider(path);
        var snapshot = provider.GetSnapshot();
        var definition = Assert.Single(snapshot.Definitions);
        var coordinate = definition.ExecutionPositions["s1"].PhysicalEntity.Coordinates.First();
        Assert.Equal(30, coordinate.Point.X);
        Assert.Equal(40, coordinate.Point.Y);
        Assert.Equal(7, coordinate.Fixed.Z);
        Assert.Equal("stage:1", coordinate.StageId);
        Assert.Equal(JsonSerializer.Serialize(candidate.Approval), JsonSerializer.Serialize(definition.Approval));
        Assert.Equal(candidate.ExecutionPositions["s1"].PhysicalEntity.PurposePoints.Count,
            definition.ExecutionPositions["s1"].PhysicalEntity.PurposePoints.Count);
        var validation = RecipeDefinitionValidator.ValidateForSave(definition, new(RecipeCatalogSnapshot.CurrentSchema, "", []));
        Assert.True(validation.Valid, string.Join(";", validation.Issues.Select(i => i.Code)));
        // A digest mismatch is an adapter integrity failure, never substituted coordinates.
        File.AppendAllText(Path.Combine(root, "coordinates.csv"), "changed");
        Assert.Equal("CoordinateSourceDigestMismatch",
            Assert.Throws<InvalidDataException>(() => new SemanticRecipeInputProvider(path)).Message);
    }

    [Fact]
    public void OldMeasuredTableIsNotACompatibilityExecutionPath()
    {
        var root = Path.Combine(TestConfiguration.Workspace(), "artifacts", "recipe-authoring-012",
            "component-inputs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "old.json");
        File.WriteAllText(path, "{\"schemaVersion\":\"semantic-recipe-input/1\",\"definitions\":[]}");
        Assert.Equal("SemanticInputSchemaUnsupported",
            Assert.Throws<InvalidDataException>(() => new SemanticRecipeInputProvider(path)).Message);
    }
}
