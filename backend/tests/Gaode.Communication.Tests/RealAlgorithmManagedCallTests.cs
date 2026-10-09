using Gaode.Communication.Tests.Devices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Algorithms;
using Gaode.Application.Ports;
using Gaode.Application.Timing;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Media;
using Gaode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Gaode.Communication.Tests;

public sealed class RealAlgorithmManagedCallTests
{
    [Fact]
    public async Task SynchronousRolesAndTwoRunsPersistExactInputsAndIgnoreOldResults()
    {
        await using var fixture = new Fixture();
        var port = new DeclaredPort(); var runtime = fixture.Runtime(port);
        AlgorithmRequest? old = null;
        for(var round=1;round<=2;round++)
        {
            var run=Guid.NewGuid(); var tray=Guid.NewGuid();
            var images=await fixture.Images(run);
            foreach(var role in new[]{AlgorithmRole.TrayPose,AlgorithmRole.FDecode,AlgorithmRole.EDecode,AlgorithmRole.Detection,AlgorithmRole.Detection})
            {
                var fusion=role==AlgorithmRole.Detection && port.ForRun(run,role)==1;
                var used=fusion?images:images.Take(1).ToArray();
                var request=Request(run,used,role,"snapshot-"+round,"parameters-"+round,fusion);
                port.Stale=old;
                var accepted=new List<AlgorithmEvent>();
                await using(var call=await runtime.DispatchSynchronousAsync(request,tray,1000,1000,3000,accepted.Add,default))
                { await call.Exited.WaitAsync(TimeSpan.FromSeconds(3)); Assert.Single(accepted,x=>x.Kind==AlgorithmEventKind.Result); }
                old=request;
                await fixture.Supervisor.FlushAsync(default);
                Assert.Equal(0,runtime.ActiveExecutions); Assert.Equal(0,fixture.Media.ActiveLeases);
                Assert.Empty(await fixture.Store.GetUnreclaimedResourcesAsync(0,128,default));
            }
        }
        await using var db=new Station01DbContext(fixture.Options);
        var facts=await db.StageEvents.Where(x=>x.EventType==nameof(Gaode.Application.Workflow.StageEventType.AlgorithmLifecycleRecorded)).ToArrayAsync();
        var final=facts.Select(x=>JsonSerializer.Deserialize<AlgorithmResourceState>(x.PayloadJson,new JsonSerializerOptions(JsonSerializerDefaults.Web))!)
            .GroupBy(x=>x.CallId).Select(x=>x.MaxBy(s=>s.Revision)!).ToArray();
        Assert.Equal(10,final.Length); Assert.All(final,x=>Assert.True(x.Reclaimed));
        Assert.Equal(2,final.Count(x=>x.Inputs.Count==2));
        foreach(var state in final)
        {
            Assert.NotNull(state.Request);Assert.Equal(state.RunId,state.Request!.Envelope.RunId);
            Assert.Equal(state.SnapshotId,state.Request.Envelope.SnapshotId);
            Assert.Equal(state.Inputs.Select(x=>x.MediaId),state.Request.Inputs.Select(x=>x.MediaId));
            Assert.Equal("model-"+state.SnapshotId,state.Request.FrozenModule!.ModelVersion);
            Assert.Equal("object-1",state.Request.TargetIdentity!.ObjectId);
            Assert.Equal(1,state.Request.TargetIdentity.LocalFace);
        }
    }

    [Fact]
    public async Task TechnicalDispatchFailureRemainsUnknownUntilCorrelatedLateRelease()
    {
        await using var fixture=new Fixture(); var port=new DeclaredPort { Fail=true };var runtime=fixture.Runtime(port);
        var run=Guid.NewGuid();var images=await fixture.Images(run);
        var request=Request(run,images.Take(1).ToArray(),AlgorithmRole.Detection,"frozen","1",false);
        await Assert.ThrowsAsync<IOException>(()=>runtime.DispatchSynchronousAsync(request,Guid.NewGuid(),1000,1000,3000,_=>{},default));
        var unknown=Assert.Single(await fixture.Store.GetUnreclaimedResourcesAsync(0,128,default));
        Assert.Equal("Unknown",unknown.Dispatch);Assert.True(unknown.BusinessEnded);Assert.False(unknown.Reclaimed);
        Assert.Equal(1,runtime.ActiveExecutions); Assert.Equal(1,fixture.Media.ActiveLeases);
        var due=unknown.ReleaseDueUtc;
        port.Release(anonymous:true); Assert.Equal(1,runtime.ActiveExecutions); Assert.Equal(1,fixture.Media.ActiveLeases);
        port.Release();
        await runtime.WaitForIdleAsync(new CancellationTokenSource(3000).Token);
        Assert.Empty(await fixture.Store.GetUnreclaimedResourcesAsync(0,128,default));
        await using var db=new Station01DbContext(fixture.Options);
        var latest=(await db.StageEvents.Where(x=>x.RunId==run).OrderByDescending(x=>x.Sequence).FirstAsync()).PayloadJson;
        var state=JsonSerializer.Deserialize<AlgorithmResourceState>(latest,new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(due,state.ReleaseDueUtc);Assert.Equal("DispatchError",state.TechnicalTerminal);Assert.True(state.Reclaimed);
        Assert.Equal(0,fixture.Media.ActiveLeases);Assert.Equal(0,fixture.Capacity.WorkingUsed);
        Assert.True(fixture.Capacity.FilesUsed>0);Assert.Equal(1,port.Calls);
    }

    [Fact]
    public async Task ActualThreeStageConsumerDoesNotReplayUnknownAlgorithmDispatch()
    {
        await using var fixture=new Fixture(); var port=new DeclaredPort {Fail=true};var runtime=fixture.Runtime(port);
        var run=Guid.NewGuid();var tray=Guid.NewGuid();var images=await fixture.Images(run);
        var call=Request(run,images.Take(1).ToArray(),AlgorithmRole.Detection,"frozen","1",false);
        var detector=new UnknownDetection(runtime,call,tray);var plc=new NoPlc();
        var plan=Gaode.Application.Recipes.RecipeRunPlanner.BuildExecutable(Recipe011Data.Candidate(1,false),tray.ToString("D"),["s1"],"Test");
        var request=new DetectionRequest(run,tray,Guid.NewGuid(),Guid.NewGuid(),WholeTrayWorkflowStage.Detection,Guid.NewGuid(),"1",1,
            DateTimeOffset.UtcNow.AddSeconds(10),["media://"+images[0].MediaId],"Test","022:no-replay",ExpectedObjects:
            [new("object-1",new("Test-point","1",1,2,"mm","Test-frame",3),"part")])
            {SessionId=Guid.NewGuid(),SnapshotId="frozen",ClockId="Test",CriticalSaveBudgetMs=3000};
        var executor=new Gaode.Application.Workflow.ThreeStageWorkflowExecutor(detector,plc,
            new Gaode.Application.Workflow.RecipeSortingMapper(fixture.Store),fixture.Store,
            new Gaode.Application.Workflow.SortingTargetAllocator(fixture.Store,TimeProvider.System,3000));
        var result=await executor.ExecuteAsync(new(request,plan,Guid.NewGuid(),Guid.NewGuid()));
        Assert.Equal(Gaode.Application.Workflow.ThreeStageExecutionStatus.Failed,result.Status);
        Assert.Equal("DetectionExecutionUnknownNoReplay",result.ErrorCode);Assert.Equal(1,detector.Calls);Assert.Equal(1,port.Calls);Assert.Equal(0,plc.Calls);
        var unknown=Assert.Single(await fixture.Store.GetUnreclaimedResourcesAsync(0,128,default));
        Assert.Equal(call.CallId,unknown.CallId);Assert.False(unknown.Reclaimed);var due=unknown.ReleaseDueUtc;
        port.Release();await runtime.WaitForIdleAsync(new CancellationTokenSource(3000).Token);
        var events=await fixture.Store.ReadAsync(run,tray,WholeTrayWorkflowStage.Detection);
        Assert.Contains(events,e=>e.ErrorCode=="DetectionExecutionUnknownNoReplay");
        Assert.Empty(await fixture.Store.GetUnreclaimedResourcesAsync(0,128,default));
        Assert.Equal(due,JsonSerializer.Deserialize<AlgorithmResourceState>(events.Last(e=>e.EventType==Gaode.Application.Workflow.StageEventType.AlgorithmLifecycleRecorded).PayloadJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!.ReleaseDueUtc);
    }
    [Fact]
    public async Task CancellationWinsOnceAndLateResultCannotReopenBusinessTerminal()
    {
        await using var fixture=new Fixture();var port=new HeldPort();var runtime=fixture.Runtime(port);
        var run=Guid.NewGuid();var images=await fixture.Images(run);
        var request=Request(run,images.Take(1).ToArray(),AlgorithmRole.Detection,"frozen","1",false);
        using var cancel=new CancellationTokenSource();
        await using var call=await runtime.DispatchSynchronousAsync(request,Guid.NewGuid(),1000,500,3000,_=>{},cancel.Token);
        cancel.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>call.Result);
        await fixture.Supervisor.FlushAsync(default);
        var before=Assert.Single(await fixture.Store.GetUnreclaimedResourcesAsync(0,128,default));
        port.Result();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>call.Result);
        Assert.Equal("NG",(await call.LateResult).DetectionDisposition);
        await call.DisposeAsync();port.Exit.TrySetResult();await runtime.WaitForIdleAsync(new CancellationTokenSource(3000).Token);
        var rows=await fixture.Store.ReadAsync(run,before.TrayId,WholeTrayWorkflowStage.Detection);
        var last=JsonSerializer.Deserialize<AlgorithmResourceState>(rows.Last(x=>x.EventType==Gaode.Application.Workflow.StageEventType.AlgorithmLifecycleRecorded).PayloadJson,new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal("Cancelled",last.TechnicalTerminal);Assert.Equal(before.ReleaseDueUtc,last.ReleaseDueUtc);Assert.True(last.Reclaimed);
    }
    internal sealed class HeldPort : IAlgorithmPort
    {
        public TaskCompletionSource Exit=new(TaskCreationOptions.RunContinuationsAsynchronously);
        private AlgorithmRequest request=null!;private Action<AlgorithmEvent> callback=null!;private Guid worker=Guid.NewGuid();
        public ComponentExecutionOrigin Origin=>new(ComponentEvidenceSource.Test,"Test:held-call","1");
        public int Calls;
        public int CallCount(AlgorithmRole role)=>Calls;
        public ValueTask<AlgorithmDispatch> RequestAsync(AlgorithmRequest request,Action<AlgorithmEvent> callback,CancellationToken token)
        {Assert.Equal("Test",request.Envelope.Purpose);Calls++;this.request=request;this.callback=callback;callback(new(request,AlgorithmEventKind.Accepted,WorkerSessionId:worker));return ValueTask.FromResult(new AlgorithmDispatch(Exit.Task));}
        public void Result()=>callback(new(request,AlgorithmEventKind.Result,WorkerSessionId:worker,DetectionDisposition:"NG"));
    }

    private sealed class UnknownDetection(AlgorithmRuntime runtime,AlgorithmRequest call,Guid tray) : IDetectionPort
    {
        public int Calls;
        public async ValueTask<DetectionPortResult> ExecuteAsync(DetectionRequest request,CancellationToken token)
        {
            Calls++;
            await Assert.ThrowsAsync<IOException>(()=>runtime.DispatchSynchronousAsync(call,tray,1000,1000,3000,_=>{},token));
            return new(request,DetectionResultKind.Disconnected,null,[],ResultSource.Test,ResultQuality.Unknown,"Test:unknown-dispatch",DateTimeOffset.UtcNow);
        }
    }
    private sealed class NoPlc : IPlcStageActionPort
    { public int Calls;public ValueTask<PlcStageActionResult> ExecuteAsync(PlcStageActionRequest request,CancellationToken token)
      {Calls++;throw new InvalidOperationException("Test:unexpected-motion");} }

    internal static AlgorithmRequest Request(Guid run,IReadOnlyList<MediaRef> inputs,AlgorithmRole role,string snapshot,string parameters,bool fusion)
    {
        var tick=TimeProvider.System.GetTimestamp();
        var target=new WorkerTargetIdentity("object-1",1,1,"A") {StageId="stage-1"};
        return new(new(run,Guid.NewGuid(),1,Guid.NewGuid(),snapshot,"1","Test",tick,tick+5*TimeProvider.System.TimestampFrequency,"Test"),
            Guid.NewGuid(),inputs.Last().CaptureId,role,inputs,parameters,"Test-capability","1",Guid.NewGuid(),"Test:declared-software-only",target)
            {InputIdentities=fusion?[target,target with {Camera="B"}]:null,
             FrozenModule=new("Test:reference-only", "Test-binding", "Test-capability", "1", "Test-contract", usedCount(inputs),
                "Test-model", "model-"+snapshot, parameters,
                [new("Test:not-a-delivered-model","1",new string('a',64))],new("Test:not-delivered-parameters","1",new string('b',64))) };
        static int usedCount(IReadOnlyList<MediaRef> images)=>images.Count;
    }
    private sealed class DeclaredPort : IAlgorithmPort
    {
        private readonly List<AlgorithmRequest> requests=[];private Action<AlgorithmEvent>? callback;private AlgorithmRequest? current;
        private Guid worker;
        public bool Fail; public AlgorithmRequest? Stale;
        public int Calls=>requests.Count;
        public int ForRun(Guid run,AlgorithmRole role)=>requests.Count(x=>x.Envelope.RunId==run&&x.Role==role);
        public int CallCount(AlgorithmRole role)=>requests.Count(x=>x.Role==role);
        public ComponentExecutionOrigin Origin=>new(ComponentEvidenceSource.Test,"Test:declared-port","1");
        public ValueTask<AlgorithmDispatch> RequestAsync(AlgorithmRequest request,Action<AlgorithmEvent> events,CancellationToken token)
        {
            Assert.Equal("Test",request.Envelope.Purpose);requests.Add(request);current=request;callback=events;worker=Guid.NewGuid();
            events(new(request,AlgorithmEventKind.Accepted,WorkerSessionId:worker));
            if(Stale is not null) events(new(Stale,AlgorithmEventKind.Result,DetectionDisposition:"NG"));
            if(Fail) throw new IOException("Test:representative-technical-failure");
            events(new(request with {TargetIdentity=request.TargetIdentity! with {ObjectId="wrong-object"}},AlgorithmEventKind.Result,WorkerSessionId:worker,DetectionDisposition:"NG"));
            events(new(request,AlgorithmEventKind.Result,WorkerSessionId:Guid.NewGuid(),DetectionDisposition:"NG"));
            events(new(request with {FrozenModule=request.FrozenModule! with {ModelVersion="wrong-model"}},AlgorithmEventKind.Result,WorkerSessionId:worker,DetectionDisposition:"NG"));
            events(new(request,AlgorithmEventKind.Result,WorkerSessionId:worker,DetectionDisposition:"OK"));
            events(new(request,AlgorithmEventKind.Result,WorkerSessionId:worker,DetectionDisposition:"NG"));
            events(new(request,AlgorithmEventKind.InputReleased,WorkerSessionId:worker));
            return ValueTask.FromResult(new AlgorithmDispatch(Task.CompletedTask));
        }
        public void Release(bool anonymous=false)=>callback!(new(current!,AlgorithmEventKind.WorkerExited,WorkerSessionId:anonymous?null:worker));
    }
    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string root=Path.Combine(Path.GetTempPath(),"gaode-022-managed-"+Guid.NewGuid().ToString("N"));
        public DbContextOptions<Station01DbContext> Options {get;}
        public MediaCapacity Capacity {get;}=new(1024*1024,0,1024*1024,1024*1024);
        public MediaStore Media {get;}
        public StageEventStore Store {get;}
        public AlgorithmResourceSupervisor Supervisor {get;}
        public Fixture() {Options=CameraCaptureJournal.Prepare(root);Media=new(root,Capacity,new(),1);Store=new(Options);Supervisor=new(Store,TimeProvider.System,3000);}
        public AlgorithmRuntime Runtime(IAlgorithmPort port)=>new(port,Media,new OperationIngress(new DeadlineScheduler(TimeProvider.System,"Test")),new(Media),resources:Supervisor);
        public async Task<MediaRef[]> Images(Guid run)
        {
            await using(var db=new Station01DbContext(Options))
            {db.Runs.Add(new(){RunId=run,RequestId=run.ToString(),SubjectId="Test",ContextJson="{\"purpose\":\"Test\"}",Revision=1,CreatedUtc=DateTimeOffset.UtcNow});await db.SaveChangesAsync();}
            var result=new List<MediaRef>();await using var writer=new TraceWriter(Options,TimeProvider.System,8);var revision=1L;
            for(var i=0;i<2;i++)
            {
                var capture=Guid.NewGuid();MediaRef image;
                using(Media.ReserveCapture(capture,"Detection",4)) image=(await Media.SaveAsync(run,capture,"Detection","1","1",[1,2,3,4],"bin","Test",default)) with {Purpose="Test"};
                var json=JsonSerializer.Serialize(image,new JsonSerializerOptions(JsonSerializerDefaults.Web));
                var saved=await writer.SubmitCritical(new(Guid.NewGuid(),run,revision,WriteKind.Media,json,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))))).Completion;
                Assert.Equal(CommitState.Committed,saved.State);revision=saved.CommittedRevision!.Value;await Media.MarkCommittedAsync(image,default);result.Add(image);
            }
            return result.ToArray();
        }
        public ValueTask DisposeAsync() {Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(root,true);return ValueTask.CompletedTask;}
    }
}
