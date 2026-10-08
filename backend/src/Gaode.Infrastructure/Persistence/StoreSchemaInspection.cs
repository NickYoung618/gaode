using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Gaode.Infrastructure.Persistence.Migrations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Gaode.Infrastructure.Persistence;

internal sealed record StoreInspection(string State, string Code, Guid? StoreId, string? Version,
    string? Profile, string? StructureDigest, string? BusinessDigest, string? MediaDigest);

// Exact support for the three existing migrations and the single 009 addition.
// This reads actual SQLite metadata; it never executes a migration on a Host query.
internal static class StoreSchemaInspection
{
    internal static readonly string[] SourceMigrations =
        ["202609210001_InitialStation01", "202609220001_StageEventing", "202609230001_Station01MainFlow"];
    internal static Migration[] Migrations(bool target) => target
        ? [new InitialStation01(), new StageEventing(), new Station01MainFlow(), new CommunicationEvidence()]
        : [new InitialStation01(), new StageEventing(), new Station01MainFlow()];
    internal static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    internal static List<object?[]> Rows(SqliteConnection connection, string sql, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand(); command.CommandText = sql; command.Transaction = transaction;
        using var reader = command.ExecuteReader();
        var rows = new List<object?[]>();
        while (reader.Read())
        {
            var values = new object[reader.FieldCount]; reader.GetValues(values);
            rows.Add(values.Select(v => v == DBNull.Value ? null : v).ToArray());
        }
        return rows;
    }
    internal static string Hash(object value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private static string Normalize(string sql) => Regex.Replace(sql.Trim().TrimEnd(';'), @"\s+", " ");
    private static string Text(object? value) => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "";

    internal static StoreInspection Inspect(SqliteConnection connection, string root, bool hashes, string expectedProfile = "Test")
    {
        Guid? storeId = null; string? version = null; string? profile = null;
        try
        {
            var manifests = Rows(connection, "SELECT StoreId,SchemaVersion,Profile,PrepareOperationId,PreparedUtc FROM Manifests");
            if (manifests.Count != 1 || !Guid.TryParse(Text(manifests[0][0]), out var id) || id == Guid.Empty)
                return Bad("ManifestIdentityInvalid");
            storeId = id; version = Text(manifests[0][1]); profile = Text(manifests[0][2]);
            if (expectedProfile is not ("Test" or Gaode.Domain.Configuration.RuntimePurposes.RealDeviceCommissioning) ||
                profile != expectedProfile || expectedProfile != "Test" && version != "s01-store/3" ||
                version is not ("s01-store/1" or "s01-store/2" or "s01-store/3") ||
                !Guid.TryParse(Text(manifests[0][3]), out var preparationId) || preparationId == Guid.Empty ||
                !DateTimeOffset.TryParse(Text(manifests[0][4]), out _)) return Bad("ManifestIncompatible");
            var target = version is "s01-store/2" or "s01-store/3";
            var publicTray = version == "s01-store/3";
            var history = Rows(connection, "SELECT MigrationId,ProductVersion FROM __EFMigrationsHistory ORDER BY MigrationId");
            var migrations = history.Select(r => Text(r[0])).ToArray();
            var expectedHistory = publicTray ? SourceMigrations.Concat([CommunicationEvidence.MigrationId, PublicTrayEnd.MigrationId]).ToArray() :
                target ? SourceMigrations.Append(CommunicationEvidence.MigrationId).ToArray() : SourceMigrations;
            if (!migrations.SequenceEqual(expectedHistory) || history.Any(r => Text(r[1]) != "10.0.12")) return Bad("MigrationSetMismatch");
            if (Text(Rows(connection, "PRAGMA integrity_check").Single()[0]) != "ok" || Rows(connection, "PRAGMA foreign_key_check").Count != 0)
                return Bad("StoreIntegrityInvalid");

            var operations = Migrations(target).Concat(publicTray ? new Migration[] { new PublicTrayEnd() } : [])
                .SelectMany(m => m.UpOperations).ToArray();
            var tables = operations.OfType<CreateTableOperation>().ToDictionary(t => t.Name);
            var names = Rows(connection, "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'")
                .Select(r => Text(r[0])).Order().ToArray();
            var expectedNames = tables.Keys.Concat(["__EFMigrationsHistory", "__EFMigrationsLock"]).Order().ToArray();
            if (!names.SequenceEqual(expectedNames)) return Bad("TableSetMismatch");
            if (Rows(connection, "SELECT name FROM sqlite_master WHERE type='view'").Count != 0) return Bad("UnexpectedView");
            if (!ColumnsMatch("__EFMigrationsHistory", [("MigrationId","TEXT",false,1),("ProductVersion","TEXT",false,0)])) return Bad("HistoryStructureInvalid");
            if (!ColumnsMatch("__EFMigrationsLock", [("Id","INTEGER",false,1),("Timestamp","TEXT",false,0)]) ||
                Rows(connection, "SELECT Id FROM __EFMigrationsLock").Count != 0) return Bad("MigrationLockActiveOrInvalid");
            foreach (var (name, table) in tables)
            {
                var columns = table.Columns.Concat(operations.OfType<AddColumnOperation>().Where(c => c.Table == name)).ToArray();
                var expected = columns.Select(c => (c.Name, c.ColumnType!,
                    operations.OfType<AlterColumnOperation>().LastOrDefault(a => a.Table == name && a.Name == c.Name)?.IsNullable ?? c.IsNullable,
                    Array.IndexOf(table.PrimaryKey!.Columns, c.Name) + 1)).ToArray();
                // SQLite rebuilds this one table from EF's target model for nullable stage references.
                // Keep the old exact shape for v1/v2 and the finite generated ordering for v3.
                if (publicTray && name == "WholeTrayCompletions")
                    expected = expected.OrderBy(c => c.Item4 > 0 ? 0 : 1).ThenBy(c => c.Name, StringComparer.Ordinal).ToArray();
                if (!ColumnsMatch(name, expected)) return Bad("ColumnOrPrimaryKeyMismatch:" + name);
                var actualColumns = Rows(connection, "PRAGMA table_info(" + Quote(name) + ")");
                if (actualColumns.Zip(columns).Any(pair => Text(pair.First[4]) != (pair.Second.DefaultValueSql ??
                    (pair.Second.DefaultValue is string text ? Literal(text) : pair.Second.DefaultValue is null ? "" : Text(pair.Second.DefaultValue)))))
                    return Bad("ColumnDefaultMismatch:" + name);
                var sql = Text(Rows(connection, "SELECT sql FROM sqlite_master WHERE type='table' AND name=" + Literal(name)).Single()[0]);
                foreach (var check in table.CheckConstraints)
                    if (!Normalize(sql).Contains("CONSTRAINT " + Quote(check.Name) + " CHECK (" + Normalize(check.Sql) + ")", StringComparison.Ordinal)) return Bad("CheckConstraintMismatch:" + name);
                var foreign = Rows(connection, "PRAGMA foreign_key_list(" + Quote(name) + ")").GroupBy(r => Convert.ToInt32(r[0]))
                    .Select(group => string.Join("|", Text(group.First()[2]), string.Join(",", group.OrderBy(r => r[1]).Select(r => Text(r[3]))),
                        string.Join(",", group.OrderBy(r => r[1]).Select(r => Text(r[4]))), Text(group.First()[5]), Text(group.First()[6]))).Order().ToArray();
                var expectedForeign = table.ForeignKeys.Select(f => string.Join("|", f.PrincipalTable, string.Join(",", f.Columns),
                    string.Join(",", f.PrincipalColumns!), f.OnUpdate == ReferentialAction.NoAction ? "NO ACTION" : f.OnUpdate.ToString().ToUpperInvariant(),
                    f.OnDelete == ReferentialAction.NoAction ? "NO ACTION" : f.OnDelete.ToString().ToUpperInvariant())).Order().ToArray();
                if (!foreign.SequenceEqual(expectedForeign)) return Bad("ForeignKeyMismatch:" + name);
                var indexes = Rows(connection, "PRAGMA index_list(" + Quote(name) + ")").Where(r => Text(r[3]) != "pk").ToArray();
                var expectedIndexes = operations.OfType<CreateIndexOperation>().Where(i => i.Table == name).ToArray();
                if (indexes.Length != expectedIndexes.Length) return Bad("IndexSetMismatch:" + name);
                foreach (var index in expectedIndexes)
                {
                    var actual = indexes.SingleOrDefault(r => Text(r[1]) == index.Name);
                    if (actual is null || Convert.ToInt64(actual[2]) != (index.IsUnique ? 1 : 0) || Convert.ToInt64(actual[4]) != 0 ||
                        !Rows(connection, "PRAGMA index_info(" + Quote(index.Name) + ")").Select(r => Text(r[2])).SequenceEqual(index.Columns))
                        return Bad("IndexMismatch:" + index.Name);
                }
            }
            var triggers = Rows(connection, "SELECT sql FROM sqlite_master WHERE type='trigger' ORDER BY name").Select(r => Normalize(Text(r[0]))).Order().ToArray();
            if (!triggers.SequenceEqual(operations.OfType<SqlOperation>().Where(s => s.Sql.TrimStart().StartsWith("CREATE TRIGGER", StringComparison.OrdinalIgnoreCase))
                .Select(s => Normalize(s.Sql)).Order())) return Bad("TriggerMismatch");
            var structure = Hash(Rows(connection, "SELECT type,name,tbl_name,sql FROM sqlite_master WHERE name NOT LIKE 'sqlite_%' ORDER BY type,name"));
            string? business = null, media = null;
            if (hashes)
            {
                var contents = tables.Where(kv => kv.Key is not ("Manifests" or "PlcCommunicationEvidence"))
                    .OrderBy(kv => kv.Key).Select(kv => new { table = kv.Key, rows = Rows(connection,
                        "SELECT " + string.Join(",", kv.Value.Columns.Select(c => Quote(c.Name))) + " FROM " + Quote(kv.Key) + " ORDER BY " + string.Join(",", kv.Value.PrimaryKey!.Columns.Select(Quote))) }).ToArray();
                // Preserve every actual SQLite value, including original JSON text and provider spelling.
                business = Hash(contents);
                media = Hash(MediaFacts(connection, root));
            }
            return new(publicTray ? "U3" : target ? "U2" : "U0", "Verified", storeId, version, profile, structure, business, media);
        }
        catch (Exception error) when (error is SqliteException or IOException or InvalidOperationException or FormatException or ArgumentException)
        { return Bad("StoreUnreadableOrInconsistent:" + error.GetType().Name); }

        StoreInspection Bad(string code) => new("UX", code, storeId, version, profile, null, null, null);
        bool ColumnsMatch(string table, (string Name, string Type, bool Nullable, int Key)[] expected)
        {
            var actual = Rows(connection, "PRAGMA table_info(" + Quote(table) + ")");
            return actual.Count == expected.Length && actual.Zip(expected).All(pair => Text(pair.First[1]) == pair.Second.Name &&
                Text(pair.First[2]).ToUpperInvariant() == pair.Second.Type.ToUpperInvariant() &&
                Convert.ToInt64(pair.First[3]) == (pair.Second.Nullable ? 0 : 1) && Convert.ToInt64(pair.First[5]) == pair.Second.Key);
        }
    }
    private static string Literal(string value) => "'" + value.Replace("'", "''") + "'";
    internal static object[] MediaFacts(SqliteConnection connection, string root)
    {
        var mediaRoot = Path.GetFullPath(Path.Combine(root, "media-root"));
        return Rows(connection, "SELECT MediaId,RelativeKey FROM Media ORDER BY MediaId").Select(row =>
        {
            var key = Text(row[1]); var path = Path.GetFullPath(Path.Combine(mediaRoot, key));
            if (!path.StartsWith(mediaRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("MediaReferenceOutsideStore");
            for (var parent = new DirectoryInfo(Path.GetDirectoryName(path)!); parent is not null && parent.FullName.Length >= mediaRoot.Length; parent = parent.Parent)
                if (parent.Exists && (parent.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("MediaReferenceIsLink");
            var exists = File.Exists(path);
            if (exists && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("MediaReferenceIsLink");
            return (object)new { id = Text(row[0]), key, exists, length = exists ? new FileInfo(path).Length : (long?)null,
                sha256 = exists ? Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))) : null };
        }).ToArray();
    }
}
