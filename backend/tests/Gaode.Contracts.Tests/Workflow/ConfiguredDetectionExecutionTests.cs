using System.Text.Json;
using System.Diagnostics;
using Gaode.Application.Capabilities;
using Gaode.Application.Motion;
using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Application.Workflow;
using Gaode.Application.Station01;
using Gaode.Contracts.Tests.Recipes;
using Gaode.Contracts.Tests.Support;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Media;
using Gaode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Gaode.Contracts.Tests.Workflow;

// Business component only: declared semantic device/camera/algorithm responses,
// with actual media files and SQLite writes. No claim of hardware or worker execution.
public sealed class ConfiguredDetectionExecutionTests
{
    [Theory]
    [InlineData(6, false, false, false, 12, 5, "OK", false, false, false, false)]
    [InlineData(4, true, false, false, 8, 4, "OK", false, false, false, false)]
    [InlineData(4, true, true, false, 8, 4, "Pending", false, false, false, false)]
    [InlineData(2, false, false, true, 2, 1, null, false, false, false, false)]
    [InlineData(2, false, false, false, 2, 1, null, true, false, false, false)]
    [InlineData(1, false, false, false, 0, 0, null, false, true, false, false)]
    [InlineData(1, false, false, false, 0, 0, null, false, true, true, false)]
    [InlineData(1, false, false, false, 0, 0, null, false, false, false, true)]
    public async Task FrozenConfigurationDrivesFacesScanAndPoseExit(int faces, bool extraE,
        bool missingCode, bool abnormalRecheck, int captureCount, int flips, string? disposition, bool cancelAfterPutBack,
        bool initiallyExcluded, bool omitInitialCapture, bool holdMoveAcceptance)
        => await Execute(faces, extraE, missingCode, abnormalRecheck, captureCount, flips, disposition,
            cancelAfterPutBack, initiallyExcluded, omitInitialCapture, holdMoveAcceptance, false);

    [Fact]
    public async Task ActualDetectionEntryUsesEveryFrozenCameraAndEScanParameter()
        => await Execute(4, true, false, false, 8, 4, "OK", false, false, false, false, true);

    private async Task Execute(int faces, bool extraE, bool missingCode, bool abnormalRecheck, int captureCount,
        int flips, string? disposition, bool cancelAfterPutBack, bool initiallyExcluded, bool omitInitialCapture,
        bool holdMoveAcceptance, bool localParameters)
    {
        var workerCapacity=Gaode.Infrastructure.Diagnostics.HostWorkerCapacity.Ensure();
        var workspace = TestConfiguration.Workspace();
        var root = Path.Combine(Gaode.Testing.ApprovedTestRoot.Resolve(workspace), "011-configured-detection", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root,"fixture-runtime-policy.json"),JsonSerializer.Serialize(new {
            scope="DeclaredComponentRuntimeSetupOnly;NotProductExecutionEvidence",
            source="ActualHostStartup/HostWorkerCapacity.Ensure",
            originalWorkerMinimum=workerCapacity.PreviousMinimum,effectiveWorkerMinimum=workerCapacity.EffectiveMinimum,
            processorCount=Environment.ProcessorCount,windowsNative=workerCapacity.WindowsNative,
            criticalSaveBudgetMs=TestConfiguration.Normal().Budget.BusinessMs.CriticalSave,
            asynchronousWriter="UnchangedChannelConsumerAndTaskRunCommit;ActualSQLite" }));
        var run = Guid.NewGuid(); var tray = Guid.NewGuid();
        using var diagnostics=new FlowFailureCollector(run,root);
        var recipe = Recipe011Data.Candidate(faces, extraE) with { RecipeId = "component", Version = "1" };
        if (localParameters)
        {
            var profiles = recipe.CaptureProfiles.ToDictionary(p => p.Key, p => p.Value);
            var n = 0;
            string Profile(string key)
            {
                n++; var basis = profiles["detect"];
                profiles[key] = basis with { Id = key, Settings = basis.Settings with
                    { ProfileId = key, ExposureUs = 110 + n * 41, Gain = 1 + n / 10d, BrightnessPercent = n * 6 } };
                return key;
            }
            var slot = recipe.ExecutionPositions["s1"];
            var input = slot.PhysicalEntity with
            {
                Coordinates = slot.PhysicalEntity.Coordinates.Select(c => c with { CaptureProfile = Profile(c.PointRef) }).ToArray(),
                PurposePoints = slot.PhysicalEntity.PurposePoints.ToDictionary(p => p.Key, p => p.Value.Purpose == RecipePointPurpose.EScan
                    ? p.Value with { CaptureProfile = Profile("actual-E") } : p.Value)
            };
            recipe = recipe with { CaptureProfiles = profiles, ExecutionPositions = new Dictionary<string, SlotExecutionInputs>
                { ["s1"] = slot with { PhysicalEntity = input } } };
        }
        recipe = recipe with { DefinitionDigest = RecipeDefinitionIdentity.ComputeDefinitionDigest(recipe) };
        var plan = RecipeRunPlanner.BuildExecutable(recipe, tray.ToString("D"), ["s1"]);
        var config = Recipe011Data.Motion();
        config = config with { Motion = config.Motion with { Points = config.Motion.Points with
            { ThreeD = config.Motion.Points.ThreeD with { Frame = config.Motion.Frame } } } };
        var budget = TestConfiguration.Normal().Budget with { BusinessMs = TestConfiguration.Normal().Budget.BusinessMs with
            { FlipCompletion = 1000, PutBackCompletion = 1000, TrayPoseAlgorithm = 1000 } };
        if (holdMoveAcceptance) budget = budget with { BusinessMs = budget.BusinessMs with { PlcAcceptance = 50 } };
        using var cancellation = new CancellationTokenSource();
        var ports = new DeclaredPorts(abnormalRecheck, missingCode, cancelAfterPutBack ? cancellation.Cancel : null, holdMoveAcceptance);
        var registry = new CapabilityRegistry();
        foreach (var req in recipe.AlgorithmRequirements.Values)
            registry.RegisterAlgorithm(req.Purpose, req.Id, "1", req.ResultContract, req.InputCount,
                "declared-component", ports.Origin.VersionRef!, "Test", "DeclaredComponentOnly");
        registry.RegisterAlgorithm(AlgorithmPurpose.TrayPose, "tray-pose", "1", "tray-observation/2", 1,
            "declared-component", ports.Origin.VersionRef!, "Test", "DeclaredComponentOnly");
        var cost = new ExecutionCostProfile("component", "1", "Test", "Declared component timing",
            $"{budget.Id}/{budget.Version}", "component-budget", 5000, 10000, 5000, 3000, 0)
            { CaptureWaitMs = 8000, AlgorithmWaitMs = 15000, InputReleaseWaitMs = 2000 };
        var inputs = RecipeAdmission.Freeze(run, tray, plan, registry, cost, "Test",
            new(new("tray-pose", "1"), "component", "1"));
        var positions = plan.ExecutionPositions["s1"].PhysicalEntity;
        var targets = plan.Steps.Where(s => s.Kind == RecipeStepKind.PositionForCapture).Select(s =>
        {
            var coordinate = positions.Coordinates.SingleOrDefault(c => c.PointRef == s.PointRef);
            var point = coordinate is null ? CoordinateResolver.Purpose(positions, s.PointRef, RecipePointPurpose.EScan).Point :
                new FixedPoint(coordinate.Point.Id, coordinate.Point.Version, coordinate.Point.X, coordinate.Point.Y,
                    coordinate.Point.Unit, coordinate.Point.Frame, coordinate.Fixed.Z);
            return new DetectionStepTarget(s.Sequence, s.MemberId!, s.SlotId!, s.PhysicalSlotIndex!.Value,
                s.LocalFace, s.CoordinateEpoch, s.Camera!, s.PointRef!, point, "DeclaredComponentConfiguration", "Test-only")
                { ResolutionKind = CoordinateResolutionKind.ApprovedFixed, StageId = s.StageId, ScanPoseId = s.ScanPoseId };
        }).ToArray();
        var options = new DbContextOptionsBuilder<Station01DbContext>().UseSqlite($"Data Source={Path.Combine(root, "run.db")}").Options;
        await using (var setup = new Station01DbContext(options))
        {
            await setup.Database.MigrateAsync();
            setup.Runs.Add(new RunEntity { RunId = run, RequestId = "declared-component", SubjectId = "Test", CreatedUtc = DateTimeOffset.UtcNow });
            await setup.SaveChangesAsync();
        }
        await using var writer = new TraceWriter(options, TimeProvider.System, 128);
        var events = new StageEventStore(options);
        var media = new MediaStore(root, new(32 * 1024 * 1024, 4 * 1024 * 1024, 32 * 1024 * 1024, 32 * 1024 * 1024), new MediaLeaseRegistry(), 1);
        var lease = new ResourceLease(); Assert.True(lease.TryHold(run));
        var motion = new MotionCoordinator(ports, ports, ports, ports, lease);
        var initial = ports.Observation(run, tray, Guid.NewGuid(), Guid.NewGuid(), null);
        long revision = 0;
        async Task<CommitReceipt> SaveInitial(WriteKind kind, object payload)
        {
            var json = JsonSerializer.Serialize(payload);
            var saved = await writer.SubmitCritical(new(Guid.NewGuid(), run, revision, kind, json,
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json))))).Completion;
            Assert.True(saved.State == CommitState.Committed, $"{kind}: {saved.ErrorCode}; evidence={root}");
            revision = saved.CommittedRevision!.Value;
            return saved;
        }
        IReadOnlyList<string> initialMediaReferences = ["declared-initial-observation"];
        CorrelatedCaptureFact? initialCaptureFact = null;
        if (initiallyExcluded)
        {
            initial = initial with { Slots = [initial.Slots[0] with {Pose=TrayPose.Abnormal,Reason="DeclaredTestPose"}] };
            var envelope = new PortEnvelope(run, Guid.NewGuid(), 1, Guid.NewGuid(), "initial", "1", "Test", 1, 2, "declared");
            var intent = await SaveInitial(WriteKind.CaptureIntent, new OperationIntentPayload(envelope.OperationId,
                "Capture3D", 1, null, initial.CaptureId, "scope", envelope.SnapshotId, Guid.Empty));
            var command = new CaptureRequest(envelope, initial.CaptureId, CaptureRole.ThreeD, "initial3d", "1",
                "scope", "1", "camera", "light", intent.WriteId, 1024);
            initialCaptureFact = new(run, initial.CaptureId, envelope.OperationId, 1,
                AcquisitionContract.RequestedSettingsDigest(command), "Test", ports.CameraOrigin, ports.LightOrigin,
                CaptureApplicationState.ConfiguredOnly, null, false, ["DeclaredInitialBuffer"]);
            using var reservation = media.ReserveCapture(initial.CaptureId, "3D", 1024);
            var savedMedia = await media.SaveAsync(run, initial.CaptureId, "3D", "1", "1", [7, 8], "bin", "Test", default);
            await SaveInitial(WriteKind.Media, savedMedia);
            initialMediaReferences = [$"media://{savedMedia.MediaId:D}"];
            if (!omitInitialCapture)
                await SaveInitial(WriteKind.CaptureFact, new { captureId = initial.CaptureId,
                    operationId = envelope.OperationId, mediaId = savedMedia.MediaId, ended = true, mediaTaken = true,
                    requestedCapture = command, captureFact = initialCaptureFact });
            await SaveInitial(WriteKind.AlgorithmIntent, new AlgorithmIntentPayload(initial.CallId, Guid.NewGuid(),
                1, run, initial.CaptureId, [savedMedia.MediaId], "1", "1", "1", "tray-pose", "1",
                ports.Origin.VersionRef!, envelope.SessionId, "declared", 1, 2, 1, "DeclaredComponentInput"));
        }
        var initialCommit = initiallyExcluded
            ? await SaveInitial(WriteKind.AlgorithmFact,
                new AlgorithmFactPayload(initial.CallId, AlgorithmState.Success, true, JsonSerializer.Serialize(initial),
                    "DeclaredComponentObservation", "Accepted") { RunId = run, CaptureId = initial.CaptureId, Origin = initial.Source })
            : await SaveInitial(WriteKind.ActionFact, new { kind = "DeclaredInitialObservation", observation = initial });
        var deadlines = RecipeExecutionBudget.Freeze(plan, budget, DateTimeOffset.UtcNow, cost);
        var request = new DetectionRequest(run, tray, Guid.NewGuid(), Guid.NewGuid(), WholeTrayWorkflowStage.Detection,
            Guid.NewGuid(), inputs.PlanRevision, 1, deadlines.DetectionDeadlineUtc, initialMediaReferences, "Test", "component",
            ExpectedObjects: [new(plan.Steps.First(s => s.Kind == RecipeStepKind.Capture).MemberId!, positions.Source!.Point, "part")],
            Plan: plan, MotionConfiguration: config, Targets: targets)
        { Inputs = inputs, SessionId = Guid.NewGuid(), SnapshotId = "declared-snapshot", ClockId = "system-component",
            CriticalSaveBudgetMs = budget.BusinessMs.CriticalSave, FrozenBusinessDurations = budget.BusinessMs,
            InitialObservation = initial, InitialObservationWriteId = initialCommit.WriteId, StageStartedAtUtc = deadlines.StartedUtc };
        var decisionCoordinator=abnormalRecheck?new Station01Coordinator(32,16,16):null;
        if(decisionCoordinator is not null) {
            decisionCoordinator.Start();
            Assert.True(decisionCoordinator.TryRegister(new(run,"declared-component","Test",RunState.Detection,0,0,TerminalOutcome.None,false,
                ActionState.NotRequested,CaptureState.NotRequested,AlgorithmState.NotRequested,SaveState.NotQueued,HandoffState.NotReady,null,null,null,[])));
        }
        var decisions=decisionCoordinator is null?null:new TrayAnomalyDecisionService(decisionCoordinator,TimeProvider.System);
        var executor = new RecipeDetectionExecutor(ports, ports, media, events, writer,
            new TraceQuery(options, TimeProvider.System, 2000), motion, ports,anomalyDecisions:decisions,coordinator:decisionCoordinator);
        async Task<DetectionPortResult> ExecuteAndDecide() {
            try {
                var executing=executor.ExecuteAsync(request,cancellation.Token).AsTask();
                if(decisionCoordinator is not null) {
                    var until=DateTimeOffset.UtcNow.AddSeconds(30);
                    while(decisionCoordinator.Query(run)?.TrayAnomalyDecision is null&&!executing.IsCompleted&&DateTimeOffset.UtcNow<until)await Task.Delay(10);
                    var pending=Assert.IsType<TrayAnomalyDecisionProjection>(decisionCoordinator.Query(run)?.TrayAnomalyDecision);
                    await decisions!.ChooseAsync(run,pending.DecisionId,"Continue","Test:component-operator",CancellationToken.None);
                }
                return await executing;
            } finally {if(decisionCoordinator is not null)await decisionCoordinator.StopConsumerAsync(CancellationToken.None);}
        }
        if (cancelAfterPutBack)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.ExecuteAsync(request, cancellation.Token).AsTask());
            Assert.Single(ports.Flips); Assert.Single(ports.PutBacks);
            Assert.Equal(2, ports.TriggerCount(CaptureRole.Detection));
            Assert.Equal(0, ports.CallCount(AlgorithmRole.TrayPose));
            Assert.True(lease.Unknown); Assert.Equal(run, lease.Owner);
            Assert.DoesNotContain(ports.Order, item => item == "capture:E" || item.StartsWith("observe:", StringComparison.Ordinal));
            await File.WriteAllTextAsync(Path.Combine(root, "cancelled.json"), JsonSerializer.Serialize(new
                { evidence = "DeclaredSemanticComponentWithActualSQLite", cancelled = cancellation.IsCancellationRequested, ports.Order, lease.Unknown }));
            return;
        }
        var result = await ExecuteAndDecide();
        await File.WriteAllTextAsync(Path.Combine(root, "result.json"), JsonSerializer.Serialize(new { evidence = "DeclaredSemanticComponentWithActualSQLite", result, ports.Order }));
        if (holdMoveAcceptance)
        {
            Assert.Equal(DetectionResultKind.UnknownHeld, result.Kind);
            Assert.Equal("DetectionTimedOut:ProductMoveAndBeginInspection", result.ErrorCode);
            Assert.True(ports.MoveToken.IsCancellationRequested);
            Assert.True(lease.Unknown); Assert.Equal(run, lease.Owner);
            Assert.Equal(["move:Detection"], ports.Order);
            Assert.Equal(0, ports.TriggerCount(CaptureRole.Detection));
            Assert.Empty(ports.Flips); Assert.Empty(ports.PutBacks);
            return;
        }
        if (initiallyExcluded)
        {
            Assert.Empty(ports.Order); Assert.Empty(result.Objects);
            Assert.Equal(SlotParticipationState.PoseExcluded, result.SlotParticipation[1].State);
            Assert.Equal(0, ports.CallCount(AlgorithmRole.Detection));
            Assert.Equal(0, ports.CallCount(AlgorithmRole.TrayPose));
            if (omitInitialCapture)
            {
                Assert.Equal(DetectionResultKind.Failed, result.Kind);
                Assert.Equal("InitialPoseExclusionEvidenceMissing", result.ErrorCode);
                Assert.False(result.AlgorithmOrigin.IsKnown); Assert.Empty(result.CaptureFacts);
            }
            else
            {
                Assert.Equal(DetectionResultKind.Completed, result.Kind);
                Assert.Equal(DetectionEvidenceBasis.InitialPoseExclusion, result.EvidenceBasis);
                Assert.Equal(initial.Source, result.AlgorithmOrigin);
                Assert.Equal(initialCaptureFact!.CaptureId, Assert.Single(result.CaptureFacts).CaptureId);
                Assert.Equal(initialCaptureFact.OperationId, result.CaptureFacts[0].OperationId);
            }
            return;
        }
        Assert.True(result.Kind == DetectionResultKind.Completed, $"{result.ErrorCode}; evidence={root}");
        Assert.Equal(captureCount, ports.TriggerCount(CaptureRole.Detection));
        if (localParameters)
        {
            var actual = ports.Requests.Where(r => r.Role is CaptureRole.Detection or CaptureRole.E).ToArray();
            Assert.Equal(9, actual.Length);
            Assert.Equal(9, actual.Select(r => r.DetectionSettings!.ProfileId).Distinct().Count());
            Assert.All(actual, r =>
            {
                var expected = plan.CaptureProfiles[r.DetectionSettings!.ProfileId].Settings;
                Assert.Equal((expected.ExposureUs, expected.Gain, expected.BrightnessPercent),
                    (r.DetectionSettings.ExposureUs, r.DetectionSettings.Gain, r.DetectionSettings.BrightnessPercent));
            });
            await File.WriteAllTextAsync(Path.Combine(root, "requested-parameters.json"), JsonSerializer.Serialize(new
                { evidence = "ActualRecipeDetectionExecutorEntry/DeclaredPorts/NoHardwareApplicationClaim", actual }));
        }
        Assert.Equal(flips, ports.Flips.Count); Assert.Equal(flips, ports.PutBacks.Count);
        Assert.Equal(flips, ports.CallCount(AlgorithmRole.TrayPose));
        Assert.Equal(extraE ? 1 : 0, ports.CallCount(AlgorithmRole.EDecode));
        Assert.Equal(0, ports.CallCount(AlgorithmRole.FDecode));
        Assert.False(lease.Unknown); Assert.Null(lease.CurrentAction);
        Assert.All(ports.Flips, f => Assert.True(f.Window.DeadlineUtc <= request.DeadlineUtc &&
            f.Window.DeadlineUtc - f.Window.StartedUtc <= TimeSpan.FromMilliseconds(1000)));
        if (disposition is null)
        {
            Assert.Empty(result.Objects);
            Assert.Equal(SlotParticipationState.PoseExcluded, result.SlotParticipation[1].State);
            var handling=Assert.Single(result.PosePending);
            Assert.Equal("FurtherInspectionTerminated",handling.DetectionState);
            var writes=await new TraceQuery(options,TimeProvider.System,2000).GetWritesAsync(run,default);
            Assert.Contains(writes,w=>w.Kind==WriteKind.AlgorithmFact&&handling.ObservationReference==$"write://{w.WriteId:D}"&&
                w.PayloadJson.Contains(handling.ObservationId.ToString("D")));
            Assert.Contains(writes,w=>w.Kind==WriteKind.AlgorithmFact&&w.PayloadJson.Contains("WorkerResult"));
            Assert.Contains(await events.ReadAsync(run,tray,WholeTrayWorkflowStage.Detection),e=>e.PayloadJson.Contains("DetectionImageCommitted"));
        }
        else Assert.Equal(disposition, Assert.Single(result.Objects).Disposition);
        if (extraE)
        {
            Assert.Equal("mark-access", ports.Flips.Last().TargetPose.PoseKey);
            Assert.True(ports.Order.LastIndexOf("capture:Detection") < ports.Order.IndexOf("capture:E"));
        }
        foreach (var flip in ports.Flips)
        {
            Assert.Contains(ports.PutBacks, p => p.TransitionId == flip.TransitionId);
            Assert.True(ports.Order.IndexOf("put:" + flip.TransitionId) < ports.Order.IndexOf("observe:" + flip.TransitionId));
        }
        await using var db = new Station01DbContext(options);
        Assert.Equal(captureCount + flips + (extraE ? 1 : 0), await db.Media.CountAsync(m => m.RunId == run));
        Assert.Contains(await db.Writes.Where(w => w.RunId == run && w.Kind == "AlgorithmFact").ToListAsync(),
            w => w.PayloadJson.Contains("PostPlacementObserved", StringComparison.Ordinal));
    }

    private sealed class FlowFailureCollector : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string,object?>>, IDisposable
    {
        private readonly Guid run;
        private readonly string path;
        private readonly Queue<string> recent=[];
        private readonly List<IDisposable> subscriptions=[];
        private readonly Process selfProcess=Process.GetCurrentProcess();
        private readonly Queue<string> gaps=[];
        private string? previousFrame;
        private long previousTick;
        private double previousCpuMs,previousGcPauseMs;
        private readonly Queue<object> observerDelays=[];
        private bool failed;
        public FlowFailureCollector(Guid run,string root) {this.run=run;path=Path.Combine(root,"runtime-errors.log");subscriptions.Add(DiagnosticListener.AllListeners.Subscribe(this));}
        public void OnNext(DiagnosticListener listener) {if(listener.Name=="Gaode.Runtime")subscriptions.Add(listener.Subscribe(this));}
        public void OnNext(KeyValuePair<string,object?> item) {
            if(item.Value is not IDictionary<string,object?> value||!value.TryGetValue("runId",out var id)||!Equals(id,run))return;
            var observerStarted=Stopwatch.GetTimestamp();
            try {
            lock(recent) {
                if(value["step"] is "DatabaseCommit" or "StageEventSave" or "DetectionSave") {
                    ThreadPool.GetAvailableThreads(out var availableWorkers,out var availableIo);
                    ThreadPool.GetMinThreads(out var minimumWorkers,out _);
                    var tick=(long)value["tick"]!;
                    var cpuMs=selfProcess.TotalProcessorTime.TotalMilliseconds;
                    var gcPauseMs=GC.GetTotalPauseDuration().TotalMilliseconds;
                    var frame=JsonSerializer.Serialize(new {utc=value["utc"],tick,step=value["step"],outcome=value["outcome"],
                        elapsed=value["elapsedMs"],facts=value["facts"],pendingWork=ThreadPool.PendingWorkItemCount,threads=ThreadPool.ThreadCount,
                        availableWorkers,availableIo,minimumWorkers,processorCount=Environment.ProcessorCount,
                        cpuMs,gcPauseMs,gen2=GC.CollectionCount(2),thread=Environment.CurrentManagedThreadId,
                        poolThread=Thread.CurrentThread.IsThreadPoolThread,context=SynchronizationContext.Current?.GetType().FullName});
                    if(previousFrame is not null && Stopwatch.GetElapsedTime(previousTick,tick).TotalMilliseconds>100) {
                        gaps.Enqueue(JsonSerializer.Serialize(new {previous=previousFrame,current=frame,
                            elapsedMs=Stopwatch.GetElapsedTime(previousTick,tick).TotalMilliseconds,
                            cpuDeltaMs=cpuMs-previousCpuMs,gcPauseDeltaMs=gcPauseMs-previousGcPauseMs,
                            evidence="IntervalBetweenDiagnosticsOnly;NotAutomaticRootCause"}));
                        while(gaps.Count>8)gaps.Dequeue();
                    }
                    previousFrame=frame;previousTick=tick;previousCpuMs=cpuMs;previousGcPauseMs=gcPauseMs;
                    recent.Enqueue(frame);
                    while(recent.Count>64)recent.Dequeue();
                }
                if(value.TryGetValue("exception",out var failure)&&failure is Exception error)
                {
                    failed=true;
                    File.WriteAllText(path,string.Join('\n',recent)+$"\n{value["utc"]} {value["step"]}/{value["outcome"]}: {error}\n");
                }
            }
            } finally {
                var elapsedMs=Stopwatch.GetElapsedTime(observerStarted).TotalMilliseconds;
                if(elapsedMs>100)lock(recent) {
                    observerDelays.Enqueue(new {step=value["step"],outcome=value["outcome"],facts=value["facts"],elapsedMs});
                    while(observerDelays.Count>8)observerDelays.Dequeue();
                }
            }
        }
        public void OnError(Exception error) { }
        public void OnCompleted() { }
        public void Dispose() {
            foreach(var subscription in subscriptions)subscription.Dispose();
            if(failed)lock(recent)File.AppendAllText(path,"\nWriter drained; bounded final diagnostic tail:\n"+string.Join('\n',recent)+"\n");
            lock(recent)File.WriteAllText(Path.Combine(Path.GetDirectoryName(path)!,"save-diagnostic-tail.jsonl"),string.Join('\n',recent)+"\n");
            lock(recent)File.WriteAllText(Path.Combine(Path.GetDirectoryName(path)!,"save-diagnostic-gaps.jsonl"),string.Join('\n',gaps)+"\n");
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(path)!,"save-observer-delays.json"),JsonSerializer.Serialize(observerDelays));
            selfProcess.Dispose();
        }
    }

    private sealed class DeclaredPorts(bool abnormal, bool missingCode, Action? afterPutBack, bool holdMoveAcceptance) : IPlcStatePort, IPlcActionPort,
        IMotionPort, IPhysicalHandlingPort, IAcquisitionCyclePort, ICapturePort, IAlgorithmPort
    {
        public ComponentExecutionOrigin Origin => new(ComponentEvidenceSource.Test, "DeclaredSemanticComponent/1", "DeclaredInput");
        public ComponentExecutionOrigin CameraOrigin => Origin;
        public ComponentExecutionOrigin LightOrigin => Origin;
        public string MediaSource => "Test";
        public long ConnectionEpoch => 1;
        public List<string> Order { get; } = [];
        public List<FlipRequest> Flips { get; } = [];
        public List<PutBackRequest> PutBacks { get; } = [];
        public List<CaptureRequest> Requests { get; } = [];
        private readonly List<CaptureRole> captures = [];
        private readonly List<AlgorithmRole> calls = [];
        private DeviceObservation observation = SemanticDeviceFixture.Ready(ClampState.Unconfirmed);
        public CancellationToken MoveToken { get; private set; }
        public DeviceObservation Observe() => observation;
        public int TriggerCount(CaptureRole role) => captures.Count(r => r == role);
        public int CallCount(AlgorithmRole role) => calls.Count(r => r == role);
        private DeviceActionEvidence Evidence(ActionCorrelation c, DeviceCompletionMeaning meaning,
            PositionReachedEvidence? position = null) => new(c, meaning, [observation.Identity!],
                position is null ? [] : [position], null, observation.ExecutionOrigin, [new(Guid.NewGuid(), Guid.NewGuid())]);
        public ValueTask RequestMoveAsync(MoveRequest request, Action<DeviceEvent> onEvent, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); Order.Add("move:" + request.Role);
            MoveToken = token;
            if (holdMoveAcceptance) return ValueTask.CompletedTask;
            var e = request.Envelope;
            var correlation = new ActionCorrelation(e.RunId, e.OperationId, request.ActionId, e.Attempt, e.SessionId, 1, e.SnapshotId);
            var purpose = request.Role is "Detection" ? "DetectionZ" : request.Role == "E" ? "ScanZ" : "XY";
            var point = new PositionObservation(request.Target.X, request.Target.Y, purpose == "XY" ? null : request.Target.Z,
                purpose, request.Target.Frame, request.Target.Unit, observation.Identity!, observation.ExecutionOrigin);
            observation = observation with { Position = point, AxisPositions = new(request.Target.X, request.Target.Y, request.Target.Z, request.Target.Z, null) };
            onEvent(new(e, DeviceEventKind.Accepted, request.ActionId, 1));
            onEvent(new(e, DeviceEventKind.Completed, request.ActionId, 1, Evidence:
                Evidence(correlation, DeviceCompletionMeaning.PositionReached, new(correlation, request.Target, point, 0.01))));
            return ValueTask.CompletedTask;
        }
        public Task<DeviceActionEvidence> FlipAsync(FlipRequest r, CancellationToken token)
        { token.ThrowIfCancellationRequested(); Flips.Add(r); Order.Add("flip:" + r.TransitionId); return Task.FromResult(Evidence(r.Correlation, DeviceCompletionMeaning.FlipCompleted) with { TransitionId = r.TransitionId }); }
        public Task<DeviceActionEvidence> PutBackAsync(PutBackRequest r, CancellationToken token)
        { token.ThrowIfCancellationRequested(); PutBacks.Add(r); Order.Add("put:" + r.TransitionId); afterPutBack?.Invoke(); return Task.FromResult(Evidence(r.Correlation, DeviceCompletionMeaning.PutBackCompleted) with { TransitionId = r.TransitionId }); }
        public ValueTask<AcquisitionSession> OpenCaptureWindowAsync(CaptureWindowRequest r, CancellationToken token) =>
            ValueTask.FromResult(new AcquisitionSession(Guid.NewGuid(), r, AcquisitionState.CaptureAllowed, Evidence(r.Correlation, DeviceCompletionMeaning.CaptureAllowed)));
        public Task<CaptureCycleResult> FinishCaptureWindowAsync(AcquisitionSession s, CaptureWorkCommit w, ActionWindow window, CancellationToken token)
        { Assert.True(w.MediaReleased); Assert.NotEmpty(w.WriteIds); return Task.FromResult(new CaptureCycleResult(s, AcquisitionState.Released, Evidence(s.Request.Correlation, DeviceCompletionMeaning.CaptureReleased), null)); }
        public ValueTask RequestCaptureAsync(CaptureRequest r, Action<CaptureEvent> onEvent, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); Assert.NotEqual(Guid.Empty, r.IntentWriteId); captures.Add(r.Role); Requests.Add(r); Order.Add("capture:" + r.Role);
            onEvent(new(r, CaptureEventKind.Ended, 1));
            onEvent(new(r, CaptureEventKind.MediaTaken, 1, [1, 2, 3, 4], "bin") { Fact = new(r.Envelope.RunId, r.CaptureId,
                r.Envelope.OperationId, 1, AcquisitionContract.RequestedSettingsDigest(r), "Test", Origin, Origin,
                CaptureApplicationState.ConfiguredOnly, null, false, ["DeclaredComponentBuffer"]) });
            return ValueTask.CompletedTask;
        }
        public TrayObservation Observation(Guid run, Guid tray, Guid capture, Guid call, TrayObservationContext? c) =>
            new(Guid.NewGuid(), run, tray, capture, call, DateTimeOffset.UtcNow, c?.Purpose ?? TrayObservationPurpose.InitialPreparation,
                c?.CheckRound ?? 1, c?.RelatedTransitionId, [new(1, TrayPresence.Present, c is not null && abnormal ? TrayPose.Abnormal : TrayPose.Normal) {
                    CellId="r1:c4",Region="OK",Row=1,Column=4 }],
                c is null ? new(10, 20, "mm", "test-frame", "DeclaredComponent") : null, Origin, ["DeclaredComponentObservation"]) {
                    SchemaVersion="tray-observation/2",MappingSourceReference="Test:declared-component-map",ExpectedPhysicalSlotIndices=[1] };
        public ValueTask<AlgorithmDispatch> RequestAsync(AlgorithmRequest r, Action<AlgorithmEvent> onEvent, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); calls.Add(r.Role);
            var result = new AlgorithmEvent(r, AlgorithmEventKind.Result, RawCodes: r.Role == AlgorithmRole.EDecode && !missingCode ? ["component-code"] : [],
                WorkerSessionId: Guid.NewGuid(), DetectionDisposition: "OK");
            if (r.Role == AlgorithmRole.TrayPose)
            { Order.Add("observe:" + r.ObservationContext!.RelatedTransitionId); result = result with { Observation = Observation(r.Envelope.RunId, r.ObservationContext.TrayId, r.CaptureId, r.CallId, r.ObservationContext) }; }
            onEvent(result); return ValueTask.FromResult(new AlgorithmDispatch(Task.CompletedTask));
        }
        public ValueTask RequestStartAsync(PortEnvelope e, Guid a, Guid i, Action<DeviceEvent> callback, CancellationToken token) => throw new NotSupportedException("No startup in this component");
        public ValueTask RequestStopAsync(PortEnvelope e, Action<DeviceEvent> callback, CancellationToken token) => throw new NotSupportedException();
        public Task<CaptureCycleResult> CloseFailedCaptureWindowAsync(AcquisitionSession s, string reason, ActionWindow w, CancellationToken t) => throw new NotSupportedException();
    }
}
