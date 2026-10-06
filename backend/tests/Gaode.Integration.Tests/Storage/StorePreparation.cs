using Gaode.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;
using Gaode.Infrastructure.Persistence.Migrations;

namespace Gaode.Integration.Tests.Storage;

public static class StorePreparation
{
    public static async Task<string> PrepareEmptyTestStoreAsync(string approvedRoot, string targetRoot)
    {
        if (!Path.IsPathFullyQualified(approvedRoot) || !Path.IsPathFullyQualified(targetRoot))
            throw new InvalidOperationException("Test根必须使用绝对路径");
        var full = Path.GetFullPath(targetRoot);
        if (Directory.Exists(full) && Directory.EnumerateFileSystemEntries(full).Any())
            throw new InvalidOperationException("已存在的Test根不能覆盖或自动升级");
        using var guard = StoreAccessGuard.Acquire(full, approvedRoot);
        var database = Path.Combine(full, "station01.test.db");
        if (File.Exists(database)) throw new InvalidOperationException("已有数据库不能覆盖");
        var connection = new SqliteConnectionStringBuilder
        // Match the formal StorePrep process boundary: preparation must leave no
        // pooled WAL connection alive when the independent Test Host takes over.
        { DataSource = database, Mode = SqliteOpenMode.ReadWriteCreate, ForeignKeys = true, Pooling = false }.ToString();
        var options = new DbContextOptionsBuilder<Station01DbContext>().UseSqlite(connection).Options;
        await using (var db = new Station01DbContext(options))
        {
            var differ = db.GetService<IMigrationsModelDiffer>();
            var previous = db.GetService<IModelRuntimeInitializer>()
                .Initialize(db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model)
                .GetRelationalModel();
            var actual = db.GetService<IDesignTimeModel>().Model.GetRelationalModel();
            var changes = differ.GetDifferences(previous, actual);
            if (changes.Count > 0)
                throw new InvalidOperationException("初始迁移与模型不同: previous=" +
                    string.Join(",", previous.Tables.Select(x => x.Name)) + " actual=" +
                    string.Join(",", actual.Tables.Select(x => x.Name)) + ";" +
                    string.Join(";", changes.Select(x => x is Microsoft.EntityFrameworkCore.Migrations.Operations.AlterColumnOperation c
                        ? $"{c.Table}.{c.Name}:{c.OldColumn.ColumnType}/{c.OldColumn.IsNullable}->{c.ColumnType}/{c.IsNullable}"
                        : x.GetType().Name)));
            await db.Database.MigrateAsync();
            db.Manifests.Add(new StoreManifestEntity
            {
                StoreId = Guid.NewGuid(), SchemaVersion = "s01-store/3", Profile = "Test",
                PrepareOperationId = Guid.NewGuid(), PreparedUtc = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }
        Directory.CreateDirectory(Path.Combine(full, "media-root"));
        var result = StoreCompatibilityProbe.Inspect(full);
        if (!result.Compatible) throw new InvalidOperationException("新准备的Test库不兼容: " + result.Code);
        return full;
    }
}
