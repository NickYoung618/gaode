using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gaode.Application.Recipes;
using Gaode.Application.Station01;
using Gaode.Application.Workflow;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Persistence;
using Gaode.Infrastructure.Recipes;
using Gaode.Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Gaode.Integration.Tests.Station01;

public sealed partial class ExpectedRecipeMismatchTests
{
    internal static async Task<string> RunMismatchAsync()
    {
        var workspace = Station01HostFixture.FindWorkspace();
        var catalogPath = Path.Combine(workspace, "specs", "008-recipe-driven-inspection",
            "fixtures", "recipes.json");
        var catalog = new JsonRecipeCatalog(catalogPath);
        var selected = Assert.Single(catalog.GetSnapshot().Definitions);
        Assert.True(RecipeAdmission.Evaluate(selected, ["P01"], "Test").Eligible);
        var inputRoot = Path.Combine(workspace, "artifacts", "recipe-execution-008",
            "sixth-batch-inputs", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(inputRoot);
        var worker = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(workspace,
            "specs", "008-recipe-driven-inspection", "fixtures", "worker-manifest.json")))!;
        worker["fCode"] = "TEST-TRAY-0202";
        var workerPath = Path.Combine(inputRoot, "worker-manifest-actual-f-different.json");
        await File.WriteAllTextAsync(workerPath, worker.ToJsonString());
        await using var rig = await VirtualLoopTestRig.CreateAsync(services =>
        {
            services.RemoveAll<IRecipeCatalog>();
            services.AddSingleton<IRecipeCatalog>(catalog);
        }, workerManifestPath: workerPath);
        var context = JsonSerializer.Serialize(new
        {
            schemaVersion = StartRunContext.RecipeSchemaVersion,
            trayId = Guid.NewGuid(),
            stationId = "10000000-0000-0000-0000-000000000001",
            lineId = "20000000-0000-0000-0000-000000000001",
            scenarioId = "S1", occupiedSlots = new[] { "P01" }, purpose = "Test",
            expectedRecipeRef = new { recipeId = selected.RecipeId, version = selected.Version,
                catalogDigest = selected.CatalogDigest }
        });
        var request = new StartPublicRequest("expected-f-mismatch-" + Guid.NewGuid().ToString("N"),
            context, new ConfigReference("s01-public-virtual-loop", "1.2.0"),
            new ConfigReference("s01-budget-virtual-loop", "3.0.0"),
            new ConfigReference("s01-sim-virtual-loop", "3.0.0"));
        var response = await rig.Host.Client.PostAsJsonAsync("/api/v1/station01/runs", request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var receipt = (await response.Content.ReadFromJsonAsync<StartReceipt>())!;
        var final = await rig.WaitAsync(receipt.RunId, TimeSpan.FromSeconds(100), RunState.Blocked);
        // Blocked can be published before the diagnostic projection has its error code.
        // Wait for the persisted rejection fact; this does not retry any device action.
        var projectionDeadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
        while (!string.Equals(final.ErrorCode, "InvalidOperationException:ExpectedRecipeFMismatch",
                   StringComparison.Ordinal) && DateTimeOffset.UtcNow < projectionDeadline)
        {
            await Task.Delay(25);
            final = (await rig.Host.Client.GetFromJsonAsync<RunApiSnapshot>(
                $"/api/v1/station01/runs/{receipt.RunId:D}"))!;
        }
        Assert.Contains("ExpectedRecipeFMismatch", final.ErrorCode ?? "", StringComparison.Ordinal);
        Assert.Equal(TerminalOutcome.None, final.FinalOutcome);
        Assert.Null(final.PlanRevision);
        var options = rig.Host.Host.Services.GetRequiredService<DbContextOptions<Station01DbContext>>();
        await using var db = new Station01DbContext(options);
        var writes = await db.Writes.AsNoTracking().Where(x => x.RunId == receipt.RunId).ToListAsync();
        Assert.Contains(writes, x => x.PayloadJson.Contains("ExpectedRecipeFMismatch", StringComparison.Ordinal));
        Assert.DoesNotContain(writes, x => x.PayloadJson.Contains("RecipePlanBound", StringComparison.Ordinal));
        Assert.DoesNotContain(writes, x => x.PayloadJson.Contains("PublicHandoffV2Committed", StringComparison.Ordinal));
        Assert.False(await db.StageEvents.AsNoTracking().AnyAsync(x => x.RunId == receipt.RunId &&
            x.EventType == StageEventType.FinalUnloadCompleted.ToString()));
        return await rig.SaveEvidenceAsync("expected-recipe-f-mismatch", request, response, receipt,
            new { selected.RecipeId, selected.Version, selected.CatalogDigest,
                actualFCode = "TEST-TRAY-0202", final.State, final.ErrorCode, final.FinalOutcome,
                disposition = "BlockedBeforeBindingProductMovementAndFinal" });
    }
}
