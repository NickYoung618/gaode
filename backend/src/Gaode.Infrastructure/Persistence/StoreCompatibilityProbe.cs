using Microsoft.Data.Sqlite;

namespace Gaode.Infrastructure.Persistence;

public sealed record StoreCompatibility(bool Compatible, string Code, Guid? StoreId,
    string? SchemaVersion);

public static class StoreCompatibilityProbe
{
    public static StoreCompatibility Inspect(string root, string expectedProfile = "Test")
    {
        var dbPath = Path.Combine(root, "station01.test.db");
        if (!File.Exists(dbPath)) return new(false, "StoreMissing", null, null);
        var block = StoreAccessGuard.AdmissionBlock(root);
        if (block is not null) return new(false, block, null, null);
        try
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = dbPath, Mode = SqliteOpenMode.ReadOnly, Cache = SqliteCacheMode.Private,
                Pooling = false, DefaultTimeout = 1 }.ToString());
            connection.Open();
            var actual = StoreSchemaInspection.Inspect(connection, root, hashes: false, expectedProfile);
            return new(actual.State == "U3", actual.State == "U3" ? "Compatible" : actual.Code == "Verified" ? "SourceStoreRequiresControlledUpgrade" : actual.Code,
                actual.StoreId, actual.Version);
        }
        catch (Exception ex) when (ex is SqliteException or IOException or FormatException)
        {
            return new(false, "StoreUnreadable", null, null);
        }
    }

    public static string ReadWriteConnectionString(string root, string expectedProfile = "Test")
    {
        var check = Inspect(root, expectedProfile);
        if (!check.Compatible) throw new InvalidOperationException(check.Code);
        return new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(root, "station01.test.db"),
            Mode = SqliteOpenMode.ReadWrite, Cache = SqliteCacheMode.Private,
            ForeignKeys = true
        }.ToString();
    }

    public static string ReadOnlyConnectionString(string root, string expectedProfile = "Test")
    {
        var check = Inspect(root, expectedProfile);
        if (!check.Compatible) throw new InvalidOperationException(check.Code);
        return new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(root, "station01.test.db"), Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private, ForeignKeys = true
        }.ToString();
    }
}
