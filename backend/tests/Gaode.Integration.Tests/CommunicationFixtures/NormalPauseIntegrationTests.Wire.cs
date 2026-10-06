using Gaode.Integration.Tests.Support;
using Gaode.Domain.Station01;
using Xunit;
namespace Gaode.Integration.Tests.Station01;
public sealed partial class NormalPauseIntegrationTests
{
    [Fact]
    public async Task ActualQ01PausesAfterThreeDThenContinuesSameRunToFinalWithoutRepeatedPublicWork()
    {
        Assert.Equal(1,await RecipeWireAssertions.Count(await RunPauseAsync(),0x0001,5));
    }
}
