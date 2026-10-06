using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Recipes;
using Gaode.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gaode.Infrastructure.Recipes;

/// <summary>The one persistent recipe source for HTTP, validation and F snapshots.</summary>
public sealed class SqliteRecipeStore(RecipeStoreOptions options, string allowedRoot,
    ILogger<SqliteRecipeStore> logger, Func<string> actorId) : IRecipeStore, IRecipeCatalog, IDisposable
{
    private readonly SemaphoreSlim writes = new(1, 1);
    private readonly object ownershipSync = new();
    private StoreAccessGuard? ownership;
    private const string ContractVersion = "recipe-contract/1.5";

    private int RemainingTimeout(long started)
    {
        var remaining = options.ReadWriteTimeoutMs - Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        if (remaining < 1000) throw new TimeoutException("RecipeStoreDeadlineExceeded");
        return Math.Min(options.DbLockTimeoutSeconds, (int)(remaining / 1000));
    }

    private SqliteConnection Open(long started, bool readOnly)
    {
        options.Validate();
        lock (ownershipSync)
        {
            if (!File.Exists(options.DatabasePath)) throw new InvalidOperationException("RecipeStoreMissing");
            ownership ??= StoreAccessGuard.Acquire(Path.GetDirectoryName(Path.GetFullPath(options.DatabasePath))!, allowedRoot);
        }
        RecipeStoreSchema.Verify(options.DatabasePath, RemainingTimeout(started));
        var connection = new SqliteConnection(options.ConnectionString(readOnly));
        connection.DefaultTimeout = RemainingTimeout(started);
        try { connection.Open(); return connection; }
        catch { connection.Dispose(); throw; }
    }

    private RecipeStoreDbContext Context(SqliteConnection connection, long started) => new(
        new DbContextOptionsBuilder<RecipeStoreDbContext>()
            .UseSqlite(connection, sqlite => sqlite.CommandTimeout(RemainingTimeout(started))).Options);

    private static IQueryable<RecipeSavedContentRow> Current(RecipeStoreDbContext db) =>
        from head in db.Heads.AsNoTracking()
        join body in db.Contents.AsNoTracking() on new { head.RecipeId, Version = head.CurrentVersion }
            equals new { body.RecipeId, body.Version }
        select body;

    private static RecipeDefinition Read(RecipeSavedContentRow row)
    {
        if (row.ContractVersion is not (ContractVersion or "recipe-contract/1.4" or "recipe-contract/1.3")) throw new InvalidDataException("RecipeContractUnsupported");
        RecipeDefinition definition;
        try { definition = RecipeDefinitionSerialization.Deserialize(row.DefinitionJson); }
        catch (JsonException error) { throw new InvalidDataException("RecipeStoredBodyInvalid", error); }
        if (definition.RecipeId != row.RecipeId || definition.Version != row.Version || definition.DefinitionDigest != row.DefinitionDigest)
            throw new InvalidDataException("RecipeStoredIdentityMismatch");
        return definition;
    }

    // Catalog source digest only. The business DefinitionDigest always comes from 011.
    public static string ComputeCatalogDigest(IEnumerable<RecipeDefinition> definitions) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(definitions.OrderBy(d => d.RecipeId, StringComparer.Ordinal)
            .Select(d => new[] { d.RecipeId, d.Version, d.DefinitionDigest })))));

    private static RecipeCatalogSnapshot Snapshot(IEnumerable<RecipeSavedContentRow> rows)
    {
        var definitions = rows.Select(Read).ToArray();
        return RecipeCatalogSnapshots.Create(ComputeCatalogDigest(definitions), definitions);
    }

    public RecipeCatalogSnapshot GetSnapshot()
    {
        var started = Stopwatch.GetTimestamp();
        try
        {
            using var connection = Open(started, readOnly: true);
            using var db = Context(connection, started);
            var snapshot = Snapshot(Current(db).ToArray());
            _ = RemainingTimeout(started);
            return snapshot;
        }
        catch (Exception error)
        {
            logger.LogError(error, "RecipeStore.ReadFailed actor={Actor} source=Sqlite", actorId());
            throw;
        }
    }

    // Audit metadata for the complete GET representation; not another business read port.
    public RecipeSavedContentRow? GetCurrentContent(string recipeId)
    {
        var started = Stopwatch.GetTimestamp();
        using var connection = Open(started, readOnly: true);
        using var db = Context(connection, started);
        var row = Current(db).SingleOrDefault(r => r.RecipeId == recipeId);
        if (row is not null) _ = Read(row);
        _ = RemainingTimeout(started);
        return row;
    }

    public async Task<RecipeSaveResult> SaveAsync(RecipeSaveRequest request, CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var entered = false;
        var commitAttempted = false;
        RecipeSaveResult? confirmed = null;
        logger.LogInformation("RecipeStore.SaveReceived requestId={RequestId} actor={Actor} recipeId={RecipeId}",
            request.RequestId, actorId(), request.TargetRecipeId);
        try
        {
            options.Validate();
            entered = await writes.WaitAsync(options.ReadWriteTimeoutMs, cancellationToken);
            if (!entered) throw new TimeoutException("RecipeStoreWriteWaitExpired");
            cancellationToken.ThrowIfCancellationRequested();
            using var connection = Open(started, readOnly: false);
            connection.DefaultTimeout = RemainingTimeout(started);
            using var transaction = connection.BeginTransaction(deferred: false);
            using var db = Context(connection, started);
            db.Database.UseTransaction(transaction);
            var current = Snapshot(Current(db).ToArray());
            var existing = request.TargetRecipeId is null ? null :
                current.Definitions.SingleOrDefault(d => d.RecipeId == request.TargetRecipeId);
            if (request.TargetRecipeId is not null &&
                (existing is null || request.ExpectedVersion is null || existing.Version != request.ExpectedVersion) ||
                request.TargetRecipeId is null && request.ExpectedVersion is not null)
            {
                logger.LogWarning("RecipeStore.VersionConflict requestId={RequestId} recipeId={RecipeId}", request.RequestId, request.TargetRecipeId);
                return new(RecipeSaveStatus.VersionConflict, Reason: "VersionConflict");
            }
            // Server-only metadata. New records have no approval; saved edits retain the prior scope.
            var candidate = request.Candidate with
            {
                RecipeId = existing?.RecipeId ?? RecipeDefinitionIdentity.CreateRecipeId(),
                Version = RecipeDefinitionIdentity.CreateVersion(), DefinitionDigest = "", CatalogDigest = "",
                PlcRecipeId = existing?.PlcRecipeId,
                Approval = existing?.Approval ?? new("", "", "", "", [], ""),
                ReleaseStatus = existing?.ReleaseStatus ?? "draft"
            };
            var validation = RecipeDefinitionValidator.ValidateForSave(candidate, current);
            if (!validation.Valid)
            {
                logger.LogWarning("RecipeStore.ValidationFailed requestId={RequestId} issues={Issues}", request.RequestId,
                    string.Join(",", validation.Issues.Select(i => i.Code + ":" + i.FieldPath)));
                return new(RecipeSaveStatus.ValidationFailed, Issues: validation.Issues);
            }
            candidate = candidate with { DefinitionDigest = RecipeDefinitionIdentity.ComputeDefinitionDigest(candidate) };
            var digest = ComputeCatalogDigest(current.Definitions.Where(d => d.RecipeId != candidate.RecipeId).Append(candidate));
            candidate = RecipeCatalogSnapshots.Freeze(candidate with { CatalogDigest = digest });
            var row = new RecipeSavedContentRow
            {
                RecipeId = candidate.RecipeId, Version = candidate.Version, DefinitionDigest = candidate.DefinitionDigest,
                ContractVersion = ContractVersion, DefinitionJson = RecipeDefinitionSerialization.Serialize(candidate),
                SavedUtc = DateTimeOffset.UtcNow, ActorId = actorId(), RequestId = request.RequestId
            };
            cancellationToken.ThrowIfCancellationRequested();
            db.Database.SetCommandTimeout(RemainingTimeout(started));
            db.Contents.Add(row);
            db.SaveChanges();
            var head = db.Heads.SingleOrDefault(h => h.RecipeId == candidate.RecipeId);
            if (head is null) db.Heads.Add(new() { RecipeId = candidate.RecipeId, CurrentVersion = candidate.Version, FCode = candidate.FCode });
            else { head.CurrentVersion = candidate.Version; head.FCode = candidate.FCode; }
            cancellationToken.ThrowIfCancellationRequested();
            db.Database.SetCommandTimeout(RemainingTimeout(started));
            db.SaveChanges();
            cancellationToken.ThrowIfCancellationRequested();
            connection.DefaultTimeout = RemainingTimeout(started);
            commitAttempted = true;
            transaction.Commit();
            confirmed = new(RecipeSaveStatus.Saved, candidate, digest);
            // No post-commit cancellation, readback or cache publication can demote this known fact.
            logger.LogInformation("RecipeStore.Saved requestId={RequestId} actor={Actor} recipeId={RecipeId} version={Version} definitionDigest={DefinitionDigest}",
                request.RequestId, row.ActorId, candidate.RecipeId, candidate.Version, candidate.DefinitionDigest);
            return confirmed;
        }
        catch (Exception error) when (error is SqliteException or DbUpdateException or IOException or InvalidOperationException
            or JsonException or OperationCanceledException or TimeoutException)
        {
            if (confirmed is not null) return confirmed;
            var status = commitAttempted ? RecipeSaveStatus.CommitUnknown : RecipeSaveStatus.SaveFailed;
            logger.LogError(error, "RecipeStore.SaveEnded requestId={RequestId} recipeId={RecipeId} result={Result}",
                request.RequestId, request.TargetRecipeId, status);
            return new(status, Reason: status.ToString());
        }
        finally { if (entered) writes.Release(); }
    }

    public void Dispose() { ownership?.Dispose(); writes.Dispose(); }
}
