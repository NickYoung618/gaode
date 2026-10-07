using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Domain.Station01;

namespace Gaode.Application.Workflow;

public sealed class RecipeSortingMapper(IStageEventStore eventStore, TimeProvider? clock = null)
{
    private readonly TimeProvider clock = clock ?? TimeProvider.System;

    public async Task<SortingMappingResult> MapAsync(
        DetectionPortResult detection,
        RecipeRunPlan plan,
        string expectedPlanRevision,
        Guid mappingOperationId,
        long connectionEpoch,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(detection);
        ArgumentNullException.ThrowIfNull(plan);
        if (string.IsNullOrWhiteSpace(expectedPlanRevision) || mappingOperationId == Guid.Empty || connectionEpoch <= 0)
            throw new ArgumentException("映射 operationId 和 connectionEpoch 必须有效");
        if (!detection.IsValid || detection.Kind != DetectionResultKind.Completed)
            throw new InvalidOperationException("DetectionResultNotCompleted");

        var request = detection.Request;
        var allExpected = plan.Steps.Where(x => x.Kind == RecipeStepKind.SortUnit && (request.Scope is null || request.Scope.Includes(x))).ToArray();
        if (detection.LastObservation is not { IsValid: true } observation || observation.RunId != request.RunId ||
            observation.TrayId != request.TrayId || allExpected.Any(x => x.PhysicalSlotIndex is not { } index ||
                !detection.SlotParticipation.TryGetValue(index, out var state) || state.State == SlotParticipationState.Unknown))
            throw new InvalidOperationException("SortingObservationCoverageUnknown");
        var expected = allExpected.Where(x => detection.SlotParticipation[x.PhysicalSlotIndex!.Value].State == SlotParticipationState.Participating)
            .OrderBy(x => x.Sequence).ToArray();
        var poseExpected = allExpected.Where(x => detection.SlotParticipation[x.PhysicalSlotIndex!.Value].State == SlotParticipationState.PoseExcluded)
            .OrderBy(x => x.Sequence).ToArray();
        var objectGroups = detection.Objects.GroupBy(x => x.ObjectId, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);
        var expectedGroups = expected.GroupBy(ExpectedObjectId, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.Ordinal);

        var missing = new HashSet<string>(StringComparer.Ordinal);
        var duplicate = new HashSet<string>(StringComparer.Ordinal);
        var ambiguous = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in objectGroups.Where(x => x.Value.Length > 1)) duplicate.Add(group.Key);
        foreach (var group in expectedGroups.Where(x => x.Value.Length > 1)) ambiguous.Add(group.Key);

        foreach (var step in expected)
        {
            var objectId = ExpectedObjectId(step);
            if (!objectGroups.TryGetValue(objectId, out var candidates))
            {
                missing.Add(objectId);
                continue;
            }
            if (candidates.Length != 1) continue;
            var objectResult = candidates[0];
            if (objectResult.SpecialHandlingCompleted) ambiguous.Add(objectId + ":LegacySpecialExitNotAuthorized");
            var classificationMatches = string.IsNullOrWhiteSpace(step.Material) ||
                string.Equals(step.Material, objectResult.Classification, StringComparison.Ordinal);
            var expectedPosition = request.ExpectedObjects?.SingleOrDefault(x => x.ObjectId == objectId)?.Position;
            if (expectedPosition is null || expectedPosition != objectResult.Position || !classificationMatches)
                ambiguous.Add(objectId);
        }

        foreach (var step in poseExpected)
        {
            var handling = detection.PosePending.SingleOrDefault(p => p.ObjectId == ExpectedObjectId(step) &&
                p.PhysicalSlotIndex == step.PhysicalSlotIndex && p.ObservationId != Guid.Empty &&
                !string.IsNullOrWhiteSpace(p.ObservationReference));
            if (handling is null) missing.Add(ExpectedObjectId(step) + ":PoseHandlingEvidence");
        }
        foreach (var objectId in objectGroups.Keys.Except(expectedGroups.Keys.Concat(poseExpected.Select(ExpectedObjectId)), StringComparer.Ordinal))
            ambiguous.Add(objectId);
        if (!string.Equals(expectedPlanRevision, request.PlanRevision, StringComparison.Ordinal))
            ambiguous.Add("planRevision:" + request.PlanRevision);

        if (missing.Count > 0 || duplicate.Count > 0 || ambiguous.Count > 0)
        {
            var failure = new SortingMappingFailure(request.RunId, request.TrayId, expectedPlanRevision,
                missing.Order().ToArray(), duplicate.Order().ToArray(), ambiguous.Order().ToArray(),
                BuildReason(missing, duplicate, ambiguous));
            await PersistMappingFailedAsync(detection, mappingOperationId, connectionEpoch, failure,
                cancellationToken);
            return new(false, Array.Empty<SortingActionPlan>(), failure, mappingOperationId, connectionEpoch);
        }

        var actions = expected.Select(step =>
        {
            var objectId = ExpectedObjectId(step);
            var result = objectGroups[objectId][0];
            var actionStep = plan.InspectionKind == RecipeInspectionKind.SpecialRotation && result.Disposition == "OK"
                ? plan.Steps.Single(s => s.Kind == RecipeStepKind.ReturnUnit && s.UnitId == step.UnitId && s.SlotId == step.SlotId) : step;
            return new SortingActionPlan(request.RunId, request.TrayId, expectedPlanRevision, actionStep.Sequence,
                objectId, plan.InspectionKind == RecipeInspectionKind.SpecialRotation ?
                    plan.RotationWorkstation?.Pick.Point ?? throw new InvalidOperationException("RotationWorkstationMissing") : result.Position, result.Classification,
                DeriveOperationId(mappingOperationId, objectId, step.Sequence), connectionEpoch,
                step.SlotId ?? "", step.MemberId, result.Disposition)
            { PhysicalSlotIndex = step.PhysicalSlotIndex, ReturnsToOrigin = plan.InspectionKind == RecipeInspectionKind.SpecialRotation && result.Disposition == "OK" };
        }).Where(x => x.Disposition != "OK" || x.ReturnsToOrigin).Concat(poseExpected.Select(step => {
            var objectId = ExpectedObjectId(step);
            var basis = detection.PosePending.Single(p => p.ObjectId == objectId && p.PhysicalSlotIndex == step.PhysicalSlotIndex);
            var source = plan.ExecutionPositions[step.SlotId!].ForObject(plan.UnitKind, step.Material).Source
                ?? throw new InvalidOperationException("PosePendingSourceMissing");
            return new SortingActionPlan(request.RunId, request.TrayId, expectedPlanRevision, step.Sequence,
                objectId, source.Point, step.Material ?? plan.Model, DeriveOperationId(mappingOperationId, objectId, step.Sequence),
                connectionEpoch, step.SlotId!, step.MemberId, "Pending") { PhysicalSlotIndex = step.PhysicalSlotIndex,
                    IsPosePending = true, ObservationReference = basis.ObservationReference, DetectionState = basis.DetectionState };
        })).ToArray();
        return new(true, actions, null, mappingOperationId, connectionEpoch);
    }

    private async Task PersistMappingFailedAsync(DetectionPortResult detection, Guid operationId,
        long connectionEpoch, SortingMappingFailure failure, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(failure);
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        var request = detection.Request;
        var append = new StageEventAppendRequest(
            Guid.NewGuid(), request.RunId, request.TrayId, request.StationId.ToString(), request.LineId.ToString(),
            WholeTrayWorkflowStage.Detection, operationId, 1, connectionEpoch, StageEventType.MappingFailed,
            clock.GetUtcNow(), detection.Source, detection.Quality, "MappingFailed", digest, payload,
            request.IdempotencyKey + ":mapping", request.PlanRevision,
            request.StageStartedAtUtc, request.DeadlineUtc);
        var result = await eventStore.AppendAsync(append, cancellationToken);
        if (result.State == StageEventCommitState.Conflict)
            throw new InvalidOperationException("MappingIdempotencyConflict");
    }

    private static string ExpectedObjectId(RecipeStep step) => step.MemberId ?? step.UnitId;

    private static string BuildReason(ICollection<string> missing, ICollection<string> duplicate,
        ICollection<string> ambiguous) => string.Join(';',
        new[]
        {
            missing.Count == 0 ? null : "Missing:" + string.Join(',', missing.Order()),
            duplicate.Count == 0 ? null : "Duplicate:" + string.Join(',', duplicate.Order()),
            ambiguous.Count == 0 ? null : "Ambiguous:" + string.Join(',', ambiguous.Order())
        }.Where(x => x is not null));

    private static Guid DeriveOperationId(Guid mappingOperationId, string objectId, int sequence)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{mappingOperationId:N}:{sequence}:{objectId}"));
        return new Guid(bytes[..16]);
    }
}
