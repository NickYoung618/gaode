using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Application.Workflow;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Microsoft.Extensions.Time.Testing;
using System.Text.Json;
using Gaode.Contracts.Tests.Support;
using Xunit;

namespace Gaode.Contracts.Tests.Workflow;

[Trait("EvidenceLevel", "UpperIsolation")]
public sealed class DetectionRetryAndPendingTests
{
    [Fact]
    public async Task StrictRecipePendingWithWrongObjectPositionCannotDispatchSorting()
    {
        var fixture = Create(DetectionResultKind.TimedOut);
        var strict = fixture.Request with { Detection = fixture.Request.Detection with
        {
            ExpectedObjects = [new DetectionObjectExpectation("wrong-object",
                new FixedPoint("P02", "approved-test", 1, 2, "mm", "TEST", 3), "ClassA")]
        } };
        var result = await fixture.Executor.ExecuteAsync(strict);
        Assert.Equal(ThreeStageExecutionStatus.PausedForManualReview, result.Status);
        Assert.Equal("PendingPhysicalPositionMissing", result.ErrorCode);
        Assert.Empty(fixture.Plc.Calls);
        Assert.Contains(fixture.Events.Events, x =>
            x.EventType == StageEventType.ManualReviewRequested);
        Assert.DoesNotContain(fixture.Events.Events, x =>
            x.EventType == StageEventType.Completed);
    }

    [Theory]
    [InlineData(DetectionResultKind.Disconnected, 4, 1, 2, 4)]
    [InlineData(DetectionResultKind.TimedOut, 3, 2, 5, 0)]
    public async Task ExhaustedDetectionRetriesCreatePendingAndContinueFormalSorting(
        DetectionResultKind kind, int expectedAttempts, int first, int second, int third)
    {
        var fixture = Create(kind);

        var result = await fixture.Executor.ExecuteAsync(fixture.Request);

        Assert.Equal(ThreeStageExecutionStatus.StagesCompleted, result.Status);
        Assert.Equal(expectedAttempts, fixture.Detection.Calls.Count);
        Assert.All(fixture.Detection.Calls, call =>
        {
            Assert.Equal(fixture.Request.Detection.DeadlineUtc, call.DeadlineUtc);
            Assert.Equal(fixture.Request.Detection.StageStartedAtUtc, call.StageStartedAtUtc);
        });
        var expectedDelays = new[] { first, second, third }.Where(x => x > 0)
            .Select(x => TimeSpan.FromSeconds(x)).ToArray();
        Assert.Equal(expectedDelays, fixture.Delay.Delays);
        Assert.Equal([PlcWorkflowStage.UnloadPreparation, PlcWorkflowStage.Sorting],
            fixture.Plc.Calls.Select(x => x.Stage));
        var pending = Assert.Single(fixture.Events.Events,
            x => x.EventType == StageEventType.PendingRecorded);
        Assert.Contains(kind == DetectionResultKind.Disconnected ? "TransportUnavailable" :
            "AlgorithmTimeout", pending.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("media://input/1", pending.PayloadJson, StringComparison.Ordinal);
        Assert.Contains("Pending", pending.PayloadJson, StringComparison.Ordinal);
        Assert.Contains(fixture.Events.Events, x => x.Stage == WholeTrayWorkflowStage.Detection &&
            x.EventType == StageEventType.Completed);
        Assert.Contains(fixture.Events.Events, x => x.Stage == WholeTrayWorkflowStage.Sorting &&
            x.EventType == StageEventType.Completed);
        Assert.Contains(fixture.Events.Events, x => x.Stage == WholeTrayWorkflowStage.UnloadPreparation &&
            x.EventType == StageEventType.Completed);
    }

    [Fact]
    public async Task SharedDeadlineExpiringDuringFirstAttemptDoesNotResetAndCreatesPending()
    {
        var fixture = Create(DetectionResultKind.TimedOut, advanceDuringCall: TimeSpan.FromSeconds(120));

        var result = await fixture.Executor.ExecuteAsync(fixture.Request);

        Assert.Equal(ThreeStageExecutionStatus.StagesCompleted, result.Status);
        Assert.Single(fixture.Detection.Calls);
        Assert.Empty(fixture.Delay.Delays);
        var pending = Assert.Single(fixture.Events.Events,
            x => x.EventType == StageEventType.PendingRecorded);
        Assert.Equal("StageDeadlineExceeded", pending.ErrorCode);
        using var payload = JsonDocument.Parse(pending.PayloadJson);
        Assert.Equal(fixture.Request.Detection.DeadlineUtc,
            payload.RootElement.GetProperty("deadline").GetDateTimeOffset());
        Assert.Equal([PlcWorkflowStage.UnloadPreparation, PlcWorkflowStage.Sorting],
            fixture.Plc.Calls.Select(x => x.Stage));
    }

    private static Fixture Create(DetectionResultKind kind, TimeSpan? advanceDuringCall = null)
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 23, 1, 0, 0, TimeSpan.Zero));
        var events = new MemoryEvents(clock);
        var detection = new ScriptedDetection(kind, clock, advanceDuringCall);
        var allocator = new SortingTargetAllocator(events, clock, 2000);
        var plc = new RecordingPlc(clock, allocator);
        var delay = new AdvancingDelay(clock);
        var plan = new RecipeRunPlan("tray", "S1", "F001", "recipe", "recipe-v1", "catalog",
            "simulationOnly", "unit", 1, null, false,
            new RecipeDisposition("OK", "NG", "Pending", "unit"),
            "motion", "quality", new Dictionary<string, CaptureProfile>(),
            new Dictionary<string, SlotExecutionInputs> { ["P01"] = SemanticPlanFixture.Sorting("P01") }, [],
            [new RecipeStep(1, RecipeStepKind.SortUnit, "unit-a", "object-a", "P01", "ClassA",
                null, null, null, 1, "NG|Pending") { PhysicalSlotIndex = 1 }], 1) { Model = "ComponentModel", DefinitionDigest = "component-digest", ECode = new(false, null, null, false, null), Approval = SemanticPlanFixture.Approval, AlgorithmRequirements = new Dictionary<string, AlgorithmRequirement>() };
        var started = clock.GetUtcNow();
        var request = new DetectionRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            WholeTrayWorkflowStage.Detection, Guid.NewGuid(), "plan-sha-1", 9,
            started.Add(StageRetryPolicy.StageDuration), ["media://input/1"], "Test", "detection-1",
            StageStartedAtUtc: started, ComponentEvidenceReferences: ["camera://sim/1", "algorithm://sim/1"])
        { ExpectedObjects = [new("object-a", new FixedPoint("P01", "coordinate-v1", 1, 2, "mm", "tray"), "ClassA")],
          SessionId = Guid.NewGuid(), SnapshotId = "unit-snapshot", ClockId = "unit-clock", MotionConfiguration = JsonSerializer.Deserialize<PublicConfiguration>(File.ReadAllText(
            Path.Combine(TestConfiguration.Workspace(), "specs", "007-station01-integrated-loop",
                "examples", "public.virtual-loop.json")), new JsonSerializerOptions(JsonSerializerDefaults.Web)) };
        var execution = new ThreeStageExecutionRequest(request, plan, Guid.NewGuid(), Guid.NewGuid(), UnloadTarget: request.MotionConfiguration!.Motion.Points.Unload,
            PositionTolerance: request.MotionConfiguration.Motion.PositionTolerance, TargetPurpose: "Test", ConfigSnapshotId: request.SnapshotId);
        var executor = new ThreeStageWorkflowExecutor(detection, plc,
            new RecipeSortingMapper(events, clock), events, allocator, clock, delay);
        return new(executor, execution, detection, plc, delay, events);
    }

    private sealed record Fixture(ThreeStageWorkflowExecutor Executor, ThreeStageExecutionRequest Request,
        ScriptedDetection Detection, RecordingPlc Plc, AdvancingDelay Delay, MemoryEvents Events);

    private sealed class ScriptedDetection(DetectionResultKind kind, FakeTimeProvider clock,
        TimeSpan? advanceDuringCall) : IDetectionPort
    {
        public List<DetectionRequest> Calls { get; } = [];
        public ValueTask<DetectionPortResult> ExecuteAsync(DetectionRequest request,
            CancellationToken cancellationToken)
        {
            Calls.Add(request);
            if (advanceDuringCall is { } advance) clock.Advance(advance);
            var error = kind == DetectionResultKind.Disconnected ? "TransportUnavailable" : "AlgorithmTimeout";
            return ValueTask.FromResult(new DetectionPortResult(request, kind, null, [],
                ResultSource.Simulated, ResultQuality.Unknown, error, clock.GetUtcNow(),
                ["algorithm-attempt://" + request.Attempt]));
        }
    }

    private sealed class RecordingPlc(FakeTimeProvider clock, SortingTargetAllocator allocator) : IPlcStageActionPort
    {
        public List<PlcStageActionRequest> Calls { get; } = [];
        public async ValueTask<PlcStageActionResult> ExecuteAsync(PlcStageActionRequest request,
            CancellationToken cancellationToken)
        {
            Calls.Add(request);
            if (request.Stage == PlcWorkflowStage.Sorting)
                await SemanticStageFixture.CommitPickAsync(allocator, request, clock, cancellationToken);
            return SemanticStageFixture.Result(request, StageActionKind.Completed, clock.GetUtcNow());

        }
    }

    private sealed class AdvancingDelay(FakeTimeProvider clock) : IWorkflowDelay
    {
        public List<TimeSpan> Delays { get; } = [];
        public Task DelayAsync(TimeSpan delay, TimeProvider timeProvider, CancellationToken cancellationToken)
        {
            Delays.Add(delay);
            clock.Advance(delay);
            return Task.CompletedTask;
        }
    }

    private sealed class MemoryEvents(FakeTimeProvider clock) : IStageEventStore
    {
        public List<StageEvent> Events { get; } = [];
        private readonly Dictionary<(Guid, Guid, WholeTrayWorkflowStage), StageProjection> projections = [];
        public Task<StageEventAppendResult> AppendAsync(StageEventAppendRequest request,
            CancellationToken cancellationToken = default)
        {
            var now = clock.GetUtcNow();
            var key = (request.RunId, request.TrayId, request.Stage);
            var sequence = Events.Count(x => x.RunId == request.RunId && x.TrayId == request.TrayId &&
                x.Stage == request.Stage) + 1;
            var evt = new StageEvent(request.EventId, request.RunId, request.TrayId, request.StationId,
                request.LineId, request.Stage, request.OperationId, request.Attempt, request.ConnectionEpoch,
                request.EventType, request.OccurredAt, now, request.Source, request.Quality,
                request.ErrorCode, request.PayloadDigest, request.PayloadJson, request.IdempotencyKey,
                sequence, now.AddYears(7), request.PlanRevision, request.StageStartedAtUtc, request.StageDeadlineAtUtc);
            var projection = StageEventProjection.Apply(projections.TryGetValue(key, out var current)
                ? current : StageEventProjection.Initial(evt), evt);
            Events.Add(evt);
            projections[key] = projection;
            return Task.FromResult(new StageEventAppendResult(StageEventCommitState.Committed,
                evt, projection, null));
        }
        public Task<IReadOnlyList<StageEvent>> ReadAsync(Guid runId, Guid trayId,
            WholeTrayWorkflowStage stage, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StageEvent>>(Events.Where(x => x.RunId == runId &&
                x.TrayId == trayId && x.Stage == stage).ToArray());
        public Task<StageProjection?> GetProjectionAsync(Guid runId, Guid trayId,
            WholeTrayWorkflowStage stage, CancellationToken cancellationToken = default) =>
            Task.FromResult(projections.GetValueOrDefault((runId, trayId, stage)));
        public Task<StageProjection> RecoverAsync(Guid runId, Guid trayId,
            WholeTrayWorkflowStage stage, CancellationToken cancellationToken = default) =>
            Task.FromResult(projections[(runId, trayId, stage)]);
    }
}
