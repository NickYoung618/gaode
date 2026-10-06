using Gaode.Integration.Tests.Support;
using Gaode.Application.Ports;
using Gaode.Domain.Station01;
using Xunit;
namespace Gaode.Integration.Tests.Station01;
public sealed partial class SingleCommandRecoveryIntegrationTests
{
    [Theory]
    [InlineData(CommitState.Failed)]
    [InlineData(CommitState.CommitUnknown)]
    public async Task UnconfirmedRestartLinkNeverDispatchesNewPhysicalStart(CommitState injected)
    {
        var saved=await RunRestartLinkAsync(injected);
        var before=await RecipeWireAssertions.Count(saved.Before,Gaode.Plc.Protocol.PlcAddressMap.Coils.XMoveStart,1);
        Assert.True(before > 0); // Original run moved; failed restart commit must not add another trigger.
        Assert.Equal(before,await RecipeWireAssertions.Count(saved.After,Gaode.Plc.Protocol.PlcAddressMap.Coils.XMoveStart,1));
    }
}
