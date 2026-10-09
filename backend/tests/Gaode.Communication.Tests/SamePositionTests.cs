using System.Diagnostics;
using Gaode.Application.Ports;
using Gaode.Plc.Protocol;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Devices.Plc;
using Xunit;

namespace Gaode.Communication.Tests.Devices;

public sealed class SamePositionTests
{
    [Theory]
    [InlineData(12, 22, 33, "X")]
    [InlineData(11, 22, 33, "")]
    [InlineData(11, 23, 34, "Y,DetectionZ")]
    public async Task RepeatedTargetsReuseOnlySatisfiedAxes(double x, double y, double z, string expected)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var plc = new ProtocolTcpFixture(motionDurationMs: 150);
        await plc.StartAsync(timeout.Token);
        await using var device = plc.Device();
        await device.StartAsync(timeout.Token);
        await device.ResetAsync(timeout.Token);
        await ProtocolTcpFixture.UntilAsync(() => device.Observe().HasReliableObservation, timeout.Token);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await device.RequestStartAsync(Envelope(), Guid.NewGuid(), Guid.NewGuid(), e =>
        {
            if (e.Kind == DeviceEventKind.Accepted) ready.TrySetResult();
            else if (e.Kind is DeviceEventKind.Failed or DeviceEventKind.UnknownHeld)
                ready.TrySetException(new IOException(e.ErrorCode));
        }, timeout.Token);
        await ready.Task.WaitAsync(timeout.Token);
        await ProtocolTcpFixture.UntilAsync(() => device.Observe().Readiness == DeviceReadiness.Ready, timeout.Token);
        // Establish eligibility through actual TCP motion, durable evidence and both sides clearing.
        var first = await Move(device, new("first", "1", 11, 22, "mm", "SIM_MACHINE", 33), timeout.Token);
        var before = plc.Engine.GetActionAudit().Actions.Count;
        var next = await Move(device, new("next", "1", x, y, "mm", "SIM_MACHINE", z), timeout.Token);
        Assert.True(next.Evidence!.IsCorrelated);
        Assert.NotEqual(first.ActionId,next.ActionId);
        Assert.NotEqual(first.Evidence!.Positions.Single().Actual.Identity.ObservationId,next.Evidence.Positions.Single().Actual.Identity.ObservationId);
        var dispatched = plc.Engine.GetActionAudit().Actions.Skip(before)
            .Where(a => a.Kind == "AxisMove" && a.Phase == "completed").Select(a => a.AxisRole).ToArray();
        Assert.Equal(expected.Length == 0 ? Array.Empty<string>() : expected.Split(','), dispatched);
        Assert.True(Math.Abs(device.Observe().PositionForPurpose("DetectionZ")!.ActualX!.Value-x) <= .01);
        Assert.True(Math.Abs(device.Observe().PositionForPurpose("DetectionZ")!.ActualY!.Value-y) <= .01);
    }
    [Theory]
    [InlineData("initial")]
    [InlineData("reset")]
    [InlineData("drift")]
    public async Task StaticPositionWithoutCurrentClosureCannotReuse(string cause)
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));var ct=timeout.Token;
        await using var plc=new ProtocolTcpFixture(150);await plc.StartAsync(ct);
        await using var device=plc.Device();await HandshakeClosureTests.Ready(device,ct);
        var point=new FixedPoint("p","1",1,2,"mm","SIM_MACHINE",3);
        if(cause=="initial") {
            plc.Store.SetFloatFromPlc(plc.Store.Definition[SignalId.MachineCurrentPosX].DocumentNumber,1);
            plc.Store.SetFloatFromPlc(plc.Store.Definition[SignalId.MachineCurrentPosY].DocumentNumber,2);
            plc.Store.SetFloatFromPlc(plc.Store.Definition[SignalId.MachineCurrentPosZ].DocumentNumber,3);
        } else await Move(device,point,ct);
        if(cause=="reset") { await HandshakeClosureTests.Ready(device,ct,false); point=point with {X=0,Y=0,Z=0}; }
        if(cause=="drift")
        {
            plc.Store.SetFloatFromPlc(plc.Store.Definition[SignalId.MachineCurrentPosX].DocumentNumber,10);
            await ProtocolTcpFixture.UntilAsync(()=>device.Observe().Position!.ActualX==10,ct);
            plc.Store.SetFloatFromPlc(plc.Store.Definition[SignalId.MachineCurrentPosX].DocumentNumber,1);
        }
        var before=plc.Engine.GetActionAudit().Actions.Count;
        var result=await HandshakeClosureTests.Move(device,point,"Detection",ct);
        Assert.NotEqual(DeviceEventKind.Completed,result.Kind);
        Assert.Contains("AxisSameTargetWithoutCompletedAction",device.Failure);
        Assert.DoesNotContain(plc.Engine.GetActionAudit().Actions.Skip(before),a=>a.Kind=="AxisMove"&&a.Phase=="accepted");
    }

    private static PortEnvelope Envelope()
    {
        var start=Stopwatch.GetTimestamp();
        return new(Guid.NewGuid(),Guid.NewGuid(),1,Guid.NewGuid(),"same-position-test","1","Test",start,
            start+Stopwatch.Frequency*10,"Stopwatch");
    }
    internal static async Task<DeviceEvent> Move(LatestProtocolPlcDevice device, FixedPoint target, CancellationToken token)
    {
        var result=new TaskCompletionSource<DeviceEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        var accepted=false;
        await device.RequestMoveAsync(new(Envelope(),Guid.NewGuid(),target,Guid.NewGuid(),"component-binding","Detection"), e =>
        {
            if(e.Kind == DeviceEventKind.Accepted) accepted=true;
            if(e.Kind is DeviceEventKind.Completed or DeviceEventKind.Failed or DeviceEventKind.UnknownHeld) result.TrySetResult(e);
        },token);
        var completion=await result.Task.WaitAsync(token);
        Assert.True(completion.Kind == DeviceEventKind.Completed, completion.ErrorCode ?? device.Failure);
        Assert.True(accepted);
        var position = Assert.Single(completion.Evidence!.Positions);
        var window = new ActionWindow(Stopwatch.GetTimestamp(), Stopwatch.GetTimestamp()+Stopwatch.Frequency*5, "Stopwatch", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddSeconds(5));
        var capture = await device.OpenCaptureWindowAsync(new(position.Correlation, CaptureRole.Detection, target, position, window), token);
        using var captureEvidence = await CaptureWorkFixture.CreateAsync(capture);
        var closed = await device.FinishCaptureWindowAsync(capture, captureEvidence.Work, window, token);
        Assert.Equal(AcquisitionState.Released, closed.State);
        return completion;
    }
}
