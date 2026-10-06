using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Application.Station01;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Persistence;
using Gaode.Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Gaode.Integration.Tests.Storage;

// Real Host + SQLite component. FullSimulation is explicit: this does not replace
// the independently launched PLC/Worker BA acceptance rows.
public sealed class RecipeApplicationReceiptTests
{
    [Theory]
    [InlineData("BA04-boundary/late-bound", false)]
    [InlineData("BA04-boundary/late-handoff", true)]
    public async Task CommittedBindingOrHandoffCannotAuthorizeAfterItsReceiptExpires(string caseId, bool holdHandoff)
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var captured = new TaskCompletionSource<(WriteBatch Batch, CommitReceipt Receipt)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ActionWindow? applicationWindow = null;
        Guid? bindingId = null;
        await using var fixture = await Station01HostFixture.CreateAsync(services =>
        {
            services.RemoveAll<TraceWriter>();
            services.AddSingleton(sp => new TraceWriter(sp.GetRequiredService<DbContextOptions<Station01DbContext>>(),
                sp.GetRequiredService<TimeProvider>(), sp.GetRequiredService<IPublicConfiguration>()
                    .LoadBudget(new("s01-budget-dev", "3.0.0")).Value.Limits.Writer,
                afterCommitBeforeReceipt: async (batch, receipt, token) =>
                {
                    using var json = JsonDocument.Parse(batch.PayloadJson);
                    var isBound = batch.Kind == WriteKind.ActionFact &&
                        json.RootElement.TryGetProperty("kind", out var kind) && kind.GetString() == "RecipePlanBound";
                    if (isBound)
                    {
                        applicationWindow = json.RootElement.GetProperty("window").Deserialize<ActionWindow>(
                            new JsonSerializerOptions(JsonSerializerDefaults.Web));
                        bindingId = json.RootElement.GetProperty("bindingId").GetGuid();
                    }
                    if (holdHandoff ? batch.Kind != WriteKind.HandoffV2 : !isBound) return;
                    Assert.Equal(CommitState.Committed, receipt.State);
                    Assert.NotNull(receipt.CommittedUtc);
                    captured.TrySetResult((batch, receipt));
                    await release.Task.WaitAsync(token);
                }));
        }, recipeFixtureCode: "RC:R-S1-A-CAP:0.4.0-review");
        var request = new StartPublicRequest(Guid.NewGuid().ToString("N"), StartRunContextJson.Create(),
            new("s01-public-dev", "1.0.0"), new("s01-budget-dev", "3.0.0"), new("s01-sim-normal", "3.0.0"));
        var submitted = await fixture.Client.PostAsJsonAsync("/api/v1/station01/runs", request, watchdog.Token);
        Assert.Equal(HttpStatusCode.Accepted, submitted.StatusCode);
        var start = (await submitted.Content.ReadFromJsonAsync<StartReceipt>(watchdog.Token))!;
        (WriteBatch Batch, CommitReceipt Receipt) actual;
        RunApiSnapshot? denied = null;
        JsonElement deniedWire = default;
        try
        {
            actual = await captured.Task.WaitAsync(watchdog.Token);
            Assert.Equal(start.RunId, actual.Batch.RunId);
            Assert.NotNull(applicationWindow);
            Assert.NotNull(bindingId);
            var options = new DbContextOptionsBuilder<Station01DbContext>()
                .UseSqlite(StoreCompatibilityProbe.ReadOnlyConnectionString(fixture.StoreRoot)).Options;
            await using var db = new Station01DbContext(options);
            var committed = await db.Writes.AsNoTracking().SingleAsync(x => x.WriteId == actual.Batch.WriteId, watchdog.Token);
            Assert.Equal(actual.Receipt.CommittedRevision, committed.Revision);
            Assert.Equal(actual.Batch.PayloadJson, committed.PayloadJson);
            Assert.Equal(holdHandoff ? 1 : 0, await db.PublicPreparationHandoffsV2.CountAsync(x => x.RunId == start.RunId, watchdog.Token));
            do
            {
                using var response = JsonDocument.Parse(await fixture.Client.GetStringAsync(start.StatusUrl, watchdog.Token));
                deniedWire = response.RootElement.Clone();
                denied = deniedWire.Deserialize<RunApiSnapshot>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (denied?.State == RunState.Blocked) break;
                await Task.Delay(20, watchdog.Token);
            } while (true);
            Assert.Equal(SaveState.CommitUnknown, denied.Save);
            Assert.NotEqual("Bound", denied.RecipeState);
            Assert.NotEqual(HandoffState.Ready, denied.Handoff);
            Assert.DoesNotContain(denied.Events, e => e.StartsWith("DetectionRequestPrepared:", StringComparison.Ordinal));
            // Preserve the original total deadline as well as the earlier CriticalSave
            // rejection; releasing an actual late receipt must not revive this request.
            var remaining = applicationWindow!.DeadlineUtc - DateTimeOffset.UtcNow;
            if (remaining > TimeSpan.Zero) await Task.Delay(remaining, watchdog.Token);
        }
        finally { release.TrySetResult(); }
        var writer = fixture.Host.Services.GetRequiredService<TraceWriter>();
        await writer.WaitForIdleAsync(watchdog.Token);
        var reconciled = await writer.ReconcileAsync(actual.Batch.WriteId, watchdog.Token);
        Assert.Equal(CommitState.Committed, reconciled?.State);
        using var afterResponse = JsonDocument.Parse(await fixture.Client.GetStringAsync(start.StatusUrl, watchdog.Token));
        var afterWire = afterResponse.RootElement.Clone();
        var after = afterWire.Deserialize<RunApiSnapshot>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(RunState.Blocked, after.State);
        Assert.NotEqual("Bound", after.RecipeState);
        Assert.NotEqual(HandoffState.Ready, after.Handoff);
        Assert.DoesNotContain(after.Events, e => e.StartsWith("DetectionRequestPrepared:", StringComparison.Ordinal));
        Assert.Equal(1, fixture.CommittedRecipeBindings);
        Assert.Equal(2, fixture.Plc.MoveCommands);
        var query = fixture.Host.Services.GetRequiredService<IStageHandoffQuery>();
        var storedHandoff = await query.GetCommittedV2Async(start.RunId, watchdog.Token);
        if (holdHandoff)
        {
            Assert.True(storedHandoff?.IsVerified);
            var h = storedHandoff!.Handoff;
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Host.Services
                .GetRequiredService<PublicPreparationHandoffV2Consumer>().CreateDetectionRequestAsync(start.RunId,
                    h.Identity.TrayId, h.PlanRevision, Guid.NewGuid(), fixture.Plc.Observe().ConnectionEpoch,
                    DateTimeOffset.UtcNow.AddSeconds(5), "must-not-revive-late-binding", cancellationToken: watchdog.Token));
            Assert.Equal("CurrentRecipeApplicationReceiptRequired", failure.Message);
        }
        else Assert.Null(storedHandoff);
        using var evidence = JsonDocument.Parse(await fixture.Client.GetStringAsync($"/api/v1/station01/runs/{start.RunId:D}/evidence", watchdog.Token));
        var application = Assert.Single(evidence.RootElement.GetProperty("motionEvidence").EnumerateArray()
            .Select(e => e.GetProperty("facts")), f => f.TryGetProperty("recipeApplication", out var r) && r.ValueKind == JsonValueKind.Object)
            .GetProperty("recipeApplication");
        Assert.Equal("AwaitingRequiredBusinessCommits", application.GetProperty("outcome").GetString());
        Assert.Equal(JsonValueKind.Null, application.GetProperty("hostValidatedTick").ValueKind);
        var uncertain = Assert.Single(application.GetProperty("requiredCommits").EnumerateArray(),
            c => c.GetProperty("kind").GetString() == (holdHandoff ? "Handoff" : "RecipePlanBound"));
        Assert.Equal(actual.Batch.WriteId, uncertain.GetProperty("writeId").GetGuid());
        Assert.Equal("Committed", uncertain.GetProperty("actualCommit").GetString());
        Assert.Equal(JsonValueKind.Null, uncertain.GetProperty("receiptValidity").ValueKind);
        Assert.Equal(JsonValueKind.Null, uncertain.GetProperty("hostReceivedTick").ValueKind);
        var file = Path.Combine(fixture.StoreRoot, caseId.Replace('/', '-') + ".json");
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(new { caseId, start.RunId, bindingId,
            applicationWindow, actual = new { actual.Batch, actual.Receipt }, reconciled, denied = deniedWire, after = afterWire, storedHandoff,
            fixture.CommittedRecipeBindings, fixture.Plc.MoveCommands, scope = "RealHostFullSimulationActualSqliteComponent" }), watchdog.Token);
        var root = Environment.GetEnvironmentVariable("GAODE_009_EVIDENCE_ROOT")
            ?? throw new InvalidOperationException("009EvidenceRootRequired");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, caseId.Replace('/', '-') + "-" + start.RunId.ToString("N") + ".json"),
            JsonSerializer.Serialize(new { caseId, fixture.StoreRoot, actualDatabaseEvidence = file,
                start.RunId, bindingId, applicationWindow, actual = new { actual.Batch, actual.Receipt }, reconciled, denied = deniedWire, after = afterWire,
                scope = "RealHostFullSimulationActualSqliteComponent;NoIndependentTcpClaim" }), watchdog.Token);
    }
}
