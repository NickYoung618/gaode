using System.Text.Json;
using Gaode.Application.Configuration;
using Gaode.Application.Motion;
using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Application.Station01;
using Gaode.Application.Workflow;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Host.Composition;
using Gaode.Host.Api;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Gaode.Integration.Tests.Support;

// One existing assembly component, from declared frozen input through actual common
// detection, handling and sorting saves. No public preparation or final tray run.
internal static class AssemblyComponentExecution
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal static async Task<string> RunAsync(bool missingCode, bool workerError, bool ngParts, bool ordinary = false)
    {
        var workspace = Station01HostFixture.FindWorkspace();
        var fixtures = Path.Combine(workspace, "specs/008-recipe-driven-inspection/fixtures");
        var root = Path.Combine(Gaode.Testing.ApprovedTestRoot.Resolve(workspace), "assembly-component-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var runId = Guid.NewGuid(); var trayId = Guid.NewGuid();
        var catalog = new Gaode.Infrastructure.Recipes.JsonRecipeCatalog(Path.Combine(fixtures, ordinary ? "usr-e-1.0.2/recipes-q03.json" : "recipes-assembly-a-e.json"));
        var selected = Assert.Single(catalog.GetSnapshot().Definitions);
        var code = ordinary ? "TEST-TRAY-0103" : "TEST-TRAY-0207";
        var plan = RecipeRunPlanner.BuildExecutable(RecipeBindingTestSupport.Match(catalog, code, selected.ScenarioId), trayId.ToString(), ["P01"]);
        var loader = new Gaode.Infrastructure.Configuration.ConfigurationLoader(Path.Combine(workspace, "specs/007-station01-integrated-loop/examples"),
            Path.Combine(workspace, "specs/001-station01-public-preparation/contracts"));
        var frozenConfig = ConfigurationFreezer.Freeze(loader.LoadPublic(new("s01-public-virtual-loop", "1.2.0")),
            loader.LoadBudget(new("s01-budget-virtual-loop", "3.0.0")), loader.LoadSimulation(new("s01-sim-virtual-loop", "3.0.0")),
            new Dictionary<string, string>());
        var config = frozenConfig.Public; var budget = frozenConfig.Budget;
        var cost = new Gaode.Infrastructure.Configuration.ApprovedExecutionCostProvider().Resolve(frozenConfig);
        var options = new DbContextOptionsBuilder<Gaode.Infrastructure.Persistence.Station01DbContext>().UseSqlite($"Data Source={Path.Combine(root, "station01.db")}").Options;
        var storeId = Guid.NewGuid();
        await using (var setup = new Gaode.Infrastructure.Persistence.Station01DbContext(options))
        {
            await setup.Database.MigrateAsync();
            setup.Manifests.Add(new Gaode.Infrastructure.Persistence.StoreManifestEntity { StoreId = storeId, SchemaVersion = "s01-store/2", Profile = "Test",
                PrepareOperationId = Guid.NewGuid(), PreparedUtc = DateTimeOffset.UtcNow });
            setup.Runs.Add(new Gaode.Infrastructure.Persistence.RunEntity { RunId = runId, RequestId = "assembly-component", SubjectId = "component",
                CreatedUtc = DateTimeOffset.UtcNow });
            await setup.SaveChangesAsync();
        }
        await using var writer = new Gaode.Infrastructure.Persistence.TraceWriter(options, TimeProvider.System, 128);
        await using var communication = await DetectionCommunicationFixture.CreateAsync(writer, storeId,
            budget.BusinessMs.CriticalSave, runId, false);
        var events = new Gaode.Infrastructure.Persistence.StageEventStore(options); var query = new Gaode.Infrastructure.Persistence.TraceQuery(options, TimeProvider.System, 2000);
        var lease = new ResourceLease(); Assert.True(lease.TryHold(runId));
        var motion = new MotionCoordinator(communication.State, communication.Action, communication.Motion, communication.Acquisition, lease);
        var media = new Gaode.Infrastructure.Media.MediaStore(root, new Gaode.Infrastructure.Media.MediaCapacity(32 * 1024 * 1024, 4 * 1024 * 1024,
            32 * 1024 * 1024, 32 * 1024 * 1024), new Gaode.Infrastructure.Media.MediaLeaseRegistry(), 1);
        var capture = new Gaode.Infrastructure.Simulation.FileBackedCapture(Path.Combine(fixtures, ordinary ? "usr-e-1.0.2/media-manifest-q03.json" : "media-manifest-assembly-a-e.json"));
        var script = Path.Combine(workspace, "scripts/virtual-station01-algorithm.py");
        var workerConfig = Path.Combine(fixtures, ordinary ? "usr-e-1.0.2/worker-manifest-q03.json" : workerError ? "worker-manifest-assembly-a-e-error.json" :
            ngParts ? "worker-manifest-assembly-a-e-ng.json" : missingCode ? "worker-manifest-assembly-a-e-no-code.json" : "worker-manifest-assembly-a-e.json");
        var python = Environment.GetEnvironmentVariable("GAODE_TEST_PYTHON") ?? (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator).Select(p => Path.Combine(p, "python.exe")).First(File.Exists);
        await using var worker = new Gaode.Infrastructure.Algorithms.WorkerProcessSupervisor(python, root, $"\"{script}\" \"{workerConfig}\"",
            implementation: Gaode.Infrastructure.Algorithms.WorkerImplementation.Read(script, workerConfig));
        await worker.StartAsync();
        try
        {
            var algorithm = new Gaode.Infrastructure.Algorithms.PythonWorkerAdapter(worker);
            var inputs = RecipeAdmission.Freeze(runId, trayId, plan, CapabilityRegistration.RegisterStation01(algorithm, "Test"), cost, "Test");
            var height = new HeightResult(runId, Guid.NewGuid(), Guid.NewGuid(), "sim-whole-tray", "1.0.0",
                [HeightSample.FromRaw("sample-a", 11, "mm", "SIM_REFERENCE"), HeightSample.FromRaw("sample-b", 11, "mm", "SIM_REFERENCE")], AlgorithmState.Success);
            // This is an explicit component measurement input, not a claim to have run 3D/F.
            // Its identities are frozen before execution and kept with the evidence.
            var identity = new WorkflowIdentity(runId, trayId, Guid.NewGuid().ToString(), Guid.NewGuid().ToString(),
                "component", selected.ScenarioId, ["P01"], DateTimeOffset.UtcNow, "component", RunPurpose.Test, "1.2.0", "2.0.0", "2.0.0");
            var handoff = new PublicPreparationHandoffV2(PublicPreparationHandoffV2.CurrentSchemaVersion, Guid.NewGuid(), identity,
                "component-points/1", "component-capabilities/1", ["component://declared-3d"], ["component://declared-f"],
                [$"algorithm-call://{height.CallId:D}"], code, "component://frozen-plan", inputs.PlanRevision,
                "component://input-only", ComponentEvidenceSource.Test, "DeclaredComponentInput", ["component://prepared-input"],
                Guid.NewGuid(), 1, DateTimeOffset.UtcNow, "");
            handoff = handoff with { PayloadDigest = PublicPreparationHandoffV2.ComputePayloadDigest(handoff) };
            var resolved = CoordinateResolver.Resolve(plan, handoff);
            var deadlines = RecipeExecutionBudget.Freeze(plan, budget, DateTimeOffset.UtcNow, cost);
            var request = new DetectionRequest(runId, trayId, Guid.Parse(identity.StationId), Guid.Parse(identity.LineId),
                WholeTrayWorkflowStage.Detection, Guid.NewGuid(), inputs.PlanRevision, communication.State.Observe().ConnectionEpoch,
                deadlines.DetectionDeadlineUtc, ["component://declared-frozen-input"], "Test", "assembly-component",
                ExpectedObjects: resolved.Item2, Plan: plan,
                MotionConfiguration: config, Targets: resolved.Item1)
                { Inputs = inputs, SessionId = Guid.NewGuid(), SnapshotId = frozenConfig.SnapshotId, ClockId = "component-system-clock",
                    FrozenBusinessDurations = budget.BusinessMs, CriticalSaveBudgetMs = budget.BusinessMs.CriticalSave, StageStartedAtUtc = deadlines.StartedUtc };
            await File.WriteAllTextAsync(Path.Combine(root, "prepared-input.json"), JsonSerializer.Serialize(new
                { evidenceLevel = "PortComponent", publicPreparationExecuted = false, inputs, height, request }, Json));
            var detection = new RecipeDetectionExecutor(capture, algorithm, media, events, writer, query, motion, communication.Handling);
            var allocator = new SortingTargetAllocator(events, TimeProvider.System, budget.BusinessMs.CriticalSave);
            var executor = new ThreeStageWorkflowExecutor(detection, communication.StageActions(budget.BusinessMs, allocator),
                new RecipeSortingMapper(events, TimeProvider.System), events, allocator);
            var result = await executor.ExecuteAsync(new(request, plan, Guid.NewGuid(), Guid.NewGuid(), config.Motion.Points.Unload,
                config.Motion.PositionTolerance, "Test", frozenConfig.SnapshotId, deadlines));
            await communication.SaveDiagnosticsAsync(root);
            await File.WriteAllTextAsync(Path.Combine(root, "result.json"), JsonSerializer.Serialize(result, Json));
            await using var db = new Gaode.Infrastructure.Persistence.Station01DbContext(options);
            var rows = await db.StageEvents.Where(e => e.RunId == runId).OrderBy(e => e.Sequence).ToArrayAsync();
            await File.WriteAllTextAsync(Path.Combine(root, "stage-events.json"), JsonSerializer.Serialize(rows));
            await File.WriteAllTextAsync(Path.Combine(root, "writes.json"), JsonSerializer.Serialize(await db.Writes.Where(e => e.RunId == runId).ToArrayAsync()));
            Assert.True(result.IsCompleted, JsonSerializer.Serialize(result));
            Assert.Equal(0, algorithm.CallCount(AlgorithmRole.Height)); Assert.Equal(0, algorithm.CallCount(AlgorithmRole.FDecode));
            if (ordinary)
            {
                Assert.Equal(6, algorithm.CallCount(AlgorithmRole.Detection));
                Assert.Equal(0, algorithm.CallCount(AlgorithmRole.EDecode));
                var facts = rows.Select(e => JsonSerializer.Deserialize<JsonElement>(e.PayloadJson)).ToArray();
                Assert.Single(facts, p => Kind(p) == "FaceEstablished");
                var captured = facts.Where(p => p.TryGetProperty("stepSequence", out _) && p.TryGetProperty("RelativeKey", out _)).ToArray();
                Assert.Equal(new[] { 1, 1, 2, 2 }, captured.Select(p => p.GetProperty("localFace").GetInt32()));
                Assert.Equal(new[] { "A", "B", "A", "B" }, captured.Select(p => p.GetProperty("camera").GetString()));
                Assert.Equal(2, facts.Count(p => Kind(p) == "FaceFusionCommitted"));
                Assert.Equal(4, await db.Media.CountAsync(m => m.RunId == runId));
                Assert.DoesNotContain(facts, p => Kind(p) == "ECodeBinding");
                var decision = Assert.Single(facts, p => Kind(p) == "DetectionUnitDecision");
                Assert.Equal("OK", Assert.Single(decision.GetProperty("objects").EnumerateArray()).GetProperty("Disposition").GetString());
            }
            else
            {
            Assert.Equal(9, algorithm.CallCount(AlgorithmRole.Detection)); Assert.Equal(1, algorithm.CallCount(AlgorithmRole.EDecode));
            var payloads = rows.Select(e => JsonSerializer.Deserialize<JsonElement>(e.PayloadJson)).ToArray();
            var flip = Assert.Single(payloads, p => Kind(p) == "FaceEstablished");
            Assert.EndsWith(":A:P01", flip.GetProperty("entity").GetString());
            var binding = Assert.Single(payloads, p => Kind(p) == "ECodeBinding");
            Assert.EndsWith(":M01", binding.GetProperty("objectId").GetString());
            if (workerError) Assert.Equal("ControlledEDecodeFailure", binding.GetProperty("issue").GetString());
            else if (missingCode) Assert.Equal("ECodeNoResult", binding.GetProperty("issue").GetString());
            else Assert.StartsWith("TEST-BASE-A:", binding.GetProperty("externalCode").GetString());
            var captured = payloads.Where(p => p.TryGetProperty("stepSequence", out _) && p.TryGetProperty("RelativeKey", out _)).ToArray();
            Assert.Equal(7, captured.Length);
            Assert.All(captured.Where(p => p.GetProperty("objectId").GetString()!.EndsWith(":M02")), p => Assert.Equal(1, p.GetProperty("localFace").GetInt32()));
            var eCapture = Assert.Single(captured, p => p.GetProperty("camera").GetString() == "E");
            Assert.EndsWith(":M01", eCapture.GetProperty("objectId").GetString()); Assert.Equal(1, eCapture.GetProperty("localFace").GetInt32());
            Assert.Equal(7, await db.Media.CountAsync(m => m.RunId == runId));
            var fusion = payloads.Where(p => Kind(p) == "FaceFusionCommitted").ToArray();
            Assert.Equal(3, fusion.Length); Assert.Equal(2, fusion.Select(p => p.GetProperty("objectId").GetString()).Distinct().Count());
            var decision = Assert.Single(payloads, p => Kind(p) == "DetectionUnitDecision");
            var entity = Assert.Single(decision.GetProperty("objects").EnumerateArray());
            Assert.EndsWith(":A:P01", entity.GetProperty("ObjectId").GetString());
            Assert.Equal(ngParts ? "NG" : "OK", entity.GetProperty("Disposition").GetString());
            var committedFacts = await events.ReadAsync(runId, trayId, WholeTrayWorkflowStage.Detection);
            var projected = CommittedResultProjection.Build([], committedFacts);
            Assert.Equal(2, projected.Results.Count(p => p.Kind == "Part"));
            Assert.Equal(3, projected.Results.Count(p => p.Kind == "Face"));
            var mediaView = await RunMediaCatalog.ReadAsync(runId, options, media, default);
            var eMedia = Assert.Single(mediaView!.Items, m => m.Role == "E");
            Assert.EndsWith(":M01", eMedia.ObjectId); Assert.Equal(1, eMedia.LocalFace);
            if (ngParts)
            {
                Assert.Contains(fusion, p => p.GetProperty("disposition").GetString() == "NG");
                Assert.Contains(fusion, p => p.GetProperty("disposition").GetString() == "Pending" && p.GetProperty("localFace").GetInt32() == 2);
                Assert.Single(rows, e => e.Stage == "Sorting" && e.EventType == "Completed");
            }
            }
            Assert.Empty(await db.WholeTrayCompletions.Where(c => c.RunId == runId).ToArrayAsync());
            Assert.DoesNotContain(rows, e => e.EventType == "FinalUnloadCompleted");
            return root;
        }
        finally { await worker.RequestStopAsync(); }
    }
    private static string? Kind(JsonElement value) => value.TryGetProperty("kind", out var kind) ? kind.GetString() : null;
}
