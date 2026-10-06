using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Application.Station01;
using Gaode.Application.Workflow;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Host.Api;
using Gaode.Infrastructure.Persistence;
using Gaode.Infrastructure.Recipes;
using Gaode.Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Gaode.Integration.Tests.Station01;

public sealed partial class StrictQ01SaveGateTests
{
    internal static async Task<string> RunSaveGateAsync()
    {
        var workspace = Station01HostFixture.FindWorkspace();
        var root = Path.Combine(workspace, "specs", "008-recipe-driven-inspection", "fixtures");
        var catalog = new JsonRecipeCatalog(Path.Combine(root, "recipes.json"));
        var selected = Assert.Single(catalog.GetSnapshot().Definitions);
        var fault = new RejectThirdMediaWrite();
        await using var rig = await VirtualLoopTestRig.CreateAsync(services =>
        {
            services.RemoveAll<IRecipeCatalog>();
            services.AddSingleton<IRecipeCatalog>(catalog);
            services.RemoveAll<ITraceWriter>();
            services.AddSingleton<ITraceWriter>(sp =>
            {
                fault.Inner = sp.GetRequiredService<TraceWriter>();
                return fault;
            });
        }, imageManifestPath: Path.Combine(root, "media-manifest.json"),
           workerManifestPath: Path.Combine(root, "worker-manifest.json"));
        var context = JsonSerializer.Serialize(new
        {
            schemaVersion = StartRunContext.RecipeSchemaVersion, trayId = Guid.NewGuid(),
            stationId = "10000000-0000-0000-0000-000000000001",
            lineId = "20000000-0000-0000-0000-000000000001",
            scenarioId = "S1", occupiedSlots = new[] { "P01" }, purpose = "Test",
            expectedRecipeRef = new { recipeId = selected.RecipeId, version = selected.Version,
                catalogDigest = selected.CatalogDigest }
        });
        var request = new StartPublicRequest("q01-save-gate-" + Guid.NewGuid().ToString("N"),
            context, new ConfigReference("s01-public-virtual-loop", "1.2.0"),
            new ConfigReference("s01-budget-virtual-loop", "3.0.0"),
            new ConfigReference("s01-sim-virtual-loop", "3.0.0"));
        var response = await rig.Host.Client.PostAsJsonAsync("/api/v1/station01/runs", request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var receipt = (await response.Content.ReadFromJsonAsync<StartReceipt>())!;
        var beforeUnlock = (await rig.Host.Client.GetFromJsonAsync<RunApiSnapshot>(
            $"/api/v1/station01/runs/{receipt.RunId:D}"))!;
        var premature = await rig.Host.Client.PostAsJsonAsync(
            $"/api/v1/station01/runs/{receipt.RunId:D}/manual-removal-confirmations",
            new ManualTrayRemovalApiRequest("premature-q01-save-gate",
                beforeUnlock.ObservedRevision, "尚未解锁，不得确认取盘"));
        Assert.Equal(HttpStatusCode.Conflict, premature.StatusCode);
        var final = await rig.WaitAsync(receipt.RunId, TimeSpan.FromSeconds(180),
            RunState.Blocked, RunState.RecoveryRequired);
        Assert.Equal(TerminalOutcome.None, final.FinalOutcome);
        Assert.Equal(1, fault.RejectedProductMediaWrites);
        Assert.Equal(2, fault.PublicMediaWrites);
        var options = rig.Host.Host.Services.GetRequiredService<DbContextOptions<Station01DbContext>>();
        await using var db = new Station01DbContext(options);
        Assert.False(await db.StageEvents.AsNoTracking().AnyAsync(x => x.RunId == receipt.RunId &&
            x.EventType == StageEventType.FinalUnloadCompleted.ToString()));
        Assert.False(await db.StageEvents.AsNoTracking().AnyAsync(x => x.RunId == receipt.RunId &&
            x.Stage == WholeTrayWorkflowStage.UnloadPreparation.ToString() &&
            x.EventType == StageEventType.Completed.ToString()));
        return await rig.SaveEvidenceAsync("q01-product-media-save-gate", request, response, receipt,
            new { selected.RecipeId, selected.Version, selected.CatalogDigest,
                fault.PublicMediaWrites, fault.RejectedProductMediaWrites,
                final.State, final.ErrorCode, final.FinalOutcome,
                disposition = "NecessaryProductMediaSaveFailed_NoNextMove_NoFinal" });
    }

    private sealed class RejectThirdMediaWrite : ITraceWriter
    {
        public ITraceWriter Inner { get; set; } = null!;
        private int mediaWrites;
        public int PublicMediaWrites => Math.Min(mediaWrites, 2);
        public int RejectedProductMediaWrites { get; private set; }
        public QueuedWrite SubmitCritical(WriteBatch batch, CancellationToken cancellationToken = default,
            Gaode.Domain.Station01.ActionWindow? window = null)
        {
            if (batch.Kind != WriteKind.Media || Interlocked.Increment(ref mediaWrites) != 3)
                return Inner.SubmitCritical(batch, cancellationToken, window);
            RejectedProductMediaWrites++;
            var queued = new CommitReceipt(batch.WriteId, batch.RunId, CommitState.Queued,
                null, TerminalOutcome.None, null);
            return new(queued, Task.FromResult(queued with
            {
                State = CommitState.Failed, ErrorCode = "Test/InjectedProductMediaSaveFailure"
            }));
        }
        public Task<CommitReceipt?> ReconcileAsync(Guid writeId, CancellationToken token) =>
            Inner.ReconcileAsync(writeId, token);
    }
}