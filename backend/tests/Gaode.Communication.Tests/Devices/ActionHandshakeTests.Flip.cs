using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Devices.Plc;
using Gaode.Plc.Protocol;
using Xunit;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Text.Json;

namespace Gaode.Communication.Tests.Devices;

public sealed partial class ActionHandshakeTests
{
    private static RecipeTargetPose ComponentPose => new("component-program", "test/1", "pose-b");
    private static PlcPoseProgram[] ComponentPrograms => [new("case-sensitive-model", "component-program", "test/1", "pose-b", 23,
        Enumerable.Range(101, 16).Select(v => (ushort)v).ToArray(), "Explicit protocol component words; not ASCII or production approval", "Test")];

    private static async Task<(DeviceActionEvidence Position, FlipRequest Request)> PreparedFlip(
        LatestProtocolPlcDevice device, CancellationToken token)
    {
        var transition = Guid.NewGuid();
        var preparation = new FlipMovePreparation(transition, "case-sensitive-model", ComponentPose, "component-entity", 3);
        var moved = await MoveTransition(device, Guid.NewGuid(), "FlipPick", new("pick", "test/1", 11, 22, "mm", "SIM_MACHINE", 33), preparation, token);
        var correlation = moved.Correlation with { ActionId = Guid.NewGuid(), OperationId = Guid.NewGuid(),
            TrayId = Guid.NewGuid(), ObjectId = preparation.PhysicalEntityId, PhysicalSlotIndex = 3, PlanRevision = "component-plan" };
        return (moved, new(correlation, transition, preparation.Model, preparation.TargetPose,
            Assert.Single(moved.Positions), Window(ProtocolTcpFixture.Envelope(5000)), Guid.NewGuid()));
    }

    private static async Task<DeviceActionEvidence> MoveTransition(LatestProtocolPlcDevice device, Guid runId,
        string role, FixedPoint point, FlipMovePreparation? preparation, CancellationToken token)
    {
        var complete = new TaskCompletionSource<DeviceEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        var envelope = ProtocolTcpFixture.Envelope() with { RunId = runId };
        await device.RequestMoveAsync(new(envelope, Guid.NewGuid(), point, Guid.NewGuid(), "component", role)
            { FlipPreparation = preparation }, e =>
            { if (e.Kind is DeviceEventKind.Completed or DeviceEventKind.Failed or DeviceEventKind.UnknownHeld) complete.TrySetResult(e); }, token);
        var actual = await complete.Task.WaitAsync(token);
        Assert.True(actual.Kind == DeviceEventKind.Completed, actual.ErrorCode ?? device.Failure);
        return Assert.IsType<DeviceActionEvidence>(actual.Evidence);
    }

    [Fact]
    public async Task FreshFlipAndPutBackUseSeparateFeedbackAndPreparedProgram()
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var plc = new ProtocolTcpFixture(150); await plc.StartAsync(watchdog.Token);
        await using var proxy = new StageFeedbackFaultProxy(plc.Port, StageFeedbackFault.OldFlipCompleted); proxy.Start();
        await using var device = plc.Device(communicationPort: proxy.Port, posePrograms: ComponentPrograms);
        var captureStartedUtc = DateTimeOffset.UtcNow; var captureStartedTick = Stopwatch.GetTimestamp();
        try
        {
        await StartAndPrepareAsync(device, watchdog.Token);
        var (position, request) = await PreparedFlip(device, watchdog.Token);
        var writes = plc.Store.GetWriteAudit();
        var programWrite = Assert.Single(writes, w => w.Accepted && w.Area == PlcArea.HoldingRegister && w.DocumentNumber == 98);
        var faceWrite = Assert.Single(writes, w => w.Accepted && w.Area == PlcArea.HoldingRegister && w.DocumentNumber == 21);
        var firstAxis = writes.First(w => w.Accepted && w.Area == PlcArea.Coil && w.DocumentNumber is 34 or 35 && w.Value == 1);
        Assert.True(programWrite.Sequence < firstAxis.Sequence && faceWrite.Sequence < firstAxis.Sequence);
        Assert.Equal((ushort)23, faceWrite.Value);
        Assert.Null(Assert.Single(position.Positions).Actual.ActualZ);
        var flip = await device.FlipAsync(request, watchdog.Token);
        Assert.Equal(DeviceCompletionMeaning.FlipCompleted, flip.Meaning); Assert.True(flip.IsCorrelated);
        Assert.Equal(request.TransitionId, flip.TransitionId); Assert.Null(flip.Face);
        Assert.Equal((ushort)0, plc.Store.ReadHoldingRegisterByDocumentNumber(97));
        var placed = await MoveTransition(device, request.Correlation.RunId, "FlipPutBack",
            new("put", "test/1", 14, 28, "mm", "SIM_MACHINE", 45), null, watchdog.Token);
        var put = new PutBackRequest(request.Correlation with { ActionId = Guid.NewGuid(), OperationId = Guid.NewGuid() },
            request.TransitionId, Assert.Single(placed.Positions), Window(ProtocolTcpFixture.Envelope()), Guid.NewGuid());
        var completed = await device.PutBackAsync(put, watchdog.Token);
        Assert.Equal(DeviceCompletionMeaning.PutBackCompleted, completed.Meaning); Assert.True(completed.IsCorrelated);
        Assert.Equal(request.TransitionId, completed.TransitionId); Assert.Null(completed.Face);
        Assert.Equal(new ushort[] { 1, 2, 0 }, plc.Store.GetWriteAudit().Where(w => w.Accepted && w.Area == PlcArea.HoldingRegister && w.DocumentNumber == 96).Select(w => w.Value));
        Assert.DoesNotContain(plc.Store.GetWriteAudit(), w => w.Accepted && w.Area == PlcArea.HoldingRegister && w.DocumentNumber == 85);
        Assert.DoesNotContain(plc.Store.GetWriteAudit(),w=>w.Accepted&&w.Area==PlcArea.HoldingRegister&&
            w.DocumentNumber==plc.Store.Definition[SignalId.GrabId].DocumentNumber);
        Assert.Empty(proxy.MutatedReplies);
        Assert.NotEmpty(proxy.FlipWireReplies);
        Assert.All(proxy.FlipWireReplies, r => { Assert.False(r.ControlledFault); Assert.Equal(r.Before, r.After); });
        Assert.Equal(new ushort[] { 1, 2, 0 }, proxy.FlipWireRequests.Where(IsFlipWrite)
            .Select(r => BinaryPrimitives.ReadUInt16BigEndian(Convert.FromHexString(r.Frame).AsSpan(10, 2))));
        WriteFlipWireProof("positive", new { controlledFault = false, request.Correlation,
            flip = flip.Meaning.ToString(), putBack = completed.Meaning.ToString(),
            requests = proxy.FlipWireRequests.ToArray(), replies = proxy.FlipWireReplies.ToArray() });
        }
        finally
        {
            SaveComponentDiagnostic(plc, device, "I-FU-01", captureStartedUtc, captureStartedTick,
                new { requests = proxy.FlipWireRequests.ToArray(), replies = proxy.FlipWireReplies.ToArray() });
        }
    }

    [Fact]
    public async Task MissingProgramCannotMoveToPick()
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var plc = new ProtocolTcpFixture(150); await plc.StartAsync(watchdog.Token);
        await using var device = plc.Device(); await StartAndPrepareAsync(device, watchdog.Token);
        var before = plc.Store.GetWriteAudit().Last().Sequence;
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => PreparedFlip(device, watchdog.Token));
        Assert.Equal("PlcPoseProgramMappingMissing", failure.Message);
        Assert.DoesNotContain(plc.Store.GetWriteAudit().Where(w => w.Sequence > before), w =>
            w.Accepted && (w.Area == PlcArea.Coil && w.DocumentNumber is 34 or 35 ||
            w.Area == PlcArea.HoldingRegister && w.DocumentNumber is 3 or 5 or 21 or 98));
    }

    [Theory]
    [InlineData("old-complete")]
    [InlineData("stale-epoch")]
    [InlineData("wrong-entity")]
    public async Task UnrelatedOrOldFeedbackCannotAuthorizeFlip(string mismatch)
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var plc = new ProtocolTcpFixture(150); await plc.StartAsync(watchdog.Token);
        await using var proxy = new StageFeedbackFaultProxy(plc.Port, StageFeedbackFault.OldFlipCompleted); proxy.Start();
        var device = plc.Device(communicationPort: proxy.Port, posePrograms: ComponentPrograms); await StartAndPrepareAsync(device, watchdog.Token);
        var (_, request) = await PreparedFlip(device, watchdog.Token);
        if (mismatch == "stale-epoch") request = request with { Correlation = request.Correlation with { ConnectionEpoch = request.Correlation.ConnectionEpoch + 1 } };
        if (mismatch == "wrong-entity") request = request with { Correlation = request.Correlation with { ObjectId = "unrelated-entity" } };
        proxy.Armed = mismatch == "old-complete";
        var before = plc.Store.GetWriteAudit().Last().Sequence;
        var callTick = Stopwatch.GetTimestamp();
        Exception? failure;
        try { failure = await Record.ExceptionAsync(() => device.FlipAsync(request, watchdog.Token)); }
        finally { await device.DisposeAsync(); }
        Assert.NotNull(failure);
        Assert.Equal(mismatch switch { "old-complete" => "TransitionFeedbackNotFresh",
            "stale-epoch" => "FlipIdentityInvalid", _ => "FlipPreparedProgramIdentityMismatch" }, failure.Message);
        if (mismatch == "old-complete") Assert.IsType<IOException>(failure);
        Assert.DoesNotContain(proxy.FlipWireRequests.Where(r => r.ReceivedTick >= callTick), IsFlipWrite);
        Assert.DoesNotContain(plc.Store.GetWriteAudit().Where(w => w.Sequence > before), w => w.Accepted && w.Area == PlcArea.HoldingRegister && w.DocumentNumber == 96);
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<Gaode.Infrastructure.Persistence.Station01DbContext>();
        Microsoft.EntityFrameworkCore.SqliteDbContextOptionsBuilderExtensions.UseSqlite(options, $"Data Source={plc.EvidenceStorePath};Mode=ReadOnly;Pooling=False");
        using var db = new Gaode.Infrastructure.Persistence.Station01DbContext(options.Options);
        var rows = db.PlcCommunicationEvidence.Where(e => e.ActionId == request.Correlation.ActionId).ToArray();
        var payloads = rows.Select(r => JsonDocument.Parse(r.RawPayloadJson).RootElement.Clone()).ToArray();
        Assert.DoesNotContain(payloads, p => p.GetProperty("interpretation").GetString() == "FlipCompleted");
        var injected = proxy.FlipWireReplies.Where(r => r.ControlledFault && r.ForwardAttemptTick >= callTick).ToArray();
        if (mismatch == "old-complete")
        {
            Assert.NotEmpty(injected);
            Assert.NotEmpty(payloads);
            foreach (var reply in injected)
            {
                var original = Convert.FromHexString(reply.Before); var changed = Convert.FromHexString(reply.After);
                var query = Convert.FromHexString(reply.Request);
                var index = 9 + (21 - BinaryPrimitives.ReadUInt16BigEndian(query.AsSpan(8, 2))) * 2;
                Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16BigEndian(original.AsSpan(index, 2)));
                Assert.Equal((ushort)2, BinaryPrimitives.ReadUInt16BigEndian(changed.AsSpan(index, 2)));
                BinaryPrimitives.WriteUInt16BigEndian(original.AsSpan(index, 2), 2);
                Assert.Equal(original, changed); // Includes transaction, unit, lengths and every other field.
                Assert.Equal(reply.Transaction, BinaryPrimitives.ReadUInt16BigEndian(query));
                // The persisted production transport journal contains the actual controlled response bytes.
                Assert.Contains(rows, r => r.RawPayloadJson.Contains(reply.After, StringComparison.OrdinalIgnoreCase));
            }
        }
        else Assert.Empty(injected);
        WriteFlipWireProof(mismatch, new { label = "Controlled test fault; injected value is not a PLC fact",
            call = "LatestProtocolPlcDevice.FlipAsync", callTick, request.Correlation, error = failure.Message,
            evidenceStore = plc.EvidenceStorePath, evidenceRows = rows.Select(r => r.RawPayloadJson).ToArray(),
            requests = proxy.FlipWireRequests.ToArray(), replies = proxy.FlipWireReplies.ToArray(),
            flipDispatched = false, flipCompleted = false });
    }

    private static bool IsFlipWrite(FlipWireRequest entry)
    {
        var frame = Convert.FromHexString(entry.Frame);
        if (frame.Length < 12 || frame[7] is not (6 or 16)) return false;
        var offset = BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(8, 2));
        return frame[7] == 6 ? offset == 95 : offset <= 95 &&
            offset + BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(10, 2)) > 95;
    }

    private static void WriteFlipWireProof(string name, object proof)
    {
        var root = Environment.GetEnvironmentVariable("GAODE_009_EVIDENCE_ROOT");
        if (string.IsNullOrWhiteSpace(root)) return;
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, $"flip-input-{name}.json"), JsonSerializer.Serialize(proof, new JsonSerializerOptions { WriteIndented = true }));
    }

    [Fact]
    public async Task HeldFlipPersistsFailureWithoutDeclaringCompletion()
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var plc = new ProtocolTcpFixture(150); await plc.StartAsync(watchdog.Token);
        var device = plc.Device(posePrograms: ComponentPrograms); await StartAndPrepareAsync(device, watchdog.Token);
        var (_, request) = await PreparedFlip(device, watchdog.Token);
        plc.Engine.InjectFault(VirtualPlc.SimulationFault.FlipFeedbackHold);
        try { await Assert.ThrowsAnyAsync<Exception>(() => device.FlipAsync(request, watchdog.Token)); }
        finally { await device.DisposeAsync(); }
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<Gaode.Infrastructure.Persistence.Station01DbContext>();
        Microsoft.EntityFrameworkCore.SqliteDbContextOptionsBuilderExtensions.UseSqlite(options, $"Data Source={plc.EvidenceStorePath};Mode=ReadOnly;Pooling=False");
        using var db = new Gaode.Infrastructure.Persistence.Station01DbContext(options.Options);
        var rows = db.PlcCommunicationEvidence.Where(e => e.ActionId == request.Correlation.ActionId).ToArray();
        Assert.NotEmpty(rows);
        Assert.All(rows, row => { Assert.Equal(request.Correlation.RunId, row.RunId); Assert.Equal(request.Correlation.OperationId, row.OperationId); });
        var payloads = rows.Select(r => System.Text.Json.JsonDocument.Parse(r.RawPayloadJson).RootElement.Clone()).ToArray();
        var failure = Assert.Single(payloads, p => p.GetProperty("interpretation").GetString()!.StartsWith("FailureWindow:"));
        Assert.All(payloads, p => Assert.False(p.GetProperty("gap").GetBoolean()));
        Assert.NotEmpty(failure.GetProperty("exchanges").EnumerateArray());
        Assert.DoesNotContain(payloads, p => p.GetProperty("interpretation").GetString() is "FlipCompleted" or "PutBackCompleted");
        Assert.DoesNotContain(plc.Store.GetWriteAudit(), w => w.Area == PlcArea.HoldingRegister && w.DocumentNumber == 96 && w.Value == 2);
    }
}
