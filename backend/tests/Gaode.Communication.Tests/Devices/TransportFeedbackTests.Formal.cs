using Gaode.Domain.Station01;
using Xunit;

namespace Gaode.Communication.Tests.Devices;

public sealed partial class TransportFeedbackTests
{
    [Fact]
    public async Task FormalDisconnectAdvancesEpochAndRemovesReliableAdmission()
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var plc = new ProtocolTcpFixture();
        await plc.StartAsync(watchdog.Token);
        await using var device = plc.Device();
        await device.StartAsync(watchdog.Token);
        await ProtocolTcpFixture.UntilAsync(() => device.Observe().HasReliableObservation, watchdog.Token);
        var previous = device.Observe();
        await plc.Server.StopAsync(watchdog.Token);
        await ProtocolTcpFixture.UntilAsync(() => !device.Observe().HasReliableObservation, watchdog.Token);
        var current = device.Observe();
        Assert.True(current.ConnectionEpoch > previous.ConnectionEpoch);
        Assert.NotEqual(SafetyAssessment.Clear, current.SafetyAssessment);
        Assert.False(string.IsNullOrWhiteSpace(device.Failure));
        var before = device.AllWriteDispatchCount;
        await Assert.ThrowsAnyAsync<InvalidOperationException>(async () => await device.RequestStartAsync(
            ProtocolTcpFixture.Envelope(), Guid.NewGuid(), Guid.NewGuid(), _ => { }, watchdog.Token));
        Assert.DoesNotContain(device.AllWriteDispatches.Skip((int)before), w => w.Offset == 8 && w.Words.SequenceEqual(new ushort[] { 1 }));
    }
    [Fact]
    public async Task HeartbeatFailureKeepsFirstFailureAndLastReliableSample()
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var plc = new ProtocolTcpFixture();
        await plc.StartAsync(watchdog.Token);
        await using var device = plc.Device();
        await device.StartAsync(watchdog.Token);
        await ProtocolTcpFixture.UntilAsync(() => device.Observe().HasReliableObservation, watchdog.Token);
        plc.Engine.InjectFault(VirtualPlc.SimulationFault.PauseHeartbeat);
        await ProtocolTcpFixture.UntilAsync(() => device.Observe().ReasonCodes.Contains("PlcHeartbeatLost"), watchdog.Token);
        var first = device.Observe();
        await Task.Delay(100, watchdog.Token);
        var later = device.Observe();
        Assert.False(later.HasReliableObservation);
        Assert.NotEqual(SafetyAssessment.Clear, later.SafetyAssessment);
        Assert.Equal(first.Identity?.SampleEndedUtc, later.Identity?.SampleEndedUtc);
        Assert.Contains("PlcHeartbeatLost", later.ReasonCodes);
        Assert.Equal("HeartbeatStoppedChanging", device.Failure);
        Assert.True(later.ConnectionEpoch > 1);
    }
}
