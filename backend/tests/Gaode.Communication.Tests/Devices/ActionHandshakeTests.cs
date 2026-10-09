using System.Diagnostics;
using Gaode.Application.Ports;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Devices.Plc;
using Xunit;

namespace Gaode.Communication.Tests.Devices;

[Collection("CommunicationTcp")]
public sealed partial class ActionHandshakeTests
{
    [Fact]
    public async Task RemovedUnlockStageCannotDispatchLegacyDeviceCommands()
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var plc = new ProtocolTcpFixture(motionDurationMs: 150);
        await plc.StartAsync(watchdog.Token);
        await using var device = plc.Device();
        await StartAndPrepareAsync(device, watchdog.Token);
        var request = StageRequest(device, PlcWorkflowStage.UnlockObservation) with { WholeTrayCompletionId = Guid.NewGuid() };
        var before = plc.Store.GetWriteAudit().Last().Sequence;
        var adapter = new LatestProtocolStageActionAdapter(device, ComponentBudget(),
            new RejectingPickPort(plc, request, ActualCommitState.Unknown, ReceiptValidity.None));
        await Assert.ThrowsAsync<ArgumentException>(() => adapter.ExecuteAsync(request, watchdog.Token).AsTask());
        Assert.DoesNotContain(plc.Store.GetWriteAudit().Where(w => w.Sequence > before),
            w => w.Area == Gaode.Plc.Protocol.PlcArea.HoldingRegister);
    }

    [Theory]
    [InlineData("WIRE-CAPTURE/3d-release", "3D", false)]
    [InlineData("WIRE-CAPTURE/f-release", "F", false)]
    [InlineData("WIRE-CAPTURE/e-release", "E", false)]
    [InlineData("WIRE-CAPTURE/detection-release", "Detection", false)]
    [InlineData("WIRE-CAPTURE/f-observation-lost", "F", true)]
    public async Task FormalCaptureUsesCurrentMoveAndCommittedRelease(string caseId, string role, bool loseObservation)
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var plc = new ProtocolTcpFixture(motionDurationMs: 150);
        await plc.StartAsync(watchdog.Token);
        await using var device = plc.Device();
        await StartAndPrepareAsync(device, watchdog.Token);
        var target = new FixedPoint(role, "1", 11, 22, "mm", "SIM_MACHINE", 33);
        var evidence = await MoveAsync(device, role, target, watchdog.Token);
        var position = Assert.Single(evidence.Positions);
        Assert.True(position.Matched);
        Assert.Equal(target, position.Target);
        var scanning = role is "F" or "E";
        Assert.Equal(role == "3D" ? "XY" : scanning ? "ScanZ" : "DetectionZ", position.Actual.AxisPurpose);
        var axes = plc.Engine.GetActionAudit().Actions.Where(a => a.Kind == "AxisMove" && a.Phase == "completed").ToArray();
        Assert.Equal(role == "3D" ? new[] { "X", "Y" } : new[] { "X", "Y", scanning ? "ScanZ" : "DetectionZ" }, axes.Select(a => a.AxisRole));
        var request = new CaptureWindowRequest(evidence.Correlation,
            role switch { "F" => CaptureRole.F, "E" => CaptureRole.E,
                "Detection" => CaptureRole.Detection, _ => CaptureRole.ThreeD }, target, position,
            Window(ProtocolTcpFixture.Envelope()));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(async () => await device.OpenCaptureWindowAsync(
            request with { Correlation = request.Correlation with { OperationId = Guid.NewGuid() } }, watchdog.Token));
        var beforeCapture = plc.Store.GetWriteAudit().Last().Sequence;
        var session = await device.OpenCaptureWindowAsync(request, watchdog.Token);
        Assert.Equal(AcquisitionState.CaptureAllowed, session.State);
        Assert.True(session.Evidence.IsCorrelated);
        // Build the typed completion from real media/SQLite; the fixture rejects invalid save receipts.
        using var saved = await CaptureWorkFixture.CreateAsync(session);
        var work = saved.Work;
        foreach (var invalid in new[] { session with { SessionId = Guid.NewGuid() },
            session with { Request = request with { Correlation = request.Correlation with { OperationId = Guid.NewGuid() } } } })
            await Assert.ThrowsAsync<InvalidOperationException>(() => device.FinishCaptureWindowAsync(invalid, work, request.Window, watchdog.Token));
        if (loseObservation)
        {
            await plc.Server.StopAsync(watchdog.Token);
            await ProtocolTcpFixture.UntilAsync(() => !device.Observe().HasReliableObservation, watchdog.Token);
        }
        var released = await device.FinishCaptureWindowAsync(session, work, request.Window, watchdog.Token);
        if (loseObservation)
        {
            Assert.Equal(AcquisitionState.HeldUnknown, released.State);
            Assert.Null(released.Evidence);
            Assert.NotEmpty(released.FailureReason!);
        }
        else
        {
            Assert.True(released.State == AcquisitionState.Released, released.FailureReason);
            Assert.True(released.Evidence!.IsCorrelated);
            Assert.All(session.Evidence.DiagnosticEvidenceReferences,
                r => Assert.Contains(r, released.Evidence.DiagnosticEvidenceReferences));
        }
        Assert.DoesNotContain(plc.Store.GetWriteAudit().Where(w => w.Sequence > beforeCapture),
            w => w.Area == Gaode.Plc.Protocol.PlcArea.HoldingRegister && w.DocumentNumber is 1 or 0x52 or 0x53);
        Assert.StartsWith("WIRE-CAPTURE/", caseId);
    }

    [Fact]
    public async Task CaptureReleaseKeepsCommittedOpeningAndSeparateClosingEvidenceAfterRoutinePollBufferWrap()
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await using var plc = new ProtocolTcpFixture();
        await plc.StartAsync(watchdog.Token);
        await using var device = plc.Device();
        await StartAndPrepareAsync(device, watchdog.Token);
        var target = new FixedPoint("F", "1", 11, 22, "mm", "SIM_MACHINE", 33);
        var moved = await MoveAsync(device, "F", target, watchdog.Token);
        var request = new CaptureWindowRequest(moved.Correlation, CaptureRole.F, target,
            Assert.Single(moved.Positions), Window(ProtocolTcpFixture.Envelope(50000)));
        var session = await device.OpenCaptureWindowAsync(request, watchdog.Token);
        var lastOpening = device.BusinessExchanges.Last().Sequence;
        // Controlled component traffic wraps the real production journal; normal 013
        // collection is deliberately too sparse to imply a wrap within this window.
        // This is never enabled in the performance representative route.
        for (var index = 0; index < 8193; index++)
            await device.StageSignals.ReadAsync([Gaode.Plc.Protocol.SignalId.XPosConfirmed], watchdog.Token);
        Assert.True(device.BusinessExchanges.Last().Sequence > lastOpening + 8192);
        Assert.True(device.HeartbeatEdges > 0);
        using var saved = await CaptureWorkFixture.CreateAsync(session);
        var release = await device.FinishCaptureWindowAsync(session, saved.Work, request.Window, watchdog.Token);
        Assert.True(release.State == AcquisitionState.Released, release.FailureReason);
        Assert.All(session.Evidence.DiagnosticEvidenceReferences, r => Assert.Contains(r, release.Evidence!.DiagnosticEvidenceReferences));
        var refs = release.Evidence!.DiagnosticEvidenceReferences;
        var ending = Assert.Single(refs.Except(session.Evidence.DiagnosticEvidenceReferences));
        await using var db = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={plc.EvidenceStorePath};Mode=ReadOnly");
        await db.OpenAsync(watchdog.Token);
        using var query = db.CreateCommand();
        query.CommandText = "SELECT RawPayloadJson FROM PlcCommunicationEvidence WHERE EvidenceId=$id";
        query.Parameters.AddWithValue("$id", ending.EvidenceId.ToString().ToUpperInvariant());
        using var payload = System.Text.Json.JsonDocument.Parse(Assert.IsType<string>(await query.ExecuteScalarAsync(watchdog.Token)));
        Assert.Equal("CaptureReleaseSegmentAfterCommittedOpening", payload.RootElement.GetProperty("captureScope").GetString());
        Assert.False(payload.RootElement.GetProperty("gap").GetBoolean());
        Assert.NotEmpty(payload.RootElement.GetProperty("precedingEvidenceReferences").EnumerateArray());
        var changes = plc.Store.GetWriteAudit().Where(x => x.Area == Gaode.Plc.Protocol.PlcArea.HoldingRegister && x.DocumentNumber == 0x52).ToArray();
        Assert.Empty(changes); // Current capture has no legacy inspection ACK exchange.
    }

    [Fact]
    public async Task UnknownFeedbackNeverCompletesFormalMove()
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var plc = new ProtocolTcpFixture();
        await plc.StartAsync(watchdog.Token);
        await using var device = plc.Device();
        await StartAndPrepareAsync(device, watchdog.Token);
        plc.Engine.InjectFault(VirtualPlc.SimulationFault.MoveTimeout);
        var envelope = ProtocolTcpFixture.Envelope(1000);
        var events = new System.Collections.Concurrent.ConcurrentQueue<DeviceEvent>();
        await device.RequestMoveAsync(new(envelope, Guid.NewGuid(), new FixedPoint("Q", "1", 11, 22, "mm", "SIM_MACHINE", 33),
            Guid.NewGuid(), "component-binding"), events.Enqueue, watchdog.Token);
        await ProtocolTcpFixture.UntilAsync(() => plc.Store.GetWriteAudit().Any(w => w.Area == Gaode.Plc.Protocol.PlcArea.Coil && w.DocumentNumber == 0x22 && w.Value == 1), watchdog.Token);
        plc.Store.SetHoldingRegisterFromPlc(0x80, 65535); // explicit unknown PLC feedback, no synthesized success
        await ProtocolTcpFixture.UntilAsync(() => Stopwatch.GetTimestamp() >= envelope.DueTick, watchdog.Token);
        Assert.DoesNotContain(events, e => e.Kind == DeviceEventKind.Completed);
        Assert.Equal(1, plc.Store.GetWriteAudit().Count(w => w.Area == Gaode.Plc.Protocol.PlcArea.Coil && w.DocumentNumber == 0x22 && w.Value == 1));
    }
    internal static async Task StartAndPrepareAsync(LatestProtocolPlcDevice device, CancellationToken token)
    {
        await device.StartAsync(token);
        await device.ResetAsync(token);
        await ProtocolTcpFixture.UntilAsync(() => device.Observe().HasReliableObservation, token);
        var accepted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await device.RequestStartAsync(ProtocolTcpFixture.Envelope(), Guid.NewGuid(), Guid.NewGuid(),
            e => {
                if (e.Kind == DeviceEventKind.Accepted) accepted.TrySetResult();
                else if (e.Kind is DeviceEventKind.Failed or DeviceEventKind.UnknownHeld)
                    accepted.TrySetException(new InvalidOperationException("StartupRejected:" + e.ErrorCode + ":" + device.Failure));
            }, token);
        try { await accepted.Task.WaitAsync(token); }
        catch (Exception error) { throw new InvalidOperationException("StartupNotAccepted:" + device.Failure + ":" +
            System.Text.Json.JsonSerializer.Serialize(device.Observe()), error); }
        await ProtocolTcpFixture.UntilAsync(() => device.Observe().HasReliableObservation &&
            device.Observe().Readiness == DeviceReadiness.Ready, token);
    }
    internal static async Task<DeviceActionEvidence> MoveAsync(LatestProtocolPlcDevice device, string role,
        FixedPoint target, CancellationToken token)
    {
        var envelope = ProtocolTcpFixture.Envelope();
        var completion = new TaskCompletionSource<DeviceEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        await device.RequestMoveAsync(new(envelope, Guid.NewGuid(), target, Guid.NewGuid(), "component-binding", role),
            e => { if (e.Kind is DeviceEventKind.Completed or DeviceEventKind.Failed) completion.TrySetResult(e); }, token);
        var result = await completion.Task.WaitAsync(token);
        Assert.True(result.Kind == DeviceEventKind.Completed, System.Text.Json.JsonSerializer.Serialize(new { result, observation = device.Observe(), device.Failure }));
        var evidence = Assert.IsType<DeviceActionEvidence>(result.Evidence);
        Assert.True(evidence.IsCorrelated);
        return evidence;
    }
    internal static ActionWindow Window(PortEnvelope envelope)
    {
        var now = DateTimeOffset.UtcNow;
        var tick = Stopwatch.GetTimestamp();
        return new(envelope.StartTick, envelope.DueTick, envelope.ClockId,
            now.AddSeconds((envelope.StartTick - tick) / (double)Stopwatch.Frequency),
            now.AddSeconds((envelope.DueTick - tick) / (double)Stopwatch.Frequency));
    }
}
