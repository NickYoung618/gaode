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
using Microsoft.Extensions.Logging;

namespace Gaode.Communication.Tests;

// Legacy Test layout normal chain only. Site-layout safety rejection is tested
// independently by SiteProtocolAdaptationTests. No actual equipment or SDK.
[Collection("CommunicationTcp")]
public sealed class CommissioningWorkflowTests
{
    [Theory]
    [InlineData(false, false, "None")]
    [InlineData(true, false, "None")]
    [InlineData(true, true, "None")]
    [InlineData(false, false, "Release3D")]
    [InlineData(false, false, "ReleaseF")]
    [InlineData(false, false, "CloseBeforeEntry")]
    [InlineData(false, false, "Release3DTimeout")]
    public async Task LegacyTestHostRunsPublicPreparationBindingFlipAndDurableWholeTrayWorkflow(bool png, bool special, string probe)
    {
        var runEvidenceRoot=Environment.GetEnvironmentVariable("GAODE_COMMISSIONING_EVIDENCE_ROOT") ??
            Path.Combine(ControlledCommissioningTests.Inputs.RepoRoot(),"artifacts","022-host-"+Guid.NewGuid().ToString("N"));
        if(probe != "None") runEvidenceRoot=Path.Combine(runEvidenceRoot,probe);
        Directory.CreateDirectory(runEvidenceRoot);
        string EvidencePath(string name) => Path.Combine(runEvidenceRoot,(special ? "022-special-" : png ? "022-png-" : "native-")+name);
        Gaode.Infrastructure.Diagnostics.HostWorkerCapacity.Ensure();
        var diagnosticPath = EvidencePath("legacy-workflow.log");
        File.WriteAllText(diagnosticPath, "");
        using var fileLogger = new WorkflowFileLogger(diagnosticPath);
        using var diagnostics = new Gaode.Infrastructure.Diagnostics.RuntimeDiagnosticLogging(fileLogger);
        using var inputs = new ControlledCommissioningTests.Inputs();
        await inputs.PrepareStore("Test");
        var recipe = special ? Recipe011Data.ForSlots(2, 1, 2) : Recipe011Data.ForSlots(2, 1);
        if(special)
        {
            recipe=recipe with {InspectionKind=RecipeInspectionKind.SpecialRotation,Route="specialType1Part",RotationLoadingGripperId=1,
                RotationWorkstation=new(new(new("rotation-place","test-1",70,80,"mm","test-frame",3),"Test:explicit rotation station"),
                    new(new("rotation-pick","test-1",70,80,"mm","test-frame",3),"Test:explicit rotation station")),
                Stages=recipe.Stages.Select(stage=>stage with {Action="rotate",AngleDeg=stage.Number==1?10:90}).ToArray(),
                ExecutionPositions=recipe.ExecutionPositions.ToDictionary(pair=>pair.Key,pair=>pair.Value with {
                    PhysicalEntity=pair.Value.PhysicalEntity with {Flip=null,OriginPutBack=pair.Value.PhysicalEntity.Source}})};
        }
        recipe = RecipeDefinitionSerialization.Deserialize(RecipeDefinitionSerialization.Serialize(recipe).Replace("test-frame", "SIM_MACHINE", StringComparison.Ordinal));
        recipe = recipe with { DefinitionDigest = RecipeDefinitionIdentity.ComputeDefinitionDigest(recipe) };
        var configRoot = inputs.Options.ConfigRoot;
        var p = JsonNode.Parse(File.ReadAllText(Path.Combine(configRoot, "public.json")))!;
        p["purpose"] = "Test"; p["source"] = "OFFLINE:legacy-Test-normal-chain";
        p["motion"]!["axes"] = new JsonArray("X", "Y", "Z"); p["motion"]!["capability"]!["id"] = "xyz.fixed";
        p["motion"]!["frame"] = "SIM_MACHINE"; p["motion"]!["positionTolerance"] = .01;
        foreach (var point in p["motion"]!["points"]!.AsObject().Select(x => x.Value).OfType<JsonNode>()) point["frame"] = "SIM_MACHINE";
        p["motion"]!["points"]!["unload"] = JsonSerializer.SerializeToNode(new FixedPoint("OFFLINE-unload", "1", 1, 2, "mm", "SIM_MACHINE", 3), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        p["motion"]!["points"]!["f"]!["z"] = 1; // Test scan axis starts at 0 without a prior completed action; require an actual initial move.
        p["parser"] = JsonSerializer.SerializeToNode(new ParserConfiguration(new("code.test-tray-format", "1.0"), "1"), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        foreach (var b in p["bindings"]!.AsArray()) b!["provider"] = b["role"]!.GetValue<string>() == "PLC" ? "Virtual" : "Simulated";
        File.WriteAllText(Path.Combine(configRoot, "public.json"), p.ToJsonString());
        var budget = JsonNode.Parse(File.ReadAllText(Path.Combine(configRoot, "budget.json")))!;
        budget["purpose"] = "Test"; budget.AsObject().Remove("recipeExecution");
        budget["businessMs"]!["trayPoseAlgorithm"] = 1000;
        if(probe=="Release3DTimeout")budget["businessMs"]!["workerReleaseGrace"]=200;
        budget["businessMs"]!["flipCompletion"] = 1000;
        budget["businessMs"]!["putBackCompletion"] = 1000;
        File.WriteAllText(Path.Combine(configRoot, "budget.json"), budget.ToJsonString());
        var simulation = ReviewBusinessData.Read<SimulationProfile>("simulation.normal.json");
        File.WriteAllText(Path.Combine(configRoot, "simulation.json"), JsonSerializer.Serialize(simulation, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        await using var plc = new CommissioningProtocolTcpFixture(400);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        await plc.StartAsync(timeout.Token);
        await using var device = plc.Device(o => {
            o.RotationBasis = new(.01,"Test","OFFLINE:explicit Test rotation basis");
            o.SortingSafePosition = new(9, "mm", "SIM_MACHINE", "Test", "OFFLINE:explicit-safe-Z");
            o.PosePrograms = [new(recipe.Model, "motion", "test-1", "face-2", 2, new ushort[16], "OFFLINE:legacy-Test-pose", "Test")];
        });
        await HandshakeClosureTests.Ready(device, timeout.Token);
        var options = inputs.Options with { Mode = "VirtualPlcIntegration", PlcProvider = "Virtual", PlcPort = plc.Port,
            SimulationReference = new(simulation.Id, simulation.Version), CommissioningPath = null, CommissioningSha256 = null,
            PlcMechanicsPath = null, PlcFieldProfilePath = null, Cameras = null };
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders(); // Isolated file logging; no machine EventLog write permission required.
        builder.Configuration["Gaode:Tokens:Operator"] = "OFFLINE-operator";
        var services = builder.Services;
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerProvider>(new FileProvider(fileLogger));
        var existingHosted = services.Where(s => s.ServiceType == typeof(IHostedService)).ToArray();
        services.AddStation01(options);
        // Device and persistence lifecycle are driven explicitly by this offline fixture.
        foreach (var service in services.Where(s => s.ServiceType == typeof(IHostedService) && !existingHosted.Contains(s)).ToArray()) services.Remove(service);
        services.AddStation01Api(builder.Configuration);
        services.AddSingleton(device);
        var catalog=new Catalog(recipe,Path.Combine(inputs.Root,"test-approved-catalog.json"));
        RecipeDefinition? nextRecipe=null;
        var versionUpdate=png && !special;
        var barrierEvidence=new System.Collections.Concurrent.ConcurrentBag<object>();
        var blockedRegistration = new PausedResourceRegistration();
        var publicReleaseEntered = new TaskCompletionSource<AlgorithmRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
        var publicRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var publicReleased = 0;
        FileReadingTestAlgorithm? testAlgorithm = null;
        if(probe=="CloseBeforeEntry") services.AddSingleton<IAlgorithmResourceStore>(sp => {
            blockedRegistration.Inner=sp.GetRequiredService<StageEventStore>(); return blockedRegistration;
        });
        services.AddSingleton<IRecipeCatalog>(catalog);
        services.AddSingleton<IAlgorithmPort>(sp => testAlgorithm = new FileReadingTestAlgorithm(sp.GetRequiredService<MediaStore>(), recipe.FCode,png,recipe.Capacity,
            versionUpdate?()=>{
                var profile=recipe.CaptureProfiles["detect"];
                var candidate=recipe with {Version="test-version-2",CatalogDigest="test-catalog-2",
                    CaptureProfiles=new Dictionary<string,CaptureProfile>{{"detect",profile with {Version="test-2",Settings=profile.Settings with {Gain=2}}}},
                    AlgorithmRequirements=recipe.AlgorithmRequirements.ToDictionary(pair=>pair.Key,pair=>pair.Value with {ParametersVersion="test-2"})};
                nextRecipe=candidate with {DefinitionDigest=RecipeDefinitionIdentity.ComputeDefinitionDigest(candidate)};
                catalog.Save(nextRecipe);
            }:null,versionUpdate?async request=>{
                var before=plc.Engine.GetActionAudit().Actions.Count(action=>action.Phase=="accepted");
                var capture=sp.GetRequiredService<ICapturePort>();
                var capturedBefore=Enum.GetValues<CaptureRole>().Sum(role=>capture.TriggerCount(role));
                await Task.Delay(100,timeout.Token);
                var after=plc.Engine.GetActionAudit().Actions.Count(action=>action.Phase=="accepted");
                var capturedAfter=Enum.GetValues<CaptureRole>().Sum(role=>capture.TriggerCount(role));
                Assert.Equal(before,after);Assert.Equal(capturedBefore,capturedAfter);
                barrierEvidence.Add(new {request.Envelope.RunId,request.CallId,request.Role,request.CapabilityId,request.TargetIdentity,
                    resultWasPending=true,acceptedActionsBefore=before,acceptedActionsAfter=after,capturedBefore,capturedAfter});
            }:null,afterResult:request=>{
                var target=probe is "Release3D" or "Release3DTimeout" ? AlgorithmRole.TrayPose : AlgorithmRole.FDecode;
                if(probe is "Release3D" or "ReleaseF" or "Release3DTimeout" && request.Role==target && Interlocked.Exchange(ref publicReleased,1)==0)
                {publicReleaseEntered.TrySetResult(request);return publicRelease.Task;}
                return Task.CompletedTask;
            }));
        if(png) services.AddSingleton<ICapturePort>(new PngTestCapture());
        await using var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization(); app.MapStation01Api();
        app.Urls.Add("http://127.0.0.1:0"); await app.StartAsync(timeout.Token);
        var provider = app.Services;
        using var client = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = new Uri(app.Urls.Single()) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "OFFLINE-operator");
        var lifecycle = provider.GetRequiredService<Gaode.Host.Lifecycle.Station01HostedService>();
        await lifecycle.InitializePersistenceAsync(timeout.Token);
        var coordinator = provider.GetRequiredService<Station01Coordinator>();
        var starts = provider.GetRequiredService<StartPublicPreparation>();
        var tray = Guid.NewGuid();
        var context = JsonSerializer.Serialize(new { schemaVersion = StartRunContext.RecipeSchemaVersion, trayId = tray,
            stationId = Guid.NewGuid().ToString(), lineId = Guid.NewGuid().ToString(), scenarioId = recipe.ScenarioId, occupiedSlots = special ? new[] { "s1", "s2" } : new[] { "s1" }, purpose = "Test",
            expectedRecipeRef = new { recipe.RecipeId, recipe.Version, recipe.CatalogDigest } }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        try
        {
            var receipt = starts.Start("OFFLINE", new("OFFLINE-normal-chain", context, options.PublicReference, options.BudgetReference, options.SimulationReference));
            if(probe=="CloseBeforeEntry")
            {
                var registered=await blockedRegistration.Entered.Task.WaitAsync(timeout.Token);
                lifecycle.NotifyStopping();
                var due=provider.GetRequiredService<Gaode.Application.Algorithms.AlgorithmResourceSupervisor>().States.Single(x=>x.CallId==registered.CallId).ReleaseDueUtc;
                blockedRegistration.Resume.TrySetResult();
                await lifecycle.StopAsync(timeout.Token);
                Assert.Empty(testAlgorithm!.Requests);
                var rows=await provider.GetRequiredService<IStageEventStore>().ReadAsync(receipt.RunId,tray,WholeTrayWorkflowStage.Detection,timeout.Token);
                var finalResource=rows.Where(x=>x.EventType==StageEventType.AlgorithmLifecycleRecorded).Select(x=>JsonSerializer.Deserialize<AlgorithmResourceState>(x.PayloadJson,new JsonSerializerOptions(JsonSerializerDefaults.Web))!).Last(x=>x.CallId==registered.CallId);
                Assert.Equal("NotDispatched",finalResource.Dispatch);Assert.True(finalResource.Reclaimed);Assert.Equal(due,finalResource.ReleaseDueUtc);
                File.WriteAllText(EvidencePath("closed-public-entry.json"),JsonSerializer.Serialize(finalResource,new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true}));
                return;
            }
            if(probe=="Release3DTimeout")
            {
                var call=await publicReleaseEntered.Task.WaitAsync(timeout.Token);
                await CommissioningProtocolTcpFixture.UntilAsync(()=>coordinator.Query(receipt.RunId)?.State is RunState.Blocked or RunState.RecoveryRequired,timeout.Token);
                var supervisor=provider.GetRequiredService<Gaode.Application.Algorithms.AlgorithmResourceSupervisor>();
                await supervisor.FlushAsync(timeout.Token);
                var unknown=Assert.Single(await provider.GetRequiredService<IAlgorithmResourceStore>().GetUnreclaimedResourcesAsync(0,128,timeout.Token));
                Assert.Equal(call.CallId,unknown.CallId);Assert.True(unknown.ObservationExpired);Assert.False(unknown.Reclaimed);
                Assert.Equal(0,provider.GetRequiredService<ICapturePort>().TriggerCount(CaptureRole.F));
                Assert.DoesNotContain(testAlgorithm!.Requests,x=>x.Role==AlgorithmRole.FDecode);
                publicRelease.TrySetResult();await provider.GetRequiredService<Gaode.Application.Algorithms.AlgorithmRuntime>().WaitForIdleAsync(timeout.Token);
                Assert.Equal(0,provider.GetRequiredService<ICapturePort>().TriggerCount(CaptureRole.F));
                var rows=await provider.GetRequiredService<IStageEventStore>().ReadAsync(receipt.RunId,tray,WholeTrayWorkflowStage.Detection,timeout.Token);
                var reclaimed=rows.Where(x=>x.EventType==StageEventType.AlgorithmLifecycleRecorded).Select(x=>JsonSerializer.Deserialize<AlgorithmResourceState>(x.PayloadJson,new JsonSerializerOptions(JsonSerializerDefaults.Web))!).Last(x=>x.CallId==call.CallId);
                Assert.True(reclaimed.Reclaimed);Assert.Equal(unknown.ReleaseDueUtc,reclaimed.ReleaseDueUtc);
                File.WriteAllText(EvidencePath("public-release-expired.json"),JsonSerializer.Serialize(new{unknown,reclaimed},new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true}));
                return;
            }
            if(probe is "Release3D" or "ReleaseF")
            {
                var call=await publicReleaseEntered.Task.WaitAsync(timeout.Token);
                try
                {
                    var before=plc.Engine.GetActionAudit().Actions.Count(x=>x.Phase=="accepted");
                    var count=Enum.GetValues<CaptureRole>().Sum(x=>provider.GetRequiredService<ICapturePort>().TriggerCount(x));
                    await Task.Delay(100,timeout.Token);
                    Assert.Equal(before,plc.Engine.GetActionAudit().Actions.Count(x=>x.Phase=="accepted"));
                    Assert.Equal(count,Enum.GetValues<CaptureRole>().Sum(x=>provider.GetRequiredService<ICapturePort>().TriggerCount(x)));
                    var resource=Assert.Single(await provider.GetRequiredService<IAlgorithmResourceStore>().GetUnreclaimedResourcesAsync(0,128,timeout.Token));
                    Assert.Equal(call.CallId,resource.CallId);Assert.False(resource.InputsReleased);Assert.False(resource.ExecutionEnded);
                    Assert.NotEqual(RunState.Completed,coordinator.Query(receipt.RunId)!.State);
                    Assert.DoesNotContain(await provider.GetRequiredService<IStageEventStore>().ReadAsync(receipt.RunId,tray,WholeTrayWorkflowStage.ManualTrayRemovalConfirmation,timeout.Token),x=>x.EventType==StageEventType.FinalUnloadCompleted);
                    File.WriteAllText(EvidencePath("public-release-barrier.json"),JsonSerializer.Serialize(new{resource,acceptedActions=before,captures=count},new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true}));
                }
                finally {publicRelease.TrySetResult();}
            }

            await CommissioningProtocolTcpFixture.UntilAsync(() => coordinator.Query(receipt.RunId)?.State is RunState.AwaitingManualRemoval or RunState.Blocked or RunState.RecoveryRequired or RunState.ConfigurationBlocked, timeout.Token);
            var state = coordinator.Query(receipt.RunId)!;
            var traces = await provider.GetRequiredService<ITraceQuery>().GetWritesAsync(receipt.RunId, timeout.Token);
            var evidence = EvidencePath("legacy-test-workflow.json");
            File.WriteAllText(evidence, JsonSerializer.Serialize(new { scope = "OFFLINE:legacy-Test-layout;formal-Host-chain;no-site-admission", state, traces, deviceLogs=plc.DeviceLogs.ToArray() }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
            Assert.True(state.State == RunState.AwaitingManualRemoval, $"state={state.State}; {state.ErrorCode}; evidence={evidence}");
            Assert.Contains(traces, w => w.Kind == WriteKind.HandoffV2 && w.State == CommitState.Committed);
            Assert.Contains(traces, w => w.Kind == WriteKind.ActionIntent && w.PayloadJson.Contains("RecipePlanAndBindingIntent"));
            await using var db = new Station01DbContext(provider.GetRequiredService<DbContextOptions<Station01DbContext>>());
            Assert.Equal(special?19:png?12:7, await db.Media.CountAsync(m => m.RunId == receipt.RunId, timeout.Token));
            var stageRows=await db.StageEvents.Where(e => e.RunId == receipt.RunId).OrderBy(e=>e.Sequence).ToListAsync(timeout.Token);
            if(special)
            {
                var cycles=stageRows.Where(e=>e.PayloadJson.Contains("UnitCycleCompleted")).ToArray();
                Assert.Equal(2,cycles.Length);
                var loads=stageRows.Where(e=>e.PayloadJson.Contains("\"stage\":\"TransferToRotation\"") && e.EventType==StageEventType.IntentRecorded.ToString()).ToArray();
                Assert.Equal(2,loads.Length);
                Assert.True(cycles[0].Sequence<loads[1].Sequence,"First physical unit must be completely returned/sorted before loading the next unit");
            }
            else Assert.Contains(stageRows, e => e.PayloadJson.Contains("Flip") || e.EventType.Contains("Flip"));
            Assert.NotNull(state.WholeTrayCompletionId);
            if(probe=="None" && !png && !special)
            {
                var store=provider.GetRequiredService<StageEventStore>();
                var resource=RealAlgorithmPipelineLifecycleTests.State(receipt.RunId,tray,Guid.NewGuid()) with {BusinessEnded=true,Dispatch="Unknown"};
                await store.AppendResourceAsync(resource,timeout.Token);
                using var blocked=await client.PostAsJsonAsync($"/api/v1/station01/runs/{receipt.RunId:D}/manual-removal-confirmations",
                    new{requestId="OFFLINE-blocked-final",expectedRevision=coordinator.Query(receipt.RunId)!.ObservedRevision,reason="Test:unknown-algorithm"},timeout.Token);
                Assert.Equal(HttpStatusCode.Conflict,blocked.StatusCode);
                Assert.Contains("AlgorithmResourcesUnconfirmed",await blocked.Content.ReadAsStringAsync(timeout.Token));
                Assert.NotEqual(RunState.Completed,coordinator.Query(receipt.RunId)!.State);
                Assert.DoesNotContain(await store.ReadAsync(receipt.RunId,tray,WholeTrayWorkflowStage.ManualTrayRemovalConfirmation,timeout.Token),x=>x.EventType==StageEventType.FinalUnloadCompleted);
                await store.AppendResourceAsync(resource with{Revision=2,InputsReleased=true,ExecutionEnded=true,DispatchReturned=true},timeout.Token);
                state=coordinator.Query(receipt.RunId)!;
            }
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
            File.WriteAllText(EvidencePath("manual-final-completion.json"), JsonSerializer.Serialize(new {
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
            var simulatedCapture = provider.GetRequiredService<ICapturePort>();
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
            if(versionUpdate)
            {
                Assert.NotNull(nextRecipe);
                secondContext["expectedRecipeRef"]=JsonSerializer.SerializeToNode(new {nextRecipe.RecipeId,nextRecipe.Version,nextRecipe.CatalogDigest},new JsonSerializerOptions(JsonSerializerDefaults.Web));
            }
            var second = starts.Start("OFFLINE", new("OFFLINE-normal-chain-second", secondContext.ToJsonString(),
                options.PublicReference, options.BudgetReference, options.SimulationReference));
            Assert.NotEqual(receipt.RunId, second.RunId);
            await CommissioningProtocolTcpFixture.UntilAsync(() => coordinator.Query(second.RunId)?.State is
                RunState.AwaitingManualRemoval or RunState.Blocked or RunState.RecoveryRequired or RunState.ConfigurationBlocked, timeout.Token);
            var secondState = coordinator.Query(second.RunId)!;
            Assert.True(secondState.State == RunState.AwaitingManualRemoval,
                $"second state={secondState.State}; error={secondState.ErrorCode}");
            Assert.Equal(special?19:png?12:7, await db.Media.CountAsync(m => m.RunId == second.RunId, timeout.Token));
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
            if(versionUpdate)
            {
                var firstWrites=await new TraceQuery(dbOptions).GetWritesAsync(receipt.RunId,timeout.Token);
                var secondWrites=await new TraceQuery(dbOptions).GetWritesAsync(second.RunId,timeout.Token);
                foreach(var (writes,parameterVersion,gain) in new[]{(firstWrites,"test-1",1d),(secondWrites,"test-2",2d)})
                {
                    var calls=writes.Where(write=>write.Kind==WriteKind.AlgorithmIntent).Select(write=>JsonSerializer.Deserialize<AlgorithmIntentPayload>(write.PayloadJson,new JsonSerializerOptions(JsonSerializerDefaults.Web))!).Where(call=>call.CapabilityId is "test-single" or "test-fusion").ToArray();
                    Assert.Equal(6,calls.Length);Assert.All(calls,call=>Assert.Equal(parameterVersion,call.ParametersVersion));
                    var captures=writes.Where(write=>write.Kind==WriteKind.CaptureFact).Select(write=>JsonNode.Parse(write.PayloadJson)!).Where(node=>node["kind"]?.GetValue<string>()=="ConfiguredCaptureCompleted").ToArray();
                    Assert.Equal(4,captures.Length);Assert.All(captures,node=>Assert.Equal(gain,node["requestedCaptureSettings"]!["Gain"]!.GetValue<double>()));
                    Assert.Contains(writes,write=>write.Kind==WriteKind.ActionIntent && write.PayloadJson.Contains(parameterVersion=="test-1"?recipe.Version:nextRecipe!.Version));
                }
                File.WriteAllText(EvidencePath("frozen-version-two-rounds.json"),JsonSerializer.Serialize(new {scope="Test:durable-approved-fixture-catalog;actual-Host-freeze-capture-call-SQLite;not-production-authoring-or-model-load",oldRecipe=recipe,newRecipe=nextRecipe,firstWrites,secondWrites,barrierEvidence=barrierEvidence.ToArray()},new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true}));
            }
            Assert.Equal(RunState.Completed, (await new TraceQuery(dbOptions).GetRunAsync(receipt.RunId, timeout.Token))!.State);
            File.WriteAllText(EvidencePath("two-round-final-completion.json"),
                JsonSerializer.Serialize(new { scope = "OFFLINE:legacy-Test-layout;formal-host-two-rounds;no-site-authority;not-desktop-DOM",
                    firstRun = receipt.RunId, secondRun = second.RunId, firstTray = tray, secondTray,
                    secondPersisted, secondManual, secondAdmission = secondAdmission.RootElement.Clone(),
                    mediaPerRun = special?19:png?12:7, specialRotation=special, registryManuallyReleased = false, hostRebuiltBetweenRuns = false,
                    sameRunEventReplay = "Replay", sameRunChangedPayload = "Conflict", sameRunFRepeat = "Rejected" },
                    new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
            if(Environment.GetEnvironmentVariable("GAODE_022_EVIDENCE_ROOT") is {Length:>0} evidenceRoot)
            {
                var destination=Path.Combine(evidenceRoot,"host-"+(special?"special-":png?"png-":"native-")+receipt.RunId.ToString("N"));Directory.CreateDirectory(destination);
                foreach(var file in Directory.GetFiles(options.TestRoot,"*",SearchOption.AllDirectories).Where(file=>!file.EndsWith(".lock",StringComparison.Ordinal)))
                {var copy=Path.Combine(destination,Path.GetRelativePath(options.TestRoot,file));Directory.CreateDirectory(Path.GetDirectoryName(copy)!);File.Copy(file,copy);}
            }
        }
        finally { publicRelease.TrySetResult(); blockedRegistration.Resume.TrySetResult(); await starts.StopAsync(CancellationToken.None); await coordinator.StopConsumerAsync(CancellationToken.None); await app.StopAsync(CancellationToken.None); }
    }
    private sealed class PngTestCapture : ICapturePort
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid,byte> f = new();
        private readonly System.Collections.Concurrent.ConcurrentDictionary<CaptureRole,int> counts = new();
        public long ConnectionEpoch=>1;
        public int TriggerCount(CaptureRole role)=>counts.GetValueOrDefault(role);
        public ValueTask RequestCaptureAsync(CaptureRequest r, Action<CaptureEvent> events, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); Assert.Equal("Test",r.Envelope.Purpose);
            if(r.Role==CaptureRole.F && !f.TryAdd(r.Envelope.RunId,0)) throw new InvalidOperationException("Test:duplicate-F");
            counts.AddOrUpdate(r.Role,1,(_,old)=>old+1);
            var at=DateTimeOffset.UtcNow;var encoded=r.Role!=CaptureRole.ThreeD;
            var metadata=encoded ? new CaptureFrameMetadata("Test:declared-Mono8",r.CameraBindingId,"Test","","","",r.Envelope.SessionId,
                1,0,1,at,at,2,2,"Mono8",4,new Dictionary<string,string>{["Width"]="2",["Height"]="2",["PayloadSize"]="4",["PixelFormat"]="Mono8"},
                [new("frame.raw",4,1,4,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(new byte[]{1,2,3,4})))]) : null;
            var origin=new ComponentExecutionOrigin(ComponentEvidenceSource.Test,"Test:declared-PNG-capture","1");
            var fact=new CorrelatedCaptureFact(r.Envelope.RunId,r.CaptureId,r.Envelope.OperationId,1,
                AcquisitionContract.RequestedSettingsDigest(r),"Test",origin,origin,CaptureApplicationState.ConfiguredOnly,r.DetectionSettings,false,["Test:software-only"])
                {FrameMetadata=metadata,ActualPublicSettings=r.PublicSettings,CameraApplicationState=CaptureApplicationState.Applied,
                 LightExecution=r.LightExecution,LightApplicationState=CaptureApplicationState.ConfiguredOnly,
                 ActualCameraSettings=new(r.DetectionSettings?.ExposureUs ?? r.PublicSettings?.ExposureUs ?? 1,r.DetectionSettings?.Gain ?? 0,1,1,0,0)};
            events(new(r,CaptureEventKind.Ended,1));events(new(r,CaptureEventKind.MediaTaken,1,[1,2,3,4],encoded?"GalaxyRaw":"bin"){Fact=fact});
            return ValueTask.CompletedTask;
        }
    }
    private sealed class Catalog : IRecipeCatalog
    {
        private readonly string path;
        public Catalog(RecipeDefinition recipe,string path){this.path=path;Save(recipe);}
        public void Save(RecipeDefinition recipe)=>File.WriteAllText(path,RecipeDefinitionSerialization.Serialize(recipe));
        public RecipeCatalogSnapshot GetSnapshot(){var saved=RecipeDefinitionSerialization.Deserialize(File.ReadAllText(path));return new(RecipeCatalogSnapshot.CurrentSchema,saved.CatalogDigest,[saved]);}
    }
    private sealed class WorkflowFileLogger(string path) : ILogger, IDisposable
    {
        private readonly StreamWriter writer = new(path, append: true) { AutoFlush = true };
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> format)
        { lock (writer) writer.WriteLine(level + " " + format(state, error) + (error is null ? "" : " exception=" + error)); }
        public void Dispose() { lock (writer) writer.Dispose(); }
    }
    private sealed class FileProvider(WorkflowFileLogger logger) : Microsoft.Extensions.Logging.ILoggerProvider
    {
        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) => logger;
        public void Dispose() { }
    }
    private sealed class PausedResourceRegistration : IAlgorithmResourceStore
    {
        public IAlgorithmResourceStore Inner=null!;
        public TaskCompletionSource<AlgorithmResourceState> Entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task AppendResourceAsync(AlgorithmResourceState state,CancellationToken token)
        {
            await Inner.AppendResourceAsync(state,token);
            if(state.Revision==1){Entered.TrySetResult(state);await Resume.Task;}
        }
        public Task<IReadOnlyList<AlgorithmResourceState>> GetUnreclaimedResourcesAsync(int offset,int limit,CancellationToken token)=>Inner.GetUnreclaimedResourcesAsync(offset,limit,token);
    }
    internal sealed class FileReadingTestAlgorithm(MediaStore media, string code,bool png=false,int slots=1,Action? saveNewVersion=null,Func<AlgorithmRequest,Task>? beforeResult=null,Func<AlgorithmRequest,Task>? afterResult=null) : IAlgorithmPort, IAlgorithmCapabilityProvider
    {
        public AlgorithmInputRepresentation InputRepresentation(AlgorithmRole role) => png && role!=AlgorithmRole.TrayPose ? AlgorithmInputRepresentation.Png : AlgorithmInputRepresentation.NativeMedia;
        public ComponentExecutionOrigin Origin => new(ComponentEvidenceSource.Test, "OFFLINE-file-reading-algorithm/1", "DeclaredTestFixture");
        public string ImplementationReference => "OFFLINE:declared-file-reading-fixture";
        public IReadOnlyList<AlgorithmCapabilityDeclaration> AlgorithmCapabilities { get; } = [
            new(AlgorithmPurpose.SingleDetection, "test-single", "1", "test-defect/1", 1, "test-1"),
            new(AlgorithmPurpose.FaceFusion, "test-fusion", "1", "test-fusion/1", 2, "test-1"),
            new(AlgorithmPurpose.TrayPose, "tray.observation", "1.0", "tray-observation/2", 1, "1") ];
        public List<AlgorithmRequest> Requests {get;}=[];
        private int updateApplied;
        public int CallCount(AlgorithmRole role) => 0;
        public async ValueTask<AlgorithmDispatch> RequestAsync(AlgorithmRequest r, Action<AlgorithmEvent> onEvent, CancellationToken ct)
        {
            Requests.Add(r);
            if(r.Role==AlgorithmRole.Detection && saveNewVersion is not null && Interlocked.Exchange(ref updateApplied,1)==0) saveNewVersion();
            if(png && r.Role!=AlgorithmRole.TrayPose) Assert.All(r.Inputs,input=>{Assert.Equal("png",input.Format);Assert.NotNull(input.AlgorithmInput);});
            foreach (var input in r.Inputs) { using var lease = media.Lease(input.MediaId, "OFFLINE-fixture"); await using var file = await media.OpenReadAsync(input.MediaId, ct); Assert.True(file.Length > 0); }
            var result = new AlgorithmEvent(r, AlgorithmEventKind.Result, RawCodes: r.Role == AlgorithmRole.FDecode ? [code] : null, WorkerSessionId: Guid.NewGuid(), DetectionDisposition: r.Role == AlgorithmRole.Detection ? "OK" : null);
            if (r.Role == AlgorithmRole.TrayPose)
            {
                var c = r.ObservationContext!;
                result = result with { Observation = new(Guid.NewGuid(), r.Envelope.RunId, c.TrayId, r.CaptureId, r.CallId, DateTimeOffset.UtcNow, c.Purpose, c.CheckRound, c.RelatedTransitionId,
                    Enumerable.Range(1,slots).Select(index=>new TraySlotObservation(index, TrayPresence.Present, TrayPose.Normal) {CellId=$"r1:c{index+3}",Region="OK",Row=1,Column=index+3}).ToArray(),
                    c.Purpose == TrayObservationPurpose.InitialPreparation ? new(10, 20, "mm", "SIM_MACHINE", "OFFLINE:Test-only") : null, Origin, ["OFFLINE:declared-test-input"]) {
                        SchemaVersion = "tray-observation/2", MappingSourceReference = "OFFLINE:Test-only-map", ExpectedPhysicalSlotIndices = Enumerable.Range(1,slots).ToArray() } };
            }
            if(beforeResult is not null) await beforeResult(r);
            onEvent(result); return new(afterResult?.Invoke(r) ?? Task.CompletedTask);
        }
    }
}
