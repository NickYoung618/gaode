using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gaode.Application.Station01;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Integration.Tests.Support;
using Gaode.Application.Ports;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Gaode.Integration.Tests.Station01;

public sealed class NormalPublicPreparationTests
{
    [Fact]
    public async Task RealElapsedSimulationCommitsHandoffToRealSqliteAndMedia()
    {
        await using var fixture = await Station01HostFixture.CreateAsync(
            recipeFixtureCode: "RC:R-S1-A-CAP:0.4.0-review");
        var request = new StartPublicRequest(Guid.NewGuid().ToString("N"),
            StartRunContextJson.Create(),
            new("s01-public-dev", "1.0.0"), new("s01-budget-dev", "3.0.0"),
            new("s01-sim-normal", "3.0.0"));
        var response = await fixture.Client.PostAsJsonAsync("/api/v1/station01/runs", request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var receipt = await response.Content.ReadFromJsonAsync<StartReceipt>();
        Assert.NotNull(receipt);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(25);
        RunApiSnapshot? snapshot = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            snapshot = await fixture.Client.GetFromJsonAsync<RunApiSnapshot>("/api/v1/station01/runs/" + receipt.RunId);
            if (snapshot?.State == RunState.WaitingClamp) break;
            if (snapshot?.State == RunState.Blocked) throw new Exception("启动受阻: " + snapshot.ErrorCode);
            await Task.Delay(25);
        }
        Assert.True(snapshot?.State is RunState.WaitingClamp or RunState.HandoffReady);
        while (DateTimeOffset.UtcNow < deadline)
        {
            snapshot = await fixture.Client.GetFromJsonAsync<RunApiSnapshot>("/api/v1/station01/runs/" + receipt.RunId);
            if (snapshot?.State == RunState.Blocked ||
                snapshot?.Events.Contains("PublicHandoffV2Committed") == true) break;
            await Task.Delay(25);
        }
        Assert.NotNull(snapshot);
        Assert.True(snapshot.State != RunState.Blocked, JsonSerializer.Serialize(snapshot));
        Assert.Equal(RunState.HandoffReady, snapshot.State);
        Assert.Equal(TerminalOutcome.None, snapshot.FinalOutcome);
        Assert.Contains("PublicHandoffV2Committed", snapshot.Events);
        Assert.Contains(snapshot.Events, x => x.StartsWith("DetectionRequestPrepared:",
            StringComparison.Ordinal));
        Assert.Equal("Bound", snapshot.RecipeState);
        Assert.Equal("NotEvaluated", snapshot.QualityState);
        Assert.Equal(1, fixture.Plc.StartCommands);
        Assert.Equal(2, fixture.Plc.MoveCommands);
        Assert.Equal(1, fixture.CommittedRecipeBindings);
        var boundWrites = await RecipeBindingTestSupport.Query(fixture).GetWritesAsync(receipt.RunId, default);
        var bound = Assert.Single(boundWrites, w => w.Kind == WriteKind.ActionFact &&
            w.PayloadJson.Contains("RecipePlanBound", StringComparison.Ordinal));
        using var boundJson = JsonDocument.Parse(bound.PayloadJson);
        Assert.Equal(snapshot.RecipeExecution!.RecipeId, boundJson.RootElement.GetProperty("recipeId").GetString());
        var capture = PublicPreparationTestStore.Capture(fixture);
        var algorithm = PublicPreparationTestStore.Algorithm(fixture);
        Assert.Equal(1, capture.TriggerCount(CaptureRole.ThreeD));
        Assert.Equal(1, capture.TriggerCount(CaptureRole.F));
        Assert.Equal(1, algorithm.CallCount(AlgorithmRole.Height));
        Assert.Equal(1, algorithm.CallCount(AlgorithmRole.FDecode));
        var handoff = await fixture.Client.GetAsync("/api/v1/station01/runs/" + receipt.RunId + "/handoff");
        Assert.Equal(HttpStatusCode.OK, handoff.StatusCode);
        Assert.NotNull(handoff.Headers.ETag);
        var json = await handoff.Content.ReadAsStringAsync();
        var handoffValue = JsonSerializer.Deserialize<PublicPreparationHandoffV2>(json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(handoffValue);
        Assert.True(handoffValue.IsComplete);
        Assert.Equal("RC:R-S1-A-CAP:0.4.0-review", handoffValue.UniqueFCode);
        Assert.Equal(receipt.RunId, handoffValue.Identity.RunId);
        Assert.False(string.IsNullOrWhiteSpace(handoffValue.RecipeRunPlanReference));
        Assert.False(string.IsNullOrWhiteSpace(handoffValue.PlanRevision));
        Assert.False(string.IsNullOrWhiteSpace(handoffValue.RecipeBindingReference));
        Assert.Equal(handoffValue.PlanRevision, snapshot.PlanRevision);
        Assert.Equal(handoffValue.HandoffId, snapshot.HandoffId);
        Assert.Equal("HandoffReady", snapshot.WholeTaskState);
        Assert.DoesNotContain("PartId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FaceId", json, StringComparison.OrdinalIgnoreCase);
        using var conditionalHandoff = new HttpRequestMessage(HttpMethod.Get,
            "/api/v1/station01/runs/" + receipt.RunId + "/handoff");
        conditionalHandoff.Headers.IfNoneMatch.Add(handoff.Headers.ETag!);
        Assert.Equal(HttpStatusCode.NotModified,
            (await fixture.Client.SendAsync(conditionalHandoff)).StatusCode);
        Assert.True(File.Exists(Path.Combine(fixture.StoreRoot, "station01.test.db")));
        Assert.Equal(2, Directory.EnumerateFiles(Path.Combine(fixture.StoreRoot, "media-root"), "*", SearchOption.AllDirectories).Count());
        var query = fixture.Host.Services.GetRequiredService<ITraceQuery>();
        var writes = (await query.GetWritesAsync(receipt.RunId, default)).ToList();
        var stored = await PublicPreparationTestStore.Read(fixture, receipt.RunId);
        var intents = writes.Where(x => x.Kind == WriteKind.AlgorithmIntent).ToArray();
        var facts = writes.Where(x => x.Kind == WriteKind.AlgorithmFact).ToArray();
        Assert.Equal(2, intents.Length);
        Assert.Equal(2, facts.Length);
        Assert.True(intents[0].Revision < facts[0].Revision);
        Assert.True(intents[1].Revision < facts[1].Revision);
        var recipeIntent = Assert.Single(writes, x => x.Kind == WriteKind.ActionIntent &&
            x.PayloadJson.Contains("RecipePlanAndBindingIntent", StringComparison.Ordinal));
        var recipeFact = Assert.Single(writes, x => x.Kind == WriteKind.ActionFact &&
            x.PayloadJson.Contains("RecipePlanBound", StringComparison.Ordinal));
        Assert.True(recipeIntent.Revision < recipeFact.Revision);
        var calls = stored.Calls;
        Assert.Equal(2, calls.Length);
        Assert.All(calls, call =>
        {
            Assert.NotEqual(Guid.Empty, call.CallId);
            Assert.NotEqual(Guid.Empty, call.OperationId);
            Assert.NotEqual(Guid.Empty, call.CaptureId);
            Assert.NotEqual(Guid.Empty, call.IntentWriteId);
            Assert.NotEqual(Guid.Empty, call.SessionId);
            Assert.False(string.IsNullOrWhiteSpace(call.InvocationBasis));
            Assert.False(string.IsNullOrWhiteSpace(call.CapabilityId));
            Assert.True(call.DueTick > call.StartTick);
            Assert.True(call.BudgetMs > 0);
            Assert.Equal("Accepted", call.DispatchEvidence);
        });
        var mediaRows = stored.Media;
        Assert.All(calls, call => Assert.Contains(mediaRows, media => media.CaptureId == call.CaptureId));
        using var mediaClient = fixture.ClientForRole("EquipmentEngineer");
        foreach (var mediaRow in mediaRows)
        {
            var mediaResponse = await mediaClient.GetAsync($"/api/v1/station01/media/{mediaRow.MediaId}");
            Assert.Equal(HttpStatusCode.OK, mediaResponse.StatusCode);
            Assert.Equal(mediaRow.ByteLength, (await mediaResponse.Content.ReadAsByteArrayAsync()).LongLength);
        }
        var handoffWrite = Assert.Single(writes, x => x.Kind == WriteKind.HandoffV2);
        var approvalObservation = Assert.Single(writes, x => x.Kind == WriteKind.Audit &&
            x.PayloadJson.Contains("RecipeApplicationReceiptObserved", StringComparison.Ordinal));
        Assert.True(recipeFact.Revision < handoffWrite.Revision);
        Assert.True(handoffWrite.Revision < approvalObservation.Revision);
        Assert.Equal(approvalObservation.WriteId, writes[^1].WriteId);
        using var observation = JsonDocument.Parse(approvalObservation.PayloadJson);
        var recordedReceipt = observation.RootElement.GetProperty("receipt").Deserialize<RecipeApplicationReceipt>(
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.True(recordedReceipt?.WasCompletedInWindow);
        Assert.Contains(recordedReceipt!.RequiredCommits, x => x.WriteId == handoffWrite.WriteId &&
            x.PersistedRevision == handoffWrite.Revision);
        Assert.DoesNotContain(writes, x => x.Kind.ToString().Contains("Recipe", StringComparison.OrdinalIgnoreCase));
        Station01Evidence.AssertM1Boundary(capture.TriggerCount(CaptureRole.F),
            capture.TriggerCount(CaptureRole.ThreeD), fixture.Plc.MoveCommands,
            snapshot.RecipeState, snapshot.QualityState, writes.Select(x => x.Kind.ToString()));
        var persisted = await query.GetRunAsync(receipt.RunId, default);
        Assert.NotNull(persisted);
        Assert.Null(persisted.TerminalRevision);
        Assert.Equal(TerminalOutcome.None, persisted.Terminal);
        Assert.Equal(handoffWrite.Revision, stored.HandoffRevision);
        Assert.Equal(approvalObservation.Revision, persisted.Revision);
        Assert.Equal(0, stored.LegacyHandoffs);
        Assert.Equal(2, stored.Media.Length);
        var evidence = await Station01Evidence.SaveNormalAsync(fixture.StoreRoot, receipt.RunId,
            new { fixture.Plc.StartCommands, fixture.Plc.MoveCommands,
                threeDTriggers = capture.TriggerCount(CaptureRole.ThreeD),
                fTriggers = capture.TriggerCount(CaptureRole.F),
                heightCalls = algorithm.CallCount(AlgorithmRole.Height),
                decodeCalls = algorithm.CallCount(AlgorithmRole.FDecode) });
        Assert.True(File.Exists(evidence));
    }

    [Fact]
    public async Task ControlledClockDrivesTheSameHostWorkflowAndDurableNormalHandoff()
    {
        await using var fixture = await Station01HostFixture.CreateAsync(
            simulationId: "s01-sim-controlled-normal", recipeFixtureCode: "RC:R-S1-A-CAP:0.4.0-review");
        var clock = Assert.IsType<FakeTimeProvider>(fixture.Host.Services.GetRequiredService<TimeProvider>());
        var request = new StartPublicRequest(Guid.NewGuid().ToString("N"),
            StartRunContextJson.Create(),
            new("s01-public-dev", "1.0.0"), new("s01-budget-dev", "3.0.0"),
            new("s01-sim-controlled-normal", "3.0.0"));
        var response = await fixture.Client.PostAsJsonAsync("/api/v1/station01/runs", request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var receipt = (await response.Content.ReadFromJsonAsync<StartReceipt>())!;
        var lockObserved = false;
        RunApiSnapshot? snapshot = null;
        for (var step = 0; step < 500; step++)
        {
            snapshot = await fixture.Client.GetFromJsonAsync<RunApiSnapshot>(receipt.StatusUrl);
            lockObserved |= fixture.Plc.Observe().Clamp == ClampState.Secured;
            if (snapshot?.State == RunState.Blocked ||
                snapshot?.Events.Contains("PublicHandoffV2Committed") == true) break;
            clock.Advance(TimeSpan.FromMilliseconds(20));
            for (var drain = 0; drain < 4; drain++) await Task.Yield();
            await Task.Delay(2);
        }

        Assert.True(lockObserved);
        Assert.Equal(RunState.HandoffReady, snapshot?.State);
        Assert.Equal(1, fixture.Plc.StartCommands);
        Assert.Equal(2, fixture.Plc.MoveCommands);
        var capture = PublicPreparationTestStore.Capture(fixture);
        var algorithm = PublicPreparationTestStore.Algorithm(fixture);
        Assert.Equal(1, capture.TriggerCount(CaptureRole.ThreeD));
        Assert.Equal(1, capture.TriggerCount(CaptureRole.F));
        Assert.Equal(1, algorithm.CallCount(AlgorithmRole.Height));
        Assert.Equal(1, algorithm.CallCount(AlgorithmRole.FDecode));
        var handoff = await fixture.Client.GetAsync($"/api/v1/station01/runs/{receipt.RunId}/handoff");
        Assert.Equal(HttpStatusCode.OK, handoff.StatusCode);
        Assert.Equal(2, Directory.EnumerateFiles(Path.Combine(fixture.StoreRoot, "media-root"),
            "*", SearchOption.AllDirectories).Count());
        Station01Evidence.AssertM1Boundary(1, 1, fixture.Plc.MoveCommands,
            snapshot!.RecipeState, snapshot.QualityState, []);
    }
}
