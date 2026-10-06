using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Application.Station01;
using Gaode.Application.Recipes;
using Gaode.Application.Workflow;
using Gaode.Contracts.Tests.Recipes;
using Gaode.Domain.Station01;
using Xunit;

namespace Gaode.Contracts.Tests.Station01;

public sealed class RuntimeObservationProjectionTests
{
    [Fact]
    public void AxisProjectionUsesCommittedActualValuesAndLeavesMissingAxesUnknown()
    {
        var run = Snapshot(); var now = DateTimeOffset.UtcNow;
        var identity = new ObservationIdentity(Guid.NewGuid(), 3, now, now, DeviceReliability.Reliable);
        var positionTime = now.AddMilliseconds(-300);
        var positionIdentity = new ObservationIdentity(Guid.NewGuid(), 3, positionTime.AddMilliseconds(-2), positionTime, DeviceReliability.Reliable);
        var origin = new ExecutionOrigin(DeviceProvider.Virtual, "axis-component/1", EvidenceQuality.Derived);
        var actual = new DeviceObservation(DeviceReliability.Reliable, DeviceConnection.Connected, 3,
            OperatingMode.Automatic, DeviceReadiness.Ready, SafetyAssessment.Clear, ClampState.Secured,
            MotionAvailability.Available, AcquisitionReadiness.Available, ManualAreaState.Clear,
            ManualHandlingState.Unconfirmed, new(12, 24, 48, "ScanZ", "component", "mm", positionIdentity, origin),
            null, [], [], origin, identity) { AxisPositions = new(12, 24, null, 48, null) };
        var committed = ObjectWrite(run.RunId, 2, WriteKind.ActionFact,
            new { kind = "ECodeMoveConfirmed", target = new { x = 900, y = 900, z = 900 }, observed = actual });
        var uncommitted = committed with { State = CommitState.Queued };
        Assert.Empty(RuntimeObservationProjection.Build(run, [uncommitted], []).AxisObservations);
        var otherRun = ObjectWrite(Guid.NewGuid(), 3, WriteKind.ActionFact,
            new { observed = actual with { AxisPositions = new(999, 999, 999, 999, 999) } });
        var result = RuntimeObservationProjection.Build(run, [committed, otherRun], []);
        var axes = result.AxisObservations.ToDictionary(a => a.Axis);
        Assert.Equal(12, axes["X"].Position);
        Assert.Equal(48, axes["ScanZ"].Position);
        Assert.Null(axes["CameraZ"].Position); Assert.Null(axes["GrabZ"].Position);
        Assert.All(axes.Values, a => { Assert.Equal(3, a.ConnectionEpoch); Assert.Equal(positionTime, a.ObservedAt);
            Assert.Equal($"write://{committed.WriteId:D}", a.EvidenceRef); Assert.Null(a.Unit); });
    }

    [Fact]
    public void MissingOrUncommittedObservationDoesNotBecomeNormalOrCreateRecipe()
    {
        var run = Snapshot();
        var fact = Observation(run.RunId, Guid.NewGuid(), 1,
            [new(3, TrayPresence.Present, TrayPose.Normal)]);
        var uncommitted = Write(run.RunId, 2, fact) with { State = CommitState.CommitUnknown };
        var result = RuntimeObservationProjection.Build(run, [uncommitted], []);
        Assert.Equal("NotObserved", result.ObservationCoverage.State);
        Assert.Null(result.AbnormalPhysicalSlotIndices);
        Assert.Empty(result.SlotStates);
        Assert.Null(result.RecipeExecution);
        Assert.Null(result.ExecutionPhase);
        var old = Fact(run.RunId, 3, new AlgorithmFactPayload(Guid.NewGuid(), AlgorithmState.Success, true,
            "{\"heightSamples\":[{\"height\":11}]}", "HistoricalHeight") {
                RunId = run.RunId, CaptureId = Guid.NewGuid(), Origin = fact.Source });
        Assert.Equal("NotObserved", RuntimeObservationProjection.Build(run, [old], []).ObservationCoverage.State);
    }

    [Fact]
    public void PhysicalSlotNumbersAndExclusionSurviveRecheckWithoutInventingMissingObservations()
    {
        var run = Snapshot(); var tray = Guid.NewGuid();
        var first = Observation(run.RunId, tray, 1, [new(1, TrayPresence.Present, TrayPose.Normal),
            new(3, TrayPresence.Present, TrayPose.Abnormal, "DeclaredTilt"), new(7, TrayPresence.Absent, TrayPose.Unknown)]);
        var initial = Write(run.RunId, 2, first);
        var before = RuntimeObservationProjection.Build(run, [initial], []);
        Assert.Equal(new[] { 1, 3, 7 }, before.SlotStates.Select(s => s.PhysicalSlotIndex));
        // Before matching/freeze there is no known configured slot set against which
        // to claim coverage. Observed abnormalities are still returned immediately.
        Assert.Equal("Partial", before.ObservationCoverage.State);
        Assert.Equal(new[] { 3 }, before.AbnormalPhysicalSlotIndices);
        var later = Observation(run.RunId, tray, 2,
            [new(3, TrayPresence.Present, TrayPose.Normal), new(7, TrayPresence.Absent, TrayPose.Unknown)]);
        var after = RuntimeObservationProjection.Build(run, [initial, Write(run.RunId, 3, later)], []);
        Assert.Equal("Partial", after.ObservationCoverage.State);
        Assert.Equal(new[] { 3 }, after.AbnormalPhysicalSlotIndices);
        Assert.Equal("Unknown", after.SlotStates.Single(s => s.PhysicalSlotIndex == 1).Presence);
        Assert.Equal("PoseExcluded", after.SlotStates.Single(s => s.PhysicalSlotIndex == 3).Participation);
        Assert.Equal("PoseRecheck", after.ExecutionPhase?.Kind);
        Assert.Equal(later.RelatedTransitionId, after.ExecutionPhase?.TransitionId);
        Assert.NotEqual(before.ObservationCoverage.LastObservationRef, after.ObservationCoverage.LastObservationRef);
    }

    [Fact]
    public void ActionIntentIsWaitingAndOnlyCommittedMatchingFactShowsCompletion()
    {
        var run = Snapshot(); var transition = Guid.NewGuid();
        var intent = ObjectWrite(run.RunId, 2, WriteKind.ActionIntent,
            new { kind = "FlipPick", transitionId = transition, sequence = 4 });
        var waiting = RuntimeObservationProjection.Build(run, [intent], []);
        Assert.Equal("Flip", waiting.ExecutionPhase?.Kind);
        Assert.Equal("Waiting", waiting.ExecutionPhase?.State);
        var completed = ObjectWrite(run.RunId, 3, WriteKind.ActionFact,
            new { kind = "FlipPickCompleted", transitionId = transition, sequence = 4 });
        var result = RuntimeObservationProjection.Build(run, [intent, completed], []);
        Assert.Equal("Completed", result.ExecutionPhase?.State);
        Assert.Equal(transition, result.ExecutionPhase?.TransitionId);
        Assert.Equal($"write://{completed.WriteId:D}", result.ExecutionPhase?.EvidenceRef);
        Assert.Equal("Waiting", RuntimeObservationProjection.Build(run, [intent, completed with { PayloadDigest = "wrong" }], []).ExecutionPhase?.State);
    }

    [Fact]
    public void FrozenIntentAloneCannotInventBoundRecipeAndConfirmedRunUsesFrozenValues()
    {
        var run = Snapshot(); var tray = Guid.NewGuid();
        var plan = Recipe011Data.Plan(tray);
        var frozen = new FrozenExecutionInputs(FrozenExecutionInputs.CurrentSchema, run.RunId, tray,
            RecipePlanRevision.Compute(plan), plan, new Dictionary<string, BoundCapability>(),
            new("component-cost", "1", "Test", "component", "component/1", "component-digest", 5000, 10000, 5000, 24100, 23000)
                { CaptureWaitMs = 8000, AlgorithmWaitMs = 15000, InputReleaseWaitMs = 2000 }, "");
        frozen = frozen with { SemanticDigest = frozen.ComputeDigest() };
        var intent = ObjectWrite(run.RunId, 2, WriteKind.ActionIntent, new { kind = "RecipePlanAndBindingIntent", frozenExecutionInputs = frozen });
        var observation = Write(run.RunId, 1, Observation(run.RunId, tray, 1, [new(1, TrayPresence.Present, TrayPose.Normal)]));
        Assert.Null(RuntimeObservationProjection.Build(run, [observation, intent], []).RecipeExecution);
        var bound = run with { RecipeExecution = new(plan.RecipeId, plan.RecipeVersion, plan.CatalogDigest,
            frozen.PlanRevision, plan.ScenarioId, "ordinaryBatch") };
        var result = RuntimeObservationProjection.Build(bound, [observation, intent], []);
        Assert.Equal(plan.Model, result.RecipeExecution?.Model);
        Assert.Equal(plan.DefinitionDigest, result.RecipeExecution?.DefinitionDigest);
        Assert.Equal(plan.RecipeVersion, result.RecipeExecution?.RecipeVersion);
        Assert.Contains(frozen.SemanticDigest, result.RecipeExecution!.SnapshotRef);
        Assert.Equal("Complete", result.ObservationCoverage.State);
        Assert.Empty(result.AbnormalPhysicalSlotIndices!);
    }

    [Theory]
    [InlineData(StageEventType.Completed, false, "Executing")]
    [InlineData(StageEventType.Completed, true, "Completed")]
    [InlineData(StageEventType.UnknownHeld, false, "UnknownHeld")]
    [InlineData(StageEventType.Failed, false, "Failed")]
    public void SortingStateUsesMatchingCommittedOperations(StageEventType outcome, bool allComplete, string expected)
    {
        var run = Snapshot(); var tray = Guid.NewGuid(); var plan = Recipe011Data.Plan(tray);
        var frozen = new FrozenExecutionInputs(FrozenExecutionInputs.CurrentSchema, run.RunId, tray,
            RecipePlanRevision.Compute(plan), plan, new Dictionary<string, BoundCapability>(),
            new("component-cost", "1", "Test", "component", "component/1", "component-digest", 5000, 10000, 5000, 24100, 23000)
                { CaptureWaitMs = 8000, AlgorithmWaitMs = 15000, InputReleaseWaitMs = 2000 }, "");
        frozen = frozen with { SemanticDigest = frozen.ComputeDigest() };
        var intent = ObjectWrite(run.RunId, 2, WriteKind.ActionIntent,
            new { kind = "RecipePlanAndBindingIntent", frozenExecutionInputs = frozen });
        var first = Guid.NewGuid(); var second = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        StageEvent Fact(Guid operation, int sequence, StageEventType type, object payload)
        {
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return new(Guid.NewGuid(), run.RunId, tray, "station", "line", WholeTrayWorkflowStage.Sorting,
                operation, 1, 1, type, now.AddSeconds(sequence), now.AddSeconds(sequence), ResultSource.Test,
                ResultQuality.Derived, null, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))),
                json, "sorting:" + sequence, sequence, now.AddYears(7), frozen.PlanRevision);
        }
        var reserved = Fact(Guid.NewGuid(), 1, StageEventType.IntentRecorded,
            new { kind = "SortingAssignmentsReserved", assignments = new[] { new { operationId = first }, new { operationId = second } } });
        var firstDone = Fact(first, 2, outcome, new { kind = "SortingAssignmentOccupied" });
        var secondDone = Fact(second, 3, StageEventType.Completed, new { kind = "SortingAssignmentOccupied" });
        Assert.Equal("NotStarted", RuntimeObservationProjection.Build(run, [intent],
            [firstDone with { RunId = Guid.NewGuid() }, secondDone with { PlanRevision = "other" }]).SortingState);
        Assert.Equal("NotStarted", RuntimeObservationProjection.Build(run, [intent],
            [firstDone with { PayloadDigest = "invalid" }]).SortingState);
        var facts = allComplete ? new[] { reserved, firstDone, secondDone } : new[] { reserved, firstDone };
        Assert.Equal(expected, RuntimeObservationProjection.Build(run, [intent], facts).SortingState);
        var noMove = Fact(Guid.NewGuid(), 1, StageEventType.Completed, new { kind = "NoAdditionalSortingRequired" });
        Assert.Equal("Completed", RuntimeObservationProjection.Build(run, [intent], [noMove]).SortingState);
    }

    [Fact]
    public void CommittedCanonicalHandoffAndFinalFactsRestoreHistoryWithoutLiveSnapshot()
    {
        var run = Snapshot() with { State = RunState.Completed, FinalOutcome = TerminalOutcome.Completed };
        var tray = Guid.NewGuid(); var plan = Recipe011Data.Plan(tray); var now = DateTimeOffset.UtcNow;
        var frozen = new FrozenExecutionInputs(FrozenExecutionInputs.CurrentSchema, run.RunId, tray,
            RecipePlanRevision.Compute(plan), plan, new Dictionary<string, BoundCapability>(),
            new("component-cost", "1", "Test", "component", "component/1", "component-digest", 5000, 10000, 5000, 24100, 23000)
                { CaptureWaitMs = 8000, AlgorithmWaitMs = 15000, InputReleaseWaitMs = 2000 }, "");
        frozen = frozen with { SemanticDigest = frozen.ComputeDigest() };
        var correlation = new ActionCorrelation(run.RunId, Guid.NewGuid(), Guid.NewGuid(), 1,
            Guid.NewGuid(), 1, "snapshot", frozen.PlanRevision, tray);
        var intent = ObjectWrite(run.RunId, 2, WriteKind.ActionIntent,
            new { kind = "RecipePlanAndBindingIntent", frozenExecutionInputs = frozen, bindingId = correlation.ActionId });
        var bound = ObjectWrite(run.RunId, 3, WriteKind.ActionFact, new { kind = "RecipePlanBound" });
        var identity = new WorkflowIdentity(run.RunId, tray, "station", "line", run.RequestId, plan.ScenarioId,
            ["s1"], now, "component", RunPurpose.Test, "public/1", "budget/1", "simulation/1");
        var handoff = new PublicPreparationHandoffV2(PublicPreparationHandoffV2.CurrentSchemaVersion,
            Guid.NewGuid(), identity, "points/1", "capabilities", ["3d-media"], ["f-media"], ["observation"],
            plan.FCode, "plan-reference", frozen.PlanRevision, $"recipe-binding://{correlation.ActionId:D}",
            ComponentEvidenceSource.Test, "Derived", ["write-evidence"], Guid.NewGuid(), 4, now, "");
        handoff = handoff with { PayloadDigest = PublicPreparationHandoffV2.ComputePayloadDigest(handoff) };
        Assert.True(handoff.IsComplete);
        var handoffWrite = new PersistedWrite(handoff.WriteId, run.RunId, 4, WriteKind.HandoffV2,
            JsonSerializer.Serialize(handoff, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            handoff.PayloadDigest, CommitState.Committed);
        RequiredCommitEvidence Commit(PersistedWrite write, string purpose, long tick) =>
            new(write.WriteId, correlation, ActualCommitState.Committed, ReceiptValidity.ValidCurrent,
                write.Revision, now, tick, null) { RecordKind = BusinessCommitRecordKind.RunWrite, SavePurpose = purpose };
        var receipt = new RecipeBindingReceipt(correlation, correlation.ActionId.ToString("D"), plan.RecipeId,
            plan.RecipeVersion, plan.DefinitionDigest, new("budget", "1", "1", "Test", "component", "digest", "snapshot", 100),
            new(10, 100, "component-clock", now, now.AddSeconds(1)),
            [Commit(bound, "RecipePlanBound", 30), Commit(handoffWrite, "Handoff", 40)], 40,
            ReceiptValidity.ValidCurrent, RecipeApplicationState.Completed) { IntentCommit = Commit(intent, "BindingIntent", 5) };
        Assert.True(receipt.WasCompletedInWindow);
        var audit = ObjectWrite(run.RunId, 5, WriteKind.Audit, new { kind = "RecipeApplicationReceiptObserved", receipt });
        var writes = new[] { intent, bound, handoffWrite, audit };
        StageEvent Event(WholeTrayWorkflowStage stage, StageEventType type, object payload, int sequence)
        {
            var value = ObjectWrite(run.RunId, sequence, WriteKind.ActionFact, payload);
            return new(Guid.NewGuid(), run.RunId, tray, "station", "line", stage, Guid.NewGuid(), 1, 1,
                type, now.AddSeconds(sequence), now.AddSeconds(sequence), ResultSource.Test, ResultQuality.Derived,
                null, value.PayloadDigest, value.PayloadJson, "history:" + sequence, sequence, now.AddYears(7), frozen.PlanRevision);
        }
        var allowed = Event(WholeTrayWorkflowStage.ManualTrayRemovalConfirmation, StageEventType.ManualRemovalAllowed,
            new { kind = "ManualRemovalAllowed" }, 1);
        var unload = Event(WholeTrayWorkflowStage.UnloadPreparation, StageEventType.Completed, new { kind = "UnloadPrepared" }, 0);
        var reference = new WholeTrayCompletionReference(Guid.NewGuid(), run.RunId, tray, Guid.NewGuid(), Guid.NewGuid(),
            frozen.PlanRevision, Guid.NewGuid(), Guid.NewGuid(), unload.EventId);
        var final = new FinalUnloadCompletion(Guid.NewGuid(), reference, allowed.EventId, "component", now,
            "ManualTrayRemovalConfirmation", true, "Actual committed test confirmation", Guid.NewGuid());
        var finalFact = Event(WholeTrayWorkflowStage.ManualTrayRemovalConfirmation, StageEventType.FinalUnloadCompleted,
            new { completion = final }, 4);
        var ready = Event(WholeTrayWorkflowStage.UnloadPreparation, StageEventType.WholeTrayCompleted,
            new { reference, sourceMatrixId = final.FinalSourceMatrixId }, 3);
        var restored = RuntimeObservationProjection.Build(run, writes, [unload, ready, allowed, finalFact]);
        Assert.NotNull(restored.RecipeExecution);
        Assert.Equal(plan.RecipeVersion, restored.RecipeExecution.Version);
        Assert.Equal("Bound", restored.RecipeState);
        Assert.Equal(HandoffState.Ready, restored.Handoff);
        Assert.Equal("FinalUnloadCompletion", restored.WholeTaskState);
        Assert.Equal(reference.CompletionId, restored.WholeTrayCompletionId);
        Assert.Equal(allowed.EventId, restored.ManualRemovalAllowedEventId);
        Assert.Empty(restored.AllowedActions);
        Assert.False(restored.AutomaticContinuationAllowed);
        Assert.Null(RuntimeObservationProjection.Build(run, [intent, bound, audit, handoffWrite with { PayloadDigest = "wrong" }], []).RecipeExecution);
        Assert.Null(RuntimeObservationProjection.Build(run, [intent, handoffWrite, audit], []).RecipeExecution);
        Assert.Equal("NotCompleted", RuntimeObservationProjection.Build(run, writes, [finalFact]).WholeTaskState);
        Assert.Equal("NotCompleted", RuntimeObservationProjection.Build(run, writes,
            [allowed, finalFact with { RunId = Guid.NewGuid() }]).WholeTaskState);
    }

    [Fact]
    public void CommittedPickPayloadDoesNotBecomeFullActionEvidenceOrFinishTransfer()
    {
        var run = Snapshot(); var tray = Guid.NewGuid(); var plan = Recipe011Data.Plan(tray);
        var frozen = new FrozenExecutionInputs(FrozenExecutionInputs.CurrentSchema, run.RunId, tray,
            RecipePlanRevision.Compute(plan), plan, new Dictionary<string, BoundCapability>(),
            new("component-cost", "1", "Test", "component", "component/1", "component-digest", 5000, 10000, 5000, 24100, 23000)
                { CaptureWaitMs = 8000, AlgorithmWaitMs = 15000, InputReleaseWaitMs = 2000 }, "");
        frozen = frozen with { SemanticDigest = frozen.ComputeDigest() };
        var bound = run with { RecipeExecution = new(plan.RecipeId, plan.RecipeVersion, plan.CatalogDigest,
            frozen.PlanRevision, plan.ScenarioId, "specialType1Part") };
        var intent = ObjectWrite(run.RunId, 2, WriteKind.ActionIntent,
            new { kind = "RecipePlanAndBindingIntent", frozenExecutionInputs = frozen });
        // Reproduce the durable production payload shape from the failed run: pick evidence
        // has no Observations collection and cannot be parsed as full DeviceActionEvidence.
        var payload = JsonSerializer.Serialize(new { schemaVersion = "sorting-evidence/1", kind = "SortingAssignmentInTransit",
            assignment = new { operationId = Guid.NewGuid(), sourceSlotId = "s1", objectId = "object" },
            evidence = new { correlation = new ActionCorrelation(run.RunId, Guid.NewGuid(), Guid.NewGuid(), 1,
                Guid.NewGuid(), 1, "snapshot", frozen.PlanRevision, tray), physicalPickState = "Observed" },
            sourceVacated = true, targetReserved = true, placeNotYetDispatched = true },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var now = DateTimeOffset.UtcNow;
        var fact = new StageEvent(Guid.NewGuid(), run.RunId, tray, "station", "line", WholeTrayWorkflowStage.Sorting,
            Guid.NewGuid(), 1, 1, StageEventType.Executing, now, now, ResultSource.Test, ResultQuality.Derived, null,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))), payload, "pick-shape", 1, now.AddYears(7), frozen.PlanRevision);
        var startPayload = JsonSerializer.Serialize(new { schemaVersion = "stage-action/1", stage = "TransferToRotation",
            transferPurpose = "RotationLoading", scope = new DetectionExecutionScope($"{tray}/s1", "s1") },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var started = fact with { EventId = Guid.NewGuid(), EventType = StageEventType.Started, Sequence = 0,
            OccurredAt = now.AddSeconds(-1), PersistedAt = now.AddSeconds(-1), PayloadJson = startPayload,
            PayloadDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(startPayload))) };
        var result = RuntimeObservationProjection.Build(bound, [intent], [started, fact]);
        Assert.Equal("TransferToRotation", result.RecipeExecution?.ExecutionPhase?.Kind);
        Assert.Equal("Running", result.RecipeExecution?.ExecutionPhase?.State);
        Assert.NotEqual("Completed", result.SortingState);
        Assert.Null(result.WholeTrayCompletionId);
        var incomplete = JsonSerializer.Serialize(new { kind = "SortingAssignmentOccupied" }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var premature = fact with { EventType = StageEventType.Completed, PayloadJson = incomplete,
            PayloadDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(incomplete))) };
        Assert.Equal("Unconfirmed", RuntimeObservationProjection.Build(bound, [intent], [premature]).RecipeExecution?.ExecutionPhase?.State);
    }

    private static RunSnapshot Snapshot() => new(Guid.NewGuid(), "projection-component", "component", RunState.Running3D,
        1, 1, TerminalOutcome.None, false, ActionState.NotRequested, CaptureState.NotRequested,
        AlgorithmState.NotRequested, SaveState.NotQueued, HandoffState.NotReady, null, null, null, []);
    private static TrayObservation Observation(Guid run, Guid tray, int round, TraySlotObservation[] slots) =>
        new(Guid.NewGuid(), run, tray, Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow,
            round == 1 ? TrayObservationPurpose.InitialPreparation : TrayObservationPurpose.PostPlacementCheck, round,
            round == 1 ? null : Guid.NewGuid(), slots, round == 1 ? new(12, 34, "mm", "component", "DeclaredComponent") : null,
            new(ComponentEvidenceSource.Test, "component-observer/1", "DeclaredComponent"), ["DeclaredComponent"]);
    private static PersistedWrite Write(Guid run, long revision, TrayObservation observation) => Fact(run, revision,
        new AlgorithmFactPayload(observation.CallId, AlgorithmState.Success, true, JsonSerializer.Serialize(observation), "Observed") {
            RunId = run, CaptureId = observation.CaptureId, Origin = observation.Source });
    private static PersistedWrite Fact(Guid run, long revision, AlgorithmFactPayload payload) => ObjectWrite(run, revision, WriteKind.AlgorithmFact, payload);
    private static PersistedWrite ObjectWrite(Guid run, long revision, WriteKind kind, object value)
    {
        var json = JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return new(Guid.NewGuid(), run, revision, kind, json,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))), CommitState.Committed);
    }
}
