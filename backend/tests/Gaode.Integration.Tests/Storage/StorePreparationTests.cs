using Gaode.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Gaode.Integration.Tests.Storage;

public sealed partial class StorePreparationTests
{
    [Fact]
    public async Task CreatesOnlyAnEmptyIsolatedTestRootAndRefusesReuse()
    {
        var approved = Path.Combine(Gaode.Testing.ApprovedTestRoot.Resolve(Gaode.Integration.Tests.Support.Station01HostFixture.FindWorkspace()), "store-preparation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(approved);
        var target = Path.Combine(approved, "case");
        await StorePreparation.PrepareEmptyTestStoreAsync(approved, target);
        Assert.True(StoreCompatibilityProbe.Inspect(target).Compatible);
        await Assert.ThrowsAsync<InvalidOperationException>(() => StorePreparation.PrepareEmptyTestStoreAsync(approved, target));
    }

    [Fact]
    public async Task PreparedStoreUsesForeignKeysWalAndExclusiveMaintenanceLock()
    {
        var approved = Path.Combine(Gaode.Testing.ApprovedTestRoot.Resolve(Gaode.Integration.Tests.Support.Station01HostFixture.FindWorkspace()), "store-preparation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(approved);
        var target = await StorePreparation.PrepareEmptyTestStoreAsync(approved, Path.Combine(approved, "case"));
        using var guard = StoreAccessGuard.Acquire(target, approved);
        Assert.Throws<InvalidOperationException>(() => StoreAccessGuard.Acquire(target, approved));
        using var connection = new SqliteConnection(StoreCompatibilityProbe.ReadWriteConnectionString(target));
        connection.Open();
        using var foreignKeys = connection.CreateCommand();
        foreignKeys.CommandText = "PRAGMA foreign_keys";
        Assert.Equal(1L, Convert.ToInt64(foreignKeys.ExecuteScalar()));
        using var journal = connection.CreateCommand();
        journal.CommandText = "PRAGMA journal_mode";
        Assert.Equal("wal", Convert.ToString(journal.ExecuteScalar())?.ToLowerInvariant());
    }

    [Fact]
    public void MissingStoreIsRejectedWithoutCreatingDatabase()
    {
        var approved = Path.Combine(Gaode.Testing.ApprovedTestRoot.Resolve(Gaode.Integration.Tests.Support.Station01HostFixture.FindWorkspace()), "store-preparation-" + Guid.NewGuid().ToString("N"));
        var target = Path.Combine(approved, "missing");
        Directory.CreateDirectory(target);
        Assert.Equal("StoreMissing", StoreCompatibilityProbe.Inspect(target).Code);
        Assert.Throws<InvalidOperationException>(() => StoreCompatibilityProbe.ReadWriteConnectionString(target));
        Assert.False(File.Exists(Path.Combine(target, "station01.test.db")));
    }
}
