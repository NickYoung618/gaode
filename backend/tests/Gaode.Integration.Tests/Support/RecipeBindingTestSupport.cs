using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Infrastructure.Configuration;
using Gaode.Infrastructure.Diagnostics;
using Gaode.Infrastructure.Persistence;
using Gaode.Infrastructure.Recipes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Gaode.Integration.Tests.Support;

// Finite fixture access to actual configuration, log subscription and SQLite.
// Business tests receive the existing semantic query types or exact stored text;
// this helper makes no device-success decisions and accesses no wire evidence.
internal static class RecipeBindingTestSupport
{
    internal static IPublicConfiguration Configuration(string root,string schema)=>new ConfigurationLoader(root,schema);
    internal static IRecipeCatalog Catalog(string path)=>new JsonRecipeCatalog(path);
    internal static RecipeDefinition Match(IRecipeCatalog catalog, string code, string scenario)
    {
        var matched = RecipeMatcher.Match(catalog.GetSnapshot(), code, null, scenario, "Test");
        Xunit.Assert.Equal(RecipeMatchStatus.Matched, matched.Status);
        return Xunit.Assert.IsType<RecipeDefinition>(matched.Definition);
    }
    internal static IDisposable Subscribe(ILogger logger)=>new RuntimeDiagnosticLogging(logger);
    internal static ITraceQuery Query(Station01HostFixture fixture)=>fixture.Host.Services.GetRequiredService<ITraceQuery>();
    private static Station01DbContext Open(Station01HostFixture fixture)=>
        new(fixture.Host.Services.GetRequiredService<DbContextOptions<Station01DbContext>>());
    internal static async Task<int> PersistedRunCount(Station01HostFixture fixture) {
        await using var db=Open(fixture); return await db.Runs.CountAsync();
    }
    internal static async Task<string[]> HandoffPayloads(Station01HostFixture fixture,Guid runId,CancellationToken token) {
        await using var db=Open(fixture);
        return await db.PublicPreparationHandoffsV2.AsNoTracking().Where(x=>x.RunId==runId).Select(x=>x.PayloadJson).ToArrayAsync(token);
    }
    internal static async Task<string[]> StagePayloads(Station01HostFixture fixture,Guid runId,CancellationToken token) {
        await using var db=Open(fixture);
        return await db.StageEvents.AsNoTracking().Where(x=>x.RunId==runId).OrderBy(x=>x.Sequence).Select(x=>x.PayloadJson).ToArrayAsync(token);
    }
    internal static async Task ReplaceControlledFrozenInput(Station01HostFixture fixture,Guid writeId,
        string payload,string digest,CancellationToken token) {
        // The owning fixture was created by ApprovedTestRoot/StorePreparation.
        // Only the specifically selected row is changed and the caller restores it.
        await using var db=Open(fixture);
        var row=await db.Writes.SingleAsync(x=>x.WriteId==writeId,token);
        row.PayloadJson=payload;row.PayloadDigest=digest;
        await db.SaveChangesAsync(token);
    }
}
