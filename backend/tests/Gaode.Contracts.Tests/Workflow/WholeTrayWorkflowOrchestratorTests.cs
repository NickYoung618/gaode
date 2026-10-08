using Gaode.Application.Ports;
using Gaode.Application.Motion;
using Gaode.Application.Recipes;
using Gaode.Application.Workflow;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Microsoft.Extensions.Time.Testing;
using Gaode.Contracts.Tests.Support;
using System.Text.Json;
using Xunit;

namespace Gaode.Contracts.Tests.Workflow;

[Trait("EvidenceLevel", "UpperIsolation")]
public sealed class WholeTrayWorkflowOrchestratorTests
{
    [Fact]
    public async Task DeviceVersionComesFromActualSemanticProducerNotCurrentProtocolDefinition()
    {
        var f = Fixture();
        var ready = await f.Orchestrator.ExecuteThreeStagesAsync(f.Request);
        var device = Assert.Single(ready.WholeTray!.SourceMatrix.Components, e => e.Component == ComponentKind.Plc);
        Assert.Equal(SemanticStageFixture.Origin.ComponentVersion, device.VersionRef);
    }

    [Fact]
    public async Task TestPurposeDoesNotReplaceEachActualProducerWithSimulatedSource()
    {
        var f = Fixture();
        var ready = await f.Orchestrator.ExecuteThreeStagesAsync(f.Request);
        var components = ready.WholeTray!.SourceMatrix.Components;
        Assert.Equal(ComponentEvidenceSource.Virtual, Assert.Single(components, x => x.Component == ComponentKind.Camera).Source);
        Assert.Equal(ComponentEvidenceSource.Test, Assert.Single(components, x => x.Component == ComponentKind.Light).Source);
        Assert.Equal(ComponentEvidenceSource.Real, Assert.Single(components, x => x.Component == ComponentKind.Algorithm).Source);
        Assert.Equal("DeclaredUnitAlgorithm/7", Assert.Single(components, x => x.Component == ComponentKind.Algorithm).VersionRef);
        Assert.Equal("Derived", Assert.Single(components, x => x.Component == ComponentKind.Host).Quality);
        Assert.Equal(EvidenceScope.SoftwareLoopOnly, ready.WholeTray.SourceMatrix.Scope);
    }

    [Theory]
    [InlineData(true, false, "Camera")]
    [InlineData(false, true, "Algorithm")]
    public async Task UnknownProducerCannotBeFilledFromPurposeOrHistoricalResultCategory(
        bool missingCamera, bool missingAlgorithm, string component)
    {
        var f = Fixture(missingCamera: missingCamera, missingAlgorithm: missingAlgorithm);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => f.Orchestrator.ExecuteThreeStagesAsync(f.Request));
        Assert.Contains("ComponentEvidenceMatrixIncomplete", error.Message);
        Assert.Contains(component, error.Message);
        Assert.DoesNotContain(f.Events.Events, x => x.EventType is StageEventType.WholeTrayCompleted or StageEventType.ManualRemovalAllowed);
    }

    [Fact]
    public async Task MainFlowPersistsWholeTrayAllowanceAndManualCompletionInOrder()
    {
        var f = Fixture();
        var ready = await f.Orchestrator.ExecuteThreeStagesAsync(f.Request);

        Assert.Equal(WholeTrayWorkflowStatus.ReadyForRemoval, ready.Status);
        Assert.NotNull(ready.WholeTray);
        Assert.Equal([PlcWorkflowStage.Sorting, PlcWorkflowStage.UnloadPreparation],
            f.Plc.Calls.Select(x => x.Stage));

        var unlocked = await f.Orchestrator.AllowManualRemovalAsync(new ManualRemovalAllowanceRequest(
            ready.WholeTray!.Reference, Guid.NewGuid(), 42, f.Clock.GetUtcNow().AddSeconds(10), "unlock-1", "unit-snapshot", Guid.NewGuid()));
        Assert.Equal(WholeTrayWorkflowStatus.ManualRemovalAllowed, unlocked.Status);
        Assert.Equal(2, f.Plc.Calls.Count); // Permission adds no device action.
        Assert.Contains(f.Events.Events, x => x.EventType == StageEventType.ManualRemovalAllowed);

        Assert.True(f.Lease.IsOwner(f.RunId));
        var final = await f.Orchestrator.ConfirmManualRemovalAsync(new ManualTrayRemovalConfirmationRequest(
            Guid.NewGuid(), ready.WholeTray.Reference, unlocked.ManualRemovalAllowedEventId!.Value,
            "operator-1", f.Clock.GetUtcNow(), "人工确认已取盘", "manual-1",
            ManualEvidence(f.Clock.GetUtcNow())));
        Assert.Equal(WholeTrayWorkflowStatus.FinalUnloadCompleted, final.Status);
        Assert.True(final.FinalCompletion!.IsValid);
        Assert.Null(f.Lease.Owner);
        Assert.Contains(f.Events.Events, x => x.EventType == StageEventType.FinalUnloadCompleted);
    }

    [Fact]
    public async Task AllowanceCannotBypassPersistedWholeTrayEvidence()
    {
        var f = Fixture();
        var reference = new WholeTrayCompletionReference(Guid.NewGuid(), f.RunId, f.TrayId,
            f.StationId, f.LineId, "recipe-v1", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Orchestrator.AllowManualRemovalAsync(
            new ManualRemovalAllowanceRequest(reference, Guid.NewGuid(), 42,
                f.Clock.GetUtcNow().AddSeconds(10), "unlock-before-completion", "unit-snapshot", Guid.NewGuid())));
        Assert.Empty(f.Plc.Calls);
    }

    [Fact]
    public async Task UnconfirmedAllowanceCommitDoesNotCreateRemovalPermission()
    {
        var f = Fixture();
        var ready = await f.Orchestrator.ExecuteThreeStagesAsync(f.Request);
        f.Events.FailAllowanceSave = true;
        await Assert.ThrowsAsync<IOException>(() => f.Orchestrator.AllowManualRemovalAsync(new ManualRemovalAllowanceRequest(
            ready.WholeTray!.Reference, Guid.NewGuid(), 42, f.Clock.GetUtcNow().AddSeconds(10), "allowance-save-failed", "unit-snapshot", Guid.NewGuid())));
        Assert.DoesNotContain(f.Events.Events, x => x.EventType == StageEventType.ManualRemovalAllowed);
        Assert.Equal(2, f.Plc.Calls.Count);
    }

    [Fact]
    public async Task CancelledExpiredOrWrongUnloadEpochCannotGrantRemoval()
    {
        var f = Fixture();
        var ready = await f.Orchestrator.ExecuteThreeStagesAsync(f.Request);
        var request = new ManualRemovalAllowanceRequest(ready.WholeTray!.Reference, Guid.NewGuid(), 42,
            f.Clock.GetUtcNow().AddSeconds(10), "allowance-invalid", "unit-snapshot", Guid.NewGuid());
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.Orchestrator.AllowManualRemovalAsync(request, cancelled.Token));
        await Assert.ThrowsAsync<TimeoutException>(() => f.Orchestrator.AllowManualRemovalAsync(request with { DeadlineUtc = f.Clock.GetUtcNow() }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Orchestrator.AllowManualRemovalAsync(request with { ConnectionEpoch = 43 }));
        Assert.DoesNotContain(f.Events.Events, x => x.EventType == StageEventType.ManualRemovalAllowed);
        Assert.Equal(2, f.Plc.Calls.Count);
    }

    [Fact]
    public async Task ProductionSourceCannotUseSimulatedStageEvidence()
    {
        var f = Fixture();
        var production = f.Request with
        {
            ThreeStages = f.Request.ThreeStages with
            {
                Detection = f.Request.ThreeStages.Detection with { Purpose = "Production" }
            }
        };
        var rejected = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            f.Orchestrator.ExecuteThreeStagesAsync(production));
        Assert.Equal("ProductionStageEvidenceRequired", rejected.Message);
    }

    private static FixtureState Fixture(bool missingCamera = false, bool missingAlgorithm = false)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 23, 0, 0, 0, TimeSpan.Zero));
        var events = new MemoryEvents(clock);
        var runId = Guid.NewGuid(); var trayId = Guid.NewGuid(); var stationId = Guid.NewGuid(); var lineId = Guid.NewGuid();
        var plan = new RecipeRunPlan("tray", "scenario", "F001", "recipe", "recipe-v1", "catalog", "simulationOnly",
            "unit", 1, null, false, new RecipeDisposition("OK", "NG", "Pending", "unit"),
            "motion", "quality", new Dictionary<string, CaptureProfile>(),
            new Dictionary<string, SlotExecutionInputs> { ["P01"] = SemanticPlanFixture.Sorting("P01") }, [],
            [new RecipeStep(1, RecipeStepKind.SortUnit, "unit-a", "object-a", "P01", "ClassA", null, null, null, 1)
                { PhysicalSlotIndex = 1 }], 1) { Model = "ComponentModel", DefinitionDigest = "component-digest", ECode = new(false, null, null, false, null), Approval = SemanticPlanFixture.Approval, AlgorithmRequirements = new Dictionary<string, AlgorithmRequirement>() };
        var detectionRequest = new DetectionRequest(runId, trayId, stationId, lineId, WholeTrayWorkflowStage.Detection,
            Guid.NewGuid(), plan.RecipeVersion, 42, clock.GetUtcNow().AddMinutes(2), ["media-1"], "Test", "detect-1")
        { ExpectedObjects = [new("object-a", new FixedPoint("P01", "coordinate-v1", 1, 2, "mm", "tray"), "ClassA")],
          SessionId = Guid.NewGuid(), SnapshotId = "unit-snapshot", ClockId = "unit-clock", MotionConfiguration = JsonSerializer.Deserialize<PublicConfiguration>(File.ReadAllText(
            Path.Combine(TestConfiguration.Workspace(), "specs", "007-station01-integrated-loop",
                "examples", "public.virtual-loop.json")), new JsonSerializerOptions(JsonSerializerDefaults.Web)) };
        var detection = new FakeDetectionPort(missingAlgorithm, missingCamera);
        var allocator = new SortingTargetAllocator(events, clock, 2000);
        var plc = new FakeStagePort(allocator, clock);
        var executor = new ThreeStageWorkflowExecutor(detection, plc, new RecipeSortingMapper(events, clock), events,
            allocator, clock, new ImmediateDelay(clock));
        var completion = new MemoryCompletions(events, clock);
        var lease = new ResourceLease(); Assert.True(lease.TryHold(runId));
        var orchestrator = new WholeTrayWorkflowOrchestrator(executor, events, completion, lease, clock, new(ComponentEvidenceSource.Real, "DeclaredUnitHost/1", "Derived"));
        return new(orchestrator, events, plc, lease, clock, runId, trayId, stationId, lineId,
            new(new(detectionRequest, plan, Guid.NewGuid(), Guid.NewGuid(), UnloadTarget: detectionRequest.MotionConfiguration!.Motion.Points.Unload,
            PositionTolerance: detectionRequest.MotionConfiguration.Motion.PositionTolerance, TargetPurpose: "Test", ConfigSnapshotId: detectionRequest.SnapshotId), Guid.NewGuid(), "completion-1"));
    }

    private sealed record FixtureState(WholeTrayWorkflowOrchestrator Orchestrator, MemoryEvents Events,
        FakeStagePort Plc, ResourceLease Lease, FakeTimeProvider Clock, Guid RunId, Guid TrayId, Guid StationId, Guid LineId,
        WholeTrayWorkflowRequest Request);

    private sealed class FakeDetectionPort(bool missingOrigin, bool missingCamera) : IDetectionPort
    {
        public ValueTask<DetectionPortResult> ExecuteAsync(DetectionRequest actual, CancellationToken cancellationToken) =>
            ValueTask.FromResult(new DetectionPortResult(actual, DetectionResultKind.Completed, "det-1",
                [new DetectionObjectResult("object-a", new FixedPoint("P01", "coordinate-v1", 1, 2, "mm", "tray"), "ClassA")],
                ResultSource.Simulated, ResultQuality.Degraded, null, actual.DeadlineUtc) {
                    LastObservation = new(Guid.NewGuid(), actual.RunId, actual.TrayId, Guid.NewGuid(), Guid.NewGuid(),
                        actual.DeadlineUtc.AddSeconds(-1), TrayObservationPurpose.InitialPreparation, 1, null,
                        [new(1, TrayPresence.Present, TrayPose.Normal)],
                        new(1, 2, "mm", "tray", "DeclaredUnitObservation"),
                        new(ComponentEvidenceSource.Test, "DeclaredUnitObservation/1", "DeclaredUnit"), ["declared-unit-observation"]),
                    SlotParticipation = new Dictionary<int, SlotParticipation> { [1] = new(1, SlotParticipationState.Participating, "DeclaredUnitObservation") },
                    AlgorithmOrigin = missingOrigin ? ComponentExecutionOrigin.Unknown :
                        new(ComponentEvidenceSource.Real, "DeclaredUnitAlgorithm/7", "Measured"),
                    CaptureFacts = [new(actual.RunId, Guid.NewGuid(), Guid.NewGuid(), 42, "declared-unit-settings", "Virtual",
                        missingCamera ? ComponentExecutionOrigin.Unknown : new(ComponentEvidenceSource.Virtual, "DeclaredUnitCamera/3", "Captured"),
                        new(ComponentEvidenceSource.Test, "DeclaredUnitLight/2", "ConfiguredOnly"), CaptureApplicationState.ConfiguredOnly,
                        null, false, ["declared-unit-media"])] });
    }

    private sealed class FakeStagePort(SortingTargetAllocator allocator, FakeTimeProvider clock) : IPlcStageActionPort
    {
        public List<PlcStageActionRequest> Calls { get; } = [];
        public async ValueTask<PlcStageActionResult> ExecuteAsync(PlcStageActionRequest request,
            CancellationToken cancellationToken)
        {
            Calls.Add(request);
            if (request.Stage == PlcWorkflowStage.Sorting)
                await SemanticStageFixture.CommitPickAsync(allocator, request, clock, cancellationToken);
            Assert.True(request.Stage is PlcWorkflowStage.Sorting or PlcWorkflowStage.UnloadPreparation);
            return SemanticStageFixture.Result(request, StageActionKind.Completed, clock.GetUtcNow());

        }
    }

    private sealed class ImmediateDelay(FakeTimeProvider clock) : IWorkflowDelay
    {
        public Task DelayAsync(TimeSpan delay, TimeProvider timeProvider, CancellationToken cancellationToken)
        { clock.Advance(delay); return Task.CompletedTask; }
    }

    private static ComponentEvidence ManualEvidence(DateTimeOffset at) => new(
        ComponentKind.ManualActor, ComponentEvidenceState.Verified,
        ComponentEvidenceSource.AuthenticatedHuman, "Authenticated", "test-auth/1",
        ["actor://operator-1"], at, "manual-digest");

    private sealed class MemoryCompletions(MemoryEvents events, FakeTimeProvider clock)
        : IWholeTrayCompletionStore
    {
        private readonly Dictionary<Guid, WholeTrayCompletionRecord> records = [];

        public Task<bool> ReconcileFinalAsync(Guid runId, Guid trayId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("This workflow fixture does not model durable final reconciliation.");

        public async Task<WholeTrayCompletionRecord> CreateAsync(WholeTrayCompletionCreateRequest request,
            CancellationToken cancellationToken = default)
        {
            var completed = new List<StageEvent>();
            foreach (var stage in new[] { WholeTrayWorkflowStage.Detection,
                         WholeTrayWorkflowStage.Sorting, WholeTrayWorkflowStage.UnloadPreparation })
                completed.Add((await events.ReadAsync(request.RunId, request.TrayId, stage,
                    cancellationToken)).Last(x => x.EventType == StageEventType.Completed));
            var reference = new WholeTrayCompletionReference(request.CompletionId, request.RunId,
                request.TrayId, request.StationId, request.LineId, request.PlanRevision,
                completed[0].EventId, completed[1].EventId, completed[2].EventId);
            var record = new WholeTrayCompletionRecord(reference, request.SourceMatrix,
                request.CreatedAtUtc, clock.GetUtcNow(), "record-digest", clock.GetUtcNow().AddYears(7));
            records[request.CompletionId] = record;
            await events.AppendAsync(Append(reference, WholeTrayWorkflowStage.ManualRemovalAdmission,
                StageEventType.WholeTrayCompleted, request.IdempotencyKey + ":evidence"), cancellationToken);
            return record;
        }

        public Task<WholeTrayCompletionRecord?> GetAsync(WholeTrayCompletionReference reference,
            CancellationToken cancellationToken = default) => Task.FromResult(
                records.GetValueOrDefault(reference.CompletionId) is { } value && value.Reference == reference
                    ? value : null);

        public Task<WholeTrayCompletionRecord?> GetByRunAsync(Guid runId,
            CancellationToken cancellationToken = default) => Task.FromResult(
                records.Values.SingleOrDefault(x => x.Reference.RunId == runId));

        public async Task<FinalUnloadCompletion> ConfirmManualRemovalAsync(
            ManualTrayRemovalConfirmationRequest request, CancellationToken cancellationToken = default)
        {
            await events.AppendAsync(Append(request.WholeTray,
                WholeTrayWorkflowStage.ManualTrayRemovalConfirmation,
                StageEventType.ManualTrayRemovalConfirmed, request.IdempotencyKey + ":manual"),
                cancellationToken);
            await events.AppendAsync(Append(request.WholeTray,
                WholeTrayWorkflowStage.ManualTrayRemovalConfirmation,
                StageEventType.FinalUnloadCompleted, request.IdempotencyKey + ":final"),
                cancellationToken);
            return new(request.FinalCompletionId, request.WholeTray, request.ManualRemovalAllowedEventId,
                request.OperatorId, request.ConfirmedAtUtc, "ManualTrayRemovalConfirmation", true,
                request.Reason, Guid.NewGuid());
        }

        private StageEventAppendRequest Append(WholeTrayCompletionReference reference,
            WholeTrayWorkflowStage stage, StageEventType type, string key) => new(Guid.NewGuid(),
            reference.RunId, reference.TrayId, reference.StationId.ToString(),
            reference.LineId.ToString(), stage, Guid.NewGuid(), 1, 42, type,
            clock.GetUtcNow(), ResultSource.Simulated, ResultQuality.Derived, null,
            "digest-" + key, "{}", key, reference.PlanRevision);
    }

    private sealed class MemoryEvents(FakeTimeProvider clock) : IStageEventStore
    {
        public List<StageEvent> Events { get; } = [];
        public bool FailAllowanceSave { get; set; }
        private readonly Dictionary<(Guid, Guid, WholeTrayWorkflowStage), StageProjection> projections = [];
        public Task<StageEventAppendResult> AppendAsync(StageEventAppendRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (FailAllowanceSave && request.EventType == StageEventType.ManualRemovalAllowed)
                throw new IOException("DeclaredStoreFailureBeforeAllowanceCommit");
            var replay = Events.FirstOrDefault(x => x.IdempotencyKey == request.IdempotencyKey);
            if (replay is not null)
                return Task.FromResult(new StageEventAppendResult(StageEventCommitState.Replay, replay,
                    projections[(request.RunId, request.TrayId, request.Stage)], null));
            var now = clock.GetUtcNow();
            var sequence = Events.Where(x => x.RunId == request.RunId && x.TrayId == request.TrayId && x.Stage == request.Stage)
                .Select(x => x.Sequence).DefaultIfEmpty(0).Max() + 1;
            var evt = new StageEvent(request.EventId, request.RunId, request.TrayId, request.StationId, request.LineId,
                request.Stage, request.OperationId, request.Attempt, request.ConnectionEpoch, request.EventType,
                request.OccurredAt, now, request.Source, request.Quality, request.ErrorCode, request.PayloadDigest,
                request.PayloadJson, request.IdempotencyKey, sequence, now.AddYears(7),
                request.PlanRevision, request.StageStartedAtUtc, request.StageDeadlineAtUtc);
            var key = (request.RunId, request.TrayId, request.Stage);
            var projection = StageEventProjection.Apply(projections.TryGetValue(key, out var p) ? p : StageEventProjection.Initial(evt), evt);
            Events.Add(evt); projections[key] = projection;
            return Task.FromResult(new StageEventAppendResult(StageEventCommitState.Committed, evt, projection, null));
        }
        public Task<IReadOnlyList<StageEvent>> ReadAsync(Guid runId, Guid trayId, WholeTrayWorkflowStage stage, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StageEvent>>(Events.Where(x => x.RunId == runId && x.TrayId == trayId && x.Stage == stage).OrderBy(x => x.Sequence).ToArray());
        public Task<StageProjection?> GetProjectionAsync(Guid runId, Guid trayId, WholeTrayWorkflowStage stage, CancellationToken cancellationToken = default) =>
            Task.FromResult(projections.GetValueOrDefault((runId, trayId, stage)));
        public Task<StageProjection> RecoverAsync(Guid runId, Guid trayId, WholeTrayWorkflowStage stage, CancellationToken cancellationToken = default) =>
            Task.FromResult(projections[(runId, trayId, stage)]);
    }
}
