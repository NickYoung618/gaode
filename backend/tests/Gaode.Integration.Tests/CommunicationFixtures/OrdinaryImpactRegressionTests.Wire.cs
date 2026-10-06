using Gaode.Integration.Tests.Support;
using Xunit;
namespace Gaode.Integration.Tests.Station01;
// The protected scenario executes all unchanged semantic assertions. Only actual
// exported wire evidence is interpreted here; this file may change with a protocol.
public sealed partial class OrdinaryImpactRegressionTests
{
    [Theory]
    [InlineData("q03", 6, 1, 0)]
    [InlineData("q03-ng", 6, 1, 1)]
    [InlineData("q03-pending", 6, 1, 1)]
    [InlineData("q09", 12, 3, 0)]
    [InlineData("q18", 12, 3, 0)]
    public async Task ExistingOrdinaryRoutesKeepFaceAndSortingCounts(string slug, int calls, int flips, int sorts)
    {
        var folder=await RunOrdinaryAsync(slug,calls,flips,sorts);
        await RecipeWireAssertions.SortingCount(folder,sorts);
    }
}
