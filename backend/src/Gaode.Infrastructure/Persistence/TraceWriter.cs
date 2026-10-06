using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Gaode.Application.Ports;
using Gaode.Application.Station01;
using Gaode.Domain.Station01;
using Microsoft.EntityFrameworkCore;
using Gaode.Diagnostics;

namespace Gaode.Infrastructure.Persistence;

public sealed partial class TraceWriter : ITraceWriter, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Channel<WriterJob> _queue;
    private readonly ConcurrentDictionary<Guid, Pending> _pending = new();
    private readonly DbContextOptions<Station01DbContext> _options;
    private readonly TimeProvider _clock;
    private readonly Func<WriteBatch, CancellationToken, Task>? _beforeCommit;
    private readonly Func<WriteBatch, CommitReceipt, CancellationToken, Task>? _afterCommitBeforeReceipt;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Task _consumer;
    public int PendingCount => _pending.Count + _communicationPending.Count;
    public event Action<Guid>? RunCommitted;

    public TraceWriter(DbContextOptions<Station01DbContext> options, TimeProvider clock, int capacity,
        Func<WriteBatch, CancellationToken, Task>? beforeCommit = null,
        Func<WriteBatch, CommitReceipt, CancellationToken, Task>? afterCommitBeforeReceipt = null)
    {
        _options = options;
        _clock = clock;
        _beforeCommit = beforeCommit;
        _afterCommitBeforeReceipt = afterCommitBeforeReceipt;
        _queue = Channel.CreateBounded<WriterJob>(new BoundedChannelOptions(capacity)
        { SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait });
        _consumer = Task.Run(ConsumeAsync);
    }

    public QueuedWrite SubmitCritical(WriteBatch batch, CancellationToken cancellationToken = default, ActionWindow? window = null)
    {
        if (!PersistenceContract.IsValid(batch)) throw new ArgumentException("保存批次身份或终态合同不完整");
        cancellationToken.ThrowIfCancellationRequested();
        if (window is not null && !window.Contains(_clock.GetTimestamp())) throw new TimeoutException("RequiredSaveWindowClosed");
        // The recipe application's explicitly registered window closes queued success
        // writes. Ordinary accepted critical writes retain the established drain and
        // reconciliation contract; cancelling their caller is not a database rollback.
        var current = _pending.GetOrAdd(batch.WriteId, _ => new Pending(batch,
            window is null ? CancellationToken.None : cancellationToken, window));
        if (current.Batch != batch && (current.Batch.PayloadDigest != batch.PayloadDigest || current.Batch.RunId != batch.RunId))
            throw new InvalidOperationException("WriteId对应不同载荷");
        if (!current.Enqueued)
        {
            lock (current)
            {
                if (!current.Enqueued)
                {
                    if (!_queue.Writer.TryWrite(current))
                    {
                        _pending.TryRemove(batch.WriteId, out _);
                        throw new InvalidOperationException("保存通道容量不足");
                    }
                    current.Enqueued = true;
                }
            }
        }
        return new(new(batch.WriteId, batch.RunId, CommitState.Queued, null, TerminalOutcome.None, null),
            current.Done.Task);
    }

    public async Task<CommitReceipt?> ReconcileAsync(Guid writeId, CancellationToken cancellationToken)
    {
        await using var ctx = new Station01DbContext(_options);
        var row = await ctx.Writes.AsNoTracking().SingleOrDefaultAsync(x => x.WriteId == writeId, cancellationToken);
        if (row is not null)
        {
            var run = await ctx.Runs.AsNoTracking().SingleAsync(x => x.RunId == row.RunId, cancellationToken);
            return new(writeId, row.RunId, CommitState.Committed, row.Revision, run.Terminal, null, row.CommittedUtc);
        }
        if (_pending.TryGetValue(writeId, out var pending) && pending.Done.Task.IsCompleted)
            return await pending.Done.Task;
        return null;
    }

    public async Task WaitForIdleAsync(CancellationToken cancellationToken)
    {
        while (!_pending.IsEmpty || !_communicationPending.IsEmpty)
            await Task.Delay(TimeSpan.FromMilliseconds(10), _clock, cancellationToken);
    }

    private async Task ConsumeAsync()
    {
        try
        {
            await foreach (var job in _queue.Reader.ReadAllAsync(_lifetime.Token))
            {
                if (job is CommunicationPending communication)
                {
                    await ConsumeCommunicationAsync(communication);
                    continue;
                }
                var pending = (Pending)job;
                var started = System.Diagnostics.Stopwatch.GetTimestamp();
                RuntimeDiagnostics.Record("DatabaseCommit", "Started", pending.Batch.RunId,
                    new { pending.Batch.WriteId, kind = pending.Batch.Kind.ToString(),
                        pending.Batch.ExpectedRevision, queueCount = PendingCount });
                if (_beforeCommit is not null)
                    await _beforeCommit(pending.Batch, _lifetime.Token);
                var commitTask = Task.Run(() => Commit(pending), _lifetime.Token);
                RuntimeDiagnostics.Record("DatabaseCommit", "TaskSubmitted", pending.Batch.RunId,
                    new { pending.Batch.WriteId, taskId = commitTask.Id });
                var result = await commitTask;
                RuntimeDiagnostics.Record("DatabaseCommit", "Returned", pending.Batch.RunId,
                    new { pending.Batch.WriteId, state = result.State.ToString(),
                        result.CommittedRevision, result.ErrorCode },
                    warning: result.State != CommitState.Committed,
                    elapsedMs: System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                if (_afterCommitBeforeReceipt is not null)
                    await _afterCommitBeforeReceipt(pending.Batch, result, _lifetime.Token);
                pending.Done.TrySetResult(result);
                RuntimeDiagnostics.Record("DatabaseCommit", "ReceiptPublished", pending.Batch.RunId,
                    new { pending.Batch.WriteId, state = result.State.ToString() });
                _pending.TryRemove(pending.Batch.WriteId, out _);
                if (result.State == CommitState.Committed)
                {
                    try { RunCommitted?.Invoke(result.RunId); }
                    catch (Exception error) { RuntimeDiagnostics.Record("RunNotification", "CommitHintFailed", result.RunId,
                        new { result.WriteId, actualCommit = "Committed" }, error); }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception error)
        {
            RuntimeDiagnostics.Record("DatabaseWriter", "ConsumerFailed", null,
                new { pending = _pending.Values.Select(x => new { x.Batch.RunId, x.Batch.WriteId }).Take(32).ToArray(),
                    pendingCount = PendingCount, disposition = "PendingCommitResultsNotConfirmed" }, error);
            throw;
        }
    }

    private CommitReceipt Commit(Pending pending)
    {
        var batch = pending.Batch;
        RuntimeDiagnostics.Record("DatabaseCommit", "TaskExecuting", batch.RunId, new { batch.WriteId, taskId = Task.CurrentId });
        // This is the transaction-start admission. Once admitted, cancellation does not pretend to undo a commit.
        if (pending.Cancellation.IsCancellationRequested || pending.Window is { } window && !window.Contains(_clock.GetTimestamp()))
            return new(batch.WriteId, batch.RunId, CommitState.ConditionRejected, null, TerminalOutcome.None, "RequiredSaveAdmissionClosed");
        var commitStarted = false;
        try
        {
            using var ctx = new Station01DbContext(_options);
            RuntimeDiagnostics.Record("DatabaseCommit", "TransactionStarting", batch.RunId, new { batch.WriteId });
            using var transaction = ctx.Database.BeginTransaction();
            RuntimeDiagnostics.Record("DatabaseCommit", "TransactionStarted", batch.RunId, new { batch.WriteId });
            if (pending.Cancellation.IsCancellationRequested || pending.Window is { } currentWindow && !currentWindow.Contains(_clock.GetTimestamp()))
                return new(batch.WriteId, batch.RunId, CommitState.ConditionRejected, null, TerminalOutcome.None, "RequiredSaveAdmissionClosed");
            var old = ctx.Writes.AsNoTracking().SingleOrDefault(x => x.WriteId == batch.WriteId);
            if (old is not null)
            {
                if (old.PayloadDigest != batch.PayloadDigest || old.RunId != batch.RunId || old.Kind != batch.Kind.ToString())
                    return new(batch.WriteId, batch.RunId, CommitState.Failed, null, TerminalOutcome.None, "WriteConflict");
                var oldRun = ctx.Runs.AsNoTracking().Single(x => x.RunId == batch.RunId);
                return new(batch.WriteId, batch.RunId, CommitState.Committed, old.Revision, oldRun.Terminal, null, old.CommittedUtc);
            }
            var nextRevision = batch.ExpectedRevision + 1;
            if (batch.Kind == WriteKind.RunCreated)
            {
                if (batch.ExpectedRevision != 0 || ctx.Runs.Any(x => x.RunId == batch.RunId))
                    return new(batch.WriteId, batch.RunId, CommitState.ConditionRejected, null, TerminalOutcome.None, "RunExists");
                var created = JsonSerializer.Deserialize<RunCreatedPayload>(batch.PayloadJson, JsonOptions)!;
                ctx.Runs.Add(new RunEntity
                {
                    RunId = batch.RunId, RequestId = created.RequestId, SubjectId = created.SubjectId,
                    ContextJson = created.ContextJson, State = RunState.Created, Revision = nextRevision,
                    Terminal = TerminalOutcome.None, CreatedUtc = _clock.GetUtcNow()
                });
                ctx.Commands.Add(new CommandEntity
                {
                    CommandId = created.CommandId, RunId = batch.RunId,
                    RequestId = created.RequestId, SubjectId = created.SubjectId,
                    PayloadDigest = created.CanonicalRequest is null ? batch.PayloadDigest :
                        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(created.CanonicalRequest))),
                    ReceiptState = "Committed", Applied = true
                });
            }
            else
            {
                var current = ctx.Runs.AsNoTracking().SingleOrDefault(x => x.RunId == batch.RunId);
                if (current is null || current.Revision != batch.ExpectedRevision ||
                    current.Terminal != TerminalOutcome.None || batch.ExpectedTerminal != TerminalOutcome.None)
                    return new(batch.WriteId, batch.RunId, CommitState.ConditionRejected,
                        current?.Revision, current?.Terminal ?? TerminalOutcome.None, "RevisionOrTerminalMismatch");
                if (batch.CandidateTerminal != TerminalOutcome.None)
                {
                    if (batch.Kind is not (WriteKind.Complete or WriteKind.Cancel))
                        return new(batch.WriteId, batch.RunId, CommitState.Failed, null, current.Terminal, "TerminalBatchInvalid");
                    var finalState = batch.CandidateTerminal switch
                    {
                        TerminalOutcome.Completed => RunState.Completed,
                        TerminalOutcome.CompletedWithExceptions => RunState.CompletedWithExceptions,
                        TerminalOutcome.Cancelled => RunState.Cancelled,
                        _ => throw new InvalidOperationException("无效终态")
                    };
                    var affected = ctx.Runs.Where(x => x.RunId == batch.RunId && x.Revision == batch.ExpectedRevision &&
                            x.Terminal == TerminalOutcome.None)
                        .ExecuteUpdate(set => set.SetProperty(x => x.Revision, nextRevision)
                            .SetProperty(x => x.State, finalState)
                            .SetProperty(x => x.Terminal, batch.CandidateTerminal)
                            .SetProperty(x => x.TerminalRevision, nextRevision));
                    if (affected != 1)
                        return new(batch.WriteId, batch.RunId, CommitState.ConditionRejected, null, TerminalOutcome.None, "TerminalRace");
                    if (batch.Kind == WriteKind.Complete)
                    {
                        if (batch.HandoffJson is null || batch.CandidateTerminal == TerminalOutcome.Cancelled)
                            return new(batch.WriteId, batch.RunId, CommitState.Failed, null, TerminalOutcome.None, "HandoffMissing");
                        ctx.Handoffs.Add(new HandoffEntity
                        {
                            HandoffId = batch.HandoffId ?? Guid.NewGuid(), RunId = batch.RunId,
                            PayloadJson = batch.HandoffJson, Revision = nextRevision,
                            Terminal = batch.CandidateTerminal, WriteId = batch.WriteId
                        });
                    }
                    else if (batch.HandoffJson is not null)
                        return new(batch.WriteId, batch.RunId, CommitState.Failed, null, TerminalOutcome.None, "CancelledHandoffForbidden");
                }
                else
                {
                    var after = batch.StateAfter ?? current.State;
                    if (!RunStateRules.IsCompatible(after, TerminalOutcome.None))
                        return new(batch.WriteId, batch.RunId, CommitState.Failed, null, current.Terminal, "RunStateInvalid");
                    var affected = ctx.Runs.Where(x => x.RunId == batch.RunId && x.Revision == batch.ExpectedRevision &&
                            x.Terminal == TerminalOutcome.None)
                        .ExecuteUpdate(set => set.SetProperty(x => x.Revision, nextRevision)
                            .SetProperty(x => x.State, after));
                    if (affected != 1)
                        return new(batch.WriteId, batch.RunId, CommitState.ConditionRejected, null, current.Terminal, "RevisionRace");
                }
            }
            if (batch.Kind == WriteKind.Audit) {
                using var audit = JsonDocument.Parse(batch.PayloadJson);
                if (audit.RootElement.TryGetProperty("kind", out var auditKind) && auditKind.GetString() == "RecoveryNewRunLinked") {
                    var r = audit.RootElement;
                    var newRunId = r.GetProperty("newRunId").GetGuid();
                    var commandId = r.GetProperty("commandId").GetGuid();
                    var newRequestId = r.GetProperty("requestId").GetString();
                    var newExpected = r.GetProperty("newRunExpectedRevision").GetInt64();
                    var linkWriteId = r.GetProperty("newRunLinkWriteId").GetGuid();
                    var initialCheckId = r.GetProperty("initialCheckId").GetGuid();
                    var consumed = ctx.Writes.AsNoTracking().Where(x => x.RunId == batch.RunId && x.Kind == "Audit").ToList().Any(x => {
                        using var previous = JsonDocument.Parse(x.PayloadJson);
                        return previous.RootElement.TryGetProperty("kind", out var k) && k.GetString() == "RecoveryNewRunLinked" &&
                            previous.RootElement.TryGetProperty("initialCheckId", out var c) && c.GetGuid() == initialCheckId;
                    });
                    if (consumed || !ctx.Commands.Any(x => x.CommandId == commandId && x.RunId == newRunId && x.RequestId == newRequestId && x.Applied == true))
                        return new(batch.WriteId, batch.RunId, CommitState.ConditionRejected, null, TerminalOutcome.None, "RestartIdentityOrCheckConflict");
                    var changed = ctx.Runs.Where(x => x.RunId == newRunId && x.Revision == newExpected && x.Terminal == TerminalOutcome.None)
                        .ExecuteUpdate(set => set.SetProperty(x => x.Revision, newExpected + 1));
                    if (changed != 1) return new(batch.WriteId, batch.RunId, CommitState.ConditionRejected, null, TerminalOutcome.None, "RestartCompanionRevisionConflict");
                    var json = JsonSerializer.Serialize(new { kind = "RecoveryFromFaultRun", faultRunId = batch.RunId,
                        newRunId, commandId, requestId = newRequestId, initialCheckId,
                        resetId = r.GetProperty("resetId").GetGuid(), fullPublicPreparationRequired = true,
                        linkedWriteId = batch.WriteId, checkConsumed = true }, JsonOptions);
                    ctx.Writes.Add(new WriteEntity { WriteId = linkWriteId, RunId = newRunId, Revision = newExpected + 1,
                        Kind = "Audit", PayloadJson = json, CommittedUtc = _clock.GetUtcNow(),
                        PayloadDigest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json))) });
                }
            }
            Project(batch, ctx);
            ctx.Writes.Add(new WriteEntity
            {
                WriteId = batch.WriteId, RunId = batch.RunId, Revision = nextRevision,
                Kind = batch.Kind.ToString(), PayloadJson = batch.PayloadJson,
                PayloadDigest = batch.PayloadDigest, CommittedUtc = _clock.GetUtcNow()
            });
            ctx.SaveChanges();
            commitStarted = true;
            RuntimeDiagnostics.Record("DatabaseCommit", "TransactionCommitting", batch.RunId, new { batch.WriteId });
            transaction.Commit();
            RuntimeDiagnostics.Record("DatabaseCommit", "TransactionCommitted", batch.RunId, new { batch.WriteId });
            return new(batch.WriteId, batch.RunId, CommitState.Committed, nextRevision,
                batch.CandidateTerminal, null, _clock.GetUtcNow());
        }
        catch (Exception ex)
        {
            RuntimeDiagnostics.Record("DatabaseCommit", "Failed", batch.RunId,
                new { batch.WriteId, kind = batch.Kind.ToString(), batch.ExpectedRevision,
                    disposition = "ReturnFailedReceipt_NoSuccessfulCommitClaim" }, ex);
            return new(batch.WriteId, batch.RunId, commitStarted ? CommitState.CommitUnknown : CommitState.Failed, null, TerminalOutcome.None,
                ex.GetType().Name + ":" + ex.Message);
        }
    }

    private static void Project(WriteBatch batch, Station01DbContext ctx)
    {
        if (batch.Kind is WriteKind.ActionIntent or WriteKind.CaptureIntent or WriteKind.StartIntent)
        {
            var intent = JsonSerializer.Deserialize<OperationIntentPayload>(batch.PayloadJson, JsonOptions)!;
            var current = ctx.Operations.Find(intent.OperationId);
            if (current is not null)
            {
                if (current.RunId != batch.RunId || current.Kind != intent.Kind ||
                    current.Attempt != 1 || intent.Attempt != 2)
                    throw new InvalidOperationException("OperationRetryIdentityOrAttemptMismatch");
                current.Attempt = intent.Attempt;
                current.IntentWriteId = batch.WriteId;
                current.State = "IntentCommitted";
                current.EvidenceJson = batch.PayloadJson;
            }
            else ctx.Operations.Add(new OperationEntity
                {
                    OperationId = intent.OperationId, RunId = batch.RunId, Kind = intent.Kind,
                    Attempt = intent.Attempt, IntentWriteId = batch.WriteId,
                    State = "IntentCommitted", EvidenceJson = batch.PayloadJson
                });
        }
        else if (batch.Kind == WriteKind.AlgorithmIntent)
        {
            var intent = JsonSerializer.Deserialize<AlgorithmIntentPayload>(batch.PayloadJson, JsonOptions)!;
            ctx.AlgorithmCalls.Add(new AlgorithmCallEntity
            {
                CallId = intent.CallId, OperationId = intent.OperationId, Attempt = intent.Attempt,
                RunId = intent.RunId, CaptureId = intent.CaptureId, IntentWriteId = batch.WriteId,
                InputMediaIdsJson = JsonSerializer.Serialize(intent.InputMediaIds),
                PublicVersion = intent.PublicVersion, ScopeVersion = intent.ScopeVersion,
                ParametersVersion = intent.ParametersVersion, CapabilityId = intent.CapabilityId,
                CapabilityVersion = intent.CapabilityVersion,
                ExpectedComponentVersion = intent.ExpectedComponentVersion,
                SessionId = intent.SessionId, ClockId = intent.ClockId,
                StartTick = intent.StartTick, DueTick = intent.DueTick,
                BudgetMs = intent.BudgetMs, InvocationBasis = intent.InvocationBasis,
                DispatchEvidence = "Unknown", TechnicalState = "Queued"
            });
        }
        else if (batch.Kind == WriteKind.AlgorithmFact)
        {
            var fact = JsonSerializer.Deserialize<AlgorithmFactPayload>(batch.PayloadJson, JsonOptions)!;
            var call = ctx.AlgorithmCalls.Single(x => x.CallId == fact.CallId);
            call.TechnicalState = fact.State.ToString();
            call.DispatchEvidence = fact.DispatchEvidence;
        }
        else if (batch.Kind == WriteKind.Media)
        {
            var media = JsonSerializer.Deserialize<MediaRef>(batch.PayloadJson, JsonOptions)!;
            ctx.Media.Add(new MediaEntity
            {
                MediaId = media.MediaId, RunId = media.RunId, CaptureId = media.CaptureId,
                RelativeKey = media.RelativeKey, ByteLength = media.ByteLength,
                Format = media.Format, Source = media.Source, State = media.StorageState
            });
        }
        else if (batch.Kind == WriteKind.HandoffV2)
        {
            var handoff = JsonSerializer.Deserialize<PublicPreparationHandoffV2>(
                batch.HandoffV2Json!, JsonOptions)!;
            if (!handoff.IsComplete || handoff.Identity.RunId != batch.RunId ||
                handoff.Identity.TrayId != batch.TrayId!.Value ||
                handoff.HandoffId != batch.HandoffId || handoff.WriteId != batch.WriteId ||
                handoff.CommittedRevision != batch.ExpectedRevision + 1 ||
                !StringComparer.Ordinal.Equals(handoff.PayloadDigest, batch.PayloadDigest))
                throw new InvalidOperationException("HandoffV2ProjectionInvalid");
            ctx.PublicPreparationHandoffsV2.Add(new PublicPreparationHandoffV2Entity
            {
                HandoffId = handoff.HandoffId, RunId = batch.RunId,
                TrayId = batch.TrayId.Value, WriteId = batch.WriteId,
                Revision = handoff.CommittedRevision, PayloadJson = batch.HandoffV2Json!,
                PayloadDigest = batch.PayloadDigest, PersistedUtc = handoff.PersistedAt
            });
        }
    }

    public async ValueTask DisposeAsync()
    {
        _queue.Writer.TryComplete();
        await _consumer;
        _lifetime.Dispose();
    }

    private abstract class WriterJob;
    private sealed class Pending(WriteBatch batch, CancellationToken cancellation, ActionWindow? window) : WriterJob
    {
        public CancellationToken Cancellation { get; } = cancellation;
        public ActionWindow? Window { get; } = window;
        public WriteBatch Batch { get; } = batch;
        public TaskCompletionSource<CommitReceipt> Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Enqueued { get; set; }
    }
}
