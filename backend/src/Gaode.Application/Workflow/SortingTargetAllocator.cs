using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Diagnostics;
using System.Text.Json.Serialization;

namespace Gaode.Application.Workflow;

public sealed record SortingAssignment(Guid OperationId, string ObjectId, string SourceSlotId,
    [property: JsonPropertyName("protocolSlotIndex")] int PhysicalSlotIndex, FixedPoint SourcePoint, FixedPoint TargetPoint, string Disposition,
    string ResultReference, string ReservationReference)
{
    public bool IsPosePending { get; init; }
    public string? DetectionState { get; init; }
    public string? Reason { get; init; }
}

public sealed record SortingReservationResult(bool Reserved, string? ErrorCode,
    IReadOnlyList<SortingAssignment> Assignments);

/// <summary>Reserves the configured same-tray Test cells; never invents alternate targets.</summary>
public sealed class SortingTargetAllocator(IStageEventStore store, TimeProvider clock,
    int criticalSaveTimeoutMs) : IPickCommitPort
{
    public TimeSpan CriticalSaveTimeout => TimeSpan.FromMilliseconds(criticalSaveTimeoutMs);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<SortingAssignment> ReserveRotationLoadingAsync(ThreeStageExecutionRequest request,
        RecipeStep step, Guid operationId, FixedPoint source, FixedPoint target, CancellationToken token)
    {
        var detection = request.Detection;
        if (detection.Scope is null || !detection.Scope.Includes(step) ||
            detection.InitialObservation is not { IsValid:true } observation ||
            observation.RunId != detection.RunId || observation.TrayId != detection.TrayId ||
            detection.InitialObservationWriteId is not { } savedObservation ||
            !observation.Slots.Any(s => s.PhysicalSlotIndex == step.PhysicalSlotIndex &&
                s.Presence == TrayPresence.Present && s.Pose == TrayPose.Normal))
            throw new InvalidOperationException("RotationLoadingOriginNotObserved");
        var prior = await ReadAsync(detection.RunId,detection.TrayId,token);
        if (prior.Any(e => Kind(e) == "SortingAssignmentInTransit" &&
            !prior.Any(done => done.OperationId == e.OperationId && Kind(done) == "SortingAssignmentOccupied")))
            throw new InvalidOperationException("PreviousTransferNotPhysicallyCompleted");
        var priorAssignments=prior.SelectMany(Assignments).ToArray();
        foreach(var loaded in priorAssignments.Where(a=>a.Disposition=="RotationLoading"&&a.TargetPoint==target))
            if(!priorAssignments.Any(a=>a.ObjectId==loaded.ObjectId&&a.Disposition!="RotationLoading"&&
                a.SourcePoint==request.Plan.RotationWorkstation!.Pick.Point&&
                prior.Any(e=>e.OperationId==a.OperationId&&Kind(e)=="SortingAssignmentOccupied")))
                throw new InvalidOperationException("RotationWorkstationStillOccupied");
        var eventId = Guid.NewGuid();
        var assignment = new SortingAssignment(operationId, step.MemberId ?? step.UnitId, step.SlotId!,
            step.PhysicalSlotIndex!.Value, source,target,"RotationLoading",$"write://{savedObservation:D}",
            $"sorting-reservation://{eventId:D}/{operationId:D}");
        await SaveAsync(new StageEventAppendRequest(eventId,detection.RunId,detection.TrayId,
            detection.StationId.ToString(),detection.LineId.ToString(),WholeTrayWorkflowStage.Sorting,
            operationId,1,detection.ConnectionEpoch,StageEventType.IntentRecorded,clock.GetUtcNow(),
            ResultSource.HostDerived,ResultQuality.Derived,null,"",JsonSerializer.Serialize(new {
                kind="SortingAssignmentsReserved", assignments=new[]{assignment}, transferPurpose="RotationLoading",
                origin=request.Plan.OriginalSlots![step.SlotId!] },Json),
            detection.IdempotencyKey+":rotation-loading-reservation",detection.PlanRevision,
            detection.StageStartedAtUtc,detection.DeadlineUtc),token);
        return assignment;
    }

    public async Task<SortingReservationResult> ReserveAsync(ThreeStageExecutionRequest request,
        DetectionPortResult detection, IReadOnlyList<SortingActionPlan> actions,
        IReadOnlyDictionary<Guid, FixedPoint?> targets, CancellationToken ct)
    {
        if (actions.Count == 0) return new(true, null, []);
        var occupiedSources = detection.Objects.Where(x => !x.SpecialHandlingCompleted)
            .Select(x => x.Position.Id).Concat(actions.Where(x => x.IsPosePending).Select(x => x.Position.Id)).ToHashSet(StringComparer.Ordinal);
        if (request.Plan.InspectionKind == RecipeInspectionKind.SpecialRotation)
        {
            var transferFacts = await ReadAsync(request.Detection.RunId,request.Detection.TrayId,ct);
            foreach (var action in actions.Where(a => !a.IsPosePending))
            {
                var loaded = transferFacts.Where(e => Kind(e) == "SortingAssignmentsReserved")
                    .SelectMany(Assignments).SingleOrDefault(a => a.ObjectId == action.ObjectId && a.Disposition == "RotationLoading");
                if (loaded is null || !transferFacts.Any(e => e.OperationId == loaded.OperationId &&
                    Kind(e) == "SortingAssignmentOccupied")) return new(false,"RotationLoadingCompletionMissing",[]);
                occupiedSources.Remove(loaded.SourcePoint.Id);
                occupiedSources.Add(loaded.TargetPoint.Id);
            }
        }
        var points = actions.Select(x => targets[x.OperationId]!).ToArray();
        if (points.Any(x => occupiedSources.Contains(x.Id))) return new(false, "SortingTargetOccupied", []);
        if (points.GroupBy(x => x.Id, StringComparer.Ordinal).Any(x => x.Count() > 1))
            return new(false, "SortingTargetCapacityExceeded", []);
        if ((request.Plan.NgCapacity is { } ng && actions.Count(x => x.Disposition == "NG") > ng) ||
            (request.Plan.PendingCapacity is { } pending && actions.Count(x => x.Disposition == "Pending") > pending))
            return new(false, "SortingTargetCapacityExceeded", []);
        var prior = await ReadAsync(request.Detection.RunId, request.Detection.TrayId, ct);
        if (prior.SelectMany(Assignments).Any(x => points.Any(p => p.Id == x.TargetPoint.Id)))
            return new(false, "SortingTargetAlreadyReserved", []);
        var eventId = Guid.NewGuid();
        var assignments = actions.Select(action => new SortingAssignment(action.OperationId, action.ObjectId,
            action.SlotId, action.PhysicalSlotIndex!.Value, action.Position, targets[action.OperationId]!,
            action.Disposition, action.ObservationReference ?? detection.ResultReference!, $"sorting-reservation://{eventId:D}/{action.OperationId:D}") {
                IsPosePending = action.IsPosePending, DetectionState = action.DetectionState,
                Reason = action.IsPosePending ? "3D姿态异常" : null }).ToArray();
        await SaveAsync(new StageEventAppendRequest(eventId, request.Detection.RunId, request.Detection.TrayId,
            request.Detection.StationId.ToString(), request.Detection.LineId.ToString(), WholeTrayWorkflowStage.Sorting,
            request.MappingOperationId, 1, request.Detection.ConnectionEpoch, StageEventType.IntentRecorded,
            clock.GetUtcNow(), detection.Source, detection.Quality, null, "", JsonSerializer.Serialize(new
            { kind = "SortingAssignmentsReserved", assignments, request.Plan.NgCapacity, request.Plan.PendingCapacity,
                origin=request.Detection.Scope is { } scope?request.Plan.OriginalSlots?.GetValueOrDefault(scope.SlotId):null,
                ordinaryOk = detection.Objects.Where(x => !x.SpecialHandlingCompleted && x.Disposition == "OK")
                    .Select(x => x.ObjectId) }, Json),
            $"{request.Detection.IdempotencyKey}:sorting-reserved:{request.MappingOperationId:N}",
            request.Detection.PlanRevision, clock.GetUtcNow(), request.Deadlines?.SortingDeadlineUtc), ct);
        return new(true, null, assignments);
    }

    public static string AssignmentDigest(SortingAssignment assignment) => Digest(JsonSerializer.Serialize(assignment, Json));
    private static FixedPointIdentity PointIdentity(FixedPoint point) => new(point.Id, point.Version,
        Digest(JsonSerializer.Serialize(point)));

    public async Task<PickCommitReceipt> CommitPickAsync(PickCompletionEvidence evidence, CancellationToken ct)
    {
        var c = evidence.Correlation;
        var received = clock.GetTimestamp();
        StageEventAppendResult? committed = null;
        var actual = ActualCommitState.Unknown;
        string? error = null;
        try
        {
            if (!c.IsValid || c.TrayId is null || !evidence.Window.Contains(received) ||
                evidence.SourcePositionReached.Correlation != c || !evidence.SourcePositionReached.Matched ||
                evidence.Observation.ConnectionEpoch != c.ConnectionEpoch || !evidence.Observation.IsValid ||
                evidence.Observation.Reliability != DeviceReliability.Reliable || evidence.DiagnosticEvidenceReferences.Count == 0 ||
                evidence.DiagnosticEvidenceReferences.Any(x => !x.IsValid))
                throw new InvalidOperationException("PickEvidenceNotCurrent");
            var rows = await ReadAsync(c.RunId, c.TrayId.Value, ct);
            var reserved = Find(rows, c);
            var assignment = reserved.Assignment;
            if (assignment.ReservationReference != evidence.ReservationReference ||
                assignment.ObjectId != evidence.ObjectId || c.ObjectId != evidence.ObjectId ||
                assignment.PhysicalSlotIndex != evidence.PhysicalSlotIndex || c.PhysicalSlotIndex != evidence.PhysicalSlotIndex ||
                AssignmentDigest(assignment) != evidence.AssignmentDigest ||
                PointIdentity(assignment.SourcePoint) != evidence.SourcePoint || PointIdentity(assignment.TargetPoint) != evidence.TargetPoint ||
                evidence.SourcePositionReached.Target != assignment.SourcePoint)
                throw new InvalidOperationException("PickAssignmentIdentityMismatch");
            if (!rows.Any(x => x.OperationId == c.OperationId && x.ConnectionEpoch == c.ConnectionEpoch &&
                    x.PlanRevision == c.PlanRevision && x.EventType == StageEventType.IntentRecorded &&
                    HasAction(x, c.ActionId))) throw new InvalidOperationException("PickActionIntentMissing");
            ct.ThrowIfCancellationRequested();
            if (!evidence.Window.Contains(clock.GetTimestamp())) throw new TimeoutException("PickCommitWindowClosed");
            var request = Event(c, reserved.Event, evidence.ExecutionOrigin, StageEventType.Executing, "picked", new
            { schemaVersion = "sorting-evidence/1", kind = "SortingAssignmentInTransit", assignment, evidence,
                sourceVacated = true, targetReserved = true, placeNotYetDispatched = true }, evidence.Window);
            committed = await SaveAsync(request, ct, evidence.Window);
            actual = ActualCommitState.Committed;
        }
        catch (StageEventCommitException fault) { actual = fault.ActualCommit; error = "PickSaveFailed"; }
        catch (Exception fault)
        {
            error = fault is OperationCanceledException or TimeoutException ? "PickCommitReceiptUnavailable" : "PickCommitRejected";
            RuntimeDiagnostics.Record("SortingAssignment", error, c.RunId, new { c.OperationId, c.ActionId,
                disposition = "PreserveReservation_NoPlace_NoReplay" }, fault);
        }
        received = clock.GetTimestamp();
        var valid = committed is { State: StageEventCommitState.Committed } && !ct.IsCancellationRequested && evidence.Window.Contains(received);
        return new(valid ? PickCommitState.Committed : PickCommitState.Unknown, actual,
            valid ? ReceiptValidity.ValidCurrent : committed is null ? ReceiptValidity.None : ReceiptValidity.Invalid,
            committed?.Event.EventId, committed?.Event.EventId, c, evidence.ReservationReference, evidence.AssignmentDigest,
            evidence.DiagnosticEvidenceReferences, committed?.Event.PersistedAt, committed?.Event.Sequence,
            clock.GetUtcNow(), received, evidence.Window, valid ? null : error ?? "PickReceiptInvalid");
    }

    public async Task<RequiredCommitEvidence> ReportPickEvidenceFailureAsync(PickEvidenceFailureNotice notice, CancellationToken ct)
    {
        var c = notice.Correlation;
        Guid eventId = Guid.NewGuid();
        try
        {
            var rows = await ReadAsync(c.RunId, c.TrayId!.Value, ct);
            var reservation = Find(rows, c);
            if (notice.ReservationReference != reservation.Assignment.ReservationReference)
                throw new InvalidOperationException("PickFailureReservationMismatch");
            var request = Event(c, reservation.Event, notice.ExecutionOrigin, StageEventType.UnknownHeld,
                "pick-evidence-unconfirmed", new { schemaVersion = "sorting-evidence/1", kind = "PickEvidencePersistenceUnconfirmed",
                    notice, reservation.Assignment, holdsDevice = true, canRetry = false }) with { EventId = eventId, ErrorCode = notice.Reason };
            var saved = await SaveAsync(request, ct);
            return new(eventId, c, ActualCommitState.Committed, ct.IsCancellationRequested ? ReceiptValidity.Invalid : ReceiptValidity.ValidCurrent,
                saved.Event.Sequence, saved.Event.PersistedAt, clock.GetTimestamp(), null)
                { RecordKind = BusinessCommitRecordKind.StageEvent };
        }
        catch (Exception error)
        {
            RuntimeDiagnostics.Record("SortingAssignment", "PickFailureRecordUnconfirmed", c.RunId,
                new { eventId, notice, persistedNewFactGuaranteed = false, restartBasis = "PreviouslyCommittedIntentAndReservation" }, error);
            return new(eventId, c, (error as StageEventCommitException)?.ActualCommit ?? ActualCommitState.Unknown,
                ReceiptValidity.None, null, null, clock.GetTimestamp(), "FailureRecordUnconfirmed")
                { RecordKind = BusinessCommitRecordKind.StageEvent };
        }
    }

    public async Task RecordOccupiedAsync(PlcStageActionRequest request, PlcStageActionResult result, CancellationToken ct)
    {
        var rows = await ReadAsync(request.RunId, request.TrayId, ct);
        var reservation = Find(rows, request.Correlation);
        var assignment = reservation.Assignment;
        if (assignment.SourcePoint != request.SortingSource || assignment.TargetPoint != request.SortingTarget ||
            assignment.PhysicalSlotIndex != request.PhysicalSlotIndex || assignment.ReservationReference != request.ReservationReference ||
            AssignmentDigest(assignment) != request.ActionParametersDigest)
            throw new InvalidOperationException("SortingReservationMismatched");
        if (!rows.Any(x => x.OperationId == request.OperationId && x.ConnectionEpoch == request.ConnectionEpoch &&
                x.PlanRevision == request.PlanRevision && Kind(x) == "SortingAssignmentInTransit"))
            throw new InvalidOperationException("SortingInTransitCommitMissing");
        if (!result.IsCompleted || result.Request != request || result.Evidence?.Meaning != DeviceCompletionMeaning.MaterialTransferred)
            throw new InvalidOperationException("SortingPlaceCompletionMissing");
        await SaveAsync(Event(request.Correlation, reservation.Event, result.ExecutionOrigin, request.Scope is null ? StageEventType.Completed : StageEventType.Executing, "completed", new
        { schemaVersion = "sorting-evidence/1", kind = "SortingAssignmentOccupied", request.Scope, request.TransferPurpose, assignment, result.Evidence,
            result.ExecutionOrigin, targetOccupied = true, sourceVacated = true }, request.Window), ct, request.Window);
    }

    private static (SortingAssignment Assignment, StageEvent Event) Find(IReadOnlyList<StageEvent> events, ActionCorrelation c)
    {
        var matches = events.Where(x => x.PlanRevision == c.PlanRevision && x.ConnectionEpoch == c.ConnectionEpoch)
            .SelectMany(e => Assignments(e).Select(a => (Assignment: a, Event: e)))
            .Where(x => x.Assignment.OperationId == c.OperationId).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException("SortingReservationMissingOrMismatched");
        return matches[0];
    }
    private static bool HasAction(StageEvent row, Guid actionId)
    {
        using var doc = JsonDocument.Parse(row.PayloadJson);
        return doc.RootElement.TryGetProperty("correlation", out var c) &&
            c.TryGetProperty("actionId", out var a) && a.TryGetGuid(out var id) && id == actionId;
    }
    private Task<IReadOnlyList<StageEvent>> ReadAsync(Guid runId, Guid trayId, CancellationToken ct) =>
        store.ReadAsync(runId, trayId, WholeTrayWorkflowStage.Sorting, ct)
            .WaitAsync(TimeSpan.FromMilliseconds(criticalSaveTimeoutMs), clock, ct);
    private static string? Kind(StageEvent e)
    {
        using var document = JsonDocument.Parse(e.PayloadJson);
        return document.RootElement.TryGetProperty("kind", out var kind) ? kind.GetString() : null;
    }
    private static IEnumerable<SortingAssignment> Assignments(StageEvent e)
    {
        if (Kind(e) != "SortingAssignmentsReserved") return [];
        using var document = JsonDocument.Parse(e.PayloadJson);
        return document.RootElement.GetProperty("assignments").Deserialize<SortingAssignment[]>(Json)!;
    }
    private StageEventAppendRequest Event(ActionCorrelation c, StageEvent reservation, ExecutionOrigin origin,
        StageEventType type, string suffix, object payload, ActionWindow? window = null) => new(Guid.NewGuid(),
        c.RunId, c.TrayId!.Value, reservation.StationId, reservation.LineId, WholeTrayWorkflowStage.Sorting,
        c.OperationId, c.Attempt, c.ConnectionEpoch, type, clock.GetUtcNow(), Source(origin), Quality(origin), null, "",
        JsonSerializer.Serialize(payload, Json), $"sorting:{c.OperationId:N}:{c.ActionId:N}:{suffix}",
        c.PlanRevision ?? "", window?.StartedUtc ?? reservation.StageStartedAtUtc, window?.DeadlineUtc ?? reservation.StageDeadlineAtUtc);
    private static ResultSource Source(ExecutionOrigin origin) => origin.Provider switch
    { DeviceProvider.Real => ResultSource.Real, DeviceProvider.Virtual => ResultSource.Virtual, DeviceProvider.Simulated => ResultSource.Simulated, _ => ResultSource.Fallback };
    private static ResultQuality Quality(ExecutionOrigin origin) => origin.Quality switch
    { EvidenceQuality.Measured => ResultQuality.Measured, EvidenceQuality.Derived => ResultQuality.Derived, EvidenceQuality.Degraded => ResultQuality.Degraded, _ => ResultQuality.Unknown };
    private static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private async Task<StageEventAppendResult> SaveAsync(StageEventAppendRequest request, CancellationToken ct, ActionWindow? window = null)
    {
        request = request with { PayloadDigest = Digest(request.PayloadJson) };
        var remaining = window is null ? criticalSaveTimeoutMs : Math.Min(criticalSaveTimeoutMs,
            (window.DueTick - clock.GetTimestamp()) * 1000d / clock.TimestampFrequency);
        if (remaining <= 0) throw new TimeoutException("SortingSaveWindowClosed");
        using var limit = new CancellationTokenSource(TimeSpan.FromMilliseconds(remaining), clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, limit.Token);
        RuntimeDiagnostics.Record("SortingAssignment", "Saving", request.RunId,
            new { request.EventId, request.OperationId, request.EventType, request.PlanRevision, criticalSaveTimeoutMs });
        var result = await store.AppendAsync(request, linked.Token).WaitAsync(TimeSpan.FromMilliseconds(remaining), clock, linked.Token);
        if (result.State != StageEventCommitState.Committed) throw new InvalidOperationException("SortingAssignmentCommitNotNew:" + result.State);
        return result;
    }
}
