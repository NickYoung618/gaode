using Gaode.Integration.Tests.Support;
using Gaode.Domain.Station01;
using Xunit;
namespace Gaode.Integration.Tests.Station01;
public sealed partial class ExpectedRecipeMismatchTests
{
    [Fact]
    public async Task SelectedQ01AndDifferentActualFBlockBeforeProductMotionOrFinal()
    {
        Assert.Equal(1,await RecipeWireAssertions.Count(await RunMismatchAsync(),0x0001,2)); // public 3D only
    }
}
