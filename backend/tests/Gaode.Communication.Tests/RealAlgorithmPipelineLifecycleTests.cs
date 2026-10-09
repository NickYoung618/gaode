using Gaode.Application.Algorithms;
using Gaode.Application.Ports;
using Gaode.Application.Workflow;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gaode.Host.Composition;
using Gaode.Host.Lifecycle;
using Gaode.Domain.Configuration;
using Gaode.Application.Station01;
using Xunit;

namespace Gaode.Communication.Tests;

// Isolated SQLite only; no device Host or real algorithm activation.
public sealed class RealAlgorithmPipelineLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualHostInitializationAndStoppingDiscoverTerminalUnknownWithoutReplaying(bool activeExpired)
    {
        using var inputs = new ControlledCommissioningTests.Inputs();
        await inputs.PrepareStore("Test");
        var configRoot = inputs.Options.ConfigRoot;
        var p = JsonNode.Parse(File.ReadAllText(Path.Combine(configRoot, "public.json")))!;
        p["purpose"] = "Test";
        foreach (var b in p["bindings"]!.AsArray()) b!["provider"] = "Simulated";
        File.WriteAllText(Path.Combine(configRoot, "public.json"), p.ToJsonString());
        var budget = JsonNode.Parse(File.ReadAllText(Path.Combine(configRoot, "budget.json")))!;
        budget["purpose"] = "Test"; budget.AsObject().Remove("recipeExecution");
        File.WriteAllText(Path.Combine(configRoot, "budget.json"), budget.ToJsonString());
        var simulation = ReviewBusinessData.Read<SimulationProfile>("simulation.normal.json");
        File.WriteAllText(Path.Combine(configRoot, "simulation.json"), JsonSerializer.Serialize(simulation, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var options = inputs.Options with { Mode = "FullSimulation", PlcProvider = "Virtual", Cameras = null,
            CommissioningPath = null, CommissioningSha256 = null, PlcMechanicsPath = null, PlcFieldProfilePath = null,
            SimulationReference = new(simulation.Id, simulation.Version) };
        var dbOptions = new DbContextOptionsBuilder<Station01DbContext>().UseSqlite(
            $"Data Source={Path.Combine(options.TestRoot, "station01.test.db")};Pooling=False").Options;
        var run = Guid.NewGuid();
        await using (var db = new Station01DbContext(dbOptions))
        {
            db.Runs.Add(new() { RunId = run, RequestId = run.ToString(), SubjectId = "Test", ContextJson = "{\"purpose\":\"Test\"}",
                State = RunState.Cancelled, Terminal = TerminalOutcome.Cancelled, TerminalRevision = 1, Revision = 1, CreatedUtc = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        var start = DateTimeOffset.UtcNow.AddMinutes(-1);
        var state = State(run, Guid.NewGuid(), Guid.NewGuid()) with { Dispatch = "Unknown", BusinessEnded = true,
            ReleaseStartUtc = start, ReleaseDueUtc = start.AddMilliseconds(1000), ReleaseStartTick = 1,
            ReleaseDueTick = 1001, ReleaseTrigger = "TimedOut", TechnicalTerminal = "TimedOut" };
        await new StageEventStore(dbOptions).AppendResourceAsync(state, default);
        for (var restart = 0; restart < 2; restart++)
        {
            var services = new ServiceCollection(); services.AddLogging(); services.AddStation01(options);
            services.AddSingleton<Gaode.Application.Recipes.IRecipeCatalog>(new EmptyCatalog());
            var lifetime = new Lifetime(); services.AddSingleton<IHostApplicationLifetime>(lifetime);
            services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromSeconds(5));
            var heldPort=new RealAlgorithmManagedCallTests.HeldPort();
            if(activeExpired) services.AddSingleton<IAlgorithmPort>(heldPort);
            await using var provider = services.BuildServiceProvider();
            var host = provider.GetRequiredService<Station01HostedService>();
            await host.InitializePersistenceAsync(default); // actual entry, no device hosted services start
            var supervisor = provider.GetRequiredService<AlgorithmResourceSupervisor>();
            Assert.Equal(state.CallId, Assert.Single(supervisor.States).CallId);
            Assert.Equal(0, provider.GetRequiredService<Station01Coordinator>().ActiveRunCount);
            Assert.Equal(0, provider.GetRequiredService<AlgorithmRuntime>().ActiveExecutions);
            var rejected=Assert.Throws<InvalidOperationException>(()=>provider.GetRequiredService<StartPublicPreparation>().Start("Test",
                new("Test:new-run-while-unknown-"+restart,"{}",options.PublicReference,options.BudgetReference,options.SimulationReference)));
            Assert.Equal("AlgorithmResourcesUnconfirmed",rejected.Message);
            Assert.Equal(0,provider.GetRequiredService<Station01Coordinator>().ActiveRunCount);
            Gaode.Application.Algorithms.ManagedAlgorithmCall? live=null;
            var held=heldPort;
            if(activeExpired && restart==0)
            {
                var media=provider.GetRequiredService<Gaode.Infrastructure.Media.MediaStore>();
                var liveRun=Guid.NewGuid();
                await using(var setup=new Station01DbContext(dbOptions))
                {setup.Runs.Add(new(){RunId=liveRun,RequestId=liveRun.ToString(),SubjectId="Test",ContextJson="{\"purpose\":\"Test\"}",Revision=1,CreatedUtc=DateTimeOffset.UtcNow});await setup.SaveChangesAsync();}
                var capture=Guid.NewGuid();MediaRef image;
                using(media.ReserveCapture(capture,"Detection",4))image=(await media.SaveAsync(liveRun,capture,"Detection","1","1",[1,2,3,4],"bin","Test",default)) with {Purpose="Test"};
                await using(var db=new Station01DbContext(dbOptions))
                {
                    var revision=(await db.Runs.SingleAsync(x=>x.RunId==liveRun)).Revision;
                    var json=JsonSerializer.Serialize(image,new JsonSerializerOptions(JsonSerializerDefaults.Web));
                    var receipt=await provider.GetRequiredService<TraceWriter>().SubmitCritical(new(Guid.NewGuid(),liveRun,revision,WriteKind.Media,json,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json))))).Completion;
                    Assert.Equal(CommitState.Committed,receipt.State);
                }
                await media.MarkCommittedAsync(image,default);
                var request=RealAlgorithmManagedCallTests.Request(liveRun,[image],AlgorithmRole.Detection,"Test-snapshot","1",false);
                live=await provider.GetRequiredService<AlgorithmRuntime>().DispatchSynchronousAsync(request,state.TrayId,100,30,3000,_=>{},default);
                await Assert.ThrowsAsync<TimeoutException>(()=>live.Result);
                await Task.Delay(50);await supervisor.FlushAsync(default);
                Assert.True(supervisor.States.Single(x=>x.CallId==request.CallId).ObservationExpired);
                await using(var ended=new Station01DbContext(dbOptions))
                {var failed=await ended.Runs.SingleAsync(x=>x.RunId==liveRun);failed.State=RunState.Cancelled;failed.Terminal=TerminalOutcome.Cancelled;failed.TerminalRevision=failed.Revision;await ended.SaveChangesAsync();}
            }
            lifetime.StopApplication(); // actual registered ApplicationStopping consumer
            var hostDue = host.ShutdownDueUtc;
            await host.StopAsync(default).WaitAsync(TimeSpan.FromSeconds(activeExpired ? 1 : 5));
            Assert.Equal(hostDue, host.ShutdownDueUtc);
            Assert.False(host.LastShutdown!.ResourcesDrained);
            Assert.True(host.LastShutdown.ConsumerStopped);
            var all=await provider.GetRequiredService<ITraceQuery>().GetUnreclaimedResourcesAsync(0,128,default);
            var persisted = Assert.Single(all,x=>x.CallId==state.CallId);
            var evidence=Environment.GetEnvironmentVariable("GAODE_COMMISSIONING_EVIDENCE_ROOT");
            if(evidence is not null)
            {
                Directory.CreateDirectory(evidence);
                File.WriteAllText(Path.Combine(evidence,$"host-resources-{activeExpired}-{restart}.json"),JsonSerializer.Serialize(new{
                    scope="Test:actual-InitializePersistence-ApplicationStopping-StopAsync-SQLite;no-hardware",run,state,all,host.LastShutdown,host.ShutdownStartedUtc,host.ShutdownDueUtc
                },new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true}));
            }
            if(live is not null)
            {
                var original=Assert.Single(all,x=>x.CallId!=state.CallId);
                Assert.True(original.ObservationExpired);Assert.False(original.Reclaimed);
                Assert.Equal(1,host.LastShutdown.RemainingAlgorithmExecutions);
                held.Result();Assert.True(live.Result.IsFaulted);
                await live.DisposeAsync();held.Exit.TrySetResult();await provider.GetRequiredService<AlgorithmRuntime>().WaitForIdleAsync(new CancellationTokenSource(3000).Token);
                var finalRows=await provider.GetRequiredService<IStageEventStore>().ReadAsync(original.RunId,state.TrayId,WholeTrayWorkflowStage.Detection);
                var reclaimed=finalRows.Where(x=>x.EventType==StageEventType.AlgorithmLifecycleRecorded).Select(x=>JsonSerializer.Deserialize<AlgorithmResourceState>(x.PayloadJson,new JsonSerializerOptions(JsonSerializerDefaults.Web))!).Last(x=>x.CallId==original.CallId);
                Assert.True(reclaimed.Reclaimed);Assert.Equal(original.ReleaseStartUtc,reclaimed.ReleaseStartUtc);Assert.Equal(original.ReleaseDueUtc,reclaimed.ReleaseDueUtc);
            }
            Assert.Equal(state.ReleaseStartUtc, persisted.ReleaseStartUtc);
            Assert.Equal(state.ReleaseDueUtc, persisted.ReleaseDueUtc);
            Assert.False(persisted.Reclaimed);
            Assert.True(persisted.ObservationExpired);
        }
    }

    [Fact]
    public async Task ParallelShortCommitsAndLostReceiptReconcileTheOriginalWriteId()
    {
        using var fixture=new Store();var runId=Guid.NewGuid();var tray=Guid.NewGuid();
        await using(var db=new Station01DbContext(fixture.Options))
        {db.Runs.Add(new(){RunId=runId,RequestId=runId.ToString(),SubjectId="Test",ContextJson="{\"purpose\":\"Test\"}",Revision=1,CreatedUtc=DateTimeOffset.UtcNow});await db.SaveChangesAsync();}
        var store=new StageEventStore(fixture.Options);
        await using(var writer=new TraceWriter(fixture.Options,TimeProvider.System,8))
        {
            var config=new Gaode.Application.Configuration.FrozenConfiguration(ReviewBusinessData.Motion(),ReviewBusinessData.Budget(),null,
                "{}","{}","","Test","Test","",new Dictionary<string,string>(),"Test-frozen");
            var run=new RunExecution(runId,Guid.NewGuid(),runId.ToString(),"Test","{\"purpose\":\"Test\"}",config,writer,TimeProvider.System,Guid.NewGuid(),"Test");
            run.AdoptInitialRevision(1);
            var first=run.SaveAsync(WriteKind.Audit,new {kind="Test:first-short-save"});
            var second=run.SaveAsync(WriteKind.Audit,new {kind="Test:second-short-save"});
            await Task.WhenAll(first,second,store.AppendResourceAsync(State(runId,tray,Guid.NewGuid()),default));
            Assert.Equal(new long?[]{2,3},new[]{(await first).CommittedRevision,(await second).CommittedRevision});
        }
        var writeId=Guid.NewGuid();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var publish=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using(var writer=new TraceWriter(fixture.Options,TimeProvider.System,8,afterCommitBeforeReceipt:async (batch,receipt,token)=>
        {if(batch.WriteId==writeId){entered.TrySetResult();await publish.Task.WaitAsync(token);}}))
        {
            var json="{\"kind\":\"Test:lost-receipt\"}";
            var queued=writer.SubmitCritical(new(writeId,runId,3,WriteKind.Audit,json,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json)))));
            try
            {
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
                await Assert.ThrowsAsync<TimeoutException>(()=>queued.Completion.WaitAsync(TimeSpan.Zero));
                var reconciled=await writer.ReconcileAsync(writeId,default);
                Assert.NotNull(reconciled);Assert.Equal(writeId,reconciled.WriteId);Assert.Equal(CommitState.Committed,reconciled.State);Assert.Equal(4,reconciled.CommittedRevision);
                Assert.Single(await new TraceQuery(fixture.Options).GetWritesAsync(runId,default),x=>x.WriteId==writeId);
            }
            finally {publish.TrySetResult();}
            Assert.Equal(writeId,(await queued.Completion).WriteId);
        }
        Assert.Single(await store.GetUnreclaimedResourcesAsync(0,128,default));
    }

    private sealed class Lifetime : IHostApplicationLifetime
    {
        private readonly CancellationTokenSource stopping = new();
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => stopping.Token;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() => stopping.Cancel();
    }
    private sealed class EmptyCatalog : Gaode.Application.Recipes.IRecipeCatalog
    {
        public Gaode.Application.Recipes.RecipeCatalogSnapshot GetSnapshot() => new(
            Gaode.Application.Recipes.RecipeCatalogSnapshot.CurrentSchema, "Test:no-replay", []);
    }
    [Fact]
    public async Task ReleaseAfterTerminalPreservesBusinessIdentityAcrossReadReplayAndRecovery()
    {
        using var fixture = new Store();
        var events = new StageEventStore(fixture.Options);
        var run = Guid.NewGuid(); var tray = Guid.NewGuid(); var mechanical = Guid.NewGuid();
        await fixture.TerminalRun(run);
        var request = new StageEventAppendRequest(Guid.NewGuid(), run, tray, "Station01", "Station01",
            WholeTrayWorkflowStage.Detection, mechanical, 1, 27, StageEventType.Completed,
            DateTimeOffset.UtcNow, ResultSource.HostDerived, ResultQuality.Derived, null, "digest", "{}", "terminal-" + run);
        var before = (await events.AppendAsync(request)).Projection;
        var resource = State(run, tray, mechanical);
        await events.AppendResourceAsync(resource, default);
        var released = resource with { Revision = 2, BusinessEnded = true, InputsReleased = true,
            ExecutionEnded = true, DispatchReturned = true };
        await events.AppendResourceAsync(released, default);
        await events.AppendResourceAsync(released, default); // idempotent resource replay
        var reloaded = await new StageEventStore(fixture.Options).GetProjectionAsync(run, tray, WholeTrayWorkflowStage.Detection);
        var replayed = (await events.AppendAsync(request)).Projection;
        var recovered = await events.RecoverAsync(run, tray, WholeTrayWorkflowStage.Detection);
        foreach (var actual in new[] { reloaded!, replayed, recovered })
        {
            Assert.Equal(before.Status, actual.Status);
            Assert.Equal(before.CurrentOperationId, actual.CurrentOperationId);
            Assert.Equal(before.ConnectionEpoch, actual.ConnectionEpoch);
            Assert.Equal(before.LastEventId, actual.LastEventId);
            Assert.Equal(before.UpdatedAt, actual.UpdatedAt);
        }
        Assert.Empty(await events.GetUnreclaimedResourcesAsync(0, 128, default));
        Assert.Equal(3, (await events.ReadAsync(run, tray, WholeTrayWorkflowStage.Detection)).Count);
    }

    [Fact]
    public async Task CallsSharingOperationRemainIndependentAndOriginalReleaseDeadlineSurvivesRestart()
    {
        using var fixture = new Store();
        var events = new StageEventStore(fixture.Options);
        var run = Guid.NewGuid(); var tray = Guid.NewGuid(); var operation = Guid.NewGuid();
        await fixture.TerminalRun(run);
        var start = DateTimeOffset.UtcNow.AddMinutes(-1);
        var unknown = State(run, tray, operation) with { Dispatch = "Unknown", BusinessEnded = true,
            ReleaseStartUtc = start, ReleaseDueUtc = start.AddMilliseconds(1000),
            ReleaseStartTick = 10, ReleaseDueTick = 1010, ReleaseTrigger = "TimedOut", TechnicalTerminal = "TimedOut" };
        var other = State(run, tray, operation);
        await events.AppendResourceAsync(unknown, default);
        await events.AppendResourceAsync(other, default);
        await events.AppendResourceAsync(other with { Revision = 2, BusinessEnded = true, InputsReleased = true,
            ExecutionEnded = true, DispatchReturned = true }, default);
        var another=State(run,tray,operation);await events.AppendResourceAsync(another,default);
        var firstPage=Assert.Single(await events.GetUnreclaimedResourcesAsync(0,1,default));
        var secondPage=Assert.Single(await events.GetUnreclaimedResourcesAsync(1,1,default));
        var expectedCalls=new[]{unknown.CallId,another.CallId}.OrderBy(id=>id.ToString("D"),StringComparer.Ordinal).ToArray();
        Assert.Equal(expectedCalls,new[]{firstPage.CallId,secondPage.CallId});
        Assert.Empty(await events.GetUnreclaimedResourcesAsync(2,1,default));
        await events.AppendResourceAsync(another with {Revision=2,BusinessEnded=true,InputsReleased=true,ExecutionEnded=true,DispatchReturned=true},default);
        var restored = Assert.Single(await events.GetUnreclaimedResourcesAsync(0, 1, default));
        Assert.Equal(unknown.CallId, restored.CallId);
        var supervisor = new AlgorithmResourceSupervisor(events, TimeProvider.System, ReviewBusinessData.Budget().BusinessMs.CriticalSave);
        supervisor.Restore(restored);
        supervisor.BeginShutdown(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(1));
        supervisor.StartObservation(restored.CallId, "Cancelled");
        supervisor.Terminal(restored.CallId, "HostClosing");
        await supervisor.FlushAsync(default);
        var persisted = Assert.Single(await new StageEventStore(fixture.Options).GetUnreclaimedResourcesAsync(0, 128, default));
        Assert.Equal(start, persisted.ReleaseStartUtc);
        Assert.Equal(unknown.ReleaseDueUtc, persisted.ReleaseDueUtc);
        Assert.Equal("TimedOut", persisted.TechnicalTerminal);
        Assert.True(persisted.ObservationExpired);
        Assert.False(persisted.Reclaimed);
        Assert.True(supervisor.HasUnreclaimedResources);
        await using var db = new Station01DbContext(fixture.Options);
        Assert.Empty(await db.StageProjections.ToArrayAsync()); // resource facts invent no business projection
    }

    internal static AlgorithmResourceState State(Guid run, Guid tray, Guid operation) => new(run, tray,
        Guid.NewGuid(), operation, "test-clock", "test-frozen-snapshot",
        [new(Guid.NewGuid(), run, Guid.NewGuid(), "Detection", "test/input.png", 1, "png", "Test", "1", "1", "FileCompleted")], 1000);

    private sealed class Store : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "gaode-022-lifecycle-" + Guid.NewGuid().ToString("N"));
        public DbContextOptions<Station01DbContext> Options { get; }
        public Store() { Options = CameraCaptureJournal.Prepare(root); }
        public async Task TerminalRun(Guid run)
        {
            await using var db = new Station01DbContext(Options);
            db.Runs.Add(new() { RunId = run, RequestId = run.ToString(), SubjectId = "Test",
                ContextJson = "{\"purpose\":\"Test\"}", State = RunState.Cancelled,
                Terminal = TerminalOutcome.Cancelled, TerminalRevision = 1, Revision = 1, CreatedUtc = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        public void Dispose() { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(root, true); }
    }
}
