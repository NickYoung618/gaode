using System.Diagnostics;
using Gaode.Application.Ports;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Xunit;

namespace Gaode.Communication.Tests.Devices;

[Collection("CommunicationTcp")]
public sealed class PlcPolling013ObservationTests
{
    [Fact]
    public async Task NewHeartbeatCannotRenewOldPositionOrAuthorizeTheNextMotion()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var plc = new ProtocolTcpFixture(150); await plc.StartAsync(deadline.Token);
        await using var probe = new PlcPolling013WireProbe(plc.Port); probe.Start();
        await using var device = plc.Device(communicationPort: probe.Port, ioTimeoutMs: 100);
        await ActionHandshakeTests.StartAndPrepareAsync(device, deadline.Token);
        var edges = device.HeartbeatEdges;
        await ProtocolTcpFixture.UntilAsync(() => device.Observe().Position?.Identity.Reliability == DeviceReliability.Stale &&
            device.Observe().HasReliableObservation && device.HeartbeatEdges > edges, deadline.Token);
        var old = device.Observe();
        Assert.Equal(DeviceConnection.Connected, old.Connection);
        Assert.True(old.Identity!.SampleStartedUtc > old.Position!.Identity.SampleStartedUtc);
        var before = plc.Store.GetWriteAudit().Last().Sequence;
        probe.DelayBeforeForward = (function, offset) => function == 3 && offset == 12 ? 250 : 0;
        var callbacks = 0;
        await Assert.ThrowsAnyAsync<Exception>(() => device.RequestMoveAsync(new(ProtocolTcpFixture.Envelope(), Guid.NewGuid(),
            new FixedPoint("013-stale", "1", 12, 24, "mm", "SIM_MACHINE", 0), Guid.NewGuid(), "component", "3D"),
            _ => callbacks++, deadline.Token).AsTask());
        Assert.Equal(0, callbacks);
        Assert.DoesNotContain(plc.Store.GetWriteAudit().Where(w => w.Sequence > before), w =>
            w.Area == Gaode.Plc.Protocol.PlcArea.Coil && w.DocumentNumber is 34 or 35 && w.Value == 1);
        probe.Save("I-OBS-N2");
    }
}
