using Gaode.Infrastructure.Devices.Plc;
using Gaode.Plc.Protocol;
using Xunit;

namespace Gaode.Communication.Tests.Devices;

[Collection("CommunicationTcp")]
public sealed class ConfirmedMemoryLayoutTests
{
    [Fact]
    public void LatestTableIncludesNewAlarmAndShiftedHeartbeatAndAllSixAxes()
    {
        var layout = ConfirmedMemoryLayout.Load();
        Assert.Equal(85, layout.Points.Length);
        Assert.Equal(26, layout.Points.Count(p => p.Direction == "PC->PLC"));
        Assert.Equal(59, layout.Points.Count(p => p.Direction == "PLC->PC"));
        Check("PC_Alarm", 6037, "BoolByte"); Check("PLC_Heartbeat_Req", 6038, "BoolByte");
        Check("PC_Heartbeat_Resp", 2011, "BoolByte"); Check("Grab_Active_ID", 6062, "Int16");
        Check("Camera_Target_X", 2024, "Float32"); Check("X_Move_Start", 2001, "BoolByte");
        Check("X_Pos_Confirmed", 6040, "Int16"); Check("Machine_Current_Pos_X", 6064, "Float32");
        Check("Rotate_Start", 2000, "BoolByte"); Check("Rotate_Target_R", 2044, "Float32");
        Check("R_Pos_Confirmed", 6060, "Int16"); Check("Machine_Current_Pos_R", 6068, "Float32");
        Assert.DoesNotContain(layout.Points, p => p.MemoryByteAddress == 6039);
        Assert.Contains(layout.Sources, s => s.Sha256 == "8e846d0f71351f87b42db1bf40790f40766a392fd0a9e0968201f8c2056eb476");
        void Check(string id, int mb, string type)
        {
            var p = Assert.Single(layout.Points, p => p.Id == id);
            Assert.Equal(mb, p.MemoryByteAddress); Assert.Equal(type, p.ValueType);
        }
    }

    [Fact]
    public async Task FieldProjectionUsesConfiguredPduAndStillReportsAbsentBusinessSemantics()
    {
        var layout = ConfirmedMemoryLayout.Load();
        Assert.Throws<InvalidOperationException>(() => layout.CreateDefinition(new()));
        var profile = new FieldAddressProfile
        {
            LayoutId = ConfirmedMemoryLayout.CurrentId, Confirmed = true, Source = "Unit test mapping only",
            PcPduBase = 100, PlcPduBase = 1000, PlcArea = "HoldingRegister", BoolByteOrder = "EvenHigh", FloatOrder = "Cdab"
        };
        var definition = layout.CreateDefinition(profile);
        Assert.Equal(106, definition[SignalId.PcHeartbeatResp].DocumentNumber);
        Assert.Equal(1, definition[SignalId.PcHeartbeatResp].ByteOffset);
        Assert.Equal(1020, definition[SignalId.PlcHeartbeatReq].DocumentNumber);
        Assert.Equal("%MB6038", definition[SignalId.PlcHeartbeatReq].DocumentAddress);
        Assert.Equal(0, definition[SignalId.PlcHeartbeatReq].ByteOffset);
        Assert.Equal(BoolByteOrder.EvenHigh, definition.ByteOrderForBools);
        Assert.Equal(Float32ByteOrder.Cdab, definition.ByteOrder);
        Assert.All(definition.Fields, p => Assert.Equal(PlcArea.HoldingRegister, p.Area));
        var errors = definition.Validate();
        Assert.NotEmpty(errors);
        Assert.All(errors, e => Assert.Equal("RequiredSignalMissing", e.Reason));
        foreach (var id in new[] { SignalId.ManualZoneOccupied, SignalId.ModelPayload, SignalId.AlarmBits })
            Assert.Contains(errors, e => e.Detail.StartsWith(id + ":", StringComparison.Ordinal));
        var profilePath = Path.Combine(Path.GetTempPath(), "gaode-017-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(profilePath, System.Text.Json.JsonSerializer.Serialize(profile));
            var options = new PlcRuntimeOptions { Provider = "Real", Host = "127.0.0.1", Port = 1 };
            await using var device = new LatestProtocolPlcDevice(options, 0.01, fieldProfilePath: profilePath);
            Assert.Equal(Float32ByteOrder.Cdab, options.Float32ByteOrder);
            var rejection = await Assert.ThrowsAsync<ProtocolDefinitionException>(() => device.StartAsync(CancellationToken.None));
            Assert.All(rejection.Violations, e => Assert.Equal("RequiredSignalMissing", e.Reason));
            Assert.Equal(0, device.AllWriteDispatchCount);
            Assert.Empty(device.BusinessExchanges); Assert.Empty(device.HeartbeatExchanges);
        }
        finally { File.Delete(profilePath); }
    }

    [Theory]
    [InlineData(BoolByteOrder.EvenLow)]
    [InlineData(BoolByteOrder.EvenHigh)]
    public async Task TwoConnectionsPreserveAdjacentByteAndDecodeOneSharedRegister(BoolByteOrder order)
    {
        var original = ConfirmedProtocol.CreateTest(Float32ByteOrder.Abcd);
        var fields = original.Fields.Select(p => p.Id is SignalId.PcHeartbeatResp or SignalId.PcSystemReady
            ? p with { Area = PlcArea.HoldingRegister, DocumentNumber = 220, ValueType = PlcValueType.BoolByte,
                ByteOffset = p.Id == SignalId.PcHeartbeatResp ? 0 : 1 } : p).ToArray();
        var definition = new ProtocolDefinition(fields, original.AlarmBits, 64, 256, original.ByteOrder) { ByteOrderForBools = order };
        Assert.Empty(definition.Validate());
        var plan = definition.ReadPlan([SignalId.PcHeartbeatResp, SignalId.PcSystemReady]);
        Assert.Equal(1, Assert.Single(plan).Count); Assert.Equal(2, plan[0].Fields.Length);
        Assert.Empty(definition.ValidatePlan(plan));
        var admission = new PlcDefinitionAdmission(definition); admission.Prepare();
        var store = new SharedWord();
        var heartbeat = new PlcSignalAccessor(new YieldingConnection(store), admission);
        var business = new PlcSignalAccessor(new YieldingConnection(store), admission);
        // Harness watchdog only. The two deliberately yielding connections still
        // race inside each case; serialize with the other communication fixtures.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        for (var i = 0; i < 3; i++)
        {
            store.Value = 0;
            await Task.WhenAll(heartbeat.WriteBitAsync(SignalId.PcHeartbeatResp, true, timeout.Token),
                business.WriteBitAsync(SignalId.PcSystemReady, true, timeout.Token));
            Assert.Equal(0x0101, store.Value);
            Assert.True(await heartbeat.ReadBitAsync(SignalId.PcHeartbeatResp, timeout.Token));
            Assert.True(await business.ReadBitAsync(SignalId.PcSystemReady, timeout.Token));
            await heartbeat.WriteBitAsync(SignalId.PcHeartbeatResp, false, timeout.Token);
            Assert.Equal(order == BoolByteOrder.EvenLow ? 0x0100 : 0x0001, store.Value);
        }
        var collision = fields.Select(p => p.Id == SignalId.PcSystemReady ? p with { ByteOffset = 0 } : p);
        Assert.Contains(new ProtocolDefinition(collision, original.AlarmBits, 64, 256, original.ByteOrder).Validate(), e => e.Reason == "FieldOverlap");
    }

    [Fact]
    public void BooleanCodecDoesNotTreatArbitraryByteAsTrueOrEraseNeighbour()
    {
        Assert.Equal(0xA501, BoolByteCodec.Merge(0xA500, 1, 0, BoolByteOrder.EvenLow));
        Assert.Equal(0x01A5, BoolByteCodec.Merge(0x00A5, 1, 0, BoolByteOrder.EvenHigh));
        Assert.Throws<InvalidDataException>(() => BoolByteCodec.Decode(2, 0, BoolByteOrder.EvenLow));
    }

    private sealed class SharedWord { public ushort Value; }
    private sealed class YieldingConnection(SharedWord word) : IPlcTransport
    {
        public async Task<ushort[]> ReadRegistersAsync(ushort offset, ushort count, CancellationToken cancellationToken = default)
        {
            Assert.Equal(219, offset); Assert.Equal(1, count);
            var value = word.Value; await Task.Yield(); return [value];
        }
        public async Task WriteRegisterAsync(ushort offset, ushort value, CancellationToken cancellationToken = default)
        { Assert.Equal(219, offset); await Task.Yield(); word.Value = value; }
        public Task<bool[]> ReadCoilsAsync(ushort offset, ushort count, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task WriteCoilAsync(ushort offset, bool value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task WriteRegistersAsync(ushort offset, ushort[] values, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
