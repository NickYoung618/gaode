using Gaode.Application.Capabilities;
using Gaode.Application.Recipes;
using Gaode.Communication.Tests.Devices;
using Gaode.Infrastructure.Recipes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gaode.Application.Ports;
using Gaode.Domain.Configuration;
using Gaode.Host.Api;
using Gaode.Host.Composition;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Gaode.Communication.Tests;

// Isolated SQLite/component evidence only. Purpose is explicitly commissioning;
// none of these fixture coordinates authorize real devices.
public sealed class RecipeCommissioningChainTests
{
    private const string Purpose = "RealDeviceCommissioning";

    [Fact]
    public async Task FormalApiValidatesSavesSelectsAndEditsAgainstRealSqlite()
    {
        var root = Path.Combine(Path.GetTempPath(), "gaode-recipe-api-" + Guid.NewGuid().ToString("N"));
        var storeRoot = Path.Combine(root, "recipes");
        RecipeStoreSchema.Prepare(root, storeRoot);
        try
        {
            var options = new RecipeStoreOptions { DatabasePath = Path.Combine(storeRoot, "recipes.db"),
                ReadWriteTimeoutMs = 10000, DbLockTimeoutSeconds = 5 };
            using var store = new SqliteRecipeStore(options, root, NullLogger<SqliteRecipeStore>.Instance, () => "offline-http-test");
            var builder = WebApplication.CreateBuilder();
            builder.Configuration["Gaode:Tokens:Operator"] = "offline-operator";
            builder.Configuration["Gaode:Tokens:ProcessEngineer"] = "offline-process";
            builder.Services.AddStation01Api(builder.Configuration);
            builder.Services.AddSingleton(store);
            builder.Services.AddSingleton<IRecipeStore>(store);
            builder.Services.AddSingleton<IRecipeCatalog>(store);
            builder.Services.AddSingleton<IPublicConfiguration>(new PurposeOnlyConfiguration());
            builder.Services.AddSingleton(new Station01RuntimeOptions(Purpose, root, root, root, root,
                new("offline", "1"), new("offline", "1"), new("offline", "1")));
            await using var app = builder.Build();
            app.UseAuthentication(); app.UseAuthorization(); app.MapRecipeEndpoints();
            app.Urls.Add("http://127.0.0.1:0");
            await app.StartAsync();
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "offline-process");
            static StringContent Body(RecipeDefinition recipe) => new(
                new JsonObject { ["requestId"] = Guid.NewGuid().ToString(),
                    ["definition"] = JsonNode.Parse(RecipeDefinitionSerialization.Serialize(recipe)) }.ToJsonString(),
                Encoding.UTF8, "application/json");

            var candidate = Recipe011Data.Candidate(1, false) with { Approval = new("", "", "", "", [], "") };
            using var validated = await client.PostAsync("/api/v1/recipes/validate", Body(candidate));
            Assert.Equal(HttpStatusCode.OK, validated.StatusCode);
            Assert.True(JsonNode.Parse(await validated.Content.ReadAsStringAsync())!["valid"]!.GetValue<bool>());
            using var saved = await client.PostAsync("/api/v1/recipes", Body(candidate));
            Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
            var definition = JsonNode.Parse(await saved.Content.ReadAsStringAsync())!["definition"]!;
            var recipe = RecipeDefinitionSerialization.Deserialize(definition.ToJsonString());
            Assert.Empty(recipe.Approval.Purpose);
            var path = "/api/v1/recipes/" + recipe.RecipeId;
            using var read = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            var readRecipe = RecipeDefinitionSerialization.Deserialize(
                JsonNode.Parse(await read.Content.ReadAsStringAsync())!["definition"]!.ToJsonString());
            Assert.Equal(recipe.DefinitionDigest, readRecipe.DefinitionDigest);
            using var catalog = await client.GetAsync("/api/v1/recipes/catalog");
            Assert.Equal(HttpStatusCode.OK, catalog.StatusCode);
            var item = JsonNode.Parse(await catalog.Content.ReadAsStringAsync())!["items"]!.AsArray().Single()!;
            Assert.Equal("Available", item["availability"]!.GetValue<string>());
            Assert.Equal(Purpose, item["purpose"]!.GetValue<string>());

            using var second = await client.PostAsync("/api/v1/recipes", Body(candidate with { FCode = "another-tray" }));
            Assert.Equal(HttpStatusCode.Created, second.StatusCode);
            using var ambiguous = await client.PostAsJsonAsync("/api/v1/recipes/editor-draft",
                new { candidate.Model, candidate.ScenarioId, candidate.UnitKind, inspectionKind = "ordinary" });
            Assert.Equal(HttpStatusCode.BadRequest, ambiguous.StatusCode);
            using var sourced = await client.PostAsJsonAsync("/api/v1/recipes/editor-draft",
                new { candidate.Model, candidate.ScenarioId, candidate.UnitKind, inspectionKind = "ordinary", sourceRecipeId = recipe.RecipeId });
            Assert.Equal(HttpStatusCode.OK, sourced.StatusCode);
            var sourceContext = JsonNode.Parse(await sourced.Content.ReadAsStringAsync())!["authoringContext"]!;
            Assert.Equal(recipe.RecipeId, sourceContext["sourceRecipeId"]!.GetValue<string>());
            Assert.Equal(recipe.Version, sourceContext["sourceVersion"]!.GetValue<string>());
            using var outdatedLayout = await client.PostAsync("/api/v1/recipes/editor-layout", new StringContent(
                new JsonObject { ["definition"] = definition.DeepClone(), ["sourceRecipeId"] = recipe.RecipeId,
                    ["sourceVersion"] = "outdated", ["trayLayout"] = JsonSerializer.SerializeToNode(recipe.TrayLayout, new JsonSerializerOptions(JsonSerializerDefaults.Web)) }.ToJsonString(),
                Encoding.UTF8, "application/json"));
            Assert.Equal(HttpStatusCode.BadRequest, outdatedLayout.StatusCode);

            var invalid = readRecipe with { Model = "" };
            using var rejected = new HttpRequestMessage(HttpMethod.Put, path) { Content = Body(invalid) };
            rejected.Headers.TryAddWithoutValidation("If-Match", read.Headers.ETag!.ToString());
            using var rejection = await client.SendAsync(rejected);
            Assert.Equal(HttpStatusCode.UnprocessableEntity, rejection.StatusCode);
            Assert.Equal(recipe.Version, store.GetSnapshot().Definitions.Single(d => d.RecipeId == recipe.RecipeId).Version);
            using var edit = new HttpRequestMessage(HttpMethod.Put, path) { Content = Body(readRecipe with { Model = "edited-model" }) };
            edit.Headers.TryAddWithoutValidation("If-Match", read.Headers.ETag.ToString());
            using var edited = await client.SendAsync(edit);
            Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
            Assert.NotEqual(recipe.Version, store.GetSnapshot().Definitions.Single(d => d.RecipeId == recipe.RecipeId).Version);
            using var stale = new HttpRequestMessage(HttpMethod.Put, path) { Content = Body(readRecipe) };
            stale.Headers.TryAddWithoutValidation("If-Match", read.Headers.ETag.ToString());
            using var conflict = await client.SendAsync(stale);
            Assert.Equal(HttpStatusCode.PreconditionFailed, conflict.StatusCode);
            // Re-read the actual HTTP-saved two-face body, then run the formal consumer.
            using var twoFace = await client.PostAsync("/api/v1/recipes", Body(
                Recipe011Data.Candidate(2, false) with { FCode = "OFFLINE-two-face-trace", Approval = new("", "", "", "", [], "") }));
            Assert.Equal(HttpStatusCode.Created, twoFace.StatusCode);
            var twoFaceRecipe = RecipeDefinitionSerialization.Deserialize(JsonNode.Parse(await twoFace.Content.ReadAsStringAsync())!["definition"]!.ToJsonString());
            using var reRead = await client.GetAsync("/api/v1/recipes/" + twoFaceRecipe.RecipeId);
            var executable = RecipeDefinitionSerialization.Deserialize(JsonNode.Parse(await reRead.Content.ReadAsStringAsync())!["definition"]!.ToJsonString());
            await new CameraBusinessRegressionTests().ExecuteSavedCommissioningRecipe(executable,
                ControlledCommissioningTests.Inputs.EvidencePath("recipe-field-trace.json"));
            RecipeDefinition? versionB = null;
            var initialCoordinate = executable.ExecutionPositions["s1"].PhysicalEntity.Coordinates[0];
            var beforeProfile = executable.CaptureProfiles[initialCoordinate.CaptureProfile!];
            await new CameraBusinessRegressionTests().ExecuteSavedCommissioningRecipe(executable,
                ControlledCommissioningTests.Inputs.EvidencePath("edit-isolation-run-a.json"), async () => {
                    var profileId = "OFFLINE-edited-single";
                    var profiles = executable.CaptureProfiles.ToDictionary(p => p.Key, p => p.Value);
                    profiles[profileId] = beforeProfile with { Id = profileId, Settings = beforeProfile.Settings with {
                        ProfileId = profileId, ExposureUs = beforeProfile.Settings.ExposureUs + 100 } };
                    var slot = executable.ExecutionPositions["s1"];
                    var coordinates = slot.PhysicalEntity.Coordinates.ToArray();
                    coordinates[0] = initialCoordinate with { Point = initialCoordinate.Point with { X = initialCoordinate.Point.X + 1 }, CaptureProfile = profileId };
                    var positions = executable.ExecutionPositions.ToDictionary(p => p.Key, p => p.Value);
                    positions["s1"] = slot with { PhysicalEntity = slot.PhysicalEntity with { Coordinates = coordinates } };
                    var changed = executable with { CaptureProfiles = profiles, ExecutionPositions = positions };
                    using var update = new HttpRequestMessage(HttpMethod.Put, "/api/v1/recipes/" + executable.RecipeId) { Content = Body(changed) };
                    update.Headers.TryAddWithoutValidation("If-Match", reRead.Headers.ETag!.ToString());
                    using var updated = await client.SendAsync(update);
                    Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
                    using var readB = await client.GetAsync("/api/v1/recipes/" + executable.RecipeId);
                    versionB = RecipeDefinitionSerialization.Deserialize(JsonNode.Parse(await readB.Content.ReadAsStringAsync())!["definition"]!.ToJsonString());
                    Assert.NotEqual(executable.Version, versionB.Version);
                    Assert.NotEqual(executable.DefinitionDigest, versionB.DefinitionDigest);
                    Assert.Equal(initialCoordinate.Point.X + 1, versionB.ExecutionPositions["s1"].PhysicalEntity.Coordinates[0].Point.X);
                    Assert.Equal(beforeProfile.Settings.ExposureUs + 100, versionB.CaptureProfiles[profileId].Settings.ExposureUs);
                });
            Assert.NotNull(versionB);
            await new CameraBusinessRegressionTests().ExecuteSavedCommissioningRecipe(versionB!,
                ControlledCommissioningTests.Inputs.EvidencePath("edit-isolation-run-b.json"));
            await app.StopAsync();
        }
        finally { Directory.Delete(root, true); }
    }

    // Only the host-purpose boundary is supplied here. No hardware/configuration
    // readiness is asserted by this authoring HTTP test.
    private sealed class PurposeOnlyConfiguration : IPublicConfiguration
    {
        public LoadedConfiguration<PublicConfiguration> LoadPublic(ConfigReference reference) => new(
            new("1", reference.Id, reference.Version, Purpose, "OfflineHttpFixture", [],
                null!, null!, null!, null!, null!, "", ""), "{}", "offline", "offline");
        public LoadedConfiguration<PublicConfiguration> SavePublicPositions(ConfigReference reference,
            FixedPoint threeD, FixedPoint manualLoading, string expectedDigest) => throw new NotSupportedException();
        public LoadedConfiguration<BusinessBudget> LoadBudget(ConfigReference reference) => throw new NotSupportedException();
        public LoadedConfiguration<SimulationProfile> LoadSimulation(ConfigReference reference) => throw new NotSupportedException();
    }

    [Fact]
    public async Task NewRecipeReloadsWithoutApprovalAndFreezesCurrentVersion()
    {
        var root = Path.Combine(Path.GetTempPath(), "gaode-recipe-commissioning-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var storeRoot = Path.Combine(root, "store");
        try
        {
            RecipeStoreSchema.Prepare(root, storeRoot);
            var options = new RecipeStoreOptions { DatabasePath = Path.Combine(storeRoot, "recipes.db"),
                ReadWriteTimeoutMs = 10000, DbLockTimeoutSeconds = 5 };
            RecipeDefinition original;
            using (var store = new SqliteRecipeStore(options, root, NullLogger<SqliteRecipeStore>.Instance, () => "offline-test"))
            {
                var saved = await store.SaveAsync(new(Recipe011Data.Candidate(1, false), null, null, Guid.NewGuid().ToString()), default);
                Assert.Equal(RecipeSaveStatus.Saved, saved.Status);
                original = saved.Definition!;
                Assert.Empty(original.Approval.Purpose);
            }
            using var reloaded = new SqliteRecipeStore(options, root, NullLogger<SqliteRecipeStore>.Instance, () => "offline-test");
            var recipe = reloaded.GetSnapshot().Definitions.Single();
            Assert.Equal(original.DefinitionDigest, recipe.DefinitionDigest);
            Assert.True(RecipeAdmission.Evaluate(recipe, ["s1"], Purpose).Eligible);
            Assert.False(RecipeAdmission.Evaluate(recipe, ["s1"], "Test").Eligible);
            Assert.False(RecipeAdmission.Evaluate(recipe, ["s1"], "Production").Eligible);
            Assert.False(RecipeAdmission.Evaluate(recipe, ["missing-slot"], Purpose).Eligible);
            var plan = RecipeRunPlanner.BuildExecutable(recipe, Guid.NewGuid().ToString(), ["s1"], Purpose);
            var registry = new CapabilityRegistry();
            foreach (var r in recipe.AlgorithmRequirements.Values)
                registry.RegisterAlgorithm(r.Purpose, r.Id, "1", r.ResultContract, r.InputCount,
                    "DeclaredOfflineFixture", "1", Purpose, "OfflineOnly");
            var cost = new ExecutionCostProfile("fixture", "1", Purpose, "OfflineOnly", "fixture/1", "fixture-budget", 100, 100, 100, 100, 0)
                { CaptureWaitMs = 100, AlgorithmWaitMs = 100, InputReleaseWaitMs = 100 };
            var frozen = RecipeAdmission.Freeze(Guid.NewGuid(), Guid.NewGuid(), plan, registry, cost, Purpose);
            Assert.True(RecipeAdmission.MatchesRunPurpose(frozen, Gaode.Domain.Station01.RunPurpose.Commissioning));
            Assert.False(RecipeAdmission.MatchesRunPurpose(frozen, Gaode.Domain.Station01.RunPurpose.Test));
            var oldRevision = frozen.PlanRevision;
            var edited = await reloaded.SaveAsync(new(recipe with { Model = "edited-model" }, recipe.RecipeId,
                recipe.Version, Guid.NewGuid().ToString()), default);
            Assert.Equal(RecipeSaveStatus.Saved, edited.Status);
            Assert.NotEqual(recipe.Version, edited.Definition!.Version);
            Assert.True(RecipeAdmission.Evaluate(edited.Definition, ["s1"], Purpose).Eligible);
            Assert.Equal(oldRevision, frozen.PlanRevision);
            Assert.Equal(recipe.Version, frozen.Plan.RecipeVersion);
            Assert.False(RecipeAdmission.Evaluate(recipe with { DefinitionDigest = "stale" }, ["s1"], Purpose).Eligible);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void LegacyTestAdmissionStillRequiresItsExistingApproval()
    {
        var recipe = Recipe011Data.ForSlots(1, 1);
        Assert.True(RecipeAdmission.Evaluate(recipe, ["s1"], "Test").Eligible);
        Assert.False(RecipeAdmission.Evaluate(recipe, ["s1"], "Production").Eligible);
        Assert.False(RecipeAdmission.Evaluate(recipe, ["s1"], "UnknownPurpose").Eligible);
    }
}
