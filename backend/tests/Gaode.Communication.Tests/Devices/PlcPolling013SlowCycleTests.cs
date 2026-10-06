using System.Diagnostics;
using Xunit;

namespace Gaode.Communication.Tests.Devices;

[Collection("CommunicationTcp")]
public sealed class PlcPolling013SlowCycleTests
{
    [Fact]
    public async Task SlowRoundsRemainSerialAndNeverCatchUpMissedRounds()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        await using var plc = new ProtocolTcpFixture(150); await plc.StartAsync(deadline.Token);
        await using var probe = new PlcPolling013WireProbe(plc.Port); probe.Start();
        await using var device = plc.Device(communicationPort: probe.Port);
        await ActionHandshakeTests.StartAndPrepareAsync(device, deadline.Token);
        // All six base blocks are delayed; heartbeat/position keep their own actual clocks.
        probe.DelayBeforeForward = (f, o) => (f == 1 && o is 3 or 7 or 15 or 33) || (f == 3 && o is 79 or 0x7F) ? 110 : 0;
        var start = Stopwatch.GetTimestamp();
        await Task.Delay(2500, deadline.Token);
        var rounds = probe.Exchanges.Where(e => e.Received >= start && e.Function == 1 && e.Offset == 3).ToArray();
        probe.Save("I-SLOW"); // Preserve the actual fault timing even if a later assertion fails.
        Assert.InRange(rounds.Length, 2, 3);
        foreach (var pair in rounds.Zip(rounds.Skip(1)))
            Assert.True(Stopwatch.GetElapsedTime(pair.First.Received, pair.Second.Received).TotalMilliseconds >= 900);
        var wire = probe.Exchanges.Where(e => e.Received >= start && !(e.Function is 1 or 5 && e.Offset is 0 or 1)).ToArray();
        foreach (var group in wire.GroupBy(e => e.Connection))
            foreach (var pair in group.Zip(group.Skip(1))) Assert.True(pair.Second.Received >= pair.First.Completed);
    }
}
