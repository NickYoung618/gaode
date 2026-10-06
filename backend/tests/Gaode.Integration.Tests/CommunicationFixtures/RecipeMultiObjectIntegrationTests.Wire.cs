using Gaode.Integration.Tests.Support;
using Xunit;
namespace Gaode.Integration.Tests.Station01;
public sealed partial class RecipeMultiObjectIntegrationTests
{
    [Fact]
    [Trait("EvidenceLevel", "PortComponent")]
    public async Task OrdinaryTwoFaceAutoFlipExecutesBothFacesFromInitialHeight() =>
        await AssemblyComponentExecution.RunAsync(false, false, false, ordinary: true);

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    public async Task AssemblySharesOneFlipAndRetainsPartsWhenECodeIsPresentOrMissing(bool missingCode, bool workerError, bool ngParts)
    { await RunAssemblyAsync(missingCode,workerError,ngParts); }
    [Fact]
    [Trait("EvidenceLevel", "PortComponent")]
    [Trait("VerificationSet", "S")]
    public async Task AssemblyNgPriorityRetainsPendingDetailAndMovesWholeEntityOnce() =>
        await RecipeWireAssertions.SortingCount(await RunAssemblyAsync(false,false,true),1);
}
