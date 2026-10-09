using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Application.Workflow;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Devices.Plc;
using Gaode.Infrastructure.Persistence;
using Gaode.Plc.Protocol;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Gaode.Communication.Tests.Devices;

public sealed partial class ActionHandshakeTests
{
    [Fact]
    public Task SortingPickPlaceRequiresRealInTransitCommitAndFinalLift() => VerifyTransferAsync();

    [Fact]
    public async Task MissingGrabSafetyConfigurationCannotStartPick()
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var plc = new ProtocolTcpFixture(motionDurationMs: 150);
        await plc.StartAsync(watchdog.Token);
        await using var device = plc.Device(includeSortingSafetyPosition: false);
        await StartAndPrepareAsync(device, watchdog.Token);
        var request = StageRequest(device, PlcWorkflowStage.Sorting);
        var before = plc.Store.GetWriteAudit().Last().Sequence;
        var gate = new RejectingPickPort(plc, request, ActualCommitState.Unknown, ReceiptValidity.None);
        var result = await new LatestProtocolStageActionAdapter(device, ComponentBudget(), gate)
            .ExecuteAsync(request, watchdog.Token);
        Assert.False(result.IsCompleted);
        Assert.Equal(0, gate.Calls);
        Assert.DoesNotContain(plc.Store.GetWriteAudit().Where(w => w.Sequence > before),
            w => w.Area == PlcArea.HoldingRegister && w.DocumentNumber is 3 or 5 or 0xB or 0x21);
    }

    private static async Task VerifyTransferAsync()
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var plc = new ProtocolTcpFixture(motionDurationMs: 150);
        await plc.StartAsync(watchdog.Token);
        await using var device = plc.Device();
        await StartAndPrepareAsync(device, watchdog.Token);
        var request = StageRequest(device, PlcWorkflowStage.Sorting);
        var (updated, gate) = await CommittedPickProbe.PrepareAsync(plc, request, watchdog.Token);
        request = updated;
        var before = plc.Store.GetWriteAudit().Last().Sequence;
        var adapter = new LatestProtocolStageActionAdapter(device, ComponentBudget(), gate);
        var result = await adapter.ExecuteAsync(request, watchdog.Token);
        Assert.True(result.IsCompleted, JsonSerializer.Serialize(result));
        Assert.Equal(DeviceCompletionMeaning.MaterialTransferred, result.Evidence!.Meaning);
        Assert.All(result.Evidence.Positions, p => Assert.True(p.Matched));
        Assert.Equal(2, result.Evidence.Positions.Count);
        Assert.Equal(2, result.Evidence.DiagnosticEvidenceReferences.Count);
        await using (var reopened = new Station01DbContext(new DbContextOptionsBuilder<Station01DbContext>()
            .UseSqlite($"Data Source={plc.EvidenceStorePath};Pooling=False").Options))
        {
            var batches = await reopened.PlcCommunicationEvidence.AsNoTracking()
                .Where(e => e.ActionId == request.Correlation.ActionId).ToArrayAsync(watchdog.Token);
            Assert.Equal(2, batches.Length);
            var raw = batches.Select(b => JsonSerializer.Deserialize<CommunicationEvidenceBatch>(b.RawPayloadJson,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!).ToArray();
            var pick = Assert.Single(raw, b => b.Interpretation == "MaterialPicked");
            var placedBatch = Assert.Single(raw, b => b.Interpretation == "MaterialTransferred");
            Assert.All(raw, b => Assert.False(b.Gap));
            Assert.Empty(pick.PrecedingEvidenceReferences);
            Assert.Equal("PlaceContinuationAfterCommittedPick", placedBatch.CaptureScope);
            Assert.Equal(pick.EvidenceId, Assert.Single(placedBatch.PrecedingEvidenceReferences).EvidenceId);
            Assert.All(batches, b => Assert.Contains(result.Evidence.DiagnosticEvidenceReferences,
                r => r.EvidenceId == b.EvidenceId && r.StoreId == b.StoreId));
        }
        Assert.Equal(150d, gate.ActualPickZ);
        Assert.Equal(150d, result.Evidence.Positions[0].Actual.ActualZ);
        Assert.Equal(1, gate.Calls);
        Assert.NotNull(gate.CommittedEventId);
        var writes = plc.Store.GetWriteAudit().Where(w => w.Sequence > before && w.Area == PlcArea.HoldingRegister && w.Accepted).ToArray();
        Assert.Equal(new (int, ushort)[] { (0x21, 1), (0x21, 2), (0x21, 0) },
            writes.Where(w => w.DocumentNumber is 0x20 or 0x21 or 0x54).Select(w => (w.DocumentNumber, w.Value)));
        Assert.Equal(new[] { 3, 5, 0xB, 0xB, 3, 5, 0xB, 0xB },
            writes.Where(w => w.DocumentNumber is 3 or 5 or 0xB).Select(w => w.DocumentNumber));
        Assert.DoesNotContain(writes, w => w.DocumentNumber == 1); // No old combined-motion command.
        Assert.Equal((ushort)2, plc.Store.ReadHoldingRegisterByDocumentNumber(0x22));
        var place = Assert.Single(writes, w => w.DocumentNumber == 0x21 && w.Value == 2);
        var grabWrites = writes.Where(w => w.DocumentNumber == 0xB).ToArray();
        Assert.Equal(4, grabWrites.Length);
        Assert.True(grabWrites[1].Sequence > gate.LastWriteBeforeReceipt); // No lift before actual commit receipt.
        Assert.True(place.Sequence > gate.LastWriteBeforeReceipt);
        Assert.True(grabWrites[3].Sequence > place.Sequence);
        Assert.Equal(200f, Float32Codec.Decode(plc.Store.ReadHoldingRegisterByDocumentNumber(0x87),
            plc.Store.ReadHoldingRegisterByDocumentNumber(0x88), Float32ByteOrder.Abcd));
        Assert.Equal(0f, Float32Codec.Decode(plc.Store.ReadHoldingRegisterByDocumentNumber(0x11),
            plc.Store.ReadHoldingRegisterByDocumentNumber(0x12), Float32ByteOrder.Abcd));
        // This is same-process actual TCP + reopened SQLite, not independent Host/Worker acceptance.
        await gate.Allocator.RecordOccupiedAsync(request, result, watchdog.Token);
        var facts = await gate.Store.ReadAsync(request.RunId, request.TrayId, WholeTrayWorkflowStage.Sorting, watchdog.Token);
        Assert.Single(facts, x => x.PayloadJson.Contains("SortingAssignmentInTransit", StringComparison.Ordinal));
        Assert.Single(facts, x => x.PayloadJson.Contains("SortingAssignmentOccupied", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SortingMotionCannotConsumeTheLongerTrayDeadline()
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var plc = new ProtocolTcpFixture();
        await plc.StartAsync(watchdog.Token);
        await using var device = plc.Device();
        await StartAndPrepareAsync(device, watchdog.Token);
        var request = StageRequest(device, PlcWorkflowStage.Sorting);
        // A component motion budget shorter than the actual declared axis duration.
        // The recipe application budget remains the confirmed 10000 ms.
        var gate = new RejectingPickPort(plc, request, ActualCommitState.Unknown, ReceiptValidity.None);
        var adapter = new LatestProtocolStageActionAdapter(device, ComponentBudget() with { XyCompletion = 40 }, gate);
        var result = await adapter.ExecuteAsync(request, watchdog.Token);
        Assert.True(result.IsUnknownHeld);
        Assert.False(result.CanRetry);
        Assert.True(request.Window.Contains(Stopwatch.GetTimestamp()));
        Assert.Equal(0, gate.Calls);
        var writes = plc.Store.GetWriteAudit().Where(w => w.Area == PlcArea.HoldingRegister).ToArray();
        Assert.Equal(new[] { 3, 5 }, writes.Where(w => w.DocumentNumber is 3 or 5).Select(w => w.DocumentNumber));
        Assert.DoesNotContain(writes, w => w.DocumentNumber is 0xB or 0x20 or 0x21 or 0x54);
    }

    [Theory]
    [InlineData("WIRE-UNLOAD/detection-z", 150)]
    [InlineData("WIRE-UNLOAD/different-z", 90)]
    public async Task UnloadConfirmsFreshXyWithoutMovingAnyZ(string caseId, int initialZ)
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var plc = new ProtocolTcpFixture(motionDurationMs: 150);
        await plc.StartAsync(watchdog.Token);
        await using var device = plc.Device();
        await StartAndPrepareAsync(device, watchdog.Token);
        plc.Store.SetFloatFromPlc(0x11, initialZ);
        plc.Store.SetFloatFromPlc(0x85, 42);
        plc.Store.SetFloatFromPlc(0x87, 99);
        var request = StageRequest(device, PlcWorkflowStage.UnloadPreparation);
        var before = plc.Store.GetWriteAudit().Last().Sequence;
        var adapter = new LatestProtocolStageActionAdapter(device, ComponentBudget(),
            new RejectingPickPort(plc, request, ActualCommitState.Unknown, ReceiptValidity.None));
        var result = await adapter.ExecuteAsync(request, watchdog.Token);
        Assert.StartsWith("WIRE-UNLOAD/", caseId);
        Assert.True(result.IsCompleted, JsonSerializer.Serialize(result) + plc.DeviceDiagnostics);
        Assert.Equal(DeviceCompletionMeaning.UnloadPrepared, result.Evidence!.Meaning);
        var position = Assert.Single(result.Evidence.Positions);
        Assert.True(position.Matched);
        Assert.Equal("XY", position.Actual.AxisPurpose);
        Assert.Null(position.Actual.ActualZ);
        var writes = plc.Store.GetWriteAudit().Where(w => w.Sequence > before && w.Accepted).ToArray();
        Assert.Equal(new[] { 3, 5 }, writes.Where(w => w.Area == PlcArea.HoldingRegister)
            .Select(w => w.DocumentNumber));
        Assert.DoesNotContain(writes, w => w.Area == PlcArea.Coil && w.DocumentNumber is 0x24 or 0x25 or 0x26);
        Assert.Equal(initialZ, plc.Store.ReadActualFloat(0x11));
        Assert.Equal(42f, plc.Store.ReadActualFloat(0x85));
        Assert.Equal(99f, plc.Store.ReadActualFloat(0x87));
    }

    [Fact]
    public async Task DisconnectAfterPickDispatchIsUnknownHeldAndNeverReplays()
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var plc = new ProtocolTcpFixture();
        await plc.StartAsync(watchdog.Token);
        await using var device = plc.Device();
        await StartAndPrepareAsync(device, watchdog.Token);
        var request = StageRequest(device, PlcWorkflowStage.Sorting);
        var dispatched = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        plc.Store.PcValueWritten += w => { if (w.Area == PlcArea.HoldingRegister && w.DocumentNumber == 0x21 && w.Value == 1) dispatched.TrySetResult(); };
        var adapter = new LatestProtocolStageActionAdapter(device, ComponentBudget(), new RejectingPickPort(plc, request, ActualCommitState.Unknown, ReceiptValidity.None));
        var pending = adapter.ExecuteAsync(request, watchdog.Token).AsTask();
        await dispatched.Task.WaitAsync(watchdog.Token);
        await plc.Server.StopAsync(watchdog.Token);
        var result = await pending.WaitAsync(watchdog.Token);
        Assert.True(result.IsUnknownHeld);
        Assert.False(result.CanRetry);
        Assert.Equal(1, plc.Store.GetWriteAudit().Count(w => w.Area == PlcArea.HoldingRegister && w.DocumentNumber == 0x21 && w.Value == 1));
        RejectingPickPort.AssertNoPlace(plc.Store.GetWriteAudit());
    }

    private static async Task PrepareUnloadAsync(LatestProtocolPlcDevice device, CancellationToken token)
    {
        var target = Point("F", 10, 20);
        var move = await MoveAsync(device, "F", target, token);
        var envelope = ProtocolTcpFixture.Envelope();
        var request = new CaptureWindowRequest(move.Correlation, CaptureRole.F, target, Assert.Single(move.Positions), Window(envelope));
        var session = await device.OpenCaptureWindowAsync(request, token);
        using var saved = await CaptureWorkFixture.CreateAsync(session);
        var released = await device.FinishCaptureWindowAsync(session, saved.Work, request.Window, token);
        Assert.Equal(AcquisitionState.Released, released.State);
    }

    private sealed class CommittedPickProbe(ProtocolTcpFixture plc, PlcStageActionRequest request, StageEventStore store, DbContextOptions<Station01DbContext> options) : IPickCommitPort
    {
        public StageEventStore Store => store;
        public SortingTargetAllocator Allocator { get; } = new(store, TimeProvider.System, 1000);
        public int Calls { get; private set; }
        public Guid? CommittedEventId { get; private set; }
        public long LastWriteBeforeReceipt { get; private set; }
        public double ActualPickZ { get; private set; }
        public static async Task<(PlcStageActionRequest, CommittedPickProbe)> PrepareAsync(ProtocolTcpFixture plc, PlcStageActionRequest request, CancellationToken ct)
        {
            var options = new DbContextOptionsBuilder<Station01DbContext>().UseSqlite($"Data Source={plc.EvidenceStorePath};Pooling=False").Options;
            await using (var writer = new TraceWriter(options, TimeProvider.System, 8))
            {
                var created = new RunCreatedPayload(Guid.NewGuid(), request.RunId.ToString(), "Test", "{}", "{}", "{}", "{}", request.ConfigSnapshotId, "p", "b", "s");
                var receipt = await writer.SubmitCritical(new(Guid.NewGuid(), request.RunId, 0, WriteKind.RunCreated,
                    JsonSerializer.Serialize(created, new JsonSerializerOptions(JsonSerializerDefaults.Web)), "created")).Completion;
                Assert.Equal(CommitState.Committed, receipt.State);
            }
            var store = new StageEventStore(options);
            var reservationId = Guid.NewGuid();
            var assignment = new SortingAssignment(request.OperationId, request.Correlation.ObjectId!, "P01", 7,
                request.SortingSource!, request.SortingTarget!, "NG", "component-detection", $"sorting-reservation://{reservationId:D}/{request.OperationId:D}");
            request = request with { ReservationReference = assignment.ReservationReference, ActionParametersDigest = SortingTargetAllocator.AssignmentDigest(assignment) };
            // Persisted test input through the actual store. Full reservation allocation is a separate business obligation.
            foreach (var (id, payload) in new[] { (reservationId, (object)new { kind = "SortingAssignmentsReserved", assignments = new[] { assignment } }),
                (Guid.NewGuid(), (object)new { kind = "SortingActionIntent", correlation = request.Correlation }) })
            {
                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                var saved = await store.AppendAsync(new(id, request.RunId, request.TrayId, request.StationId.ToString(), request.LineId.ToString(),
                    WholeTrayWorkflowStage.Sorting, request.OperationId, 1, request.ConnectionEpoch, StageEventType.IntentRecorded,
                    DateTimeOffset.UtcNow, ResultSource.Virtual, ResultQuality.Measured, null,
                    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))), json, id.ToString(), request.PlanRevision,
                    request.Window.StartedUtc, request.DeadlineUtc), ct);
                Assert.True(saved.IsCommitted);
            }
            return (request, new(plc, request, store, options));
        }
        public async Task<PickCommitReceipt> CommitPickAsync(PickCompletionEvidence evidence, CancellationToken ct)
        {
            Calls++;
            ActualPickZ = Float32Codec.Decode(plc.Store.ReadHoldingRegisterByDocumentNumber(0x87),
                plc.Store.ReadHoldingRegisterByDocumentNumber(0x88), Float32ByteOrder.Abcd);
            RejectingPickPort.AssertNoPlace(plc.Store.GetWriteAudit());
            var receipt = await Allocator.CommitPickAsync(evidence, ct);
            Assert.True(receipt.MayAuthorizePlace(evidence, Stopwatch.GetTimestamp()), JsonSerializer.Serialize(receipt));
            await using var reopened = new Station01DbContext(options);
            var row = await reopened.StageEvents.SingleAsync(x => x.EventId == receipt.EventId, ct);
            Assert.Contains("SortingAssignmentInTransit", row.PayloadJson);
            Assert.Equal(request.OperationId, row.OperationId);
            Assert.Equal(request.ConnectionEpoch, row.ConnectionEpoch);
            RejectingPickPort.AssertNoPlace(plc.Store.GetWriteAudit());
            CommittedEventId = row.EventId;
            LastWriteBeforeReceipt = plc.Store.GetWriteAudit().Last().Sequence;
            return receipt;
        }
        public Task<RequiredCommitEvidence> ReportPickEvidenceFailureAsync(PickEvidenceFailureNotice notice, CancellationToken ct) =>
            Allocator.ReportPickEvidenceFailureAsync(notice, ct);
    }
}
