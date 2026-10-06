using Gaode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Gaode.Contracts.Tests.Persistence;

public sealed class ModelContractTests
{
    [Fact]
    public void ModelContainsOnlyStation01RecordsAndRequiredIdentityConstraints()
    {
        var options = new DbContextOptionsBuilder<Station01DbContext>()
            .UseSqlite("Data Source=:memory:").Options;
        using var db = new Station01DbContext(options);
        var names = db.Model.GetEntityTypes().Select(x => x.ClrType.Name).Order().ToArray();
        Assert.Equal(new[] { "AlgorithmCallEntity", "CommandEntity", "CommunicationEvidenceEntity", "ComponentEvidenceMatrixEntity",
            "ControlledRecoveryDecisionEntity", "HandoffEntity", "MediaEntity", "OperationEntity",
            "RunEntity", "StageEventEntity", "StageIdempotencyEntity", "StageProjectionEntity",
            "StoreManifestEntity", "PublicPreparationHandoffV2Entity", "WholeTrayCompletionEntity",
            "WriteEntity" }.Order().ToArray(), names);
        Assert.DoesNotContain(names, x => x.Contains("Recipe", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("Part", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("Sorting", StringComparison.OrdinalIgnoreCase));

        var run = db.Model.FindEntityType(typeof(RunEntity))!;
        Assert.NotNull(run.FindProperty(nameof(RunEntity.TerminalRevision)));
        Assert.Contains(run.GetIndexes(), index => index.Properties.Select(x => x.Name).SequenceEqual([
            nameof(RunEntity.RunId), nameof(RunEntity.TerminalRevision), nameof(RunEntity.Terminal)]));
        var handoff = db.Model.FindEntityType(typeof(HandoffEntity))!;
        Assert.True(handoff.GetIndexes().Single(x =>
            x.Properties.Single().Name == nameof(HandoffEntity.RunId)).IsUnique);
        Assert.Contains(handoff.GetForeignKeys(), x => x.PrincipalEntityType.ClrType == typeof(RunEntity));
        var call = db.Model.FindEntityType(typeof(AlgorithmCallEntity))!;
        foreach (var name in new[] { nameof(AlgorithmCallEntity.IntentWriteId),
            nameof(AlgorithmCallEntity.StartTick), nameof(AlgorithmCallEntity.DueTick),
            nameof(AlgorithmCallEntity.InvocationBasis), nameof(AlgorithmCallEntity.DispatchEvidence) })
            Assert.NotNull(call.FindProperty(name));
    }

    [Fact]
    public void ExactlyOneCheckedInInitialMigrationDefinesTerminalAndHandoffGuards()
    {
        var options = new DbContextOptionsBuilder<Station01DbContext>()
            .UseSqlite("Data Source=:memory:").Options;
        using var db = new Station01DbContext(options);
        var migrations = db.GetService<IMigrationsAssembly>().Migrations;
        Assert.Equal(4, migrations.Count);
        Assert.Contains("202610010001_CommunicationEvidence", migrations.Keys);
        Assert.Contains("202609210001_InitialStation01", migrations.Keys);
        Assert.Contains("202609220001_StageEventing", migrations.Keys);
        Assert.Contains("202609230001_Station01MainFlow", migrations.Keys);
    }
}
