using Gaode.Application.Ports;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Devices.Plc;
using Gaode.Plc.Protocol;
using Xunit;

namespace Gaode.Communication.Tests.Devices;

public sealed partial class ActionHandshakeTests
{
    [Theory]
    [InlineData("FC01-stale", VirtualPlc.SimulationFault.AxisResponseDelayed, "Delay")]
    [InlineData("FC03-disconnect", VirtualPlc.SimulationFault.AxisWriteResponseLost, "Close")]
    public async Task FiniteAxisTransportFaultUsesRealTcpAndKeepsUnknown(string caseId, VirtualPlc.SimulationFault fault, string phase)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        await using var plc = new ProtocolTcpFixture();
        await plc.StartAsync(timeout.Token);
        await using var device = plc.Device();
        await StartAndPrepareAsync(device, timeout.Token);
        await PrepareUnloadAsync(device, timeout.Token);
        Assert.True(plc.Engine.InjectFault(fault).Accepted);
        var request = StageRequest(device, PlcWorkflowStage.UnloadPreparation);
        var gate = new RejectingPickPort(plc, request, ActualCommitState.Unknown, ReceiptValidity.None);
        var before = plc.Store.GetWriteAudit().Last().Sequence;
        var result = await new LatestProtocolStageActionAdapter(device, ComponentBudget() with { XyCompletion = 600 }, gate)
            .ExecuteAsync(request, timeout.Token);
        Assert.True(result.IsUnknownHeld);
        Assert.False(result.CanRetry);
        Assert.Equal(0, gate.Calls);
        var write = Assert.Single(plc.Store.GetWriteAudit(), w => w.Sequence > before &&
            w.Area == PlcArea.Coil && w.DocumentNumber == 0x22 && w.Value == 1);
        Assert.NotNull(write.RequestHex);
        if (fault is VirtualPlc.SimulationFault.AxisResponseDelayed or VirtualPlc.SimulationFault.AxisWriteResponseLost)
        {
            var actual = Assert.Single(plc.Store.GetFeedbackFaults());
            Assert.Equal(phase, actual.Disposition);
            Assert.Equal(write.ConnectionId, actual.ConnectionId);
            if (fault == VirtualPlc.SimulationFault.AxisWriteResponseLost)
            {
                Assert.Null(write.ResponseHex);
                Assert.Null(write.ResponseSentAtUtc);
            }
        }
        else Assert.Contains(plc.Engine.GetActionAudit().Actions, a => a.Phase == phase);
        Assert.StartsWith("FC", caseId);
    }

    [Fact]
    public Task FiniteWrongPickPositionNeverApprovesBusinessCommitOrPlace() => VerifyWrongPickAsync(false);

    [Fact]
    public Task InitiallyMatchingPickPointCannotSurviveLaterWrongXy() => VerifyWrongPickAsync(true);

    private static async Task VerifyWrongPickAsync(bool initiallyMatching)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var plc = new ProtocolTcpFixture();
        await plc.StartAsync(timeout.Token);
        await using var device = plc.Device();
        await StartAndPrepareAsync(device, timeout.Token);
        if (initiallyMatching)
        {
            // Reproduce the independent route: the preceding unload happens to
            // equal the pick point. This is an old position, not a new arrival.
            plc.Store.SetFloatFromPlc(0xD, 100);
            plc.Store.SetFloatFromPlc(0xF, 100);
            plc.Store.SetFloatFromPlc(0x11, 150);
        }
        Assert.True(plc.Engine.InjectFault(VirtualPlc.SimulationFault.SortingPositionMismatch).Accepted);
        var request = StageRequest(device, PlcWorkflowStage.Sorting);
        var gate = new RejectingPickPort(plc, request, ActualCommitState.Unknown, ReceiptValidity.None);
        var result = await new LatestProtocolStageActionAdapter(device, ComponentBudget(), gate).ExecuteAsync(request, timeout.Token);
        Assert.True(result.IsUnknownHeld);
        Assert.False(result.CanRetry);
        Assert.Equal(0, gate.Calls);
        Assert.Contains("TransferCurrentTargetNotObserved", plc.DeviceDiagnostics);
        Assert.Equal(110, plc.Store.ReadActualFloat(0xD));
        // The existing simulator now records accepted/completed actions; the old
        // testWrongPosition audit phase and Pick=2 assertion predate integrated 011.
        Assert.Contains(plc.Engine.GetActionAudit().Actions, a => a.Kind == "Sort" && a.Phase == "accepted");
        RejectingPickPort.AssertNoPlace(plc.Store.GetWriteAudit());
    }

    [Fact]
    public async Task ExpiredWindowCannotReleaseCommittedCapture()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var plc = new ProtocolTcpFixture(motionDurationMs: 150);
        await plc.StartAsync(timeout.Token);
        await using var device = plc.Device();
        await StartAndPrepareAsync(device, timeout.Token);
        var target = new FixedPoint("3D", "1", 11, 22, "mm", "SIM_MACHINE", 33);
        var moved = await MoveAsync(device, "3D", target, timeout.Token);
        var request = new CaptureWindowRequest(moved.Correlation, CaptureRole.ThreeD, target,
            Assert.Single(moved.Positions), Window(ProtocolTcpFixture.Envelope()));
        var session = await device.OpenCaptureWindowAsync(request, timeout.Token);
        var before = plc.Store.GetWriteAudit().Last().Sequence;
        using var saved = await CaptureWorkFixture.CreateAsync(session);
        var shortWindow = Window(ProtocolTcpFixture.Envelope(1));
        await Task.Delay(10, timeout.Token);
        await Assert.ThrowsAsync<TimeoutException>(() => device.FinishCaptureWindowAsync(session,
            saved.Work, shortWindow, timeout.Token));
        Assert.DoesNotContain(plc.Store.GetWriteAudit(), w => w.Sequence > before && w.Area == PlcArea.HoldingRegister);
    }

    [Fact]
    public async Task MissingTargetStageObservationNeverSubmitsPlaceOrSortingOk()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var plc = new ProtocolTcpFixture();
        await plc.StartAsync(timeout.Token);
        await using var proxy = new StageFeedbackFaultProxy(plc.Port, StageFeedbackFault.MissingPickTarget);
        proxy.Start();
        await using var device = plc.Device(communicationPort: proxy.Port);
        await StartAndPrepareAsync(device, timeout.Token);
        var request = StageRequest(device, PlcWorkflowStage.Sorting);
        var gate = new RejectingPickPort(plc, request, ActualCommitState.Unknown, ReceiptValidity.None);
        proxy.Armed = true;
        var result = await new LatestProtocolStageActionAdapter(device, ComponentBudget(), gate).ExecuteAsync(request, timeout.Token);
        Assert.True(result.IsUnknownHeld);
        Assert.False(result.CanRetry);
        Assert.Equal(0, gate.Calls);
        Assert.NotEmpty(proxy.MutatedReplies);
        Assert.Contains("TransferCurrentTargetNotObserved", plc.DeviceDiagnostics);
        // Actual Pick was dispatched, then the unavailable current coordinates
        // blocked authorization. Unknown may stop the PLC before Pick completes.
        Assert.Contains(plc.Store.GetWriteAudit(), w => w.Area == PlcArea.HoldingRegister &&
            w.DocumentNumber == 0x21 && w.Value == 1);
        RejectingPickPort.AssertNoPlace(plc.Store.GetWriteAudit());
    }

    [Fact]
    public async Task SameTargetWithoutDistinguishableFeedbackIsUnknownHeld()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var plc = new ProtocolTcpFixture();
        await plc.StartAsync(timeout.Token);
        await using var proxy = new StageFeedbackFaultProxy(plc.Port, StageFeedbackFault.FrozenUnloadCycle);
        proxy.Start();
        await using var device = plc.Device(communicationPort: proxy.Port);
        await StartAndPrepareAsync(device, timeout.Token);
        await PrepareUnloadAsync(device, timeout.Token);
        plc.Store.SetFloatFromPlc(0xD, 300); plc.Store.SetFloatFromPlc(0xF, 100); plc.Store.SetFloatFromPlc(0x11, 150);
        var request = StageRequest(device, PlcWorkflowStage.UnloadPreparation);
        var gate = new RejectingPickPort(plc, request, ActualCommitState.Unknown, ReceiptValidity.None);
        proxy.Armed = true;
        var before = plc.Store.GetWriteAudit().Last().Sequence;
        var result = await new LatestProtocolStageActionAdapter(device, ComponentBudget() with { XyCompletion = 400 }, gate).ExecuteAsync(request, timeout.Token);
        Assert.True(result.IsUnknownHeld);
        Assert.False(result.CanRetry);
        Assert.NotEmpty(proxy.MutatedReplies);
        Assert.Equal(1, plc.Store.GetWriteAudit().Count(w => w.Sequence > before && w.Area == PlcArea.Coil && w.DocumentNumber == 0x22 && w.Value == 1));
        Assert.DoesNotContain(plc.Store.GetWriteAudit(), w => w.Sequence > before && w.Area == PlcArea.Coil && w.DocumentNumber == 0x22 && w.Value == 0);
    }

    [Fact]
    public async Task EpochChangeAfterDispatchIsUnknownAndNeverReplaysPhysicalAction()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var plc = new ProtocolTcpFixture();
        await plc.StartAsync(timeout.Token);
        await using var device = plc.Device();
        await StartAndPrepareAsync(device, timeout.Token);
        await PrepareUnloadAsync(device, timeout.Token);
        var before = plc.Store.GetWriteAudit().Last().Sequence;
        var request = StageRequest(device, PlcWorkflowStage.UnloadPreparation);
        plc.Engine.InjectFault(VirtualPlc.SimulationFault.MoveTimeout);
        var dispatched = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        plc.Store.PcValueWritten += w => { if (w.Area == PlcArea.Coil && w.DocumentNumber == 0x22 && w.Value == 1) dispatched.TrySetResult(); };
        var adapter = new LatestProtocolStageActionAdapter(device, ComponentBudget(), new RejectingPickPort(plc, request, ActualCommitState.Unknown, ReceiptValidity.None));
        var pending = adapter.ExecuteAsync(request, timeout.Token).AsTask();
        await dispatched.Task.WaitAsync(timeout.Token);
        await device.ResetAsync(timeout.Token);
        var result = await pending.WaitAsync(timeout.Token);
        Assert.True(result.IsUnknownHeld);
        Assert.False(result.CanRetry);
        Assert.True(device.Observe().ConnectionEpoch > request.ConnectionEpoch);
        Assert.Equal(1, plc.Store.GetWriteAudit().Count(w => w.Sequence > before && w.Area == PlcArea.Coil && w.DocumentNumber == 0x22 && w.Value == 1));
    }
}
