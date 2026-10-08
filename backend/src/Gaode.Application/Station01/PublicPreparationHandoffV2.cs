using Gaode.Application.Ports;
using Gaode.Domain.Station01;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Recipes;
using Gaode.Domain.Configuration;
using Gaode.Application.Workflow;

namespace Gaode.Application.Station01;

public sealed record PublicPreparationHandoffV2(
    string SchemaVersion,
    Guid HandoffId,
    WorkflowIdentity Identity,
    string FrozenPointRevision,
    string CapabilitySummaryDigest,
    IReadOnlyList<string> ThreeDMediaReferences,
    IReadOnlyList<string> FMediaReferences,
    IReadOnlyList<string> ResultReferences,
    string UniqueFCode,
    string RecipeRunPlanReference,
    string PlanRevision,
    string RecipeBindingReference,
    ComponentEvidenceSource Source,
    string Quality,
    IReadOnlyList<string> EvidenceReferences,
    Guid WriteId,
    long CommittedRevision,
    DateTimeOffset PersistedAt,
    string PayloadDigest)
{
    public const string CurrentSchemaVersion = "s01-handoff/2.0";

    public bool IsComplete => SchemaVersion == CurrentSchemaVersion && HandoffId != Guid.Empty &&
        Identity is not null && Identity.RunId != Guid.Empty && Identity.TrayId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(Identity.StationId) && !string.IsNullOrWhiteSpace(Identity.LineId) &&
        Identity.OccupiedSlots is not null && Identity.OccupiedSlots.All(NotBlank) &&
        NotBlank(FrozenPointRevision) && NotBlank(CapabilitySummaryDigest) &&
        ThreeDMediaReferences is { Count: > 0 } && ThreeDMediaReferences.All(NotBlank) &&
        FMediaReferences is { Count: > 0 } && FMediaReferences.All(NotBlank) &&
        ResultReferences is { Count: > 0 } && ResultReferences.All(NotBlank) &&
        NotBlank(UniqueFCode) && NotBlank(RecipeRunPlanReference) && NotBlank(PlanRevision) &&
        NotBlank(RecipeBindingReference) && NotBlank(Quality) &&
        EvidenceReferences is { Count: > 0 } && EvidenceReferences.All(NotBlank) &&
        WriteId != Guid.Empty && CommittedRevision > 0 &&
        PersistedAt > DateTimeOffset.MinValue && NotBlank(PayloadDigest) &&
        StringComparer.Ordinal.Equals(PayloadDigest, ComputePayloadDigest(this));

    private static bool NotBlank(string value) => !string.IsNullOrWhiteSpace(value);

    public static string ComputePayloadDigest(PublicPreparationHandoffV2 value)
    {
        var canonical = JsonSerializer.Serialize(value with { PayloadDigest = "" },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}

public sealed record CommittedPublicPreparationHandoffV2(
    PublicPreparationHandoffV2 Handoff,
    Guid PersistedHandoffId,
    Guid PersistedWriteId,
    long PersistedRevision,
    string PersistedPayloadDigest)
{
    public bool IsVerified => Handoff.IsComplete &&
        PersistedHandoffId == Handoff.HandoffId && PersistedWriteId == Handoff.WriteId &&
        PersistedRevision == Handoff.CommittedRevision &&
        StringComparer.Ordinal.Equals(PersistedPayloadDigest, Handoff.PayloadDigest);
}

public sealed class PublicPreparationHandoffV2Consumer(IStageHandoffQuery handoffs, ITraceQuery trace, TimeProvider? clock = null)
{
    public async Task<DetectionRequest> CreateDetectionRequestAsync(
        Guid expectedRunId,
        Guid expectedTrayId,
        string expectedPlanRevision,
        Guid operationId,
        long connectionEpoch,
        DateTimeOffset deadline,
        string idempotencyKey,
        RecipeRunPlan? plan = null,
        CancellationToken cancellationToken = default,
        DateTimeOffset? stageStartedAtUtc = null,
        PublicConfiguration? motionConfiguration = null,
        RecipeBindingReceipt? recipeApplicationReceipt = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var committed = await handoffs.GetCommittedV2Async(expectedRunId, cancellationToken);
        if (committed is null || !committed.IsVerified)
            throw new InvalidOperationException("CommittedHandoffV2Required");
        var handoff = committed.Handoff;
        cancellationToken.ThrowIfCancellationRequested();
        if (recipeApplicationReceipt is not { WasCompletedInWindow: true } approval ||
            approval.Correlation.RunId != expectedRunId || approval.Correlation.TrayId != expectedTrayId ||
            approval.Correlation.ConnectionEpoch != connectionEpoch || approval.Correlation.PlanRevision != expectedPlanRevision ||
            handoff.RecipeBindingReference != $"recipe-binding://{approval.BindingId}" ||
            !approval.RequiredCommits.Any(c => c.WriteId == handoff.WriteId && c.PersistedRevision == handoff.CommittedRevision))
            throw new InvalidOperationException("CurrentRecipeApplicationReceiptRequired");
        if ((clock ?? TimeProvider.System).GetUtcNow() >= deadline)
            throw new TimeoutException("ExistingDetectionDeadlineExpired");
        handoff.Identity.EnsureScope(expectedRunId, expectedTrayId);
        if (!StringComparer.Ordinal.Equals(handoff.PlanRevision, expectedPlanRevision))
            throw new InvalidOperationException("HandoffPlanRevisionMismatch");
        if (!Guid.TryParse(handoff.Identity.StationId, out var stationId) ||
            !Guid.TryParse(handoff.Identity.LineId, out var lineId))
            throw new InvalidOperationException("HandoffStationLineIdentityInvalid");
        if (plan is not null && !StringComparer.Ordinal.Equals(
            RecipePlanRevision.Compute(plan), handoff.PlanRevision))
            throw new InvalidOperationException("PersistedRecipePlanRevisionMismatch");
        var writes = await trace.GetWritesAsync(expectedRunId, cancellationToken);
        var intent = writes.SingleOrDefault(w => w.WriteId == approval.IntentCommit?.WriteId &&
            w.RunId == expectedRunId && w.Kind == WriteKind.ActionIntent && w.State == CommitState.Committed);
        if (intent is null || intent.PayloadDigest != Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(intent.PayloadJson))))
            throw new InvalidOperationException("CommittedExecutionInputsMissing");
        using var document = JsonDocument.Parse(intent.PayloadJson);
        if (!document.RootElement.TryGetProperty("frozenExecutionInputs", out var stored) || stored.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("CommittedExecutionInputsMissing");
        var inputs = stored.Deserialize<FrozenExecutionInputs>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (inputs is null || !inputs.IsValid || inputs.RunId != expectedRunId || inputs.TrayId != expectedTrayId ||
            inputs.PlanRevision != expectedPlanRevision)
            throw new InvalidOperationException("CommittedExecutionInputsMismatch");
        inputs = inputs with { Plan = RecipeCatalogSnapshots.Freeze(inputs.Plan),
            Capabilities = RecipeCatalogSnapshots.Map(inputs.Capabilities) };
        plan = inputs.Plan;
        if (approval.RecipeId != plan.RecipeId || approval.RecipeVersion != plan.RecipeVersion ||
            approval.DefinitionDigest != plan.DefinitionDigest)
            throw new InvalidOperationException("BoundRecipeIdentityMismatch");
        if (motionConfiguration is null) throw new InvalidOperationException("ProductTargetResolutionMissing");
        var mediaWrites = writes.Where(w => w.RunId == expectedRunId && w.Kind == WriteKind.Media &&
                w.State == CommitState.Committed && w.Revision < handoff.CommittedRevision &&
                w.PayloadDigest == Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(w.PayloadJson))))
            .Select(w => (Write: w, Media: JsonSerializer.Deserialize<MediaRef>(w.PayloadJson,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)))).ToArray();
        var source = writes.Where(w => w.Kind == WriteKind.AlgorithmFact && w.State == CommitState.Committed &&
                handoff.EvidenceReferences.Contains($"write://{w.WriteId:D}", StringComparer.Ordinal))
            .Select(w => (Write: w, Fact: JsonSerializer.Deserialize<AlgorithmFactPayload>(w.PayloadJson,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))))
            .SingleOrDefault(x => x.Fact is { State: AlgorithmState.Success } fact && fact.RunId == expectedRunId &&
                handoff.ResultReferences.Contains($"algorithm-call://{fact.CallId:D}", StringComparer.Ordinal) &&
                mediaWrites.Any(m => m.Media is { } media && m.Write.Revision < x.Write.Revision &&
                    media.RunId == expectedRunId && media.CaptureId == fact.CaptureId &&
                    handoff.FMediaReferences.Contains($"media://{media.MediaId:D}", StringComparer.Ordinal)));
        if (source.Fact is null || !source.Fact.Origin.IsKnown || source.Fact.Origin.Source != handoff.Source ||
            source.Write.Revision >= handoff.CommittedRevision ||
            source.Write.PayloadDigest != Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source.Write.PayloadJson))))
            throw new InvalidOperationException("CommittedFSourceMissingOrMismatched");
        var observationFact = writes.Where(w => w.Kind == WriteKind.AlgorithmFact && w.State == CommitState.Committed &&
                w.Revision < handoff.CommittedRevision && w.WriteId != source.Write.WriteId &&
                handoff.EvidenceReferences.Contains($"write://{w.WriteId:D}", StringComparer.Ordinal))
            .Select(w => (Write: w, Fact: JsonSerializer.Deserialize<AlgorithmFactPayload>(w.PayloadJson,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))))
            .SingleOrDefault(x => x.Fact is { RunId: var run } fact && run == expectedRunId &&
                handoff.ResultReferences.Contains($"algorithm-call://{fact.CallId:D}", StringComparer.Ordinal));
        if (observationFact.Fact is not { State: AlgorithmState.Success } observationPayload ||
            observationFact.Write.PayloadDigest != Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(observationFact.Write.PayloadJson))))
            throw new InvalidOperationException("CommittedTrayObservationRequired");
        var observation = JsonSerializer.Deserialize<TrayObservation>(observationPayload.RawResultJson);
        if (observation is not { IsValid: true, Purpose: TrayObservationPurpose.InitialPreparation } ||
            observation.RunId != expectedRunId || observation.TrayId != expectedTrayId ||
            observation.CallId != observationPayload.CallId || observation.CaptureId != observationPayload.CaptureId ||
            observation.Source != observationPayload.Origin)
            throw new InvalidOperationException("CommittedTrayObservationMismatch");
        if (!mediaWrites.Any(m => m.Media is { } media && m.Write.Revision < observationFact.Write.Revision &&
                media.RunId == expectedRunId && media.CaptureId == observation.CaptureId &&
                handoff.ThreeDMediaReferences.Contains($"media://{media.MediaId:D}", StringComparer.Ordinal)))
            throw new InvalidOperationException("CommittedTrayObservationMediaRequired");
        var problem = RecipeExecutionCoordinator.ValidateDetectionPlan(plan);
        if (problem is not null) throw new InvalidOperationException(problem);
        var (targets, expected) = CoordinateResolver.Resolve(plan, handoff);
        var media = handoff.ThreeDMediaReferences.Concat(handoff.FMediaReferences)
            .Distinct(StringComparer.Ordinal).ToArray();
        var request = new DetectionRequest(expectedRunId, expectedTrayId, stationId, lineId,
            WholeTrayWorkflowStage.Detection, operationId, handoff.PlanRevision,
            connectionEpoch, deadline, media, inputs.CostProfile.Purpose, idempotencyKey,
            StageStartedAtUtc: stageStartedAtUtc ??
                deadline - Gaode.Application.Workflow.StageRetryPolicy.StageDuration,
            ComponentEvidenceReferences: handoff.EvidenceReferences,
            ExpectedObjects: expected,
            Plan: plan,
            MotionConfiguration: motionConfiguration,
            Targets: targets) { Inputs = inputs, SessionId = approval.Correlation.SessionId,
                SnapshotId = approval.Correlation.SnapshotId, ClockId = approval.Window.ClockId,
                InitialObservation = observation, InitialObservationWriteId = observationFact.Write.WriteId };
        if (!request.IsValid) throw new InvalidOperationException("DetectionRequestInvalid");
        if (RecipeExecutionCoordinator.ValidateTargets(request) is { } targetProblem)
            throw new InvalidOperationException(targetProblem);
        return request;
    }

}
