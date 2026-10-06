using Gaode.Infrastructure.Devices.Plc;
using Xunit;

namespace Gaode.Communication.Tests.Devices;

[Collection("CommunicationTcp")]
public sealed class RotationFeedbackArbiterTests
{
    [Fact]
    public async Task NewNormalFeedbackSharesExistingBaseAndPositionFairnessOverActualTcp()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await using var plc = new ProtocolTcpFixture();
        await plc.StartAsync(timeout.Token);
        await using var probe = new PlcPolling013WireProbe(plc.Port);
        probe.DelayBeforeForward = (_, offset) => offset == 0x14 ? 100 : 0;
        probe.Start();
        await using var wire = new ModbusTcpClient("127.0.0.1", probe.Port, 1, TimeSpan.FromMilliseconds(1000));
        var scheduled = new PlcScheduledTransport(wire, 1000);
        // Submit synchronously before the one drain resumes. The first critical
        // request holds the connection; all feedback belongs to that same queue.
        Task<ushort[]> Submit(string source, ushort offset)
        {
            var prior = PlcCommunicationMeasurement.Source.Value;
            try { PlcCommunicationMeasurement.Source.Value = source; return scheduled.ReadRegistersAsync(offset, 1, timeout.Token); }
            finally { PlcCommunicationMeasurement.Source.Value = prior; }
        }
        var tasks = new[] { Submit("K", 0x14), Submit("B", 0x10), Submit("P", 0x11),
            Submit("G", 0x12), Submit("R", 0x13), Submit("B", 0x15) };
        await Task.WhenAll(tasks);
        await ProtocolTcpFixture.UntilAsync(() => probe.Exchanges.Count == 6, timeout.Token);
        var order = probe.Exchanges.Select(e => (int)e.Offset).ToArray();
        Assert.Equal(6, order.Length);
        Assert.Equal(0x14, order[0]);
        Assert.True(Array.IndexOf(order, 0x12) < Array.IndexOf(order, 0x15), "G must receive the existing feedback slot before a second B request");
        Assert.True(Array.IndexOf(order, 0x11) <= 3, "P retains its existing two ordinary-slot bound");
        Assert.Contains(0x13, order);
    }

    [Fact]
    public async Task PromptWireReplyStillReportsAndRejectsTheOriginalAbsoluteDeadline()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await using var plc = new ProtocolTcpFixture(); await plc.StartAsync(timeout.Token);
        await using var probe = new PlcPolling013WireProbe(plc.Port); probe.Start();
        await using var wire = new ModbusTcpClient("127.0.0.1", probe.Port, 1, TimeSpan.FromMilliseconds(1000));
        var clock = new DeadlineClock();
        var scheduled = new PlcScheduledTransport(wire, 1000, clock);
        probe.DelayBeforeForward = (_, _) =>
        {
            // Explicit component clock fault, no modified/fabricated TCP reply.
            clock.Advance(); return 0;
        };
        try
        {
            var error = await Assert.ThrowsAsync<TimeoutException>(() => scheduled.ReadCoilsAsync(0, 1, timeout.Token));
            Assert.Equal("PlcConnectionQueueOrExchangeDeadline", error.Message);
            await ProtocolTcpFixture.UntilAsync(() => probe.Exchanges.Count == 1, timeout.Token);
            Assert.Single(wire.Exchanges);
            Assert.Contains("PlcConnectionQueueOrExchangeDeadline", wire.Exchanges[0].Error);
            var refusal = await Assert.ThrowsAsync<PlcConnectionUnusableException>(() => scheduled.ReadCoilsAsync(0, 1, timeout.Token));
            Assert.Same(error, refusal.FirstCause);
            Assert.Equal("Connection requires reconciliation; automatic reconnect is disabled.", refusal.Message);
            Assert.Single(probe.Exchanges); // Refusal is distinct and sends no second request.
            await wire.ResetConnectionAsync(timeout.Token);
            probe.DelayBeforeForward = (_, _) => 0;
            var recovered = await scheduled.ReadCoilsAsync(0, 1, timeout.Token);
            Assert.Single(recovered); // Only the explicit existing reset clears the first cause.
            // Diagnostic snapshot itself is independent of the optional trace switch.
            var deadline = new PlcIoDeadline(clock, clock.GetTimestamp(), 1000, timeout.Token);
            clock.Advance();
            Assert.Throws<TimeoutException>(() => deadline.Check("ComponentAbsoluteCheck"));
            var fact = deadline.Snapshot;
            Assert.Equal(1000, fact.Milliseconds);
            Assert.Equal(clock.TimestampFrequency, fact.Frequency);
            Assert.Equal("ComponentAbsoluteCheck", fact.LastCheckpoint);
            Assert.Equal("TimeoutException", fact.LastResult);
            Assert.True(fact.LastElapsedMs >= 1001);
            using var caller = new CancellationTokenSource(); caller.Cancel();
            var cancelled = new PlcIoDeadline(clock, clock.GetTimestamp(), 1000, caller.Token, caller.Token);
            clock.Advance();
            Assert.True(cancelled.IsExpired);
            Assert.True(cancelled.CallerCancelled);
            Assert.ThrowsAny<OperationCanceledException>(() => cancelled.Check("CancelledCallerStillCancelled"));
        }
        finally { PlcScheduledTransport.Eligibility.Value = null; }
    }

    [Fact]
    public void MovingWatchPreservesActualShortStateButRejectsStaleUnsentAndForeignEpoch()
    {
        var axis = Gaode.Plc.Protocol.SignalId.XPosConfirmed;
        var clock = new PlcExchangeClock();
        var watch = new AxisMovingWatch(7, new Dictionary<Gaode.Plc.Protocol.SignalId, PlcExchangeClock> { [axis] = clock });
        SignalValues Sample(long sent, ushort state)
        {
            var value = new SignalValues(new Dictionary<Gaode.Plc.Protocol.SignalId, ushort[]> { [axis] = [state] },
                Gaode.Plc.Protocol.Float32ByteOrder.Abcd);
            value.Stamps[axis] = new(sent, sent, sent + 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            return value;
        }
        watch.Observe(Sample(100, 0), 7); Assert.False(watch.SawMoving(axis)); // Not dispatched.
        clock.Ended = 200;
        watch.Observe(Sample(199, 0), 7); Assert.False(watch.SawMoving(axis));
        watch.Observe(Sample(201, 0), 6); Assert.False(watch.SawMoving(axis));
        watch.Observe(Sample(202, 1), 7); Assert.False(watch.SawMoving(axis)); // Arrival alone.
        watch.Observe(Sample(203, 0), 7); Assert.True(watch.SawMoving(axis));
        watch.Observe(Sample(204, 1), 7); Assert.True(watch.SawMoving(axis)); // Consumer resumes late.
        var next = new AxisMovingWatch(7, new Dictionary<Gaode.Plc.Protocol.SignalId, PlcExchangeClock> { [axis] = new() { Ended = 205 } });
        next.Observe(Sample(203, 0), 7); Assert.False(next.SawMoving(axis)); // No next-action carryover.
    }

    [Fact]
    public async Task CancelledQueuedReadCanReenterWithoutSendingTheObsoleteRequest()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        using var cancelled = new CancellationTokenSource();
        await using var plc = new ProtocolTcpFixture(); await plc.StartAsync(timeout.Token);
        await using var probe = new PlcPolling013WireProbe(plc.Port);
        probe.DelayBeforeForward = (_, offset) => offset == 0x14 ? 100 : 0; probe.Start();
        await using var wire = new ModbusTcpClient("127.0.0.1", probe.Port, 1, TimeSpan.FromMilliseconds(1000));
        var scheduled = new PlcScheduledTransport(wire, 1000);
        var first = scheduled.ReadRegistersAsync(0x14, 1, timeout.Token);
        async Task Reenter()
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scheduled.ReadRegistersAsync(0x10, 1, cancelled.Token));
            await scheduled.ReadRegistersAsync(0x11, 1, timeout.Token);
        }
        var later = Reenter(); cancelled.Cancel();
        await Task.WhenAll(first, later);
        await ProtocolTcpFixture.UntilAsync(() => probe.Exchanges.Count == 2, timeout.Token);
        Assert.DoesNotContain(probe.Exchanges, e => e.Offset == 0x10);
        Assert.Contains(probe.Exchanges, e => e.Offset == 0x11);
        Assert.Equal(2, probe.Exchanges.Count);
    }

    private sealed class DeadlineClock : TimeProvider
    {
        private long advance;
        public override long TimestampFrequency => global::System.Diagnostics.Stopwatch.Frequency;
        public override long GetTimestamp() => global::System.Diagnostics.Stopwatch.GetTimestamp() + Interlocked.Read(ref advance);
        internal void Advance() => Interlocked.Add(ref advance, TimestampFrequency * 1001 / 1000);
    }
}
