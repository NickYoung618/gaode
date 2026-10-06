using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Domain.Station01;
using Gaode.Host.Api;
using Gaode.Integration.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Gaode.Application.Recipes;
using Gaode.Infrastructure.Recipes;
using Gaode.Integration.Tests.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Gaode.Integration.Tests.Station01;

public sealed class RecipeAuthoringBindingTests
{
    [Fact]
    public async Task SavedCatalogFeedsTheCommonExactMatcherAndOldFrozenContentSurvivesANewSave()
    {
        var (allowed, root) = RecipeAuthoringTestRoots.New(); RecipeStoreSchema.Prepare(allowed, root);
        var options = new RecipeStoreOptions { DatabasePath = Path.Combine(root, "recipes.db"), ReadWriteTimeoutMs = 10000, DbLockTimeoutSeconds = 1 };
        using var store = new SqliteRecipeStore(options, allowed, NullLogger<SqliteRecipeStore>.Instance, () => "component-author");
        var first = await store.SaveAsync(new(RecipeAuthoringTestInputs.Candidate(), null, null, "matcher-first"), CancellationToken.None);
        Assert.Equal(RecipeSaveStatus.Saved, first.Status);
        var definition = first.Definition!;
        var snapshot = store.GetSnapshot();
        var selected = new RecipeSelectionIntent(definition.RecipeId, definition.Version, snapshot.CatalogDigest);
        var match = RecipeMatcher.Match(snapshot, definition.FCode, selected, definition.ScenarioId, "Test");
        // The code resolves the saved identity but cannot invent approval for a new draft.
        Assert.Equal(RecipeMatchStatus.Restricted, match.Status);
        Assert.Equal("RecipeApprovalMissing", match.Reason);
        Assert.Equal(definition.RecipeId, match.RecipeId);
        Assert.Equal(RecipeMatchStatus.Unmatched,
            RecipeMatcher.Match(snapshot, definition.FCode.Trim(), null, definition.ScenarioId, "Test").Status);
        var frozen = RecipeCatalogSnapshots.Freeze(match.Definition!);
        var oldBody = RecipeDefinitionSerialization.Serialize(frozen);
        var update = await store.SaveAsync(new(definition with { Model = "next-f-content" },
            definition.RecipeId, definition.Version, "matcher-update"), CancellationToken.None);
        Assert.Equal(RecipeSaveStatus.Saved, update.Status);
        var next = RecipeMatcher.Match(store.GetSnapshot(), definition.FCode, selected, definition.ScenarioId, "Test");
        Assert.Equal(update.Version, next.Version); Assert.Equal("next-f-content", next.Definition!.Model);
        Assert.Equal(oldBody, RecipeDefinitionSerialization.Serialize(frozen));
        Assert.Equal(definition.Version, Assert.Single(snapshot.Definitions).Version);
        // Actual Bound/Handoff/run continuation and frozen execution are joint-chain evidence from 011.
        // This component test does not synthesize a binding receipt, device completion or production approval.
    }
}

public sealed class RecipeAuthoringPlanEndpointsTests
{
    [Fact]
    public async Task PlanAndBindRequireTheActualCommittedHandoffAndKeepExistingAuthorization()
    {
        // Real isolated Host/SQLite. A declared initial run is persisted; no device,
        // F result, frozen plan or successful binding is synthesized for this refusal proof.
        await using var host = await Station01HostFixture.CreateAsync();
        var runId = Guid.NewGuid();
        var payload = new RunCreatedPayload(Guid.NewGuid(), "reader-api-initial", "test:Operator",
            "{}", "{}", "{}", "{}", "declared-initial", "public", "budget", "simulation");
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var batch = new WriteBatch(Guid.NewGuid(), runId, 0, WriteKind.RunCreated, json,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var writer = host.Host.Services.GetRequiredService<ITraceWriter>();
        Assert.Equal(CommitState.Committed, (await writer.SubmitCritical(batch).Completion.WaitAsync(timeout.Token)).State);
        foreach (var endpoint in new[] { "plan", "bind" })
        {
            using var missing = await host.Client.PostAsJsonAsync("/api/v1/recipes/" + endpoint,
                new RecipePlanRequest(runId, "declared-scope", ["slot-one"]), timeout.Token);
            Assert.Equal(HttpStatusCode.Conflict, missing.StatusCode);
            Assert.Contains("HandoffNotReady", await missing.Content.ReadAsStringAsync(timeout.Token));
            using var invalid = await host.Client.PostAsJsonAsync("/api/v1/recipes/" + endpoint,
                new RecipePlanRequest(Guid.Empty, "declared-scope", ["slot-one"]), timeout.Token);
            Assert.Equal(HttpStatusCode.Conflict, invalid.StatusCode);
            Assert.Contains("BindingHandoffInputMismatch", await invalid.Content.ReadAsStringAsync(timeout.Token));
        }
        using var anonymous = host.Host.CreateClient();
        using var denied = await anonymous.PostAsJsonAsync("/api/v1/recipes/bind",
            new RecipePlanRequest(runId, "declared-scope", ["slot-one"]), timeout.Token);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Equal(0, host.CommittedRecipeBindings);
        Assert.Equal(1, (await host.Host.Services.GetRequiredService<ITraceQuery>().GetRunAsync(runId, timeout.Token))!.Revision);
    }
}
