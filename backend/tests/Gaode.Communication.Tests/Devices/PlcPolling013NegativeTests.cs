using System.Diagnostics;
using Gaode.Plc.Protocol;
using Xunit;

namespace Gaode.Communication.Tests.Devices;

[Collection("CommunicationTcp")]
public sealed class PlcPolling013NegativeTests
{
    [Fact]
    public async Task IndependentWireBudgetRejectsAnActualSecondContinuousReader()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        await using var plc = new ProtocolTcpFixture(); await plc.StartAsync(deadline.Token);
        await using var probe = new PlcPolling013WireProbe(plc.Port); probe.Start();
        await using var device = plc.Device(communicationPort: probe.Port);
        await ActionHandshakeTests.StartAndPrepareAsync(device, deadline.Token);
        var action = ActionHandshakeTests.MoveAsync(device, "3D", new("N1", "1", 12, 24, "mm", "SIM_MACHINE", 0), deadline.Token);
        await ProtocolTcpFixture.UntilAsync(() => probe.Exchanges.Any(e => e.Function == 3 && e.Offset == 0x7F &&
            PlcPolling013AcquisitionTests.Word(e, 0x7F) == 0 && PlcPolling013AcquisitionTests.Word(e, 0x80) == 0), deadline.Token);
        await Task.Delay(100, deadline.Token);
        var start = Stopwatch.GetTimestamp(); await Task.Delay(1100, deadline.Token);
        var legalEnd = Stopwatch.GetTimestamp();
        var legal = probe.Exchanges.Count(e => e.Received >= start && e.Received < legalEnd && e.Function == 3 && e.Offset == 0x7F);
        Assert.True(AcceptActiveAxisSource(legal)); Assert.False(action.IsCompleted);
        // An action wait deliberately regresses to its own second feedback reader,
        // through the same transport. Product cadence and simulator duration remain unchanged.
        for (var i = 0; i < 6; i++)
        { await device.StageSignals.ReadAsync([SignalId.XPosConfirmed, SignalId.YPosConfirmed, SignalId.ZCameraPosConfirmed, SignalId.ZScanPosConfirmed, SignalId.ZGrapPosConfirmed], deadline.Token); await Task.Delay(200, deadline.Token); }
        var negativeEnd = legalEnd + (long)(Stopwatch.Frequency * 1.1);
        var duplicate = probe.Exchanges.Count(e => e.Received >= legalEnd && e.Received < negativeEnd && e.Function == 3 && e.Offset == 0x7F);
        Assert.False(AcceptActiveAxisSource(duplicate)); Assert.True(duplicate > legal);
        Assert.True((await action).IsCorrelated); // Completion cannot excuse the duplicate communication.
        probe.Save("N1");
    }
    // Independent expectation: active feedback is 200 ms; ceil(1.1/.2)+1 permits at most 7.
    private static bool AcceptActiveAxisSource(int actualRequests) => actualRequests is >= 5 and <= 7;
}
