using System.Security.Cryptography;
using System.Text.Json;
using Gaode.Infrastructure.Persistence.Migrations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Gaode.Infrastructure.Persistence;

public sealed record StoreMaintenanceResult(string State, string Code, Guid? StoreId, int DdlExecuted,
    string? EvidenceDirectory);

// A single controlled Test upgrade. The Host cannot call this through its admission/query path.
public static class StoreMaintenance
{
    public static StoreMaintenanceResult UpgradePublicTrayTest(string allowedRoot, string root)
    {
        using var guard = StoreAccessGuard.AcquireMaintenance(root, allowedRoot);
        root = guard.Root;
        var path = Path.Combine(root, "station01.test.db");
        using var connection = Open(path, readOnly: false);
        RequireDurableSettings(connection);
        var before = StoreSchemaInspection.Inspect(connection, root, hashes: true);
        if (before.State == "U3") return new("U3", "AlreadyCurrent_NoDdl", before.StoreId, 0, null);
        if (before.State != "U2" || guard.ReadMaintenanceIntent().Length > 0)
            throw new InvalidOperationException("VerifiedV2StoreAndNoPendingMaintenanceRequired");
        var directory = Path.Combine(root, ".016-maintenance", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var backupPath = Path.Combine(directory, "source-v2.db");
        using (var backup = Open(backupPath, readOnly: false, create: true)) connection.BackupDatabase(backup);
        using (var backup = Open(backupPath, readOnly: true))
            if (StoreSchemaInspection.Inspect(backup, root, hashes: true) != before)
                throw new InvalidOperationException("PublicTrayUpgradeBackupMismatch");
        guard.WriteMaintenanceIntent(JsonSerializer.Serialize(new { kind = "PublicTrayEndUpgrade", backupPath,
            backupSha256 = FileHash(backupPath), source = before }, Json));
        var executedCommands = new List<string>();
        var options = new DbContextOptionsBuilder<Station01DbContext>().UseSqlite(connection)
            .LogTo(executedCommands.Add, [Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.CommandExecuted],
                Microsoft.Extensions.Logging.LogLevel.Information).Options;
        using (var db = new Station01DbContext(options)) db.Database.Migrate();
        var ddlCount = executedCommands.Sum(command => System.Text.RegularExpressions.Regex.Matches(command,
            @"\b(?:CREATE\s+(?:UNIQUE\s+)?(?:TABLE|INDEX)|ALTER\s+TABLE|DROP\s+TABLE)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count);
        var after = StoreSchemaInspection.Inspect(connection, root, hashes: true);
        if (after.State != "U3" || after.StoreId != before.StoreId || after.BusinessDigest != before.BusinessDigest ||
            after.MediaDigest != before.MediaDigest) throw new InvalidOperationException("PublicTrayUpgradeVerificationFailed");
        File.WriteAllText(Path.Combine(directory, "receipt.json"), JsonSerializer.Serialize(new { before, after,
            ddlCount, executedCommands, backupSha256 = FileHash(backupPath) }, Json));
        guard.CompleteVerifiedMaintenance();
        return new("U3", "PublicTrayEndUpgraded_HistoricalValuesPreserved", after.StoreId, ddlCount, directory);
    }
    private sealed record Intent(Guid OperationId, string BackupSha256, StoreInspection Source,
        string[] ManifestIdentity);
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static StoreMaintenanceResult UpgradeTest(string allowedRoot, string root,
        bool reconcileOnly = false, Action<string>? testCheckpoint = null)
    {
        using var guard = StoreAccessGuard.AcquireMaintenance(root, allowedRoot);
        root = guard.Root;
        var path = Path.Combine(root, "station01.test.db");
        if (!File.Exists(path)) return new("UX", "StoreMissing", null, 0, null);
        var pending = guard.ReadMaintenanceIntent();
        var checkpoint = testCheckpoint ?? (_ => { });
        if (pending.Length > 0) checkpoint("before-reconciliation");
        Intent intent;
        using var connection = Open(path, readOnly: false);
        RequireDurableSettings(connection);
        // Opening the actual file permits SQLite's own journal/WAL recovery. Never delete recovery files.
        var actual = StoreSchemaInspection.Inspect(connection, root, hashes: true);
        if (pending.Length > 0)
        {
            intent = JsonSerializer.Deserialize<Intent>(pending) ?? throw new InvalidOperationException("UnclassifiedMaintenanceIntent");
            var validation = VerifyBackupAndOldData(root, connection, intent, actual);
            if (validation is not null) return new("UX", validation, actual.StoreId, 0, EvidenceRoot(root, intent.OperationId));
            // U1 only becomes U0/U2 after complete actual structure, identity and old-data verification.
            WriteEvidence(root, intent.OperationId, "reconciliation", actual);
            if (actual.State == "U2")
            {
                guard.CompleteVerifiedMaintenance();
                return new("U2", "TargetVerified_NoRepeatedDdl", actual.StoreId, 0, EvidenceRoot(root, intent.OperationId));
            }
            if (actual.State != "U0") return new("UX", actual.Code, actual.StoreId, 0, EvidenceRoot(root, intent.OperationId));
            if (reconcileOnly) return new("U0", "SourceVerified_ExplicitUpgradeStillRequired", actual.StoreId, 0, EvidenceRoot(root, intent.OperationId));
            // Successful open+exclusive directory ownership, complete source/backup recheck and a new
            // non-deferred SQLite transaction below are required before redoing this one DDL operation.
        }
        else
        {
            if (actual.State == "U2") return new("U2", "AlreadyTarget_NoDdl", actual.StoreId, 0, null);
            if (actual.State != "U0" || reconcileOnly) return new(actual.State, actual.Code, actual.StoreId, 0, null);
            var operation = Guid.NewGuid();
            var directory = EvidenceRoot(root, operation); Directory.CreateDirectory(directory);
            var backupPath = Path.Combine(directory, "source-backup.db");
            using (var backup = Open(backupPath, readOnly: false, create: true)) connection.BackupDatabase(backup);
            using (var backup = Open(backupPath, readOnly: true))
            {
                var verified = StoreSchemaInspection.Inspect(backup, root, hashes: true);
                if (verified != actual) throw new InvalidOperationException("SourceBackupVerificationFailed");
            }
            intent = new(operation, FileHash(backupPath), actual, ManifestIdentity(connection));
            WriteEvidence(root, operation, "source-and-backup", intent);
            // Persist only the unfinished-maintenance latch in the existing lock file. Manifests remains in SQLite.
            guard.WriteMaintenanceIntent(JsonSerializer.Serialize(intent, Json));
        }

        var ddlCount = 0;
        var options = new DbContextOptionsBuilder<Station01DbContext>().UseSqlite(connection).Options;
        using var db = new Station01DbContext(options);
        var migration = new CommunicationEvidence();
        var operations = migration.UpOperations;
        if (operations.Any(o => o is not CreateTableOperation and not CreateIndexOperation) ||
            operations.OfType<CreateTableOperation>().Any(o => o.Name != "PlcCommunicationEvidence") ||
            operations.OfType<CreateIndexOperation>().Any(o => o.Table != "PlcCommunicationEvidence"))
            throw new InvalidOperationException("UpgradeOutsideSingleAddition");
        var commands = db.GetService<IMigrationsSqlGenerator>().Generate(operations);
        if (commands.Any(c => c.TransactionSuppressed || c.CommandText.Contains("PRAGMA", StringComparison.OrdinalIgnoreCase) ||
            c.CommandText.Contains("VACUUM", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("UpgradeRequiresSingleTransaction");
        checkpoint("before-transaction");
        using (var transaction = connection.BeginTransaction(deferred: false))
        {
            foreach (var sql in commands)
            {
                using var command = connection.CreateCommand(); command.Transaction = transaction;
                command.CommandText = sql.CommandText; command.ExecuteNonQuery();
                ddlCount++;
                WriteEvidence(root, intent.OperationId, "ddl-" + ddlCount, new { command.CommandText, executedAtUtc = DateTimeOffset.UtcNow });
            }
            checkpoint("after-ddl");
            using (var history = connection.CreateCommand())
            {
                history.Transaction = transaction;
                history.CommandText = db.GetService<IHistoryRepository>().GetInsertScript(new HistoryRow(CommunicationEvidence.MigrationId, "10.0.12"));
                history.ExecuteNonQuery();
            }
            checkpoint("after-history");
            using (var manifest = connection.CreateCommand())
            {
                manifest.Transaction = transaction;
                manifest.CommandText = "UPDATE Manifests SET SchemaVersion='s01-store/2' WHERE StoreId=$id AND Profile='Test' AND SchemaVersion='s01-store/1'";
                manifest.Parameters.AddWithValue("$id", intent.Source.StoreId!.Value);
                if (manifest.ExecuteNonQuery() != 1) throw new InvalidOperationException("ManifestConditionalUpdateFailed");
            }
            checkpoint("after-manifest");
            transaction.Commit();
            // Any interruption here leaves the durable maintenance latch set. No caller infers rollback.
            checkpoint("after-commit-before-receipt");
        }
        actual = StoreSchemaInspection.Inspect(connection, root, hashes: true);
        var failure = VerifyBackupAndOldData(root, connection, intent, actual);
        if (actual.State != "U2" || failure is not null)
            return new("UX", failure ?? actual.Code, actual.StoreId, ddlCount, EvidenceRoot(root, intent.OperationId));
        WriteEvidence(root, intent.OperationId, "target-verified", actual);
        guard.CompleteVerifiedMaintenance();
        return new("U2", "UpgradedAndVerified", actual.StoreId, ddlCount, EvidenceRoot(root, intent.OperationId));
    }

    public static StoreMaintenanceResult RestoreVerifiedSourceTest(string allowedRoot, string root)
    {
        using var guard = StoreAccessGuard.AcquireMaintenance(root, allowedRoot);
        root = guard.Root;
        var intent = JsonSerializer.Deserialize<Intent>(guard.ReadMaintenanceIntent()) ?? throw new InvalidOperationException("TrustedMaintenanceIntentRequired");
        var backupPath = Path.Combine(EvidenceRoot(root, intent.OperationId), "source-backup.db");
        if (FileHash(backupPath) != intent.BackupSha256) throw new InvalidOperationException("BackupDigestMismatch");
        using (var backup = Open(backupPath, readOnly: true))
        {
            var source = StoreSchemaInspection.Inspect(backup, root, hashes: true);
            if (source != intent.Source || !ManifestIdentity(backup).SequenceEqual(intent.ManifestIdentity))
                throw new InvalidOperationException("BackupOrMediaChanged_RestoreForbidden");
        }
        var path = Path.Combine(root, "station01.test.db");
        using (var current = Open(path, readOnly: true))
        {
            var currentState = StoreSchemaInspection.Inspect(current, root, hashes: false);
            if (currentState.StoreId != intent.Source.StoreId)
                throw new InvalidOperationException("CurrentStoreIdentityUnconfirmed_RestoreForbidden");
        }
        // Preserve the failed controlled copy and sidecars; never discard SQLite's original files.
        var archive = Path.Combine(EvidenceRoot(root, intent.OperationId), "preserved-failed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(archive);
        foreach (var source in new[] { path, path + "-wal", path + "-shm", path + "-journal" })
        {
            if (!Path.GetFullPath(source).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("RestoreOutsideControlledStore");
            if (File.Exists(source)) File.Move(source, Path.Combine(archive, Path.GetFileName(source)));
        }
        File.Copy(backupPath, path, overwrite: false);
        using var restored = Open(path, readOnly: false);
        var verified = StoreSchemaInspection.Inspect(restored, root, hashes: true);
        if (verified != intent.Source) throw new InvalidOperationException("RestoredSourceVerificationFailed");
        WriteEvidence(root, intent.OperationId, "restored-source", verified);
        // Keep the latch: U0 is not a usable v2 Host store. A later explicit upgrade must recheck it.
        return new("U0", "VerifiedSourceRestored_HostStillBlocked", verified.StoreId, 0, EvidenceRoot(root, intent.OperationId));
    }

    private static string? VerifyBackupAndOldData(string root, SqliteConnection actualConnection, Intent intent, StoreInspection actual)
    {
        var backupPath = Path.Combine(EvidenceRoot(root, intent.OperationId), "source-backup.db");
        if (!File.Exists(backupPath) || FileHash(backupPath) != intent.BackupSha256) return "BackupMissingOrChanged";
        using var backup = Open(backupPath, readOnly: true);
        var verified = StoreSchemaInspection.Inspect(backup, root, hashes: true);
        if (verified != intent.Source) return "BackupOrMediaVerificationMismatch";
        if (actual.State is not ("U0" or "U2") || actual.StoreId != intent.Source.StoreId || actual.Profile != intent.Source.Profile ||
            actual.BusinessDigest != intent.Source.BusinessDigest || actual.MediaDigest != intent.Source.MediaDigest ||
            !ManifestIdentity(actualConnection).SequenceEqual(intent.ManifestIdentity)) return "ActualStateIdentityOrOldDataMismatch";
        return null;
    }
    private static string[] ManifestIdentity(SqliteConnection connection) => StoreSchemaInspection.Rows(connection,
        "SELECT StoreId,Profile,PrepareOperationId,PreparedUtc FROM Manifests").Single().Select(v => Convert.ToString(v) ?? "").ToArray();
    private static SqliteConnection Open(string path, bool readOnly, bool create = false)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path,
            Mode = readOnly ? SqliteOpenMode.ReadOnly : create ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadWrite,
            Pooling = false, Cache = SqliteCacheMode.Private, ForeignKeys = true, DefaultTimeout = 1 }.ToString());
        connection.Open(); return connection;
    }
    private static void RequireDurableSettings(SqliteConnection connection)
    {
        var journal = Convert.ToString(StoreSchemaInspection.Rows(connection, "PRAGMA journal_mode").Single()[0]);
        var synchronous = Convert.ToInt64(StoreSchemaInspection.Rows(connection, "PRAGMA synchronous").Single()[0]);
        if (journal is "off" or "memory" || synchronous == 0) throw new InvalidOperationException("UnsafeStoreDurabilitySettings");
    }
    private static string EvidenceRoot(string root, Guid operation) => Path.Combine(root, ".009-maintenance", operation.ToString("N"));
    private static string FileHash(string path) { using var file = File.OpenRead(path); return Convert.ToHexStringLower(SHA256.HashData(file)); }
    private static void WriteEvidence(string root, Guid operation, string kind, object value)
    {
        var path = Path.Combine(EvidenceRoot(root, operation), kind + "-" + Guid.NewGuid().ToString("N") + ".json");
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        JsonSerializer.Serialize(stream, value, Json); stream.Flush(flushToDisk: true);
    }
}
