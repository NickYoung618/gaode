using Gaode.Application.Recipes;
using Gaode.Infrastructure.Recipes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Gaode.Integration.Tests.Support;

namespace Gaode.Integration.Tests.Storage;

// Small declared component inputs; real SQLite and common planner. No device success is synthesized.
public sealed class RecipePerCaptureTests
{
    [Fact]
    public async Task EachCaptureAndGripperSurviveSaveReadEditAndFreeze()
    {
        var (allowed, root) = RecipeAuthoringTestRoots.New(); RecipeStoreSchema.Prepare(allowed, root);
        var options = new RecipeStoreOptions { DatabasePath = Path.Combine(root, "recipes.db"), ReadWriteTimeoutMs = 10000, DbLockTimeoutSeconds = 1 };
        var candidate = PerCapture(RecipeAuthoringTestInputs.RichCandidate(4, true));
        RecipeDefinition saved, updated;
        using (var store = new SqliteRecipeStore(options, allowed, NullLogger<SqliteRecipeStore>.Instance, () => "declared-component"))
        {
            var first = await store.SaveAsync(new(candidate, null, null, "per-capture-first"), CancellationToken.None);
            Assert.Equal(RecipeSaveStatus.Saved, first.Status); saved = first.Definition!;
            Assert.Equal(RecipeDefinitionSerialization.Serialize(saved), RecipeDefinitionSerialization.Serialize(Assert.Single(store.GetSnapshot().Definitions)));
            var input = saved with { Approval = new("component", "1", "component-digest", "Test", ["slot-one"], "ComponentOnly") };
            var frozen = RecipeRunPlanner.BuildExecutable(input, Guid.NewGuid().ToString("D"), ["slot-one"]);
            var oldRevision = RecipePlanRevision.Compute(frozen);
            Assert.Equal(1, frozen.SortingGripperId);
            var captures = frozen.Steps.Where(s => s.Kind is RecipeStepKind.Capture or RecipeStepKind.ReadECode).ToArray();
            Assert.Equal(9, captures.Length);
            Assert.Equal(9, captures.Select(s => s.CaptureProfile).Distinct().Count());
            Assert.Contains(captures, s => s.Camera == "E");
            Assert.All(captures, s => {
                var expected = candidate.CaptureProfiles[s.CaptureProfile!].Settings;
                var actual = frozen.CaptureProfiles[s.CaptureProfile!].Settings;
                Assert.Equal((expected.ExposureUs, expected.Gain, expected.BrightnessPercent), (actual.ExposureUs, actual.Gain, actual.BrightnessPercent));
            });
            var key = captures[0].CaptureProfile!;
            var profiles = saved.CaptureProfiles.ToDictionary(p => p.Key, p => p.Value);
            profiles[key] = profiles[key] with { Settings = profiles[key].Settings with { ExposureUs = 9876 } };
            var result = await store.SaveAsync(new(saved with { SortingGripperId = 2, CaptureProfiles = profiles }, saved.RecipeId, saved.Version, "per-capture-edit"), CancellationToken.None);
            Assert.Equal(RecipeSaveStatus.Saved, result.Status); updated = result.Definition!;
            Assert.NotEqual(saved.DefinitionDigest, updated.DefinitionDigest);
            Assert.Equal(oldRevision, RecipePlanRevision.Compute(frozen));
            Assert.Equal(1, frozen.SortingGripperId);
            Assert.NotEqual(9876, frozen.CaptureProfiles[key].Settings.ExposureUs);
            Assert.Equal(2, updated.SortingGripperId);
            Assert.Equal(9876, updated.CaptureProfiles[key].Settings.ExposureUs);
            Assert.Equal(RecipeSaveStatus.VersionConflict, (await store.SaveAsync(new(saved, saved.RecipeId, saved.Version, "stale"), CancellationToken.None)).Status);
            foreach (var value in new int?[] { null, 0, 3 })
                Assert.Equal(RecipeSaveStatus.ValidationFailed, (await store.SaveAsync(new(updated with { SortingGripperId = value }, updated.RecipeId, updated.Version, "invalid-gripper"), CancellationToken.None)).Status);
            var match = RecipeMatcher.Match(store.GetSnapshot(), updated.FCode, null, updated.ScenarioId, "Test");
            Assert.Equal(updated.Version, match.Version); // Restricted draft still cannot invent approval.
            Assert.Equal(RecipeMatchStatus.Restricted, match.Status);
        }
        using var reopened = new SqliteRecipeStore(options, allowed, NullLogger<SqliteRecipeStore>.Instance, () => "declared-component");
        Assert.Equal(RecipeDefinitionSerialization.Serialize(updated), RecipeDefinitionSerialization.Serialize(Assert.Single(reopened.GetSnapshot().Definitions)));
    }

    [Theory]
    [InlineData("recipe-definition/2","recipe-contract/1.3")]
    [InlineData("recipe-definition/3","recipe-contract/1.4")]
    public async Task HistoricalRowsAreActuallyReadWithoutUpgradingOrInventingLayout(string schema,string contract)
    {
        var (allowed,root)=RecipeAuthoringTestRoots.New();RecipeStoreSchema.Prepare(allowed,root);
        // Historical contract fixture seeded directly into its declared record version,
        // using the read-only 011 source; it is never reported as a current successful save.
        var source=JsonDocument.Parse(File.ReadAllText(Path.Combine(Station01HostFixture.FindWorkspace(),
            "specs/011-plc-interaction-update/examples/joint/catalog.json"))).RootElement.GetProperty("definitions")[0];
        var old=RecipeDefinitionSerialization.Deserialize(source.GetRawText()) with {SchemaVersion=schema,
            RecipeId="declared-history",Version="declared-old-version",SortingGripperId=schema.EndsWith("/2",StringComparison.Ordinal)?null:1};
        old=old with {DefinitionDigest=RecipeDefinitionIdentity.ComputeDefinitionDigest(old)};
        var body=RecipeDefinitionSerialization.Serialize(old);
        var options=new RecipeStoreOptions {DatabasePath=Path.Combine(root,"recipes.db"),ReadWriteTimeoutMs=10000,DbLockTimeoutSeconds=1};
        await using(var db=new RecipeStoreDbContext(new DbContextOptionsBuilder<RecipeStoreDbContext>().UseSqlite(options.ConnectionString(false)).Options))
        {
            db.Contents.Add(new() {RecipeId=old.RecipeId,Version=old.Version,DefinitionDigest=old.DefinitionDigest,ContractVersion=contract,
                DefinitionJson=body,SavedUtc=DateTimeOffset.UtcNow,ActorId="declared-historical-fixture",RequestId="history-only"});
            db.Heads.Add(new() {RecipeId=old.RecipeId,CurrentVersion=old.Version,FCode=old.FCode});
            await db.SaveChangesAsync();
        }
        using var store=new SqliteRecipeStore(options,allowed,NullLogger<SqliteRecipeStore>.Instance,()=>"component-reader");
        var read=Assert.Single(store.GetSnapshot().Definitions);
        Assert.Equal(body,RecipeDefinitionSerialization.Serialize(read));Assert.Equal(schema,read.SchemaVersion);
        Assert.Null(read.TrayLayout);Assert.Null(read.TraySlotMapping);Assert.Null(read.RotationLoadingGripperId);
        Assert.Equal(old.SortingGripperId,read.SortingGripperId);
        Assert.Equal(RecipeSaveStatus.ValidationFailed,(await store.SaveAsync(new(read,read.RecipeId,read.Version,"reject-historical-new-write"),default)).Status);
        Assert.Equal(body,RecipeDefinitionSerialization.Serialize(Assert.Single(store.GetSnapshot().Definitions)));
    }

    [Fact]
    public void HistoricalMissingValuesStayMissingAndCurrentMissingAssociationIsRejected()
    {
        var legacy = RecipeAuthoringTestInputs.Candidate() with { SchemaVersion = RecipeDefinitionSerialization.HistoricalSchema, SortingGripperId = null };
        var json = RecipeDefinitionSerialization.Serialize(legacy);
        Assert.DoesNotContain("sortingGripperId", json);
        Assert.Null(RecipeDefinitionSerialization.Deserialize(json).SortingGripperId);
        Assert.Equal(json, RecipeDefinitionSerialization.Serialize(RecipeDefinitionSerialization.Deserialize(json)));
        var current = PerCapture(RecipeAuthoringTestInputs.Candidate());
        var slot = current.ExecutionPositions["slot-one"];
        var body = slot.PhysicalEntity with { Coordinates = slot.PhysicalEntity.Coordinates.Select((p, i) => i == 0 ? p with { CaptureProfile = null } : p).ToArray() };
        var invalid = current with { ExecutionPositions = new Dictionary<string, SlotExecutionInputs> { ["slot-one"] = slot with { PhysicalEntity = body } } };
        Assert.Contains(RecipeDefinitionValidator.ValidateForSave(invalid, new(RecipeCatalogSnapshot.CurrentSchema, "", [])).Issues,
            i => i.Code == "ProductCaptureProfileMissing");
    }

    internal static RecipeDefinition PerCapture(RecipeDefinition recipe)
    {
        var profiles = recipe.CaptureProfiles.ToDictionary(p => p.Key, p => p.Value);
        var n = 0;
        string Profile(string key)
        {
            n++;
            var basis = recipe.CaptureProfiles.Values.First();
            profiles[key] = new(key, basis.Version, basis.Settings with { ProfileId = key, ExposureUs = 100 + n * 31, Gain = 1 + n / 10d, BrightnessPercent = 10 + n * 3 });
            return key;
        }
        ObjectExecutionInputs Object(string slot, string member, ObjectExecutionInputs input) => input with
        {
            Coordinates = input.Coordinates.Select(p => p with { CaptureProfile = Profile($"{slot}/{member}/{p.PointRef}/{p.Camera}") }).ToArray(),
            PurposePoints = input.PurposePoints.ToDictionary(p => p.Key, p => p.Value.Purpose == RecipePointPurpose.EScan
                ? p.Value with { CaptureProfile = Profile($"{slot}/{member}/{p.Key}/E") } : p.Value)
        };
        var inputs = recipe.ExecutionPositions.ToDictionary(p => p.Key, p => p.Value with
        {
            PhysicalEntity = Object(p.Key, "physical", p.Value.PhysicalEntity),
            Members = p.Value.Members.ToDictionary(m => m.Key, m => Object(p.Key, m.Key, m.Value))
        });
        return recipe with { SchemaVersion = RecipeDefinitionSerialization.CurrentSchema, SortingGripperId = 1, CaptureProfiles = profiles, ExecutionPositions = inputs };
    }
}
