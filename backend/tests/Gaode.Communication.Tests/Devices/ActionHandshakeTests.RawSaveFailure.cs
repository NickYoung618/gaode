using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Application.Workflow;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Devices.Plc;
using Gaode.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Gaode.Communication.Tests.Devices;

public sealed partial class ActionHandshakeTests
{
    [Theory]
    [InlineData("PICK-RAW/rollback-business-writable", 0)]
    [InlineData("PICK-RAW/committed-reference-withheld", 1)]
    [InlineData("PICK-RAW/store-unavailable-until-reconciliation", 2)]
    public async Task ObservedPickWithoutDurableReferencePreservesReservation(string caseId, int faultKind)
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var plc = new ProtocolTcpFixture();
        await plc.StartAsync(watchdog.Token);
        await using var device = plc.Device();
        await StartAndPrepareAsync(device, watchdog.Token);
        var (request, _) = await CommittedPickProbe.PrepareAsync(plc,
            StageRequest(device, PlcWorkflowStage.Sorting), watchdog.Token);
        var connection = new SqliteConnectionStringBuilder { DataSource = plc.EvidenceStorePath,
            Pooling = false, DefaultTimeout = 1 }.ToString();
        var options = new DbContextOptionsBuilder<Station01DbContext>().UseSqlite(connection).Options;
        await using var faultConnection = new SqliteConnection(connection);
        await faultConnection.OpenAsync(watchdog.Token);
        using (var sql = faultConnection.CreateCommand())
        {
            if (faultKind == 0)
                sql.CommandText = "CREATE TRIGGER reject_pick_raw BEFORE INSERT ON PlcCommunicationEvidence " +
                    "BEGIN SELECT RAISE(ABORT,'controlled actual raw save failure'); END";
            else sql.CommandText = "PRAGMA journal_mode=DELETE";
            await sql.ExecuteNonQueryAsync(watchdog.Token);
        }
        var writer = Assert.IsType<TraceWriter>(plc.EvidenceWriter);
        var entered = new TaskCompletionSource<CommunicationEvidenceBatch>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (faultKind == 1)
            writer.AfterCommunicationCommitBeforeReceipt = async (batch, actual, _) =>
            {
                if (batch.ActionId != request.Correlation.ActionId || batch.Interpretation != "MaterialPicked") return;
                Assert.Equal(ActualCommitState.Committed, actual.ActualCommit);
                entered.TrySetResult(batch);
                await release.Task;
            };
        else writer.BeforeCommunicationCommit = async (batch, _) =>
        {
            if (batch.ActionId != request.Correlation.ActionId || batch.Interpretation != "MaterialPicked") return;
            if (faultKind == 2)
            {
                using var sql = faultConnection.CreateCommand();
                sql.CommandText = "BEGIN EXCLUSIVE"; await sql.ExecuteNonQueryAsync();
            }
            entered.TrySetResult(batch);
            if (faultKind == 2) await release.Task;
        };
        var probe = new ActualRawFailureProbe(new SortingTargetAllocator(new StageEventStore(options),
            TimeProvider.System, ComponentBudget().CriticalSave));
        var adapter = new LatestProtocolStageActionAdapter(device, ComponentBudget(), probe);
        CommunicationEvidenceBatch? captured = null;
        PlcStageActionResult? outcome = null;
        try
        {
            var operation = adapter.ExecuteAsync(request, watchdog.Token).AsTask();
            captured = await entered.Task.WaitAsync(watchdog.Token);
            Assert.NotEmpty(captured.Exchanges);
            outcome = await operation.WaitAsync(watchdog.Token);
            Assert.True(outcome.IsUnknownHeld, JsonSerializer.Serialize(outcome));
            Assert.False(outcome.CanRetry);
            Assert.Equal(0, probe.NormalCommitCalls);
            Assert.NotNull(probe.Notice);
            Assert.Equal(PhysicalPickState.Observed, probe.Notice.PhysicalPick);
            Assert.Equal(request.Correlation, probe.Notice.Correlation);
            Assert.NotNull(probe.FailureReceipt);
            Assert.Equal(BusinessCommitRecordKind.StageEvent, probe.FailureReceipt.RecordKind);
            Assert.Equal(MotionAvailability.HeldUnknown, device.Observe().MotionAvailability);
            RejectingPickPort.AssertNoPlace(plc.Store.GetWriteAudit());
            if (faultKind == 2)
            {
                using var reader = new SqliteConnection(connection);
                var busy = Assert.Throws<SqliteException>(() => {
                    reader.Open(); using var sql = reader.CreateCommand();
                    sql.CommandText = "SELECT count(*) FROM StageEvents"; sql.CommandTimeout = 1; sql.ExecuteScalar(); });
                Assert.Equal(5, busy.SqliteErrorCode);
                Assert.NotEqual(ReceiptValidity.ValidCurrent, probe.FailureReceipt?.Validity);
            }
            else
            {
                Assert.Equal(ActualCommitState.Committed, probe.FailureReceipt?.ActualCommit);
                await using var db = new Station01DbContext(options);
                Assert.Equal(faultKind == 1 ? 1 : 0, await db.PlcCommunicationEvidence.CountAsync(
                    e => e.EvidenceId == captured.EvidenceId, watchdog.Token));
            }
        }
        finally
        {
            if (faultKind == 2)
            {
                using var sql = faultConnection.CreateCommand(); sql.CommandText = "ROLLBACK";
                await sql.ExecuteNonQueryAsync(CancellationToken.None);
            }
            release.TrySetResult();
        }
        await writer.WaitForIdleAsync(watchdog.Token);
        await using var reopened = new Station01DbContext(options);
        var facts = await reopened.StageEvents.Where(e => e.RunId == request.RunId).ToArrayAsync(watchdog.Token);
        Assert.DoesNotContain(facts, e => e.PayloadJson.Contains("SortingAssignmentInTransit", StringComparison.Ordinal));
        Assert.Equal(faultKind == 2 ? 0 : 1,
            facts.Count(e => e.PayloadJson.Contains("PickEvidencePersistenceUnconfirmed", StringComparison.Ordinal)));
        var recovery = await new StageEventStore(options).RecoverAsync(request.RunId, request.TrayId,
            WholeTrayWorkflowStage.Sorting, watchdog.Token);
        Assert.True(recovery.DeviceHeld);
        Assert.False(recovery.AutomaticRetryAllowed);
        // RecoverAsync may append its real UnknownHeld recovery event. The later
        // raw query only reads committed facts; neither revives the old permission.
        var edge = device.HeartbeatEdges;
        await ProtocolTcpFixture.UntilAsync(() => device.HeartbeatEdges >= edge + 2, watchdog.Token);
        RejectingPickPort.AssertNoPlace(plc.Store.GetWriteAudit());
        var raw = await reopened.PlcCommunicationEvidence.AsNoTracking().SingleOrDefaultAsync(
            e => e.EvidenceId == captured!.EvidenceId, watchdog.Token);
        Assert.Equal(faultKind == 1, raw is not null);
        var root = Environment.GetEnvironmentVariable("GAODE_009_EVIDENCE_ROOT") ?? throw new InvalidOperationException("RawFailureEvidenceRootRequired");
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, caseId.Replace('/', '-') + "-" + request.RunId + ".json"),
            JsonSerializer.Serialize(new { caseId, request, outcome, probe.Notice, probe.FailureReceipt,
                rawPersisted = raw is not null, captured!.EvidenceId, plc.EvidenceStorePath, recovery,
                actualStageEvents = facts.Select(e => new { e.EventId, e.EventType, e.PayloadJson }),
                writes = plc.Store.GetWriteAudit(), scope = "FormalTcpAndActualSqliteComponent;IndependentHostStillRequired",
                storageUnavailableClaim = faultKind == 2 ? "NoNewFactPersistenceClaim;RecoveryUsesPriorReservationAndIntent" : null }), watchdog.Token);
    }

    private sealed class ActualRawFailureProbe(SortingTargetAllocator allocator) : IPickCommitPort
    {
        public int NormalCommitCalls;
        public PickEvidenceFailureNotice? Notice;
        public RequiredCommitEvidence? FailureReceipt;
        public Task<PickCommitReceipt> CommitPickAsync(PickCompletionEvidence evidence, CancellationToken token)
        {
            NormalCommitCalls++;
            throw new InvalidOperationException("NoValidRawReferenceMustNotEnterNormalCommitPick");
        }
        public async Task<RequiredCommitEvidence> ReportPickEvidenceFailureAsync(PickEvidenceFailureNotice notice, CancellationToken token)
        {
            Notice = notice;
            return FailureReceipt = await allocator.ReportPickEvidenceFailureAsync(notice, token);
        }
    }
}
