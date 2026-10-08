using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Configuration;
using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Host.Composition;
using Gaode.Infrastructure.Devices.Cameras;
using Gaode.Infrastructure.Diagnostics;
using Gaode.Infrastructure.Media;
using Gaode.Infrastructure.Persistence;
using Gaode.Infrastructure.Simulation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Gaode.Communication.Tests;

// Explicit OFFLINE inputs. These numbers and the seven unstarted worker bindings
// are test fixtures only, never a configuration for physical motion or SDK evidence.
public sealed class ControlledCommissioningTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FormalCommissioningStartBlocksWithoutSafeInputsOrConfirmedSiteSafety(bool missingInput)
    {
        Gaode.Infrastructure.Diagnostics.HostWorkerCapacity.Ensure();
        using var fixture = new Inputs();
        await fixture.PrepareStore();
        await using var plc = new SiteProtocolTcpFixture();
        var config = missingInput ? fixture.Config with { FLocation = null } : fixture.Config;
        var loaded = Inputs.Load(config);
        File.WriteAllText(fixture.Options.CommissioningPath!, loaded.CanonicalJson, new UTF8Encoding(false));
        var options = fixture.Options with { PlcPort = plc.Port, PlcIoTimeoutMs = 1000, CommissioningSha256 = loaded.Digest };
        var evidenceName = missingInput ? "start-missing-input" : "start-site-safety-unconfirmed";
        var logger = new FileLogger(Inputs.EvidencePath(evidenceName + ".log"));
        using var diagnostics = new RuntimeDiagnosticLogging(logger);
        var services = new ServiceCollection(); services.AddLogging(); services.AddStation01(options);
        services.AddSingleton<IRecipeCatalog>(new EmptyCatalog());
        await using var provider = services.BuildServiceProvider();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await provider.GetRequiredService<Gaode.Host.Lifecycle.Station01HostedService>().InitializePersistenceAsync(timeout.Token);
        var device = provider.GetRequiredService<Gaode.Infrastructure.Devices.Plc.LatestProtocolPlcDevice>();
        await device.StartAsync(timeout.Token);
        var coordinator = provider.GetRequiredService<Gaode.Application.Station01.Station01Coordinator>();
        var starts = provider.GetRequiredService<Gaode.Application.Station01.StartPublicPreparation>();
        var tray = Guid.NewGuid();
        var context = JsonSerializer.Serialize(new { schemaVersion = "station01-start-run-context/1.0", trayId = tray,
            stationId = Guid.NewGuid().ToString(), lineId = Guid.NewGuid().ToString(), scenarioId = config.ExpectedRecipe.ScenarioId,
            occupiedSlots = Array.Empty<string>(), purpose = "Commissioning" });
        try
        {
            var receipt = starts.Start("OFFLINE", new(evidenceName, context, options.PublicReference, options.BudgetReference, options.SimulationReference));
            await Gaode.Communication.Tests.Devices.CommissioningProtocolTcpFixture.UntilAsync(() => coordinator.Query(receipt.RunId)?.State is RunState.Blocked or RunState.ConfigurationBlocked, timeout.Token);
            await starts.StopAsync(timeout.Token);
            var state = coordinator.Query(receipt.RunId)!;
            Assert.Equal(RunState.Blocked, state.State);
            var trace = await provider.GetRequiredService<ITraceQuery>().GetWritesAsync(receipt.RunId, timeout.Token);
            Assert.DoesNotContain(trace, w => w.Kind == WriteKind.ActionIntent);
            Assert.Contains(trace, w => w.Kind == WriteKind.Audit && w.PayloadJson.Contains("FrozenPublicConfiguration"));
            Assert.All(plc.Writes, w => Assert.Equal(1005, w.Offset)); // PC heartbeat only; no ready/start/target/axis command.
            Assert.All(provider.GetRequiredService<PersistentCameraGateway>().Status, s => Assert.Equal("Stopped", s.State));
            var algorithm = provider.GetRequiredService<CommissioningAlgorithm>();
            Assert.Equal(0, algorithm.CallCount(AlgorithmRole.TrayPose));
            var log = File.ReadAllText(logger.Path);
            Assert.Contains(receipt.RunId.ToString(), log);
            if (missingInput)
            {
                Assert.Contains("RunInputsRejectedBeforeMotion", log);
                Assert.Contains("CommissioningSafeInputsMissingOrScopeMismatch", log);
            }
            else
            {
                Assert.Equal("StartupNotReady", state.ErrorCode);
                Assert.Contains("StartupReadiness", log);
                Assert.Contains("PLC-Q3/Q4", log);
                Assert.Contains(trace, w => w.Kind == WriteKind.Audit && w.PayloadJson.Contains("BlockedNoDeviceAction") && w.PayloadJson.Contains("noStartOrMotionRequest"));
            }
            File.WriteAllText(Inputs.EvidencePath(evidenceName + ".json"), JsonSerializer.Serialize(new {
                scope = "OFFLINE:formal-commissioning-DI-and-Start;site-layout-loopback;workers-not-started", missingInput,
                state, trace, writes = plc.Writes.Select(w => new { w.Offset, w.Words, w.Accepted }), inputDigest = loaded.Digest
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        }
        finally { await starts.StopAsync(CancellationToken.None); await coordinator.StopConsumerAsync(CancellationToken.None); }
    }
    private sealed class EmptyCatalog : IRecipeCatalog
    {
        public RecipeCatalogSnapshot GetSnapshot() => new(RecipeCatalogSnapshot.CurrentSchema, "OFFLINE-unused-before-admission", []);
    }

    [Fact]
    public async Task HostComposesRealPortsAndControlledSimulationWithoutLoadingTestSimulation()
    {
        using var fixture = new Inputs();
        await fixture.PrepareStore();
        var services = new ServiceCollection(); services.AddLogging();
        services.AddStation01(fixture.Options);
        Assert.Single(services, s => s.ServiceType == typeof(IPlcActionPort));
        Assert.DoesNotContain(services, s => s.ServiceType == typeof(SimulatedPlc));
        await using var provider = services.BuildServiceProvider();
        Assert.Same(TimeProvider.System, provider.GetRequiredService<TimeProvider>());
        var camera = Assert.IsType<CameraCaptureAdapter>(provider.GetRequiredService<ICapturePort>());
        Assert.Equal(ComponentEvidenceSource.Real, camera.CameraOrigin.Source);
        Assert.Equal(ComponentEvidenceSource.Simulated, camera.LightOrigin.Source);
        Assert.Equal(7, provider.GetRequiredService<PersistentCameraGateway>().Status.Count);
        Assert.All(provider.GetRequiredService<PersistentCameraGateway>().Status, s => Assert.NotEqual("Ready", s.State));
        var algorithm = Assert.IsType<CommissioningAlgorithm>(provider.GetRequiredService<IAlgorithmPort>());
        Assert.Same(algorithm, provider.GetRequiredService<ICommissioningRunInputs>());
        Assert.Same(provider.GetRequiredService<IPlcStatePort>(), provider.GetRequiredService<IPlcActionPort>());
        Assert.Same(provider.GetRequiredService<IPlcStatePort>(), provider.GetRequiredService<IMotionPort>());
        Assert.Equal(ComponentEvidenceSource.Simulated, algorithm.Origin.Source);
        var registry = provider.GetRequiredService<Gaode.Application.Capabilities.CapabilityRegistry>();
        Assert.Equal(fixture.Config.ExpectedRecipe.FCode, registry.Decode(new("decoded-content-exact", "1.0"), fixture.Config.Purpose, fixture.Config.ExpectedRecipe.FCode)!.ParsedCode);
        Assert.False(registry.IsCompatible(new("code.test-tray-format", "1.0"), fixture.Config.Purpose, "Parser"));
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddStation01(fixture.Options with { PlcProvider = "Virtual" }));
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddStation01(fixture.Options with { Cameras = null }));
        Assert.Throws<InvalidDataException>(() => new ServiceCollection().AddStation01(fixture.Options with { CommissioningSha256 = new string('0', 64) }));
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddStation01(fixture.Options with { SimulationReference = new("other", "1") }));
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddStation01(fixture.Options with { Mode = "Production" }));
        var loaded = provider.GetRequiredService<IPublicConfiguration>();
        var frozen = ConfigurationFreezer.Freeze(loaded.LoadPublic(fixture.Options.PublicReference), loaded.LoadBudget(fixture.Options.BudgetReference), null,
            registry.Versions, fixture.Loaded);
        Assert.Null(frozen.Simulation); Assert.Equal(fixture.Loaded.Digest, frozen.CommissioningDigest);
        Assert.Contains("OFFLINE", frozen.CommissioningJson);
    }

    [Fact]
    public async Task ControlledAlgorithmReadsCurrentFilesAndReleasesInputsWithPersistentFailureDiagnostics()
    {
        using var fixture = new Inputs();
        var logger = new FileLogger(Path.Combine(fixture.Root, "runtime.log"));
        using var diagnostics = new RuntimeDiagnosticLogging(logger);
        var media = new MediaStore(fixture.Root, new(65536, 0, 65536, 65536), new(), 1);
        var algorithm = new CommissioningAlgorithm(fixture.Loaded, media);
        var run = Guid.NewGuid(); var tray = Guid.NewGuid();
        algorithm.FreezeRun(run, tray, fixture.Config.ExpectedRecipe.ScenarioId, fixture.Public);
        var capture = Guid.NewGuid();
        MediaRef image;
        using (media.ReserveCapture(capture, "F", 1024))
            image = await media.SaveAsync(run, capture, "F", "1", "1", [11, 23, 37], "bin", "OFFLINEFixture", default);
        await media.MarkCommittedAsync(image, default);
        var now = Stopwatch.GetTimestamp();
        var request = new AlgorithmRequest(new(run, Guid.NewGuid(), 1, Guid.NewGuid(), "OFFLINE", "1", fixture.Config.Purpose,
            now, now + Stopwatch.Frequency * 5, "OFFLINE"), Guid.NewGuid(), capture, AlgorithmRole.FDecode,
            [image], "1", "code.raw-candidates", "1.0", Guid.NewGuid(), "OFFLINE-file");
        var events = new List<AlgorithmEvent>();
        var dispatch = await algorithm.RequestAsync(request, events.Add, default); await dispatch.Exited;
        Assert.Equal(fixture.Config.RawCodes, Assert.Single(events, e => e.Kind == AlgorithmEventKind.Result).RawCodes);
        Assert.Contains(events, e => e.Kind == AlgorithmEventKind.InputReleased); Assert.Equal(0, media.ActiveLeases);
        events.Clear();
        dispatch = await algorithm.RequestAsync(request with { CallId = Guid.NewGuid(), Inputs = [image with { RelativeKey = "wrong" }] }, events.Add, default);
        await dispatch.Exited;
        Assert.DoesNotContain(events, e => e.Kind == AlgorithmEventKind.Result);
        Assert.Contains(events, e => e.ErrorCode == "CommissioningMediaIdentityMismatch"); Assert.Equal(0, media.ActiveLeases);
        algorithm.ReleaseRun(run);
        await Assert.ThrowsAsync<AlgorithmNotDispatchedException>(async () => await algorithm.RequestAsync(request, events.Add, default));
        var missing = new CommissioningAlgorithm(Inputs.Load(fixture.Config with { FLocation = null }), media);
        Assert.Throws<InvalidOperationException>(() => missing.FreezeRun(run, tray, fixture.Config.ExpectedRecipe.ScenarioId, fixture.Public));
        Assert.Equal(0, missing.CallCount(AlgorithmRole.TrayPose));
        var log = File.ReadAllText(logger.Path);
        Assert.Contains("CurrentMediaRead", log); Assert.Contains("CommissioningMediaIdentityMismatch", log);
        Assert.Contains("RunInputsRejectedBeforeMotion", log); Assert.Contains("NoDependentMotion", log);
        Assert.Contains(run.ToString(), log); Assert.Contains(capture.ToString(), log);
        Assert.Contains(fixture.Loaded.Digest, log); Assert.Contains("Error", log);
        File.WriteAllText(Inputs.EvidencePath("algorithm-diagnostics.log"), log);
    }

    [Fact]
    public async Task PoseSingleAndFusionUseFrozenRecipeScopesAndCurrentMedia()
    {
        using var fixture = new Inputs();
        var media = new MediaStore(fixture.Root, new(65536, 0, 65536, 65536), new(), 1);
        var draft = Gaode.Communication.Tests.Devices.Recipe011Data.Candidate(2, false);
        var requirements = draft.AlgorithmRequirements.ToDictionary(p => p.Key, p => p.Value with {
            ResultContract = p.Value.Purpose == AlgorithmPurpose.SingleDetection ? "image-quality/1" : p.Value.Purpose == AlgorithmPurpose.FaceFusion ? "face-quality/1" : p.Value.ResultContract });
        draft = draft with { AlgorithmRequirements = requirements };
        Gaode.Infrastructure.Recipes.RecipeStoreSchema.Prepare(fixture.Root, Path.Combine(fixture.Root, "recipes"));
        using var store = new Gaode.Infrastructure.Recipes.SqliteRecipeStore(new() {
            DatabasePath = Path.Combine(fixture.Root, "recipes", "recipes.db"), ReadWriteTimeoutMs = 10000, DbLockTimeoutSeconds = 5 },
            fixture.Root, Microsoft.Extensions.Logging.Abstractions.NullLogger<Gaode.Infrastructure.Recipes.SqliteRecipeStore>.Instance, () => "OFFLINE");
        var recipe = (await store.SaveAsync(new(draft, null, null, "OFFLINE"), default)).Definition!;
        var run = Guid.NewGuid(); var tray = Guid.NewGuid();
        var plan = RecipeRunPlanner.BuildExecutable(recipe, tray.ToString(), ["s1"], fixture.Config.Purpose);
        CommissioningResultScope Scope(RecipeStep s, string camera) => new(s.SlotId!, s.Material!, s.StageId!, s.LocalFace!.Value, s.CoordinateEpoch, camera);
        var steps = plan.Steps.Where(s => s.Kind == RecipeStepKind.Capture).ToArray();
        var results = steps.Select(s => new CommissioningAlgorithmResult("single-" + s.Sequence, "1", "OFFLINE:explicit-scoped-result", AlgorithmPurpose.SingleDetection, Scope(s, s.Camera!), "OK"))
            .Concat(steps.GroupBy(s => (s.StageId, s.LocalFace, s.CoordinateEpoch)).Select(g => new CommissioningAlgorithmResult("fusion-" + g.First().Sequence, "1", "OFFLINE:explicit-scoped-result", AlgorithmPurpose.FaceFusion, Scope(g.First(), string.Concat(g.Select(s => s.Camera).Order(StringComparer.Ordinal))), "OK"))).ToArray();
        var config = fixture.Config with { ExpectedRecipe = new(recipe.RecipeId, recipe.Version, recipe.DefinitionDigest, recipe.ScenarioId, recipe.Model, recipe.FCode), RawCodes = [recipe.FCode], Results = results,
            Algorithms = [..fixture.Config.Algorithms, new(AlgorithmPurpose.SingleDetection, "detection.single", "1.0", "image-quality/1", 1, "test-1"), new(AlgorithmPurpose.FaceFusion, "detection.fusion", "1.0", "face-quality/1", 2, "test-1")] };
        var algorithm = new CommissioningAlgorithm(Inputs.Load(config), media);
        var capabilities = CapabilityRegistration.RegisterStation01(algorithm, config.Purpose, config);
        var cost = new ExecutionCostProfile("OFFLINE", "1", config.Purpose, "OFFLINE", "OFFLINE", "OFFLINE", 1000, 1000, 1000, 1000, 0) { CaptureWaitMs = 1000, AlgorithmWaitMs = 1000, InputReleaseWaitMs = 1000 };
        var frozen = RecipeAdmission.Freeze(run, tray, plan, capabilities, cost, config.Purpose, fixture.Public.Algorithms.TrayPose);
        algorithm.FreezeRun(run, tray, recipe.ScenarioId, fixture.Public);
        Assert.Throws<InvalidOperationException>(() => algorithm.BindRecipe(run, frozen with { Plan = plan with { DefinitionDigest = "wrong" } }));
        algorithm.BindRecipe(run, frozen);
        var missing = new CommissioningAlgorithm(Inputs.Load(config with { Results = [] }), media);
        missing.FreezeRun(run, tray, recipe.ScenarioId, fixture.Public);
        Assert.Throws<InvalidOperationException>(() => missing.BindRecipe(run, frozen));
        async Task<MediaRef> Save(string kind)
        {
            var capture = Guid.NewGuid(); using var reserve = media.ReserveCapture(capture, kind, 1024);
            var image = await media.SaveAsync(run, capture, kind, "1", "1", [3, 5, 7], "bin", "OFFLINE", default);
            await media.MarkCommittedAsync(image, default); return image;
        }
        async Task<AlgorithmEvent> Call(AlgorithmRole role, IReadOnlyList<MediaRef> images, string id, string parameters,
            IReadOnlyList<WorkerTargetIdentity>? identities = null, TrayObservationContext? observation = null)
        {
            var tick = Stopwatch.GetTimestamp();
            var request = new AlgorithmRequest(new(run, Guid.NewGuid(), 1, Guid.NewGuid(), "OFFLINE", "1", config.Purpose, tick, tick + Stopwatch.Frequency, "OFFLINE"),
                Guid.NewGuid(), images[0].CaptureId, role, images, parameters, id, "1.0", Guid.NewGuid(), "OFFLINE") { InputIdentities = identities, ObservationContext = observation };
            var events = new List<AlgorithmEvent>(); var dispatch = await algorithm.RequestAsync(request, events.Add, default); await dispatch.Exited;
            return Assert.Single(events, e => e.Kind == AlgorithmEventKind.Result);
        }
        var pose = await Call(AlgorithmRole.TrayPose, [await Save("3D")], "tray.observation", "1", observation: new(tray, TrayObservationPurpose.InitialPreparation, 1, null));
        Assert.True(pose.Observation!.HasCompleteCoverage); Assert.Equal(ComponentEvidenceSource.Simulated, pose.Observation.Source.Source);
        var pair = steps.Take(2).ToArray(); var images = new[] { await Save("Detection"), await Save("Detection") };
        WorkerTargetIdentity Identity(RecipeStep s) => new((s.MemberId ?? s.UnitId)!, s.LocalFace!.Value, s.CoordinateEpoch, s.Camera!) { StageId = s.StageId };
        Assert.Equal("OK", (await Call(AlgorithmRole.Detection, [images[0]], "detection.single", "test-1", [Identity(pair[0])])).DetectionDisposition);
        Assert.Equal("OK", (await Call(AlgorithmRole.Detection, images, "detection.fusion", "test-1", pair.Select(Identity).ToArray())).DetectionDisposition);
        Assert.Equal(0, media.ActiveLeases); Assert.Equal(2, algorithm.CallCount(AlgorithmRole.Detection));
    }

    internal sealed class FileLogger(string path) : ILogger
    {
        public string Path => path;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> format)
        { lock (this) File.AppendAllText(path, level + " " + format(state, error) + (error is null ? "" : " exception=" + error) + Environment.NewLine); }
    }
    internal sealed class Inputs : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "gaode-controlled-OFFLINE-" + Guid.NewGuid().ToString("N"));
        public PublicConfiguration Public { get; }
        public CommissioningConfiguration Config { get; }
        public LoadedConfiguration<CommissioningConfiguration> Loaded { get; }
        public Station01RuntimeOptions Options { get; }
        public Inputs()
        {
            Directory.CreateDirectory(Root);
            var basis = ReviewBusinessData.Motion();
            Public = basis with { Purpose = RuntimePurposes.RealDeviceCommissioning, Source = "OFFLINE:matrix-fixture;no-field-authority",
                Capture3d = basis.Capture3d with { BindingId = "3D" }, CaptureF = basis.CaptureF with { BindingId = "F" },
                Motion = basis.Motion with { Points = basis.Motion.Points with {
                    ThreeD = basis.Motion.Points.ThreeD with { Frame = basis.Motion.Frame },
                    F = basis.Motion.Points.F with { Frame = basis.Motion.Frame } } },
                Bindings = basis.Bindings.Select(b => b with { Role = b.Role == "HeightAlgorithm" ? "TrayPose" : b.Role,
                    Id = b.Role == "HeightAlgorithm" ? "OFFLINE-tray-pose" : b.Role == "Camera3D" ? "3D" : b.Role == "CameraF" ? "F" : b.Id,
                    Provider = b.Role == "PLC" || b.Role.StartsWith("Camera") ? "Real" : "Simulated" }).ToArray(),
                Parser = new(new("decoded-content-exact", "1.0"), "1"),
                Algorithms = basis.Algorithms with { Height = null, FDecode = new(new("code.raw-candidates", "1.0"), basis.Algorithms.FDecode.BindingId, "1"), TrayPose = new(new("tray.observation", "1.0"), "OFFLINE-tray-pose", "1") } };
            var budget = ReviewBusinessData.Budget() with { Purpose = Public.Purpose, Source = "OFFLINE:explicit-budget",
                RecipeExecution = new(123, 234, 345, 456, 567, 678) };
            Config = new("020-commissioning/1", "OFFLINE-inputs", "1", Public.Purpose, "OFFLINE:confirmed-fixture-only",
                new(Public.Id, Public.Version), new(budget.Id, budget.Version), new("OFFLINE-recipe", "1", "OFFLINE-digest", "OFFLINE-scenario", "OFFLINE-model", "OFFLINE-tray"),
                new("decoded-content-exact", "1.0", "OFFLINE:exact-code-rule"),
                new Dictionary<string, string> { [Public.Capture3d.LightBindingId] = "3d", [Public.CaptureF.LightBindingId] = "f" },
                [new(AlgorithmPurpose.TrayCode, "code.raw-candidates", "1.0", "decoded-code/1", 1, "1"),
                 new(AlgorithmPurpose.TrayPose, "tray.observation", "1.0", "tray-observation/2", 1, "1")],
                [new(1, TrayPresence.Present, TrayPose.Normal) { CellId = "r1:c4", Region = "OK", Row = 1, Column = 4 }],
                new(10, 20, "mm", Public.Motion.Frame, "OFFLINE:safe-fixture-only"), "OFFLINE:slot-map", ["OFFLINE-tray"], []);
            Loaded = Load(Config);
            File.WriteAllText(Path.Combine(Root, "mechanics.json"), JsonSerializer.Serialize(new Gaode.Infrastructure.Devices.Plc.PlcMechanicalConfiguration {
                SchemaVersion = "plc-mechanics/1", Purpose = Public.Purpose, SourceReference = "OFFLINE:mechanics-test-only", PosePrograms = [], SortingSafePosition = null,
                PositionBasis = new(Public.Motion.Frame, "mm", Public.Purpose, "OFFLINE:position-interpretation") }, CommissioningAlgorithmInputs.Json));
            File.WriteAllText(Path.Combine(Root, "field.json"), JsonSerializer.Serialize(SiteProtocolAdaptationTests.Profile(), CommissioningAlgorithmInputs.Json));
            var inputPath = Path.Combine(Root, "inputs.json"); File.WriteAllText(inputPath, Loaded.CanonicalJson, new UTF8Encoding(false));
            var configRoot = Path.Combine(Root, "config"); Directory.CreateDirectory(configRoot);
            var configJson = new JsonSerializerOptions(CommissioningAlgorithmInputs.Json)
                { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };
            var publicJson = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(Public, configJson))!;
            var bounds = publicJson["capture3d"]!["scope"]!["bounds"]!.AsObject();
            bounds.Remove("zMin"); bounds.Remove("zMax");
            File.WriteAllText(Path.Combine(configRoot, "public.json"), publicJson.ToJsonString());
            File.WriteAllText(Path.Combine(configRoot, "budget.json"), JsonSerializer.Serialize(budget, configJson));
            var site = Path.Combine(Root, "cameras.json");
            File.WriteAllText(site, JsonSerializer.Serialize(new { Cameras = new[] { "A", "B", "C", "D", "E", "F", "3D" }
                .Select((role, i) => new CameraBinding(role, role == "3D" ? "3D" : "2D", "OFFLINE-" + i, "0011223344" + i.ToString("D2"))) }));
            var schemas = Path.Combine(RepoRoot(), "specs", "001-station01-public-preparation", "contracts");
            Options = new(Public.Purpose, Path.Combine(Root, "store"), Root, configRoot, schemas,
                Config.PublicConfigRef, Config.BudgetRef, new(Config.Id, Config.Version), PlcHost: "127.0.0.1", PlcProvider: "Real",
                PlcMechanicsPath: Path.Combine(Root, "mechanics.json"), PlcFieldProfilePath: Path.Combine(Root, "field.json"),
                Cameras: new(site, Path.Combine(AppContext.BaseDirectory, "CameraWorkerFixture", "Gaode.CameraWorkerFixture.exe"), Root, Root, Path.Combine(Root, "camera-state")),
                CommissioningPath: inputPath, CommissioningSha256: Loaded.Digest);
        }
        internal static string RepoRoot()
        { var dir = new DirectoryInfo(AppContext.BaseDirectory); while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent; return dir!.FullName; }
        internal static string EvidencePath(string name)
        {
            var root = Environment.GetEnvironmentVariable("GAODE_COMMISSIONING_EVIDENCE_ROOT") ?? Path.Combine(RepoRoot(), "specs", "020-real-device-commissioning", "evidence", "phase8");
            if (!Path.IsPathFullyQualified(root)) throw new InvalidOperationException("EvidenceRootMustBeAbsolute");
            Directory.CreateDirectory(root); return Path.Combine(root, name);
        }
        internal static LoadedConfiguration<CommissioningConfiguration> Load(CommissioningConfiguration config)
        { var json = JsonSerializer.Serialize(config, CommissioningAlgorithmInputs.Json); return new(config, json, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))), "OFFLINE-fixture"); }
        public async Task PrepareStore(string? profile = null)
        {
            Directory.CreateDirectory(Options.TestRoot); Directory.CreateDirectory(Path.Combine(Options.TestRoot, "media-root"));
            var opts = new DbContextOptionsBuilder<Station01DbContext>().UseSqlite($"Data Source={Path.Combine(Options.TestRoot, "station01.test.db")};Pooling=False").Options;
            await using var db = new Station01DbContext(opts); await db.Database.MigrateAsync();
            db.Manifests.Add(new() { StoreId = Guid.NewGuid(), SchemaVersion = "s01-store/3", Profile = profile ?? Public.Purpose, PrepareOperationId = Guid.NewGuid(), PreparedUtc = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        public void Dispose() { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(Root, true); }
    }
}
