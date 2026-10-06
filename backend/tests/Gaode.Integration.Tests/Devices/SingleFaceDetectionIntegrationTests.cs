using Gaode.Integration.Tests.Support;
using System.Text.Json;
using Gaode.Application.Motion;
using Gaode.Application.Workflow;
using Gaode.Host.Composition;
using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Application.Station01;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Algorithms;
using Gaode.Infrastructure.Media;
using Gaode.Infrastructure.Persistence;
using Gaode.Infrastructure.Recipes;
using Gaode.Infrastructure.Simulation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Gaode.Integration.Tests.Devices;

public sealed partial class SingleFaceDetectionIntegrationTests
{
    public enum FailureKind { None, WrongArrival, MediaSave, ReleaseUnavailable, SaveWindowExpiry }

    internal static async Task<string> RunAsync(string caseId, string[] slots, string pair,
        int captureCount, FailureKind failure = FailureKind.None)
    {
        var workspace = Station01HostFixture.FindWorkspace();
        var fixtureRoot = Path.Combine(workspace, "specs", "008-recipe-driven-inspection", "fixtures");
        var root = Path.Combine(Gaode.Testing.ApprovedTestRoot.Resolve(workspace),
            "009-detection-components",
            caseId.ToLowerInvariant() + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var catalog = new JsonRecipeCatalog(Path.Combine(fixtureRoot,
            caseId == "Q01" ? "recipes.json" : "recipes-q02.json"));
        var fCode = caseId == "Q01" ? "TEST-TRAY-0101" : "TEST-TRAY-0202";
        var trayId = Guid.NewGuid();
        var plan = RecipeRunPlanner.BuildExecutable(RecipeBindingTestSupport.Match(catalog, fCode, "S1"), trayId.ToString("D"), slots);
        // Explicit component-only target bindings. The restricted catalog on disk is untouched.
        plan = plan with { Steps = plan.Steps.Select(step => step.Kind == RecipeStepKind.PositionForCapture
            ? step with { PointRef = $"TEST-{caseId}-{step.SlotId}-{step.Camera}",
                PhysicalSlotIndex = int.Parse(step.SlotId![1..],
                    System.Globalization.CultureInfo.InvariantCulture) }
            : step).ToArray() };
        var revision = RecipePlanRevision.Compute(plan);
        var configPath = Path.Combine(workspace, "specs", "007-station01-integrated-loop",
            "examples", "public.virtual-loop.json");
        var config = JsonSerializer.Deserialize<PublicConfiguration>(File.ReadAllText(configPath),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var budget = JsonSerializer.Deserialize<BusinessBudget>(File.ReadAllText(Path.Combine(workspace,
            "specs", "007-station01-integrated-loop", "examples", "budget.virtual-loop.json")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var targets = plan.Steps.Where(step => step.Kind == RecipeStepKind.PositionForCapture)
            .Select((step, i) => new DetectionStepTarget(step.Sequence, step.MemberId ?? step.UnitId,
                step.SlotId!, step.PhysicalSlotIndex!.Value, step.LocalFace!.Value,
                step.CoordinateEpoch, step.Camera!, step.PointRef!,
                new FixedPoint(step.PointRef!, "component-test/1", 80 + i * 20, 90 + i * 10,
                    "mm", config.Motion.Frame, 100 + i * 5), "Test/InjectedXYZ",
                "Test/ExplicitXYZ-No3DConversion") { ResolutionKind = CoordinateResolutionKind.ApprovedFixed }).ToArray();
        var expected = plan.Steps.Where(step => step.Kind == RecipeStepKind.PositionForCapture)
            .GroupBy(step => step.MemberId ?? step.UnitId)
            .Select(group => new DetectionObjectExpectation(group.Key,
                targets.Single(target => target.StepSequence == group.First().Sequence).Point,
                group.First().Material!)).ToArray();
        var runId = Guid.NewGuid();
        await using var dbSetup = new Station01DbContext(new DbContextOptionsBuilder<Station01DbContext>()
            .UseSqlite($"Data Source={Path.Combine(root, "station01.db")}").Options);
        var dbOptions = new DbContextOptionsBuilder<Station01DbContext>()
            .UseSqlite($"Data Source={Path.Combine(root, "station01.db")}").Options;
        await dbSetup.Database.MigrateAsync();
        var storeId = Guid.NewGuid();
        dbSetup.Manifests.Add(new StoreManifestEntity { StoreId = storeId, SchemaVersion = "s01-store/2",
            Profile = "Test", PrepareOperationId = Guid.NewGuid(), PreparedUtc = DateTimeOffset.UtcNow });
        dbSetup.Runs.Add(new RunEntity { RunId = runId, RequestId = "fourth-batch-" + caseId,
            SubjectId = "Test/Component", CreatedUtc = DateTimeOffset.UtcNow });
        await dbSetup.SaveChangesAsync();
        await using var writer = new TraceWriter(dbOptions, TimeProvider.System, 128,
            beforeCommit: async (batch, token) =>
            {
                if (failure == FailureKind.SaveWindowExpiry && batch.Kind == WriteKind.Media)
                    await Task.Delay(budget.BusinessMs.CriticalSave + 500, token);
            });
        await using var communication=await DetectionCommunicationFixture.CreateAsync(writer,storeId,budget.BusinessMs.CriticalSave,runId,failure==FailureKind.ReleaseUnavailable);
        var device=communication.State;
        var traces = new TraceQuery(dbOptions, TimeProvider.System, 2000);
        var events = new StageEventStore(dbOptions);
        var media = new MediaStore(root, new MediaCapacity(32 * 1024 * 1024,
            4 * 1024 * 1024, 32 * 1024 * 1024, 32 * 1024 * 1024), new MediaLeaseRegistry(), 1);
        var python = FindPython();
        var script = Path.Combine(workspace, "scripts", "virtual-station01-algorithm.py");
        var workerConfig = Path.Combine(workspace, "specs", "007-station01-integrated-loop",
                "examples", "virtual-algorithm.json");
        await using var worker = new WorkerProcessSupervisor(python, root,
            $"\"{script}\" \"{workerConfig}\"");
        var workerStarted = false;
        try
        {
            await worker.StartAsync();
            workerStarted = true;
            var lease = new ResourceLease();
            Assert.True(lease.TryHold(runId));
            IPlcStatePort state = failure == FailureKind.WrongArrival
                ? new WrongArrivalObservation(device) : device;
            var motion = new MotionCoordinator(state, communication.Action, communication.Motion, communication.Acquisition, lease);
            var capture = new FileBackedCapture(Path.Combine(fixtureRoot,
                caseId == "Q01" ? "media-manifest.json" : "media-manifest-q02.json"));
            // SRC-07c: same real capture instance must reject an uncommitted intent before any callback/trigger.
            var invalidCapture = new CaptureRequest(new(runId, Guid.NewGuid(), 1, Guid.NewGuid(),
                "declared-input", config.Version, "Test", 1, 100, "component-clock"), Guid.NewGuid(),
                CaptureRole.Detection, "point", "1", null, null, pair[0].ToString(), "SIM_WHITE", Guid.Empty, 4096);
            var callbacks = 0;
            await Assert.ThrowsAsync<ArgumentException>(async () =>
                await capture.RequestCaptureAsync(invalidCapture, _ => callbacks++, default));
            Assert.Equal(0, callbacks); Assert.Equal(0, capture.TriggerCount(CaptureRole.Detection));
            ITraceWriter traceWriter = failure == FailureKind.MediaSave
                ? new RejectMediaFactWriter(writer) : writer;
            var adapter = new PythonWorkerAdapter(worker);
            var observedAlgorithm = new ObservedAlgorithmPort(adapter);
            var input = RecipeAdmission.Freeze(runId, trayId, plan, CapabilityRegistration.RegisterStation01(adapter, "Test"),
                new("component-approved-cost", "008-preserved/1", "Test", "component", $"{budget.Id}/{budget.Version}",
                    "explicit-component-budget", 5000, 10000, 5000, 24100, 23000)
                { CaptureWaitMs = 8000, AlgorithmWaitMs = 15000, InputReleaseWaitMs = 2000 }, "Test");
            var detection = new RecipeDetectionExecutor(capture, observedAlgorithm,
                media, events, traceWriter, traces, motion);
            var request = new DetectionRequest(runId, trayId, Guid.NewGuid(), Guid.NewGuid(),
                WholeTrayWorkflowStage.Detection, Guid.NewGuid(), revision,
                device.Observe().ConnectionEpoch, DateTimeOffset.UtcNow.AddMinutes(4),
                ["component://explicit-targets"], "Test", "fourth-batch:" + caseId,
                ExpectedObjects: expected, Plan: plan,
                MotionConfiguration: config, Targets: targets) { Inputs = input, SessionId = Guid.NewGuid(),
                    SnapshotId = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                        System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { config, budget, plan, targets })))),
                    ClockId = "component-system-clock", FrozenBusinessDurations = budget.BusinessMs, CriticalSaveBudgetMs = budget.BusinessMs.CriticalSave };
            Assert.True(request.IsValid);
            var executionStarted = System.Diagnostics.Stopwatch.GetTimestamp();
            var result=await detection.ExecuteAsync(request,default);
            await communication.SaveDiagnosticsAsync(root);
            await File.WriteAllTextAsync(Path.Combine(root, "component-observation.json"),
                JsonSerializer.Serialize(new { caseId, runId, failure = failure.ToString(),
                    result, observation = device.Observe() }));
            if (failure != FailureKind.None)
            {
                Assert.NotEqual(DetectionResultKind.Completed, result.Kind);
                Assert.Empty(result.Objects);
                if (failure == FailureKind.SaveWindowExpiry)
                {
                    using var drain = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    await writer.WaitForIdleAsync(drain.Token);
                    await using var saved = new Station01DbContext(dbOptions);
                    // The actual writer was blocked before transaction admission. Expiry must
                    // close that queued save, not just stop the caller while it commits later.
                    Assert.False(await saved.Writes.AnyAsync(row => row.RunId == runId && row.Kind == "Media"));
                    Assert.Equal(1, capture.TriggerCount(CaptureRole.Detection));
                }
                Assert.Equal(failure == FailureKind.WrongArrival ? 0 : 1,
                    capture.TriggerCount(CaptureRole.Detection));
                Assert.True(lease.Unknown);
                await File.WriteAllTextAsync(Path.Combine(root, "component-failure.json"),
                    JsonSerializer.Serialize(new { caseId, runId, failure = failure.ToString(),
                        result.Kind, result.ErrorCode, captureCount = capture.TriggerCount(CaptureRole.Detection),
                        nextMoveDispatched = false, source = "Test/FaultInjected" }));
                return root;
            }
            Assert.True(result.Kind == DetectionResultKind.Completed, JsonSerializer.Serialize(result));
            Assert.Equal(DetectionResultKind.Completed, result.Kind);
            Assert.Equal(ResultSource.Test, result.Source);
            Assert.Equal(ComponentEvidenceSource.Test, result.AlgorithmOrigin.Source);
            Assert.Equal(adapter.Origin.VersionRef, result.AlgorithmOrigin.VersionRef);
            Assert.Equal(ComponentEvidenceSource.Test, capture.CameraOrigin.Source);
            Assert.Equal("ConfiguredOnly", capture.LightOrigin.Quality);
            Assert.Equal(slots.Length, result.Objects.Count);
            Assert.Equal(captureCount, capture.TriggerCount(CaptureRole.Detection));
            Assert.False(lease.Unknown);
            Assert.Null(lease.CurrentAction);
            await using var readback = new Station01DbContext(dbOptions);
            var algorithmIntents = (await readback.Writes.Where(row => row.RunId == runId &&
                row.Kind == "AlgorithmIntent").ToListAsync()).Select(row =>
                JsonSerializer.Deserialize<AlgorithmIntentPayload>(row.PayloadJson)!).ToArray();
            Assert.NotEmpty(algorithmIntents);
            Assert.All(algorithmIntents, intent =>
            {
                Assert.Equal(request.SessionId, intent.SessionId);
                Assert.Equal(request.ClockId, intent.ClockId);
                Assert.True(intent.StartTick >= executionStarted);
                Assert.True(intent.DueTick > intent.StartTick);
                var dispatched = Assert.Single(observedAlgorithm.Requests, call => call.CallId == intent.CallId);
                Assert.Equal(dispatched.Envelope.StartTick, intent.StartTick);
                Assert.Equal(dispatched.Envelope.DueTick, intent.DueTick);
                Assert.Equal(dispatched.Envelope.ConfigVersion, intent.PublicVersion);
                Assert.Equal(15000, intent.BudgetMs); // existing per-call wait, separately bounded by the stage
            });
            Assert.Equal(captureCount,
                await readback.Media.CountAsync(row => row.RunId == runId));
            Assert.Equal(captureCount * 3 / 2,
                await readback.AlgorithmCalls.CountAsync(row => row.RunId == runId));
            var calls = await readback.AlgorithmCalls.Where(row => row.RunId == runId).ToListAsync();
            Assert.Equal(slots.Length, calls.Count(call =>
                JsonSerializer.Deserialize<Guid[]>(call.InputMediaIdsJson)?.Length == 2));
            Assert.Equal(captureCount, await readback.StageEvents.CountAsync(row => row.RunId == runId &&
                row.PayloadJson.Contains("AcquisitionReleased")));
            var captureFacts = await readback.StageEvents.Where(row => row.RunId == runId &&
                row.PayloadJson.Contains("\"sha256\"") &&
                row.PayloadJson.Contains("\"stepSequence\"")).OrderBy(row => row.Sequence).ToListAsync();
            var actualOrder = captureFacts.Select(fact =>
            {
                using var payload = JsonDocument.Parse(fact.PayloadJson);
                var item = payload.RootElement;
                var objectId = item.GetProperty("objectId").GetString()!;
                var slot = slots.Single(slot => objectId.Contains(":" + slot + ":", StringComparison.Ordinal));
                return $"{item.GetProperty("camera").GetString()}:{slot}";
            }).ToArray();
            Assert.Equal(pair.SelectMany(camera => slots.Select(slot => $"{camera}:{slot}")), actualOrder);
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixtureRoot,
                caseId == "Q01" ? "media-manifest.json" : "media-manifest-q02.json")));
            foreach (var row in captureFacts)
            {
                var fact = JsonSerializer.Deserialize<JsonElement>(row.PayloadJson);
                var camera = fact.GetProperty("camera").GetString();
                var expectedMedia = Assert.Single(manifest.RootElement.GetProperty("images").EnumerateArray(),
                    m => m.GetProperty("role").GetString() == "Detection" && m.GetProperty("camera").GetString() == camera);
                Assert.Equal(expectedMedia.GetProperty("sha256").GetString()!.ToUpperInvariant(), fact.GetProperty("sha256").GetString());
                var mediaId = fact.GetProperty("MediaId").GetGuid(); var captureId = fact.GetProperty("captureId").GetGuid();
                Assert.Contains(result.CaptureFacts, f => f.CaptureId == captureId && f.RunId == runId &&
                    f.Replayed && f.ApplicationState == CaptureApplicationState.ConfiguredOnly && f.ActualSettings == null);
                Assert.True(await readback.Media.AnyAsync(m => m.MediaId == mediaId && m.CaptureId == captureId));
            }
            if (caseId == "Q02") Assert.Equal([1, 3], targets.Select(target =>
                target.PhysicalSlotIndex).Distinct().OrderBy(value => value).ToArray());
            var fusionFacts = await readback.StageEvents.Where(row => row.RunId == runId &&
                row.PayloadJson.Contains("FaceFusionCommitted")).ToListAsync();
            Assert.Equal(slots.Length, fusionFacts.Count);
            Assert.All(expected, item => Assert.Contains(fusionFacts,
                fact => fact.PayloadJson.Contains(item.ObjectId, StringComparison.Ordinal)));
            Assert.All(await readback.Media.Where(row => row.RunId == runId).ToListAsync(), row =>
                Assert.True(File.Exists(Path.Combine(root, row.RelativeKey))));
            await File.WriteAllTextAsync(Path.Combine(root, "component-result.json"),
                JsonSerializer.Serialize(new { caseId, runId, revision, source = "Test/InjectedXYZ",
                    result.Kind, result.Objects, targets,
                    mediaCount = captureCount, workerCalls = captureCount * 3 / 2,
                    noFormalQAcceptance = true }));
            return root;
        }
        finally
        {
            if (workerStarted) await worker.RequestStopAsync();
        }
    }

    private static string FindPython() => (Environment.GetEnvironmentVariable("GAODE_TEST_PYTHON") is { Length: > 0 } explicitPath
        ? explicitPath : (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => Path.Combine(path, OperatingSystem.IsWindows() ? "python.exe" : "python"))
            .First(File.Exists));

    private sealed class ObservedAlgorithmPort(IAlgorithmPort inner) : IAlgorithmPort
    {
        public ComponentExecutionOrigin Origin => inner.Origin;
        public List<AlgorithmRequest> Requests { get; } = [];
        public ValueTask<AlgorithmDispatch> RequestAsync(AlgorithmRequest request, Action<AlgorithmEvent> onEvent,
            CancellationToken token)
        {
            Requests.Add(request);
            return inner.RequestAsync(request, onEvent, token);
        }
        public int CallCount(AlgorithmRole role) => inner.CallCount(role);
    }

    private sealed class WrongArrivalObservation(IPlcStatePort inner) : IPlcStatePort
    {
        public DeviceObservation Observe()
        {
            var observed = inner.Observe();
            return observed with { Position = observed.Position! with { ActualX = observed.Position!.ActualX + 5 } };
        }
    }

    private sealed class RejectMediaFactWriter(ITraceWriter inner) : ITraceWriter
    {
        public QueuedWrite SubmitCritical(WriteBatch batch, CancellationToken cancellationToken = default,
            Gaode.Domain.Station01.ActionWindow? window = null)
        {
            if (batch.Kind != WriteKind.Media) return inner.SubmitCritical(batch, cancellationToken, window);
            var queued = new CommitReceipt(batch.WriteId, batch.RunId, CommitState.Queued,
                null, TerminalOutcome.None, null);
            var rejected = queued with { State = CommitState.Failed,
                ErrorCode = "Test/InjectedNecessaryMediaSaveFailure" };
            return new(queued, Task.FromResult(rejected));
        }
        public Task<CommitReceipt?> ReconcileAsync(Guid writeId, CancellationToken token) =>
            inner.ReconcileAsync(writeId, token);
    }
}

