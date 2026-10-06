using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Application.Motion;
using Gaode.Domain.Station01;
using Gaode.Diagnostics;
using Gaode.Application.Timing;

namespace Gaode.Application.Workflow;

public enum WholeTrayWorkflowStatus
{
    ThreeStagesIncomplete,
    ReadyForRemoval,
    ManualRemovalAllowed,
    FinalUnloadCompleted,
    UnknownHeld,
    Failed
}

public sealed record WholeTrayWorkflowRequest(
    ThreeStageExecutionRequest ThreeStages,
    Guid CompletionId,
    string CompletionIdempotencyKey)
{
    public bool IsValid => ThreeStages.IsValid && CompletionId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(CompletionIdempotencyKey);
}

public sealed record ManualRemovalAllowanceRequest(
    WholeTrayCompletionReference WholeTray,
    Guid OperationId,
    long ConnectionEpoch,
    DateTimeOffset DeadlineUtc,
    string IdempotencyKey, string SnapshotId, Guid SessionId)
{
    public bool IsValid => WholeTray.IsValid && OperationId != Guid.Empty && ConnectionEpoch > 0 &&
        DeadlineUtc > DateTimeOffset.MinValue && !string.IsNullOrWhiteSpace(IdempotencyKey) &&
        !string.IsNullOrWhiteSpace(SnapshotId) && SessionId != Guid.Empty;
}

public sealed record WholeTrayWorkflowResult(
    WholeTrayWorkflowStatus Status,
    ThreeStageExecutionResult? ThreeStages,
    WholeTrayCompletionRecord? WholeTray,
    Guid? ManualRemovalAllowedEventId,
    FinalUnloadCompletion? FinalCompletion,
    string? ErrorCode)
{
    public bool IsCompleted => Status == WholeTrayWorkflowStatus.FinalUnloadCompleted;
}

/// <summary>
/// Host-side progression for the current first-station tray only. It
/// grants removal only after persisted whole-tray and matching unload completion facts.
/// The permission is a Host decision, not a fabricated device-unlock observation.
/// </summary>
public sealed class WholeTrayWorkflowOrchestrator(
    ThreeStageWorkflowExecutor threeStages,
    IStageEventStore events,
    IWholeTrayCompletionStore completions, ResourceLease motionLease,
    TimeProvider? clock = null, ComponentExecutionOrigin? hostOrigin = null)
{
    private readonly TimeProvider clock = clock ?? TimeProvider.System;

    public async Task<WholeTrayWorkflowResult> ExecuteThreeStagesAsync(WholeTrayWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.IsValid) throw new ArgumentException("整盘流程请求合同不完整", nameof(request));
        return await RuntimeDiagnostics.ObserveAsync(
            "WholeTrayStages", request.ThreeStages.Detection.RunId,
            new { request.CompletionId, request.ThreeStages.Detection.OperationId,
                request.ThreeStages.Detection.PlanRevision },
            () => ExecuteThreeStagesCoreAsync(request, cancellationToken),
            ResultFacts, r => r.Status != WholeTrayWorkflowStatus.ReadyForRemoval);
    }

    private async Task<WholeTrayWorkflowResult> ExecuteThreeStagesCoreAsync(WholeTrayWorkflowRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.IsValid) throw new ArgumentException("整盘流程请求合同不完整", nameof(request));

        var result = await threeStages.ExecuteAsync(request.ThreeStages, cancellationToken);
        if (!result.IsCompleted)
            return new(WholeTrayWorkflowStatus.ThreeStagesIncomplete, result, null, null, null,
                result.ErrorCode);

        var detection = request.ThreeStages.Detection;
        if (string.Equals(detection.Purpose, "Production", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("ProductionStageEvidenceRequired");
        var matrix = await BuildReadyForRemovalMatrixAsync(request, cancellationToken);
        if (!matrix.IsComplete)
            throw new InvalidOperationException("ComponentEvidenceMatrixIncomplete:" + string.Join(',', matrix.BlockedComponents));
        var completion = await completions.CreateAsync(new WholeTrayCompletionCreateRequest(
            request.CompletionId, detection.RunId, detection.TrayId, detection.StationId,
            detection.LineId, detection.PlanRevision, matrix,
            clock.GetUtcNow(), request.CompletionIdempotencyKey) {
                RecipePlanRevision = detection.PlanRevision,
                InspectionCompleted = detection.InitialObservation is { } initial &&
                    initial.Slots.All(s => s.Presence != TrayPresence.Present || s.Pose == TrayPose.Normal) &&
                    !(await events.ReadAsync(detection.RunId, detection.TrayId, WholeTrayWorkflowStage.Detection, cancellationToken))
                        .Any(e => e.ErrorCode == "PoseInspectionTerminated") }, cancellationToken);
        return new(WholeTrayWorkflowStatus.ReadyForRemoval, result, completion, null, null, null);
    }

    public async Task<WholeTrayWorkflowResult> ExecuteEarlyEndAsync(PublicUnloadRequest request,
        TrayEndReason reason, string endBasisReference, IReadOnlyList<ComponentEvidence> preparationEvidence,
        string? recipePlanRevision, CancellationToken token)
    {
        if (reason == TrayEndReason.NormalCompletion) throw new ArgumentException("EarlyEndReasonRequired");
        var result = await threeStages.ExecutePublicUnloadAsync(request, token);
        if (!result.IsCompleted) return new(WholeTrayWorkflowStatus.ThreeStagesIncomplete, result, null, null, null, result.ErrorCode);
        var facts = await events.ReadAsync(request.RunId, request.TrayId, WholeTrayWorkflowStage.UnloadPreparation, token);
        var unload = facts.Last(f => f.EventType == StageEventType.Completed);
        using var payload = JsonDocument.Parse(unload.PayloadJson);
        var origin = payload.RootElement.GetProperty("executionOrigin").Deserialize<ExecutionOrigin>(new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("UnloadOriginMissing");
        var at = clock.GetUtcNow();
        var plc = new ComponentEvidence(ComponentKind.Plc, ComponentEvidenceState.Verified,
            origin.Provider switch { DeviceProvider.Real => ComponentEvidenceSource.Real, DeviceProvider.Virtual => ComponentEvidenceSource.Virtual,
                _ => ComponentEvidenceSource.Simulated }, origin.Quality.ToString(), origin.ComponentVersion,
            [$"stage-event://{unload.EventId:D}"], at, unload.PayloadDigest);
        var host = hostOrigin ?? ComponentExecutionOrigin.Unknown;
        var matrix = ComponentEvidenceMatrix.Create(Guid.NewGuid(), request.RunId, request.TrayId, request.ExecutionRevision,
            EvidenceMilestone.ReadyForRemoval, preparationEvidence.Where(e => e.Component is not (ComponentKind.Plc or ComponentKind.Host or ComponentKind.ManualActor))
                .Concat([new ComponentEvidence(ComponentKind.Host, ComponentEvidenceState.Verified, host.Source, host.Quality,
                    host.VersionRef, [$"public-snapshot://{request.SnapshotId}"], at, request.SnapshotId), plc,
                    ComponentEvidence.NotYetRequired(ComponentKind.ManualActor)]));
        var completion = await completions.CreateAsync(new(Guid.NewGuid(), request.RunId, request.TrayId,
            request.StationId, request.LineId, request.ExecutionRevision, matrix, at, request.IdempotencyKey + ":tray-end") {
                EndReason = reason, InspectionCompleted = false, RecipePlanRevision = recipePlanRevision,
                EndBasisReference = endBasisReference }, token);
        return new(WholeTrayWorkflowStatus.ReadyForRemoval, result, completion, null, null, null);
    }

    public async Task<WholeTrayWorkflowResult> AllowManualRemovalAsync(ManualRemovalAllowanceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.IsValid) throw new ArgumentException("人工取盘允许请求合同不完整", nameof(request));
        return await RuntimeDiagnostics.ObserveAsync(
            "ManualRemovalAllowance", request.WholeTray.RunId, new { request.OperationId,
                request.ConnectionEpoch, request.DeadlineUtc, request.WholeTray.CompletionId },
            () => AllowManualRemovalCoreAsync(request, cancellationToken),
            ResultFacts, r => r.Status != WholeTrayWorkflowStatus.ManualRemovalAllowed);
    }

    private async Task<WholeTrayWorkflowResult> AllowManualRemovalCoreAsync(ManualRemovalAllowanceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.IsValid) throw new ArgumentException("ManualRemovalAllowanceRequestInvalid", nameof(request));
        var remaining = request.DeadlineUtc - clock.GetUtcNow();
        if (remaining <= TimeSpan.Zero) throw new TimeoutException("ManualRemovalAllowanceDeadlineExpired");
        using var deadline = new CancellationTokenSource(remaining, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        var token = linked.Token;
        var persisted = await completions.GetAsync(request.WholeTray, token);
        if (persisted is null || persisted.Reference != request.WholeTray || !persisted.IsValid)
            throw new InvalidOperationException("WholeTrayCompletionReferenceNotPersisted");
        var unloadFacts = await events.ReadAsync(request.WholeTray.RunId, request.WholeTray.TrayId,
            WholeTrayWorkflowStage.UnloadPreparation, token);
        var unload = unloadFacts.LastOrDefault();
        if (unload is null || unload.EventType != StageEventType.Completed ||
            unload.EventId != request.WholeTray.UnloadPreparationCompletedEventId ||
            unload.PlanRevision != request.WholeTray.PlanRevision || unload.ConnectionEpoch != request.ConnectionEpoch ||
            unload.StationId != request.WholeTray.StationId.ToString() || unload.LineId != request.WholeTray.LineId.ToString())
            throw new InvalidOperationException("CurrentUnloadCompletionRequired");
        token.ThrowIfCancellationRequested();
        if (clock.GetUtcNow() >= request.DeadlineUtc) throw new TimeoutException("ManualRemovalAllowanceDeadlineExpired");
        var payload = JsonSerializer.Serialize(new { kind = "ManualRemovalAllowed",
            request.WholeTray.CompletionId, unload.EventId, request.WholeTray.PlanRevision,
            permissionSource = "CommittedWholeTrayAndUnload", deviceUnlockClaimed = false });
        var allowed = await AppendAsync(request.WholeTray, request.OperationId, request.ConnectionEpoch,
            StageEventType.ManualRemovalAllowed, null, request.IdempotencyKey + ":allowed", payload, token);
        if (!allowed.IsCommitted) throw new InvalidOperationException(allowed.ErrorCode ?? "ManualRemovalAllowanceNotCommitted");
        token.ThrowIfCancellationRequested();
        return new(WholeTrayWorkflowStatus.ManualRemovalAllowed, null, persisted, allowed.Event.EventId, null, null);
    }

    public async Task<WholeTrayWorkflowResult> ConfirmManualRemovalAsync(
        ManualTrayRemovalConfirmationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.IsValid) throw new ArgumentException("人工取盘确认合同不完整", nameof(request));
        return await RuntimeDiagnostics.ObserveAsync("ManualRemovalConfirmation", request.WholeTray.RunId,
            new { request.WholeTray.CompletionId, request.ManualRemovalAllowedEventId },
            () => ConfirmManualRemovalCoreAsync(request, cancellationToken),
            ResultFacts, r => !r.IsCompleted);
    }

    private static object ResultFacts(WholeTrayWorkflowResult result) => new
    {
        status = result.Status.ToString(), result.ErrorCode,
        threeStageStatus = result.ThreeStages?.Status.ToString(),
        currentStage = result.ThreeStages?.CurrentStage.ToString(),
        result.ManualRemovalAllowedEventId, completionId = result.WholeTray?.Reference.CompletionId,
        finalCompletionId = result.FinalCompletion?.FinalCompletionId
    };

    private async Task<WholeTrayWorkflowResult> ConfirmManualRemovalCoreAsync(
        ManualTrayRemovalConfirmationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.IsValid) throw new ArgumentException("人工取盘确认合同不完整", nameof(request));
        var completion = await completions.GetAsync(request.WholeTray, cancellationToken);
        if (completion is null || completion.Reference != request.WholeTray)
            throw new InvalidOperationException("WholeTrayCompletionReferenceNotPersisted");

        if (!motionLease.IsOwner(request.WholeTray.RunId) || motionLease.CurrentAction is not null || motionLease.Unknown)
            throw new InvalidOperationException("ManualRemovalMotionOwnershipUnconfirmed");
        var final = await completions.ConfirmManualRemovalAsync(request, cancellationToken);
        if (!final.IsValid || final.WholeTray != request.WholeTray || final.ManualRemovalAllowedEventId != request.ManualRemovalAllowedEventId)
            throw new InvalidOperationException("ManualRemovalFinalCommitMismatch");
        motionLease.ReleaseOnlyAfterVerifiedPhysicalClear(request.WholeTray.RunId);
        return new(WholeTrayWorkflowStatus.FinalUnloadCompleted, null, completion,
            request.ManualRemovalAllowedEventId, final, null);
    }

    private Task<StageEventAppendResult> AppendAsync(WholeTrayCompletionReference reference,
        Guid operationId, long epoch, StageEventType type, string? error, string key, string payload,
        CancellationToken cancellationToken) => events.AppendAsync(new StageEventAppendRequest(Guid.NewGuid(),
            reference.RunId, reference.TrayId, reference.StationId.ToString(), reference.LineId.ToString(),
            WholeTrayWorkflowStage.ManualRemovalAdmission, operationId, 1, epoch, type, clock.GetUtcNow(),
            ResultSource.HostDerived, ResultQuality.Derived, error, Digest(payload), payload, key,
            reference.PlanRevision), cancellationToken);

    private async Task<ComponentEvidenceMatrix> BuildReadyForRemovalMatrixAsync(
        WholeTrayWorkflowRequest request, CancellationToken cancellationToken)
    {
        var detectionRequest = request.ThreeStages.Detection;
        var detectionEvents = await events.ReadAsync(detectionRequest.RunId, detectionRequest.TrayId,
            WholeTrayWorkflowStage.Detection, cancellationToken);
        var unloadEvents = await events.ReadAsync(detectionRequest.RunId, detectionRequest.TrayId,
            WholeTrayWorkflowStage.UnloadPreparation, cancellationToken);
        var detection = detectionEvents.LastOrDefault(x => x.EventType == StageEventType.Completed)
            ?? throw new InvalidOperationException("DetectionCompletedEvidenceMissing");
        var plc = unloadEvents.LastOrDefault(x => x.EventType == StageEventType.Completed)
            ?? throw new InvalidOperationException("UnloadPreparationCompletedEvidenceMissing");
        // These are current committed semantic facts, never a raw protocol decoder or
        // a reconstruction of absent historical provenance from the Host's mode.
        using var plcPayload = JsonDocument.Parse(plc.PayloadJson);
        var plcOrigin = plcPayload.RootElement.TryGetProperty("schemaVersion", out var schema) && schema.GetString() == "stage-action/1" &&
            plcPayload.RootElement.TryGetProperty("executionOrigin", out var originJson)
            ? originJson.Deserialize<ExecutionOrigin>(new JsonSerializerOptions(JsonSerializerDefaults.Web)) : null;
        using var detectionPayload = JsonDocument.Parse(detection.PayloadJson);
        var algorithmOrigin = detectionPayload.RootElement.TryGetProperty("AlgorithmOrigin", out var algorithmJson)
            ? algorithmJson.Deserialize<ComponentExecutionOrigin>() : null;
        var captures = detectionPayload.RootElement.TryGetProperty("CaptureFacts", out var captureJson)
            ? captureJson.Deserialize<CorrelatedCaptureFact[]>() ?? [] : [];
        var currentCaptures = captures.Length > 0 && captures.All(c => c.RunId == detectionRequest.RunId &&
            c.CaptureId != Guid.Empty && c.OperationId != Guid.Empty && c.ConnectionEpoch > 0 &&
            !string.IsNullOrWhiteSpace(c.RequestedSettingsDigest));
        ComponentExecutionOrigin? CapturedOrigin(Func<CorrelatedCaptureFact, ComponentExecutionOrigin> select)
        {
            if (!currentCaptures) return null;
            var origins = captures.Select(select).Distinct().ToArray();
            return origins.Length == 1 && origins[0].IsKnown ? origins[0] : null;
        }
        var at = clock.GetUtcNow();
        ComponentEvidence Evidence(ComponentKind kind, ComponentEvidenceSource source,
            string quality, string version, string reference, string digest) =>
            new(kind, ComponentEvidenceState.Verified, source, quality, version,
                [reference], at, digest);
        ComponentEvidence Producer(ComponentKind kind, ComponentExecutionOrigin? origin, string reference, string digest) =>
            origin is { IsKnown: true }
                ? Evidence(kind, origin.Source!.Value, origin.Quality!, origin.VersionRef!, reference, digest)
                : new(kind, ComponentEvidenceState.Unknown, origin?.Source, origin?.Quality,
                    origin?.VersionRef, [reference], at, digest);
        var evidence = new[]
        {
            Producer(ComponentKind.Host, hostOrigin, $"handoff://{detectionRequest.IdempotencyKey}",
                detectionRequest.PlanRevision),
            Producer(ComponentKind.Plc, plcOrigin is null || plcOrigin.Provider == DeviceProvider.Unavailable ? null :
                new(plcOrigin.Provider switch { DeviceProvider.Real => ComponentEvidenceSource.Real,
                    DeviceProvider.Virtual => ComponentEvidenceSource.Virtual, _ => ComponentEvidenceSource.Simulated },
                    plcOrigin.ComponentVersion, plcOrigin.Quality.ToString()), $"stage-event://{plc.EventId:D}", plc.PayloadDigest),
            Producer(ComponentKind.Camera, CapturedOrigin(c => c.CameraOrigin),
                $"stage-event://{detection.EventId:D}", detection.PayloadDigest),
            Producer(ComponentKind.Light, CapturedOrigin(c => c.LightOrigin),
                $"stage-event://{detection.EventId:D}", detection.PayloadDigest),
            Producer(ComponentKind.Algorithm, algorithmOrigin,
                $"stage-event://{detection.EventId:D}", detection.PayloadDigest),
            ComponentEvidence.NotYetRequired(ComponentKind.ManualActor)
        };
        return ComponentEvidenceMatrix.Create(Guid.NewGuid(), detectionRequest.RunId,
            detectionRequest.TrayId, detectionRequest.PlanRevision,
            EvidenceMilestone.ReadyForRemoval, evidence);
    }

    private static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
