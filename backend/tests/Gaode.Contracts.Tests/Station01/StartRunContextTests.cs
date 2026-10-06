using System.Text.Json;
using Gaode.Application.Station01;
using Gaode.Domain.Station01;
using Xunit;

namespace Gaode.Contracts.Tests.Station01;

public sealed class StartRunContextTests
{
    [Fact]
    public void ValidVersionedContextFreezesOneIdentityWithoutGeneratingTrayId()
    {
        var trayId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var startedAt = new DateTimeOffset(2026, 9, 23, 4, 0, 0, TimeSpan.FromHours(8));
        var parsed = StartRunContextParser.Parse(Json(trayId));

        var identity = parsed.Freeze(runId, "request-1", "operator-1", startedAt,
            "public-2", "budget-3", "simulation-4");
        var snapshot = Snapshot(runId, identity);
        var advanced = snapshot.Next(RunState.Preparing);

        Assert.Equal(trayId, identity.TrayId);
        Assert.Equal(runId, identity.RunId);
        Assert.Equal("S01", identity.StationId);
        Assert.Equal("L01", identity.LineId);
        Assert.Equal("scenario-A", identity.ScenarioId);
        Assert.Equal(["A01", "A02"], identity.OccupiedSlots);
        Assert.Equal(RunPurpose.Test, identity.Purpose);
        Assert.Same(identity, advanced.Identity);
        identity.EnsureScope(runId, trayId);
        Assert.Throws<InvalidOperationException>(() => identity.EnsureScope(Guid.NewGuid(), trayId));
        Assert.Throws<InvalidOperationException>(() => identity.EnsureScope(runId, Guid.NewGuid()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{}")]
    public void MissingOrEmptyContextIsRejected(string json) =>
        Assert.Throws<ArgumentException>(() => StartRunContextParser.Parse(json));

    [Fact]
    public void WrongSchemaVersionIsRejected()
    {
        var json = Json(Guid.NewGuid()).Replace(StartRunContext.CurrentSchemaVersion,
            "station01-start-run-context/3.0", StringComparison.Ordinal);

        var error = Assert.Throws<ArgumentException>(() => StartRunContextParser.Parse(json));

        Assert.StartsWith("StartRunContextSchemaVersionUnsupported", error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RecipeContextRequiresAndPreservesExactExpectedReference()
    {
        var json = JsonSerializer.Serialize(new
        {
            schemaVersion = StartRunContext.RecipeSchemaVersion,
            trayId = Guid.NewGuid(), stationId = "S01", lineId = "L01",
            scenarioId = "S1", occupiedSlots = new[] { "P01" }, purpose = "Test",
            expectedRecipeRef = new { recipeId = "R008-Q01", version = "1.0.0-test", catalogDigest = "DIGEST" }
        });
        var parsed = StartRunContextParser.Parse(json);
        Assert.Equal("R008-Q01", parsed.ExpectedRecipeRef!.RecipeId);
        Assert.Equal("DIGEST", parsed.ExpectedRecipeRef.CatalogDigest);
        Assert.StartsWith("ExpectedRecipeRefRequired", Assert.Throws<ArgumentException>(() =>
            StartRunContextParser.Parse(Json(Guid.NewGuid()).Replace(
                StartRunContext.CurrentSchemaVersion, StartRunContext.RecipeSchemaVersion,
                StringComparison.Ordinal))).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyIdentityFieldIsRejected()
    {
        var json = JsonSerializer.Serialize(new
        {
            schemaVersion = StartRunContext.CurrentSchemaVersion,
            trayId = Guid.NewGuid(), stationId = "", lineId = "L01",
            scenarioId = "scenario-A", occupiedSlots = new[] { "A01" }, purpose = "Test"
        });

        Assert.Throws<ArgumentException>(() => StartRunContextParser.Parse(json));
    }

    [Fact]
    public void DuplicateOccupiedIdentityIsRejected()
    {
        var json = JsonSerializer.Serialize(new
        {
            schemaVersion = StartRunContext.CurrentSchemaVersion,
            trayId = Guid.NewGuid(), stationId = "S01", lineId = "L01",
            scenarioId = "scenario-A", occupiedSlots = new[] { "A01", "A01" }, purpose = "Test"
        });

        var error = Assert.Throws<ArgumentException>(() => StartRunContextParser.Parse(json));

        Assert.StartsWith("StartRunContextOccupiedSlotsDuplicate", error.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyTrayIdIsRejectedRatherThanGenerated()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            StartRunContextParser.Parse(Json(Guid.Empty)));

        Assert.StartsWith("StartRunContextTrayIdRequired", error.Message,
            StringComparison.Ordinal);
    }

    private static string Json(Guid trayId) => JsonSerializer.Serialize(new
    {
        schemaVersion = StartRunContext.CurrentSchemaVersion,
        trayId, stationId = "S01", lineId = "L01", scenarioId = "scenario-A",
        occupiedSlots = new[] { "A01", "A02" }, purpose = "Test"
    });

    private static RunSnapshot Snapshot(Guid runId, WorkflowIdentity identity) =>
        new(runId, "request-1", "operator-1", RunState.Created, 0, 0,
            TerminalOutcome.None, false, ActionState.NotRequested, CaptureState.NotRequested,
            AlgorithmState.NotRequested, SaveState.NotQueued, HandoffState.NotReady,
            null, null, null, [], Identity: identity);
}
