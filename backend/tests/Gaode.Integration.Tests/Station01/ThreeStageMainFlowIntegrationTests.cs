
using System.Net;
using System.Net.Http.Json;
using Gaode.Application.Station01;
using Gaode.Application.Workflow;
using Gaode.Application.Ports;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Persistence;
using Gaode.Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Gaode.Integration.Tests.Station01;

public sealed class ThreeStageMainFlowIntegrationTests
{
    [Fact]
    public async Task UnintegratedProducersCannotClaimRealExecutionOrIgnoreCancellation()
    {
        var now = DateTimeOffset.UtcNow;
        var detection = new DetectionRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            WholeTrayWorkflowStage.Detection, Guid.NewGuid(), "not-integrated-test", 1, now.AddSeconds(30),
            ["test-input-reference"], "Test", "not-integrated-detection",
            ExpectedObjects: [new("part", new FixedPoint("declared", "1", 1, 2, "mm", "machine", 3), "CAP")]);
        Assert.True(detection.IsValid);
        await using var host = await Station01HostFixture.CreateAsync();
        IDetectionPort missing = host.Host.Services.GetRequiredService<IDetectionPort>();
        Assert.IsType<RecipeDetectionExecutor>(missing);
        var capabilities = host.Host.Services.GetRequiredService<Gaode.Application.Capabilities.CapabilityRegistry>();
        var missingCapability = Assert.Throws<InvalidOperationException>(() => capabilities.Bind(
            new("declared-single", "1", Gaode.Application.Recipes.AlgorithmPurpose.SingleDetection, 1, "image-quality/1"), "Test"));
        Assert.Equal("AlgorithmRequirementNotBound:declared-single", missingCapability.Message);
        var result = await missing.ExecuteAsync(detection, default);
        Assert.Equal(DetectionResultKind.Failed, result.Kind);
        Assert.Equal("FrozenDetectionPlanMissing", result.ErrorCode);
        Assert.Equal(ResultSource.Fallback, result.Source);
        Assert.Equal(ResultQuality.Unknown, result.Quality);
        Assert.False(result.AlgorithmOrigin.IsKnown);
        Assert.False(result.IsRealAcceptance);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await missing.ExecuteAsync(detection, new CancellationToken(true)));

        var correlation = new ActionCorrelation(detection.RunId, detection.OperationId, Guid.NewGuid(), 1,
            Guid.NewGuid(), 1, "test-snapshot", detection.PlanRevision, detection.TrayId);
        var action = new PlcStageActionRequest(correlation, Guid.NewGuid(), Guid.NewGuid(),
            PlcWorkflowStage.UnloadPreparation, "test-parameters",
            new(1, 100, "test-clock", now, now.AddSeconds(1)), "not-integrated-action",
            null, null, new FixedPoint("unload", "1", 1, 2, "mm", "test"), .01, "Test");
        Assert.True(action.IsValid);
        IPlcStageActionPort noPlc = new Gaode.Infrastructure.Integrations.NotIntegratedPlcStageActionPort();
        var rejected = await noPlc.ExecuteAsync(action, default);
        Assert.Equal(StageActionKind.Failed, rejected.Kind);
        Assert.Equal(DeviceProvider.Unavailable, rejected.ExecutionOrigin.Provider);
        Assert.Null(rejected.Evidence);
        Assert.False(rejected.IsCompleted);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await noPlc.ExecuteAsync(action, new CancellationToken(true)));
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("wrong-mapping")]
    [InlineData("unknown-held")]
    [Trait("EvidenceLevel", "UpperIsolation")]
    public async Task PersistedHandoffRecordsPendingMappingFailedAndUnknownHeldWithoutFalseCompletion(string scenario)
    {
        if (scenario == "pending") await RunScenarioAsync(services =>
        {
            services.RemoveAll<IDetectionPort>();
            services.AddSingleton<IDetectionPort, DisconnectDetectionPort>();
            services.RemoveAll<IWorkflowDelay>();
            services.AddSingleton<IWorkflowDelay, ImmediateWorkflowDelay>();
        }, async (fixture, snapshot, db) =>
        {
            Assert.Equal("AwaitingManualTrayRemoval", snapshot.WholeTaskState);
            var pending = await db.StageEvents.SingleAsync(x => x.RunId == snapshot.RunId &&
                x.EventType == StageEventType.PendingRecorded.ToString());
            Assert.Equal("InjectedCommunicationLoss", pending.ErrorCode);
            Assert.Contains("Pending", pending.PayloadJson, StringComparison.Ordinal);
            Assert.Contains(await db.StageEvents.Where(x => x.RunId == snapshot.RunId &&
                x.Stage == WholeTrayWorkflowStage.Sorting.ToString())
                .Select(x => x.EventType).ToListAsync(), x => x == StageEventType.Completed.ToString());
            var whole = await db.WholeTrayCompletions.SingleAsync(x => x.RunId == snapshot.RunId);
            var matrix = await db.ComponentEvidenceMatrices.SingleAsync(x =>
                x.MatrixId == whole.SourceMatrixId);
            Assert.Equal(EvidenceScope.SoftwareLoopOnly.ToString(), matrix.Scope);
        });

        if (scenario == "wrong-mapping") await RunScenarioAsync(services =>
        {
            services.RemoveAll<IDetectionPort>();
            services.AddSingleton<IDetectionPort, AmbiguousDetectionPort>();
        }, async (_, snapshot, db) =>
        {
            Assert.Equal(RunState.Blocked, snapshot.State);
            Assert.Equal(TerminalOutcome.None, snapshot.FinalOutcome);
            Assert.Contains("Missing:", snapshot.ErrorCode, StringComparison.Ordinal);
            Assert.Contains("Ambiguous:unexpected", snapshot.ErrorCode, StringComparison.Ordinal);
            Assert.Single(await db.StageEvents.Where(x => x.RunId == snapshot.RunId &&
                x.EventType == StageEventType.MappingFailed.ToString()).ToListAsync());
            Assert.Empty(await db.StageEvents.Where(x => x.RunId == snapshot.RunId &&
                x.Stage == WholeTrayWorkflowStage.Sorting.ToString()).ToListAsync());
        });

        if (scenario == "unknown-held") await RunScenarioAsync(services =>
        {
            services.RemoveAll<IPlcStageActionPort>();
            services.AddSingleton<InjectedUnknownStagePort>();
            services.AddSingleton<IPlcStageActionPort>(sp =>
                sp.GetRequiredService<InjectedUnknownStagePort>());
        }, async (fixture, snapshot, db) =>
        {
            Assert.Equal(RunState.RecoveryRequired, snapshot.State);
            Assert.Equal("InjectedPostDispatchDisconnect", snapshot.ErrorCode);
            var portAdapter = fixture.Host.Services.GetRequiredService<InjectedUnknownStagePort>();
            Assert.Single(portAdapter.Calls);
            Assert.Equal(PlcWorkflowStage.UnloadPreparation, portAdapter.Calls[0].Stage);
            Assert.Single(await db.StageEvents.Where(x => x.RunId == snapshot.RunId &&
                x.EventType == StageEventType.UnknownHeld.ToString()).ToListAsync());
            Assert.Empty(await db.WholeTrayCompletions.Where(x => x.RunId == snapshot.RunId).ToListAsync());
            var actualWrites = await db.Writes.Where(x => x.RunId == snapshot.RunId)
                .OrderBy(x => x.Revision).ToArrayAsync();
            var faultClosure = Assert.Single(actualWrites, x => x.PayloadJson.Contains("\"kind\":\"FailedMoveRecoveryRequired\""));
            Assert.Contains(actualWrites, x => x.Revision < faultClosure.Revision &&
                x.PayloadJson.Contains("\"kind\":\"DetectionUnitDecision\""));
            Assert.Contains(actualWrites, x => x.Revision == faultClosure.Revision + 1 &&
                x.PayloadJson.Contains("\"kind\":\"RecoveryOldExecutionClosed\""));
        });
    }

    [Fact]
    public async Task OwnedHostNormalShutdownDrainsResourcesWithoutStartingARun()
    {
        var root = Environment.GetEnvironmentVariable("GAODE_011_FULLRUN_ROOT")
            ?? throw new InvalidOperationException("011 current isolated evidence root required");
        var fixture = Environment.GetEnvironmentVariable("GAODE_011_FIXTURE")
            ?? throw new InvalidOperationException("011 declared fixture required");
        await using (var driver = await RecipeExecution010RunHarness.CreateAsync(root, fixture))
        {
            var connection = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            {
                DataSource = Path.Combine(driver.StoreRoot, "station01.test.db"),
                Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly
            }.ToString();
            await using var db = new Station01DbContext(new DbContextOptionsBuilder<Station01DbContext>().UseSqlite(connection).Options);
            Assert.Empty(await db.Runs.AsNoTracking().ToArrayAsync());
        }
    }

    [Fact]
    public async Task CommittedV2HandoffContinuesThroughThreeStagesOverFormalTcpAndSqlite()
    {
        var root = Environment.GetEnvironmentVariable("GAODE_011_FULLRUN_ROOT")
            ?? throw new InvalidOperationException("011 current FullRun evidence root required");
        var fixture = Environment.GetEnvironmentVariable("GAODE_011_FIXTURE")
            ?? throw new InvalidOperationException("011 declared FullRun fixture required");
        await using var harness = await RecipeExecution010RunHarness.CreateAsync(root, fixture);
        var runId = await harness.RunToFinalAsync();
        Assert.NotEqual(Guid.Empty, runId);
        await RecipeExecution010Expectations.VerifyAsync(harness, runId);
    }

    private static async Task RunScenarioAsync(Action<IServiceCollection> configure,
        Func<Station01HostFixture, RunApiSnapshot, Station01DbContext, Task> assert)
    {
        await using var rig = await VirtualLoopTestRig.CreateAsync(configure, useCurrentRecipe: true);
        var fixture = rig.Host;
        var (request, response, receipt) = await rig.StartAsync();
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        // Overall Test wait includes actual public camera/worker calls; PLC and stage limits are unchanged.
        var end = DateTimeOffset.UtcNow.AddSeconds(180);
        RunApiSnapshot snapshot;
        while (true)
        {
            snapshot = (await fixture.Client.GetFromJsonAsync<RunApiSnapshot>(
                $"/api/v1/station01/runs/{receipt.RunId:D}"))!;
            if (snapshot.State == RunState.AwaitingManualRemoval ||
                snapshot.State is RunState.Blocked or RunState.RecoveryRequired) break;
            if (DateTimeOffset.UtcNow >= end)
                Assert.Fail($"Scenario timed out at {snapshot.State}: {snapshot.ErrorCode}");
            await Task.Delay(500);
        }
        var dbOptions = fixture.Host.Services.GetRequiredService<DbContextOptions<Station01DbContext>>();
        await using var db = new Station01DbContext(dbOptions);
        await rig.SaveEvidenceAsync("three-stage-injected", request, response, receipt,
            new { snapshot.State, snapshot.ErrorCode, snapshot.WholeTaskState, snapshot.FinalOutcome,
                evidenceLevel = "UpperIsolation",
                mapping = await db.StageEvents.Where(e => e.RunId == receipt.RunId && e.EventType == "MappingFailed")
                    .Select(e => new { e.OperationId, e.ConnectionEpoch, e.ErrorCode }).ToArrayAsync(),
                sortingCount = await db.StageEvents.CountAsync(e => e.RunId == receipt.RunId && e.Stage == "Sorting") });
        await assert(fixture, snapshot, db);
    }

    private sealed class ImmediateWorkflowDelay : IWorkflowDelay
    {
        public Task DelayAsync(TimeSpan delay, TimeProvider clock,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class DisconnectDetectionPort : IDetectionPort
    {
        public ValueTask<DetectionPortResult> ExecuteAsync(DetectionRequest request,
            CancellationToken cancellationToken) => ValueTask.FromResult(new DetectionPortResult(
                request, DetectionResultKind.Disconnected, null, [], ResultSource.Simulated,
                ResultQuality.Unknown, "InjectedCommunicationLoss", DateTimeOffset.UtcNow,
                ["injected://detection/disconnect"]) { AlgorithmOrigin =
                    new(ComponentEvidenceSource.Simulated, "InjectedDisconnectPort/1", "DeclaredFailure"),
                // Explicit declared upper-isolation input, never actual camera evidence.
                CaptureFacts = [new(request.RunId, Guid.NewGuid(), request.OperationId,
                    request.ConnectionEpoch, "declared-upper-settings", "Simulated",
                    new(ComponentEvidenceSource.Simulated, "DeclaredUnitCamera/1", "Declared"),
                    new(ComponentEvidenceSource.Simulated, "DeclaredUnitLight/1", "Declared"),
                    CaptureApplicationState.ConfiguredOnly, null, false, ["unit://declared-capture"])] });
    }

    private sealed class AmbiguousDetectionPort : IDetectionPort
    {
        public ValueTask<DetectionPortResult> ExecuteAsync(DetectionRequest request,
            CancellationToken cancellationToken) => ValueTask.FromResult(new DetectionPortResult(
                request, DetectionResultKind.Completed, "injected://result/ambiguous",
                [new DetectionObjectResult("unexpected",
                    new FixedPoint("P01", "coordinate-v1", 1, 2, "mm", "tray"),
                    "ClassA", "NG", ["injected://object/unexpected"])],
                ResultSource.Simulated, ResultQuality.Derived, null, DateTimeOffset.UtcNow,
                ["injected://result/ambiguous"]));
    }

    private sealed class InjectedUnknownStagePort : IPlcStageActionPort
    {
        public List<PlcStageActionRequest> Calls { get; } = [];
        public ValueTask<PlcStageActionResult> ExecuteAsync(PlcStageActionRequest request,
            CancellationToken cancellationToken)
        {
            Calls.Add(request);
            return ValueTask.FromResult(new PlcStageActionResult(request,
                StageActionKind.UnknownHeld, request.Correlation.ActionId, request.ConnectionEpoch,
                "InjectedPostDispatchDisconnect", true, false, DateTimeOffset.UtcNow,
                new(DeviceProvider.Simulated, "InjectedStagePort/1", EvidenceQuality.Unknown), null));
        }
    }

}
