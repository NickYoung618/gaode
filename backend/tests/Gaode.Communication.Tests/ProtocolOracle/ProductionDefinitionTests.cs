using System.Globalization;
using System.Text.Json;
using Gaode.Plc.Protocol;
using Xunit;

namespace Gaode.Communication.Tests.ProtocolOracle;

public sealed class ProductionDefinitionTests
{
    private static JsonDocument Oracle() => JsonDocument.Parse(File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "ProtocolOracle", "confirmed-20260925.json")));

    [Fact]
    public void CurrentAxesAndMechanicalFeedbackMatchIndependentLiteralOracle()
    {
        using var oracle = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "ProtocolOracle", "confirmed-011.json")));
        var definition = ConfirmedProtocol.CreateTest(Float32ByteOrder.Abcd);
        Assert.Empty(definition.Validate());
        Assert.Equal("Test", definition.Purpose);
        Assert.Equal(256, definition.RegisterCapacity); // PC02 explicit Test capacity, not physical PLC approval.
        var identities = new[] {
            (SignalId.CameraTargetX, SignalId.XMoveStart, SignalId.XPosConfirmed, SignalId.MachineCurrentPosX),
            (SignalId.CameraTargetY, SignalId.YMoveStart, SignalId.YPosConfirmed, SignalId.MachineCurrentPosY),
            (SignalId.CameraTargetZ, SignalId.ZCameraMoveStart, SignalId.ZCameraPosConfirmed, SignalId.MachineCurrentPosZ),
            (SignalId.ScanTargetZ, SignalId.ZScanMoveStart, SignalId.ZScanPosConfirmed, SignalId.ScanCurrentPosZ),
            (SignalId.GrabTargetZ, SignalId.ZGrabMoveStart, SignalId.ZGrapPosConfirmed, SignalId.FlipGrapCurrentPosZ)
        };
        var axes = oracle.RootElement.GetProperty("axes").EnumerateArray().ToArray();
        Assert.Equal(5, axes.Length);
        for (var i = 0; i < axes.Length; i++)
        {
            var (target, start, confirmed, actual) = identities[i];
            foreach (var (id, key, area, type, width, writer) in new[] {
                (target, "target", PlcArea.HoldingRegister, PlcValueType.Float32, 2, PlcWriter.Pc),
                (start, "start", PlcArea.Coil, PlcValueType.Bool, 1, PlcWriter.Pc),
                (confirmed, "confirmed", PlcArea.HoldingRegister, PlcValueType.Int16, 1, PlcWriter.Plc),
                (actual, "actual", PlcArea.HoldingRegister, PlcValueType.Float32, 2, PlcWriter.Plc) })
            {
                var point = definition[id];
                Assert.Equal(axes[i].GetProperty(key).GetInt32(), point.DocumentNumber);
                Assert.Equal(point.DocumentNumber - 1, PlcAddressMap.ToPduOffset(point.DocumentNumber));
                Assert.Equal(area, point.Area); Assert.Equal(type, point.ValueType);
                Assert.Equal(width, point.RegisterCount); Assert.Equal(writer, point.Writer);
                Assert.Equal(writer, point.ClearWriter);
                Assert.Equal(writer == PlcWriter.Pc ? PlcDirection.PcToPlc : PlcDirection.PlcToPc, point.Direction);
            }
            CheckCodes(confirmed, "confirmedCodes", ["Moving", "Arrived", "Timeout"]);
        }
        CheckCodes(SignalId.FlipStatus, "flipCodes", ["Idle", "Executing", "Completed", "Failed"]);
        CheckCodes(SignalId.FlipUnloadStatus, "putBackCodes", ["Idle", "Executing", "Completed", "Failed"]);
        CheckCodes(SignalId.SortingExecStatus, "sortingCodes", ["Idle", "Picked", "Placed", "GrabFailed"]);
        void CheckCodes(SignalId id, string key, string[] labels)
        {
            var values = oracle.RootElement.GetProperty(key).EnumerateObject().ToArray();
            var codes = SignalCodes.For(id)!;
            Assert.Equal(labels.Length, codes.Count); Assert.Equal(labels.Length, values.Length);
            for (var i = 0; i < labels.Length; i++) Assert.Equal(values[i].Value.GetUInt16(), codes[labels[i]]);
        }
    }

    [Fact]
    public void Float32MatchesIndependentFourOrderVectors()
    {
        using var oracle = Oracle();
        foreach (var vector in oracle.RootElement.GetProperty("float32Vectors").EnumerateArray())
            foreach (var layout in vector.GetProperty("words").EnumerateObject())
            {
                var order = Enum.Parse<Float32ByteOrder>(layout.Name, true);
                var expected = layout.Value.EnumerateArray().Select(w => ushort.Parse(w.GetString()!, NumberStyles.HexNumber)).ToArray();
                var value = vector.GetProperty("decimal").GetSingle();
                Assert.Equal(expected, Float32Codec.Encode(value, order));
                Assert.Equal(value, Float32Codec.Decode(expected[0], expected[1], order));
            }
    }

    [Fact]
    public void ReadGroupingUsesDeclaredIntervalsRatherThanWholeTableSpan()
    {
        var original = ConfirmedProtocol.CreateTest(Float32ByteOrder.Abcd);
        var remapped = new ProtocolDefinition(original.Fields.Select(p => p.Id switch
        {
            SignalId.CameraTargetX => p with { DocumentNumber = 200 },
            SignalId.CameraTargetY => p with { DocumentNumber = 210 },
            _ => p
        }), original.AlarmBits, 64, 256, original.ByteOrder);
        Assert.Empty(remapped.Validate());
        var plan = remapped.ReadPlan([SignalId.CameraTargetX, SignalId.CameraTargetY]);
        Assert.Equal(2, plan.Length);
        Assert.All(plan, p => Assert.Equal(2, p.Count));
        Assert.Empty(remapped.ValidatePlan(plan));
        Assert.Contains(remapped.ValidatePlan([plan[0] with { Count = 0 }]), v => v.Reason == "AccessLengthInvalid");
    }
}
