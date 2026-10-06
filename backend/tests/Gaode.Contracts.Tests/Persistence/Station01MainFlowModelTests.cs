using Gaode.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace Gaode.Contracts.Tests.Persistence;

public sealed class Station01MainFlowModelTests
{
    [Fact]
    public void ModelHasDeadlineMatrixDecisionCompletionAndImmutableReferenceConstraints()
    {
        var options = new DbContextOptionsBuilder<Station01DbContext>()
            .UseSqlite("Data Source=:memory:").Options;
        using var db = new Station01DbContext(options);
        var model = db.GetService<IDesignTimeModel>().Model;

        var stageEvent = model.FindEntityType(typeof(StageEventEntity))!;
        Assert.NotNull(stageEvent.FindProperty(nameof(StageEventEntity.PlanRevision)));
        Assert.NotNull(stageEvent.FindProperty(nameof(StageEventEntity.StageStartedUtc)));
        Assert.NotNull(stageEvent.FindProperty(nameof(StageEventEntity.StageDeadlineUtc)));

        var matrix = model.FindEntityType(typeof(ComponentEvidenceMatrixEntity))!;
        Assert.Contains(matrix.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(x => x.Name).SequenceEqual([
                nameof(ComponentEvidenceMatrixEntity.RunId), nameof(ComponentEvidenceMatrixEntity.TrayId),
                nameof(ComponentEvidenceMatrixEntity.Milestone)]));
        Assert.Contains(matrix.GetCheckConstraints(), x => x.Name == "CK_ComponentEvidenceMatrices_Retention");
        Assert.Equal(PropertySaveBehavior.Throw,
            matrix.FindProperty(nameof(ComponentEvidenceMatrixEntity.MatrixDigest))!.GetAfterSaveBehavior());

        var decision = model.FindEntityType(typeof(ControlledRecoveryDecisionEntity))!;
        Assert.Contains(decision.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(x => x.Name).SequenceEqual([
                nameof(ControlledRecoveryDecisionEntity.RunId), nameof(ControlledRecoveryDecisionEntity.RequestId)]));
        Assert.Contains(decision.GetCheckConstraints(), x => x.Name == "CK_ControlledRecoveryDecisions_Retention");
        Assert.Equal(PropertySaveBehavior.Throw,
            decision.FindProperty(nameof(ControlledRecoveryDecisionEntity.EvidenceReferencesJson))!.GetAfterSaveBehavior());

        var completion = model.FindEntityType(typeof(WholeTrayCompletionEntity))!;
        Assert.Contains(completion.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(x => x.Name).SequenceEqual([
                nameof(WholeTrayCompletionEntity.RunId), nameof(WholeTrayCompletionEntity.PersistedRevision)]));
        Assert.Equal(3, completion.GetForeignKeys().Count(x =>
            x.PrincipalEntityType.ClrType == typeof(StageEventEntity)));
        Assert.Contains(completion.GetForeignKeys(), x =>
            x.PrincipalEntityType.ClrType == typeof(ComponentEvidenceMatrixEntity));
        foreach (var property in new[]
        {
            nameof(WholeTrayCompletionEntity.DetectionCompletedEventId),
            nameof(WholeTrayCompletionEntity.SortingCompletedEventId),
            nameof(WholeTrayCompletionEntity.UnloadPreparationCompletedEventId),
            nameof(WholeTrayCompletionEntity.SourceMatrixId),
            nameof(WholeTrayCompletionEntity.PersistedRevision)
        })
            Assert.Equal(PropertySaveBehavior.Throw,
                completion.FindProperty(property)!.GetAfterSaveBehavior());
    }

    [Fact]
    public async Task ControlledMigrationCreatesAllMainFlowTables()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<Station01DbContext>().UseSqlite(connection).Options;
        await using var db = new Station01DbContext(options);

        var migrations = db.GetService<IMigrationsAssembly>();
        var differ = db.GetService<IMigrationsModelDiffer>();
        var designModel = db.GetService<IDesignTimeModel>().Model;
        var snapshotModel = db.GetService<IModelRuntimeInitializer>()
            .Initialize(migrations.ModelSnapshot!.Model, designTime: true);
        var differences = differ.GetDifferences(
            snapshotModel.GetRelationalModel(), designModel.GetRelationalModel());
        Assert.True(differences.Count == 0,
            string.Join(Environment.NewLine, differences.Select(x => x is CreateIndexOperation index
                ? $"CreateIndex:{index.Table}:{index.Name}:{string.Join(',', index.Columns)}"
                : x.GetType().Name)));

        await db.Database.MigrateAsync();

        var applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.Contains("202609230001_Station01MainFlow", applied);
        Assert.Equal("202610010001_CommunicationEvidence", applied.Last());
        foreach (var table in new[]
        {
            "ComponentEvidenceMatrices", "ControlledRecoveryDecisions", "WholeTrayCompletions",
            "PublicPreparationHandoffsV2", "PlcCommunicationEvidence"
        })
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=$name";
            command.Parameters.AddWithValue("$name", table);
            Assert.Equal(1L, (long)(await command.ExecuteScalarAsync())!);
        }
    }

    [Fact]
    public async Task CompatibilityProbeRejectsOldSchemaWithoutSilentlyMigratingIt()
    {
        var root = Path.Combine(Path.GetTempPath(), "gaode-main-flow-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var database = Path.Combine(root, "station01.test.db");
            var options = new DbContextOptionsBuilder<Station01DbContext>()
                .UseSqlite($"Data Source={database}").Options;
            await using (var db = new Station01DbContext(options))
            {
                await db.Database.MigrateAsync();
                db.Manifests.Add(new StoreManifestEntity
                {
                    StoreId = Guid.NewGuid(), SchemaVersion = "s01-store/1", Profile = "Test",
                    PrepareOperationId = Guid.NewGuid(), PreparedUtc = DateTimeOffset.UtcNow
                });
                await db.SaveChangesAsync();
                await db.Database.ExecuteSqlRawAsync("DROP TABLE WholeTrayCompletions");
                await db.Database.ExecuteSqlRawAsync("DROP TABLE ControlledRecoveryDecisions");
                await db.Database.ExecuteSqlRawAsync("DROP TABLE ComponentEvidenceMatrices");
                await db.Database.ExecuteSqlRawAsync("DROP TABLE PublicPreparationHandoffsV2");
                await db.Database.ExecuteSqlRawAsync(
                    "DELETE FROM __EFMigrationsHistory WHERE MigrationId='202609230001_Station01MainFlow'");
            }

            var compatibility = StoreCompatibilityProbe.Inspect(root);

            Assert.False(compatibility.Compatible);
            Assert.Equal("MigrationSetMismatch", compatibility.Code);
            await using var connection = new SqliteConnection($"Data Source={database}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ControlledRecoveryDecisions'";
            Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }
}
