using System.Text.Json;
using Gaode.Application.Workflow;
using Gaode.Application.Ports;
using Gaode.Domain.Station01;

namespace Gaode.Host.Api;

// Reads committed events only. No device calls, new state store or historical writes.
public static class CommittedResultProjection
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static (IReadOnlyList<RunResultProjection> Results, ResultContextProjection? Context) Build(
        IReadOnlyList<RunResultProjection> aggregates, IReadOnlyList<StageEvent> facts)
    {
        var images = new Dictionary<string, List<InspectionResultProjection>>(StringComparer.Ordinal);
        var faces = new Dictionary<(string Id, int Face, string? StageId), RunResultProjection>();
        var required = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var complete = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var kinds = new Dictionary<string, string>(StringComparer.Ordinal);
        var parents = new Dictionary<string, string>(StringComparer.Ordinal);
        ResultContextProjection? context = null;
        foreach (var fact in facts.OrderBy(f => f.Sequence))
        {
            using var doc = JsonDocument.Parse(fact.PayloadJson);
            var r = doc.RootElement;
            if (Text(r, "kind") == "DetectionRequirements")
            {
                foreach (var t in r.GetProperty("targets").EnumerateArray())
                {
                    var id = Text(t, "objectId")!;
                    if (!required.TryGetValue(id, out var refs)) required[id] = refs = [];
                    refs.Add(Text(t, "stepSequence")!);
                }
            }
            if (Text(r, "kind") is not ("DetectionImageCommitted" or "FaceFusionCommitted")) continue;
            var id2 = Text(r, "objectId");
            if (id2 is null) continue; // Old facts without identity remain unavailable.
            var localFace = Number(r, "localFace");
            var sequence = Number(r, "stepSequence");
            var kind = Text(r, "unitKind") switch { "looseGroup" => "Member", "assembledEntity" => "Part", _ => "Single" };
            kinds[id2] = kind;
            if (kind is "Member" or "Part" && Text(r, "unitId") is { } parent) parents[id2] = parent;
            if (Text(r, "camera") == "E") continue;
            context = new(kind, id2, localFace, sequence);
            var reference = $"stage-event://{fact.EventId:D}";
            if (Text(r, "kind") == "FaceFusionCommitted")
            {
                if (localFace is { } face && Text(r, "disposition") is { } disposition)
                    faces[(id2, face, Text(r,"stageId"))] = new("Face", $"{id2}:face{face}" + (Text(r,"stageId") is { } sid ? ":"+sid : ""), disposition, reference,
                        fact.PlanRevision, fact.EventId, fact.Source.ToString(), fact.Quality.ToString())
                        { ParentId = id2, LocalFace = face, StageId=Text(r,"stageId"), CommittedRevision = fact.Sequence, Completeness = "Complete" };
            }
            var parameters = new List<InspectionParameterProjection>();
            if (r.TryGetProperty("requestedCaptureSettings", out var settings))
                foreach (var parameter in settings.EnumerateObject())
                    parameters.Add(new(parameter.Name, parameter.Value.Clone(), "RequestedCapture", reference));
            if (Value(r, "captureFact") is { ValueKind: JsonValueKind.Object } actualCapture &&
                Value(actualCapture, "actualSettings") is { ValueKind: JsonValueKind.Object } actualSettings)
                foreach (var parameter in actualSettings.EnumerateObject())
                    parameters.Add(new(parameter.Name, parameter.Value.Clone(), "ActualCapture", reference));
            var mediaIds = Value(r, "mediaIds") is { ValueKind: JsonValueKind.Array } media
                ? media.EnumerateArray().Select(x => x.GetGuid()).ToArray()
                : Value(r, "mediaId") is { ValueKind: JsonValueKind.String } single ? new[] { single.GetGuid() } : [];
            var callId = GuidValue(r, "callId");
            var inspection = new InspectionResultProjection(callId?.ToString("D") ?? reference,
                sequence, localFace, Text(r, "camera"), GuidValue(r, "captureId"), callId,
                mediaIds, Text(r, "algorithmProfile"), null, null, Text(r, "technicalState"),
                Text(r, "disposition") ?? Text(r, "detectionDisposition"),
                Text(r, "errorCode") is { Length: > 0 } error ? [error] : [], parameters,
                new(parameters.Count > 0 ? "Provided" : "NotProvided", "NotProvided", "NotProvided", "NotProvided"),
                reference, fact.EventId, fact.Sequence, fact.Source.ToString(), fact.Quality.ToString())
                { StageId=Text(r,"stageId"), InputCallIds = r.TryGetProperty("inputCallIds", out var calls)
                    ? calls.EnumerateArray().Select(x => x.GetGuid()).ToArray() : [] };
            if (!images.TryGetValue(id2, out var list)) images[id2] = list = [];
            list.Add(inspection);
            if (Text(r, "kind") == "DetectionImageCommitted" && sequence is { } seq)
            {
                if (!complete.TryGetValue(id2, out var refs)) complete[id2] = refs = [];
                refs.Add(seq.ToString());
            }
        }
        var results = aggregates.Select(a => Enrich(a)).ToList();
        foreach (var pair in images)
            if (!results.Any(x => x.Id == pair.Key && x.Kind != "Face"))
                results.Add(Enrich(new(kinds[pair.Key], pair.Key, null, null, facts.Last().PlanRevision,
                    null, null, null) { Availability = "NotProduced", SaveState = "NotProduced",
                    ParentId = parents.GetValueOrDefault(pair.Key) }));
        foreach (var face in faces.Values)
        {
            results.RemoveAll(x => x.Kind == "Face" && x.Id == face.Id);
            results.Add(face with { Inspections = images.GetValueOrDefault(face.ParentId!, [])
                .Where(x => x.LocalFace == face.LocalFace && x.StageId == face.StageId).ToArray() });
        }
        return (results, context);

        RunResultProjection Enrich(RunResultProjection a)
        {
            var req = required.GetValueOrDefault(a.Id);
            var done = complete.GetValueOrDefault(a.Id);
            return a with { CommittedRevision = a.CommittedEventId is { } eventId ? facts.FirstOrDefault(f => f.EventId == eventId)?.Sequence : null,
                Inspections = images.GetValueOrDefault(a.Id, []),
                RequiredTargetRefs = req, CompletedTargetRefs = done,
                Completeness = req is null ? "Unknown" : req.All(x => done?.Contains(x) == true) ? "Complete" : "Incomplete" };
        }
    }
    public static (IReadOnlyList<RunResultProjection> Results, IReadOnlyList<RunMovementProjection> Movements)
        ApplyDisposition(Guid runId, Guid trayId, string planRevision,
            IReadOnlyList<RunResultProjection> results, IReadOnlyList<StageEvent> facts)
    {
        var physical = results.Where(x => x.PlanRevision == planRevision &&
            x.Kind is "Single" or "Member" or "Assembly").Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        var states = new Dictionary<string, string>(StringComparer.Ordinal);
        var assignments = new Dictionary<Guid, (SortingAssignment Assignment, long Epoch)>();
        var origins = new Dictionary<string,string>(StringComparer.Ordinal);
        var committedPicks = new Dictionary<Guid, ActionCorrelation>();
        var movements = new Dictionary<Guid, RunMovementProjection>();
        foreach (var fact in facts.Where(x => x.RunId == runId && x.TrayId == trayId && x.PlanRevision == planRevision)
            .OrderBy(x => x.PersistedAt).ThenBy(x => x.Sequence))
        {
            using var doc = JsonDocument.Parse(fact.PayloadJson);
            var r = doc.RootElement;
            var kind = Text(r, "kind");
            if (fact.Stage == WholeTrayWorkflowStage.Sorting &&
                (kind == "SortingAssignmentsReserved" && fact.EventType == StageEventType.IntentRecorded ||
                 kind == "NoAdditionalSortingRequired" && fact.EventType == StageEventType.Completed))
            {
                if (Value(r, "ordinaryOk") is { ValueKind: JsonValueKind.Array } ordinary)
                    foreach (var id in ordinary.EnumerateArray().Select(x => x.GetString()!))
                        if (physical.Contains(id) && !states.ContainsKey(id)) states[id] = "NoMoveRequired";
                if (kind == "SortingAssignmentsReserved")
                    foreach (var a in r.GetProperty("assignments").Deserialize<SortingAssignment[]>(Json)!)
                        if (physical.Contains(a.ObjectId) || a.IsPosePending && a.PhysicalSlotIndex > 0 &&
                            a.DetectionState is "NotInspected" or "FurtherInspectionTerminated" && !string.IsNullOrWhiteSpace(a.ResultReference))
                        {
                            physical.Add(a.ObjectId);
                            if(Value(r,"origin") is {ValueKind:JsonValueKind.Object} origin && Text(origin,"cellId") is { } cell)origins[a.ObjectId]=cell;
                            assignments[a.OperationId] = (a, fact.ConnectionEpoch);
                            SetMovement(a, "Reserved", fact.EventId);
                        }
            }
            if (fact.Stage == WholeTrayWorkflowStage.Sorting && assignments.TryGetValue(fact.OperationId, out var reserved) &&
                reserved.Epoch == fact.ConnectionEpoch)
            {
                if (fact.EventType == StageEventType.UnknownHeld)
                    SetMovement(reserved.Assignment, "UnknownHeld", fact.EventId);
                else if (kind is "SortingAssignmentInTransit" or "SortingAssignmentOccupied" &&
                    Value(r, "assignment") is { } value && value.Deserialize<SortingAssignment>(Json) == reserved.Assignment)
                {
                    // A later saved fact is still history. It cannot reopen an
                    // operation that already lost its continuation qualification.
                    if (movements.GetValueOrDefault(fact.OperationId)?.State == "UnknownHeld") continue;
                    if (Text(r, "schemaVersion") == "sorting-evidence/1")
                    {
                        if (kind == "SortingAssignmentInTransit" && fact.EventType == StageEventType.Executing &&
                            movements.GetValueOrDefault(fact.OperationId)?.State == "Reserved" &&
                            Value(r, "evidence")?.Deserialize<PickCompletionEvidence>(Json) is { } pick &&
                            pick.Correlation.RunId == runId && pick.Correlation.OperationId == fact.OperationId &&
                            pick.Correlation.TrayId == trayId && pick.Correlation.PlanRevision == planRevision &&
                            pick.Correlation.ConnectionEpoch == fact.ConnectionEpoch &&
                            pick.Correlation.ObjectId == reserved.Assignment.ObjectId && pick.ObjectId == reserved.Assignment.ObjectId &&
                            pick.Correlation.PhysicalSlotIndex == reserved.Assignment.PhysicalSlotIndex &&
                            pick.PhysicalSlotIndex == reserved.Assignment.PhysicalSlotIndex &&
                            pick.ReservationReference == reserved.Assignment.ReservationReference &&
                            pick.AssignmentDigest == SortingTargetAllocator.AssignmentDigest(reserved.Assignment) &&
                            pick.SourcePositionReached.Correlation == pick.Correlation && pick.SourcePositionReached.Matched &&
                            pick.SourcePositionReached.Target == reserved.Assignment.SourcePoint)
                        {
                            committedPicks[fact.OperationId] = pick.Correlation;
                            SetMovement(reserved.Assignment, "InTransit", fact.EventId);
                        }
                        if (kind == "SortingAssignmentOccupied" && (fact.EventType == StageEventType.Completed ||
                            fact.EventType == StageEventType.Executing && Value(r,"scope") is {ValueKind:JsonValueKind.Object}) &&
                            movements.GetValueOrDefault(fact.OperationId)?.State == "InTransit" &&
                            Value(r, "evidence")?.Deserialize<DeviceActionEvidence>(Json) is { IsCorrelated: true,
                                Meaning: DeviceCompletionMeaning.MaterialTransferred } evidence &&
                            evidence.Correlation.RunId == runId && evidence.Correlation.OperationId == fact.OperationId &&
                            evidence.Correlation.ConnectionEpoch == fact.ConnectionEpoch &&
                            committedPicks.GetValueOrDefault(fact.OperationId) == evidence.Correlation &&
                            evidence.Positions.Any(x => x.Target == reserved.Assignment.SourcePoint && x.Matched) &&
                            evidence.Positions.Any(x => x.Target == reserved.Assignment.TargetPoint && x.Matched) &&
                            Value(r, "targetOccupied") is { ValueKind: JsonValueKind.True })
                        {
                            var purpose=Text(r,"transferPurpose");
                            if (Value(r,"scope") is {ValueKind:JsonValueKind.Object} && evidence.SafeReached is not {Matched:true}) continue;
                            SetMovement(reserved.Assignment, "Completed", fact.EventId);
                            movements[fact.OperationId]=movements[fact.OperationId] with {
                                TransferPurpose=purpose,
                                OriginalCellId=origins.GetValueOrDefault(reserved.Assignment.ObjectId), SafeConfirmed=evidence.SafeReached?.Matched };
                            if (purpose == "RotationLoading") states[reserved.Assignment.ObjectId]="OnRotationStation";
                        }
                    }
                    else if ((kind == "SortingAssignmentInTransit" && fact.EventType == StageEventType.Executing ||
                              kind == "SortingAssignmentOccupied" && fact.EventType == StageEventType.Completed) &&
                        Gaode.Infrastructure.Persistence.LegacyDispositionHistory.SortingState(fact.PayloadJson,
                        movements.GetValueOrDefault(fact.OperationId)?.State == "InTransit") is { } historicalState)
                        SetMovement(reserved.Assignment, historicalState, fact.EventId);
                }
            }
            if (fact.Stage == WholeTrayWorkflowStage.Detection && kind == "SpecialExitCompleted" &&
                Text(r, "objectId") is { } entityId && physical.Contains(entityId) &&
                Text(r, "sourceSlotId") is { Length: > 0 } source && Text(r, "targetPointRef") is { Length: > 0 } target &&
                Value(r, "result")?.Deserialize<AuxiliaryHandlingResult>(Json) is { OccupiedEntityId: null,
                    Evidence: { IsCorrelated: true, Meaning: DeviceCompletionMeaning.MaterialTransferred } } exit &&
                exit.Request.Correlation.RunId == runId && exit.Request.Correlation.OperationId == fact.OperationId &&
                exit.Evidence.Correlation == exit.Request.Correlation &&
                exit.Request.Correlation.ObjectId == entityId && exit.Request.Kind == AuxiliaryHandlingKind.Exit &&
                exit.Request.Correlation.PhysicalSlotIndex is > 0)
            {
                movements[fact.OperationId] = new(entityId, exit.Request.Correlation.PhysicalSlotIndex.Value, source, target,
                    "Completed", fact.OperationId, fact.EventId);
                states[entityId] = "Completed";
            }
        }
        return (results.Select(x => x with { DispositionState = physical.Contains(x.Id) &&
            x.PlanRevision == planRevision && x.Kind is "Single" or "Member" or "Assembly"
                ? states.GetValueOrDefault(x.Id) : null }).ToArray(), movements.Values.ToArray());

        void SetMovement(SortingAssignment a, string state, Guid eventId)
        {
            movements[a.OperationId] = new(a.ObjectId, a.PhysicalSlotIndex, a.SourcePoint.Id, a.TargetPoint.Id,
                state, a.OperationId, eventId) { IsPosePending=a.IsPosePending,DetectionState=a.DetectionState,
                    PhysicalDisposition=a.Disposition,Reason=a.Reason };
            states[a.ObjectId] = state;
        }
    }
    private static JsonElement? Value(JsonElement e, string name) => e.EnumerateObject()
        .Where(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
        .Select(p => (JsonElement?)p.Value).FirstOrDefault();
    private static string? Text(JsonElement e, string name) => Value(e, name) is { } v &&
        v.ValueKind != JsonValueKind.Null ? v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString() : null;
    private static int? Number(JsonElement e, string name) => int.TryParse(Text(e, name), out var n) ? n : null;
    private static Guid? GuidValue(JsonElement e, string name) => Guid.TryParse(Text(e, name), out var g) ? g : null;
}
