using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Domain.Station01;

namespace Gaode.Application.Recipes;

// Current run reads consume the persisted F binding. This has no catalog or
// planner dependency and cannot authorize another product execution.
public sealed class CommittedRecipePlanReader(ITraceQuery traces, IStageHandoffQuery handoffs)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<FrozenExecutionInputs?> ReadAsync(Guid runId, string scenarioId,
        IReadOnlyList<string> occupiedSlots, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (runId == Guid.Empty || string.IsNullOrWhiteSpace(scenarioId) || occupiedSlots is null ||
            occupiedSlots.Count == 0 || occupiedSlots.Any(string.IsNullOrWhiteSpace) ||
            occupiedSlots.Distinct(StringComparer.Ordinal).Count() != occupiedSlots.Count)
            throw new InvalidOperationException("BindingHandoffInputMismatch");
        var persisted = await traces.GetRunAsync(runId, token)
            ?? throw new InvalidOperationException("BindingRunNotFound");
        if (persisted.CancelRequested || persisted.Terminal == TerminalOutcome.Cancelled)
            throw new InvalidOperationException("BindingRunCancelled");
        var committed = await handoffs.GetCommittedV2Async(runId, token);
        if (committed is not { IsVerified: true }) return null;
        var handoff = committed.Handoff;
        if (handoff.Identity.RunId != runId || handoff.Identity.ScenarioId != scenarioId)
            throw new InvalidOperationException("BindingHandoffInputMismatch");
        var writes = await traces.GetWritesAsync(runId, token);
        FrozenExecutionInputs? result = null;
        foreach (var write in writes.Where(w => w.RunId == runId && w.Kind == WriteKind.ActionIntent &&
            w.State == CommitState.Committed && w.Revision < handoff.CommittedRevision))
        {
            using var document = JsonDocument.Parse(write.PayloadJson);
            var root = document.RootElement;
            if (!root.TryGetProperty("kind", out var kind) || kind.GetString() != "RecipePlanAndBindingIntent" ||
                !root.TryGetProperty("bindingId", out var binding) ||
                handoff.RecipeBindingReference != "recipe-binding://" + binding.GetString()) continue;
            if (write.PayloadDigest != Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(write.PayloadJson))) ||
                !root.TryGetProperty("frozenExecutionInputs", out var frozen) || frozen.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("CommittedExecutionInputsMissing");
            var inputs = frozen.Deserialize<FrozenExecutionInputs>(Json);
            if (inputs is null || !inputs.IsValid || inputs.RunId != runId || inputs.TrayId != handoff.Identity.TrayId ||
                inputs.PlanRevision != handoff.PlanRevision || inputs.Plan.FCode != handoff.UniqueFCode ||
                inputs.Plan.ScenarioId != scenarioId || inputs.Plan.Approval.Purpose != handoff.Identity.Purpose.ToString() ||
                !inputs.Plan.Steps.Select(s => s.SlotId).OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
                    .SequenceEqual(occupiedSlots.Order(StringComparer.Ordinal), StringComparer.Ordinal))
                throw new InvalidOperationException("CommittedExecutionInputsMismatch");
            if (result is not null) throw new InvalidOperationException("CommittedExecutionInputsAmbiguous");
            result = inputs;
        }
        token.ThrowIfCancellationRequested();
        if (result is null) throw new InvalidOperationException("CommittedExecutionInputsMissing");
        return result with { Plan = RecipeCatalogSnapshots.Freeze(result.Plan),
            Capabilities = RecipeCatalogSnapshots.Map(result.Capabilities) };
    }
}
