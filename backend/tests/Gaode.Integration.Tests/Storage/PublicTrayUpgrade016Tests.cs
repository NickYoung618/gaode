using System.Security.Cryptography;
using System.Text;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Persistence;
using Gaode.Infrastructure.Persistence.Migrations;
using Gaode.Integration.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Gaode.Integration.Tests.Storage;

public sealed class PublicTrayUpgrade016Tests
{
    [Fact]
    public async Task ControlledV2UpgradePreservesStoredUnicodeRowsAndOldMissingInspectionIsUnknown()
    {
        var repo=Station01HostFixture.FindWorkspace();var allowed=Path.Combine(repo,"artifacts","016-public-tray-flow");
        var root=Path.Combine(allowed,"upgrade-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        var path=Path.Combine(root,"station01.test.db");
        var options=new DbContextOptionsBuilder<Station01DbContext>().UseSqlite($"Data Source={path};Pooling=False").Options;
        var storeId=Guid.NewGuid();var runId=Guid.NewGuid();
        const string original="{\"kind\":\"历史原文\",\"quality\":\"RecordedClaim\"}";
        await using(var db=new Station01DbContext(options)) {
            var name=db.GetService<IMigrationsIdGenerator>().GetName(CommunicationEvidence.MigrationId);
            await db.GetService<IMigrator>().MigrateAsync(name);
            db.Manifests.Add(new(){StoreId=storeId,Profile="Test",SchemaVersion="s01-store/2",PrepareOperationId=Guid.NewGuid(),PreparedUtc=DateTimeOffset.UtcNow});
            db.Runs.Add(new(){RunId=runId,RequestId="016-history",SubjectId="test",State=RunState.Created,Revision=1,CreatedUtc=DateTimeOffset.UtcNow});
            db.Writes.Add(new(){RunId=runId,WriteId=Guid.NewGuid(),Revision=1,Kind="Audit",PayloadJson=original,
                PayloadDigest=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(original))),CommittedUtc=DateTimeOffset.UtcNow});
            await db.SaveChangesAsync();
        }
        StoreInspection before;
        using(var c=new SqliteConnection($"Data Source={path};Pooling=False")){c.Open();before=StoreSchemaInspection.Inspect(c,root,true);}
        Assert.Equal("U2",before.State);Assert.False(StoreCompatibilityProbe.Inspect(root).Compatible);
        var result=StoreMaintenance.UpgradePublicTrayTest(allowed,root);
        Assert.Equal("U3",result.State);Assert.Equal(storeId,result.StoreId);Assert.True(result.DdlExecuted>0);
        using(var c=new SqliteConnection($"Data Source={path};Pooling=False")) {
            c.Open();var after=StoreSchemaInspection.Inspect(c,root,true);
            Assert.Equal("U3",after.State);Assert.Equal(before.BusinessDigest,after.BusinessDigest);Assert.Equal(before.MediaDigest,after.MediaDigest);
        }
        Assert.True(StoreCompatibilityProbe.Inspect(root).Compatible);
        await using(var db=new Station01DbContext(options))Assert.Equal(original,(await db.Writes.AsNoTracking().SingleAsync()).PayloadJson);
        Assert.Equal(0,StoreMaintenance.UpgradePublicTrayTest(allowed,root).DdlExecuted);
        var historical=new WholeTrayCompletionReference(Guid.NewGuid(),runId,Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"old",
            Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid());
        Assert.True(historical.IsValid);Assert.Null(historical.InspectionCompleted);
    }
}
