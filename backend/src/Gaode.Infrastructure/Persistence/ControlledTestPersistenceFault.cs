using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Application.Workflow;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace Gaode.Infrastructure.Persistence;

// Finite independent-process Test instrumentation. Registration is rejected
// outside VirtualPlcIntegration + Virtual + Test. It never returns a fake receipt.
public sealed class ControlledTestPersistenceFault : IDisposable
{
    private readonly string root;
    private readonly string faultCase;
    private readonly string connectionString;
    private readonly CancellationTokenSource lifetime = new();
    private readonly object logLock = new();
    private int entered;
    private Arm? scheduledBinding;
    private sealed record Arm(string CaseId, Guid RunId, Guid Nonce);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static bool IsKnown(string value) => value is "F05-A" or "F05-B" or "F05-C" or
        "F06-A" or "F06-B" or "F06-C" or "BA04-late-bound" or "BA04-late-handoff" or
        "BA06-before-port" or "BA06-during-save" or "BA06-before-next" or "BA02-api-input";

    public ControlledTestPersistenceFault(string testRoot, string selectedCase)
    {
        if (!IsKnown(selectedCase)) throw new ArgumentException("UnknownTestPersistenceFaultCase");
        root = Path.GetFullPath(testRoot); faultCase = selectedCase;
        var store = StoreCompatibilityProbe.Inspect(root);
        if (!store.Compatible) throw new InvalidOperationException(store.Code);
        connectionString = new SqliteConnectionStringBuilder(StoreCompatibilityProbe.ReadWriteConnectionString(root))
            { Pooling = false, DefaultTimeout = 1 }.ToString();
        if (faultCase is "F05-C" or "F06-C") {
            using var connection = new SqliteConnection(connectionString); connection.Open();
            using var sql = connection.CreateCommand(); sql.CommandText = "PRAGMA journal_mode=DELETE";
            if (!string.Equals(Convert.ToString(sql.ExecuteScalar()), "delete", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("ControlledTestExclusiveLockUnavailable");
        }
    }

    public void Configure(DbContextOptionsBuilder<Station01DbContext> builder) =>
        builder.AddInterceptors(new BeforeSave(this), new AfterTransaction(this));

    public async Task BeforeRecipeBindingAsync(Guid runId, RecipeExecutionDeadlines? deadlines,
        int criticalSaveMs, CancellationToken token)
    {
        if (faultCase is not ("BA06-before-port" or "BA06-during-save") || Take(runId) is not { } arm) return;
        if (deadlines is null || criticalSaveMs <= 500) throw new InvalidOperationException("ControlledOriginalDeadlineRequired");
        scheduledBinding = arm;
        // Delay only this Test request at an existing semantic boundary. Neither
        // clock, frozen budget nor absolute deadline is changed by this seam.
        var until = deadlines.DetectionDeadlineUtc.AddMilliseconds(faultCase == "BA06-before-port" ? 100 : -(criticalSaveMs - 250));
        RecordScheduling(arm, "BeforeBindingHeld", deadlines, until);
        await WaitUntilAsync(until, token);
        RecordScheduling(arm, "BeforeBindingReleased", deadlines, until);
    }

    public async Task BeforeRecipeContinuationAsync(Guid runId, RecipeExecutionDeadlines? deadlines, CancellationToken token)
    {
        if (faultCase is not ("BA06-before-next" or "BA02-api-input") || Take(runId) is not { } arm) return;
        if (faultCase == "BA06-before-next")
        {
            if (deadlines is null) throw new InvalidOperationException("ControlledOriginalDeadlineRequired");
            var until = deadlines.DetectionDeadlineUtc.AddMilliseconds(100);
            RecordScheduling(arm, "BeforeContinuationHeld", deadlines, until);
            await WaitUntilAsync(until, token);
            RecordScheduling(arm, "BeforeContinuationReleased", deadlines, until);
        }
        else
        {
            RecordScheduling(arm, "ExistingHandoffInputHeld", deadlines, null);
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
            stop.CancelAfter(TimeSpan.FromSeconds(30)); // harness ceiling, never a business deadline
            while (true)
            {
                var path = Path.Combine(root, "009-fault-release.json");
                if (File.Exists(path))
                {
                    var release = JsonSerializer.Deserialize<Arm>(await File.ReadAllTextAsync(path, stop.Token), Json);
                    if (release == arm) break;
                }
                await Task.Delay(50, stop.Token);
            }
            RecordScheduling(arm, "ExistingHandoffInputReleased", deadlines, null);
        }
    }

    private async Task WaitUntilAsync(DateTimeOffset until, CancellationToken token)
    {
        if (until - DateTimeOffset.UtcNow > TimeSpan.FromMinutes(10)) throw new InvalidOperationException("ControlledSchedulingCaseTooLong");
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
        while (until > DateTimeOffset.UtcNow)
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Clamp((until - DateTimeOffset.UtcNow).TotalMilliseconds, 1, 200)), stop.Token);
    }

    private void RecordScheduling(Arm arm, string phase, RecipeExecutionDeadlines? deadlines, DateTimeOffset? until)
    {
        var value = JsonSerializer.Serialize(new { schemaVersion = "009-test-scheduling/1", arm.CaseId, arm.RunId,
            arm.Nonce, phase, originalDeadlines = deadlines, waitingUntilUtc = until, atUtc = DateTimeOffset.UtcNow,
            tick = Stopwatch.GetTimestamp(), processId = Environment.ProcessId, source = "ControlledTestInstrumentation" }, Json);
        lock (logLock) File.AppendAllText(Path.Combine(root, "009-fault-events.jsonl"), value + Environment.NewLine);
    }

    public TraceWriter CreateWriter(DbContextOptions<Station01DbContext> options, TimeProvider clock, int capacity)
    {
        var writer = new TraceWriter(options, clock, capacity, afterCommitBeforeReceipt: AfterTraceCommit);
        writer.BeforeCommunicationCommit = BeforeRawCommit;
        writer.AfterCommunicationCommitBeforeReceipt = AfterRawCommit;
        return writer;
    }

    private Arm? Take(Guid? runId)
    {
        if (Volatile.Read(ref entered) != 0 || runId is null) return null;
        var path = Path.Combine(root, "009-fault-arm.json");
        if (!File.Exists(path)) return null;
        // Malformed/changed arm files are explicit Test setup failures, not ignored injections.
        var arm = JsonSerializer.Deserialize<Arm>(File.ReadAllText(path), Json);
        if (arm is null || arm.Nonce == Guid.Empty || arm.CaseId != faultCase || arm.RunId == Guid.Empty)
            throw new InvalidDataException("InvalidControlledTestFaultArm");
        return arm.RunId == runId && Interlocked.CompareExchange(ref entered, 1, 0) == 0 ? arm : null;
    }

    private static StageEventEntity? PickEvent(DbContext? db) => db?.ChangeTracker.Entries<StageEventEntity>()
        .Select(e => e.Entity).SingleOrDefault(e => HasKind(e.PayloadJson, "SortingAssignmentInTransit"));
    private static bool HasKind(string payload, string kind) {
        using var json = JsonDocument.Parse(payload);
        return json.RootElement.TryGetProperty("kind", out var value) && value.GetString() == kind;
    }

    private sealed class BeforeSave(ControlledTestPersistenceFault owner) : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            if (owner.faultCase != "F06-A") return result;
            var db = eventData.Context!;
            var item = db.ChangeTracker.Entries<CommunicationEvidenceEntity>().Select(e => e.Entity)
                .SingleOrDefault(e => {
                    using var raw = JsonDocument.Parse(e.RawPayloadJson);
                    return raw.RootElement.TryGetProperty("interpretation", out var value) && value.GetString() == "MaterialPicked";
                });
            if (item is null || owner.Take(item.RunId) is not { } arm) return result;
            using var sql = db.Database.GetDbConnection().CreateCommand();
            sql.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
            // Connection-local trigger exercises the actual insert/rollback without
            // changing the store's persistent schema or its compatibility manifest.
            sql.CommandText = "CREATE TEMP TRIGGER reject_009_raw BEFORE INSERT ON main.PlcCommunicationEvidence " +
                $"WHEN lower(NEW.EvidenceId)=lower('{item.EvidenceId:D}') " +
                "BEGIN SELECT RAISE(ABORT,'009 actual raw save rejected before commit'); END";
            sql.ExecuteNonQuery();
            owner.Record(arm, "SqliteRejectionInstalledBeforeInsert", item.EvidenceId, null);
            return result;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (owner.faultCase != "F05-A" || PickEvent(eventData.Context) is not { } item || owner.Take(item.RunId) is not { } arm)
                return result;
            var db = eventData.Context!;
            using var sql = db.Database.GetDbConnection().CreateCommand();
            sql.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
            sql.CommandText = "CREATE TEMP TRIGGER reject_009_pick BEFORE INSERT ON main.StageEvents " +
                $"WHEN lower(NEW.EventId)=lower('{item.EventId:D}') " +
                "BEGIN SELECT RAISE(ABORT,'009 actual pick save rejected before commit'); END";
            await sql.ExecuteNonQueryAsync(cancellationToken);
            owner.Record(arm, "SqliteRejectionInstalledBeforeInsert", item.EventId, null);
            return result;
        }
    }

    private sealed class AfterTransaction(ControlledTestPersistenceFault owner) : DbTransactionInterceptor
    {
        public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (owner.faultCase is not ("F05-B" or "F05-C") || PickEvent(eventData.Context) is not { } item || owner.Take(item.RunId) is not { } arm)
                return;
            owner.Record(arm, "ActualCommitCompleted", item.EventId, "Committed");
            await owner.HoldAsync(arm, item.EventId, owner.faultCase == "F05-C");
        }
    }

    private async Task BeforeRawCommit(CommunicationEvidenceBatch batch, CancellationToken token)
    {
        if (faultCase != "F06-C" || batch.Interpretation != "MaterialPicked" || Take(batch.RunId) is not { } arm) return;
        Record(arm, "ObservedPickBeforeRawCommit", batch.EvidenceId, "NotYetCommitted");
        await HoldAsync(arm, batch.EvidenceId, lockDatabase: true);
    }

    private async Task AfterRawCommit(CommunicationEvidenceBatch batch, EvidenceCommitResult result, CancellationToken token)
    {
        if (faultCase != "F06-B" || batch.Interpretation != "MaterialPicked" ||
            result.ActualCommit != ActualCommitState.Committed || Take(batch.RunId) is not { } arm) return;
        Record(arm, "ActualCommitCompleted", batch.EvidenceId, "Committed");
        await HoldAsync(arm, batch.EvidenceId, lockDatabase: false);
    }

    private async Task AfterTraceCommit(WriteBatch batch, CommitReceipt receipt, CancellationToken token)
    {
        if (faultCase == "BA06-during-save" && receipt.State == CommitState.Committed &&
            batch.Kind == WriteKind.ActionFact && HasKind(batch.PayloadJson, "RecipePlanBound") &&
            scheduledBinding is { } scheduled && scheduled.RunId == batch.RunId)
        {
            Record(scheduled, "ActualCommitCompleted", batch.WriteId, "Committed");
            await HoldAsync(scheduled, batch.WriteId, lockDatabase: false);
            return;
        }
        if (receipt.State != CommitState.Committed ||
            !(faultCase == "BA04-late-bound" && batch.Kind == WriteKind.ActionFact && HasKind(batch.PayloadJson, "RecipePlanBound") ||
              faultCase == "BA04-late-handoff" && batch.Kind == WriteKind.HandoffV2) || Take(batch.RunId) is not { } arm) return;
        Record(arm, "ActualCommitCompleted", batch.WriteId, "Committed");
        await HoldAsync(arm, batch.WriteId, lockDatabase: false);
    }

    private async Task HoldAsync(Arm arm, Guid identity, bool lockDatabase)
    {
        await using var blocker = new SqliteConnection(connectionString);
        if (lockDatabase) {
            await blocker.OpenAsync(lifetime.Token);
            using var sql = blocker.CreateCommand(); sql.CommandText = "BEGIN EXCLUSIVE";
            await sql.ExecuteNonQueryAsync(lifetime.Token);
            Record(arm, "ActualExclusiveLockAcquired", identity, null);
        }
        Record(arm, "ReceiptHeld", identity, null);
        var start = Stopwatch.GetTimestamp();
        var releaseReason = "TestHoldLimitExpired";
        try {
            while (Stopwatch.GetElapsedTime(start) < TimeSpan.FromSeconds(30)) {
                var release = Path.Combine(root, "009-fault-release.json");
                if (File.Exists(release)) {
                    using var data = JsonDocument.Parse(await File.ReadAllTextAsync(release, lifetime.Token));
                    if (data.RootElement.GetProperty("runId").GetGuid() == arm.RunId &&
                        data.RootElement.GetProperty("nonce").GetGuid() == arm.Nonce) { releaseReason = "MatchingTestRelease"; break; }
                }
                await Task.Delay(50, lifetime.Token);
            }
        }
        finally {
            if (lockDatabase) await blocker.CloseAsync();
            Record(arm, "ReceiptReleased", identity, null, releaseReason);
        }
    }

    private void Record(Arm arm, string phase, Guid identity, string? actualCommit, string? reason = null)
    {
        var value = JsonSerializer.Serialize(new { schemaVersion = "009-test-persistence-fault/1", arm.CaseId,
            arm.RunId, arm.Nonce, phase, identity, actualCommit, reason, atUtc = DateTimeOffset.UtcNow,
            tick = Stopwatch.GetTimestamp(), processId = Environment.ProcessId, source = "ControlledTestInstrumentation" }, Json);
        try { lock (logLock) File.AppendAllText(Path.Combine(root, "009-fault-events.jsonl"), value + Environment.NewLine); }
        catch (IOException error) { Console.Error.WriteLine("009 fault evidence unavailable: " + error.Message); }
    }
    public void Dispose() { lifetime.Cancel(); lifetime.Dispose(); }
}
