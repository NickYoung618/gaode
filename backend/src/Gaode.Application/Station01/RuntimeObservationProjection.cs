using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Application.Workflow;
using Gaode.Domain.Station01;

namespace Gaode.Application.Station01;

/// <summary>Read-only projection of committed run facts; never reads the active recipe catalog.</summary>
public static class RuntimeObservationProjection
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public static async Task<RunSnapshot> ReadAsync(RunSnapshot snapshot, ITraceQuery traces,
        IStageEventStore stages, CancellationToken token)
    {
        var persisted = await traces.GetRunAsync(snapshot.RunId, token);
        var writes = await traces.GetWritesAsync(snapshot.RunId, token);
        Guid? tray = snapshot.Identity?.TrayId;
        if (tray is null && persisted is not null)
        {
            using var context = JsonDocument.Parse(persisted.ContextJson);
            if (Field(context.RootElement, "trayId") is { ValueKind: JsonValueKind.String } value && value.TryGetGuid(out var id)) tray = id;
        }
        var facts = new List<StageEvent>();
        if (tray is { } trayId)
            foreach (var stage in Enum.GetValues<WholeTrayWorkflowStage>())
                facts.AddRange(await stages.ReadAsync(snapshot.RunId, trayId, stage, token));
        var revision = Math.Max(persisted?.Revision ?? snapshot.PersistedRevision,
            writes.Where(w => w.RunId == snapshot.RunId && w.State == CommitState.Committed)
                .Select(w => w.Revision).DefaultIfEmpty(0).Max());
        return Build(snapshot with { PersistedRevision = revision }, writes, facts);
    }

    public static RunSnapshot Build(RunSnapshot snapshot, IReadOnlyList<PersistedWrite> writes,
        IReadOnlyList<StageEvent> stageEvents)
    {
        var committed = writes.Where(w => w.RunId == snapshot.RunId && VerifiedWrite(w))
            .OrderBy(w => w.Revision).ToArray();
        FrozenExecutionInputs? inputs = null;
        RecipeBindingReceipt? receipt = null;
        PublicPreparationHandoffV2? handoff = null;
        var observations = new List<(TrayObservation Value, string Reference)>();
        ExecutionPhaseProjection? phase = null;
        var axes = new Dictionary<string, AxisObservationProjection>(StringComparer.Ordinal);
        string? publicExecutionRevision = null;
        TrayAnomalyDecisionProjection? decision = snapshot.TrayAnomalyDecision;
        foreach (var write in committed)
        {
            using var doc = JsonDocument.Parse(write.PayloadJson);
            var root = doc.RootElement;
            var kind = Text(root, "kind");
            if (kind == "FrozenPublicConfiguration" && Text(root, "snapshotId") is { } snapshotId)
                publicExecutionRevision = "public-snapshot:" + snapshotId;
            if (kind == "TrayAnomalyDecision" && Field(root, "decision") is { ValueKind: JsonValueKind.Object } decisionJson &&
                decisionJson.Deserialize<TrayAnomalyDecisionProjection>(Json) is { } actualDecision && actualDecision.RunId == snapshot.RunId)
                decision = actualDecision with { EvidenceReference = $"write://{write.WriteId:D}" };
            if (write.Kind == WriteKind.HandoffV2)
                handoff = root.Deserialize<PublicPreparationHandoffV2>(Json);
            if (write.Kind == WriteKind.ActionFact)
            {
                var reference = $"write://{write.WriteId:D}";
                if (Field(root, "observed") is { ValueKind: JsonValueKind.Object } observed &&
                    Field(observed, "axisPositions") is { ValueKind: JsonValueKind.Object } &&
                    observed.Deserialize<DeviceObservation>(Json) is { ExecutionOrigin.Provider: not DeviceProvider.Unavailable,
                        ExecutionOrigin.Quality: not EvidenceQuality.Unknown } device)
                    foreach (var axis in AxisObservationProjectionBuilder.From(device, reference)) axes[axis.Axis] = axis;
                foreach (var name in new[] { "positionEvidence", "evidence" })
                    if (Field(root, name) is { ValueKind: JsonValueKind.Object } raw &&
                        Field(raw, "positions") is { ValueKind: JsonValueKind.Array } &&
                        raw.Deserialize<DeviceActionEvidence>(Json) is { IsCorrelated: true } action &&
                        action.Correlation.RunId == snapshot.RunId)
                        foreach (var position in action.Positions.Where(p => p.Correlation == action.Correlation))
                            foreach (var axis in AxisObservationProjectionBuilder.From(position, reference)) axes[axis.Axis] = axis;
            }
            if (kind == "RecipePlanAndBindingIntent" && Field(root, "frozenExecutionInputs") is { ValueKind: JsonValueKind.Object } frozen)
            {
                var candidate = Text(frozen, "schemaVersion") is FrozenExecutionInputs.CurrentSchema or FrozenExecutionInputs.HistoricalSchema
                    ? frozen.Deserialize<FrozenExecutionInputs>(Json) : null;
                if (candidate is { IsValid: true } && candidate.RunId == snapshot.RunId) inputs = candidate;
            }
            if (kind == "RecipeApplicationReceiptObserved" && Field(root, "receipt") is { ValueKind: JsonValueKind.Object } binding &&
                Text(binding, "definitionDigest") is not null)
                receipt = binding.Deserialize<RecipeBindingReceipt>(Json);
            if (write.Kind == WriteKind.AlgorithmFact)
            {
                var fact = root.Deserialize<AlgorithmFactPayload>(Json);
                if (fact is { State: AlgorithmState.Success } && fact.RunId == snapshot.RunId && fact.Origin.IsKnown)
                {
                    using var raw = JsonDocument.Parse(fact.RawResultJson);
                    if (Field(raw.RootElement, "observationId") is not null)
                    {
                        var observed = raw.RootElement.Deserialize<TrayObservation>(Json);
                        if (observed is { IsValid: true } && observed.RunId == snapshot.RunId &&
                            observed.CallId == fact.CallId && observed.CaptureId == fact.CaptureId && observed.Source == fact.Origin &&
                            (observations.Count == 0 ? observed.Purpose == TrayObservationPurpose.InitialPreparation :
                                observed.TrayId == observations[0].Value.TrayId && observed.CheckRound > observations[^1].Value.CheckRound))
                        {
                            var reference = $"write://{write.WriteId:D}";
                            observations.Add((observed, reference));
                            phase = new(observed.Purpose == TrayObservationPurpose.InitialPreparation ? "Initial3D" : "PoseRecheck",
                                "Completed", TransitionId: observed.RelatedTransitionId, ObservationRef: reference, EvidenceRef: reference);
                        }
                    }
                }
            }
            if (WritePhase(write, root, kind, inputs?.Plan) is { } next) phase = next;
        }
        var plan = inputs?.Plan;
        if (inputs is not null) observations.RemoveAll(o => o.Value.TrayId != inputs.TrayId);
        var bindingConfirmed = inputs is not null && (snapshot.RecipeExecution is { } current &&
            current.PlanRevision == inputs.PlanRevision && current.RecipeId == plan!.RecipeId && current.Version == plan.RecipeVersion ||
            receipt is { WasCompletedInWindow: true } && receipt.Correlation.RunId == snapshot.RunId &&
            receipt.Correlation.TrayId == inputs.TrayId && receipt.Correlation.PlanRevision == inputs.PlanRevision &&
            receipt.RecipeId == plan!.RecipeId && receipt.RecipeVersion == plan.RecipeVersion && receipt.DefinitionDigest == plan.DefinitionDigest &&
            receipt.IntentCommit is { } intent && committed.Any(w => w.WriteId == intent.WriteId) &&
            receipt.RequiredCommits.All(c => committed.Any(w => w.WriteId == c.WriteId && w.Revision == c.PersistedRevision)));
        var actualTrayId = inputs?.TrayId ?? observations.FirstOrDefault().Value?.TrayId;
        var actualRevision = inputs?.PlanRevision ?? publicExecutionRevision;
        var facts = stageEvents.Where(e => e.RunId == snapshot.RunId && e.TrayId == actualTrayId &&
            e.PlanRevision == actualRevision && e.PayloadDigest == Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(e.PayloadJson))))
            .OrderBy(e => e.PersistedAt).ThenBy(e => e.Sequence).ToArray();
        foreach (var fact in facts)
        {
            using var payload = JsonDocument.Parse(fact.PayloadJson);
            if (Text(payload.RootElement, "kind") == "TrayAnomalyDecision" &&
                Field(payload.RootElement, "decision") is { ValueKind: JsonValueKind.Object } rawDecision &&
                rawDecision.Deserialize<TrayAnomalyDecisionProjection>(Json) is { } actualDecision && actualDecision.RunId == snapshot.RunId)
                decision = actualDecision with { EvidenceReference = $"stage-event://{fact.EventId:D}" };
        }
        var stage = facts.LastOrDefault()?.Stage.ToString();
        if (facts.LastOrDefault() is { } lastStage)
        {
            using var currentPayload=JsonDocument.Parse(lastStage.PayloadJson);
            var currentRoot=currentPayload.RootElement;
            var currentKind=Text(currentRoot,"kind");
            var relatedStart = facts.LastOrDefault(e => e.OperationId == lastStage.OperationId &&
                e.ConnectionEpoch == lastStage.ConnectionEpoch && e.EventType == StageEventType.Started);
            using var relatedPayload = relatedStart is null ? null : JsonDocument.Parse(relatedStart.PayloadJson);
            JsonElement? requestRoot = relatedPayload?.RootElement is { } requestValue &&
                Text(requestValue, "schemaVersion") == "stage-action/1" ? requestValue : null;
            var scopeSource = Field(currentRoot, "scope") ?? (requestRoot is { } request ? Field(request, "scope") : null);
            var actualScope=scopeSource is {ValueKind:JsonValueKind.Object} scopeValue
                ? scopeValue.Deserialize<DetectionExecutionScope>(Json):null;
            var transferPurpose = Text(currentRoot, "transferPurpose") ?? (requestRoot is { } purposeRequest ? Text(purposeRequest, "transferPurpose") : null);
            var actionStage = Text(currentRoot, "stage") ?? (requestRoot is { } stageRequest ? Text(stageRequest, "stage") : null);
            var sequence=Field(currentRoot,"sequence") is {ValueKind:JsonValueKind.Number} sequenceValue?sequenceValue.GetInt32():(int?)null;
            var actualStep=sequence is null?null:plan?.Steps.SingleOrDefault(s=>s.Sequence==sequence);
            var original=actualScope?.SlotId is { } scopedSlot?plan?.OriginalSlots?.GetValueOrDefault(scopedSlot):
                actualStep?.SlotId is { } stepSlot?plan?.OriginalSlots?.GetValueOrDefault(stepSlot):null;
            if (original is null && Field(currentRoot, "assignment") is { ValueKind: JsonValueKind.Object } assignment &&
                Text(assignment, "sourceSlotId") is { } sourceSlot &&
                plan?.OriginalSlots?.GetValueOrDefault(sourceSlot) is { } assignedOrigin &&
                assignedOrigin.EntityId == Text(assignment, "objectId")) original = assignedOrigin;
            var phaseKind = lastStage.Stage switch {
                WholeTrayWorkflowStage.Sorting => "Sorting", WholeTrayWorkflowStage.UnloadPreparation => "Unload",
                WholeTrayWorkflowStage.ManualTrayRemovalConfirmation => lastStage.EventType == StageEventType.FinalUnloadCompleted ? "FinalSave" : "ManualRemoval",
                WholeTrayWorkflowStage.ManualRemovalAdmission => "ManualRemoval",
                WholeTrayWorkflowStage.Detection when currentKind is "RotationIntent" or "RotationReached"=>"Rotation",
                _ => null };
            if(transferPurpose=="RotationLoading"||actionStage=="TransferToRotation")phaseKind="TransferToRotation";
            else if(transferPurpose=="ReturnToOrigin")phaseKind="ReturnToOrigin";
            if (phaseKind is not null)
            {
                // InTransit contains PickCompletionEvidence, not a full action completion.
                var actualEvidence=currentKind is "SortingAssignmentOccupied" or "RotationReached" &&
                    Field(currentRoot,"evidence") is {ValueKind:JsonValueKind.Object} evidenceValue
                    ? evidenceValue.Deserialize<DeviceActionEvidence>(Json):null;
                var actionCompleted=actualEvidence is {IsCorrelated:true} && actualEvidence.Correlation.RunId==snapshot.RunId&&
                    actualEvidence.Correlation.PlanRevision==actualRevision&&
                    (currentKind=="SortingAssignmentOccupied"&&actualEvidence.SafeReached is {Matched:true}||
                     currentKind=="RotationReached"&&actualEvidence.AngleReached is {Matched:true});
                var physicalCompletionKind = currentKind is "SortingAssignmentOccupied" or "RotationReached";
                var phaseState = actionCompleted ? "Completed" :
                    physicalCompletionKind && lastStage.EventType == StageEventType.Completed && inputs?.SchemaVersion == FrozenExecutionInputs.CurrentSchema
                        ? "Unconfirmed" : StageState(lastStage.EventType);
                phase = new(phaseKind,phaseState,sequence,
                    EntityId:original?.EntityId??actualStep?.PhysicalEntityId,PhysicalSlotIndex:original?.PhysicalSlotIndex??actualStep?.PhysicalSlotIndex,
                    EvidenceRef: $"stage-event://{lastStage.EventId:D}") {CellId=original?.CellId,StageId=actualStep?.StageId};
            }
        }
        if (phase is not null && snapshot.State is RunState.Cancelled or RunState.CancelRequested or RunState.Restricted or RunState.RecoveryRequired)
            phase = phase with { State = snapshot.State switch { RunState.Cancelled => "Cancelled", RunState.CancelRequested => "Restricted",
                RunState.RecoveryRequired => "UnknownHeld", _ => "Restricted" } };
        IReadOnlyDictionary<int, SlotParticipation> participation = new Dictionary<int, SlotParticipation>();
        var lastSlots = new Dictionary<int, (TraySlotObservation Value, string Reference)>();
        foreach (var (observation, reference) in observations)
        {
            participation = SlotParticipation.Apply(participation.Values, observation.Slots);
            foreach (var key in lastSlots.Keys.ToArray())
                if (!observation.Slots.Any(s => s.PhysicalSlotIndex == key)) lastSlots.Remove(key);
            foreach (var slot in observation.Slots) lastSlots[slot.PhysicalSlotIndex] = (slot, reference);
        }
        var requiredSlots = plan?.ExecutionPositions.Values.SelectMany(s => s.PhysicalEntity.Coordinates
            .Concat(s.Members.Values.SelectMany(m => m.Coordinates))).Select(c => c.PhysicalSlotIndex).OfType<int>().Distinct().ToArray() ?? [];
        var expectedSlots = requiredSlots.Concat(observations.FirstOrDefault().Value?.Slots.Select(s => s.PhysicalSlotIndex) ?? []).Distinct().ToArray();
        var coverage = observations.Count == 0 ? "NotObserved" : (observations[^1].Value.HasCompleteCoverage || requiredSlots.Length > 0) && expectedSlots.All(i => lastSlots.TryGetValue(i, out var s) &&
            s.Value.Presence != TrayPresence.Unknown && (s.Value.Presence == TrayPresence.Absent || s.Value.Pose != TrayPose.Unknown)) ? "Complete" : "Partial";
        var slots = expectedSlots.Concat(participation.Keys).Distinct().Order().Select(index => {
            lastSlots.TryGetValue(index, out var observed);
            participation.TryGetValue(index, out var state);
            var movement = snapshot.Movements.LastOrDefault(m => m.PhysicalSlotIndex == index && m.IsPosePending);
            var entityIds = EntityIds(plan,index);
            var hasPriorFacts = snapshot.Results.Any(r => entityIds.Contains(r.Id) || r.ParentId is not null && entityIds.Contains(r.ParentId));
            return new SlotStateProjection(index, observed.Value?.Presence.ToString() ?? "Unknown", observed.Value?.Pose.ToString() ?? "Unknown",
                state?.State.ToString() ?? "Unknown", observed.Reference, EntityIds(plan, index),
                state?.Reason is { Length: > 0 } reason ? [reason] : []) {
                    DetectionState=movement?.DetectionState ?? (state?.State == SlotParticipationState.PoseExcluded
                        ? hasPriorFacts ? "FurtherInspectionTerminated" : "NotInspected" : observed.Value?.Presence == TrayPresence.Absent ? "NoMaterial" : null),
                    PhysicalDisposition=movement is null ? null : movement.State == "Completed" ? movement.PhysicalDisposition : "Pending:"+movement.State,
                    SortingEvidenceRef=movement is null ? null : $"stage-event://{movement.CommittedEventId:D}",
                    CellId=observed.Value?.CellId,
                    Region=observed.Value?.Region,
                    Row=observed.Value?.Row,
                    Column=observed.Value?.Column,
                    RegionOrdinal=plan?.TrayLayout?.Ordered(RecipeTrayRegion.OK).Select((c,i)=>(c.CellId,Ordinal:i+1))
                        .Where(c=>c.CellId==plan.TraySlotMapping?.Bindings.SingleOrDefault(b=>b.PhysicalSlotIndex==index)?.CellId).Select(c=>(int?)c.Ordinal).SingleOrDefault() };
        }).ToArray();
        FinalUnloadCompletion? final = null;
        WholeTrayCompletionReference? ready = null;
        Guid? readyMatrixId = null;
        foreach (var fact in facts)
        {
            using var payload = JsonDocument.Parse(fact.PayloadJson);
            if (fact.EventType == StageEventType.WholeTrayCompleted &&
                Field(payload.RootElement, "reference") is { ValueKind: JsonValueKind.Object } reference &&
                reference.Deserialize<WholeTrayCompletionReference>(Json) is { IsValid: true } whole &&
                whole.RunId == snapshot.RunId && whole.TrayId == actualTrayId && whole.PlanRevision == actualRevision &&
                (whole.EndReason != TrayEndReason.NormalCompletion || bindingConfirmed) &&
                facts.Any(f => f.EventId == whole.UnloadPreparationCompletedEventId && f.EventType == StageEventType.Completed) &&
                Field(payload.RootElement, "sourceMatrixId") is { ValueKind: JsonValueKind.String } matrix && matrix.TryGetGuid(out var matrixId))
            { readyMatrixId = matrixId; ready = whole; }
            if (fact.Stage == WholeTrayWorkflowStage.ManualTrayRemovalConfirmation &&
                fact.EventType == StageEventType.FinalUnloadCompleted &&
                Field(payload.RootElement, "completion") is { ValueKind: JsonValueKind.Object } raw &&
                raw.Deserialize<FinalUnloadCompletion>(Json) is { IsValid: true } completed &&
                completed.WholeTray.RunId == snapshot.RunId && completed.WholeTray.TrayId == actualTrayId &&
                completed.WholeTray.PlanRevision == actualRevision && ready?.CompletionId == completed.WholeTray.CompletionId &&
                facts.Any(f => f.EventId == completed.ManualRemovalAllowedEventId && f.EventType == StageEventType.ManualRemovalAllowed))
                final = completed;
        }
        var verifiedHandoff = bindingConfirmed && handoff is not null && handoff.Identity.TrayId == inputs!.TrayId &&
            handoff.PlanRevision == inputs.PlanRevision && handoff.RecipeBindingReference == $"recipe-binding://{receipt?.BindingId}"
                ? handoff : null;
        var execution = bindingConfirmed ? new RecipeExecutionProjection(plan!.RecipeId, plan.RecipeVersion, plan.CatalogDigest,
            inputs!.PlanRevision, plan.ScenarioId, snapshot.RecipeExecution?.Route) {
                DefinitionDigest = plan.DefinitionDigest, Model = plan.Model, FCode = plan.FCode,
                SnapshotRef = $"execution-inputs://{snapshot.RunId:D}/{inputs.SemanticDigest}", Stage = stage, ExecutionPhase = phase } : null;
        var results = snapshot.Results.Select(r => {
            var slot = slots.FirstOrDefault(s => s.EntityRefs.Contains(r.Id, StringComparer.Ordinal) || r.ParentId is not null && s.EntityRefs.Contains(r.ParentId, StringComparer.Ordinal));
            return r with { PhysicalSlotIndex = slot?.PhysicalSlotIndex, PoseState = slot?.PoseState, Participation = slot?.Participation };
        }).ToArray();
        return snapshot with { RecipeExecution = execution, ExecutionPhase = execution is null ? phase : null,
            RecipeState = bindingConfirmed ? "Bound" : snapshot.RecipeState,
            Identity = verifiedHandoff?.Identity ?? snapshot.Identity,
            PublicVersion = verifiedHandoff?.Identity.PublicConfigRevision ?? snapshot.PublicVersion,
            BudgetVersion = verifiedHandoff?.Identity.BudgetRevision ?? snapshot.BudgetVersion,
            SimulationVersion = verifiedHandoff?.Identity.SimulationConfigRevision ?? snapshot.SimulationVersion,
            Handoff = verifiedHandoff is null ? snapshot.Handoff : HandoffState.Ready,
            HandoffId = verifiedHandoff?.HandoffId ?? snapshot.HandoffId,
            PlanRevision = bindingConfirmed ? inputs!.PlanRevision : snapshot.PlanRevision,
            RecipeBindingReference = verifiedHandoff?.RecipeBindingReference ?? snapshot.RecipeBindingReference,
            WholeTaskState = final is null ? snapshot.WholeTaskState : "FinalUnloadCompletion",
            WholeTrayCompletionId = final?.WholeTray.CompletionId ?? ready?.CompletionId ?? snapshot.WholeTrayCompletionId,
            TrayEndReason = final?.WholeTray.EndReason.ToString() ?? ready?.EndReason.ToString() ?? snapshot.TrayEndReason,
            InspectionCompleted = final?.WholeTray.InspectionCompleted ?? ready?.InspectionCompleted ?? snapshot.InspectionCompleted,
            TrayAnomalyDecision = decision,
            ReadyForRemovalSourceMatrixId = readyMatrixId ?? snapshot.ReadyForRemovalSourceMatrixId,
            ManualRemovalAllowedEventId = final?.ManualRemovalAllowedEventId ?? snapshot.ManualRemovalAllowedEventId,
            SlotStates = slots, AbnormalPhysicalSlotIndices = observations.Count == 0 ? null :
                participation.Values.Where(s => s.State == SlotParticipationState.PoseExcluded).Select(s => s.PhysicalSlotIndex).Order().ToArray(),
            ObservationCoverage = new(coverage, observations.LastOrDefault().Reference), Results = results,
            SortingState = SortingState(facts, snapshot.SortingState),
            AxisObservations = axes.Values.OrderBy(a => a.Axis, StringComparer.Ordinal).ToArray(),
            DeviceSchemaVersion = "device-semantics/1.2", ResultSchemaVersion = "station01-result-display/1.1" };
    }

    private static bool VerifiedWrite(PersistedWrite write)
    {
        if (write.State != CommitState.Committed) return false;
        // HandoffV2 uses its existing canonical digest (empty digest field), not
        // the byte digest used by ordinary Writes. Both still require validation.
        if (write.Kind == WriteKind.HandoffV2)
        {
            var value = JsonSerializer.Deserialize<PublicPreparationHandoffV2>(write.PayloadJson, Json);
            return value is not null && value.Identity.RunId == write.RunId &&
                new CommittedPublicPreparationHandoffV2(value, value.HandoffId, write.WriteId,
                    write.Revision, write.PayloadDigest).IsVerified;
        }
        return write.PayloadDigest == Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(write.PayloadJson)));
    }

    private static string SortingState(IReadOnlyList<StageEvent> facts, string previous)
    {
        var sorting = facts.Where(f => f.Stage == WholeTrayWorkflowStage.Sorting).OrderBy(f => f.Sequence).ToArray();
        if (sorting.Length == 0) return previous;
        var projected = sorting.Aggregate(StageEventProjection.Initial(sorting[0]), StageEventProjection.Apply);
        var required = new HashSet<Guid>();
        foreach (var fact in sorting.Where(f => f.EventType == StageEventType.IntentRecorded))
        {
            using var doc = JsonDocument.Parse(fact.PayloadJson);
            if (Text(doc.RootElement, "kind") == "SortingAssignmentsReserved" &&
                Field(doc.RootElement, "assignments") is { ValueKind: JsonValueKind.Array } assignments)
                foreach (var item in assignments.EnumerateArray()) required.Add(item.GetProperty("operationId").GetGuid());
        }
        if (projected.Status == StageProjectionStatus.Completed && required.Any(operation =>
                !sorting.Any(f => f.OperationId == operation && (f.EventType == StageEventType.Completed ||
                    f.EventType == StageEventType.Executing && HasPayloadKind(f.PayloadJson,"SortingAssignmentOccupied")))))
            return StageProjectionStatus.Executing.ToString();
        return projected.Status == StageProjectionStatus.NotStarted
            ? StageProjectionStatus.Started.ToString() : projected.Status.ToString();
    }

    private static bool HasPayloadKind(string payload,string kind)
    {
        using var doc=JsonDocument.Parse(payload);
        return Text(doc.RootElement,"kind")==kind;
    }
    private static IReadOnlyList<string> EntityIds(RecipeRunPlan? plan, int index) => plan?.Steps.Where(s => s.PhysicalSlotIndex == index)
        .SelectMany(s => plan.UnitKind == "looseGroup" ? new[] { s.PhysicalEntityId, s.MemberId } : new[] { s.PhysicalEntityId, s.MemberId, s.UnitId })
        .OfType<string>().Where(s => s.Length > 0).Distinct(StringComparer.Ordinal).ToArray() ?? [];
    private static string StageState(StageEventType value) => value switch {
        StageEventType.Completed or StageEventType.FinalUnloadCompleted or StageEventType.ManualRemovalAllowed => "Completed",
        StageEventType.Failed or StageEventType.TimedOut => "Failed", StageEventType.UnknownHeld or StageEventType.Disconnected => "UnknownHeld",
        StageEventType.IntentRecorded => "Waiting", _ => "Running" };
    private static ExecutionPhaseProjection? WritePhase(PersistedWrite write, JsonElement root, string? kind, RecipeRunPlan? plan)
    {
        var phase = kind switch {
            "3D" or "Capture3D" => "Initial3D", "F" or "CaptureF" or "FCode" => "FScan",
            "RecipePlanAndBindingIntent" or "RecipePlanBound" => "RecipeBinding",
            "Detection" or "DetectionMoveConfirmed" or "DetectionCapture" => "InspectFace",
            "RotationIntent" or "RotationReached" => "Rotation",
            "FlipPick" => Field(root, "transitionId") is null ? "PositionForFlip" : "Flip",
            "FlipPutBack" => Field(root, "transitionId") is null ? "PositionForPutBack" : "PutBack",
            "FlipPickCompleted" => "Flip", "FlipPutBackCompleted" => "PutBack",
            "ThreeDRescan" or "PostPlacement3D" => "PoseRecheck", "ECode" or "CaptureE" or "ECodeBinding" => "EntityCode",
            _ => null };
        if (phase is null) return null;
        var sequence = Field(root, "sequence") is { ValueKind: JsonValueKind.Number } n ? n.GetInt32() : (int?)null;
        var step = plan?.Steps.SingleOrDefault(s => s.Sequence == sequence);
        var state = write.Kind is WriteKind.ActionIntent or WriteKind.CaptureIntent ? "Waiting" :
            kind is "FlipPickCompleted" or "FlipPutBackCompleted" or "ECodeBinding" ? "Completed" : "Running";
        if (kind == "ECodeBinding" && Text(root, "issue") is not null) state = "Failed";
        return new(phase, state, sequence, Field(root, "transitionId") is { ValueKind: JsonValueKind.String } t && t.TryGetGuid(out var id) ? id : null,
            step?.PhysicalEntityId, step?.PhysicalSlotIndex, step?.LocalFace, step?.ScanPoseId, EvidenceRef: $"write://{write.WriteId:D}") {StageId=step?.StageId,CellId=step?.SlotId is { } slotId ? plan?.OriginalSlots?.GetValueOrDefault(slotId)?.CellId ?? plan?.TraySlotMapping?.Bindings.SingleOrDefault(b=>b.PhysicalSlotIndex==step.PhysicalSlotIndex)?.CellId : null};
    }
    private static JsonElement? Field(JsonElement root, string key) => root.ValueKind == JsonValueKind.Object ? root.EnumerateObject()
        .Where(p => p.Name.Equals(key, StringComparison.OrdinalIgnoreCase)).Select(p => (JsonElement?)p.Value).FirstOrDefault() : null;
    private static string? Text(JsonElement root, string key) => Field(root, key) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;
}
