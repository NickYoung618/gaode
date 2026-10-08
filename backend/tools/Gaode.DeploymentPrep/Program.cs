using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Recipes;
using Gaode.Infrastructure.Persistence;
using Gaode.Infrastructure.Recipes;
using Gaode.Plc.Protocol;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

if (args.Length == 2 && args[0] == "--inspect-plc")
{
    var definition = FieldAddressProfile.LoadDefinition(args[1]);
    var issues = definition.Validate();
    Console.WriteLine(JsonSerializer.Serialize(new { connected = false, networkAccess = false,
        source = definition.SourceReference, valid = issues.Count == 0, issues }));
    Environment.ExitCode = issues.Count == 0 ? 0 : 2;
    return;
}
if (args.Length == 3 && args[0] == "--inspect-recipes")
{
    var root = Path.GetFullPath(args[1]);
    using var store = new SqliteRecipeStore(new() { DatabasePath = Path.Combine(root, "recipes.db"),
        ReadWriteTimeoutMs = 10000, DbLockTimeoutSeconds = 5 }, Path.GetDirectoryName(root)!,
        NullLogger<SqliteRecipeStore>.Instance, () => "deployment-readonly-verification");
    var snapshot = store.GetSnapshot();
    var rows = snapshot.Definitions.Select(d => new { definition = d,
        admission = RecipeAdmission.Evaluate(d, d.Positions.Select(p=>p.SlotId), Gaode.Domain.Configuration.RuntimePurposes.RealDeviceCommissioning),
        plan = RecipeRunPlanner.BuildExecutable(d, "deployment-readonly-plan", d.Positions.Select(p=>p.SlotId).ToArray(),
            Gaode.Domain.Configuration.RuntimePurposes.RealDeviceCommissioning) }).ToArray();
    var output = Path.GetFullPath(args[2]);
    if (File.Exists(output)) throw new InvalidOperationException("ExistingEvidenceCannotBeOverwritten");
    Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    File.WriteAllText(output, JsonSerializer.Serialize(new { connected=false, deviceDispatches=0, snapshot.CatalogDigest,
        recipes=rows }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented=true }));
    Console.WriteLine(JsonSerializer.Serialize(new { verified=rows.Length, networkAccess=false, output }));
    return;
}
if (args.Length == 3 && args[0] == "--inspect-commissioning")
{
    var loaded = Gaode.Infrastructure.Simulation.CommissioningAlgorithmInputs.Load(Path.GetFullPath(args[1]), args[2]);
    Console.WriteLine(JsonSerializer.Serialize(new { valid=true, networkAccess=false,
        loaded.Value.Id, loaded.Value.Version, loaded.Value.ExpectedRecipe,
        resultScopes=loaded.Value.Results.Count, loaded.Digest }));
    return;
}
if (args.Length == 3 && args[0] == "--prepare-recipes")
{
    RecipeStoreSchema.Prepare(Path.GetFullPath(args[1]), Path.GetFullPath(args[2]));
    Console.WriteLine("Recipe store prepared; no hardware accessed.");
    return;
}
if (args.Length == 4 && args[0] == "--seed-authoring")
{
    var bytes = File.ReadAllBytes(args[2]);
    if (!string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), args[3], StringComparison.OrdinalIgnoreCase))
        throw new InvalidDataException("AuthoringSourceDigestMismatch");
    var input = RecipeDefinitionSerialization.Deserialize(Encoding.UTF8.GetString(bytes));
    if (!string.IsNullOrEmpty(input.RecipeId) || !string.IsNullOrEmpty(input.Version) || input.PlcRecipeId is not null ||
        input.ReleaseStatus != "draft" || input.Approval.AllowedSlots.Count != 0 ||
        new[] { input.Approval.Id, input.Approval.Version, input.Approval.Digest, input.Approval.Purpose,
            input.Approval.EvidenceReference }.Any(v => !string.IsNullOrEmpty(v)))
        throw new InvalidDataException("AuthoringSourceCannotGrantApproval");
    var root = Path.GetFullPath(args[1]);
    using var store = new SqliteRecipeStore(new() { DatabasePath = Path.Combine(root, "recipes.db"),
        ReadWriteTimeoutMs = 10000, DbLockTimeoutSeconds = 5 }, Path.GetDirectoryName(root)!,
        NullLogger<SqliteRecipeStore>.Instance, () => "deployment-authoring-preparation");
    if (store.GetSnapshot().Definitions.Any(d => d.FCode == input.FCode))
        throw new InvalidOperationException("AuthoringSourceAlreadyExists:请读取已有来源；准备工具不会重复新增或覆盖。");
    var saved = await store.SaveAsync(new(input, null, null, Guid.NewGuid().ToString("N")), default);
    if (saved.Status != RecipeSaveStatus.Saved || saved.Definition is null)
        throw new InvalidOperationException("AuthoringSourceSaveFailed:" + JsonSerializer.Serialize(saved));
    Console.WriteLine(JsonSerializer.Serialize(new { saved.Status, saved.Definition.RecipeId, saved.Definition.Version,
        productionApproved = false }));
    return;
}
if (args.Length is not (3 or 4) || args[0] != "--prepare-runtime")
    throw new ArgumentException("Use --prepare-runtime <allowedRoot> <newRoot> [Test|RealDeviceCommissioning], --prepare-recipes, --seed-authoring or --inspect-plc.");
var profile = args.Length == 4 ? args[3] : "Test";
if (profile is not ("Test" or Gaode.Domain.Configuration.RuntimePurposes.RealDeviceCommissioning))
    throw new ArgumentException("UnsupportedStoreProfile");
var allowed = Path.GetFullPath(args[1]);
var target = Path.GetFullPath(args[2]);
if (Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any())
    throw new InvalidOperationException("ExistingRootCannotBeOverwritten");
using var guard = StoreAccessGuard.Acquire(target, allowed);
var connection = new SqliteConnectionStringBuilder { DataSource = Path.Combine(target, "station01.test.db"),
    Mode = SqliteOpenMode.ReadWriteCreate, ForeignKeys = true }.ToString();
await using (var db = new Station01DbContext(new DbContextOptionsBuilder<Station01DbContext>().UseSqlite(connection).Options))
{
    await db.Database.MigrateAsync();
    db.Manifests.Add(new StoreManifestEntity { StoreId = Guid.NewGuid(), SchemaVersion = "s01-store/3", Profile = profile,
        PrepareOperationId = Guid.NewGuid(), PreparedUtc = DateTimeOffset.UtcNow });
    await db.SaveChangesAsync();
}
Directory.CreateDirectory(Path.Combine(target, "media-root"));
var check = StoreCompatibilityProbe.Inspect(target, profile);
if (!check.Compatible) throw new InvalidOperationException(check.Code);
Console.WriteLine(JsonSerializer.Serialize(check));
