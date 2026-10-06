using System.Diagnostics;
using Gaode.Application.Ports;
using Gaode.Infrastructure.Devices.Plc;
using Gaode.Plc.Protocol;
using Xunit;

namespace Gaode.Communication.Tests.Devices;

public sealed partial class ActionHandshakeTests
{
    [Fact]
    public async Task PredispatchCommunicationUsesFourTotalAttemptsAndOneTwoFourBackoff()
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        await using var plc = new ProtocolTcpFixture();
        await plc.StartAsync(watchdog.Token);
        await using var proxy = new PreflightReadFaultProxy(plc.Port);
        proxy.Start();
        await using var device = plc.Device(communicationPort: proxy.Port);
        await StartAndPrepareAsync(device, watchdog.Token);
        var request = StageRequest(device, PlcWorkflowStage.UnloadPreparation);
        var adapter = new LatestProtocolStageActionAdapter(device, ComponentBudget(),
            new RejectingPickPort(plc, request, ActualCommitState.Unknown, ReceiptValidity.None));
        var before = plc.Store.GetWriteAudit().Last().Sequence;
        proxy.Armed = true;
        var result = await adapter.ExecuteAsync(request, watchdog.Token);
        Assert.Equal(StageActionKind.Failed, result.Kind);
        Assert.False(result.HoldsDevice);
        Assert.Equal(4, proxy.RejectedAt.Count);
        var attempts = proxy.RejectedAt.ToArray();
        Assert.InRange(Stopwatch.GetElapsedTime(attempts[0], attempts[1]).TotalMilliseconds, 990, 1800);
        Assert.InRange(Stopwatch.GetElapsedTime(attempts[1], attempts[2]).TotalMilliseconds, 1990, 2800);
        Assert.InRange(Stopwatch.GetElapsedTime(attempts[2], attempts[3]).TotalMilliseconds, 3990, 4800);
        Assert.DoesNotContain(plc.Store.GetWriteAudit().Where(w => w.Sequence > before), w => w.Area == PlcArea.HoldingRegister);
        Assert.True(device.Observe().HasReliableObservation);
        Assert.Null(device.Failure);
    }
}
