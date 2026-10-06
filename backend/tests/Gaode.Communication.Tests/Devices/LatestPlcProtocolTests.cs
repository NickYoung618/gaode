using Gaode.Plc.Protocol;
using VirtualPlc;
using Xunit;

namespace Gaode.Communication.Tests.Devices;

// Migrated from Contracts: original literal vectors, widths and ownership remain communication obligations.
// Both endpoints now use one codec; matching independent literals (and wire tests) replaces duplicate-code equality.
public sealed class LatestPlcProtocolTests
{
    [Theory]
    [InlineData(Float32ByteOrder.Abcd, 0x42F6, 0xE979)]
    [InlineData(Float32ByteOrder.Cdab, 0xE979, 0x42F6)]
    [InlineData(Float32ByteOrder.Badc, 0xF642, 0x79E9)]
    [InlineData(Float32ByteOrder.Dcba, 0x79E9, 0xF642)]
    public void KnownFloatHasExpectedWireWordsAndBothSidesAgree(Float32ByteOrder order, int a, int b)
    {
        ushort[] expected = [(ushort)a, (ushort)b];
        Assert.Equal(expected, Float32Codec.Encode(123.456f, order));
        Assert.Equal(123.456f, Float32Codec.Decode(expected[0], expected[1], order));
        foreach (var value in new[] { 0f, -0.125f, -100.5f, 100f, 150f, 65536.25f })
        {
            var words = Float32Codec.Encode(value, order);
            Assert.Equal(value, Float32Codec.Decode(words[0], words[1], order));
        }
    }

    [Fact]
    public void RetiredAcksAndRegionRecipeWordsAreNotCurrentSignals()
    {
        // These retired literal addresses are independent of the implementation map.
        var store = new PlcDataStore(Microsoft.Extensions.Options.Options.Create(new SimulationOptions()));
        foreach (var document in new[] { 1, 2, 0x13, 0x17, 0x20, 0x23, 0x24, 0x25, 0x26, 0x27, 0x28, 0x29, 0x40, 0x52, 0x53, 0x54, 0x55 })
        {
            Assert.Null(PlcAddressMap.RegisterAt(document));
            Assert.False(store.TryWriteHoldingRegistersFromPc(document - 1, [1]));
        }
        foreach (var document in new[] { 3, 5, 7, 9, 0xB, 0xD, 0xF, 0x11 })
        {
            var point = PlcAddressMap.HoldingRegisterPoints[document];
            Assert.Equal(PlcValueType.Float32, point.ValueType);
            Assert.Equal(2, point.RegisterCount);
            Assert.Same(point, PlcAddressMap.RegisterAt(document + 1));
        }
    }

    [Fact]
    public void OwnershipAndWholeFloatWritesAreEnforced()
    {
        var store = new PlcDataStore(Microsoft.Extensions.Options.Options.Create(new SimulationOptions()));
        Assert.False(store.TryWriteHoldingRegistersFromPc(0x7F, [2])); // PLC-owned X feedback
        Assert.False(store.TryWriteHoldingRegistersFromPc(2, [100])); // half a Float32
        Assert.False(store.TryWriteCoilsFromPc(0, [true])); // PLC-owned heartbeat
        Assert.Throws<InvalidOperationException>(() => store.SetCoilFromPlc(0x22, true));
        Assert.Throws<InvalidOperationException>(() => store.SetHoldingRegisterFromPlc(3, 1));
        Assert.True(store.TryWriteHoldingRegistersFromPc(0x5F, [1]));
    }

    [Fact]
    public void RetiredClampAndStartCommandsCannotBeWritten()
    {
        var store = new PlcDataStore(Microsoft.Extensions.Options.Options.Create(new SimulationOptions()));
        Assert.False(store.TryWriteCoilsFromPc(8, [true]));
        Assert.False(store.TryWriteCoilsFromPc(0x10, [true]));
        Assert.False(store.TryWriteHoldingRegistersFromPc(0x22, [0]));
        Assert.False(store.TryWriteHoldingRegistersFromPc(0x22, [1]));
        Assert.False(store.TryWriteHoldingRegistersFromPc(0x23, [1]));
    }
}
