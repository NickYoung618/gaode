using System.Text.Json;
using System.Text.Json.Nodes;
using Gaode.Application.Configuration;
using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Application.Station01;
using Gaode.Application.Workflow;
using Gaode.Communication.Tests.Devices;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Host.Composition;
using Gaode.Infrastructure.Devices.Plc;
using Gaode.Infrastructure.Media;
using Gaode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Gaode.Host.Api;
using Microsoft.AspNetCore.Builder;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Hosting;

namespace Gaode.Communication.Tests;

// Legacy Test layout normal chain only. Site-layout safety rejection is tested
// independently by SiteProtocolAdaptationTests. No actual equipment or SDK.
[Collection("CommunicationTcp")]
public sealed class CommissioningWorkflowTests
{
    [Fact]
    public async Task LegacyTestHostRunsPublicPreparationBindingFlipAndDurableWholeTrayWorkflow()
    {
        Gaode.Infrastructure.Diagnostics.HostWorkerCapacity.Ensure();
        var diagnosticPath = ControlledCommissioningTests.Inputs.EvidencePath("legacy-workflow.log");
        File.WriteAllText(diagnosticPath, "");
        using var diagnostics = new Gaode.Infrastructure.Diagnostics.RuntimeDiagnosticLogging(new ControlledCommissioningTests.FileLogger(diagnosticPath));
        using var inputs = new ControlledCommissioningTests.Inputs();
        await inputs.PrepareStore("Test");
        var recipe = Recipe011Data.ForSlots(2, 1);
        recipe = RecipeDefinitionSerialization.Deserialize(RecipeDefinitionSerialization.Serialize(recipe).Replace("test-frame", "SIM_MACHINE", StringComparison.Ordinal));
        recipe = recipe with { DefinitionDigest = RecipeDefinitionIdentity.ComputeDefinitionDigest(recipe) };
        var configRoot = inputs.Options.ConfigRoot;
        var p = JsonNode.Parse(File.ReadAllText(Path.Combine(configRoot, "public.json")))!;
        p["purpose"] = "Test"; p["source"] = "OFFLINE:legacy-Test-normal-chain";
        p["motion"]!["axes"] = new JsonArray("X", "Y", "Z"); p["motion"]!["capability"]!["id"] = "xyz.fixed";
        p["motion"]!["frame"] = "SIM_MACHINE"; p["motion"]!["positionTolerance"] = .01;
        foreach (var point in p["motion"]!["points"]!.AsObject().Select(x => x.Value).OfType<JsonNode>()) point["frame"] = "SIM_MACHINE";
        p["motion"]!["points"]!["unload"] = JsonSerializer.SerializeToNode(new FixedPoint("OFFLINE-unload", "1", 1, 2, "mm", "SIM_MACHINE", 3), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        p["parser"] = JsonSerializer.SerializeToNode(new ParserConfiguration(new("code.test-tray-format", "1.0"), "1"), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        foreach (var b in p["bindings"]!.AsArray()) b!["provider"] = b["role"]!.GetValue<string>() == "PLC" ? "Virtual" : "Simulated";
        File.WriteAllText(Path.Combine(configRoot, "public.json"), p.ToJsonString());
        var budget = JsonNode.Parse(File.ReadAllText(Path.Combine(configRoot, "budget.json")))!;
        budget["purpose"] = "Test"; budget.AsObject().Remove("recipeExecution");
        budget["businessMs"]!["trayPoseAlgorithm"] = 1000;
        budget["businessMs"]!["flipCompletion"] = 1000;
        budget["businessMs"]!["putBackCompletion"] = 1000;
        File.WriteAllText(Path.Combine(configRoot, "budget.json"), budget.ToJsonString());
        var simulation = ReviewBusinessData.Read<SimulationProfile>("simulation.normal.json");
        File.WriteAllText(Path.Combine(configRoot, "simulation.json"), JsonSerializer.Serialize(simulation, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        await using var plc = new ProtocolTcpFixture(400);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        await plc.StartAsync(timeout.Token);
        await using var device = plc.Device(o => {
            o.SortingSafePosition = new(9, "mm", "SIM_MACHINE", "Test", "OFFLINE:explicit-safe-Z");
            o.PosePrograms = [new(recipe.Model, "motion", "test-1", "face-2", 2, new ushort[16], "OFFLINE:legacy-Test-pose", "Test")];
        });
        await HandshakeClosureTests.Ready(device, timeout.Token);
        var options = inputs.Options with { Mode = "VirtualPlcIntegration", PlcProvider = "Virtual", PlcPort = plc.Port,
            SimulationReference = new(simulation.Id, simulation.Version), CommissioningPath = null, CommissioningSha256 = null,
            PlcMechanicsPath = null, PlcFieldProfilePath = null, Cameras = null };
        var builder = WebApplication.CreateBuilder();
        builder.Configuration["Gaode:Tokens:Operator"] = "OFFLINE-operator";
        var services = builder.Services;
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerProvider>(new FileProvider(diagnosticPath));
        var existingHosted = services.Where(s => s.ServiceType == typeof(IHostedService)).ToArray();
        services.AddStation01(options);
        // Device and persistence lifecycle are driven explicitly by this offline fixture.
        foreach (var service in services.Where(s => s.ServiceType == typeof(IHostedService) && !existingHosted.Contains(s)).ToArray()) services.Remove(service);
        services.AddStation01Api(builder.Configuration);
        services.AddSingleton(device);
        services.AddSingleton<IRecipeCatalog>(new Catalog(recipe));
        services.AddSingleton<IAlgorithmPort>(sp => new FileReadingTestAlgorithm(sp.GetRequiredService<MediaStore>(), recipe.FCode));
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization(); app.MapStation01Api();
        app.Urls.Add("http://127.0.0.1:0"); await app.StartAsync(timeout.Token);
        var provider = app.Services;
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "OFFLINE-operator");
        var lifecycle = provider.GetRequiredService<Gaode.Host.Lifecycle.Station01HostedService>();
        await lifecycle.InitializePersistenceAsync(timeout.Token);
        var coordinator = provider.GetRequiredService<Station01Coordinator>();
        var starts = provider.GetRequiredService<StartPublicPreparation>();
        var tray = Guid.NewGuid();
        var context = JsonSerializer.Serialize(new { schemaVersion = StartRunContext.RecipeSchemaVersion, trayId = tray,
            stationId = Guid.NewGuid().ToString(), lineId = Guid.NewGuid().ToString(), scenarioId = recipe.ScenarioId, occupiedSlots = new[] { "s1" }, purpose = "Test",
            expectedRecipeRef = new { recipe.RecipeId, recipe.Version, recipe.CatalogDigest } }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        try
        {
            var receipt = starts.Start("OFFLINE", new("OFFLINE-normal-chain", context, options.PublicReference, options.BudgetReference, options.SimulationReference));
            await ProtocolTcpFixture.UntilAsync(() => coordinator.Query(receipt.RunId)?.State is RunState.AwaitingManualRemoval or RunState.Blocked or RunState.RecoveryRequired or RunState.ConfigurationBlocked, timeout.Token);
            var state = coordinator.Query(receipt.RunId)!;
            var traces = await provider.GetRequiredService<ITraceQuery>().GetWritesAsync(receipt.RunId, timeout.Token);
            var evidence = ControlledCommissioningTests.Inputs.EvidencePath("legacy-test-workflow.json");
            File.WriteAllText(evidence, JsonSerializer.Serialize(new { scope = "OFFLINE:legacy-Test-layout;formal-Host-chain;no-site-admission", state, traces }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
            Assert.True(state.State == RunState.AwaitingManualRemoval, $"state={state.State}; {state.ErrorCode}; evidence={evidence}");
            Assert.Contains(traces, w => w.Kind == WriteKind.HandoffV2 && w.State == CommitState.Committed);
            Assert.Contains(traces, w => w.Kind == WriteKind.ActionIntent && w.PayloadJson.Contains("RecipePlanAndBindingIntent"));
            await using var db = new Station01DbContext(provider.GetRequiredService<DbContextOptions<Station01DbContext>>());
            Assert.Equal(7, await db.Media.CountAsync(m => m.RunId == receipt.RunId, timeout.Token));
            Assert.Contains(await db.StageEvents.Where(e => e.RunId == receipt.RunId).ToListAsync(timeout.Token), e => e.PayloadJson.Contains("Flip") || e.EventType.Contains("Flip"));
            Assert.NotNull(state.WholeTrayCompletionId);
            using var confirmed = await client.PostAsJsonAsync($"/api/v1/station01/runs/{receipt.RunId:D}/manual-removal-confirmations",
                new { requestId = "OFFLINE-manual-removal", expectedRevision = state.ObservedRevision, reason = "OFFLINE:explicit Test human confirmation; no physical tray" }, timeout.Token);
            Assert.True(confirmed.StatusCode == HttpStatusCode.Accepted, await confirmed.Content.ReadAsStringAsync(timeout.Token));
            Assert.Equal(RunState.Completed, coordinator.Query(receipt.RunId)!.State);
            // Fresh contexts/readers reconstruct the outcome from durable facts, not this coordinator mutation.
            var dbOptions = provider.GetRequiredService<DbContextOptions<Station01DbContext>>();
            var query = new TraceQuery(dbOptions);
            var stageStore = new StageEventStore(dbOptions);
            var persisted = await query.GetRunAsync(receipt.RunId, timeout.Token);
            Assert.NotNull(persisted);
            Assert.Equal(RunState.Completed, persisted.State);
            Assert.Equal(TerminalOutcome.Completed, persisted.Terminal);
            var reloaded = await RuntimeObservationProjection.ReadAsync(state with {
                State = persisted.State, FinalOutcome = persisted.Terminal, PersistedRevision = persisted.Revision
            }, query, stageStore, timeout.Token);
            Assert.Equal(RunState.Completed, reloaded.State);
            Assert.Equal(TerminalOutcome.Completed, reloaded.FinalOutcome);
            Assert.Equal("FinalUnloadCompletion", reloaded.WholeTaskState);
            var manual = await stageStore.ReadAsync(receipt.RunId, tray, WholeTrayWorkflowStage.ManualTrayRemovalConfirmation, timeout.Token);
            var final = Assert.Single(manual, e => e.EventType == StageEventType.FinalUnloadCompleted);
            Assert.Equal(ResultSource.Test, Assert.Single(manual, e => e.EventType == StageEventType.ManualTrayRemovalConfirmed).Source);
            Assert.Equal(ResultSource.HostDerived, final.Source);
            Assert.Contains("OFFLINE-manual-removal", final.IdempotencyKey);
            using var get = await client.GetAsync($"/api/v1/station01/runs/{receipt.RunId:D}", timeout.Token);
            Assert.Equal(HttpStatusCode.OK, get.StatusCode);
            File.WriteAllText(ControlledCommissioningTests.Inputs.EvidencePath("manual-final-completion.json"), JsonSerializer.Serialize(new {
                scope = "OFFLINE:legacy-Test-layout;formal-manual-confirmation-API;actual-SQLite-reload", receipt.RunId, tray,
                confirmation = await confirmed.Content.ReadAsStringAsync(timeout.Token), reloaded, manual,
                queried = await get.Content.ReadAsStringAsync(timeout.Token) }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
            using var admissionResponse = await client.GetAsync("/api/v1/station01/start-admission", timeout.Token);
            admissionResponse.EnsureSuccessStatusCode();
            using var admission = JsonDocument.Parse(await admissionResponse.Content.ReadAsStringAsync(timeout.Token));
            Assert.Equal("Available", admission.RootElement.GetProperty("state").GetString());
            Assert.Equal(receipt.RunId, admission.RootElement.GetProperty("completedRunId").GetGuid());
            var detectionFacts = await stageStore.ReadAsync(receipt.RunId, tray, WholeTrayWorkflowStage.Detection, timeout.Token);
            var intent = detectionFacts.First(e => e.EventType == StageEventType.IntentRecorded);
            Assert.StartsWith($"run:{receipt.RunId:N}:tray:{tray:N}:", intent.IdempotencyKey);
            var replayRequest = new StageEventAppendRequest(Guid.NewGuid(), intent.RunId, intent.TrayId,
                intent.StationId, intent.LineId, intent.Stage, intent.OperationId, intent.Attempt, intent.ConnectionEpoch,
                intent.EventType, intent.OccurredAt, intent.Source, intent.Quality, intent.ErrorCode,
                intent.PayloadDigest, intent.PayloadJson, intent.IdempotencyKey, intent.PlanRevision,
                intent.StageStartedAtUtc, intent.StageDeadlineAtUtc);
            Assert.Equal(StageEventCommitState.Replay, (await stageStore.AppendAsync(replayRequest, timeout.Token)).State);
            Assert.Equal(StageEventCommitState.Conflict, (await stageStore.AppendAsync(
                replayRequest with { EventId = Guid.NewGuid(), PayloadDigest = "different-payload" }, timeout.Token)).State);
            Assert.Equal(detectionFacts.Count, (await stageStore.ReadAsync(receipt.RunId, tray, WholeTrayWorkflowStage.Detection, timeout.Token)).Count);
            var simulatedCapture = Assert.IsType<Gaode.Infrastructure.Simulation.SimulatedCapture>(provider.GetRequiredService<ICapturePort>());
            var duplicateF = new CaptureRequest(HandshakeClosureTests.Envelope() with { RunId = receipt.RunId },
                Guid.NewGuid(), CaptureRole.F, "OFFLINE-duplicate", "1", null, null, "F", null, Guid.NewGuid(), 1024);
            await Assert.ThrowsAsync<InvalidOperationException>(() => simulatedCapture.RequestCaptureAsync(
                duplicateF, _ => Assert.Fail("Duplicate F request must not be accepted"), timeout.Token).AsTask());
            Assert.Equal(1, simulatedCapture.TriggerCount(CaptureRole.F));
            // A second explicit request on the same host must traverse the full chain;
            // no test-only registry release, reset or service reconstruction is used.
            var secondTray = Guid.NewGuid();
            var secondContext = JsonNode.Parse(context)!;
            secondContext["trayId"] = secondTray;
            var second = starts.Start("OFFLINE", new("OFFLINE-normal-chain-second", secondContext.ToJsonString(),
                options.PublicReference, options.BudgetReference, options.SimulationReference));
            Assert.NotEqual(receipt.RunId, second.RunId);
            await ProtocolTcpFixture.UntilAsync(() => coordinator.Query(second.RunId)?.State is
                RunState.AwaitingManualRemoval or RunState.Blocked or RunState.RecoveryRequired or RunState.ConfigurationBlocked, timeout.Token);
            var secondState = coordinator.Query(second.RunId)!;
            Assert.True(secondState.State == RunState.AwaitingManualRemoval,
                $"second state={secondState.State}; error={secondState.ErrorCode}");
            Assert.Equal(7, await db.Media.CountAsync(m => m.RunId == second.RunId, timeout.Token));
            using var secondConfirmation = await client.PostAsJsonAsync(
                $"/api/v1/station01/runs/{second.RunId:D}/manual-removal-confirmations",
                new { requestId = "OFFLINE-manual-removal-second", expectedRevision = secondState.ObservedRevision,
                    reason = "OFFLINE:second explicit human confirmation; no physical tray" }, timeout.Token);
            Assert.Equal(HttpStatusCode.Accepted, secondConfirmation.StatusCode);
            var secondPersisted = await new TraceQuery(dbOptions).GetRunAsync(second.RunId, timeout.Token);
            Assert.NotNull(secondPersisted);
            Assert.Equal(RunState.Completed, secondPersisted.State);
            Assert.Equal(TerminalOutcome.Completed, secondPersisted.Terminal);
            var secondManual = await new StageEventStore(dbOptions).ReadAsync(second.RunId, secondTray,
                WholeTrayWorkflowStage.ManualTrayRemovalConfirmation, timeout.Token);
            Assert.Single(secondManual, e => e.EventType == StageEventType.FinalUnloadCompleted);
            Assert.Single(secondManual, e => e.EventType == StageEventType.ManualTrayRemovalConfirmed);
            using var secondAdmissionResponse = await client.GetAsync("/api/v1/station01/start-admission", timeout.Token);
            secondAdmissionResponse.EnsureSuccessStatusCode();
            using var secondAdmission = JsonDocument.Parse(await secondAdmissionResponse.Content.ReadAsStringAsync(timeout.Token));
            Assert.Equal("Available", secondAdmission.RootElement.GetProperty("state").GetString());
            Assert.Equal(second.RunId, secondAdmission.RootElement.GetProperty("completedRunId").GetGuid());
            Assert.Equal(2, simulatedCapture.TriggerCount(CaptureRole.F));
            Assert.Equal(RunState.Completed, (await new TraceQuery(dbOptions).GetRunAsync(receipt.RunId, timeout.Token))!.State);
            File.WriteAllText(ControlledCommissioningTests.Inputs.EvidencePath("two-round-final-completion.json"),
                JsonSerializer.Serialize(new { scope = "OFFLINE:legacy-Test-layout;formal-host-two-rounds;no-site-authority;not-desktop-DOM",
                    firstRun = receipt.RunId, secondRun = second.RunId, firstTray = tray, secondTray,
                    secondPersisted, secondManual, secondAdmission = secondAdmission.RootElement.Clone(),
                    mediaPerRun = 7, registryManuallyReleased = false, hostRebuiltBetweenRuns = false,
                    sameRunEventReplay = "Replay", sameRunChangedPayload = "Conflict", sameRunFRepeat = "Rejected" },
                    new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        }
        finally { await starts.StopAsync(CancellationToken.None); await coordinator.StopConsumerAsync(CancellationToken.None); await app.StopAsync(CancellationToken.None); }
    }
    private sealed class Catalog(RecipeDefinition recipe) : IRecipeCatalog
    { public RecipeCatalogSnapshot GetSnapshot() => new(RecipeCatalogSnapshot.CurrentSchema, recipe.CatalogDigest, [recipe]); }
    private sealed class FileProvider(string path) : Microsoft.Extensions.Logging.ILoggerProvider
    {
        private readonly ControlledCommissioningTests.FileLogger logger = new(path);
        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) => logger;
        public void Dispose() { }
    }
    internal sealed class FileReadingTestAlgorithm(MediaStore media, string code) : IAlgorithmPort, IAlgorithmCapabilityProvider
    {
        public ComponentExecutionOrigin Origin => new(ComponentEvidenceSource.Test, "OFFLINE-file-reading-algorithm/1", "DeclaredTestFixture");
        public string ImplementationReference => "OFFLINE:declared-file-reading-fixture";
        public IReadOnlyList<AlgorithmCapabilityDeclaration> AlgorithmCapabilities { get; } = [
            new(AlgorithmPurpose.SingleDetection, "test-single", "1", "test-defect/1", 1, "test-1"),
            new(AlgorithmPurpose.FaceFusion, "test-fusion", "1", "test-fusion/1", 2, "test-1"),
            new(AlgorithmPurpose.TrayPose, "tray.observation", "1.0", "tray-observation/2", 1, "1") ];
        public int CallCount(AlgorithmRole role) => 0;
        public async ValueTask<AlgorithmDispatch> RequestAsync(AlgorithmRequest r, Action<AlgorithmEvent> onEvent, CancellationToken ct)
        {
            foreach (var input in r.Inputs) { using var lease = media.Lease(input.MediaId, "OFFLINE-fixture"); await using var file = await media.OpenReadAsync(input.MediaId, ct); Assert.True(file.Length > 0); }
            var result = new AlgorithmEvent(r, AlgorithmEventKind.Result, RawCodes: r.Role == AlgorithmRole.FDecode ? [code] : null, WorkerSessionId: Guid.NewGuid(), DetectionDisposition: r.Role == AlgorithmRole.Detection ? "OK" : null);
            if (r.Role == AlgorithmRole.TrayPose)
            {
                var c = r.ObservationContext!;
                result = result with { Observation = new(Guid.NewGuid(), r.Envelope.RunId, c.TrayId, r.CaptureId, r.CallId, DateTimeOffset.UtcNow, c.Purpose, c.CheckRound, c.RelatedTransitionId,
                    [new(1, TrayPresence.Present, TrayPose.Normal) { CellId = "r1:c4", Region = "OK", Row = 1, Column = 4 }],
                    c.Purpose == TrayObservationPurpose.InitialPreparation ? new(10, 20, "mm", "SIM_MACHINE", "OFFLINE:Test-only") : null, Origin, ["OFFLINE:declared-test-input"]) {
                        SchemaVersion = "tray-observation/2", MappingSourceReference = "OFFLINE:Test-only-map", ExpectedPhysicalSlotIndices = [1] } };
            }
            onEvent(result); return new(Task.CompletedTask);
        }
    }
}
