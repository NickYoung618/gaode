using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Diagnostics;
using Gaode.Application.Timing;

namespace Gaode.Application.Workflow;

public enum ThreeStageExecutionStatus
{
    StagesCompleted,
    Failed,
    TimedOut,
    Disconnected,
    PausedForManualReview,
    UnknownHeld,
    UnitCompleted,
    EarlyEndRequested
}

public sealed record PublicUnloadRequest(Guid RunId, Guid TrayId, Guid StationId, Guid LineId,
    Guid SessionId, long ConnectionEpoch, string SnapshotId, string ExecutionRevision,
    Guid OperationId, FixedPoint Target, double PositionTolerance, string Purpose,
    ActionWindow Window, string IdempotencyKey);

public sealed record ThreeStageExecutionRequest(
    DetectionRequest Detection,
    RecipeRunPlan Plan,
    Guid MappingOperationId,
    Guid UnloadOperationId,
    FixedPoint? UnloadTarget = null,
    double PositionTolerance = 0,
    string? TargetPurpose = null,
    string? ConfigSnapshotId = null,
    RecipeExecutionDeadlines? Deadlines = null)
{
    public bool IsValid => Detection.IsValid && Detection.Stage == WholeTrayWorkflowStage.Detection &&
        Plan is not null &&
        MappingOperationId != Guid.Empty && UnloadOperationId != Guid.Empty;
}

public sealed record PlcActionAssociation(
    WholeTrayWorkflowStage WorkflowStage,
    PlcWorkflowStage PlcStage,
    Guid OperationId,
    long ConnectionEpoch,
    bool DeviceHeld,
    bool AutomaticRetryAllowed);

public sealed record ThreeStageExecutionResult(
    ThreeStageExecutionStatus Status,
    WholeTrayWorkflowStage CurrentStage,
    string? ErrorCode,
    IReadOnlyList<PlcActionAssociation> PlcActions,
    StageProjection? Projection)
{
    public bool IsCompleted => Status == ThreeStageExecutionStatus.StagesCompleted;
    internal DetectionPortResult? ScopedDetection { get; init; }
    public string? EndBasisReference { get; init; }
}

public static class T050RetryPolicy
{
    public const int CommunicationRetries = 3;
    public const int AlgorithmTimeoutRetries = 2;
    public static readonly TimeSpan StageTimeout = TimeSpan.FromSeconds(120);
    public static readonly IReadOnlyList<TimeSpan> CommunicationBackoff =
        [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)];
    public static readonly IReadOnlyList<TimeSpan> AlgorithmBackoff =
        [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5)];
}

public interface IWorkflowDelay
{
    Task DelayAsync(TimeSpan delay, TimeProvider clock, CancellationToken cancellationToken);
}

public sealed class SystemWorkflowDelay : IWorkflowDelay
{
    public Task DelayAsync(TimeSpan delay, TimeProvider clock, CancellationToken cancellationToken) =>
        Task.Delay(delay, clock, cancellationToken);
}

public sealed class ThreeStageWorkflowExecutor(
    IDetectionPort detectionPort,
    IPlcStageActionPort plcPort,
    RecipeSortingMapper mapper,
    IStageEventStore eventStore,
    SortingTargetAllocator sortingAllocator,
    TimeProvider? clock = null,
    IWorkflowDelay? delay = null, Gaode.Application.Station01.NormalPauseBoundary? pause = null,
    RotationExecutionConfiguration? rotationConfiguration = null)
{
    private readonly TimeProvider clock = clock ?? TimeProvider.System;
    private readonly IWorkflowDelay delay = delay ?? new SystemWorkflowDelay();

    public async Task<ThreeStageExecutionResult> ExecuteAsync(
        ThreeStageExecutionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.IsValid) throw new ArgumentException("T050 三阶段执行请求合同不完整", nameof(request));
        return await RuntimeDiagnostics.ObserveAsync("DetectionSortingUnload", request.Detection.RunId,
            new { request.Detection.OperationId, request.MappingOperationId, request.UnloadOperationId,
                request.Detection.ConnectionEpoch, request.Detection.DeadlineUtc,
                request.Detection.PlanRevision, request.UnloadTarget, request.TargetPurpose,
                request.ConfigSnapshotId },
            () => ExecuteCoreAsync(request, cancellationToken),
            r => new { status = r.Status.ToString(), currentStage = r.CurrentStage.ToString(),
                r.ErrorCode, r.PlcActions }, r => !r.IsCompleted);
    }

    private async Task<ThreeStageExecutionResult> ExecuteCoreAsync(
        ThreeStageExecutionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.IsValid) throw new ArgumentException("T050 三阶段执行请求合同不完整", nameof(request));

        if(request.Plan.InspectionKind==RecipeInspectionKind.SpecialRotation && request.Plan.Steps.Any(s =>
                (request.Detection.Scope is null || request.Detection.Scope.Includes(s)) && request.Detection.InitialObservation?.Slots.Any(o =>
                    o.PhysicalSlotIndex == s.PhysicalSlotIndex && o.Presence == TrayPresence.Present && o.Pose == TrayPose.Normal) == true) &&
            (rotationConfiguration?.Basis is not { } mechanical || mechanical.Purpose!=request.Detection.Purpose ||
             string.IsNullOrWhiteSpace(mechanical.SourceReference) || !double.IsFinite(mechanical.AngleToleranceDeg) || mechanical.AngleToleranceDeg<0))
        {
            await AppendAsync(request,WholeTrayWorkflowStage.Detection,request.Detection.OperationId,1,
                StageEventType.ManualReviewRequested,"RotationMechanicalBasisMissing","rotation:mechanical-basis-missing",
                ResultSource.HostDerived,ResultQuality.Derived,cancellationToken,
                JsonSerializer.Serialize(new{kind="RotationMechanicalBasisMissing",noDeviceDispatch=true}));
            RuntimeDiagnostics.Record("Rotation","Blocked",request.Detection.RunId,new{request.Detection.OperationId,
                reason="RotationMechanicalBasisMissing",noDeviceDispatch=true},warning:true);
            return new(ThreeStageExecutionStatus.PausedForManualReview,WholeTrayWorkflowStage.Detection,
                "RotationMechanicalBasisMissing",[],await ProjectionAsync(request,WholeTrayWorkflowStage.Detection,cancellationToken));
        }
        if (request.Plan.InspectionKind == RecipeInspectionKind.SpecialRotation && request.Detection.Scope is null)
            return await ExecuteSpecialTrayAsync(request,cancellationToken);
        var priorAssociations = new List<PlcActionAssociation>();
        if (request.Detection.Scope is { } scope)
        {
            var loading = request.Plan.Steps.Single(s => scope.Includes(s) && s.Kind == RecipeStepKind.TransferToRotation);
            var observed = request.Detection.InitialObservation?.Slots.SingleOrDefault(s => s.PhysicalSlotIndex == loading.PhysicalSlotIndex);
            if (observed is {Presence:TrayPresence.Present,Pose:TrayPose.Normal})
            {
                var operation = Guid.NewGuid();
                var source = request.Plan.ExecutionPositions[loading.SlotId!].ForObject(request.Plan.UnitKind,loading.Material).Source
                    ?? throw new InvalidOperationException("RotationLoadingSourceMissing");
                var target = request.Plan.RotationWorkstation?.Place ?? throw new InvalidOperationException("RotationWorkstationMissing");
                var reserved = await sortingAllocator.ReserveRotationLoadingAsync(request,loading,operation,source.Point,target.Point,cancellationToken);
                var action = new PlcStageActionRequest(new(request.Detection.RunId,operation,Guid.NewGuid(),1,
                    request.Detection.SessionId,request.Detection.ConnectionEpoch,request.Detection.SnapshotId,
                    request.Detection.PlanRevision,request.Detection.TrayId,loading.MemberId??loading.UnitId,
                    PhysicalSlotIndex:loading.PhysicalSlotIndex),request.Detection.StationId,request.Detection.LineId,
                    PlcWorkflowStage.TransferToRotation,SortingTargetAllocator.AssignmentDigest(reserved),
                    ActionWindows.FromUtc(clock,request.Detection.StageStartedAtUtc??clock.GetUtcNow(),request.Detection.DeadlineUtc,request.Detection.ClockId),
                    request.Detection.IdempotencyKey+":rotation-loading",PhysicalSlotIndex:loading.PhysicalSlotIndex,
                    PositionTolerance:request.PositionTolerance,TargetPurpose:request.TargetPurpose,
                    SortingSource:source.Point,SortingTarget:target.Point,ReservationReference:reserved.ReservationReference)
                { Scope=request.Detection.Scope, TransferPurpose=TransferPurpose.RotationLoading,RequestedGripperId=request.Plan.RotationLoadingGripperId };
                var outcome=await ExecutePlcActionAsync(action,WholeTrayWorkflowStage.Sorting,request.Detection.DeadlineUtc,cancellationToken);
                priorAssociations.Add(outcome.Association);
                if(outcome.Terminal is { } terminal)return new(terminal,WholeTrayWorkflowStage.Detection,outcome.ErrorCode,
                    priorAssociations,await ProjectionAsync(request,WholeTrayWorkflowStage.Detection,cancellationToken));
            }
        }
        var detection = await ExecuteDetectionAsync(request, cancellationToken);
        if (detection.Terminal is not null) return detection.Terminal;

        var mapped = await mapper.MapAsync(detection.Result!, request.Plan,
            request.Detection.PlanRevision,
            request.MappingOperationId, request.Detection.ConnectionEpoch, cancellationToken);
        if (!mapped.IsComplete)
        {
            return new(ThreeStageExecutionStatus.PausedForManualReview,
                WholeTrayWorkflowStage.Detection, mapped.Failure?.Reason ?? "MappingFailed",
                [], await ProjectionAsync(request, WholeTrayWorkflowStage.Detection, cancellationToken));
        }

        // Freeze all required same-tray destinations before the first post-detection motion.
        var sortingTargets = mapped.Actions.ToDictionary(x => x.OperationId,
            x => ResolveSortingTarget(request.Plan, x, request.Detection.MotionConfiguration));
        if (mapped.Actions.Any(x => sortingTargets[x.OperationId] is null ||
            x.PhysicalSlotIndex is not > 0))
        {
            RuntimeDiagnostics.Record("Sorting", "Blocked", request.Detection.RunId,
                new { request.Detection.OperationId, reason = "SortingTargetUnconfigured",
                    count = mapped.Actions.Count, disposition = "NoPLCDispatch_NoFinal" }, warning: true);
            await AppendAsync(request, WholeTrayWorkflowStage.Sorting,
                request.MappingOperationId, 1, StageEventType.ManualReviewRequested,
                "SortingTargetUnconfigured", "sorting:target-unconfigured",
                detection.Result!.Source, detection.Result.Quality, cancellationToken,
                JsonSerializer.Serialize(new { reason = "SortingTargetUnconfigured",
                    objects = mapped.Actions.Select(action => new { action.ObjectId, action.Disposition,
                        action.SlotId }), noPlcDispatch = true }));
            return new(ThreeStageExecutionStatus.PausedForManualReview,
                WholeTrayWorkflowStage.Sorting, "SortingTargetUnconfigured", [],
                await ProjectionAsync(request, WholeTrayWorkflowStage.Detection, cancellationToken));
        }

        // Resolve every member before any sorting motion; never ask the transport to interpret a recipe.
        Dictionary<Guid, int> sortingGrippers;
        try { sortingGrippers = mapped.Actions.ToDictionary(a => a.OperationId,
            a => RecipeSortingGripperSelection.Resolve(request.Plan, a)); }
        catch (InvalidOperationException error)
        {
            RuntimeDiagnostics.Record("Sorting", "Blocked", request.Detection.RunId,
                new { reason = error.Message, noPlcDispatch = true }, warning: true);
            await AppendAsync(request, WholeTrayWorkflowStage.Sorting, request.MappingOperationId, 1,
                StageEventType.ManualReviewRequested, error.Message, "sorting:gripper-unconfigured",
                detection.Result!.Source, detection.Result.Quality, cancellationToken);
            return new(ThreeStageExecutionStatus.PausedForManualReview, WholeTrayWorkflowStage.Sorting,
                error.Message, [], await ProjectionAsync(request, WholeTrayWorkflowStage.Sorting, cancellationToken));
        }

        var reservation = await sortingAllocator.ReserveAsync(request, detection.Result!, mapped.Actions,
            sortingTargets, cancellationToken);
        if (!reservation.Reserved)
        {
            await AppendAsync(request, WholeTrayWorkflowStage.Sorting, request.MappingOperationId, 1,
                StageEventType.ManualReviewRequested, reservation.ErrorCode, "sorting:reservation-blocked",
                detection.Result!.Source, detection.Result.Quality, cancellationToken,
                JsonSerializer.Serialize(new { reason = reservation.ErrorCode, noPlcDispatch = true }));
            RuntimeDiagnostics.Record("Sorting", "Blocked", request.Detection.RunId,
                new { reason = reservation.ErrorCode, disposition = "NoPLCDispatch_NoFinal" }, warning: true);
            return new(ThreeStageExecutionStatus.PausedForManualReview, WholeTrayWorkflowStage.Sorting,
                reservation.ErrorCode, [], await ProjectionAsync(request, WholeTrayWorkflowStage.Sorting, cancellationToken));
        }
        var associations = priorAssociations;
        var sessionId = request.Detection.SessionId;
        if (sessionId == Guid.Empty) throw new InvalidOperationException("StageSessionIdentityMissing");
        var sortingDeadline = request.Deadlines?.SortingDeadlineUtc ??
            clock.GetUtcNow() + T050RetryPolicy.StageTimeout;
        if (mapped.Actions.Count == 0)
        {
            await AppendAsync(request, WholeTrayWorkflowStage.Sorting, request.MappingOperationId,
                1, request.Detection.Scope is null ? StageEventType.Completed : StageEventType.Executing, null, "sorting:no-action-required",
                detection.Result!.Source, detection.Result.Quality, cancellationToken,
                JsonSerializer.Serialize(new { kind = "NoAdditionalSortingRequired",
                    ordinaryOk = detection.Result.Objects.Where(x => !x.SpecialHandlingCompleted).Select(x => x.ObjectId),
                    excludedScope = request.Detection.Scope }));
        }
        foreach (var action in mapped.Actions)
        {
            await PauseAtAsync(request, "BeforeSorting:" + action.OperationId,
                WholeTrayWorkflowStage.Sorting, RunState.Detection, sortingDeadline, cancellationToken);
            var assignment = reservation.Assignments.Single(x => x.OperationId == action.OperationId);
            var requestForAction = new PlcStageActionRequest(
                new(action.RunId, action.OperationId, Guid.NewGuid(), 1, sessionId, action.ConnectionEpoch,
                    request.ConfigSnapshotId ?? throw new InvalidOperationException("SnapshotRequired"), action.RecipePlanVersion,
                    action.TrayId, action.ObjectId, PhysicalSlotIndex: action.PhysicalSlotIndex),
                request.Detection.StationId, request.Detection.LineId, PlcWorkflowStage.Sorting,
                SortingTargetAllocator.AssignmentDigest(assignment),
                ActionWindows.FromUtc(clock, request.Deadlines?.StartedUtc ?? sortingDeadline - T050RetryPolicy.StageTimeout,
                    sortingDeadline, request.Detection.ClockId),
                $"{request.Detection.IdempotencyKey}:sorting:{action.Sequence}:{action.OperationId:N}",
                PhysicalSlotIndex: action.PhysicalSlotIndex, PositionTolerance: request.PositionTolerance,
                TargetPurpose: request.TargetPurpose, SortingSource: action.Position,
                SortingTarget: sortingTargets[action.OperationId], ReservationReference: assignment.ReservationReference)
            { Scope=request.Detection.Scope, TransferPurpose = action.ReturnsToOrigin ? TransferPurpose.ReturnToOrigin :
                    TransferPurpose.Sorting,
                RequestedGripperId = sortingGrippers[action.OperationId] };
            RuntimeDiagnostics.Record("SortingGripper", "Resolved", request.Detection.RunId,
                new { action.OperationId, action.ObjectId, action.MemberId, action.SlotId,
                    requestForAction.RequestedGripperId });
            var outcome = await ExecutePlcActionAsync(requestForAction, WholeTrayWorkflowStage.Sorting,
                sortingDeadline, cancellationToken);
            associations.Add(outcome.Association);
            if (outcome.Terminal is not null)
            {
                return new(outcome.Terminal.Value, WholeTrayWorkflowStage.Sorting, outcome.ErrorCode,
                    associations, await ProjectionAsync(request, WholeTrayWorkflowStage.Sorting, cancellationToken));
            }
        }

        if(request.Detection.Scope is { } completedScope)
        {
            await AppendAsync(request,WholeTrayWorkflowStage.Sorting,request.MappingOperationId,1,
                StageEventType.Executing,null,"unit-cycle:"+completedScope.UnitId,detection.Result!.Source,
                detection.Result.Quality,cancellationToken,JsonSerializer.Serialize(new {
                    kind="UnitCycleCompleted",completedScope,origin=request.Plan.OriginalSlots![completedScope.SlotId],
                    actions=associations.Select(a=>a.OperationId),physicalCompletionRequired=true }));
            return new(ThreeStageExecutionStatus.UnitCompleted,WholeTrayWorkflowStage.Sorting,null,associations,
                await ProjectionAsync(request,WholeTrayWorkflowStage.Sorting,cancellationToken)){ScopedDetection=detection.Result};
        }
        return await ExecuteUnloadAsync(request,associations,cancellationToken);
    }

    private async Task<ThreeStageExecutionResult> ExecuteUnloadAsync(ThreeStageExecutionRequest request,
        List<PlcActionAssociation> associations,CancellationToken cancellationToken)
    {
        var sessionId=request.Detection.SessionId;
        var unloadDeadline = request.Deadlines?.UnloadDeadlineUtc ??
            clock.GetUtcNow() + T050RetryPolicy.StageTimeout;
        var unloadWindow = ActionWindows.FromUtc(clock, request.Deadlines?.StartedUtc ?? unloadDeadline - T050RetryPolicy.StageTimeout,
            unloadDeadline, request.Detection.ClockId);
        await PauseAtAsync(request, "BeforeUnload", WholeTrayWorkflowStage.UnloadPreparation,
            RunState.Detection, unloadDeadline, cancellationToken);
        var result = await ExecutePublicUnloadAsync(new(request.Detection.RunId, request.Detection.TrayId,
            request.Detection.StationId, request.Detection.LineId, sessionId, request.Detection.ConnectionEpoch,
            request.ConfigSnapshotId ?? throw new InvalidOperationException("SnapshotRequired"), request.Detection.PlanRevision,
            request.UnloadOperationId, request.UnloadTarget ?? throw new InvalidOperationException("ManualLoadingPositionMissing"),
            request.PositionTolerance, request.TargetPurpose ?? request.Detection.Purpose, unloadWindow,
            $"{request.Detection.IdempotencyKey}:unload-preparation:{request.UnloadOperationId:N}"), cancellationToken);
        return result with { PlcActions = associations.Concat(result.PlcActions).ToArray() };
    }

    public async Task<ThreeStageExecutionResult> ExecutePublicUnloadAsync(PublicUnloadRequest request,
        CancellationToken token)
    {
        var action = new PlcStageActionRequest(new(request.RunId, request.OperationId, Guid.NewGuid(), 1,
            request.SessionId, request.ConnectionEpoch, request.SnapshotId, request.ExecutionRevision, request.TrayId),
            request.StationId, request.LineId, PlcWorkflowStage.UnloadPreparation,
            Digest(JsonSerializer.Serialize(new { request.Target, request.SnapshotId })), request.Window, request.IdempotencyKey,
            UnloadTarget: request.Target, PositionTolerance: request.PositionTolerance, TargetPurpose: request.Purpose);
        var unload = await ExecutePlcActionAsync(action, WholeTrayWorkflowStage.UnloadPreparation,
            request.Window.DeadlineUtc, token);
        return new(unload.Terminal ?? ThreeStageExecutionStatus.StagesCompleted, WholeTrayWorkflowStage.UnloadPreparation,
            unload.ErrorCode, [unload.Association], await eventStore.GetProjectionAsync(request.RunId, request.TrayId,
                WholeTrayWorkflowStage.UnloadPreparation, token));
    }

    private async Task<ThreeStageExecutionResult> ExecuteSpecialTrayAsync(ThreeStageExecutionRequest request,CancellationToken token)
    {
        var completed=new List<DetectionPortResult>();var actions=new List<PlcActionAssociation>();
        // Anomalous entities stay in their original slots until normal unit cycles close.
        var units = request.Plan.Steps.Where(s=>s.Kind==RecipeStepKind.TransferToRotation).ToArray();
        var ordered = units.OrderBy(s => request.Detection.InitialObservation?.Slots.SingleOrDefault(o =>
            o.PhysicalSlotIndex == s.PhysicalSlotIndex)?.Pose == TrayPose.Abnormal ? 1 : 0).ThenBy(s => s.Sequence);
        foreach(var unit in ordered)
        {
            var scope=new DetectionExecutionScope(unit.UnitId,unit.SlotId!);
            var sequences=request.Plan.Steps.Where(scope.Includes).Select(s=>s.Sequence).ToHashSet();
            var identities=request.Plan.Steps.Where(scope.Includes).Select(s=>s.MemberId??s.UnitId).ToHashSet(StringComparer.Ordinal);
            var scoped=request with { MappingOperationId=Guid.NewGuid(),Detection=request.Detection with {
                OperationId=Guid.NewGuid(),Scope=scope,
                IdempotencyKey=request.Detection.IdempotencyKey+":unit:"+unit.UnitId,
                Targets=request.Detection.Targets?.Where(t=>sequences.Contains(t.StepSequence)).ToArray(),
                ExpectedObjects=request.Detection.ExpectedObjects?.Where(e=>identities.Contains(e.ObjectId)).ToArray() } };
            var result=await ExecuteCoreAsync(scoped,token);actions.AddRange(result.PlcActions);
            if(result.Status!=ThreeStageExecutionStatus.UnitCompleted)return result with {PlcActions=actions};
            completed.Add(result.ScopedDetection??throw new InvalidOperationException("CompletedUnitDetectionMissing"));
        }
        if(completed.Count==0)throw new InvalidOperationException("SpecialTrayUnitsMissing");
        await AppendAsync(request,WholeTrayWorkflowStage.Detection,request.Detection.OperationId,1,StageEventType.Completed,
            null,"detection:all-unit-cycles-completed",completed[0].Source,completed[0].Quality,token,
            JsonSerializer.Serialize(new {kind="DetectionCompleted",ResultReference="detection://"+request.Detection.OperationId,
                AlgorithmOrigin=completed[0].AlgorithmOrigin,CaptureFacts=completed.SelectMany(d=>d.CaptureFacts).ToArray(),
                EvidenceBasis=completed.All(d=>d.EvidenceBasis==DetectionEvidenceBasis.InitialPoseExclusion)?
                    DetectionEvidenceBasis.InitialPoseExclusion:DetectionEvidenceBasis.ProductInspection,
                objects=completed.SelectMany(d=>d.Objects).ToArray(),unitCycles=completed.Count,
                posePending=completed.SelectMany(d=>d.PosePending).ToArray(),
                frozenInputsDigest=request.Detection.Inputs!.SemanticDigest }));
        await AppendAsync(request,WholeTrayWorkflowStage.Sorting,request.MappingOperationId,1,StageEventType.Completed,
            null,"sorting:all-unit-cycles-completed",completed[0].Source,completed[0].Quality,token,
            JsonSerializer.Serialize(new {kind="AllUnitCyclesCompleted",unitCycles=completed.Count}));
        return await ExecuteUnloadAsync(request,actions,token);
    }

    private Task PauseAtAsync(ThreeStageExecutionRequest request, string boundary,
        WholeTrayWorkflowStage stage, RunState resume, DateTimeOffset deadline, CancellationToken ct) =>
        pause is null ? Task.CompletedTask : pause.WaitAsync(request.Detection.RunId, resume, boundary,
            request.Detection.ConnectionEpoch, deadline,
            (kind, _, token) => AppendAsync(request, stage, Guid.NewGuid(), 1,
                StageEventType.Executing, null, "pause:" + boundary + ":" + kind,
                ResultSource.HostDerived, ResultQuality.Derived, token,
                JsonSerializer.Serialize(new { kind, boundary, sameRun = true })), ct);

    private static FixedPoint? ResolveSortingTarget(RecipeRunPlan plan, SortingActionPlan action,
        PublicConfiguration? config)
    {
        if (config is null || !plan.ExecutionPositions.TryGetValue(action.SlotId, out var inputs)) return null;
        var configured = action.ReturnsToOrigin
            ? plan.OriginalSlots?.GetValueOrDefault(action.SlotId) is { } origin && origin.EntityId == action.ObjectId && origin.TrayId == action.TrayId
                ? inputs.ForObject(plan.UnitKind, action.Classification).OriginPutBack : null
            : inputs.ForObject(plan.UnitKind, action.Classification).Sorting.GetValueOrDefault(action.Disposition);
        if (configured is null || string.IsNullOrWhiteSpace(configured.CoordinateEvidenceReference)) return null;
        var target = configured.Point;
        var limits = config.Motion.Limits;
        return target.Unit == config.Motion.Unit && target.Frame == config.Motion.Frame &&
            double.IsFinite(target.X) && double.IsFinite(target.Y) && double.IsFinite(target.Z) &&
            target.X >= limits.XMin && target.X <= limits.XMax &&
            target.Y >= limits.YMin && target.Y <= limits.YMax &&
            target.Z >= limits.ZMin && target.Z <= limits.ZMax ? target : null;
    }

    private async Task<DetectionExecutionOutcome> ExecuteDetectionAsync(
        ThreeStageExecutionRequest request, CancellationToken cancellationToken)
    {
        var deadline = request.Detection.DeadlineUtc;
        var communicationRetries = 0;
        var algorithmRetries = 0;
        var attempt = 0;
        var algorithmOrigin = ComponentExecutionOrigin.Unknown;
        IReadOnlyList<CorrelatedCaptureFact> captureFacts = [];
        DetectionPortResult? lastResult = null;
        while (true)
        {
            attempt++;
            if (clock.GetUtcNow() >= deadline)
            {
                return await PendingAsync(request, attempt, "StageDeadlineExceeded", deadline,
                    cancellationToken, algorithmOrigin, captureFacts, lastResult);
            }

            var detectionIntentPayload = JsonSerializer.Serialize(new
            {
                request.Detection.InputMediaReferences,
                request.Detection.ComponentEvidenceReferences,
                request.Detection.PlanRevision,
                stageStartedAt = request.Detection.StageStartedAtUtc,
                deadline
            });
            await AppendAsync(request, WholeTrayWorkflowStage.Detection, request.Detection.OperationId,
                attempt, StageEventType.IntentRecorded, null, $"detection:{attempt}:intent",
                ResultSource.HostDerived, ResultQuality.Derived, cancellationToken,
                detectionIntentPayload);
            await AppendAsync(request, WholeTrayWorkflowStage.Detection, request.Detection.OperationId,
                attempt, StageEventType.Started, null, $"detection:{attempt}:started",
                ResultSource.HostDerived, ResultQuality.Derived, cancellationToken);
            DetectionPortResult result;
            try
            {
                result = await detectionPort.ExecuteAsync(request.Detection with
                {
                    DeadlineUtc = deadline,
                    Attempt = attempt,
                    Plan = request.Plan,
                    StageStartedAtUtc = request.Detection.StageStartedAtUtc ?? clock.GetUtcNow()
                }, cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                result = new(request.Detection, DetectionResultKind.TimedOut, null, [],
                    ResultSource.Fallback, ResultQuality.Unknown, "DetectionDeadlineExceeded", clock.GetUtcNow());
            }

            if (!IsDetectionCorrelated(request.Detection, result))
            {
                await AppendAsync(request, WholeTrayWorkflowStage.Detection, request.Detection.OperationId,
                    attempt, StageEventType.Failed, "StaleDetectionFeedback", $"detection:{attempt}:stale",
                    result.Source, result.Quality, cancellationToken);
                return Terminal(new(ThreeStageExecutionStatus.Failed, WholeTrayWorkflowStage.Detection,
                    "StaleDetectionFeedback", [], await ProjectionAsync(request,
                        WholeTrayWorkflowStage.Detection, cancellationToken)));
            }

            // Only a correlated result can contribute actual producer/capture facts.
            // Pending changes disposition; it must neither erase nor invent those facts.
            lastResult = result;
            if (result.EndBasisReference is { } intervention)
                return Terminal(new(ThreeStageExecutionStatus.EarlyEndRequested, WholeTrayWorkflowStage.Detection,
                    "ManualIntervention", [], await ProjectionAsync(request, WholeTrayWorkflowStage.Detection, cancellationToken))
                    { EndBasisReference = intervention });
            algorithmOrigin = result.AlgorithmOrigin;
            captureFacts = result.CaptureFacts;
            if (clock.GetUtcNow() >= deadline && result.Kind is not (DetectionResultKind.Failed or DetectionResultKind.UnknownHeld))
                return await PendingAsync(request, attempt, "StageDeadlineExceeded", deadline,
                    cancellationToken, algorithmOrigin, captureFacts, lastResult);

            switch (result.Kind)
            {
                case DetectionResultKind.UnknownHeld:
                    await AppendAsync(request, WholeTrayWorkflowStage.Detection, request.Detection.OperationId,
                        attempt, StageEventType.UnknownHeld, result.ErrorCode, $"detection:{attempt}:unknown-held",
                        result.Source, result.Quality, cancellationToken);
                    return Terminal(new(ThreeStageExecutionStatus.UnknownHeld, WholeTrayWorkflowStage.Detection,
                        result.ErrorCode ?? "DetectionMotionUnconfirmed", [], await ProjectionAsync(request,
                            WholeTrayWorkflowStage.Detection, cancellationToken)));
                case DetectionResultKind.Completed when result.IsValid:
                    await AppendAsync(request, WholeTrayWorkflowStage.Detection, request.Detection.OperationId,
                        attempt, request.Detection.Scope is null ? StageEventType.Completed : StageEventType.Executing, null, $"detection:{attempt}:completed",
                        result.Source, result.Quality, cancellationToken,
                        JsonSerializer.Serialize(new
                        {
                            kind = request.Detection.Scope is null ? "DetectionCompleted" : "DetectionUnitCompleted",
                            request.Detection.Scope, result.ResultReference,
                            result.EvidenceReferences,
                            result.AlgorithmOrigin, result.CaptureFacts, result.EvidenceBasis,
                            objects = result.Objects.Select(x => new
                            {
                                x.ObjectId, x.Disposition, x.EvidenceReferences
                            })
                        }));
                    return new(result, null);
                case DetectionResultKind.Disconnected:
                    await AppendAsync(request, WholeTrayWorkflowStage.Detection, request.Detection.OperationId,
                        attempt, StageEventType.AttemptFailed, result.ErrorCode, $"detection:{attempt}:disconnected",
                        result.Source, result.Quality, cancellationToken);
                    if (communicationRetries < T050RetryPolicy.CommunicationRetries)
                    {
                        if (await DelayBeforeRetryAsync(deadline,
                            T050RetryPolicy.CommunicationBackoff[communicationRetries], cancellationToken))
                        {
                            await AppendAsync(request, WholeTrayWorkflowStage.Detection,
                                request.Detection.OperationId, attempt, StageEventType.RetryScheduled,
                                result.ErrorCode, $"detection:{attempt}:communication-retry",
                                result.Source, result.Quality, cancellationToken);
                            communicationRetries++;
                            continue;
                        }
                        return await PendingAsync(request, attempt, "StageDeadlineExceeded", deadline,
                            cancellationToken, algorithmOrigin, captureFacts, lastResult);
                    }
                    return await PendingAsync(request, attempt,
                        result.ErrorCode ?? "DetectionDisconnected", deadline, cancellationToken, algorithmOrigin, captureFacts, lastResult);
                case DetectionResultKind.TimedOut:
                    await AppendAsync(request, WholeTrayWorkflowStage.Detection, request.Detection.OperationId,
                        attempt, StageEventType.AttemptFailed, result.ErrorCode, $"detection:{attempt}:timedout",
                        result.Source, result.Quality, cancellationToken);
                    if (algorithmRetries < T050RetryPolicy.AlgorithmTimeoutRetries)
                    {
                        if (await DelayBeforeRetryAsync(deadline,
                            T050RetryPolicy.AlgorithmBackoff[algorithmRetries], cancellationToken))
                        {
                            await AppendAsync(request, WholeTrayWorkflowStage.Detection,
                                request.Detection.OperationId, attempt, StageEventType.RetryScheduled,
                                result.ErrorCode, $"detection:{attempt}:algorithm-retry",
                                result.Source, result.Quality, cancellationToken);
                            algorithmRetries++;
                            continue;
                        }
                        return await PendingAsync(request, attempt, "StageDeadlineExceeded", deadline,
                            cancellationToken, algorithmOrigin, captureFacts, lastResult);
                    }
                    return await PendingAsync(request, attempt,
                        result.ErrorCode ?? "DetectionTimedOut", deadline, cancellationToken, algorithmOrigin, captureFacts, lastResult);
                default:
                    await AppendAsync(request, WholeTrayWorkflowStage.Detection, request.Detection.OperationId,
                        attempt, StageEventType.Failed, result.ErrorCode ?? "DetectionFailed",
                        $"detection:{attempt}:failed", result.Source, result.Quality, cancellationToken);
                    return Terminal(new(ThreeStageExecutionStatus.Failed, WholeTrayWorkflowStage.Detection,
                        result.ErrorCode ?? "DetectionFailed", [], await ProjectionAsync(request,
                            WholeTrayWorkflowStage.Detection, cancellationToken)));
            }
        }
    }

    private async Task<DetectionExecutionOutcome> PendingAsync(ThreeStageExecutionRequest request,
        int attempt, string originalError, DateTimeOffset deadline,
        CancellationToken cancellationToken, ComponentExecutionOrigin algorithmOrigin,
        IReadOnlyList<CorrelatedCaptureFact> captureFacts, DetectionPortResult? lastResult)
    {
        var configured = request.Plan.Steps.Where(x => x.Kind == RecipeStepKind.SortUnit).ToArray();
        if (lastResult?.LastObservation is not { IsValid: true } observation ||
            observation.RunId != request.Detection.RunId || observation.TrayId != request.Detection.TrayId ||
            configured.Any(s => s.PhysicalSlotIndex is not { } index ||
                !lastResult.SlotParticipation.TryGetValue(index, out var state) || state.State == SlotParticipationState.Unknown))
        {
            await AppendAsync(request, WholeTrayWorkflowStage.Detection, request.Detection.OperationId,
                attempt, StageEventType.ManualReviewRequested, "PendingObservationCoverageUnknown",
                $"detection:{attempt}:pending-observation-missing", ResultSource.HostDerived,
                ResultQuality.Unknown, cancellationToken, JsonSerializer.Serialize(new { originalError }));
            return Terminal(new(ThreeStageExecutionStatus.PausedForManualReview,
                WholeTrayWorkflowStage.Detection, "PendingObservationCoverageUnknown", [],
                await ProjectionAsync(request, WholeTrayWorkflowStage.Detection, cancellationToken)));
        }
        var activeIds = configured.Where(s => lastResult.SlotParticipation[s.PhysicalSlotIndex!.Value].State ==
            SlotParticipationState.Participating).Select(s => s.MemberId ?? s.UnitId).ToHashSet(StringComparer.Ordinal);
        var expectedIds = request.Plan.Steps.Where(x => x.Kind == RecipeStepKind.SortUnit)
            .Select(step => step.MemberId ?? step.UnitId).Distinct(StringComparer.Ordinal).ToArray();
        var known = request.Detection.ExpectedObjects;
        if (known is null || expectedIds.Length == 0 ||
            expectedIds.Any(id => known.Count(item => item.ObjectId == id) != 1) ||
            known.Any(item => !item.IsApprovedPosition ||
                !expectedIds.Contains(item.ObjectId, StringComparer.Ordinal)))
        {
            await AppendAsync(request.Detection.RunId, request.Detection.TrayId,
                request.Detection.StationId.ToString(), request.Detection.LineId.ToString(),
                WholeTrayWorkflowStage.Detection, request.Detection.OperationId, attempt,
                request.Detection.ConnectionEpoch, StageEventType.ManualReviewRequested,
                "PendingPhysicalPositionMissing",
                $"{request.Detection.IdempotencyKey}:pending-position-missing",
                ResultSource.Fallback, ResultQuality.Unknown,
                JsonSerializer.Serialize(new { originalError, disposition = "NoSortingOrFinal",
                    missingPositionFor = expectedIds }), cancellationToken,
                request.Detection.PlanRevision, request.Detection.StageStartedAtUtc, deadline);
            return Terminal(new(ThreeStageExecutionStatus.PausedForManualReview,
                WholeTrayWorkflowStage.Detection, "PendingPhysicalPositionMissing", [],
                await ProjectionAsync(request, WholeTrayWorkflowStage.Detection, cancellationToken)));
        }
        var objects = known!.Where(item => activeIds.Contains(item.ObjectId)).Select(item => new DetectionObjectResult(item.ObjectId,
            item.Position, item.ExpectedClassification, "Pending",
            [$"error://{originalError}", $"plan://{request.Detection.PlanRevision}"])).ToArray();
        var result = new DetectionPortResult(request.Detection with
        {
            Attempt = attempt,
            DeadlineUtc = deadline
        }, DetectionResultKind.Completed,
            $"pending://{request.Detection.RunId:D}/{request.Detection.OperationId:D}",
            objects, ResultSource.Fallback, ResultQuality.Degraded, originalError, clock.GetUtcNow(),
            [..request.Detection.InputMediaReferences, $"error://{originalError}"]) { AlgorithmOrigin = algorithmOrigin, CaptureFacts = captureFacts,
                LastObservation = observation, SlotParticipation = lastResult.SlotParticipation,
                EvidenceBasis = lastResult.EvidenceBasis, PosePending = lastResult.PosePending };
        var payload = JsonSerializer.Serialize(new
        {
            originalError,
            attempt,
            stageStartedAt = request.Detection.StageStartedAtUtc,
            deadline,
            inputReferences = request.Detection.InputMediaReferences,
            result.ResultReference,
            result.AlgorithmOrigin, result.CaptureFacts, result.EvidenceBasis,
            result.LastObservation, result.SlotParticipation,
            objects = objects.Select(x => new { x.ObjectId, x.Disposition, x.EvidenceReferences })
        });
        await AppendAsync(request.Detection.RunId, request.Detection.TrayId,
            request.Detection.StationId.ToString(), request.Detection.LineId.ToString(),
            WholeTrayWorkflowStage.Detection, request.Detection.OperationId, attempt,
            request.Detection.ConnectionEpoch, StageEventType.PendingRecorded, originalError,
            $"{request.Detection.IdempotencyKey}:pending", ResultSource.Fallback,
            ResultQuality.Degraded, payload, cancellationToken,
            request.Detection.PlanRevision, request.Detection.StageStartedAtUtc, deadline);
        await AppendAsync(request.Detection.RunId, request.Detection.TrayId,
            request.Detection.StationId.ToString(), request.Detection.LineId.ToString(),
            WholeTrayWorkflowStage.Detection, request.Detection.OperationId, attempt,
            request.Detection.ConnectionEpoch, StageEventType.Completed, originalError,
            $"{request.Detection.IdempotencyKey}:pending-completed", ResultSource.Fallback,
            ResultQuality.Degraded, payload, cancellationToken,
            request.Detection.PlanRevision, request.Detection.StageStartedAtUtc, deadline);
        return new(result, null);
    }

    private async Task<PlcActionOutcome> ExecutePlcActionAsync(
        PlcStageActionRequest request, WholeTrayWorkflowStage workflowStage,
        DateTimeOffset deadline, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        if (now >= deadline)
        {
            await AppendPlcEventAsync(request, workflowStage, StageEventType.TimedOut,
                "StageDeadlineExceeded", "deadline", cancellationToken);
            return PlcActionOutcome.TerminalResult(ThreeStageExecutionStatus.TimedOut,
                "StageDeadlineExceeded", request, workflowStage);
        }

        await AppendPlcEventAsync(request, workflowStage, StageEventType.IntentRecorded, null,
            "intent", cancellationToken);
        await AppendPlcEventAsync(request, workflowStage, StageEventType.Started, null,
            "started", cancellationToken);
        PlcStageActionResult result;
        try
        {
            result = await plcPort.ExecuteAsync(request, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            result = new(request, StageActionKind.UnknownHeld, request.Correlation.ActionId, request.ConnectionEpoch,
                "DeviceActionDeadlineExceeded", true, false, clock.GetUtcNow(),
                new(DeviceProvider.Unavailable, null, EvidenceQuality.Unknown), null);
        }

        var association = new PlcActionAssociation(workflowStage, request.Stage, request.OperationId,
            request.ConnectionEpoch, result.HoldsDevice, result.CanRetry);
        var matches = Matches(request, result);
        var unknown = !matches || result.Kind is StageActionKind.UnknownHeld or
            StageActionKind.Accepted or StageActionKind.Executing ||
            (result.Kind is StageActionKind.Disconnected or StageActionKind.TimedOut && result.HoldsDevice) ||
            (result.Kind == StageActionKind.Completed && !result.IsCompleted);
        if (unknown)
        {
            association = association with { DeviceHeld = true, AutomaticRetryAllowed = false };
            var error = !matches ? "StalePlcFeedback" : result.ErrorCode ?? "UnknownPlcActionResult";
            await AppendPlcEventAsync(request, workflowStage, StageEventType.UnknownHeld, error,
                "unknown", cancellationToken, result);
            return PlcActionOutcome.Unknown(association, error);
        }

        if (result.Kind == StageActionKind.Failed)
        {
            await AppendPlcEventAsync(request, workflowStage, StageEventType.Failed,
                result.ErrorCode ?? "PlcActionFailed", "failed", cancellationToken, result);
            return PlcActionOutcome.TerminalResult(ThreeStageExecutionStatus.Failed,
                result.ErrorCode ?? "PlcActionFailed", request, workflowStage, association);
        }

        if (clock.GetUtcNow() >= deadline)
        {
            await AppendPlcEventAsync(request, workflowStage, StageEventType.UnknownHeld,
                "StageDeadlineExceeded", "late-completion", cancellationToken, result);
            return PlcActionOutcome.Unknown(association with { DeviceHeld = true,
                AutomaticRetryAllowed = false }, "StageDeadlineExceeded");
        }

        if (request.Stage is PlcWorkflowStage.Sorting or PlcWorkflowStage.TransferToRotation)
            await sortingAllocator.RecordOccupiedAsync(request, result, cancellationToken);
        else
            await AppendPlcEventAsync(request, workflowStage, StageEventType.Completed, null,
                "completed", cancellationToken, result);
        return PlcActionOutcome.Success(association with { DeviceHeld = false,
            AutomaticRetryAllowed = false });
    }

    private async Task<bool> DelayBeforeRetryAsync(DateTimeOffset deadline, TimeSpan requested,
        CancellationToken cancellationToken)
    {
        var remaining = deadline - clock.GetUtcNow();
        if (remaining <= TimeSpan.Zero) return false;
        var actual = requested <= remaining ? requested : remaining;
        await delay.DelayAsync(actual, clock, cancellationToken);
        return clock.GetUtcNow() < deadline;
    }

    private async Task AppendPlcEventAsync(PlcStageActionRequest request,
        WholeTrayWorkflowStage workflowStage, StageEventType eventType, string? error, string suffix,
        CancellationToken cancellationToken, PlcStageActionResult? result = null) => await AppendAsync(
        request.RunId, request.TrayId, request.StationId.ToString(), request.LineId.ToString(), workflowStage,
        request.OperationId, request.Attempt, request.ConnectionEpoch, eventType, error,
        $"{request.IdempotencyKey}:{suffix}", Source(result?.ExecutionOrigin),
        Quality(result?.ExecutionOrigin),
        JsonSerializer.Serialize(new { schemaVersion = "stage-action/1", correlation = request.Correlation,
            stage = request.Stage.ToString(), request.Scope,request.TransferPurpose, request.OperationId, request.ConnectionEpoch, request.UnloadTarget,
            request.PositionTolerance, request.TargetPurpose, request.ConfigSnapshotId, result?.ErrorCode,
            result?.HoldsDevice, result?.ExecutionOrigin, result?.Evidence }, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            cancellationToken, request.PlanRevision, request.Window.StartedUtc, request.DeadlineUtc);

    private static ResultSource Source(ExecutionOrigin? origin) => origin?.Provider switch
    { DeviceProvider.Real => ResultSource.Real, DeviceProvider.Virtual => ResultSource.Virtual,
      DeviceProvider.Simulated => ResultSource.Simulated, _ => ResultSource.Fallback };
    private static ResultQuality Quality(ExecutionOrigin? origin) => origin?.Quality switch
    { EvidenceQuality.Measured => ResultQuality.Measured, EvidenceQuality.Derived => ResultQuality.Derived,
      EvidenceQuality.Degraded => ResultQuality.Degraded, _ => ResultQuality.Unknown };

    private Task AppendAsync(ThreeStageExecutionRequest request, WholeTrayWorkflowStage stage,
        Guid operationId, int attempt, StageEventType eventType, string? error, string key,
        ResultSource source, ResultQuality quality, CancellationToken cancellationToken,
        string payload = "{}") => AppendAsync(
        request.Detection.RunId, request.Detection.TrayId, request.Detection.StationId.ToString(),
        request.Detection.LineId.ToString(), stage, operationId, attempt, request.Detection.ConnectionEpoch,
        eventType, error, request.Detection.Scope is { } scope
            ? $"{key}:unit:{scope.UnitId}:slot:{scope.SlotId}" : key,
        source, quality, payload, cancellationToken,
        request.Detection.PlanRevision, request.Detection.StageStartedAtUtc,
        request.Detection.DeadlineUtc);

    private async Task AppendAsync(Guid runId, Guid trayId, string stationId, string lineId,
        WholeTrayWorkflowStage stage, Guid operationId, int attempt, long epoch, StageEventType eventType,
        string? error, string key, ResultSource source, ResultQuality quality, string payload,
        CancellationToken cancellationToken, string planRevision = "",
        DateTimeOffset? stageStartedAtUtc = null, DateTimeOffset? stageDeadlineAtUtc = null)
    {
        var digest = Digest(payload);
        var result = await eventStore.AppendAsync(new StageEventAppendRequest(Guid.NewGuid(), runId, trayId,
            stationId, lineId, stage, operationId, attempt, epoch, eventType, clock.GetUtcNow(), source,
            quality, error, digest, payload, key, planRevision, stageStartedAtUtc,
            stageDeadlineAtUtc), cancellationToken).WaitAsync(sortingAllocator.CriticalSaveTimeout, clock,
                cancellationToken);
        if (result.State == StageEventCommitState.Conflict)
            throw new InvalidOperationException("T050StageEventIdempotencyConflict");
    }

    private async Task<StageProjection?> ProjectionAsync(ThreeStageExecutionRequest request,
        WholeTrayWorkflowStage stage, CancellationToken cancellationToken) =>
        await eventStore.GetProjectionAsync(request.Detection.RunId, request.Detection.TrayId, stage,
            cancellationToken);

    private static bool IsDetectionCorrelated(DetectionRequest expected, DetectionPortResult actual) =>
        actual.IsValid && actual.Request.RunId == expected.RunId && actual.Request.TrayId == expected.TrayId &&
        actual.Request.StationId == expected.StationId && actual.Request.LineId == expected.LineId &&
        actual.Request.OperationId == expected.OperationId &&
        actual.Request.ConnectionEpoch == expected.ConnectionEpoch &&
        actual.Request.Stage == WholeTrayWorkflowStage.Detection;

    private static bool Matches(PlcStageActionRequest expected, PlcStageActionResult actual) =>
        actual.IsCorrelated && actual.Request == expected && actual.Request.RunId == expected.RunId &&
        actual.Request.TrayId == expected.TrayId && actual.Request.StationId == expected.StationId &&
        actual.Request.LineId == expected.LineId && actual.Request.Stage == expected.Stage &&
        actual.Request.OperationId == expected.OperationId && actual.Request.ConnectionEpoch == expected.ConnectionEpoch &&
        actual.ConnectionEpoch == expected.ConnectionEpoch;

    private static string Digest(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed record PlcActionOutcome(
        PlcActionAssociation Association,
        ThreeStageExecutionStatus? Terminal,
        string? ErrorCode)
    {
        public static PlcActionOutcome Success(PlcActionAssociation association) =>
            new(association, null, null);
        public static PlcActionOutcome Unknown(PlcActionAssociation association, string error) =>
            new(association, ThreeStageExecutionStatus.UnknownHeld, error);
        public static PlcActionOutcome TerminalResult(ThreeStageExecutionStatus status, string error,
            PlcStageActionRequest request, WholeTrayWorkflowStage stage,
            PlcActionAssociation? association = null) => new(
                association ?? new(stage, request.Stage, request.OperationId, request.ConnectionEpoch,
                    false, false), status, error);
    }

    private sealed record DetectionExecutionOutcome(
        DetectionPortResult? Result,
        ThreeStageExecutionResult? Terminal);

    private static DetectionExecutionOutcome Terminal(ThreeStageExecutionResult result) =>
        new(null, result);
}

public sealed record ThreeStageRecoveryPlan(
    bool CanResume,
    bool RequiresManualReview,
    WholeTrayWorkflowStage CurrentStage,
    WholeTrayWorkflowStage? LastConfirmedCompletedStage,
    PlcActionAssociation? PlcAction);

public sealed class ThreeStageRecoveryService(IStageEventStore eventStore)
{
    public async Task<IReadOnlyList<StageProjection>> RecoverCommittedAsync(Guid runId,
        Guid trayId, CancellationToken cancellationToken = default)
    {
        var recovered = new List<StageProjection>();
        foreach (var stage in new[] { WholeTrayWorkflowStage.Detection,
                     WholeTrayWorkflowStage.Sorting, WholeTrayWorkflowStage.UnloadPreparation,
                     WholeTrayWorkflowStage.UnlockObservation })
        {
            if (await eventStore.GetProjectionAsync(runId, trayId, stage,
                    cancellationToken) is null) continue;
            recovered.Add(await eventStore.RecoverAsync(runId, trayId, stage,
                cancellationToken));
        }
        return recovered;
    }

    public async Task<ThreeStageRecoveryPlan> InspectAsync(Guid runId, Guid trayId,
        CancellationToken cancellationToken = default)
    {
        var projections = new Dictionary<WholeTrayWorkflowStage, StageProjection?>
        {
            [WholeTrayWorkflowStage.Detection] = await eventStore.GetProjectionAsync(runId, trayId,
                WholeTrayWorkflowStage.Detection, cancellationToken),
            [WholeTrayWorkflowStage.Sorting] = await eventStore.GetProjectionAsync(runId, trayId,
                WholeTrayWorkflowStage.Sorting, cancellationToken),
            [WholeTrayWorkflowStage.UnloadPreparation] = await eventStore.GetProjectionAsync(runId, trayId,
                WholeTrayWorkflowStage.UnloadPreparation, cancellationToken)
        };
        var order = new[] { WholeTrayWorkflowStage.Detection, WholeTrayWorkflowStage.Sorting,
            WholeTrayWorkflowStage.UnloadPreparation };
        WholeTrayWorkflowStage? lastCompleted = null;
        foreach (var stage in order)
        {
            if (projections[stage]?.Status == StageProjectionStatus.Completed) lastCompleted = stage;
            else break;
        }

        foreach (var stage in order)
        {
            var projection = projections[stage];
            if (projection is null || projection.Status == StageProjectionStatus.NotStarted) continue;
            if (projection.Status == StageProjectionStatus.UnknownHeld)
            {
                var plcStage = stage switch
                {
                    WholeTrayWorkflowStage.Sorting => PlcWorkflowStage.Sorting,
                    WholeTrayWorkflowStage.UnloadPreparation => PlcWorkflowStage.UnloadPreparation,
                    _ => PlcWorkflowStage.Sorting
                };
                return new(false, true, stage, lastCompleted,
                    new(stage, plcStage, projection.CurrentOperationId ?? Guid.Empty,
                        projection.ConnectionEpoch, projection.DeviceHeld, projection.AutomaticRetryAllowed));
            }
            if (projection.Status is StageProjectionStatus.Failed or StageProjectionStatus.TimedOut or
                StageProjectionStatus.Disconnected or StageProjectionStatus.PausedForManualReview)
                return new(false, true, stage, lastCompleted, null);
        }

        var next = order.FirstOrDefault(stage => projections[stage]?.Status != StageProjectionStatus.Completed);
        return new(true, false, next, lastCompleted, null);
    }
}
