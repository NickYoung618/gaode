using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Configuration;
using Gaode.Application.Motion;
using Gaode.Application.Ports;
using Gaode.Application.Station01;
using Gaode.Application.Timing;
using Gaode.Application.Workflow;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;

namespace Gaode.Application.Recipes;

public sealed class IndependentRecipeApplication(ITraceQuery traces, IStageHandoffQuery handoffs,
    MotionCoordinator motion, DeadlineScheduler time, IStageEventStore stageEvents)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task<RecipeBindingReceipt> ExecuteAsync(Guid runId, RecipeRunPlan plan,
        int? ngCapacity, int? pendingCapacity, CancellationToken token)
    {
        var persisted = await traces.GetRunAsync(runId, token) ?? throw new InvalidOperationException("BindingRunNotFound");
        if (persisted.CancelRequested || persisted.Terminal == TerminalOutcome.Cancelled)
            throw new InvalidOperationException("BindingRunCancelled");
        var frozenInputs = await new CommittedRecipePlanReader(traces, handoffs).ReadAsync(runId, plan.ScenarioId,
            plan.Steps.Select(s => s.SlotId).OfType<string>().Distinct(StringComparer.Ordinal).ToArray(), token)
            ?? throw new InvalidOperationException("CommittedHandoffV2Required");
        if (RecipePlanRevision.Compute(plan) != frozenInputs.PlanRevision || ngCapacity != frozenInputs.Plan.NgCapacity ||
            pendingCapacity != frozenInputs.Plan.PendingCapacity)
            throw new InvalidOperationException("BindingFrozenPlanMismatch");
        plan = frozenInputs.Plan;
        var v2 = await handoffs.GetCommittedV2Async(runId, token);
        if (v2 is not { IsVerified: true } || v2.Handoff.PlanRevision != frozenInputs.PlanRevision)
            throw new InvalidOperationException("CommittedHandoffV2Required");
        var writes = await traces.GetWritesAsync(runId, token);
        var frozenFact = writes.Where(w => w.State == CommitState.Committed && w.Kind == WriteKind.Audit)
            .SingleOrDefault(w => Kind(w.PayloadJson) == "FrozenPublicConfiguration")
            ?? throw new InvalidOperationException("FrozenBindingBudgetNotRecorded");
        using var frozenDoc = JsonDocument.Parse(frozenFact.PayloadJson);
        var f = frozenDoc.RootElement;
        if (!f.TryGetProperty("snapshotId", out var snapshot) || snapshot.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(snapshot.GetString()))
            throw new InvalidOperationException("FrozenBindingSnapshotNotRecorded");
        var config = ConfigurationFreezer.RestoreAudit(f, $"audit://{frozenFact.WriteId:D}");
        if (config.SnapshotId != snapshot.GetString() || config.Public.Purpose != frozenInputs.CostProfile.Purpose ||
            !RecipeAdmission.MatchesRunPurpose(frozenInputs, v2.Handoff.Identity.Purpose))
            throw new InvalidOperationException("FrozenBindingSnapshotMismatch");
        var deadlines = new List<DateTimeOffset>();
        var deadlineReferences = new List<RecipeDeadlineReference>();
        var completeDeadlineOrigins = true;
        foreach (var fact in writes.Where(w => w.State == CommitState.Committed && Kind(w.PayloadJson) == "RecipeExecutionDeadlinesFrozen"))
        {
            using var d = JsonDocument.Parse(fact.PayloadJson);
            var existing = d.RootElement.GetProperty("routeDeadlines").Deserialize<RecipeExecutionDeadlines>(Json)!;
            deadlines.AddRange([existing.DetectionDeadlineUtc, existing.SortingDeadlineUtc, existing.UnloadDeadlineUtc]);
            deadlineReferences.AddRange([new("Detection", existing.StartedUtc, existing.DetectionDeadlineUtc),
                new("Sorting", existing.StartedUtc, existing.SortingDeadlineUtc),
                new("UnloadPreparation", existing.StartedUtc, existing.UnloadDeadlineUtc)]);
        }
        if (v2 is { IsVerified: true })
            foreach (var stage in new[] { WholeTrayWorkflowStage.Detection, WholeTrayWorkflowStage.UnloadPreparation, WholeTrayWorkflowStage.Sorting })
                foreach (var existing in (await stageEvents.ReadAsync(runId, v2.Handoff.Identity.TrayId, stage, token))
                    .Where(e => e.StageDeadlineAtUtc.HasValue))
                {
                    deadlines.Add(existing.StageDeadlineAtUtc!.Value);
                    if (existing.StageStartedAtUtc is { } beginning)
                        deadlineReferences.Add(new(stage.ToString(), beginning, existing.StageDeadlineAtUtc.Value));
                    else completeDeadlineOrigins = false;
                }
        var context = StartRunContextParser.Parse(persisted.ContextJson);
        if (v2 is { IsVerified: true } && v2.Handoff.Identity.TrayId != context.TrayId)
            throw new InvalidOperationException("BindingTrayIdentityMismatch");
        var current = motion.Observe();
        if (!current.HasReliableObservation || current.SafetyAssessment != SafetyAssessment.Clear ||
            current.Readiness != DeviceReadiness.Ready || !motion.IsHeld(runId))
            throw new InvalidOperationException("RecipeApplicationAdmissionRejected");
        var bindingId = Guid.NewGuid();
        var saves = new IndependentBindingEventWriter(stageEvents, runId, context.TrayId,
            context.StationId, context.LineId, bindingId, current.ConnectionEpoch, time.Clock);
        var run = new RunExecution(runId, Guid.NewGuid(), persisted.RequestId, persisted.SubjectId,
            persisted.ContextJson, config, saves, time.Clock, Guid.NewGuid(), time.ClockId);
        run.ExecutionInputs = frozenInputs;
        if (v2 is { IsVerified: true }) run.FreezeIdentity(v2.Handoff.Identity);
        // A new binding has its own save stream; original Run.Revision remains untouched.

        try
        {
            var result = await new RecipeApplicationCoordinator().ExecuteAsync(run, plan,
                RecipePlanRevision.Compute(plan), bindingId, current.ConnectionEpoch, ngCapacity, pendingCapacity,
                deadlines, _ => Task.FromResult<PublicPreparationHandoffV2?>(null), token,
                completeDeadlineOrigins ? deadlineReferences.Distinct().ToArray() : null);

            // The input handoff remains unchanged. This receipt does not authorize repeated product actions.
            return result.Receipt;
        }
        catch { throw; }
    }
    private static string? Kind(string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        return doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("kind", out var kind)
            ? kind.GetString() : null;
    }
}
