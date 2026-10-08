using Gaode.Plc.Protocol;
using Xunit;
using Gaode.Infrastructure.Devices.Plc;
using Gaode.Application.Ports;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using System.Diagnostics;

namespace Gaode.Communication.Tests;

// Site layout evidence, independent of the legacy Test map. Offline only.
public sealed class SiteProtocolAdaptationTests
{
    [Fact]
    public async Task SiteTcpUsesRealModelEncodingIntPoseAndRejectsAlarmWritesBeforeDispatch()
    {
        await using var fixture = new SiteProtocolTcpFixture();
        await using var client = fixture.Client();
        var admission = new PlcDefinitionAdmission(ConfirmedMemoryLayout.Load().CreateDefinition(Profile()));
        admission.Prepare();
        var accessor = new PlcSignalAccessor(client, admission);
        await accessor.WriteFloatAsync(SignalId.ModelNumber, 1.25f, default);
        await accessor.WriteWordAsync(SignalId.FlipTargetFace, 2, default);
        Assert.Equal((ushort)0, fixture.Word(2048)); Assert.Equal((ushort)0x3fa0, fixture.Word(2050));
        Assert.Equal((ushort)2, fixture.Word(2012));
        var before = fixture.Writes.Count;
        await Assert.ThrowsAsync<InvalidOperationException>(() => accessor.WriteWordAsync(SignalId.AlarmBits, 0, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => accessor.WriteWordAsync(SignalId.AlarmSeverity, 0, default));
        Assert.Equal(before, fixture.Writes.Count);
    }

    [Fact]
    public async Task SiteAlarmZeroNeverOverridesCurtainDoorOrIndependentFaultAndUnknownSafetyNeverMoves()
    {
        await using var fixture = new SiteProtocolTcpFixture();
        var definition = ConfirmedMemoryLayout.Load().CreateDefinition(Profile());
        await using var device = new LatestProtocolPlcDevice(new PlcRuntimeOptions { Provider = "Real",
            Purpose = RuntimePurposes.RealDeviceCommissioning,
            PositionBasis = new("OFFLINE_ONLY", "mm", RuntimePurposes.RealDeviceCommissioning, "Offline:coordinate-fixture"),
            Definition = definition, Host = "127.0.0.1", Port = fixture.Port, IoTimeoutMs = 1000, HeartbeatTimeoutMs = 3000 }, .01);
        using var deadline = new CancellationTokenSource(15000);
        await device.StartAsync(deadline.Token);
        Assert.Equal("XY", device.AxisPurpose("F")); Assert.Equal("ScanZ", device.AxisPurpose("E"));
        Assert.Equal("DetectionZ", device.AxisPurpose("Detection"));
        Assert.Equal(SafetyAssessment.Unconfirmed, device.Observe().SafetyAssessment);
        Assert.Equal("OFFLINE_ONLY", device.Observe().Position!.CoordinateFrame);
        Assert.Equal("Offline:coordinate-fixture:mm", device.Observe().Position!.UnitBasis);
        Assert.Equal(ManualAreaState.Unconfirmed, device.Observe().ManualArea);
        foreach (var (mask, name) in new[] { ((ushort)1, "LightCurtain"), ((ushort)4, "SafetyDoor") })
        {
            fixture.SetWord(6056, mask); fixture.SetWord(6058, 0);
            await UntilAsync(() => device.Observe().Alarms!.Any(a => a.Name == name), deadline.Token);
            Assert.Equal(SafetyAssessment.ExplicitUnsafe, device.Observe().SafetyAssessment);
            Assert.Contains(device.Observe().Alarms!, a => a.Name == name && a.Severity == AlarmLevel.Critical);
        }
        fixture.SetWord(6056, 0); fixture.SetByte(6020, 1);
        await UntilAsync(() => device.Observe().ReasonCodes.Contains("PlcIndependentSafetyActive:EStopActive") && device.Observe().SafetyAssessment == SafetyAssessment.ExplicitUnsafe, deadline.Token);
        fixture.SetByte(6020, 0);
        await UntilAsync(() => device.Observe().SafetyAssessment == SafetyAssessment.Unconfirmed, deadline.Token);
        var now = Stopwatch.GetTimestamp();
        var envelope = new PortEnvelope(Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(), "offline-site", "1",
            "RealDeviceCommissioning", now, now + 5 * Stopwatch.Frequency, "fixture");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(async () => await device.RequestMoveAsync(
            new(envelope, Guid.NewGuid(), new FixedPoint("offline", "1", 1.25, 1.25, "mm", "OFFLINE_ONLY"),
                Guid.NewGuid(), "fixture", "F"), _ => { }, deadline.Token));
        Assert.Contains("PLC-Q3/Q4", error.Message);
        Assert.DoesNotContain(fixture.Writes, w => w.Offset is 3028 or 3029);
        Assert.All(fixture.Writes, w => Assert.Equal(1005, w.Offset)); // Only PC heartbeat; zero movement/targets.
    }
    private static async Task UntilAsync(Func<bool> test, CancellationToken ct)
    { while (!test()) await Task.Delay(10, ct); }
    internal static FieldAddressProfile Profile() => new()
    {
        LayoutId = ConfirmedMemoryLayout.CurrentId, Confirmed = true,
        Source = "Offline:XLS-address-convention-fixture", PcPduBase = 1000, PlcPduBase = 3000,
        PlcArea = "HoldingRegister", BoolByteOrder = "EvenLow", FloatOrder = "Cdab"
    };

    [Fact]
    public void ConfirmedSiteDefinitionUsesRealTypesAndHasNoRetiredTeachingRequirement()
    {
        var missingBasis = new PlcRuntimeOptions { Provider = "Real", Purpose = RuntimePurposes.RealDeviceCommissioning };
        Assert.Contains("PlcPositionBasisMissingOrMismatched", Assert.Throws<InvalidOperationException>(missingBasis.Validate).Message);
        missingBasis.PositionBasis = new("OFFLINE_ONLY", "mm", "Test", "Offline:wrong-purpose");
        Assert.Contains("PlcPositionBasisMissingOrMismatched", Assert.Throws<InvalidOperationException>(missingBasis.Validate).Message);
        var definition = ConfirmedMemoryLayout.Load().CreateDefinition(Profile());
        Assert.Empty(definition.Validate());
        Assert.Equal(1003, definition[SignalId.PcStartCmd].DocumentNumber - 1);
        Assert.Equal(1, definition[SignalId.PcStartCmd].ByteOffset);
        Assert.Equal(PlcWriter.Pc, definition[SignalId.PcStartCmd].ClearWriter);
        Assert.DoesNotContain(ConfirmedProtocol.CreateTest(Float32ByteOrder.Abcd).Fields, p => p.Id == SignalId.PcStartCmd);
        Assert.DoesNotContain(definition.Fields, p => p.Id is SignalId.TeachModeCmd or SignalId.TeachConfirm or SignalId.ManualZoneOccupied);
        Assert.Equal(3028, definition[SignalId.AlarmBits].DocumentNumber - 1);
        Assert.Equal(3029, definition[SignalId.AlarmSeverity].DocumentNumber - 1);
        Assert.Equal(PlcWriter.Plc, definition[SignalId.AlarmBits].Writer);
        Assert.Equal(PlcWriter.Plc, definition[SignalId.AlarmBits].ClearWriter);
        Assert.Equal(1006, definition[SignalId.FlipTargetFace].DocumentNumber - 1);
        Assert.Equal(PlcValueType.Int16, definition[SignalId.FlipTargetFace].ValueType);
        Assert.Equal(1024, definition[SignalId.ModelNumber].DocumentNumber - 1);
        Assert.Equal(PlcValueType.Float32, definition[SignalId.ModelNumber].ValueType);
        Assert.Equal("LightCurtain", definition.AlarmBits.Single(b => b.Bit == 0).Name);
        Assert.Equal("SafetyDoor", definition.AlarmBits.Single(b => b.Bit == 2).Name);
        Assert.Equal((ushort)0, SignalCodes.Value(SignalId.AlarmSeverity, "Normal"));
    }
}
