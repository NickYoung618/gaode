using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Application.Workflow;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Devices.Plc;
using Gaode.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace Gaode.Communication.Tests.Devices;

public sealed partial class ActionHandshakeTests
{
    [Theory]
    [InlineData("PICK-STORE/rollback-before-commit", false, false)]
    [InlineData("PICK-STORE/committed-receipt-withheld", true, false)]
    [InlineData("PICK-STORE/committed-temporarily-unreadable", true, true)]
    public async Task ActualPickAndDatabaseOutcomeRemainSeparateFromPlacePermission(string caseId, bool afterCommit, bool unreadable)
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var plc = new ProtocolTcpFixture(motionDurationMs: 150);
        await plc.StartAsync(watchdog.Token);
        await using var device = plc.Device();
        await StartAndPrepareAsync(device, watchdog.Token);
        var (request, _) = await CommittedPickProbe.PrepareAsync(plc,
            StageRequest(device, PlcWorkflowStage.Sorting), watchdog.Token);
        var connectionString = new SqliteConnectionStringBuilder {
            DataSource = plc.EvidenceStorePath, Pooling = false, DefaultTimeout = 1 }.ToString();
        await using (var configure = new SqliteConnection(connectionString))
        {
            await configure.OpenAsync(watchdog.Token);
            using var command = configure.CreateCommand();
            if (!afterCommit)
            {
                command.CommandText = "CREATE TRIGGER reject_pick_commit BEFORE INSERT ON StageEvents " +
                    "WHEN json_extract(NEW.PayloadJson,'$.kind')='SortingAssignmentInTransit' " +
                    "BEGIN SELECT RAISE(ABORT,'controlled actual pick save failure'); END;";
                await command.ExecuteNonQueryAsync(watchdog.Token);
            }
            else if (unreadable)
            {
                command.CommandText = "PRAGMA journal_mode=DELETE";
                Assert.Equal("delete", Convert.ToString(await command.ExecuteScalarAsync(watchdog.Token)));
            }
        }
        await using var fault = new PickCommitReceiptHold(connectionString, afterCommit, unreadable);
        var options = new DbContextOptionsBuilder<Station01DbContext>().UseSqlite(connectionString).AddInterceptors(fault).Options;
        var gate = new ActualPickSaveProbe(plc, new SortingTargetAllocator(new StageEventStore(options),
            TimeProvider.System, ComponentBudget().CriticalSave));
        var adapter = new LatestProtocolStageActionAdapter(device, ComponentBudget(), gate);
        PlcStageActionResult? result = null;
        try
        {
            result = await adapter.ExecuteAsync(request, watchdog.Token);
            Assert.True(result.IsUnknownHeld, JsonSerializer.Serialize(result));
            Assert.False(result.CanRetry);
            Assert.True(gate.Calls == 1, JsonSerializer.Serialize(new { result, gate.Calls }) + plc.DeviceDiagnostics);
            Assert.NotNull(gate.Evidence);
            Assert.NotEmpty(gate.Evidence.DiagnosticEvidenceReferences);
            Assert.NotNull(gate.Receipt);
            Assert.False(gate.Receipt.MayAuthorizePlace(gate.Evidence, Stopwatch.GetTimestamp()));
            RejectingPickPort.AssertNoPlace(plc.Store.GetWriteAudit());
            if (afterCommit)
            {
                Assert.True(fault.Committed.Task.IsCompletedSuccessfully, "Fault point must be an actual successful DB commit.");
                Assert.Equal(ActualCommitState.Unknown, gate.Receipt.ActualCommit);
                if (unreadable)
                {
                    using var reader = new SqliteConnection(new SqliteConnectionStringBuilder(connectionString) {
                        Mode = SqliteOpenMode.ReadOnly }.ToString());
                    var busy = Assert.Throws<SqliteException>(() => {
                        reader.Open();
                        using var query = reader.CreateCommand(); query.CommandText = "SELECT count(*) FROM StageEvents";
                        query.CommandTimeout = 1; query.ExecuteScalar();
                    });
                    Assert.Equal(5, busy.SqliteErrorCode);
                }
                else Assert.Equal(1, await CountInTransitAsync());
            }
            else
            {
                Assert.Equal(ActualCommitState.ConfirmedRolledBack, gate.Receipt.ActualCommit);
                Assert.False(fault.Committed.Task.IsCompleted);
                Assert.Equal(0, await CountInTransitAsync());
            }
        }
        finally { fault.Release.TrySetResult(); }
        if (afterCommit) await fault.Exited.Task.WaitAsync(watchdog.Token);
        var actualRows = await CountInTransitAsync();
        Assert.Equal(afterCommit ? 1 : 0, actualRows);
        await using var readback = new Station01DbContext(options);
        var recovered = await new StageEventStore(options).RecoverAsync(request.RunId, request.TrayId,
            WholeTrayWorkflowStage.Sorting, watchdog.Token);
        Assert.True(recovered.DeviceHeld);
        Assert.False(recovered.AutomaticRetryAllowed);
        RejectingPickPort.AssertNoPlace(plc.Store.GetWriteAudit());
        var root = Environment.GetEnvironmentVariable("GAODE_009_EVIDENCE_ROOT") ?? throw new InvalidOperationException("PickStoreEvidenceRootRequired");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, caseId.Replace('/', '-') + "-" + request.RunId + ".json"),
            JsonSerializer.Serialize(new { caseId, request, result, gate.Evidence, gate.Receipt, actualRows,
                plc.EvidenceStorePath, recovered, writes = plc.Store.GetWriteAudit(), scope = "FormalTcpAndActualSqliteComponent;IndependentHostStillRequired" }), watchdog.Token);

        async Task<int> CountInTransitAsync()
        {
            await using var db = new Station01DbContext(options);
            return await db.StageEvents.CountAsync(e => e.RunId == request.RunId && e.PayloadJson.Contains("SortingAssignmentInTransit"), watchdog.Token);
        }
    }

    private sealed class ActualPickSaveProbe(ProtocolTcpFixture plc, SortingTargetAllocator allocator) : IPickCommitPort
    {
        public int Calls;
        public PickCompletionEvidence? Evidence;
        public PickCommitReceipt? Receipt;
        public async Task<PickCommitReceipt> CommitPickAsync(PickCompletionEvidence evidence, CancellationToken token)
        {
            Calls++; Evidence = evidence;
            RejectingPickPort.AssertNoPlace(plc.Store.GetWriteAudit());
            Receipt = await allocator.CommitPickAsync(evidence, token);
            return Receipt;
        }
        public Task<RequiredCommitEvidence> ReportPickEvidenceFailureAsync(PickEvidenceFailureNotice notice, CancellationToken token) =>
            allocator.ReportPickEvidenceFailureAsync(notice, token);
    }
    // Intercept only the receipt after EF has completed a real COMMIT. Never fabricate a commit failure.
    private sealed class PickCommitReceiptHold(string connection, bool enabled, bool lockReadback) : DbTransactionInterceptor, IAsyncDisposable
    {
        public readonly TaskCompletionSource Committed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private SqliteConnection? blocker;
        private int entered;
        public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            if (!enabled || Interlocked.Exchange(ref entered, 1) != 0) return;
            try
            {
                if (lockReadback)
                {
                    blocker = new SqliteConnection(connection); await blocker.OpenAsync();
                    using var sql = blocker.CreateCommand();
                    sql.CommandText = "PRAGMA locking_mode=EXCLUSIVE"; sql.ExecuteScalar();
                    sql.CommandText = "BEGIN EXCLUSIVE"; sql.ExecuteNonQuery();
                }
                Committed.TrySetResult();
                await Release.Task;
            }
            finally
            {
                if (blocker is not null) { await blocker.DisposeAsync(); blocker = null; }
                Exited.TrySetResult();
            }
        }
        public async ValueTask DisposeAsync()
        {
            Release.TrySetResult();
            if (entered != 0) await Exited.Task;
        }
    }
}
