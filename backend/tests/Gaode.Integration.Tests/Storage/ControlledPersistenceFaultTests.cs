using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Application.Workflow;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Persistence;
using Gaode.Host.Composition;
using Gaode.Domain.Configuration;
using Gaode.Integration.Tests.Support;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Gaode.Integration.Tests.Storage;

// Actual file SQLite and the same interceptor installed by Host. This is a seam
// component check, not evidence of physical pick or independent-process acceptance.
public sealed class ControlledPersistenceFaultTests
{
    [Theory]
    [InlineData("SCHED-009/before-port", "BA06-before-port")]
    [InlineData("SCHED-009/before-next", "BA06-before-next")]
    [InlineData("SCHED-009/api-cancel", "BA02-api-input")]
    public async Task FiniteSchedulingSeamPreservesOriginalDeadlineAndCancellation(string caseId, string selectedCase)
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var approved = Gaode.Testing.ApprovedTestRoot.Resolve(Station01HostFixture.FindWorkspace());
        var root = await StorePreparation.PrepareEmptyTestStoreAsync(approved,
            Path.Combine(approved, "schedule-seam-" + Guid.NewGuid().ToString("N")));
        using var guard = StoreAccessGuard.Acquire(root, approved);
        using var fault = new ControlledTestPersistenceFault(root, selectedCase);
        var run = Guid.NewGuid();
        await File.WriteAllTextAsync(Path.Combine(root, "009-fault-arm.json"),
            JsonSerializer.Serialize(new { caseId = selectedCase, runId = run, nonce = Guid.NewGuid() }), watchdog.Token);
        var started = DateTimeOffset.UtcNow;
        var deadlines = new RecipeExecutionDeadlines(started, started.AddMilliseconds(250),
            started.AddSeconds(3), started.AddSeconds(2), 1, 1, 0) { FormulaVersion = "declared-scheduling-input/1" };
        var original = JsonSerializer.Serialize(deadlines);
        // Wrong run cannot consume the arm or create a scheduling record. This
        // component does not claim physical zero writes or a device outcome.
        await fault.BeforeRecipeBindingAsync(Guid.NewGuid(), deadlines, 2000, watchdog.Token);
        await fault.BeforeRecipeContinuationAsync(Guid.NewGuid(), deadlines, watchdog.Token);
        var log = Path.Combine(root, "009-fault-events.jsonl");
        Assert.False(File.Exists(log));
        if (selectedCase == "BA06-before-port")
            await fault.BeforeRecipeBindingAsync(run, deadlines, 2000, watchdog.Token);
        else if (selectedCase == "BA06-before-next")
            await fault.BeforeRecipeContinuationAsync(run, deadlines, watchdog.Token);
        else
        {
            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(watchdog.Token);
            var pending = fault.BeforeRecipeContinuationAsync(run, deadlines, cancel.Token);
            while (!File.Exists(log)) await Task.Delay(10, watchdog.Token);
            Assert.False(pending.IsCompleted);
            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        }
        Assert.Equal(original, JsonSerializer.Serialize(deadlines));
        var lines = await File.ReadAllLinesAsync(log, watchdog.Token);
        Assert.All(lines, line => Assert.Contains(run.ToString("D"), line));
        if (selectedCase == "BA02-api-input") Assert.DoesNotContain(lines, line => line.Contains("ExistingHandoffInputReleased"));
        else Assert.True(DateTimeOffset.UtcNow >= deadlines.DetectionDeadlineUtc);
        Assert.StartsWith("SCHED-009/", caseId);
    }

    [Theory]
    [InlineData("FAULT-SEAM/reject-production", "Production", "Virtual", "F05-A")]
    [InlineData("FAULT-SEAM/reject-real", "VirtualPlcIntegration", "Real", "F05-A")]
    [InlineData("FAULT-SEAM/reject-full-simulation", "FullSimulation", "Virtual", "F05-A")]
    [InlineData("FAULT-SEAM/reject-unknown", "VirtualPlcIntegration", "Virtual", "AnyFault")]
    public void OfficialCompositionRejectsFaultOutsideApprovedTestScope(string caseId, string mode, string provider, string faultCase)
    {
        var options = new Station01RuntimeOptions(mode, "unused", "unused", "unused", "unused",
            new ConfigReference("public", "1"), new ConfigReference("budget", "2"), new ConfigReference("sim", "2"),
            PlcProvider: provider, TestPersistenceFaultCase: faultCase);
        var error = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddStation01(options));
        Assert.True(error.Message == "PersistenceFaultRequiresControlledVirtualTestCase", $"{caseId}: {error.Message}");
    }

    [Theory]
    [InlineData("FAULT-SEAM/pick-before-commit", "F05-A")]
    [InlineData("FAULT-SEAM/pick-after-commit", "F05-B")]
    [InlineData("FAULT-SEAM/pick-unknown-readback", "F05-C")]
    public async Task PickFaultTargetsRealStoreBoundaryAndOnlyArmedRun(string caseId, string faultCase)
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        var approved = Gaode.Testing.ApprovedTestRoot.Resolve(Station01HostFixture.FindWorkspace());
        var root = await StorePreparation.PrepareEmptyTestStoreAsync(approved,
            Path.Combine(approved, "fault-seam-" + Guid.NewGuid().ToString("N")));
        using var guard = StoreAccessGuard.Acquire(root, approved);
        using var fault = new ControlledTestPersistenceFault(root, faultCase);
        var builder = new DbContextOptionsBuilder<Station01DbContext>()
            .UseSqlite(StoreCompatibilityProbe.ReadWriteConnectionString(root));
        fault.Configure(builder);
        var store = new StageEventStore(builder.Options);
        var run = Guid.NewGuid(); var unrelatedRun = Guid.NewGuid(); var nonce = Guid.NewGuid();
        await using (var seed = new Station01DbContext(builder.Options)) {
            foreach (var id in new[] { run, unrelatedRun }) seed.Runs.Add(new RunEntity {
                RunId = id, RequestId = "fault-seam-" + id, SubjectId = "Test", State = RunState.Sorting,
                CreatedUtc = DateTimeOffset.UtcNow });
            await seed.SaveChangesAsync(watchdog.Token);
        }
        await PublishAsync("009-fault-arm.json",
            JsonSerializer.Serialize(new { caseId = faultCase, runId = run, nonce }), watchdog.Token);
        Assert.True((await store.AppendAsync(Request(unrelatedRun), watchdog.Token)).IsCommitted);
        Assert.False(File.Exists(Path.Combine(root, "009-fault-events.jsonl")));
        var request = Request(run);
        var operation = store.AppendAsync(request, watchdog.Token);
        if (faultCase == "F05-A")
        {
            var error = await Assert.ThrowsAsync<StageEventCommitException>(() => operation);
            Assert.Equal(ActualCommitState.ConfirmedRolledBack, error.ActualCommit);
            Assert.Equal(0, await CountAsync());
        }
        else
        {
            await WaitForEventAsync("ReceiptHeld");
            Assert.False(operation.IsCompleted);
            if (faultCase == "F05-B") Assert.Equal(1, await CountAsync());
            else
            {
                await using var reader = new SqliteConnection(new SqliteConnectionStringBuilder {
                    DataSource = Path.Combine(root, "station01.test.db"), Mode = SqliteOpenMode.ReadOnly,
                    Pooling = false, DefaultTimeout = 1 }.ToString());
                var error = await Assert.ThrowsAsync<SqliteException>(async () => {
                    await reader.OpenAsync(watchdog.Token);
                    using var command = reader.CreateCommand(); command.CommandText = "SELECT count(*) FROM StageEvents";
                    command.CommandTimeout = 1; await command.ExecuteScalarAsync(watchdog.Token);
                });
                Assert.Equal(5, error.SqliteErrorCode);
            }
            await PublishAsync("009-fault-release.json",
                JsonSerializer.Serialize(new { runId = run, nonce }), watchdog.Token);
            Assert.True((await operation.WaitAsync(watchdog.Token)).IsCommitted);
            Assert.Equal(1, await CountAsync());
            await WaitForEventAsync("ReceiptReleased");
        }
        var evidenceRoot = Environment.GetEnvironmentVariable("GAODE_009_EVIDENCE_ROOT") ?? throw new InvalidOperationException("EvidenceRootRequired");
        Directory.CreateDirectory(evidenceRoot);
        await File.WriteAllTextAsync(Path.Combine(evidenceRoot, caseId.Replace('/', '-') + "-" + run + ".json"),
            JsonSerializer.Serialize(new { caseId, runId = run, root, request.EventId,
                scope = "ActualSQLiteHostInterceptorComponent;NoPhysicalPickClaim",
                actualRows = await CountAsync(), events = await File.ReadAllLinesAsync(Path.Combine(root, "009-fault-events.jsonl")) }), watchdog.Token);

        StageEventAppendRequest Request(Guid id) {
            var now = DateTimeOffset.UtcNow;
            return new(Guid.NewGuid(), id, Guid.NewGuid(), "station01", "line01", WholeTrayWorkflowStage.Sorting,
                Guid.NewGuid(), 1, 1, StageEventType.Executing, now, ResultSource.Virtual, ResultQuality.Derived,
                null, "controlled-seam-payload", "{\"kind\":\"SortingAssignmentInTransit\"}", Guid.NewGuid().ToString(),
                "plan-fault-seam", now.AddSeconds(-1), now.AddSeconds(10));
        }
        async Task PublishAsync(string name, string payload, CancellationToken token) {
            var destination = Path.Combine(root, name);
            var temporary = destination + ".pending";
            await File.WriteAllTextAsync(temporary, payload, token);
            File.Move(temporary, destination); // Publish complete, closed input exactly once.
        }
        async Task<int> CountAsync() {
            var plain = new DbContextOptionsBuilder<Station01DbContext>()
                .UseSqlite(StoreCompatibilityProbe.ReadOnlyConnectionString(root)).Options;
            await using var db = new Station01DbContext(plain);
            return await db.StageEvents.CountAsync(e => e.EventId == request.EventId, watchdog.Token);
        }
        async Task WaitForEventAsync(string name) {
            var path = Path.Combine(root, "009-fault-events.jsonl");
            while (!File.Exists(path) || !(await File.ReadAllLinesAsync(path, watchdog.Token)).Any(s => s.Contains('"' + name + '"')))
                await Task.Delay(20, watchdog.Token);
        }
    }
}
