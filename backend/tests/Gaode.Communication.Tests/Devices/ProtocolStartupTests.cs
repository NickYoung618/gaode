using Gaode.Application.Ports;
using Gaode.Domain.Station01;
using Gaode.Plc.Protocol;
using Xunit;

namespace Gaode.Communication.Tests.Devices;

[Collection("CommunicationTcp")]
public sealed class ProtocolStartupTests
{
    [Fact]
    public async Task ActualReadyDoesNotInventMissingRecoveryMechanics()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var plc = new ProtocolTcpFixture(motionDurationMs: 150);
        await plc.StartAsync(timeout.Token);
        await using var device = plc.Device();
        await ActionHandshakeTests.StartAndPrepareAsync(device, timeout.Token);
        var actual = await device.ReadInitialStateAsync(timeout.Token);
        Assert.Equal(InitialReadiness.Blocked, actual.Readiness);
        Assert.False(actual.Passed);
        Assert.Contains("RecoveryProtocolNotConfigured", actual.BlockedReasons);
        Assert.Null(actual.ResetGeneration);
        Assert.True(actual.Observation.HasReliableObservation);
    }

    [Fact]
    public async Task ActualReadinessHandshakeDoesNotClaimClampOrSendOldRegionCommands()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var plc = new ProtocolTcpFixture(motionDurationMs: 150);
        await plc.StartAsync(timeout.Token);
        await using var device = plc.Device();
        await device.StartAsync(timeout.Token);
        await ProtocolTcpFixture.UntilAsync(() => device.Observe().HasReliableObservation, timeout.Token);
        var before = plc.Store.GetWriteAudit().LastOrDefault()?.Sequence ?? 0;
        var envelope = ProtocolTcpFixture.Envelope(2000);
        long receivedTick = 0;
        var actionId = Guid.NewGuid();
        var result = new TaskCompletionSource<DeviceEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        await device.RequestStartAsync(envelope, actionId, Guid.NewGuid(),
            e => { if (e.Kind is DeviceEventKind.Accepted or DeviceEventKind.Failed)
                { receivedTick = System.Diagnostics.Stopwatch.GetTimestamp(); result.TrySetResult(e); } }, timeout.Token);
        var response = await result.Task.WaitAsync(timeout.Token);
        Assert.True(response.Kind == DeviceEventKind.Accepted, plc.DeviceDiagnostics);
        Assert.Equal(actionId, response.ActionId);
        Assert.True(receivedTick <= envelope.DueTick);
        Assert.Equal(envelope, response.Envelope);
        Assert.True(device.Observe().HasReliableObservation);
        Assert.Equal(DeviceReadiness.Ready, device.Observe().Readiness);
        Assert.Equal(ClampState.Unconfirmed, device.Observe().Clamp);
        Assert.True(Assert.IsType<DeviceActionEvidence>(response.Evidence).IsCorrelated);
        var writes = plc.Store.GetWriteAudit().Where(w => w.Sequence > before && w.Accepted).ToArray();
        Assert.Contains(writes, w => w.Area == PlcArea.Coil && w.DocumentNumber == 3 && w.Value == 1);
        Assert.DoesNotContain(writes, w => w.Area == PlcArea.Coil && w.DocumentNumber == 9 ||
            w.Area == PlcArea.HoldingRegister && w.DocumentNumber is 0x23 or 0x27 or 0x29 or 0x2A);
    }
}
