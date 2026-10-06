using Gaode.Contracts.Tests.Support;
using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Application.Workflow;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using System.Text.Json;
using Xunit;

namespace Gaode.Contracts.Tests.Workflow;

public sealed class RecipeSortingMapperTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PosePendingIsLastUsesOriginalSourceAndPreservesPreviousQuality(bool hasPreviousResult)
    {
        var source = new FixedPoint("origin-b", "declared-016", 130, 140, "mm", "tray", 50);
        var inputs = new ObjectExecutionInputs(new(source, "Test:016-source"), [], null, null,
            new Dictionary<string, HandlingPoint>(), new Dictionary<string, RecipePurposePoint>());
        var plan = Plan(SortStep(1,"unit-a","object-a","P01","ClassA"),
            SortStep(2,"unit-b","object-b","P02","ClassB"),SortStep(3,"unit-c","object-c","P03","ClassC")) with {
            UnitKind="independentPart", ExecutionPositions=new Dictionary<string,SlotExecutionInputs> {
                ["P02"]=new("P02",inputs,new Dictionary<string,ObjectExecutionInputs>()) } };
        var objects = new List<DetectionObjectResult> { Object("object-a","P01","ClassA") };
        if(hasPreviousResult)objects.Add(Object("object-b","P02","ClassB","NG"));
        var detection = Detection(plan,objects);
        var slots = new TraySlotObservation[] { new(1,TrayPresence.Present,TrayPose.Normal),
            new(2,TrayPresence.Present,TrayPose.Abnormal,"tilt"),new(3,TrayPresence.Absent,TrayPose.Unknown) };
        detection = detection with { SlotParticipation=SlotParticipation.Apply([],slots),
            PosePending=[new("object-b",2,detection.LastObservation!.ObservationId,"write://016-pose",hasPreviousResult?"FurtherInspectionTerminated":"NotInspected","3D姿态异常")] };
        var result = await new RecipeSortingMapper(new RecordingStageEventStore()).MapAsync(detection,plan,plan.RecipeVersion,mappingOperationId,42);
        Assert.True(result.IsComplete);
        Assert.Equal(["object-a","object-b"],result.Actions.Select(a=>a.ObjectId));
        Assert.True(result.Actions[1].IsPosePending);Assert.Equal("Pending",result.Actions[1].Disposition);
        Assert.Equal(source,result.Actions[1].Position);Assert.DoesNotContain(result.Actions,a=>a.ObjectId=="object-c");
        Assert.Equal(hasPreviousResult?"NG":null,detection.Objects.SingleOrDefault(x=>x.ObjectId=="object-b")?.Disposition);
    }
    private readonly Guid runId = Guid.NewGuid();
    private readonly Guid trayId = Guid.NewGuid();
    private readonly Guid stationId = Guid.NewGuid();
    private readonly Guid lineId = Guid.NewGuid();
    private readonly Guid mappingOperationId = Guid.NewGuid();

    [Fact]
    public async Task CompleteDetectionMapsToFrozenSortOrderAndPreservesCorrelation()
    {
        var store = new RecordingStageEventStore();
        var plan = Plan(SortStep(1, "unit-a", "object-a", "P01", "ClassA"),
            SortStep(2, "unit-b", "object-b", "P02", "ClassB"));
        var detection = Detection(plan, [Object("object-b", "P02", "ClassB"), Object("object-a", "P01", "ClassA")]);

        var mapped = await new RecipeSortingMapper(store).MapAsync(detection, plan,
            detection.Request.PlanRevision, mappingOperationId, 42);

        Assert.True(mapped.IsComplete);
        Assert.True(mapped.IsValid);
        Assert.Empty(store.Appends);
        Assert.Equal(["object-a", "object-b"], mapped.Actions.Select(x => x.ObjectId));
        Assert.All(mapped.Actions, action =>
        {
            Assert.Equal(runId, action.RunId);
            Assert.Equal(trayId, action.TrayId);
            Assert.Equal(plan.RecipeVersion, action.RecipePlanVersion);
            Assert.Equal(42, action.ConnectionEpoch);
            Assert.NotEqual(Guid.Empty, action.OperationId);
        });
    }

    [Fact]
    public async Task MissingObjectAppendsMappingFailedAndPausesForManualReview()
    {
        var store = new RecordingStageEventStore();
        var plan = Plan(SortStep(1, "unit-a", "object-a", "P01", "ClassA"),
            SortStep(2, "unit-b", "object-b", "P02", "ClassB"));
        var mapped = await new RecipeSortingMapper(store).MapAsync(
            Detection(plan, [Object("object-a", "P01", "ClassA")]), plan,
            plan.RecipeVersion, mappingOperationId, 42);

        Assert.False(mapped.IsComplete);
        Assert.Empty(mapped.Actions);
        Assert.Contains("object-b", mapped.Failure!.MissingObjectIds);
        Assert.False(mapped.Failure.AllowsPartialDispatch);
        Assert.Single(store.Appends);
        Assert.Equal(StageEventType.MappingFailed, store.Appends[0].EventType);
        Assert.Equal(WholeTrayWorkflowStage.Detection, store.Appends[0].Stage);
        Assert.Equal(StageProjectionStatus.PausedForManualReview, store.Projection!.Status);
        Assert.True(store.Projection.NeedsManualReview);
        Assert.Equal(WholeTrayWorkflowStage.Detection, store.Projection.Stage);
    }

    [Fact]
    public async Task DuplicateObjectIdsAreRejectedWithoutPartialPlan()
    {
        var store = new RecordingStageEventStore();
        var plan = Plan(SortStep(1, "unit-a", "object-a", "P01", "ClassA"));
        var mapped = await new RecipeSortingMapper(store).MapAsync(
            Detection(plan, [Object("object-a", "P01", "ClassA"), Object("object-a", "P01", "ClassA")]),
            plan, plan.RecipeVersion, mappingOperationId, 42);

        Assert.False(mapped.IsComplete);
        Assert.Contains("object-a", mapped.Failure!.DuplicateObjectIds);
        Assert.Empty(mapped.Actions);
        Assert.Single(store.Appends);
    }

    [Fact]
    public async Task PositionClassificationAndUnexpectedObjectsAreAmbiguous()
    {
        var store = new RecordingStageEventStore();
        var plan = Plan(SortStep(1, "unit-a", "object-a", "P01", "ClassA"),
            SortStep(2, "unit-b", "object-b", "P02", "ClassB"));
        var mapped = await new RecipeSortingMapper(store).MapAsync(
            Detection(plan, [Object("object-a", "P99", "ClassA"), Object("object-b", "P02", "Wrong"),
                Object("object-extra", "P03", "ClassC")]), plan,
            plan.RecipeVersion, mappingOperationId, 42);

        Assert.False(mapped.IsComplete);
        Assert.Contains("object-a", mapped.Failure!.AmbiguousObjectIds);
        Assert.Contains("object-b", mapped.Failure.AmbiguousObjectIds);
        Assert.Contains("object-extra", mapped.Failure.AmbiguousObjectIds);
        Assert.Empty(mapped.Actions);
    }

    [Fact]
    public async Task PlanRevisionMismatchIsRejectedAndDoesNotCallSortingPort()
    {
        var store = new RecordingStageEventStore();
        var plan = Plan(SortStep(1, "unit-a", "object-a", "P01", "ClassA"));
        var detection = Detection(plan, [Object("object-a", "P01", "ClassA")], planRevision: "old-plan");
        var mapped = await new RecipeSortingMapper(store).MapAsync(detection, plan,
            plan.RecipeVersion, mappingOperationId, 42);

        Assert.False(mapped.IsComplete);
        Assert.Contains("planRevision:old-plan", mapped.Failure!.AmbiguousObjectIds);
        Assert.Empty(mapped.Actions);
        Assert.Single(store.Appends);
    }

    [Fact]
    public async Task OkIsNotDispatchedWhileNgAndPendingKeepDistinctFormalActions()
    {
        var store = new RecordingStageEventStore();
        var plan = Plan(SortStep(1, "unit-a", "object-a", "P01", "ClassA"),
            SortStep(2, "unit-b", "object-b", "P02", "ClassB"),
            SortStep(3, "unit-c", "object-c", "P03", "ClassC"));
        var detection = Detection(plan,
            [Object("object-a", "P01", "ClassA", "OK"),
                Object("object-b", "P02", "ClassB", "NG"),
                Object("object-c", "P03", "ClassC", "Pending")]);

        var mapped = await new RecipeSortingMapper(store).MapAsync(detection, plan,
            plan.RecipeVersion, mappingOperationId, 42);

        Assert.True(mapped.IsComplete);
        Assert.Equal(["object-b", "object-c"], mapped.Actions.Select(x => x.ObjectId));
        Assert.Equal(["NG", "Pending"], mapped.Actions.Select(x => x.Disposition));
        Assert.DoesNotContain(mapped.Actions, x => x.ObjectId == "object-a");
    }

    [Fact]
    public async Task LegacySpecialExitCannotExemptCurrentNgSorting()
    {
        var plan = Plan(SortStep(1, "unit-a", "object-a", "P01", "ClassA"));
        var detection = Detection(plan, [Object("object-a", "P01", "ClassA") with
        { SpecialHandlingCompleted = true, HandlingEvidenceReference = "special-exit:" + Guid.NewGuid() }]);
        var mapped = await new RecipeSortingMapper(new RecordingStageEventStore()).MapAsync(
            detection, plan, plan.RecipeVersion, mappingOperationId, 42);
        Assert.False(mapped.IsComplete);
        Assert.Contains("object-a:LegacySpecialExitNotAuthorized", mapped.Failure!.AmbiguousObjectIds);
        Assert.Empty(mapped.Actions);
    }

    [Fact]
    public async Task PoseExcludedPhysicalSlotWithoutDispositionRejectsPartialSorting()
    {
        var plan = Plan(SortStep(1, "unit-a", "object-a", "P01", "ClassA"),
            SortStep(2, "unit-b", "object-b", "P02", "ClassB"));
        var detection = Detection(plan, [Object("object-b", "P02", "ClassB")]);
        detection = detection with { SlotParticipation = new Dictionary<int, SlotParticipation>(detection.SlotParticipation)
            { [1] = new(1, SlotParticipationState.PoseExcluded, "DeclaredAbnormalPose") } };
        var mapped = await new RecipeSortingMapper(new RecordingStageEventStore()).MapAsync(
            detection, plan, plan.RecipeVersion, mappingOperationId, 42);
        Assert.False(mapped.IsComplete);
        Assert.Contains("object-a:PoseHandlingEvidence",mapped.Failure!.MissingObjectIds);
        Assert.Empty(mapped.Actions);
    }

    private DetectionPortResult Detection(RecipeRunPlan plan, IReadOnlyList<DetectionObjectResult> objects,
        string? planRevision = null)
    {
        var origin = new ComponentExecutionOrigin(ComponentEvidenceSource.Test, "mapper-component/1", "DeclaredInput");
        var slots = plan.Steps.Select(s => new TraySlotObservation(s.PhysicalSlotIndex!.Value,
            TrayPresence.Present, TrayPose.Normal, "DeclaredComponentInput")).ToArray();
        var observation = new TrayObservation(Guid.NewGuid(), runId, trayId, Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow, TrayObservationPurpose.InitialPreparation, 1, null, slots,
            new(1, 2, "mm", "tray", "Test:mapper"), origin, ["Test:declared-mapper-input"]);
        return new(new DetectionRequest(runId, trayId, stationId, lineId, WholeTrayWorkflowStage.Detection, mappingOperationId,
            planRevision ?? plan.RecipeVersion, 42, DateTimeOffset.UtcNow.AddMinutes(1), ["media-1"],
            "Test", "detection-request-1", ExpectedObjects: plan.Steps.Select(s =>
                new DetectionObjectExpectation(s.MemberId ?? s.UnitId,
                    Object(s.MemberId ?? s.UnitId, s.SlotId!, s.Material!).Position, s.Material!)).ToArray()),
            DetectionResultKind.Completed, "result-1", objects, ResultSource.Test, ResultQuality.Derived, null, DateTimeOffset.UtcNow)
        { LastObservation = observation, SlotParticipation = SlotParticipation.Apply([], slots) };
    }

    private static DetectionObjectResult Object(string id, string positionId, string classification,
        string disposition = "NG") =>
        new(id, new FixedPoint(positionId, "coordinate-v1", 1, 2, "mm", "tray"),
            classification, disposition, ["evidence://detection/" + id]);

    private static RecipeRunPlan Plan(params RecipeStep[] sortSteps) => new(
        "tray-run", "scenario", "F001", "recipe", "recipe-v1", "catalog", "Released", "unit",
        1, null, false, new RecipeDisposition("OK", "NG", "Pending", "unit"),
        "motion", "quality", new Dictionary<string, CaptureProfile>(), new Dictionary<string, SlotExecutionInputs>(),
        Array.Empty<string>(), sortSteps, 1) { Model = "ComponentModel", DefinitionDigest = "component-digest", ECode = new(false, null, null, false, null), Approval = SemanticPlanFixture.Approval, AlgorithmRequirements = new Dictionary<string, AlgorithmRequirement>() };

    private static RecipeStep SortStep(int sequence, string unitId, string objectId, string slotId,
        string classification) => new(sequence, RecipeStepKind.SortUnit, unitId, objectId, slotId,
        classification, null, null, null, 1) { PhysicalSlotIndex = sequence };

    private sealed class RecordingStageEventStore : IStageEventStore
    {
        public List<StageEventAppendRequest> Appends { get; } = [];
        public List<StageEvent> Existing { get; } = [];
        public StageProjection? Projection { get; private set; }

        public Task<StageEventAppendResult> AppendAsync(StageEventAppendRequest request,
            CancellationToken cancellationToken = default)
        {
            Appends.Add(request);
            var persisted = DateTimeOffset.UtcNow;
            var evt = new StageEvent(request.EventId, request.RunId, request.TrayId, request.StationId,
                request.LineId, request.Stage, request.OperationId, request.Attempt, request.ConnectionEpoch,
                request.EventType, request.OccurredAt, persisted, request.Source, request.Quality,
                request.ErrorCode, request.PayloadDigest, request.PayloadJson, request.IdempotencyKey, 1,
                persisted.AddYears(7));
            Projection = StageEventProjection.Apply(StageEventProjection.Initial(evt), evt);
            return Task.FromResult(new StageEventAppendResult(StageEventCommitState.Committed, evt,
                Projection, null));
        }

        public Task<IReadOnlyList<StageEvent>> ReadAsync(Guid runId, Guid trayId, WholeTrayWorkflowStage stage,
            CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<StageEvent>>(Existing);

        public Task<StageProjection?> GetProjectionAsync(Guid runId, Guid trayId, WholeTrayWorkflowStage stage,
            CancellationToken cancellationToken = default) => Task.FromResult(Projection);

        public Task<StageProjection> RecoverAsync(Guid runId, Guid trayId, WholeTrayWorkflowStage stage,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
