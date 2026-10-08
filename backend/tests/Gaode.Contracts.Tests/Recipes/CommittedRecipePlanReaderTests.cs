using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Application.Station01;
using Gaode.Contracts.Tests.Support;
using Gaode.Domain.Station01;
using Xunit;

namespace Gaode.Contracts.Tests.Recipes;

public sealed class CommittedRecipePlanReaderTests
{
    [Fact]
    public async Task ReadsOnlyCommittedFrozenPlanWithoutCatalogOrV1Fallback()
    {
        var fixture = new Query();
        var result = await fixture.Read();
        Assert.NotNull(result);
        Assert.True(result.IsValid);
        Assert.Equal(fixture.Plan.RecipeVersion, result.Plan.RecipeVersion);
        Assert.Equal(RecipePlanRevision.Compute(fixture.Plan), result.PlanRevision);
        Assert.Throws<NotSupportedException>(() => ((IList<RecipeStep>)result.Plan.Steps).Clear());
    }

    [Fact]
    public async Task OldHandoffCannotProvideCurrentExecutionInputs()
    {
        var fixture = new Query { HasV2 = false };
        Assert.Null(await fixture.Read()); // GetHandoffAsync throws if accidentally called.
    }

    [Fact]
    public async Task MissingFrozenPayloadCannotBeRebuiltFromRecipeHead()
    {
        var fixture = new Query();
        fixture.EditIntent(root => root.Remove("frozenExecutionInputs"));
        Assert.Equal("CommittedExecutionInputsMissing", (await Assert.ThrowsAsync<InvalidOperationException>(fixture.Read)).Message);
    }

    [Fact]
    public async Task ChangedIntentWithoutItsActualDigestIsRejected()
    {
        var fixture = new Query();
        var intent = fixture.Inputs.Writes.Single(w => w.Kind == WriteKind.ActionIntent);
        fixture.Inputs.Writes[fixture.Inputs.Writes.IndexOf(intent)] = intent with { PayloadJson = intent.PayloadJson + " " };
        Assert.Equal("CommittedExecutionInputsMissing", (await Assert.ThrowsAsync<InvalidOperationException>(fixture.Read)).Message);
    }

    [Fact]
    public async Task DifferentScenarioOrPhysicalScopeCannotUseTheFrozenPlan()
    {
        var fixture = new Query();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new CommittedRecipePlanReader(fixture, fixture)
            .ReadAsync(fixture.Run.RunId, "other", ["s1"], default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new CommittedRecipePlanReader(fixture, fixture)
            .ReadAsync(fixture.Run.RunId, fixture.Plan.ScenarioId, ["other"], default));
    }

    [Fact]
    public async Task CancelledRunCannotAcquireAnotherBinding()
    {
        var fixture = new Query();
        fixture.Run = fixture.Run with { CancelRequested = true };
        Assert.Equal("BindingRunCancelled", (await Assert.ThrowsAsync<InvalidOperationException>(fixture.Read)).Message);
    }

    // Declared component persisted facts; real SQLite/save evidence is separate.
    private sealed class Query : ITraceQuery, IStageHandoffQuery
    {
        public RecipeRunPlan Plan { get; }
        public SemanticHandoffInputs Inputs { get; }
        public PersistedRun Run { get; set; }
        public bool HasV2 { get; init; } = true;
        public Query()
        {
            var runId = Guid.NewGuid(); var trayId = Guid.NewGuid();
            Plan = Recipe011Data.Plan(trayId);
            var identity = new WorkflowIdentity(runId, trayId, Guid.NewGuid().ToString(), Guid.NewGuid().ToString(),
                "reader-component", Plan.ScenarioId, ["s1"], DateTimeOffset.UtcNow, "component", RunPurpose.Test,
                "public/1", "budget/1", "simulation/1");
            var handoff = new PublicPreparationHandoffV2(PublicPreparationHandoffV2.CurrentSchemaVersion, Guid.NewGuid(), identity,
                "points/1", "capabilities/1", ["media://3d"], ["media://f"], ["result://f"], Plan.FCode,
                $"recipe-plan://{runId:D}/{RecipePlanRevision.Compute(Plan)}", RecipePlanRevision.Compute(Plan),
                "recipe-binding://" + Guid.NewGuid().ToString("D"), ComponentEvidenceSource.Test, "DeclaredComponent",
                ["component://evidence"], Guid.NewGuid(), 10, DateTimeOffset.UtcNow, "");
            Inputs = new(handoff, Plan);
            EditIntent(root => root["bindingId"] = Inputs.Receipt.BindingId);
            Run = new(runId, identity.RequestId, "component", "{}", RunState.SavingHandoff, 10, TerminalOutcome.None, null, false);
        }
        public void EditIntent(Action<JsonObject> edit)
        {
            var intent = Inputs.Writes.Single(w => w.Kind == WriteKind.ActionIntent);
            var node = JsonNode.Parse(intent.PayloadJson)!.AsObject(); edit(node);
            var json = node.ToJsonString();
            Inputs.Writes[Inputs.Writes.IndexOf(intent)] = intent with { PayloadJson = json,
                PayloadDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))) };
        }
        public Task<FrozenExecutionInputs?> Read() => new CommittedRecipePlanReader(this, this)
            .ReadAsync(Run.RunId, Plan.ScenarioId, ["s1"], default);
        public Task<Gaode.Application.Station01.StartReceipt?> GetStartReceiptAsync(string subject, string requestId, CancellationToken token) =>
            Inputs.GetStartReceiptAsync(subject, requestId, token);
        public Task<PersistedRun?> GetRunAsync(Guid id, CancellationToken token) => Task.FromResult<PersistedRun?>(Run);
        public Task<IReadOnlyList<PersistedWrite>> GetWritesAsync(Guid id, CancellationToken token) => Inputs.GetWritesAsync(id, token);
        public Task<PersistedWrite?> GetWriteAsync(Guid id, CancellationToken token) => Inputs.GetWriteAsync(id, token);
        public Task<IReadOnlyList<PersistedRun>> GetUnfinishedRunsAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<PersistedRun>>([]);
        public Task<PersistedHandoff?> GetHandoffAsync(Guid id, CancellationToken token) => throw new InvalidOperationException("LegacyExecutionReadForbidden");
        public Task<CommittedPublicPreparationHandoffV2?> GetCommittedV2Async(Guid id, CancellationToken token)
        {
            var h = Inputs.Handoff;
            return Task.FromResult<CommittedPublicPreparationHandoffV2?>(HasV2 ? new(h, h.HandoffId, h.WriteId, h.CommittedRevision, h.PayloadDigest) : null);
        }
    }
}
