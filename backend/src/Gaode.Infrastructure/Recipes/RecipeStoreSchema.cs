using Gaode.Infrastructure.Persistence;
using Gaode.Infrastructure.Recipes.Migrations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Gaode.Infrastructure.Recipes;

// Only the maintenance command creates/migrates the separate recipe store.
public static class RecipeStoreSchema
{
    private const int MaintenanceTimeoutSeconds = 10;
    public static void Prepare(string allowedRoot, string newRoot)
    {
        if (Directory.Exists(newRoot) && Directory.EnumerateFileSystemEntries(newRoot).Any())
            throw new InvalidOperationException("ExistingRecipeRootCannotBePreparedOrOverwritten");
        using var ownership = StoreAccessGuard.Acquire(newRoot, allowedRoot);
        var path = Path.Combine(ownership.Root, "recipes.db");
        var connection = new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate, ForeignKeys = true,
            DefaultTimeout = MaintenanceTimeoutSeconds, Pooling = false
        }.ToString();
        using (var database = new RecipeStoreDbContext(new DbContextOptionsBuilder<RecipeStoreDbContext>()
                   .UseSqlite(connection).Options)) database.Database.Migrate();
        Verify(path, MaintenanceTimeoutSeconds);
    }

    public static void Inspect(string allowedRoot, string root)
    {
        var path = Path.Combine(root, "recipes.db");
        if (!File.Exists(path)) throw new InvalidOperationException("RecipeStoreMissing");
        using var ownership = StoreAccessGuard.Acquire(root, allowedRoot);
        Verify(path, MaintenanceTimeoutSeconds);
    }

    // Host/readers call only this read-only inspection, never Prepare/Migrate/EnsureCreated.
    public static void Verify(string path, int timeoutSeconds)
    {
        if (!File.Exists(path)) throw new InvalidOperationException("RecipeStoreMissing");
        if (timeoutSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadOnly, DefaultTimeout = timeoutSeconds, Pooling = false
        }.ToString());
        connection.Open();
        List<object[]> Rows(string sql)
        {
            using var command = connection.CreateCommand(); command.CommandText = sql; command.CommandTimeout = timeoutSeconds;
            using var reader = command.ExecuteReader(); var rows = new List<object[]>();
            while (reader.Read()) { var row = new object[reader.FieldCount]; reader.GetValues(row); rows.Add(row); }
            return rows;
        }
        static string Text(object value) => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "";
        static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
        try
        {
            var tables = Rows("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'")
                .Select(r => Text(r[0])).Order(StringComparer.Ordinal).ToArray();
            if (!tables.SequenceEqual(new[] { "RecipeHead", "RecipeSavedContent", "__EFMigrationsHistory", "__EFMigrationsLock" }.Order(StringComparer.Ordinal)))
                throw new InvalidOperationException("RecipeStoreTableSetMismatch");
            if (!Rows("SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId")
                    .Select(r => Text(r[0])).SequenceEqual([InitialRecipeStore.MigrationId]) ||
                Rows("SELECT Id FROM __EFMigrationsLock").Count != 0)
                throw new InvalidOperationException("RecipeStoreMigrationMismatch");
            var operations = new InitialRecipeStore().UpOperations;
            foreach (var table in operations.OfType<CreateTableOperation>())
            {
                var columns = Rows("PRAGMA table_info(" + Quote(table.Name) + ")");
                var expected = table.Columns.Select(c => $"{c.Name}|{c.ColumnType}|{(c.IsNullable ? 0 : 1)}|{Array.IndexOf(table.PrimaryKey!.Columns, c.Name) + 1}");
                if (!columns.Select(r => $"{r[1]}|{r[2]}|{r[3]}|{r[5]}").SequenceEqual(expected))
                    throw new InvalidOperationException("RecipeStoreColumnsMismatch:" + table.Name);
                var actualIndexes = Rows("PRAGMA index_list(" + Quote(table.Name) + ")").Where(r => Text(r[3]) != "pk").ToArray();
                var indexes = operations.OfType<CreateIndexOperation>().Where(i => i.Table == table.Name).ToArray();
                if (actualIndexes.Length != indexes.Length) throw new InvalidOperationException("RecipeStoreIndexSetMismatch");
                foreach (var index in indexes)
                {
                    var found = actualIndexes.SingleOrDefault(r => Text(r[1]) == index.Name);
                    var keys = Rows("PRAGMA index_xinfo(" + Quote(index.Name) + ")").Where(r => Convert.ToInt32(r[5]) == 1).ToArray();
                    if (found is null || Convert.ToInt32(found[2]) != (index.IsUnique ? 1 : 0) || Convert.ToInt32(found[4]) != 0 ||
                        !keys.Select(r => Text(r[2])).SequenceEqual(index.Columns) || keys.Any(r => Text(r[4]) != "BINARY"))
                        throw new InvalidOperationException("RecipeStoreIndexMismatch:" + index.Name);
                }
                var foreign = Rows("PRAGMA foreign_key_list(" + Quote(table.Name) + ")")
                    .OrderBy(r => Convert.ToInt32(r[1])).Select(r => $"{r[2]}|{r[3]}|{r[4]}|{r[6]}");
                var expectedForeign = table.ForeignKeys.SelectMany(f => f.Columns.Select((column, i) =>
                    $"{f.PrincipalTable}|{column}|{f.PrincipalColumns![i]}|RESTRICT"));
                if (!foreign.SequenceEqual(expectedForeign)) throw new InvalidOperationException("RecipeStoreForeignKeyMismatch");
            }
        }
        catch (SqliteException error) { throw new InvalidOperationException("RecipeStoreSchemaUnavailable", error); }
    }
}
