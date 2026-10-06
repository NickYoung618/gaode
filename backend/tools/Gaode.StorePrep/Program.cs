using Gaode.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Gaode.Infrastructure.Recipes;

using Gaode.Application.Recipes;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Cryptography;

if (args.Length == 3 && args[0] == "--upgrade-public-tray-test")
{
    Console.WriteLine(JsonSerializer.Serialize(StoreMaintenance.UpgradePublicTrayTest(args[1], args[2])));
    return;
}

if (args.Length == 4 && args[0] == "--prepare-authoring-source")
{
    var root = Path.GetFullPath(args[1]);
    var bytes = await File.ReadAllBytesAsync(args[2]);
    if (!string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), args[3], StringComparison.OrdinalIgnoreCase))
        throw new InvalidDataException("AuthoringSourceDigestMismatch");
    var input = RecipeDefinitionSerialization.Deserialize(System.Text.Encoding.UTF8.GetString(bytes));
    if (!string.IsNullOrEmpty(input.RecipeId) || !string.IsNullOrEmpty(input.Version) ||
        input.PlcRecipeId is not null || input.ReleaseStatus != "draft" ||
        new[] { input.Approval.Id, input.Approval.Version, input.Approval.Digest,
            input.Approval.Purpose, input.Approval.EvidenceReference }.Any(v => !string.IsNullOrEmpty(v)) ||
        input.Approval.AllowedSlots.Count != 0)
        throw new InvalidDataException("AuthoringSourceCannotGrantApprovalOrOverwriteHead");
    using var store = new SqliteRecipeStore(new RecipeStoreOptions {
        DatabasePath = Path.Combine(root, "recipes.db"), ReadWriteTimeoutMs = 10000, DbLockTimeoutSeconds = 5
    }, Path.GetDirectoryName(root)!, NullLogger<SqliteRecipeStore>.Instance, () => "authoring-source-maintenance");
    var requestId = Guid.NewGuid().ToString("N");
    var saved = await store.SaveAsync(new(input, null, null, requestId), CancellationToken.None);
    if (saved.Status != RecipeSaveStatus.Saved || saved.Definition is null)
        throw new InvalidOperationException("AuthoringSourceSaveFailed:" + JsonSerializer.Serialize(saved));
    var read = store.GetCurrentContent(saved.Definition.RecipeId)
        ?? throw new InvalidOperationException("AuthoringSourceReadMissing");
    Console.WriteLine(JsonSerializer.Serialize(new { sourceSha256 = args[3], saved.Status,
        saved.Definition.RecipeId, saved.Definition.Version, saved.Definition.DefinitionDigest,
        read.DefinitionJson, requestId, productionApproved = false }));
    return;
}

if (args.Length == 5 && args[0] == "--seed-test-recipes")
{
    Console.WriteLine(JsonSerializer.Serialize(RecipeTestPreparation.Seed(args[1], args[2], args[3], args[4])));
    return;
}

if (args.Length == 3 && args[0] is "--prepare-recipes" or "--inspect-recipes")
{
    if (args[0] == "--prepare-recipes") RecipeStoreSchema.Prepare(args[1], args[2]);
    else RecipeStoreSchema.Inspect(args[1], args[2]);
    Console.WriteLine(JsonSerializer.Serialize(new { schema = "recipe-store/1", database = Path.Combine(Path.GetFullPath(args[2]), "recipes.db"), verified = true }));
    return;
}

if (args.Length >= 3 && args[0] is "--upgrade-test" or "--reconcile-test" or "--restore-source-test")
{
    if (args.Length is not (3 or 5) || args.Length == 5 &&
        (args[0] == "--restore-source-test" || args[3] != "--pause-at" ||
         Environment.GetEnvironmentVariable("GAODE_009_STORE_INTERRUPTION") != "1" ||
         args[4] is not ("before-transaction" or "after-ddl" or "after-history" or "after-manifest" or "after-commit-before-receipt" or "before-reconciliation")))
        throw new ArgumentException("Invalid controlled Test maintenance invocation");
    Action<string>? pause = args.Length == 5 ? phase =>
    {
        if (phase != args[4]) return;
        Console.WriteLine("UPGRADE_CHECKPOINT:" + phase);
        Console.Out.Flush();
        Thread.Sleep(Timeout.Infinite); // Explicit Test child-process kill seam, never enabled for Host.
    } : null;
    var maintenanceResult = args[0] == "--restore-source-test"
        ? StoreMaintenance.RestoreVerifiedSourceTest(args[1], args[2])
        : StoreMaintenance.UpgradeTest(args[1], args[2], args[0] == "--reconcile-test", pause);
    Console.WriteLine(JsonSerializer.Serialize(maintenanceResult));
    if (maintenanceResult.State == "UX") Environment.ExitCode = 2;
    return;
}

if (args.Length != 2 || !Path.IsPathFullyQualified(args[0]) ||
    !Path.IsPathFullyQualified(args[1]))
    throw new ArgumentException("Usage: Gaode.StorePrep <allowedTestRoot> <newTestRoot>");
var allowed = Path.GetFullPath(args[0]).TrimEnd(Path.DirectorySeparatorChar);
var target = Path.GetFullPath(args[1]).TrimEnd(Path.DirectorySeparatorChar);
if (!target.StartsWith(allowed + Path.DirectorySeparatorChar,
        StringComparison.OrdinalIgnoreCase))
    throw new InvalidOperationException("TestRootOutsideApprovedRoot");
if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any())
    throw new InvalidOperationException("ExistingTestRootCannotBePreparedOrOverwritten");
using var guard = StoreAccessGuard.Acquire(target, allowed);
var database = Path.Combine(target, "station01.test.db");
var connection = new SqliteConnectionStringBuilder
{
    DataSource = database, Mode = SqliteOpenMode.ReadWriteCreate, ForeignKeys = true
}.ToString();
var options = new DbContextOptionsBuilder<Station01DbContext>().UseSqlite(connection).Options;
await using (var db = new Station01DbContext(options))
{
    await db.Database.MigrateAsync();
    db.Manifests.Add(new StoreManifestEntity
    {
        StoreId = Guid.NewGuid(), SchemaVersion = "s01-store/3", Profile = "Test",
        PrepareOperationId = Guid.NewGuid(), PreparedUtc = DateTimeOffset.UtcNow
    });
    await db.SaveChangesAsync();
}
Directory.CreateDirectory(Path.Combine(target, "media-root"));
var result = StoreCompatibilityProbe.Inspect(target);
if (!result.Compatible) throw new InvalidOperationException(result.Code);
Console.WriteLine($"{target}|{result.StoreId}");
