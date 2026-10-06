using Gaode.Application.Ports;
using Gaode.Infrastructure.Persistence;
using Gaode.Infrastructure.Simulation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Gaode.Integration.Tests.Support;

// Actual fixture providers and read-only SQLite projections. These records keep
// the original storage assertions without exposing EF or any communication data.
internal sealed record PreparationCall(Guid CallId,Guid OperationId,Guid CaptureId,Guid IntentWriteId,
    Guid SessionId,string InvocationBasis,string CapabilityId,long StartTick,long DueTick,int BudgetMs,string DispatchEvidence);
internal sealed record PreparationMedia(Guid MediaId,Guid CaptureId,long ByteLength);
internal sealed record PreparationReadback(PreparationCall[] Calls,PreparationMedia[] Media,long HandoffRevision,int LegacyHandoffs);
internal sealed record TrayStageFact(Guid EventId,Guid RunId,Guid TrayId,Guid OperationId,long ConnectionEpoch,
    string Stage,string EventType,string PayloadJson,string PayloadDigest,DateTimeOffset PersistedUtc);
internal sealed record TrayWriteFact(Guid WriteId,string Kind,string PayloadJson,long Revision);
internal sealed record TrayCompletionFact(Guid CompletionId,Guid? DetectionCompletedEventId,Guid? SortingCompletedEventId);
internal sealed record TrayReadback(TrayStageFact[] Stages,TrayWriteFact[] Writes,TrayCompletionFact[] Completions);
internal static class PublicPreparationTestStore
{
    internal static async Task<TrayReadback> ReadTray(RecipeExecution010RunHarness driver,Guid runId) {
        // The driver has admitted and owns this store. Offline compatibility
        // probing deliberately rejects another active Host; the fixture reads
        // its own live database through SQLite's read-only connection instead.
        var connection=new SqliteConnectionStringBuilder {DataSource=Path.Combine(driver.StoreRoot,"station01.test.db"),
            Mode=SqliteOpenMode.ReadOnly,Cache=SqliteCacheMode.Private,Pooling=false}.ToString();
        var options=new DbContextOptionsBuilder<Station01DbContext>().UseSqlite(connection).Options;
        await using var db=new Station01DbContext(options);
        var stages=await db.StageEvents.AsNoTracking().Where(x=>x.RunId==runId).Select(x=>
            new TrayStageFact(x.EventId,x.RunId,x.TrayId,x.OperationId,x.ConnectionEpoch,x.Stage,x.EventType,x.PayloadJson,x.PayloadDigest,x.PersistedUtc)).ToArrayAsync();
        var writes=await db.Writes.AsNoTracking().Where(x=>x.RunId==runId).OrderBy(x=>x.Revision)
            .Select(x=>new TrayWriteFact(x.WriteId,x.Kind,x.PayloadJson,x.Revision)).ToArrayAsync();
        var completions=await db.WholeTrayCompletions.AsNoTracking().Where(x=>x.RunId==runId)
            .Select(x=>new TrayCompletionFact(x.WholeTrayCompletionId,x.DetectionCompletedEventId,x.SortingCompletedEventId)).ToArrayAsync();
        return new(stages.OrderBy(x=>x.PersistedUtc).ToArray(),writes,completions);
    }
    internal static ICapturePort Capture(Station01HostFixture fixture)=>Assert.IsType<SimulatedCapture>(fixture.Host.Services.GetRequiredService<ICapturePort>());
    internal static IAlgorithmPort Algorithm(Station01HostFixture fixture)=>Assert.IsType<SimulatedAlgorithm>(fixture.Host.Services.GetRequiredService<IAlgorithmPort>());
    internal static async Task<PreparationReadback> Read(Station01HostFixture fixture,Guid runId) {
        var options=new DbContextOptionsBuilder<Station01DbContext>().UseSqlite(StoreCompatibilityProbe.ReadOnlyConnectionString(fixture.StoreRoot)).Options;
        await using var db=new Station01DbContext(options);
        var calls=await db.AlgorithmCalls.AsNoTracking().Where(x=>x.RunId==runId).OrderBy(x=>x.StartTick)
            .Select(x=>new PreparationCall(x.CallId,x.OperationId,x.CaptureId,x.IntentWriteId,x.SessionId,x.InvocationBasis,
                x.CapabilityId,x.StartTick,x.DueTick,x.BudgetMs,x.DispatchEvidence)).ToArrayAsync();
        var media=await db.Media.AsNoTracking().Where(x=>x.RunId==runId).Select(x=>new PreparationMedia(x.MediaId,x.CaptureId,x.ByteLength)).ToArrayAsync();
        var handoff=await db.PublicPreparationHandoffsV2.SingleAsync(x=>x.RunId==runId);
        return new(calls,media,handoff.Revision,await db.Handoffs.CountAsync(x=>x.RunId==runId));
    }
}
