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
        // Physical starting position: emulate PLC actual registers, not a completed Host inspection.
        plc.Store.SetFloatFromPlc(PlcAddressMap.HoldingRegisters.MachineCurrentPosX, 11);
        plc.Store.SetFloatFromPlc(PlcAddressMap.HoldingRegisters.MachineCurrentPosY, 22);
        plc.Store.SetFloatFromPlc(PlcAddressMap.HoldingRegisters.MachineCurrentPosZ, 33);
        await ProtocolTcpFixture.UntilAsync(() => device.Observe().PositionForPurpose("DetectionZ")?.ActualZ == 33, timeout.Token);
        foreach (var id in new[] { SignalId.XPosConfirmed, SignalId.YPosConfirmed, SignalId.ZCameraPosConfirmed })
            plc.Store.SetHoldingRegisterFromPlc(plc.Store.Definition[id].DocumentNumber, 1);
        var before = plc.Engine.GetActionAudit().Actions.Count;
        await Move(device, new("next", "1", x, y, "mm", "SIM_MACHINE", z), timeout.Token);
        var dispatched = plc.Engine.GetActionAudit().Actions.Skip(before)
            .Where(a => a.Kind == "AxisMove" && a.Phase == "completed").Select(a => a.AxisRole).ToArray();
        Assert.Equal(expected.Length == 0 ? Array.Empty<string>() : expected.Split(','), dispatched);
        Assert.True(Math.Abs(device.Observe().PositionForPurpose("DetectionZ")!.ActualX!.Value-x) <= .01);
        Assert.True(Math.Abs(device.Observe().PositionForPurpose("DetectionZ")!.ActualY!.Value-y) <= .01);
    }
    private static PortEnvelope Envelope()
    {
        var start=Stopwatch.GetTimestamp();
        return new(Guid.NewGuid(),Guid.NewGuid(),1,Guid.NewGuid(),"same-position-test","1","Test",start,
            start+Stopwatch.Frequency*10,"Stopwatch");
    }
    private static async Task Move(LatestProtocolPlcDevice device, FixedPoint target, CancellationToken token)
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
    }
}
