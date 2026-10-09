using System.Diagnostics;
using Gaode.Application.Ports;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Devices.Plc;
using Gaode.Plc.Protocol;
using VirtualPlc;
using Xunit;
using Gaode.Application.Recipes;
using Gaode.Application.Workflow;
using Gaode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace Gaode.Communication.Tests.Devices;

[Collection("CommunicationTcp")]
public sealed class HandshakeClosureTests
{
    internal static ActionWindow Window(double seconds=10)
    {
        var tick=Stopwatch.GetTimestamp();var utc=DateTimeOffset.UtcNow;
        return new(tick,tick+(long)(Stopwatch.Frequency*seconds),"Stopwatch",utc,utc.AddSeconds(seconds));
    }
    internal static PortEnvelope Envelope(double seconds=10)
    {
        var w=Window(seconds);
        return new(Guid.NewGuid(),Guid.NewGuid(),1,Guid.NewGuid(),"handshake-loopback","1","Test",w.StartTick,w.DueTick,w.ClockId);
    }
    internal static async Task Ready(LatestProtocolPlcDevice device,CancellationToken ct,bool start=true)
    {
        if(start)await device.StartAsync(ct);await device.ResetAsync(ct);
        var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await device.RequestStartAsync(Envelope(),Guid.NewGuid(),Guid.NewGuid(),e=>{
            if(e.Kind==DeviceEventKind.Accepted)ready.TrySetResult();
            if(e.Kind is DeviceEventKind.Failed or DeviceEventKind.UnknownHeld)ready.TrySetException(new IOException(e.ErrorCode));
        },ct);
        await ready.Task.WaitAsync(ct);
        await ProtocolTcpFixture.UntilAsync(()=>device.Observe().Readiness==DeviceReadiness.Ready,ct);
    }
    internal static async Task<DeviceEvent> Move(LatestProtocolPlcDevice device,FixedPoint point,string role,
        CancellationToken ct,double seconds=10,FlipMovePreparation? preparation=null,PortEnvelope? envelope=null)
    {
        var result=new TaskCompletionSource<DeviceEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        await device.RequestMoveAsync(new(envelope??Envelope(seconds),Guid.NewGuid(),point,Guid.NewGuid(),"loopback",role)
            {FlipPreparation=preparation},e=>{
                if(e.Kind is DeviceEventKind.Completed or DeviceEventKind.Failed or DeviceEventKind.UnknownHeld)result.TrySetResult(e);
            },ct);
        return await result.Task.WaitAsync(ct);
    }
    internal static PlcStageActionRequest Stage(LatestProtocolPlcDevice device,PlcWorkflowStage stage,double seconds=10)
        => new(new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,Guid.NewGuid(),device.Observe().ConnectionEpoch,
            "loopback","1",Guid.NewGuid(),"part",PhysicalSlotIndex:1),Guid.NewGuid(),Guid.NewGuid(),stage,"digest",Window(seconds),Guid.NewGuid().ToString());

    [Fact]
    public async Task SixAxisMappingsClearIndependentlyAndRepeat()
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));var ct=timeout.Token;
        await using var plc=new ProtocolTcpFixture(150);await plc.StartAsync(ct);
        await using var device=plc.Device(o=>o.RotationBasis=new(.01,"Test","loopback-only"));await Ready(device,ct);
        // Detection and F use public move/capture paths; GrabZ/R use the same internal stage ports as the formal adapter.
        await SamePositionTests.Move(device,new("d","1",1,2,"mm","SIM_MACHINE",3),ct);
        for(var scanRound=0;scanRound<2;scanRound++)
        {
            var f=await Move(device,new("f","1",4,5,"mm","SIM_MACHINE",6+scanRound),"F",ct);
            Assert.Equal(DeviceEventKind.Completed,f.Kind);
            var fp=Assert.Single(f.Evidence!.Positions);
            var capture=await device.OpenCaptureWindowAsync(new(fp.Correlation,CaptureRole.F,fp.Target,fp,Window()),ct);
            using var captureEvidence = await CaptureWorkFixture.CreateAsync(capture);
            Assert.Equal(AcquisitionState.Released,(await device.FinishCaptureWindowAsync(capture,
                captureEvidence.Work,Window(),ct)).State);
        }
        for(var round=0;round<2;round++)
        {
            var stage=Stage(device,PlcWorkflowStage.UnloadPreparation);
            var grab=await device.MoveStageAxesAsync(stage,new("grab","1",4,5,"mm","SIM_MACHINE",7+round),true,ct);
            Assert.True(grab.Matched);
            var rotation=Stage(device,PlcWorkflowStage.Rotate) with {TargetPurpose="Test",RotationTarget=new(10+round,"R",.01,"loopback-only")};
            var angle=await device.RotateStageAsync(rotation,ct);Assert.True(angle.Angle.Matched);
        }
        foreach(var (start,feedback) in new[]{(SignalId.XMoveStart,SignalId.XPosConfirmed),(SignalId.YMoveStart,SignalId.YPosConfirmed),
            (SignalId.ZCameraMoveStart,SignalId.ZCameraPosConfirmed),(SignalId.ZScanMoveStart,SignalId.ZScanPosConfirmed),
            (SignalId.ZGrabMoveStart,SignalId.ZGrapPosConfirmed),(SignalId.RotateStart,SignalId.RPosConfirmed)})
        {
            Assert.False(plc.Store.ReadCoilByDocumentNumber(plc.Store.Definition[start].DocumentNumber));
            Assert.Equal(1,plc.Store.ReadHoldingRegisterByDocumentNumber(plc.Store.Definition[feedback].DocumentNumber));
        }
        Assert.Contains(plc.DeviceLogs,x=>x.Contains("PlcClearConfirmed"));
    }

    [Fact]
    public async Task AxisArrivalRetentionDoesNotWaitForFeedbackClearDelay()
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));var ct=timeout.Token;
        await using var plc=new ProtocolTcpFixture(150);await plc.StartAsync(ct);
        await using var device=plc.Device();await Ready(device,ct);plc.Engine.FeedbackClearDelayMs=600;
        var move=Move(device,new("p","1",1,2,"mm","SIM_MACHINE",3),"Detection",ct);
        await ProtocolTcpFixture.UntilAsync(()=>plc.Store.GetWriteAudit().Any(w=>w.Accepted&&w.DocumentNumber==plc.Store.Definition[SignalId.XMoveStart].DocumentNumber&&w.Value==0),ct);
        Assert.False(move.IsCompleted);
        Assert.Equal(1,plc.Store.ReadHoldingRegisterByDocumentNumber(plc.Store.Definition[SignalId.XPosConfirmed].DocumentNumber));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>device.RequestMoveAsync(new(Envelope(),Guid.NewGuid(),new("next","1",5,5,"mm","SIM_MACHINE"),Guid.NewGuid(),"test"),_=>{},ct).AsTask());
        var completed=await move;Assert.Equal(DeviceEventKind.Completed,completed.Kind);
        var clear=plc.Store.GetWriteAudit().Last(w=>w.Accepted&&w.DocumentNumber==plc.Store.Definition[SignalId.ZCameraMoveStart].DocumentNumber&&w.Value==0);
        Assert.True(Assert.Single(completed.Evidence!.Positions).Actual.Identity.SampleEndedUtc<=clear.ReceivedAtUtc);
    }

    [Fact]
    public async Task AxisRetainedArrivalDoesNotRequireFeedbackClear()
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));var ct=timeout.Token;
        await using var plc=new ProtocolTcpFixture(150);await plc.StartAsync(ct);
        await using var device=plc.Device();await Ready(device,ct);plc.Engine.HoldFeedbackClear=true;
        var result=await Move(device,new("p","1",1,2,"mm","SIM_MACHINE",3),"Detection",ct,1.5);
        Assert.Equal(DeviceEventKind.Completed,result.Kind);
        Assert.Equal(1,plc.Store.ReadHoldingRegisterByDocumentNumber(plc.Store.Definition[SignalId.XPosConfirmed].DocumentNumber));
        Assert.False(plc.Store.ReadCoilByDocumentNumber(plc.Store.Definition[SignalId.XMoveStart].DocumentNumber));
        Assert.Single(plc.Engine.GetActionAudit().Actions,a=>a.AxisRole=="X"&&a.Phase=="accepted");
    }

    [Theory]
    [InlineData(SimulationFault.AxisWriteResponseLost)]
    [InlineData(SimulationFault.AxisResponseDelayed)]
    [InlineData(SimulationFault.AxisClearWriteResponseLost)]
    public async Task UnknownWriteOrDelayedTransportDoesNotReplay(SimulationFault fault)
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));var ct=timeout.Token;
        await using var plc=new ProtocolTcpFixture(150);await plc.StartAsync(ct);
        await using var device=plc.Device();await Ready(device,ct);Assert.True(plc.Engine.InjectFault(fault).Accepted);
        var result=await Move(device,new("p","1",1,2,"mm","SIM_MACHINE",3),"Detection",ct,3);
        Assert.NotEqual(DeviceEventKind.Completed,result.Kind);
        Assert.Single(plc.Store.GetWriteAudit(),w=>w.Accepted&&w.DocumentNumber==plc.Store.Definition[SignalId.XMoveStart].DocumentNumber&&w.Value==1);
        Assert.NotNull(device.Failure);
    }
    [Theory]
    [InlineData("before-write")]
    [InlineData("expired")]
    [InlineData("wrong-epoch")]
    public void ClearanceRejectsOldOrInvalidReads(string fault)
    {
        var fields=new[]{SignalId.XMoveStart,SignalId.XPosConfirmed};
        var values=new SignalValues(fields.ToDictionary(id=>id,_=>new ushort[]{0}),Float32ByteOrder.Abcd);
        var now=DateTimeOffset.UtcNow;
        foreach(var id in fields)values.Stamps[id]=new(101,fault=="before-write"?99:101,102,
            fault=="expired"?now.AddSeconds(-10):now,now);
        var error=Assert.Throws<IOException>(()=>LatestProtocolPlcDevice.ConfirmClearance(Guid.NewGuid(),2,
            fault=="wrong-epoch"?1:2,100,values,fields,now,500));
        Assert.Contains(fault=="expired"?"Expired":"Invalid",error.Message);
    }
    [Fact]
    public async Task FlipHoldsParentUntilPutBackWithItsOwnWindow()
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));var ct=timeout.Token;
        await using var plc=new ProtocolTcpFixture(150);await plc.StartAsync(ct);
        await using var device=plc.Device(o=>o.PosePrograms=[new("part","pose","1","CD",1,new ushort[16],"Test-only mapping","Test")]);
        await Ready(device,ct);
        for(var round=0;round<2;round++)
        {
            var point=new FixedPoint("flip","1",4,5,"mm","SIM_MACHINE");
            var before=plc.Store.GetWriteAudit().Count;
            var transition=Guid.NewGuid();var pose=new RecipeTargetPose("pose","1","CD");var env=Envelope();
            var positioned=await Move(device,point,"FlipPick",ct,preparation:new(transition,"part",pose,"part",1),envelope:env);
            Assert.Equal(DeviceEventKind.Completed,positioned.Kind);
            var position=Assert.Single(positioned.Evidence!.Positions);
            var correlation=position.Correlation with {ActionId=Guid.NewGuid(),TrayId=Guid.NewGuid(),ObjectId="part",PhysicalSlotIndex=1,PlanRevision="1"};
            var flipWindow=Window(1);
            var flip=await device.FlipAsync(new(correlation,transition,"part",pose,position,flipWindow,Guid.NewGuid()),ct);
            Assert.Equal(DeviceCompletionMeaning.FlipCompleted,flip.Meaning);
            Assert.Equal(1,plc.Store.ReadHoldingRegisterByDocumentNumber(plc.Store.Definition[SignalId.FlipSorting].DocumentNumber));
            Assert.Equal(2,plc.Store.ReadHoldingRegisterByDocumentNumber(plc.Store.Definition[SignalId.FlipStatus].DocumentNumber));
            await Task.Delay(1100,ct); // PutBack must not inherit Flip's now-closed deadline.
            var back=await Move(device,point,"FlipPutBack",ct,envelope:Envelope() with {RunId=env.RunId});
            Assert.Equal(DeviceEventKind.Completed,back.Kind);
            var bp=Assert.Single(back.Evidence!.Positions);
            var result=await device.PutBackAsync(new(correlation with {ActionId=Guid.NewGuid(),OperationId=bp.Correlation.OperationId},transition,bp,Window(),Guid.NewGuid()),ct);
            Assert.Equal(DeviceCompletionMeaning.PutBackCompleted,result.Meaning);
            foreach(var id in new[]{SignalId.FlipSorting,SignalId.FlipStatus,SignalId.FlipUnloadStatus})
                Assert.Equal(0,plc.Store.ReadHoldingRegisterByDocumentNumber(plc.Store.Definition[id].DocumentNumber));
            var writes=plc.Store.GetWriteAudit().Skip(before).Where(w=>w.Accepted&&w.DocumentNumber==plc.Store.Definition[SignalId.FlipSorting].DocumentNumber).ToArray();
            Assert.Equal(new ushort[]{1,2,0},writes.Select(w=>w.Value));
            Assert.True(result.Observations.Single().SampleEndedUtc<=writes.Last().ReceivedAtUtc);
        }
    }

    [Theory]
    [InlineData(false,false)]
    [InlineData(true,false)]
    [InlineData(false,true)]
    [InlineData(true,true)]
    public async Task TransitionOwnsDeviceUntilRealSqliteSaveReceipt(bool failReceipt,bool puttingBack)
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));var ct=timeout.Token;
        await using var plc=new ProtocolTcpFixture(150);await plc.StartAsync(ct);
        await using var device=plc.Device(o=>o.PosePrograms=[new("part","pose","1","CD",1,new ushort[16],"Test-only mapping","Test")]);
        await Ready(device,ct);
        var transition=Guid.NewGuid();var pose=new RecipeTargetPose("pose","1","CD");
        var move=await Move(device,new("flip","1",4,5,"mm","SIM_MACHINE"),"FlipPick",ct,preparation:new(transition,"part",pose,"part",1));
        var position=Assert.Single(move.Evidence!.Positions);
        var correlation=position.Correlation with {ActionId=Guid.NewGuid(),TrayId=Guid.NewGuid(),ObjectId="part",PhysicalSlotIndex=1,PlanRevision="1"};
        if(puttingBack)
        {
            await device.FlipAsync(new(correlation,transition,"part",pose,position,Window(),Guid.NewGuid()),ct);
            var back=await Move(device,position.Target,"FlipPutBack",ct,envelope:Envelope() with {RunId=correlation.RunId});
            position=Assert.Single(back.Evidence!.Positions);
            correlation=correlation with {ActionId=Guid.NewGuid(),OperationId=position.Correlation.OperationId};
        }
        var committed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        plc.EvidenceWriter.AfterCommunicationCommitBeforeReceipt=async (batch,result,token)=>{
            if(batch.ActionId!=correlation.ActionId)return;
            Assert.Equal(ActualCommitState.Committed,result.ActualCommit);committed.TrySetResult();
            await release.Task.WaitAsync(token);if(failReceipt)throw new IOException("test receipt unavailable after actual SQLite commit");
        };
        var flip=puttingBack ? device.PutBackAsync(new(correlation,transition,position,Window(),Guid.NewGuid()),ct) :
            device.FlipAsync(new(correlation,transition,"part",pose,position,Window(),Guid.NewGuid()),ct);
        await committed.Task.WaitAsync(ct);
        Assert.False(flip.IsCompleted);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>device.RequestMoveAsync(new(Envelope(),Guid.NewGuid(),position.Target,Guid.NewGuid(),"test","FlipPutBack"),_=>{},ct).AsTask());
        release.TrySetResult();
        if(failReceipt)
        {
            await Assert.ThrowsAnyAsync<IOException>(()=>flip);Assert.NotNull(device.Failure);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>device.RequestMoveAsync(new(Envelope(),Guid.NewGuid(),position.Target,Guid.NewGuid(),"test","FlipPutBack"),_=>{},ct).AsTask());
        }
        else Assert.Equal(puttingBack?DeviceCompletionMeaning.PutBackCompleted:DeviceCompletionMeaning.FlipCompleted,(await flip).Meaning);
        plc.EvidenceWriter.AfterCommunicationCommitBeforeReceipt=null;
    }

    private static readonly BusinessDurations Budget=new(1000,1000,10000,1000,1000,1000,1000,1000,1000,1000,2000,1000,1000,3000,1000,1000);
    [Fact]
    public async Task InactiveGripperPlaceholderCannotDispatchWithoutSortingSafetyConfiguration()
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));var ct=timeout.Token;
        await using var plc=new ProtocolTcpFixture(150);await plc.StartAsync(ct);
        await using var device=plc.Device();await Ready(device,ct);
        var allocator=new SortingTargetAllocator(new StageEventStore(plc.DatabaseOptions),TimeProvider.System,2000);
        var adapter=new LatestProtocolStageActionAdapter(device,Budget,allocator);
        var request=Stage(device,PlcWorkflowStage.Sorting) with {
            PhysicalSlotIndex=1,SortingSource=new("source","1",1,2,"mm","SIM_MACHINE",3),
            SortingTarget=new("target","1",4,5,"mm","SIM_MACHINE",6),
            PositionTolerance=.01,TargetPurpose="Test",ReservationReference="reserved",RequestedGripperId=1};
        var before=plc.Store.GetWriteAudit().Count;
        var result=await adapter.ExecuteAsync(request,ct);
        Assert.Equal(StageActionKind.Failed,result.Kind);
        Assert.Equal(0,plc.Store.ReadHoldingRegisterByDocumentNumber(plc.Store.Definition[SignalId.SortingCmd].DocumentNumber));
        var prohibited=new[]{SignalId.GrabId,SignalId.SortingCmd,SignalId.XMoveStart,SignalId.YMoveStart,SignalId.ZGrabMoveStart}
            .Select(id=>plc.Store.Definition[id].DocumentNumber).ToHashSet();
        Assert.DoesNotContain(plc.Store.GetWriteAudit().Skip(before),w=>w.Accepted&&w.Value!=0&&prohibited.Contains(w.DocumentNumber));
        Assert.Contains(plc.DeviceLogs,line=>line.Contains("SortingSafety")||line.Contains("SortingSafe"));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SortingUsesActualCommitAndFinalSafetyBeforeParentClear(bool omitIntent)
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(35));var ct=timeout.Token;
        await using var plc=new ProtocolTcpFixture(150);await plc.StartAsync(ct);
        await using var device=plc.Device(o=>o.SortingSafePosition=new(9,"mm","SIM_MACHINE","Test","loopback-only"));await Ready(device,ct);
        var events=new StageEventStore(plc.DatabaseOptions);var allocator=new SortingTargetAllocator(events,TimeProvider.System,2000);
        var adapter=new LatestProtocolStageActionAdapter(device,Budget,allocator);
        for(var round=0;round<(omitIntent?1:2);round++)
        {
            var request=Stage(device,PlcWorkflowStage.Sorting,25) with {
                PhysicalSlotIndex=1,SortingSource=new("source","1",1,2,"mm","SIM_MACHINE",3),
                SortingTarget=new("target","1",4,5,"mm","SIM_MACHINE",6),PositionTolerance=.01,TargetPurpose="Test",ReservationReference="reserved",RequestedGripperId=2};
            await using(var db=new Station01DbContext(plc.DatabaseOptions))
            {
                db.Runs.Add(new RunEntity {RunId=request.RunId,RequestId=Guid.NewGuid().ToString(),SubjectId="Test",CreatedUtc=DateTimeOffset.UtcNow});
                await db.SaveChangesAsync(ct);
            }
            var assignment=new SortingAssignment(request.OperationId,"part","s1",1,request.SortingSource!,request.SortingTarget!,"NG","test-result",request.ReservationReference!);
            request=request with {ActionParametersDigest=SortingTargetAllocator.AssignmentDigest(assignment)};
            async Task Seed(StageEventType type,object payload)
            {
                var json=JsonSerializer.Serialize(payload,new JsonSerializerOptions(JsonSerializerDefaults.Web));
                var saved=await events.AppendAsync(new(Guid.NewGuid(),request.RunId,request.TrayId,"station","line",WholeTrayWorkflowStage.Sorting,
                    request.OperationId,1,request.ConnectionEpoch,type,DateTimeOffset.UtcNow,ResultSource.Test,ResultQuality.Derived,null,
                    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))),json,Guid.NewGuid().ToString(),"1",request.Window.StartedUtc,request.Window.DeadlineUtc),ct);
                Assert.True(saved.IsCommitted);
            }
            await Seed(StageEventType.Started,new {kind="SortingAssignmentsReserved",assignments=new[]{assignment}});
            if(!omitIntent)await Seed(StageEventType.IntentRecorded,new {correlation=request.Correlation});
            var result=await adapter.ExecuteAsync(request,ct);
            if(omitIntent)
            {
                Assert.Equal(StageActionKind.UnknownHeld,result.Kind);
                Assert.DoesNotContain(plc.Store.GetWriteAudit(),w=>w.Accepted&&w.DocumentNumber==plc.Store.Definition[SignalId.SortingCmd].DocumentNumber&&w.Value==2);
                Assert.Equal(1,plc.Store.ReadHoldingRegisterByDocumentNumber(plc.Store.Definition[SignalId.SortingCmd].DocumentNumber));
                return;
            }
            Assert.True(result.IsCompleted,result.ErrorCode+" "+device.Failure);
            Assert.Equal(0,plc.Store.ReadHoldingRegisterByDocumentNumber(plc.Store.Definition[SignalId.SortingExecStatus].DocumentNumber));
            var clear=plc.Store.GetWriteAudit().Last(w=>w.Accepted&&w.DocumentNumber==plc.Store.Definition[SignalId.SortingCmd].DocumentNumber&&w.Value==0);
            Assert.True(result.Evidence!.SafeReached!.Actual.Identity.SampleEndedUtc<clear.ReceivedAtUtc);
            Assert.Contains(await events.ReadAsync(request.RunId,request.TrayId,WholeTrayWorkflowStage.Sorting,ct),e=>e.PayloadJson.Contains("SortingAssignmentInTransit"));
            Assert.Equal(2,plc.Store.ReadHoldingRegisterByDocumentNumber(plc.Store.Definition[SignalId.GrabActiveId].DocumentNumber));
        }
        Assert.Single(plc.Store.GetWriteAudit(),w=>w.Accepted&&w.DocumentNumber==plc.Store.Definition[SignalId.GrabId].DocumentNumber&&w.Value==2);
    }
    [Fact]
    public async Task DisconnectWhileWaitingClearCannotStartSuccessor()
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));var ct=timeout.Token;
        await using var plc=new ProtocolTcpFixture(150);await plc.StartAsync(ct);
        await using var device=plc.Device();await Ready(device,ct);plc.Engine.HoldFeedbackClear=true;
        var move=Move(device,new("p","1",1,2,"mm","SIM_MACHINE",3),"Detection",ct,3);
        await ProtocolTcpFixture.UntilAsync(()=>plc.DeviceLogs.Any(x=>x.Contains("PlcClearWaiting")),ct);
        await plc.Server.StopAsync(ct);
        Assert.NotEqual(DeviceEventKind.Completed,(await move).Kind);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>device.RequestMoveAsync(new(Envelope(),Guid.NewGuid(),new("next","1",5,5,"mm","SIM_MACHINE"),Guid.NewGuid(),"test"),_=>{},ct).AsTask());
        Assert.Single(plc.Store.GetWriteAudit(),w=>w.Accepted&&w.DocumentNumber==plc.Store.Definition[SignalId.XMoveStart].DocumentNumber&&w.Value==1);
    }
}
