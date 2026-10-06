using Gaode.Plc.Protocol;
using Gaode.Domain.Station01;
using Microsoft.Extensions.Options;
using VirtualPlc;
using Xunit;

namespace Gaode.Communication.Tests.Devices;

public sealed class VirtualPlcMonitorTests
{
    [Fact]
    public void SameValueFullFloatBatchStillHasIndependentWriteReceipt()
    {
        var store = new PlcDataStore(Options.Create(new SimulationOptions()));
        ushort[] words = [..Float32Codec.Encode(10, Float32ByteOrder.Abcd), ..Float32Codec.Encode(20, Float32ByteOrder.Abcd)];
        Assert.True(store.TryWriteHoldingRegistersFromPc(2, words));
        var first = store.GetWriteAudit().Last().Sequence;
        var changes = store.GetChanges(0).LatestSequence;
        Assert.True(store.TryWriteHoldingRegistersFromPc(2, words));
        Assert.Equal(changes, store.GetChanges(0).LatestSequence);
        var again = store.GetWriteAudit().Where(x => x.Sequence > first).ToArray();
        Assert.Equal(2, again.Length); // Two logical Float32 points; the wire batch still contains four words.
        Assert.Equal([3,5], again.Select(x => x.DocumentNumber));
        Assert.All(again, x => { Assert.Equal(words, x.RawWords); Assert.Equal(2, x.PduOffset); Assert.Equal(4, x.RegisterCount); Assert.Equal("Accepted", x.Receipt); });
    }

    [Fact]
    public void ChangeCursorRetainsIntermediateWritesAndReportsFiniteCacheGap()
    {
        var store = new PlcDataStore(Options.Create(new SimulationOptions()));
        var cursor = store.GetChanges(0).LatestSequence;
        store.SetCoilFromPcForSimulation(PlcAddressMap.Coils.PcSystemReady, true);
        store.SetCoilFromPcForSimulation(PlcAddressMap.Coils.PcSystemReady, false);
        store.SetCoilFromPcForSimulation(PlcAddressMap.Coils.PcSystemReady, true);
        var changes = store.GetChanges(cursor).Changes;
        Assert.Equal(["1", "0", "1"], changes.Select(x => x.Current));
        Assert.Equal(["0", "1", "0"], changes.Select(x => x.Previous));
        Assert.Equal([cursor + 1, cursor + 2, cursor + 3], changes.Select(x => x.Sequence));
        Assert.True(store.ReadCoilByDocumentNumber(PlcAddressMap.Coils.PcSystemReady));

        for (var i = 0; i < 8200; i++)
            store.SetCoilFromPcForSimulation(PlcAddressMap.Coils.PcSystemReady, i % 2 == 1);
        var first = store.GetChanges(0);
        Assert.True(first.Gap);
        Assert.Equal(first.LatestSequence - 8191, first.OldestSequence);
        Assert.Equal(1024, first.Changes.Count);
        Assert.Equal(first.OldestSequence, first.Changes[0].Sequence);
        var second = store.GetChanges(first.Changes[^1].Sequence);
        Assert.False(second.Gap);
        Assert.Equal(first.Changes[^1].Sequence + 1, second.Changes[0].Sequence);
    }
}
