using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Application.Workflow;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Contracts.Tests.Support;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Gaode.Contracts.Tests.Workflow;

[Trait("EvidenceLevel", "UpperIsolation")]
public sealed class ThreeStageWorkflowExecutorTests
{
    private readonly Guid runId = Guid.NewGuid();
    private readonly Guid trayId = Guid.NewGuid();
    private readonly Guid stationId = Guid.NewGuid();
    private readonly Guid lineId = Guid.NewGuid();
    private readonly Guid detectionOperationId = Guid.NewGuid();
    private readonly Guid mappingOperationId = Guid.NewGuid();
    private readonly Guid unloadOperationId = Guid.NewGuid();

    [Fact]
    public async Task SpecialWithoutMechanicalBasisRecordsARefusalBeforeAnyLoading()
    {
        var recipe=RecipeDefinitionSerialization.Deserialize(JsonDocument.Parse(File.ReadAllText(Path.Combine(TestConfiguration.Workspace(),
            "specs/014-special-part-rotation/examples/software-joint/catalog.json"))).RootElement.GetProperty("definitions")[1].GetRawText());
        recipe=recipe with {SchemaVersion=RecipeDefinitionSerialization.CurrentSchema,RecipeId="basis-refusal-component",Version="component-1"};
        recipe=recipe with {DefinitionDigest=RecipeDefinitionIdentity.ComputeDefinitionDigest(recipe)};
        var plan=RecipeRunPlanner.BuildExecutable(recipe,trayId.ToString("D"),["s2","s1"]);
        var clock=new FakeTimeProvider(new DateTimeOffset(2026,9,22,0,0,0,TimeSpan.Zero));
        var store=new RecordingEventStore(clock,runId,trayId,stationId,lineId);
        var allocator=new SortingTargetAllocator(store,clock,2000);
        var request=DetectionRequest(plan) with {InitialObservationWriteId=Guid.NewGuid(),
            ExpectedObjects=plan.OriginalSlots!.Values.Select(o=>new DetectionObjectExpectation(o.EntityId,plan.ExecutionPositions[o.SlotId].PhysicalEntity.Source!.Point,"part")).ToArray(),
            InitialObservation=DetectionRequest(plan).InitialObservation! with {Slots=[new(1,TrayPresence.Present,TrayPose.Normal),new(2,TrayPresence.Present,TrayPose.Normal)]}};
        var detection=new RecordingDetectionPort([]);
        var plc=new RecordingPlcPort(allocator,clock);
        var executor=new ThreeStageWorkflowExecutor(detection,plc,new RecipeSortingMapper(store,clock),store,allocator,clock,new AdvancingDelay(clock));
        var result=await executor.ExecuteAsync(new(request,plan,mappingOperationId,unloadOperationId,
            request.MotionConfiguration!.Motion.Points.Unload,request.MotionConfiguration.Motion.PositionTolerance,"Test","component-snapshot"));
        Assert.Equal(ThreeStageExecutionStatus.PausedForManualReview,result.Status);
        Assert.Equal("RotationMechanicalBasisMissing",result.ErrorCode);
        Assert.Empty(detection.Calls);Assert.Empty(plc.Calls);
        Assert.Single(store.Events,e=>e.EventType==StageEventType.ManualReviewRequested&&e.ErrorCode=="RotationMechanicalBasisMissing");
        Assert.DoesNotContain(store.Events,e=>e.EventType==StageEventType.Completed||e.PayloadJson.Contains("UnitCycleCompleted",StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SpecialReturnFailureOrMissingSafeCannotCompleteTheUnitOrStartTheNext(bool omitSafe)
    {
        var recipe=RecipeDefinitionSerialization.Deserialize(JsonDocument.Parse(File.ReadAllText(Path.Combine(TestConfiguration.Workspace(),
            "specs/014-special-part-rotation/examples/software-joint/catalog.json"))).RootElement.GetProperty("definitions")[1].GetRawText());
        recipe=recipe with {SchemaVersion=RecipeDefinitionSerialization.CurrentSchema,RecipeId="component-special",Version="component-1"};
        recipe=recipe with {DefinitionDigest=RecipeDefinitionIdentity.ComputeDefinitionDigest(recipe)};
        var plan=RecipeRunPlanner.BuildExecutable(recipe,trayId.ToString("D"),["s2","s1"]);
        var started=new DateTimeOffset(2026,9,22,0,0,0,TimeSpan.Zero);var clock=new FakeTimeProvider(started);
        var store=new RecordingEventStore(clock,runId,trayId,stationId,lineId);var allocator=new SortingTargetAllocator(store,clock,2000);
        var request=DetectionRequest(plan) with {Plan=plan,InitialObservationWriteId=Guid.NewGuid(),
            MotionConfiguration=JsonSerializer.Deserialize<PublicConfiguration>(File.ReadAllText(Path.Combine(TestConfiguration.Workspace(),
                "specs/014-special-part-rotation/examples/software-joint/config/public.json")),new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            ExpectedObjects=plan.OriginalSlots!.Values.Select(o=>new DetectionObjectExpectation(o.EntityId,plan.ExecutionPositions[o.SlotId].PhysicalEntity.Source!.Point,"part")).ToArray(),
            InitialObservation=DetectionRequest(plan).InitialObservation! with {Slots=[new(1,TrayPresence.Present,TrayPose.Normal),new(2,TrayPresence.Present,TrayPose.Normal)]}};
        var objects=plan.OriginalSlots!.Values.Select(o=>new DetectionObjectResult(o.EntityId,plan.ExecutionPositions[o.SlotId].PhysicalEntity.Source!.Point,"part") {Disposition="OK"}).ToArray();
        var detection=new RecordingDetectionPort([DetectionResult(request,DetectionResultKind.Completed,objects)]);
        detection.Transform=r=>DetectionResult(r.Request,DetectionResultKind.Completed,objects.Where(o=>o.ObjectId==plan.OriginalSlots[r.Request.Scope!.SlotId].EntityId).ToArray());
        var plc=new RecordingPlcPort(allocator,clock);
        plc.Handler=r=>r.TransferPurpose!=TransferPurpose.ReturnToOrigin?Result(r,StageActionKind.Completed):
            omitSafe?Result(r,StageActionKind.Completed) with {Evidence=Result(r,StageActionKind.Completed).Evidence! with {SafeReached=null}}:
                Result(r,StageActionKind.Failed,"ControlledReturnFailure",holds:true);
        var executor=new ThreeStageWorkflowExecutor(detection,plc,new RecipeSortingMapper(store,clock),store,allocator,clock,new AdvancingDelay(clock),
            rotationConfiguration:new(new(.01,"Test","014 component basis; no hardware claim")));
        var result=await executor.ExecuteAsync(new(request,plan,mappingOperationId,unloadOperationId,
            request.MotionConfiguration!.Motion.Points.Unload,request.MotionConfiguration.Motion.PositionTolerance,"Test","component-snapshot"));
        Assert.NotEqual(ThreeStageExecutionStatus.UnitCompleted,result.Status);Assert.NotEqual(ThreeStageExecutionStatus.StagesCompleted,result.Status);
        Assert.Single(detection.Calls);Assert.All(plc.Calls,c=>Assert.Equal("s1",c.Scope!.SlotId));
        Assert.DoesNotContain(store.Events,e=>e.PayloadJson.Contains("UnitCycleCompleted",StringComparison.Ordinal));
        Assert.DoesNotContain(plc.Calls,c=>c.Stage==PlcWorkflowStage.UnloadPreparation);
    }

    [Fact]
    public async Task TwoSpecialUnitCyclesHaveDistinctDurableEventKeysAndOneTrayUnload()
    {
        var recipe=RecipeDefinitionSerialization.Deserialize(JsonDocument.Parse(File.ReadAllText(Path.Combine(TestConfiguration.Workspace(),
            "specs/014-special-part-rotation/examples/software-joint/catalog.json"))).RootElement.GetProperty("definitions")[1].GetRawText());
        recipe=recipe with {SchemaVersion=RecipeDefinitionSerialization.CurrentSchema,RecipeId="component-special",Version="component-1"};
        recipe=recipe with {DefinitionDigest=RecipeDefinitionIdentity.ComputeDefinitionDigest(recipe)};
        var plan=RecipeRunPlanner.BuildExecutable(recipe,trayId.ToString("D"),["s2","s1"]);
        var started=new DateTimeOffset(2026,9,22,0,0,0,TimeSpan.Zero);var clock=new FakeTimeProvider(started);
        var store=new RecordingEventStore(clock,runId,trayId,stationId,lineId);var allocator=new SortingTargetAllocator(store,clock,2000);
        var request=DetectionRequest(plan) with {Plan=plan,InitialObservationWriteId=Guid.NewGuid(),
            MotionConfiguration=JsonSerializer.Deserialize<PublicConfiguration>(File.ReadAllText(Path.Combine(TestConfiguration.Workspace(),
                "specs/014-special-part-rotation/examples/software-joint/config/public.json")),new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            ExpectedObjects=plan.OriginalSlots!.Values.Select(o=>new DetectionObjectExpectation(o.EntityId,plan.ExecutionPositions[o.SlotId].PhysicalEntity.Source!.Point,"part")).ToArray(),
            InitialObservation=DetectionRequest(plan).InitialObservation! with {Slots=[new(1,TrayPresence.Present,TrayPose.Normal),new(2,TrayPresence.Present,TrayPose.Normal)]}};
        var frozen=new FrozenExecutionInputs(FrozenExecutionInputs.CurrentSchema,runId,trayId,RecipePlanRevision.Compute(plan),plan,
            new Dictionary<string,BoundCapability>(),new("component-cost","1","Test","component","component/1","component-digest",5000,10000,5000,24100,23000)
                {CaptureWaitMs=8000,AlgorithmWaitMs=15000,InputReleaseWaitMs=2000},"");
        frozen=frozen with {SemanticDigest=frozen.ComputeDigest()};
        request=request with {Inputs=frozen};
        var objects=plan.OriginalSlots!.Values.Select(o=>new DetectionObjectResult(o.EntityId,plan.ExecutionPositions[o.SlotId].PhysicalEntity.Source!.Point,"part") {Disposition="OK"}).ToArray();
        var detection=new RecordingDetectionPort([DetectionResult(request,DetectionResultKind.Completed,objects)]);
        detection.Transform=r=>DetectionResult(r.Request,DetectionResultKind.Completed,objects.Where(o=>o.ObjectId==plan.OriginalSlots[r.Request.Scope!.SlotId].EntityId).ToArray());
        var plc=new RecordingPlcPort(allocator,clock);
        plc.Handler=r=>Result(r,StageActionKind.Completed);
        var executor=new ThreeStageWorkflowExecutor(detection,plc,new RecipeSortingMapper(store,clock),store,allocator,clock,new AdvancingDelay(clock),
            rotationConfiguration:new(new(.01,"Test","014 component basis; no hardware claim")));
        var result=await executor.ExecuteAsync(new(request,plan,mappingOperationId,unloadOperationId,
            request.MotionConfiguration!.Motion.Points.Unload,request.MotionConfiguration.Motion.PositionTolerance,"Test","component-snapshot"));
        Assert.Equal(ThreeStageExecutionStatus.StagesCompleted,result.Status);
        Assert.Equal(new[] {"s1","s2"},detection.Calls.Select(x=>x.Scope!.SlotId));
        var unitFacts=store.Events.Where(e=>e.PayloadJson.Contains("DetectionUnitCompleted",StringComparison.Ordinal)).ToArray();
        Assert.Equal(2,unitFacts.Length);
        Assert.Equal(unitFacts.Length,unitFacts.Select(e=>e.IdempotencyKey).Distinct().Count());
        Assert.Equal(store.Events.Count,store.Events.Select(e=>e.IdempotencyKey).Distinct().Count());
        Assert.Single(plc.Calls,c=>c.Stage==PlcWorkflowStage.UnloadPreparation);
        Assert.Equal(2,plc.Calls.Count(c=>c.TransferPurpose==TransferPurpose.ReturnToOrigin));
    }

    [Fact]
    public async Task CompleteMappingExecutesOnlyDetectionSortingAndUnloadPreparationInOrder()
    {
        var fixture = Fixture(DetectionResultKind.Completed);

        var result = await fixture.Executor.ExecuteAsync(Request());

        Assert.Equal(ThreeStageExecutionStatus.StagesCompleted, result.Status);
        var hostIntents = fixture.Store.Events.Where(e => e.Stage == WholeTrayWorkflowStage.Detection &&
            e.EventType is StageEventType.IntentRecorded or StageEventType.Started).ToArray();
        Assert.Equal(2, hostIntents.Length);
        Assert.All(hostIntents, e => { Assert.Equal(ResultSource.HostDerived, e.Source);
            Assert.Equal(ResultQuality.Derived, e.Quality); });
        var completed = Assert.Single(fixture.Store.Events, e =>
            e.Stage == WholeTrayWorkflowStage.Detection && e.EventType == StageEventType.Completed);
        using var evidence = JsonDocument.Parse(completed.PayloadJson);
        Assert.Equal("ProductInspection", evidence.RootElement.GetProperty("EvidenceBasis").GetString());

        Assert.Single(fixture.Detection.Calls);
        Assert.Equal([PlcWorkflowStage.Sorting, PlcWorkflowStage.UnloadPreparation],
            fixture.Plc.Calls.Select(x => x.Stage));
        Assert.DoesNotContain(fixture.Plc.Calls, x => x.Stage == PlcWorkflowStage.UnlockObservation);
        Assert.All(fixture.Store.Events, x => Assert.Contains(x.Stage,
            new[] { WholeTrayWorkflowStage.Detection, WholeTrayWorkflowStage.Sorting,
                WholeTrayWorkflowStage.UnloadPreparation }));
        Assert.DoesNotContain(fixture.Store.Events, x => x.EventType is
            StageEventType.UnlockRequested or StageEventType.ObservedUnlocked or
            StageEventType.ManualTrayRemovalConfirmed or StageEventType.FinalUnloadCompleted);
        Assert.Equal([PlcWorkflowStage.Sorting, PlcWorkflowStage.UnloadPreparation],
            result.PlcActions.Select(x => x.PlcStage));
        Assert.Equal([WholeTrayWorkflowStage.Sorting, WholeTrayWorkflowStage.UnloadPreparation],
            result.PlcActions.Select(x => x.WorkflowStage));
    }

    [Fact]
    public async Task SpecialNormalUnitReturnsSafelyBeforeAbnormalUnitTransfersFromOriginalSlot()
    {
        using var catalog=JsonDocument.Parse(File.ReadAllText(Path.Combine(TestConfiguration.Workspace(),
            "specs/014-special-part-rotation/examples/software-joint/catalog.json")));
        var recipe=RecipeDefinitionSerialization.Deserialize(catalog.RootElement.GetProperty("definitions")[1].GetRawText()) with {
            SchemaVersion=RecipeDefinitionSerialization.CurrentSchema,RecipeId="016-special-component",Version="1"};
        recipe=recipe with {DefinitionDigest=RecipeDefinitionIdentity.ComputeDefinitionDigest(recipe)};
        var plan=RecipeRunPlanner.BuildExecutable(recipe,trayId.ToString("D"),["s2","s1"]);
        var clock=new FakeTimeProvider(new DateTimeOffset(2026,10,6,0,0,0,TimeSpan.Zero));
        var store=new RecordingEventStore(clock,runId,trayId,stationId,lineId);
        var allocator=new SortingTargetAllocator(store,clock,2000);
        var config=JsonSerializer.Deserialize<PublicConfiguration>(File.ReadAllText(Path.Combine(TestConfiguration.Workspace(),
            "specs/014-special-part-rotation/examples/software-joint/config/public.json")),new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var cost=new ExecutionCostProfile("declared","1","Test","UpperIsolation only","declared","declared",5000,10000,5000,3000,0) {
            CaptureWaitMs=8000,AlgorithmWaitMs=15000,InputReleaseWaitMs=2000};
        var inputs=new FrozenExecutionInputs(FrozenExecutionInputs.CurrentSchema,runId,trayId,RecipePlanRevision.Compute(plan),plan,
            new Dictionary<string,BoundCapability>(),cost,"");
        inputs=inputs with {SemanticDigest=inputs.ComputeDigest()};
        var request=DetectionRequest(plan) with {PlanRevision=inputs.PlanRevision,Inputs=inputs,MotionConfiguration=config,
            InitialObservationWriteId=Guid.NewGuid(),DeadlineUtc=clock.GetUtcNow().AddMinutes(5),
            ExpectedObjects=plan.OriginalSlots!.Values.Select(o=>new DetectionObjectExpectation(o.EntityId,plan.ExecutionPositions[o.SlotId].PhysicalEntity.Source!.Point,"part")).ToArray()};
        request=request with {InitialObservation=request.InitialObservation! with {Slots=plan.OriginalSlots.Values.Select(o=>
            new TraySlotObservation(o.PhysicalSlotIndex,TrayPresence.Present,o.SlotId=="s1"?TrayPose.Normal:TrayPose.Abnormal) {
                CellId=o.CellId,Region="OK",Row=o.Row,Column=o.Column }).ToArray(),SchemaVersion="tray-observation/2",
            ExpectedPhysicalSlotIndices=[1,2],MappingSourceReference="Test:declared-special-map"}};
        var detection=new RecordingDetectionPort([DetectionResult(request,DetectionResultKind.Completed,[])]);
        detection.Transform=r=> {
            var original=plan.OriginalSlots[r.Request.Scope!.SlotId];
            var result=DetectionResult(r.Request,DetectionResultKind.Completed,original.SlotId=="s1"?
                [new DetectionObjectResult(original.EntityId,plan.ExecutionPositions[original.SlotId].PhysicalEntity.Source!.Point,"part","OK")]:[]);
            return original.SlotId=="s1"?result:result with {EvidenceBasis=DetectionEvidenceBasis.InitialPoseExclusion,
                PosePending=[new(original.EntityId,original.PhysicalSlotIndex,request.InitialObservation.ObservationId,
                    $"write://{request.InitialObservationWriteId:D}","NotInspected","Declared pose anomaly")]};
        };
        var plc=new RecordingPlcPort(allocator,clock);
        var executor=new ThreeStageWorkflowExecutor(detection,plc,new RecipeSortingMapper(store,clock),store,allocator,clock,new AdvancingDelay(clock),
            rotationConfiguration:new(new(.01,"Test","Declared component basis; no hardware claim")));
        var result=await executor.ExecuteAsync(new(request,plan,mappingOperationId,unloadOperationId,config.Motion.Points.Unload,
            config.Motion.PositionTolerance,"Test","component-snapshot"));
        Assert.True(result.Status==ThreeStageExecutionStatus.StagesCompleted,result.ErrorCode);
        Assert.Equal(["s1","s2"],detection.Calls.Select(c=>c.Scope!.SlotId));
        Assert.Equal([TransferPurpose.RotationLoading,TransferPurpose.ReturnToOrigin,TransferPurpose.Sorting],
            plc.Calls.Where(c=>c.Stage!=PlcWorkflowStage.UnloadPreparation).Select(c=>c.TransferPurpose));
        var pending=Assert.Single(plc.Calls,c=>c.Scope?.SlotId=="s2");
        Assert.Equal(plan.ExecutionPositions["s2"].PhysicalEntity.Source!.Point,pending.SortingSource);
        Assert.DoesNotContain(plc.Calls,c=>c.Scope?.SlotId=="s2"&&c.TransferPurpose is TransferPurpose.RotationLoading or TransferPurpose.ReturnToOrigin);
        Assert.Equal(PlcWorkflowStage.UnloadPreparation,plc.Calls[^1].Stage);
    }

    [Fact]
    public async Task MappingFailedPausesDetectionAndNeverCallsPlcPort()
    {
        var fixture = Fixture(DetectionResultKind.Completed, detectionObjects:
            [Object("object-a", "P01", "ClassA")], includeSecondSortStep: true);

        var result = await fixture.Executor.ExecuteAsync(Request(includeSecondSortStep: true));

        Assert.Equal(ThreeStageExecutionStatus.PausedForManualReview, result.Status);
        Assert.Empty(fixture.Plc.Calls);
        Assert.Contains(fixture.Store.Events, x => x.EventType == StageEventType.MappingFailed &&
            x.Stage == WholeTrayWorkflowStage.Detection);
        Assert.DoesNotContain(fixture.Store.Events, x => x.Stage is WholeTrayWorkflowStage.Sorting or
            WholeTrayWorkflowStage.UnloadPreparation);
    }

    [Fact]
    public async Task MissingSortingTargetBlocksBeforeUnloadMotion()
    {
        var fixture = Fixture(DetectionResultKind.Completed);
        var original = Request();
        var missingTarget = original with { Plan = original.Plan with {
            ExecutionPositions = new Dictionary<string, SlotExecutionInputs>() } };

        var result = await fixture.Executor.ExecuteAsync(missingTarget);

        Assert.Equal(ThreeStageExecutionStatus.PausedForManualReview, result.Status);
        Assert.Equal("SortingTargetUnconfigured", result.ErrorCode);
        Assert.Empty(fixture.Plc.Calls);
        Assert.Contains(fixture.Store.Events, x => x.Stage == WholeTrayWorkflowStage.Sorting &&
            x.EventType == StageEventType.ManualReviewRequested);
    }

    [Fact]
    public async Task TwoProblemEntitiesCannotReserveTheSameTargetCell()
    {
        var fixture = Fixture(DetectionResultKind.Completed, includeSecondSortStep: true);
        var result = await fixture.Executor.ExecuteAsync(Request(includeSecondSortStep: true));
        Assert.Equal("SortingTargetCapacityExceeded", result.ErrorCode);
        Assert.Empty(fixture.Plc.Calls);
    }

    [Fact]
    public async Task OccupiedSourceCellCannotBeAssignedAsSortingDestination()
    {
        var fixture = Fixture(DetectionResultKind.Completed);
        var request = Request();
        request = request with { Plan = request.Plan with { ExecutionPositions =
            new Dictionary<string, SlotExecutionInputs> { ["P01"] = SemanticPlanFixture.Sorting("P01", "P01") } } };
        var result = await fixture.Executor.ExecuteAsync(request);
        Assert.Equal("SortingTargetOccupied", result.ErrorCode);
        Assert.Empty(fixture.Plc.Calls);
    }

    [Fact]
    public async Task ReservationSaveFailurePreventsEvenUnloadDispatch()
    {
        var fixture = Fixture(DetectionResultKind.Completed);
        fixture.Store.FailPayloadKind = "SortingAssignmentsReserved";
        await Assert.ThrowsAsync<IOException>(() => fixture.Executor.ExecuteAsync(Request()));
        Assert.Empty(fixture.Plc.Calls);
        Assert.DoesNotContain(fixture.Store.Events, e => e.Stage == WholeTrayWorkflowStage.UnloadPreparation);
    }

    [Fact]
    public async Task MixedTrayCommitsNormalMemberRetentionWithoutAnotherSortingAction()
    {
        var fixture = Fixture(DetectionResultKind.Completed, detectionObjects:
            [Object("object-a", "P01", "ClassA"), Object("object-b", "P02", "ClassB") with { Disposition = "OK" }],
            includeSecondSortStep: true);
        var result = await fixture.Executor.ExecuteAsync(Request(includeSecondSortStep: true));
        Assert.Equal(ThreeStageExecutionStatus.StagesCompleted, result.Status);
        Assert.Single(fixture.Plc.Calls, x => x.Stage == PlcWorkflowStage.Sorting);
        var reservation = Assert.Single(fixture.Store.Events, x => x.PayloadJson.Contains("SortingAssignmentsReserved"));
        using var doc = JsonDocument.Parse(reservation.PayloadJson);
        Assert.Equal("object-b", Assert.Single(doc.RootElement.GetProperty("ordinaryOk").EnumerateArray()).GetString());
        Assert.Equal("object-a", Assert.Single(doc.RootElement.GetProperty("assignments").EnumerateArray())
            .GetProperty("objectId").GetString());
    }

    [Fact]
    public async Task CommunicationFailuresUseInitialAttemptPlusThreeRetriesAndOneTwoFourBackoff()
    {
        var fixture = Fixture(DetectionResultKind.Disconnected, DetectionResultKind.Disconnected,
            DetectionResultKind.Disconnected, DetectionResultKind.Completed);

        var result = await fixture.Executor.ExecuteAsync(Request());

        Assert.Equal(ThreeStageExecutionStatus.StagesCompleted, result.Status);
        Assert.Equal(4, fixture.Detection.Calls.Count);
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)],
            fixture.Delay.Delays);
        Assert.Single(fixture.Plc.Calls, x => x.Stage == PlcWorkflowStage.Sorting);
    }

    [Fact]
    public async Task AlgorithmTimeoutsUseInitialAttemptPlusTwoRetriesAndTwoFiveBackoff()
    {
        var fixture = Fixture(DetectionResultKind.TimedOut, DetectionResultKind.TimedOut,
            DetectionResultKind.Completed);

        var result = await fixture.Executor.ExecuteAsync(Request());

        Assert.Equal(ThreeStageExecutionStatus.StagesCompleted, result.Status);
        Assert.Equal(3, fixture.Detection.Calls.Count);
        Assert.Equal([TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5)], fixture.Delay.Delays);
    }

    [Fact]
    public async Task SharedStageDeadlineStopsRetryBeforeItCanCrossOneHundredTwentySeconds()
    {
        var fixture = Fixture(DetectionResultKind.Disconnected, DetectionResultKind.Completed);
        fixture.Detection.BeforeResult = call =>
        {
            if (call == 1) fixture.Clock.Advance(TimeSpan.FromSeconds(119));
        };

        var result = await fixture.Executor.ExecuteAsync(Request());

        Assert.Equal(ThreeStageExecutionStatus.StagesCompleted, result.Status);
        Assert.Single(fixture.Detection.Calls);
        Assert.Equal([TimeSpan.FromSeconds(1)], fixture.Delay.Delays);
        Assert.Equal([PlcWorkflowStage.Sorting, PlcWorkflowStage.UnloadPreparation],
            fixture.Plc.Calls.Select(x => x.Stage));
        Assert.Equal(TimeSpan.FromSeconds(120), fixture.Clock.GetUtcNow() - fixture.StartedAt);
        var pending = Assert.Single(fixture.Store.Events, e => e.EventType == StageEventType.PendingRecorded);
        using var document = JsonDocument.Parse(pending.PayloadJson);
        var fact = Assert.Single(document.RootElement.GetProperty("CaptureFacts").EnumerateArray());
        Assert.Equal(runId, fact.GetProperty("RunId").GetGuid());
        Assert.Equal("DeclaredCamera/1", fact.GetProperty("CameraOrigin").GetProperty("VersionRef").GetString());
    }

    [Fact]
    public async Task DetectionExecutionAtStageDeadlineCannotDispatchSorting()
    {
        var fixture = Fixture(DetectionResultKind.Completed);
        fixture.Detection.BeforeResult = _ => fixture.Clock.Advance(TimeSpan.FromSeconds(120));

        var result = await fixture.Executor.ExecuteAsync(Request());

        Assert.Equal(ThreeStageExecutionStatus.StagesCompleted, result.Status);
        Assert.Equal([PlcWorkflowStage.Sorting, PlcWorkflowStage.UnloadPreparation],
            fixture.Plc.Calls.Select(x => x.Stage));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingRetainsPoseExclusionAndMissingObservationCannotAuthorizeSorting(bool missingObservation)
    {
        var fixture = Fixture(DetectionResultKind.Disconnected, includeSecondSortStep: true);
        fixture.Detection.BeforeResult = _ => fixture.Clock.Advance(TimeSpan.FromSeconds(120));
        fixture.Detection.Transform = result =>
        {
            var observed = result.LastObservation! with { Slots = [
                new(1, TrayPresence.Present, TrayPose.Abnormal), new(2, TrayPresence.Present, TrayPose.Normal)] };
            return result with { LastObservation = missingObservation ? null : observed,
                SlotParticipation = SlotParticipation.Apply([], observed.Slots),
                PosePending = [new("object-a",1,observed.ObservationId,"unit://declared-observation","NotInspected","Declared abnormal pose")] };
        };
        var request=Request(includeSecondSortStep: true);
        var positions=request.Plan.ExecutionPositions.ToDictionary(p=>p.Key,p=>p.Value);
        positions["P01"]=positions["P01"] with {PhysicalEntity=positions["P01"].PhysicalEntity with {
            Source=new(new("P01","coordinate-v1",1,2,"mm","tray",150),"unit://declared-source"),
            Sorting=new Dictionary<string,HandlingPoint>(positions["P01"].PhysicalEntity.Sorting) {
                ["Pending"]=new(new("P16","test-v1",450,250,"mm","SIM_MACHINE",150),"unit://declared-second-pending") } }};
        request=request with {Plan=request.Plan with {ExecutionPositions=positions}};
        var result = await fixture.Executor.ExecuteAsync(request);
        if (missingObservation)
        {
            Assert.Equal(ThreeStageExecutionStatus.PausedForManualReview, result.Status);
            Assert.Equal("PendingObservationCoverageUnknown", result.ErrorCode);
            Assert.Empty(fixture.Plc.Calls);
            Assert.DoesNotContain(fixture.Store.Events, e => e.EventType == StageEventType.PendingRecorded);
            return;
        }
        Assert.True(result.Status==ThreeStageExecutionStatus.StagesCompleted,result.ErrorCode);
        var sorting = fixture.Plc.Calls.Where(c => c.Stage == PlcWorkflowStage.Sorting).ToArray();
        Assert.Equal(["object-b","object-a"],sorting.Select(c=>c.Correlation.ObjectId));
        using var payload = JsonDocument.Parse(Assert.Single(fixture.Store.Events,
            e => e.EventType == StageEventType.PendingRecorded).PayloadJson);
        Assert.Equal("object-b", Assert.Single(payload.RootElement.GetProperty("objects").EnumerateArray())
            .GetProperty("ObjectId").GetString());
        Assert.Equal([PlcWorkflowStage.Sorting,PlcWorkflowStage.Sorting, PlcWorkflowStage.UnloadPreparation], fixture.Plc.Calls.Select(c => c.Stage));
    }

    [Fact]
    public async Task PlcUnknownHeldKeepsAssociationAndNeverRetriesOrAdvances()
    {
        var fixture = Fixture(DetectionResultKind.Completed);
        fixture.Plc.Handler = request => request.Stage == PlcWorkflowStage.Sorting
            ? Result(request, StageActionKind.UnknownHeld, "ReadbackTimeout", true, false)
            : Result(request, StageActionKind.Completed);

        var result = await fixture.Executor.ExecuteAsync(Request());

        Assert.Equal(ThreeStageExecutionStatus.UnknownHeld, result.Status);
        Assert.Equal([PlcWorkflowStage.Sorting],
            fixture.Plc.Calls.Select(x => x.Stage));
        var sortingAction = result.PlcActions.Last();
        Assert.Equal(PlcWorkflowStage.Sorting, sortingAction.PlcStage);
        Assert.Equal(WholeTrayWorkflowStage.Sorting, sortingAction.WorkflowStage);
        Assert.Equal(fixture.Plc.Calls[0].OperationId, sortingAction.OperationId);
        Assert.Equal(fixture.Plc.Calls[0].ConnectionEpoch, sortingAction.ConnectionEpoch);
        var projection = fixture.Store.Projections[WholeTrayWorkflowStage.Sorting];
        Assert.Equal(StageProjectionStatus.UnknownHeld, projection.Status);
        Assert.True(projection.DeviceHeld);
        Assert.False(projection.AutomaticRetryAllowed);
        Assert.DoesNotContain(fixture.Store.Events, x => x.EventType == StageEventType.Completed &&
            x.Stage == WholeTrayWorkflowStage.Sorting);
        Assert.DoesNotContain(fixture.Plc.Calls, x => x.Stage is PlcWorkflowStage.UnloadPreparation or PlcWorkflowStage.UnlockObservation);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DetectionMotionUnknownRemainsHeldBeforeAndAfterStageDeadline(bool atDeadline)
    {
        var fixture = Fixture(DetectionResultKind.UnknownHeld);
        if (atDeadline) fixture.Detection.BeforeResult = _ => fixture.Clock.Advance(TimeSpan.FromSeconds(120));
        var result = await fixture.Executor.ExecuteAsync(Request());
        Assert.Equal(ThreeStageExecutionStatus.UnknownHeld, result.Status);
        Assert.Empty(fixture.Plc.Calls);
        var projection = fixture.Store.Projections[WholeTrayWorkflowStage.Detection];
        Assert.Equal(StageProjectionStatus.UnknownHeld, projection.Status);
        Assert.True(projection.DeviceHeld);
        Assert.False(projection.AutomaticRetryAllowed);
        Assert.DoesNotContain(fixture.Store.Events, e => e.EventType is StageEventType.PendingRecorded or
            StageEventType.RetryScheduled or StageEventType.Completed);
        Assert.Single(fixture.Store.Events, e => e.EventType == StageEventType.UnknownHeld);
    }

    [Theory]
    [InlineData("F04-COMPONENT/physical-failure/false", false)]
    [InlineData("F04-COMPONENT/physical-failure/true", true)]
    public async Task FailedPhysicalDetectionAtDeadlineCannotBecomePendingOrDispatchUnload(string caseId, bool afterDeadline)
    {
        var fixture = Fixture(DetectionResultKind.Failed);
        fixture.Detection.BeforeResult = _ => fixture.Clock.Advance(
            TimeSpan.FromSeconds(afterDeadline ? 121 : 120));

        var result = await fixture.Executor.ExecuteAsync(Request());

        Assert.Equal(ThreeStageExecutionStatus.Failed, result.Status);
        Assert.Empty(fixture.Plc.Calls);
        Assert.DoesNotContain(fixture.Store.Events, e => e.EventType is
            StageEventType.PendingRecorded or StageEventType.Completed);
        Assert.Contains(fixture.Store.Events, e => e.Stage == WholeTrayWorkflowStage.Detection &&
            e.EventType == StageEventType.Failed && e.ErrorCode == "Failed");
        Assert.StartsWith("F04-COMPONENT/", caseId);
    }

    [Fact]
    public async Task StaleOperationOrEpochFeedbackBecomesUnknownHeldAndCannotCompleteCurrentAction()
    {
        var fixture = Fixture(DetectionResultKind.Completed);
        fixture.Plc.Handler = request =>
        {
            var stale = request with { Correlation = request.Correlation with { OperationId = Guid.NewGuid(), ConnectionEpoch = request.ConnectionEpoch - 1 } };
            return Result(stale, StageActionKind.Completed) with
            {
                ConnectionEpoch = stale.ConnectionEpoch
            };
        };

        var result = await fixture.Executor.ExecuteAsync(Request());

        Assert.Equal(ThreeStageExecutionStatus.UnknownHeld, result.Status);
        Assert.Single(fixture.Plc.Calls);
        Assert.Equal("StalePlcFeedback", result.ErrorCode);
        Assert.DoesNotContain(fixture.Store.Events, x => x.EventType == StageEventType.Completed &&
            x.Stage == WholeTrayWorkflowStage.Sorting);
    }

    [Fact]
    public void DetectionDisconnectedRemainsDetectionFailureInsteadOfPlcUnknownHeld()
    {
        var now = DateTimeOffset.UtcNow;
        var evt = new StageEvent(Guid.NewGuid(), runId, trayId, stationId.ToString(), lineId.ToString(),
            WholeTrayWorkflowStage.Detection, detectionOperationId, 1, 42, StageEventType.Disconnected,
            now, now, ResultSource.Simulated, ResultQuality.Unknown, "DetectionDisconnected", "digest", "{}",
            "detection-disconnected", 1, now.AddYears(7));

        var projection = StageEventProjection.Apply(StageEventProjection.Initial(evt), evt);

        Assert.Equal(StageProjectionStatus.Disconnected, projection.Status);
        Assert.False(projection.DeviceHeld);
        Assert.True(projection.AutomaticRetryAllowed);
    }

    [Fact]
    public async Task RecoveryBlocksUnknownPlcActionAndPreservesItsOperationAndEpoch()
    {
        var fixture = Fixture(DetectionResultKind.Completed);
        fixture.Store.Seed(WholeTrayWorkflowStage.Detection, StageProjectionStatus.Completed,
            detectionOperationId, 42, deviceHeld: false);
        var currentOperation = Guid.NewGuid();
        fixture.Store.Seed(WholeTrayWorkflowStage.Sorting, StageProjectionStatus.UnknownHeld,
            currentOperation, 42, deviceHeld: true);
        var service = new ThreeStageRecoveryService(fixture.Store);

        var plan = await service.InspectAsync(runId, trayId);

        Assert.False(plan.CanResume);
        Assert.True(plan.RequiresManualReview);
        Assert.Equal(WholeTrayWorkflowStage.Sorting, plan.CurrentStage);
        Assert.Equal(PlcWorkflowStage.Sorting, plan.PlcAction!.PlcStage);
        Assert.Equal(currentOperation, plan.PlcAction.OperationId);
        Assert.Equal(42, plan.PlcAction.ConnectionEpoch);
        Assert.True(plan.PlcAction.DeviceHeld);
        Assert.False(plan.PlcAction.AutomaticRetryAllowed);
        Assert.Empty(fixture.Plc.Calls);
    }

    private FixtureState Fixture(params DetectionResultKind[] kinds) => Fixture(kinds, null, false);

    private FixtureState Fixture(DetectionResultKind first, IReadOnlyList<DetectionObjectResult>? detectionObjects = null,
        bool includeSecondSortStep = false) => Fixture([first], detectionObjects, includeSecondSortStep);

    private FixtureState Fixture(DetectionResultKind[] kinds,
        IReadOnlyList<DetectionObjectResult>? detectionObjects, bool includeSecondSortStep)
    {
        var startedAt = new DateTimeOffset(2026, 9, 22, 0, 0, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(startedAt);
        var store = new RecordingEventStore(clock, runId, trayId, stationId, lineId);
        var plan = Plan(includeSecondSortStep);
        var request = DetectionRequest(plan);
        var objects = detectionObjects ?? (includeSecondSortStep
            ? [Object("object-a", "P01", "ClassA"), Object("object-b", "P02", "ClassB")]
            : [Object("object-a", "P01", "ClassA")]);
        var detection = new RecordingDetectionPort(kinds.Select(kind => DetectionResult(request, kind, objects)).ToArray());
        var allocator = new SortingTargetAllocator(store, clock, 2000);
        var plc = new RecordingPlcPort(allocator, clock);
        var delay = new AdvancingDelay(clock);
        var mapper = new RecipeSortingMapper(store, clock);
        var executor = new ThreeStageWorkflowExecutor(detection, plc, mapper, store, allocator, clock, delay);
        return new(executor, detection, plc, store, clock, delay, startedAt);
    }

    private ThreeStageExecutionRequest Request(bool includeSecondSortStep = false)
    {
        var plan = Plan(includeSecondSortStep);
        var detection = DetectionRequest(plan);
        return new(detection, plan, mappingOperationId, unloadOperationId,
            UnloadTarget: detection.MotionConfiguration!.Motion.Points.Unload,
            PositionTolerance: detection.MotionConfiguration.Motion.PositionTolerance,
            TargetPurpose: "Test", ConfigSnapshotId: detection.SnapshotId);
    }

    private DetectionRequest DetectionRequest(RecipeRunPlan plan) => new(runId, trayId, stationId, lineId,
        WholeTrayWorkflowStage.Detection, detectionOperationId, plan.RecipeVersion, 42,
        new DateTimeOffset(2026, 9, 22, 0, 2, 0, TimeSpan.Zero), ["media-1"], "Test", "detection-1")
    { ExpectedObjects = plan.Steps.Select(x => new DetectionObjectExpectation(x.MemberId!,
        new FixedPoint(x.SlotId!, "coordinate-v1", 1, 2, "mm", "tray"), x.Material!)).ToArray(),
      InitialObservation = new(Guid.NewGuid(), runId, trayId, Guid.NewGuid(), Guid.NewGuid(),
        new DateTimeOffset(2026, 9, 22, 0, 0, 0, TimeSpan.Zero), TrayObservationPurpose.InitialPreparation, 1, null,
        plan.Steps.Select(x => new TraySlotObservation(x.PhysicalSlotIndex!.Value, TrayPresence.Present, TrayPose.Normal)).ToArray(),
        new FLocation(1, 2, "mm", "tray", "unit://declared-observation"),
        new(ComponentEvidenceSource.Simulated, "DeclaredObservation/1", "Component"), ["unit://declared-observation"]),
      SessionId = Guid.NewGuid(), SnapshotId = "unit-snapshot", ClockId = "unit-clock", MotionConfiguration = JsonSerializer.Deserialize<PublicConfiguration>(File.ReadAllText(
        Path.Combine(TestConfiguration.Workspace(), "specs", "007-station01-integrated-loop",
            "examples", "public.virtual-loop.json")), new JsonSerializerOptions(JsonSerializerDefaults.Web)) };

    private static DetectionPortResult DetectionResult(DetectionRequest request, DetectionResultKind kind,
        IReadOnlyList<DetectionObjectResult> objects) => new(request, kind,
        kind == DetectionResultKind.Completed ? "result-1" : null,
        kind == DetectionResultKind.Completed ? objects : [], ResultSource.Simulated,
        kind == DetectionResultKind.Completed ? ResultQuality.Degraded : ResultQuality.Unknown,
        kind == DetectionResultKind.Completed ? null : kind.ToString(), request.DeadlineUtc)
        { LastObservation = request.InitialObservation,
          SlotParticipation = SlotParticipation.Apply([], request.InitialObservation!.Slots),
          EvidenceBasis = DetectionEvidenceBasis.ProductInspection, CaptureFacts = [new(request.RunId, Guid.NewGuid(), request.OperationId, request.ConnectionEpoch,
            "declared-settings", "Simulated", new(ComponentEvidenceSource.Simulated, "DeclaredCamera/1", "Declared"),
            new(ComponentEvidenceSource.Simulated, "DeclaredLight/1", "Declared"),
            CaptureApplicationState.ConfiguredOnly, null, false, ["unit://capture-fact"])] };

    private static PlcStageActionResult Result(PlcStageActionRequest request, StageActionKind kind,
        string? error = null, bool holds = false, bool retry = false) => SemanticStageFixture.Result(
            request, kind, request.Window.StartedUtc, error, holds, retry);

    private static DetectionObjectResult Object(string id, string position, string classification) =>
        new(id, new FixedPoint(position, "coordinate-v1", 1, 2, "mm", "tray"), classification);

    private static RecipeRunPlan Plan(bool includeSecond) => new(
        "tray-run", "scenario", "F001", "recipe", "recipe-v1", "catalog", "simulationOnly", "unit",
        1, null, false, new RecipeDisposition("OK", "NG", "Pending", "unit"),
        "motion", "quality", new Dictionary<string, CaptureProfile>(),
        new Dictionary<string, SlotExecutionInputs> {
            ["P01"] = SemanticPlanFixture.Sorting("P01"),
            ["P02"] = SemanticPlanFixture.Sorting("P02")
        }, [],
        includeSecond
            ? [SortStep(1, "unit-a", "object-a", "P01", "ClassA"),
               SortStep(2, "unit-b", "object-b", "P02", "ClassB")]
            : [SortStep(1, "unit-a", "object-a", "P01", "ClassA")], 1) { Model = "ComponentModel", DefinitionDigest = "component-digest", ECode = new(false, null, null, false, null), Approval = SemanticPlanFixture.Approval, AlgorithmRequirements = new Dictionary<string, AlgorithmRequirement>() };

    private static RecipeStep SortStep(int sequence, string unitId, string objectId, string slot,
        string classification) => new(sequence, RecipeStepKind.SortUnit, unitId, objectId, slot,
        classification, null, null, null, 1) { PhysicalSlotIndex = int.Parse(slot[1..]) };

    private sealed record FixtureState(ThreeStageWorkflowExecutor Executor, RecordingDetectionPort Detection,
        RecordingPlcPort Plc, RecordingEventStore Store, FakeTimeProvider Clock, AdvancingDelay Delay,
        DateTimeOffset StartedAt);

    private sealed class RecordingDetectionPort(IReadOnlyList<DetectionPortResult> results) : IDetectionPort
    {
        public List<DetectionRequest> Calls { get; } = [];
        public Action<int>? BeforeResult { get; set; }
        public Func<DetectionPortResult, DetectionPortResult>? Transform { get; set; }

        public ValueTask<DetectionPortResult> ExecuteAsync(DetectionRequest request,
            CancellationToken cancellationToken)
        {
            Calls.Add(request);
            BeforeResult?.Invoke(Calls.Count);
            var index = Math.Min(Calls.Count - 1, results.Count - 1);
            var result = results[index] with { Request = request };
            return ValueTask.FromResult(Transform is null ? result : Transform(result));
        }
    }

    private sealed class RecordingPlcPort(SortingTargetAllocator allocator, FakeTimeProvider clock) : IPlcStageActionPort
    {
        public List<PlcStageActionRequest> Calls { get; } = [];
        public Func<PlcStageActionRequest, PlcStageActionResult> Handler { get; set; } = request =>
            Result(request, StageActionKind.Completed);

        public async ValueTask<PlcStageActionResult> ExecuteAsync(PlcStageActionRequest request,
            CancellationToken cancellationToken)
        {
            Calls.Add(request);
            var result = Handler(request);
            if (request.Stage is PlcWorkflowStage.Sorting or PlcWorkflowStage.TransferToRotation && result.IsCompleted)
                await SemanticStageFixture.CommitPickAsync(allocator, request, clock, cancellationToken);
            return result;
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

    private sealed class RecordingEventStore(TimeProvider clock, Guid runId, Guid trayId,
        Guid stationId, Guid lineId) : IStageEventStore
    {
        public List<StageEvent> Events { get; } = [];
        public string? FailPayloadKind { get; set; }
        public Dictionary<WholeTrayWorkflowStage, StageProjection> Projections { get; } = [];

        public Task<StageEventAppendResult> AppendAsync(StageEventAppendRequest request,
            CancellationToken cancellationToken = default)
        {
            if (FailPayloadKind is { } kind && request.PayloadJson.Contains(kind, StringComparison.Ordinal))
                throw new IOException("ControlledNecessarySortingSaveFailure");
            var sequence = Events.Count(x => x.Stage == request.Stage) + 1;
            var now = clock.GetUtcNow();
            var evt = new StageEvent(request.EventId, request.RunId, request.TrayId, request.StationId,
                request.LineId, request.Stage, request.OperationId, request.Attempt, request.ConnectionEpoch,
                request.EventType, request.OccurredAt, now, request.Source, request.Quality, request.ErrorCode,
                request.PayloadDigest, request.PayloadJson, request.IdempotencyKey, sequence, now.AddYears(7),
                request.PlanRevision, request.StageStartedAtUtc, request.StageDeadlineAtUtc);
            var projection = StageEventProjection.Apply(
                Projections.TryGetValue(request.Stage, out var existing)
                    ? existing : StageEventProjection.Initial(evt), evt);
            Events.Add(evt);
            Projections[request.Stage] = projection;
            return Task.FromResult(new StageEventAppendResult(StageEventCommitState.Committed, evt,
                projection, null));
        }

        public Task<IReadOnlyList<StageEvent>> ReadAsync(Guid runId, Guid trayId,
            WholeTrayWorkflowStage stage, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StageEvent>>(Events.Where(x => x.RunId == runId &&
                x.TrayId == trayId && x.Stage == stage).ToArray());

        public Task<StageProjection?> GetProjectionAsync(Guid runId, Guid trayId,
            WholeTrayWorkflowStage stage, CancellationToken cancellationToken = default) =>
            Task.FromResult(Projections.GetValueOrDefault(stage));

        public Task<StageProjection> RecoverAsync(Guid runId, Guid trayId, WholeTrayWorkflowStage stage,
            CancellationToken cancellationToken = default) => Task.FromResult(Projections[stage]);

        public void Seed(WholeTrayWorkflowStage stage, StageProjectionStatus status, Guid operationId,
            long epoch, bool deviceHeld)
        {
            var now = clock.GetUtcNow();
            Projections[stage] = new StageProjection(runId, trayId, stationId.ToString(), lineId.ToString(),
                stage, 1, status, operationId, epoch, deviceHeld,
                status == StageProjectionStatus.PausedForManualReview, Guid.NewGuid(), now, now.AddYears(7));
        }
    }
}
