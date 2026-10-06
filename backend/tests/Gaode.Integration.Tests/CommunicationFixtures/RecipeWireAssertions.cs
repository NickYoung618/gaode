using System.Text.Json;
using Gaode.Plc.Protocol;
using Xunit;
namespace Gaode.Integration.Tests.Support;
// Existing actual device assertions, separated from protected business scenarios.
// Independent literal-wire expectations remain a separate required gate.
internal static class RecipeWireAssertions
{
    internal static async Task<int> Count(string folder,int address,ushort value) {
        using var audit=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(folder,"plc-write-audit.json")));
        return audit.RootElement.EnumerateArray().Count(x=>x.GetProperty("Accepted").GetBoolean()&&
            x.GetProperty("DocumentNumber").GetInt32()==address&&x.GetProperty("Value").GetInt32()==value);
    }
    internal static async Task SortingCount(string folder,int count) {
        Assert.Equal(count,await Count(folder,PlcAddressMap.HoldingRegisters.SortingCmd,1));
        Assert.Equal(count,await Count(folder,PlcAddressMap.HoldingRegisters.SortingCmd,2));
    }
}
