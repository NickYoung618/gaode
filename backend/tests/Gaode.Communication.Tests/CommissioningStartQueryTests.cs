using Gaode.Application.Station01;
using Gaode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace Gaode.Communication.Tests;
public sealed class CommissioningStartQueryTests
{
    [Fact]
    public async Task PersistedLookupIsSubjectScopedAndDatabaseFailureIsNotNotFound()
    {
        var path=Path.Combine(Path.GetTempPath(),"021-query-"+Guid.NewGuid().ToString("N")+".db");
        var options=new DbContextOptionsBuilder<Station01DbContext>().UseSqlite("Data Source="+path).Options;
        try {
            await using(var db=new Station01DbContext(options)){await db.Database.EnsureCreatedAsync();
                db.Commands.Add(new(){CommandId=Guid.NewGuid(),RunId=Guid.NewGuid(),SubjectId="owner",RequestId="lost",ReceiptState="Committed"});await db.SaveChangesAsync();}
            var query=new TraceQuery(options);var receipt=await query.GetStartReceiptAsync("owner","lost",default);
            Assert.NotNull(receipt);Assert.Equal("Committed",receipt.ReceiptDurability);
            Assert.Null(await query.GetStartReceiptAsync("other","lost",default));
            var invalid=new TraceQuery(new DbContextOptionsBuilder<Station01DbContext>().UseSqlite("Data Source=:memory:").Options);
            await Assert.ThrowsAnyAsync<Exception>(()=>invalid.GetStartReceiptAsync("owner","lost",default));
        } finally {Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();File.Delete(path);}
    }
    [Fact]
    public void LookupDoesNotRegisterOrReleaseAndOldCompletionCannotReleaseNewOwner()
    {
        var registry=new CommandRegistry();Assert.Null(registry.FindStart("owner","lost"));
        var first=registry.Register("owner","one","one");Assert.Null(registry.FindStart("other","one"));
        Assert.Throws<InvalidOperationException>(()=>registry.Register("owner","two","two"));
        registry.ReleaseAfterCommittedFinal(first.RunId);
        var second=registry.Register("owner","two","two");registry.ReleaseAfterCommittedFinal(first.RunId);
        Assert.Throws<InvalidOperationException>(()=>registry.Register("owner","three","three"));
        Assert.Equal(second,registry.FindStart("owner","two"));
    }
}
