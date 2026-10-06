using System.Diagnostics;
using Gaode.Application.Ports;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Devices.Plc;
using Gaode.Plc.Protocol;
using Xunit;

namespace Gaode.Communication.Tests.Devices;

[Collection("CommunicationTcp")]
public sealed class PlcPolling013AcquisitionTests
{
    [Fact]
    public async Task IdleSourcesAreBoundedAndActionWakesWithoutWaitingForIdleTimer()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var plc = new ProtocolTcpFixture(150); await plc.StartAsync(deadline.Token);
        await using var probe = new PlcPolling013WireProbe(plc.Port); probe.Start();
        await using var device = plc.Device(communicationPort: probe.Port);
        await ActionHandshakeTests.StartAndPrepareAsync(device, deadline.Token);
        await Task.Delay(550, deadline.Token);
        var start = Stopwatch.GetTimestamp();
        // The same read-only semantic query used by the API must not initiate collection.
        for (var i = 0; i < 10; i++) { _ = device.Observe(); await Task.Delay(200, deadline.Token); }
        var idle = probe.Exchanges.Where(e => e.Received >= start).ToArray();
        Assert.InRange(idle.Count(e => e.Function == 1 && e.Offset == 0), 6, 8);
        Assert.InRange(idle.Count(e => e.Function == 3 && e.Offset == 0x7F), 3, 5);
        Assert.InRange(idle.Count(e => e.Function == 3 && e.Offset == 12), 1, 3);
        Assert.DoesNotContain(idle, e => e.Function == 3 && e.Offset is 21 or 33 or 96);
        Assert.Equal(2, idle.Select(e => e.Connection).Distinct().Count());
        var before = Stopwatch.GetTimestamp();
        var reached = await ActionHandshakeTests.MoveAsync(device, "3D", new("013", "1", 12, 24, "mm", "SIM_MACHINE", 0), deadline.Token);
        Assert.True(reached.IsCorrelated);
        var moving = probe.Exchanges.Where(e => e.Received >= before && e.Function == 3 && e.Offset == 0x7F).ToArray();
        var trigger = probe.Exchanges.Where(e => e.Received >= before && e.Function == 5 && e.Offset == 34 && e.Count != 0).Single();
        Assert.True(Stopwatch.GetElapsedTime(trigger.Completed, moving.First(e => e.Received > trigger.Completed).Completed).TotalMilliseconds <= 75);
        // Intermediate and arrival values must both be actual replies, including both axes.
        Assert.Contains(moving, e => Word(e, 0x7F) == 0 && Word(e, 0x80) == 0);
        Assert.Contains(moving, e => Word(e, 0x7F) == 1 && Word(e, 0x80) == 1);
        Assert.True(reached.Positions.Single().Actual.Identity.SampleStartedUtc >= reached.Observations.Min(x => x.SampleStartedUtc));
        probe.Save("I-ACQ");
    }

    internal static ushort Word(PlcPolling013WireProbe.Exchange e, int offset)
    {
        var bytes = Convert.FromHexString(e.Response);
        return System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(9 + 2 * (offset - e.Offset), 2));
    }

    [Fact]
    public async Task FrozenPlansAreReusedAndAnotherMappingCannotIssueIo()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await using var plc = new ProtocolTcpFixture(150); await plc.StartAsync(deadline.Token);
        await using var wire = plc.Client();
        var first = new PlcDefinitionAdmission(ConfirmedProtocol.CreateTest(Float32ByteOrder.Abcd)); first.Prepare();
        var second = new PlcDefinitionAdmission(ProtocolDefinitionAdmissionTests.Input("PD-P03-layout/noncontiguous-split").Definition); second.Prepare();
        var plans = first.Plans; first.Prepare(); Assert.Same(plans, first.Plans);
        var accessor = new PlcSignalAccessor(wire, first);
        var plan = first.Plans.Get(PreparedPlcReadPlans.Base);
        for (var i = 0; i < 3; i++) { await accessor.ReadAsync(plan, deadline.Token); Assert.Same(plan, first.Plans.Get(PreparedPlcReadPlans.Base)); }
        var count = wire.ExchangeSequence;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => accessor.ReadAsync(second.Plans.Get(PreparedPlcReadPlans.Base), deadline.Token));
        Assert.Equal("PreparedPlanMappingMismatch", error.Message); Assert.Equal(count, wire.ExchangeSequence);
        Assert.Equal(18, count); // Six confirmed legal blocks, three actual reads.
    }
}
