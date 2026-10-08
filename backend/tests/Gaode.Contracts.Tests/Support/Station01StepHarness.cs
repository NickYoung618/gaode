using Gaode.Application.Acquisition;
using Gaode.Application.Algorithms;
using Gaode.Application.Configuration;
using Gaode.Application.Motion;
using Gaode.Application.Ports;
using Gaode.Application.Station01;
using Gaode.Application.Station01.Steps;
using Gaode.Application.Timing;
using Gaode.Domain.Configuration;
using Gaode.Infrastructure.Media;
using Gaode.Infrastructure.Simulation;
using Microsoft.Extensions.Time.Testing;
using System.Threading.Channels;
using System.Text.Json;
using Gaode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Gaode.Contracts.Tests.Support;

public sealed class Station01StepHarness : IAsyncDisposable
{
    private Station01StepHarness(FrozenConfiguration config, RecordingWriter writer,
        RunExecution run, SimulatedPlc plc, SimulatedCapture capture,
        IAlgorithmPort algorithm, StartPreparationStep start, ThreeDStep threeD,
        FScanStep fScan, ControlLatch control, MotionCoordinator motion,
        OperationIngress ingress, FakeTimeProvider clock,
        SimulationEventScheduler scheduler, TraceWriter evidenceWriter)
    {
        Config = config;
        Writer = writer;
        Run = run;
        Plc = plc;
        Capture = capture;
        Algorithm = algorithm;
        Start = start;
        ThreeD = threeD;
        FScan = fScan;
        Control = control;
        Motion = motion;
        Ingress = ingress;
        Clock = clock;
        this.evidenceWriter = evidenceWriter;
        scheduler.DeliveryScheduled += (due, task) => deliveries.Writer.TryWrite((due, task));
    }

    public FrozenConfiguration Config { get; }
    public RecordingWriter Writer { get; }
    public RunExecution Run { get; }
    public SimulatedPlc Plc { get; }
    public SimulatedCapture Capture { get; }
    public IAlgorithmPort Algorithm { get; }
    public StartPreparationStep Start { get; }
    public ThreeDStep ThreeD { get; }
    public FScanStep FScan { get; }
    public ControlLatch Control { get; }
    public MotionCoordinator Motion { get; }
    public OperationIngress Ingress { get; }
    public FakeTimeProvider Clock { get; }
    private readonly TraceWriter evidenceWriter;
    public async ValueTask DisposeAsync() { Plc.Dispose(); await evidenceWriter.DisposeAsync(); }
    private readonly Channel<(long Due, Task Completion)> deliveries = Channel.CreateUnbounded<(long, Task)>();

    public async Task<T> DriveAsync<T>(Task<T> operation)
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        while (!operation.IsCompleted)
        {
            watchdog.Token.ThrowIfCancellationRequested();
            Clock.Advance(TimeSpan.FromMilliseconds(10));
            // A scheduled completion may itself await the real evidence writer. Let that
            // work finish before moving the component clock past its original deadline.
            while (deliveries.Reader.TryRead(out var delivery))
            {
                var remaining = delivery.Due - Clock.GetTimestamp();
                if (remaining > 0) Clock.Advance(TimeSpan.FromSeconds(remaining / (double)Clock.TimestampFrequency));
                await delivery.Completion.WaitAsync(watchdog.Token);
            }
            await Task.Delay(1, watchdog.Token);
        }
        return await operation;
    }

    public static Station01StepHarness Create(FakeTimeProvider? clock = null,
        SimulationProfile? simulationOverride = null,
        Gaode.Domain.Station01.ComponentExecutionOrigin? algorithmOrigin = null, string? mediaSourceOverride = null)
    {
        clock ??= new FakeTimeProvider();
        var loader = TestConfiguration.Loader();
        var simulation = loader.LoadSimulation(new("s01-sim-normal", "3.0.0"));
        if (simulationOverride is not null)
        {
            var json = JsonSerializer.Serialize(simulationOverride,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            simulation = simulation with { Value = simulationOverride, CanonicalJson = json,
                Digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))) };
        }
        var publicInput = loader.LoadPublic(new("s01-public-dev", "1.0.0"));
        var budgetInput = loader.LoadBudget(new("s01-budget-dev", "3.0.0"));
        publicInput = Changed(publicInput, publicInput.Value with { Algorithms = publicInput.Value.Algorithms with
            { TrayPose = new(new("tray.observation", "1.0"), "component-observer", "component/1") } });
        budgetInput = Changed(budgetInput, budgetInput.Value with { BusinessMs = budgetInput.Value.BusinessMs with { TrayPoseAlgorithm = 1000 } });
        var config = ConfigurationFreezer.Freeze(
            publicInput, budgetInput,
            simulation,
            new Dictionary<string, string>
            {
                ["xy.fixed"] = "1.0", ["capture.whole-tray"] = "1.0",
                ["capture.single-frame"] = "1.0", ["algorithm.height"] = "1.0",
                ["algorithm.f-decode"] = "1.0", ["code.test-tray-format"] = "1.0"
            });
        var scheduler = new SimulationEventScheduler(clock,
            config.Budget.Limits.MaxPendingTimerEvents);
        var root = Path.Combine(Gaode.Testing.ApprovedTestRoot.Resolve(TestConfiguration.Workspace()),
            "step-contract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var options = new DbContextOptionsBuilder<Station01DbContext>()
            .UseSqlite($"Data Source={Path.Combine(root, "step.test.db")};Pooling=False").Options;
        var storeId = Guid.NewGuid();
        using (var db = new Station01DbContext(options))
        {
            db.Database.Migrate();
            db.Manifests.Add(new() { StoreId = storeId, Profile = "Test", SchemaVersion = "s01-store/2",
                PrepareOperationId = Guid.NewGuid(), PreparedUtc = clock.GetUtcNow() });
            db.SaveChanges();
        }
        var evidenceWriter = new TraceWriter(options, clock, 32);
        var plc = StepSimulationFactory.Create(config, scheduler, evidenceWriter, storeId, clock);
        var frozenSimulation = config.Simulation ?? throw new InvalidOperationException("TestSimulationRequired");
        var capture = new SimulatedCapture(frozenSimulation, scheduler);
        IAlgorithmPort algorithm = new DeclaredObservationAlgorithm(new SimulatedAlgorithm(frozenSimulation, scheduler),
            scheduler, frozenSimulation.Stages.HeightAlgorithm, clock);
        var ingress = new OperationIngress(new DeadlineScheduler(clock, "step-contract"),
            config.Budget.Limits.LateEvidencePerOperation,
            config.Budget.Limits.DuplicateSummariesPerOperation);
        var lease = new ResourceLease();
        var motion = new MotionCoordinator(plc, plc, plc, plc, lease);
        var media = new MediaStore(root,
            new MediaCapacity(config.Budget.Limits.MediaMemoryBytes,
                config.Budget.Limits.FReservedMemoryBytes,
                config.Budget.Limits.RunMediaQuotaBytes,
                config.Budget.Limits.DataQuotaBytes), new MediaLeaseRegistry(),
            config.Budget.Limits.MediaJobs);
        var writer = new RecordingWriter();
        var run = new RunExecution(Guid.NewGuid(), Guid.NewGuid(), "step-contract", "test:Operator",
            JsonSerializer.Serialize(new { schemaVersion = StartRunContext.CurrentSchemaVersion, trayId = Guid.NewGuid(),
                stationId = "S01", lineId = "Component", scenarioId = "component", occupiedSlots = new[] { "s1", "s3" }, purpose = "Test" }),
            config, writer, clock, Guid.NewGuid(), "step-contract");
        run.AdoptInitialRevision(1);
        var fixedMove = new FixedMoveStep(motion, ingress);
        var acquisition = new AcquisitionCoordinator(mediaSourceOverride is null ? capture : new MediaSourceOverride(capture, mediaSourceOverride), media, ingress);
        var runtime = new AlgorithmRuntime(algorithmOrigin is null ? algorithm : new OriginOverride(algorithm, algorithmOrigin), media, ingress,
            new AlgorithmLeaseSupervisor(media), 1, 1);
        return new(config, writer, run, plc, capture, algorithm,
            new StartPreparationStep(motion, ingress), new ThreeDStep(fixedMove, acquisition, runtime, motion),
            new FScanStep(fixedMove, acquisition, runtime, motion, SemanticPlanFixture.Capabilities()), new ControlLatch(), motion, ingress, clock, scheduler, evidenceWriter);
    }

    private static LoadedConfiguration<T> Changed<T>(LoadedConfiguration<T> input, T value)
    {
        var json = JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return input with { Value = value, CanonicalJson = json, Digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))) };
    }

    // Explicit semantic component double, separate from the real-media worker proof.
    private sealed class DeclaredObservationAlgorithm(IAlgorithmPort inner, SimulationEventScheduler scheduler,
        SimStage timing, TimeProvider clock) : IAlgorithmPort
    {
        private int observations;
        public Gaode.Domain.Station01.ComponentExecutionOrigin Origin => inner.Origin;
        public int CallCount(AlgorithmRole role) => role == AlgorithmRole.TrayPose ? observations : inner.CallCount(role);
        public ValueTask<AlgorithmDispatch> RequestAsync(AlgorithmRequest request, Action<AlgorithmEvent> callback, CancellationToken token)
        {
            if (request.Role != AlgorithmRole.TrayPose) return inner.RequestAsync(request, callback, token);
            observations++;
            var context = request.ObservationContext ?? throw new InvalidOperationException("DeclaredObservationContextRequired");
            callback(new(request, AlgorithmEventKind.Accepted));
            var ended = scheduler.RespondTracked(timing, () => {
                var observation = new Gaode.Domain.Station01.TrayObservation(Guid.NewGuid(), request.Envelope.RunId, context.TrayId,
                    request.CaptureId, request.CallId, clock.GetUtcNow(), context.Purpose, context.CheckRound, context.RelatedTransitionId,
                    [new(1, Gaode.Domain.Station01.TrayPresence.Present, Gaode.Domain.Station01.TrayPose.Normal),
                     new(3, Gaode.Domain.Station01.TrayPresence.Present, Gaode.Domain.Station01.TrayPose.Abnormal)],
                    new(205, 105, "mm", "SIM_MACHINE", "DeclaredComponentOnly"), Origin, ["DeclaredComponentOnly"]);
                callback(new(request, AlgorithmEventKind.Result) { Observation = observation });
                callback(new(request, AlgorithmEventKind.InputReleased));
            }, token);
            return ValueTask.FromResult(new AlgorithmDispatch(ended));
        }
    }

    public async Task<StartPreparationEvidence> CompleteStartAsync()
    {
        var stages = new List<Gaode.Domain.Station01.RunState>();
        var task = Start.ExecuteAsync(Run, Control, state =>
        {
            stages.Add(state);
            return Task.CompletedTask;
        }, CancellationToken.None);
        var result = await DriveAsync(task);
        if (!stages.Contains(Gaode.Domain.Station01.RunState.WaitingStartAcceptance))
            throw new InvalidOperationException("未观察到启动就绪等待状态");
        return result;
    }

    private sealed class MediaSourceOverride(ICapturePort inner, string source) : ICapturePort
    {
        public long ConnectionEpoch => inner.ConnectionEpoch;
        public int TriggerCount(CaptureRole role) => inner.TriggerCount(role);
        public ValueTask RequestCaptureAsync(CaptureRequest request, Action<CaptureEvent> callback, CancellationToken token) =>
            inner.RequestCaptureAsync(request, value => callback(value.Fact is { } fact ? value with {
                Fact = fact with { MediaSource = source, ApplicationState = CaptureApplicationState.Unknown, ActualSettings = null }
            } : value), token);
    }

    private sealed class OriginOverride(IAlgorithmPort inner, Gaode.Domain.Station01.ComponentExecutionOrigin origin) : IAlgorithmPort
    {
        public Gaode.Domain.Station01.ComponentExecutionOrigin Origin => origin;
        public int CallCount(AlgorithmRole role) => inner.CallCount(role);
        public ValueTask<AlgorithmDispatch> RequestAsync(AlgorithmRequest request, Action<AlgorithmEvent> callback, CancellationToken token) =>
            inner.RequestAsync(request, value => callback(value.Observation is { } observed
                ? value with { Observation = observed with { Source = origin } } : value), token);
    }

    public sealed class RecordingWriter : ITraceWriter
    {
        private readonly object gate = new();
        private readonly List<WriteBatch> batches = [];
        public WriteKind? FailOnKind { get; set; }
        public IReadOnlyList<WriteBatch> Batches { get { lock (gate) return batches.ToArray(); } }
        public QueuedWrite SubmitCritical(WriteBatch batch, CancellationToken cancellationToken = default,
            Gaode.Domain.Station01.ActionWindow? window = null)
        {
            if (batch.Kind == FailOnKind) throw new IOException("InjectedNecessarySaveFailure");
            lock (gate) batches.Add(batch);
            var receipt = new CommitReceipt(batch.WriteId, batch.RunId, CommitState.Committed,
                batch.ExpectedRevision + 1, batch.CandidateTerminal, null);
            return new(new(batch.WriteId, batch.RunId, CommitState.Queued, null,
                Gaode.Domain.Station01.TerminalOutcome.None, null), Task.FromResult(receipt));
        }
        public Task<CommitReceipt?> ReconcileAsync(Guid writeId, CancellationToken cancellationToken) =>
            Task.FromResult<CommitReceipt?>(null);
    }
}
