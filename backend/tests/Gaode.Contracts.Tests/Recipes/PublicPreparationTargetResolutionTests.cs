using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Application.Station01;
using Gaode.Domain.Station01;
using Gaode.Contracts.Tests.Support;
using Xunit;

namespace Gaode.Contracts.Tests.Recipes;

public sealed class PublicPreparationTargetResolutionTests
{
    [Fact]
    public async Task CommittedHandoffResolvesBothFacesFromFrozenConfiguration()
    {
        var plan = Recipe011Data.Plan(Guid.NewGuid(), 2);
        var query = Query(plan);
        var request = await Resolve(plan, query);
        Assert.Equal(new[] { 1, 1, 2, 2 }, request.Targets!.Select(t => t.LocalFace!.Value));
        Assert.Equal(new[] { 1, 1, 2, 2 }, request.Targets!.Select(t => t.CoordinateEpoch));
        Assert.All(request.Targets!, t => { Assert.Equal(10, t.Point.X); Assert.Equal(20, t.Point.Y); Assert.Equal(3, t.Point.Z); });
        Assert.NotNull(request.InitialObservation);
        Assert.NotNull(request.InitialObservationWriteId);
    }

    [Theory]
    [InlineData("face")]
    [InlineData("physical-slot")]
    [InlineData("stage")]
    public void FutureTargetRejectsWrongIdentity(string field)
    {
        var recipe = Recipe011Data.ForSlots(2, 1);
        var input = recipe.ExecutionPositions["s1"].PhysicalEntity;
        var coordinates = input.Coordinates.ToArray();
        var i = Array.FindIndex(coordinates, c => c.LocalFace == 2);
        coordinates[i] = field switch { "face" => coordinates[i] with { LocalFace = 1 },
            "stage" => coordinates[i] with { StageId = "stage:1" }, _ => coordinates[i] with { PhysicalSlotIndex = 2 } };
        var changed = recipe with { ExecutionPositions = new Dictionary<string, SlotExecutionInputs>
            { ["s1"] = recipe.ExecutionPositions["s1"] with { PhysicalEntity = input with { Coordinates = coordinates } } } };
        Assert.False(RecipeDefinitionValidator.ValidateForSave(changed, RecipeCatalogSnapshots.Create("empty", [])).Valid);
    }

    [Fact]
    public async Task MissingObservationCannotBecomeHeightDerivedPermission()
    {
        var plan = Recipe011Data.Plan(Guid.NewGuid());
        var query = Query(plan);
        var observationCall = query.Inputs.Observation.CallId.ToString();
        query.Inputs.Writes.RemoveAll(w => w.Kind == WriteKind.AlgorithmFact && w.PayloadJson.Contains(observationCall));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Resolve(plan, query));
        Assert.Equal("CommittedTrayObservationRequired", error.Message);
    }

    [Fact]
    public async Task MissingFrozenPointIsRejectedWithoutZeroOrHeightFallback()
    {
        var plan = Recipe011Data.Plan(Guid.NewGuid()) with { ExecutionPositions = new Dictionary<string, SlotExecutionInputs>() };
        var query = Query(plan);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Resolve(plan, query));
        Assert.Equal("ProductPointCoordinatesMissing", error.Message);
    }

    private static Task<DetectionRequest> Resolve(RecipeRunPlan plan, FixedHandoffQuery query)
    {
        var handoff = query.Value.Handoff;
        return new PublicPreparationHandoffV2Consumer(query, query.Trace).CreateDetectionRequestAsync(
            handoff.Identity.RunId, handoff.Identity.TrayId, handoff.PlanRevision, Guid.NewGuid(), 1,
            DateTimeOffset.UtcNow.AddMinutes(2), "component", plan, motionConfiguration: Recipe011Data.Motion(),
            recipeApplicationReceipt: query.Inputs.Receipt);
    }
    private static FixedHandoffQuery Query(RecipeRunPlan plan)
    {
        var identity = new WorkflowIdentity(Guid.NewGuid(), Guid.Parse(plan.TrayRunId), Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(), "component", plan.ScenarioId, ["s1"], DateTimeOffset.UtcNow, "component", RunPurpose.Test,
            "component-public", "component-budget", "component-simulation");
        var handoff = new PublicPreparationHandoffV2(PublicPreparationHandoffV2.CurrentSchemaVersion, Guid.NewGuid(), identity,
            "component-points", "component-capabilities", ["media://component-3d"], ["media://component-f"], ["result://component"],
            plan.FCode, "plan://component", RecipePlanRevision.Compute(plan), "recipe-binding://component", ComponentEvidenceSource.Test,
            "DeclaredComponent", ["component://source"], Guid.NewGuid(), 10, DateTimeOffset.UtcNow, "");
        handoff = handoff with { PayloadDigest = PublicPreparationHandoffV2.ComputePayloadDigest(handoff) };
        return new(new(handoff, handoff.HandoffId, handoff.WriteId, handoff.CommittedRevision, handoff.PayloadDigest), plan);
    }
    private sealed class FixedHandoffQuery : IStageHandoffQuery
    {
        public CommittedPublicPreparationHandoffV2 Value { get; private set; } = null!;
        public SemanticHandoffInputs Inputs { get; private set; } = null!;
        public ITraceQuery Trace => new CurrentTrace(this);
        public FixedHandoffQuery(CommittedPublicPreparationHandoffV2 value, RecipeRunPlan plan) => Configure(value.Handoff, plan);
        public void Configure(PublicPreparationHandoffV2 handoff, RecipeRunPlan plan)
        {
            Inputs = new(handoff, plan);
            var h = Inputs.Handoff;
            Value = new(h, h.HandoffId, h.WriteId, h.CommittedRevision, h.PayloadDigest);
        }
        public Task<PersistedHandoff?> GetHandoffAsync(Guid runId, CancellationToken token) => Task.FromResult<PersistedHandoff?>(null);
        public Task<CommittedPublicPreparationHandoffV2?> GetCommittedV2Async(Guid runId, CancellationToken token) =>
            Task.FromResult<CommittedPublicPreparationHandoffV2?>(Value);
        private sealed class CurrentTrace(FixedHandoffQuery owner) : ITraceQuery
        {
            public Task<Gaode.Application.Station01.StartReceipt?> GetStartReceiptAsync(string subject, string requestId, CancellationToken token) =>
                owner.Inputs.GetStartReceiptAsync(subject, requestId, token);
            public Task<PersistedRun?> GetRunAsync(Guid id, CancellationToken token) => owner.Inputs.GetRunAsync(id, token);
            public Task<IReadOnlyList<PersistedWrite>> GetWritesAsync(Guid id, CancellationToken token) => owner.Inputs.GetWritesAsync(id, token);
            public Task<PersistedWrite?> GetWriteAsync(Guid id, CancellationToken token) => owner.Inputs.GetWriteAsync(id, token);
            public Task<IReadOnlyList<PersistedRun>> GetUnfinishedRunsAsync(CancellationToken token) => owner.Inputs.GetUnfinishedRunsAsync(token);
        }
    }
}
