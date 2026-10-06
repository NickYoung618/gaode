using System.Collections.Concurrent;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Persistence;
using Gaode.Infrastructure.Devices.Plc;
using Gaode.Plc.Protocol;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Gaode.Communication.Tests.Devices;

[Collection("CommunicationTcp")]
public sealed class FailureEvidenceTests
{
    [Fact]
    public async Task RecorderRejectsAnActualRawJournalGap()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var plc = new ProtocolTcpFixture(150); await plc.StartAsync(deadline.Token);
        await using var owner = plc.Device(); await using var wire = plc.Client();
        await wire.ReadRegistersAsync(0x7F, 5, deadline.Token);
        var envelope = ProtocolTcpFixture.Envelope();
        var correlation = new ActionCorrelation(envelope.RunId, envelope.OperationId, Guid.NewGuid(),
            envelope.Attempt, envelope.SessionId, 1, envelope.SnapshotId);
        var exchange = wire.Exchanges.Single();
        var receipt = await plc.EvidenceRecorder!.RecordAsync(correlation,
            new(Guid.NewGuid(), 1, exchange.StartedUtc, exchange.ObservedAtUtc, DeviceReliability.Reliable),
            new(DeviceProvider.Virtual, "013-gap-component", EvidenceQuality.Derived), ActionHandshakeTests.Window(envelope),
            wire.Exchanges, true, Float32ByteOrder.Abcd, "GapCannotAuthorizeCompletion", deadline.Token);
        Assert.Equal("CommunicationEvidenceGap", receipt.FailureReason);
        Assert.Null(receipt.Reference); Assert.NotEqual(ActualCommitState.Committed, receipt.ActualCommit);
        var options = new DbContextOptionsBuilder<Station01DbContext>().UseSqlite($"Data Source={plc.EvidenceStorePath};Mode=ReadOnly;Pooling=False").Options;
        await using var db = new Station01DbContext(options);
        Assert.Empty(await db.PlcCommunicationEvidence.ToArrayAsync(deadline.Token));
    }

    [Fact]
    public async Task AutomaticThresholdCheckpointPrecedesRealFailureContinuation()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var plc = new ProtocolTcpFixture(150);
        await plc.StartAsync(deadline.Token);
        // Install the real recorder/SQLite store without starting device polling.
        // This controlled component supplies real exchanges to the production evidence boundary.
        await using var owner = plc.Device();
        await using var business = plc.Client();
        await using var heartbeat = plc.Client();
        var envelope = ProtocolTcpFixture.Envelope(10000);
        var correlation = new ActionCorrelation(envelope.RunId, envelope.OperationId, Guid.NewGuid(),
            envelope.Attempt, envelope.SessionId, 1, envelope.SnapshotId);
        var now = DateTimeOffset.UtcNow;
        var window = new ActionWindow(envelope.StartTick, envelope.DueTick, envelope.ClockId, now, now.AddSeconds(10));
        var origin = new ExecutionOrigin(DeviceProvider.Virtual, "TCP/013-evidence-component", EvidenceQuality.Derived);
        ObservationIdentity ObserveActual()
        {
            var actual = business.Exchanges.Last();
            return new(Guid.NewGuid(), 1, actual.StartedUtc, actual.ObservedAtUtc, DeviceReliability.Reliable);
        }
        var recorder = plc.EvidenceRecorder!;
        for (var i = 0; i < 1023; i++) await business.ReadRegistersAsync(0x7F, 5, deadline.Token);
        Assert.Null(await TransitionEvidenceCapture.CheckpointAsync(business, heartbeat, recorder, (0, 0), null,
            correlation, ObserveActual, origin, window, Float32ByteOrder.Abcd, deadline.Token));
        var options = new DbContextOptionsBuilder<Station01DbContext>().UseSqlite($"Data Source={plc.EvidenceStorePath};Mode=ReadOnly;Pooling=False").Options;
        await using (var before = new Station01DbContext(options)) Assert.Empty(await before.PlcCommunicationEvidence.ToArrayAsync(deadline.Token));
        await business.ReadRegistersAsync(0x7F, 5, deadline.Token);
        var segment = Assert.IsType<TransitionEvidenceSegment>(await TransitionEvidenceCapture.CheckpointAsync(
            business, heartbeat, recorder, (0, 0), null, correlation, ObserveActual, origin, window, Float32ByteOrder.Abcd, deadline.Token));
        Assert.Equal(1024, segment.Business);
        Assert.Single(segment.References);
        // A genuinely rejected exchange after the committed checkpoint is the failure window.
        await Assert.ThrowsAnyAsync<IOException>(() => business.ReadRegistersAsync(ushort.MaxValue, 1, deadline.Token));
        var frozen = TransitionEvidenceCapture.Freeze(business, heartbeat, (segment.Business, segment.Heartbeat), segment.References);
        Assert.False(frozen.Gap);
        Assert.Contains(frozen.Exchanges, e => e.Error is not null);
        var receipt = await recorder.RecordFailureAsync(correlation, Guid.NewGuid(), 1, origin,
            frozen.Exchanges, [], frozen.Gap, Float32ByteOrder.Abcd, "ControlledTransportReadRejected", frozen.References);
        Assert.Equal(ActualCommitState.Committed, receipt.ActualCommit);
        await using var db = new Station01DbContext(options);
        var rows = await db.PlcCommunicationEvidence.Where(r => r.ActionId == correlation.ActionId).ToArrayAsync(deadline.Token);
        Assert.Equal(2, rows.Length);
        Assert.All(rows, r => { Assert.Equal(correlation.RunId, r.RunId); Assert.Equal(correlation.OperationId, r.OperationId); });
        var payloads = rows.Select(r => JsonDocument.Parse(r.RawPayloadJson).RootElement.Clone()).ToArray();
        var checkpoint = Assert.Single(payloads, p => p.GetProperty("interpretation").GetString() == "FlipCommunicationCheckpoint");
        Assert.Equal(1024, checkpoint.GetProperty("exchanges").GetArrayLength());
        var failure = Assert.Single(payloads, p => p.GetProperty("interpretation").GetString()!.StartsWith("FailureWindow:"));
        Assert.Single(failure.GetProperty("precedingEvidenceReferences").EnumerateArray());
        Assert.Contains(segment.References[0].EvidenceId.ToString(), failure.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(payloads, p => p.GetProperty("interpretation").GetString() is "FlipCompleted" or "PutBackCompleted");
        Assert.DoesNotContain(plc.Store.GetWriteAudit(), w => w.DocumentNumber == 96 && w.Value == 2);
    }

    [Theory]
    [InlineData("DIAG-COMPONENT/success")]
    [InlineData("DIAG-COMPONENT/unknown")]
    [InlineData("DIAG-COMPONENT/timeout")]
    public async Task ActualSuccessUnknownAndMissingResponseSurviveDeviceDisposal(string caseId)
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        await using var plc = new ProtocolTcpFixture();
        await plc.StartAsync(watchdog.Token);
        await using var proxy = new StageFeedbackFaultProxy(plc.Port, StageFeedbackFault.DropReadResponse);
        proxy.Start();
        var device = plc.Device(communicationPort: proxy.Port);
        var actionId = Guid.NewGuid();
        var kind = caseId.Split('/')[1];
        var envelope = ProtocolTcpFixture.Envelope(kind == "unknown" ? 1200 : 10000);
        var events = new ConcurrentQueue<DeviceEvent>();
        try
        {
            await ActionHandshakeTests.StartAndPrepareAsync(device, watchdog.Token);
            envelope = ProtocolTcpFixture.Envelope(kind == "unknown" ? 1200 : 10000);
            if (kind != "success") plc.Engine.InjectFault(VirtualPlc.SimulationFault.MoveTimeout);
            await device.RequestMoveAsync(new(envelope, actionId, new FixedPoint("Q", "1", 11, 22, "mm", "SIM_MACHINE", 33),
                Guid.NewGuid(), "diagnostic-component"), events.Enqueue, watchdog.Token);
            await ProtocolTcpFixture.UntilAsync(() => plc.Store.GetWriteAudit().Any(w =>
                w.Area == Gaode.Plc.Protocol.PlcArea.HoldingRegister && w.DocumentNumber == 1 && w.Value != 0), watchdog.Token);
            if (kind == "unknown") plc.Store.SetHoldingRegisterFromPlc(2, 65535);
            if (kind == "timeout") proxy.Armed = true;
            await ProtocolTcpFixture.UntilAsync(() => events.Any(e => e.Kind is DeviceEventKind.Completed or DeviceEventKind.Failed), watchdog.Token);
            Assert.Equal(kind == "success", events.Any(e => e.Kind == DeviceEventKind.Completed));
        }
        finally { await device.DisposeAsync(); }
        var options = new DbContextOptionsBuilder<Station01DbContext>().UseSqlite($"Data Source={plc.EvidenceStorePath};Mode=ReadOnly;Pooling=False").Options;
        await using var db = new Station01DbContext(options);
        var rows = await db.PlcCommunicationEvidence.AsNoTracking().Where(x => x.ActionId == actionId).ToArrayAsync(watchdog.Token);
        Assert.NotEmpty(rows);
        var storeId = (await db.Manifests.SingleAsync(watchdog.Token)).StoreId;
        var documents = new List<CommunicationEvidenceDocument>();
        foreach (var row in rows)
        {
            var document = await new CommunicationEvidenceReader(options, storeId).ReadAsync(row.EvidenceId, watchdog.Token);
            Assert.NotNull(document);
            Assert.Equal(envelope.RunId, document.RunId);
            Assert.Equal(envelope.OperationId, document.OperationId);
            Assert.False(document.Gap);
            documents.Add(document);
        }
        var exchanges = documents.SelectMany(d => JsonDocument.Parse(d.RawPayloadJson).RootElement.GetProperty("exchanges")
            .EnumerateArray().Select(e => e.Clone())).ToArray();
        var actualWire = device.BusinessEvidenceSince(0).Exchanges.Concat(device.HeartbeatEvidenceSince(0).Exchanges).ToArray();
        Assert.All(exchanges, e => Assert.Contains(actualWire, actual => actual.ConnectionId == e.GetProperty("connectionId").GetGuid() &&
            actual.Request == e.GetProperty("requestHex").GetString() && actual.Response == e.GetProperty("responseHex").GetString()));
        if (kind == "unknown") Assert.Contains(exchanges, e => WordAt(e, 1) == ushort.MaxValue);
        if (kind == "timeout")
        {
            Assert.NotEmpty(proxy.MutatedReplies);
            Assert.Contains(exchanges, e => e.GetProperty("responseHex").ValueKind == JsonValueKind.Null &&
                e.GetProperty("error").ValueKind == JsonValueKind.String);
        }
        var root = Environment.GetEnvironmentVariable("GAODE_009_EVIDENCE_ROOT") ?? throw new InvalidOperationException("009EvidenceRootRequired");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, caseId.Replace('/', '-') + "-" + actionId.ToString("N") + ".json"),
            JsonSerializer.Serialize(new { caseId, actionId, envelope, documents, events, actualWire,
                plc.EvidenceStorePath, componentOnly = true, independentProcess = false }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    // Independent expectation: the confirmed XY feedback uses PDU offset 1.
    // Decode its location within the actual read group, not a fixed response tail.
    private static ushort? WordAt(JsonElement exchange, int expectedOffset)
    {
        if (exchange.GetProperty("function").GetInt32() != 3 || exchange.GetProperty("responseHex").GetString() is not { } hex) return null;
        var offset = exchange.GetProperty("offset").GetInt32();
        var count = exchange.GetProperty("count").GetInt32();
        if (expectedOffset < offset || expectedOffset >= offset + count) return null;
        var bytes = Convert.FromHexString(hex);
        var position = 9 + 2 * (expectedOffset - offset);
        return bytes.Length >= position + 2 && bytes[7] == 3 ? BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(position, 2)) : null;
    }
}
