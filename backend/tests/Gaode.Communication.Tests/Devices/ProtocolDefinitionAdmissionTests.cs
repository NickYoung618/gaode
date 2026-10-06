using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Devices.Plc;
using Gaode.Plc.Protocol;
using Xunit;

namespace Gaode.Communication.Tests.Devices;

[Collection("CommunicationTcp")]
public sealed class ProtocolDefinitionAdmissionTests
{
    [Fact]
    public void PreparedMasksKeepOldLowKeysAndNewHighKeysDistinctAndOwned()
    {
        var plans=new PreparedPlcReadPlans(ConfirmedProtocol.CreateTest(Float32ByteOrder.Abcd));
        var low=plans.Get([SignalId.PlcHeartbeatReq]);
        var high=plans.Get([SignalId.GrabId]);
        Assert.Equal((UInt128)1,low.FieldMask);
        Assert.Equal((UInt128)1<<64,high.FieldMask);
        Assert.NotEqual(low.FieldMask,high.FieldMask);
        plans.RequireOwned(low);plans.RequireOwned(high);
        Assert.NotEmpty(plans.Get(PreparedPlcReadPlans.Rotation).Blocks);
        Assert.Throws<InvalidOperationException>(()=>plans.Get([(SignalId)63]));
        Assert.Throws<InvalidOperationException>(()=>plans.Get([(SignalId)128]));
        var other=new PreparedPlcReadPlans(ConfirmedProtocol.CreateTest(Float32ByteOrder.Abcd));
        Assert.Throws<InvalidOperationException>(()=>plans.RequireOwned(other.Get([SignalId.GrabId])));
        Assert.InRange(plans.Count,1,512); // Finite admitted set; no high-bit powerset.
    }

    [Theory]
    [InlineData("PD-N01-overlap/same-area", "FieldOverlap")]
    [InlineData("PD-N02-width/float32-one-word", "TypeWidthMismatch")]
    [InlineData("PD-N03-bit/out-of-field", "BitOutOfRange")]
    [InlineData("PD-N04-owner/read-direction", "ReadDirectionConflict")]
    [InlineData("PD-N04-owner/write-responsibility", "WriteResponsibilityConflict")]
    [InlineData("PD-N05-required/missing", "RequiredSignalMissing")]
    [InlineData("PD-N06-range/address-capacity", "AddressRangeInvalid")]
    [InlineData("PD-N06-range/access-length", "AccessLengthInvalid")]
    [InlineData("PD-P01-area/same-number", null)]
    [InlineData("PD-P02-width/float32-two-words", null)]
    [InlineData("PD-P03-layout/noncontiguous-split", null)]
    [InlineData("PD-P04-owner/confirmed-read-write-clear", null)]
    public async Task FormalEntryAdmission(string caseId, string? expectedReason)
    {
        // Watchdog protects the component harness; it is not a business budget.
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var plc = new ProtocolTcpFixture();
        await plc.StartAsync(watchdog.Token);
        var input = Input(caseId);
        await using var device = plc.Device(input);
        var envelope = ProtocolTcpFixture.Envelope();
        var actionId = Guid.NewGuid();
        var intent = Guid.NewGuid();
        var accepted = new TaskCompletionSource<DeviceEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        var startAt = DateTimeOffset.UtcNow;
        var startFailure = await Record.ExceptionAsync(() => device.StartAsync(watchdog.Token));
        if (expectedReason is null)
            await ProtocolTcpFixture.UntilAsync(() => device.Observe().HasReliableObservation &&
                device.Observe().OperatingMode == OperatingMode.Automatic &&
                device.Observe().SafetyAssessment == SafetyAssessment.Clear, watchdog.Token);
        var requestAt = DateTimeOffset.UtcNow;
        var actionFailure = await Record.ExceptionAsync(async () => await device.RequestStartAsync(
            envelope, actionId, intent, e => { if (e.Kind is DeviceEventKind.Accepted or DeviceEventKind.Failed) accepted.TrySetResult(e); }, watchdog.Token));
        if (expectedReason is null && actionFailure is null)
        {
            var result = await accepted.Task.WaitAsync(watchdog.Token);
            Assert.True(result.Kind == DeviceEventKind.Accepted, plc.DeviceDiagnostics);
            Assert.Equal(actionId, result.ActionId);
            Assert.Equal(envelope, result.Envelope);
            await ProtocolTcpFixture.UntilAsync(() => device.AllWriteDispatches.Any(w => w.Channel == "heartbeat"), watchdog.Token);
        }
        if (caseId.StartsWith("PD-P04", StringComparison.Ordinal))
        {
            Assert.True(device.Observe().HasReliableObservation);
            Assert.Equal(DeviceReadiness.Ready, device.Observe().Readiness);
            Assert.DoesNotContain(plc.Store.GetWriteAudit(), x => x.Area == PlcArea.HoldingRegister &&
                x.DocumentNumber is 0x27 or 0x28); // Removed region handshake never dispatched.

        }
        var dispatchSnapshot = device.BothChannelDispatchSnapshot;
        var writes = dispatchSnapshot.Writes;
        var count = dispatchSnapshot.Total;
        var gap = dispatchSnapshot.Gap;
        var plan = input.PreparedPlan ?? input.Definition.ReadPlan(input.Definition.Fields.Select(p => p.Id));
        var oraclePath = Path.Combine(AppContext.BaseDirectory, "ProtocolOracle", "confirmed-011.json");
        var details = new
        {
            caseId, scope = "SameProcessTcpComponent", startAt, requestAt,
            entry = "LatestProtocolPlcDevice.StartAsync -> same instance RequestStartAsync",
            validator = "PlcDefinitionAdmission.Prepare/RequireAdmitted", expectedReason,
            envelope, actionId, intent, fixture = input.Definition, plan,
            fixtureDigest = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(input))),
            oracleDigest = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(oraclePath))),
            definitionAdmitted = device.DefinitionAdmitted,
            startReason = startFailure?.Message, actionReason = actionFailure?.Message,
            dispatchScope = new[] { "business", "heartbeat" }, count, gap, writes,
            businessExchanges = device.BusinessExchanges, virtualPlcWrites = plc.Store.GetWriteAudit()
        };
        var evidenceRoot = Environment.GetEnvironmentVariable("GAODE_009_EVIDENCE_ROOT") ??
            Path.Combine(Path.GetTempPath(), "gaode-009-wire-evidence");
        Directory.CreateDirectory(evidenceRoot);
        await File.WriteAllTextAsync(Path.Combine(evidenceRoot,
            caseId.Replace('/', '-') + "-" + Guid.NewGuid().ToString("N") + ".json"),
            JsonSerializer.Serialize(details, new JsonSerializerOptions { WriteIndented = true }));
        Assert.False(gap);
        Assert.Equal(count, writes.Count);
        if (expectedReason is not null)
        {
            var preparation = Assert.IsType<ProtocolDefinitionException>(startFailure);
            var dispatch = Assert.IsType<ProtocolDefinitionException>(actionFailure);
            Assert.Equal(expectedReason, Assert.Single(preparation.Violations.Select(v => v.Reason).Distinct()));
            Assert.Equal(expectedReason, Assert.Single(dispatch.Violations.Select(v => v.Reason).Distinct()));
            Assert.False(device.DefinitionAdmitted);
            Assert.Equal(0, count);
            Assert.Empty(writes);
            Assert.Empty(plc.Store.GetWriteAudit());
            Assert.False(accepted.Task.IsCompleted);
            return;
        }
        Assert.Null(startFailure);
        Assert.Null(actionFailure);
        Assert.True(device.DefinitionAdmitted);
        // Current startup uses actual PC_System_Ready; no retired PC_Start pulse.
        Assert.Contains(writes, w => w.Channel == "business" && w.Function == 5 && w.Offset == 2 && w.Words.SequenceEqual(new ushort[] { 1 }));
        Assert.DoesNotContain(writes, w => w.Channel == "business" && w.Function == 5 && w.Offset == 8);
        Assert.Contains(writes, w => w.Channel == "heartbeat" && w.Function == 5 && w.Offset == 1);
        Assert.Contains(plc.Store.GetWriteAudit(), w => w.Accepted && w.Area == PlcArea.Coil && w.DocumentNumber == 3 && w.Value == 1 && w.RequestHex is not null && w.ResponseHex is not null);
        if (caseId.StartsWith("PD-P01", StringComparison.Ordinal))
            Assert.Equal(input.Definition[SignalId.PcSystemReady].DocumentNumber, input.Definition[SignalId.CameraTargetX].DocumentNumber);
        if (caseId.StartsWith("PD-P02", StringComparison.Ordinal))
        {
            Assert.Equal(2, input.Definition[SignalId.CameraTargetX].RegisterCount);
            Assert.Equal(new ushort[] { 0x42F6, 0xE979 }, Float32Codec.Encode(123.456f, Float32ByteOrder.Abcd));
            Assert.Contains(plan, p => p.Area == PlcArea.HoldingRegister && p.Offset <= 2 && p.Offset + p.Count >= 4);
        }
        if (caseId.StartsWith("PD-P03", StringComparison.Ordinal))
        {
            var fields = input.Definition.Fields.Where(p => p.Area == PlcArea.HoldingRegister).ToArray();
            Assert.True(fields.Max(p => p.DocumentNumber + p.RegisterCount - 1) - fields.Min(p => p.DocumentNumber) + 1 > 125);
            Assert.Empty(input.Definition.ValidatePlan(plan));
            Assert.All(plan.Where(p => p.Area == PlcArea.HoldingRegister), p => Assert.InRange(p.Count, 1, 125));
            // A definition accepted but then bypassed by the old fixed polling window must fail.
            Assert.Contains(device.BusinessExchanges, x => IsRead(x.Request, 3, 199, 2));
        }

    }
    private static bool IsRead(string hex, byte function, int offset, int count)
    {
        var b = Convert.FromHexString(hex);
        return b.Length == 12 && b[7] == function && (b[8] << 8 | b[9]) == offset && (b[10] << 8 | b[11]) == count;
    }
    internal static PlcDefinitionTestInput Input(string id)
    {
        var baseline = ConfirmedProtocol.CreateTest(Float32ByteOrder.Abcd);
        var fields = baseline.Fields.ToArray();
        var bits = baseline.AlarmBits.ToArray();
        void Change(SignalId signal, Func<PlcPoint, PlcPoint> edit)
        { var index = Array.FindIndex(fields, p => p.Id == signal); fields[index] = edit(fields[index]); }
        switch (id)
        {
            case "PD-N01-overlap/same-area": Change(SignalId.CameraTargetY, p => p with { DocumentNumber = 4 }); break;
            case "PD-N02-width/float32-one-word": Change(SignalId.CameraTargetX, p => p with { RegisterCount = 1 }); break;
            case "PD-N03-bit/out-of-field": bits[0] = bits[0] with { Bit = 16 }; break;
            case "PD-N04-owner/read-direction": Change(SignalId.PlcReadyState, p => p with { HostReadable = false }); break;
            case "PD-N04-owner/write-responsibility": Change(SignalId.ZCameraPosConfirmed, p => p with { Writer = PlcWriter.Pc }); break;
            case "PD-N05-required/missing": fields = fields.Where(p => p.Id != SignalId.XMoveStart).ToArray(); break;
            case "PD-N06-range/address-capacity": Change(SignalId.CameraTargetX, p => p with { DocumentNumber = 257 }); break;
            case "PD-P03-layout/noncontiguous-split": Change(SignalId.MachineCurrentPosX, p => p with { DocumentNumber = 200 }); break;
        }
        var definition = new ProtocolDefinition(fields, bits, 64, 256, Float32ByteOrder.Abcd);
        ImmutableArray<SignalReadGroup>? plan = null;
        if (id == "PD-N06-range/access-length")
        {
            var original = definition.ReadPlan(fields.Select(p => p.Id));
            plan = original.SetItem(0, original[0] with { Count = 0 });
        }
        return new(definition, plan);
    }
}
