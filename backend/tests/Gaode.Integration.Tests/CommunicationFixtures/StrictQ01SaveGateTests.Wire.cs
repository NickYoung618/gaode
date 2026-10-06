using Gaode.Integration.Tests.Support;
using Gaode.Domain.Station01;
using Xunit;
namespace Gaode.Integration.Tests.Station01;
public sealed partial class StrictQ01SaveGateTests
{
    [Fact]
    public async Task ProductMediaCommitFailureStopsBeforeNextMoveAndFinal()
    {
        Assert.Equal(2,await RecipeWireAssertions.Count(await RunSaveGateAsync(),0x0001,2)); // public 3D + A, no B
    }
}
