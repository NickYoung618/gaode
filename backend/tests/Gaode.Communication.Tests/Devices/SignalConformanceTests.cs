using Gaode.Infrastructure.Devices.Plc;
using Gaode.Plc.Protocol;
using Xunit;

namespace Gaode.Communication.Tests.Devices;

[Collection("CommunicationTcp")]
public sealed class SignalConformanceTests
{
    // Literal offsets/words transcribed from the independent oracle, never derived from the DUT map.
    [Theory]
    [InlineData("WIRE-OWNER/feedback", 0x7F, 2)]
    [InlineData("WIRE-OWNER/half-float", 2, 100)]
    [InlineData("WIRE-OWNER/retired-pallet-feedback", 0x23, 1)]
    [InlineData("WIRE-OWNER/retired-pallet-host-lock", 0x22, 1)]
    public async Task IllegalHostRegisterWriteIsRejectedOverTcp(string caseId, int offset, int value)
    {
        Assert.StartsWith("WIRE-OWNER/", caseId);
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var plc = new ProtocolTcpFixture();
        await plc.StartAsync(watchdog.Token);
        await using var wire = plc.Client();
        await Assert.ThrowsAsync<IOException>(() => wire.WriteRegisterAsync((ushort)offset, (ushort)value, watchdog.Token));
        var receipt = Assert.Single(plc.Store.GetWriteAudit());
        Assert.False(receipt.Accepted);
        Assert.Equal(offset + 1, receipt.DocumentNumber);
        Assert.NotNull(receipt.RequestHex);
        Assert.NotNull(receipt.ResponseHex);
        Assert.NotNull(receipt.RejectionReason);
    }
    [Fact]
    public async Task HeartbeatOwnershipAndFullFloatWritePreserveActualReceipt()
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var plc = new ProtocolTcpFixture();
        await plc.StartAsync(watchdog.Token);
        await using var rejected = plc.Client();
        await Assert.ThrowsAsync<IOException>(() => rejected.WriteCoilAsync(0, true, watchdog.Token));
        await using var accepted = plc.Client();
        await accepted.WriteRegistersAsync(2, [0x42F6, 0xE979], watchdog.Token);
        await accepted.WriteRegistersAsync(2, [0x42F6, 0xE979], watchdog.Token);
        Assert.Equal(new ushort[] { 0x42F6, 0xE979 }, await accepted.ReadRegistersAsync(2, 2, watchdog.Token));
        var audit = plc.Store.GetWriteAudit();
        Assert.False(audit[0].Accepted);
        var batches = audit.Where(x => x.Accepted && x.Function == 16).ToArray();
        Assert.Equal(2, batches.Length);
        Assert.All(batches, x =>
        {
            Assert.Equal(2, x.RegisterCount);
            Assert.Equal(new ushort[] { 0x42F6, 0xE979 }, x.RawWords);
            Assert.NotNull(x.ResponseHex);
        });
        Assert.NotEqual(batches[0].TransactionId, batches[1].TransactionId);
        Assert.True(batches[1].Sequence > batches[0].Sequence);
    }
}
