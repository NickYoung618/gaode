using System.Diagnostics;
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
    [InlineData("WIRE-PICK/rollback", ActualCommitState.ConfirmedRolledBack, ReceiptValidity.None)]
    [InlineData("WIRE-PICK/commit-invalid-receipt", ActualCommitState.Committed, ReceiptValidity.Invalid)]
    [InlineData("WIRE-PICK/unknown", ActualCommitState.Unknown, ReceiptValidity.None)]
    public async Task InvalidPickReceiptNeverDispatchesAnyPlaceField(string caseId, ActualCommitState actual, ReceiptValidity validity)
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var plc = new ProtocolTcpFixture(motionDurationMs: 150);
        await plc.StartAsync(watchdog.Token);
        await using var device = plc.Device();
        await StartAndPrepareAsync(device, watchdog.Token);
        var request = StageRequest(device, PlcWorkflowStage.Sorting);
        var gate = new RejectingPickPort(plc, request, actual, validity);
        var adapter = new LatestProtocolStageActionAdapter(device, ComponentBudget(), gate);
        var outcome = await adapter.ExecuteAsync(request, watchdog.Token);
        Assert.StartsWith("WIRE-PICK/", caseId);
        Assert.Equal(1, gate.Calls);
        Assert.True(outcome.IsUnknownHeld);
        Assert.False(outcome.IsCompleted);
        Assert.False(outcome.CanRetry);
        RejectingPickPort.AssertNoPlace(plc.Store.GetWriteAudit());
        Assert.Equal(1, plc.Store.GetWriteAudit().Count(w => w.Area == PlcArea.HoldingRegister && w.DocumentNumber == 0x21 && w.Value == 1));
    }

    [Fact]
    public async Task MissingUnloadTargetDispatchesNoTargets()
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var plc = new ProtocolTcpFixture();
        await plc.StartAsync(watchdog.Token);
        await using var device = plc.Device();
        await StartAndPrepareAsync(device, watchdog.Token);
        var request = StageRequest(device, PlcWorkflowStage.UnloadPreparation);
        request = request with { UnloadTarget = null };
        var adapter = new LatestProtocolStageActionAdapter(device, ComponentBudget(), new RejectingPickPort(plc, request, ActualCommitState.Unknown, ReceiptValidity.None));
        var before = plc.Store.GetWriteAudit().Last().Sequence;
        var outcome = await adapter.ExecuteAsync(request, watchdog.Token);
        Assert.False(outcome.IsCompleted);
        Assert.DoesNotContain(plc.Store.GetWriteAudit().Where(w => w.Sequence > before),
            w => w.Area == PlcArea.HoldingRegister && w.DocumentNumber is 1 or 3 or 5 or 0xB);
    }
    private static PlcStageActionRequest StageRequest(LatestProtocolPlcDevice device, PlcWorkflowStage stage)
    {
        var e = ProtocolTcpFixture.Envelope();
        var correlation = new ActionCorrelation(e.RunId, e.OperationId, Guid.NewGuid(), e.Attempt,
            e.SessionId, device.Observe().ConnectionEpoch, e.SnapshotId, "test-plan/1", Guid.NewGuid(), "Object01", PhysicalSlotIndex: 7);
        return new(correlation, Guid.NewGuid(), Guid.NewGuid(), stage, "component-target-digest", Window(e), "component-once",
            WholeTrayCompletionId: stage == PlcWorkflowStage.UnlockObservation ? Guid.NewGuid() : null,
            PhysicalSlotIndex: 7, UnloadTarget: Point("Unload", 300, 100), PositionTolerance: 0.01, TargetPurpose: "Test",
            SortingSource: Point("Pick", 100, 100), SortingTarget: Point("Place", 400, 300), ReservationReference: "component-reservation");
    }
    private static FixedPoint Point(string id, int x, int y) => new(id, "1", x, y, "mm", "SIM_MACHINE", 150);
    // Existing motion/ACK values from 007 Test fixture. Does not define or alter the recipe application budget.
    private static BusinessDurations ComponentBudget() => new(2000, 5000, 8000, 8000, 15000, 8000, 15000,
        1000, 300, 1000, 2000, 2000, 500, 3000, 10000, 1000);
    private sealed class RejectingPickPort(ProtocolTcpFixture plc, PlcStageActionRequest request,
        ActualCommitState actual, ReceiptValidity validity) : IPickCommitPort
    {
        public int Calls { get; private set; }
        public Task<PickCommitReceipt> CommitPickAsync(PickCompletionEvidence evidence, CancellationToken cancellationToken)
        {
            Calls++;
            Assert.Equal(request.Correlation, evidence.Correlation);
            Assert.True(evidence.SourcePositionReached.Matched);
            Assert.NotEmpty(evidence.DiagnosticEvidenceReferences);
            AssertNoPlace(plc.Store.GetWriteAudit());
            Assert.Equal((ushort)1, plc.Store.ReadHoldingRegisterByDocumentNumber(0x22));
            // Contract response fixture, not an assertion that any database transaction committed/rolled back.
            return Task.FromResult(new PickCommitReceipt(PickCommitState.Unknown, actual, validity, null, null,
                evidence.Correlation, evidence.ReservationReference, evidence.AssignmentDigest,
                evidence.DiagnosticEvidenceReferences, null, null, DateTimeOffset.UtcNow,
                Stopwatch.GetTimestamp(), request.Window, "ComponentInvalidReceipt"));
        }
        public Task<RequiredCommitEvidence> ReportPickEvidenceFailureAsync(PickEvidenceFailureNotice notice, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Test requires a real durable raw pick observation before invoking the invalid receipt fixture.");
        internal static void AssertNoPlace(IReadOnlyList<VirtualPlc.PcWriteAudit> writes)
        {
            var action = writes.Where(w => w.Area == PlcArea.HoldingRegister).ToArray();
            Assert.DoesNotContain(action, w => w.DocumentNumber == 0x20 || w.DocumentNumber == 0x21 && w.Value == 2 || w.DocumentNumber == 0x54);
            Assert.Equal(3, action.Count(w => w.DocumentNumber is 3 or 5 or 0xB));
        }
    }
}
