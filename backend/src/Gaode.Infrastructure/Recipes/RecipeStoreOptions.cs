using Microsoft.Data.Sqlite;

namespace Gaode.Infrastructure.Recipes;

public sealed class RecipeStoreOptions
{
    public string DatabasePath { get; init; } = "";
    public int ReadWriteTimeoutMs { get; init; }
    public int DbLockTimeoutSeconds { get; init; }

    public void Validate(string? runtimeDatabasePath = null)
    {
        if (!Path.IsPathFullyQualified(DatabasePath) || ReadWriteTimeoutMs <= 0 || DbLockTimeoutSeconds <= 0 ||
            (long)DbLockTimeoutSeconds * 1000 > ReadWriteTimeoutMs)
            throw new InvalidOperationException("RecipeStore requires an absolute path and explicit finite read/write and lock budgets.");
        if (runtimeDatabasePath is not null && string.Equals(Path.GetFullPath(DatabasePath),
                Path.GetFullPath(runtimeDatabasePath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("RecipeStore must not use the runtime database.");
    }

    internal string ConnectionString(bool readOnly = false) => new SqliteConnectionStringBuilder
    {
        DataSource = Path.GetFullPath(DatabasePath),
        Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWrite,
        ForeignKeys = true,
        DefaultTimeout = DbLockTimeoutSeconds,
        Pooling = false
    }.ToString();
}
