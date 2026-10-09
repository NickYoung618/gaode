using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Configuration;
using Gaode.Application.Ports;
using Gaode.Domain.Station01;
using Gaode.Application.Recipes;
using Gaode.Diagnostics;

namespace Gaode.Application.Station01;

public sealed class SaveGateException(string code, Guid writeId, string message) : Exception(message)
{
    public string Code { get; } = code;
    public Guid WriteId { get; } = writeId;
}

public sealed class RunExecution(Guid runId, Guid commandId, string requestId, string subjectId,
    string contextJson, FrozenConfiguration config, ITraceWriter writer,
    TimeProvider clock, Guid sessionId, string clockId)
{
    private int _fCaptureRequested;
    internal void ReserveSingleFCapture()
    {
        if (Interlocked.Exchange(ref _fCaptureRequested, 1) != 0)
            throw new InvalidOperationException("FAlreadyRequestedInThisRun_NoAutomaticReplay");
    }
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public Guid RunId { get; } = runId;
    public Guid CommandId { get; } = commandId;
    public string RequestId { get; } = requestId;
    public string SubjectId { get; } = subjectId;
    public string ContextJson { get; } = contextJson;
    public FrozenConfiguration Config { get; } = config;
    public Guid SessionId { get; } = sessionId;
    public string ClockId { get; } = clockId;
    public long Timestamp => clock.GetTimestamp();
    public TimeProvider Clock => clock;
    public long PersistedRevision { get; private set; }
    public WorkflowIdentity? Identity { get; private set; }
    public RecipeRunPlan? RecipePlan { get; private set; }
    public string? PlanRevision { get; private set; }
    public Guid? RecipeBindingId { get; private set; }
    public FrozenExecutionInputs? ExecutionInputs { get; internal set; }
    public CommittedAlgorithmSource? CommittedFSource { get; private set; }
    public TrayObservation? InitialObservation { get; private set; }
    public Guid? InitialObservationWriteId { get; private set; }
    public CorrelatedCaptureFact? InitialThreeDCapture { get; internal set; }
    public Guid? InitialThreeDCaptureWriteId { get; internal set; }
    internal void RecordInitialObservation(TrayObservation observation, CommitReceipt commit)
    {
        if (!observation.IsValid || observation.Purpose != TrayObservationPurpose.InitialPreparation || observation.RunId != RunId ||
            commit.State != CommitState.Committed || commit.WriteId == Guid.Empty || commit.CommittedRevision != PersistedRevision ||
            observation.TrayId != StartRunContextParser.Parse(ContextJson).TrayId || InitialObservation is not null)
            throw new InvalidOperationException("InitialObservationCommitMismatch");
        InitialObservation = observation;
        InitialObservationWriteId = commit.WriteId;
    }
    internal void RecordCommittedFSource(CommittedAlgorithmSource source)
    {
        if (source.RunId != RunId || source.CallId == Guid.Empty || source.CaptureId == Guid.Empty ||
            source.WriteId == Guid.Empty || source.CommittedRevision != PersistedRevision || CommittedFSource is not null)
            throw new InvalidOperationException("CommittedFSourceIdentityMismatch");
        CommittedFSource = source;
    }
    internal ActionWindow? RequiredSaveWindow { get; set; }
    internal CancellationToken RequiredSaveCancellation { get; set; }
    internal ActionCorrelation? RecipeApplicationCorrelation { get; set; }
    public RecipeBindingReceipt? RecipeApplicationReceipt { get; internal set; }
    public CancellationToken RecipeApplicationCancellation { get; internal set; }
    public IReadOnlyList<RequiredCommitEvidence> RecipeApplicationCommits => recipeCommits;
    private readonly List<RequiredCommitEvidence> recipeCommits = [];
    internal long? LastCommitObservedTick { get; private set; }
    internal void RecordRequiredCommit(CommitReceipt receipt, long received, string? purpose = null)
    {
        LastCommitObservedTick = received;
        if (RecipeApplicationCorrelation is not { } c || RequiredSaveWindow is not { } window) return;
        recipeCommits.Add(new(receipt.WriteId, c, receipt.State == CommitState.Committed ? ActualCommitState.Committed : ActualCommitState.Unknown,
            !RequiredSaveCancellation.IsCancellationRequested && window.Contains(received) ? ReceiptValidity.ValidCurrent : ReceiptValidity.Invalid,
            receipt.CommittedRevision, receipt.CommittedUtc, received, receipt.ErrorCode)
            { RecordKind = receipt.RecordKind, SavePurpose = purpose });
    }
    private CancellationTokenSource SaveLimit(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        RequiredSaveCancellation.ThrowIfCancellationRequested();
        var ms = RequiredSaveWindow is { } window ? Math.Min(Config.Budget.BusinessMs.CriticalSave,
            (window.DueTick - clock.GetTimestamp()) * 1000d / clock.TimestampFrequency) : Config.Budget.BusinessMs.CriticalSave;
        if (ms <= 0) throw new TimeoutException("RequiredSaveWindowClosed");
        var limit = CancellationTokenSource.CreateLinkedTokenSource(token, RequiredSaveCancellation);
        // TimeProvider is shared with receipt validation, including the controlled-clock boundary tests.
        var timer = clock.CreateTimer(_ => { try { limit.Cancel(); } catch (ObjectDisposedException) { } }, null,
            TimeSpan.FromMilliseconds(ms), Timeout.InfiniteTimeSpan);
        limit.Token.Register(() => timer.Dispose());
        return limit;
    }
    public Steps.StartPreparationEvidence? RecoveredStart { get; internal set; }
    public Func<ActionState?, CaptureState?, AlgorithmState?, SaveState?, HandoffState?, long?, Task>?
        Progress { get; set; }

    public Task ReportAsync(ActionState? action = null, CaptureState? capture = null,
        AlgorithmState? algorithm = null, SaveState? save = null,
        HandoffState? handoff = null, long? persistedRevision = null) =>
        Progress?.Invoke(action, capture, algorithm, save, handoff, persistedRevision) ?? Task.CompletedTask;
    public void AdoptInitialRevision(long revision)
    {
        if (PersistedRevision != 0 || revision <= 0) throw new InvalidOperationException("初始提交版本无效");
        PersistedRevision = revision;
    }
    internal async Task ApplyCompanionCommitAsync(long expected, long committed) {
        if (PersistedRevision != expected || committed != expected + 1) throw new InvalidOperationException("CompanionCommitRevisionConflict");
        PersistedRevision = committed;
        await ReportAsync(save: SaveState.Committed, persistedRevision: committed);
    }
    internal void AdoptClosedWorkflowRevision(PersistedRun persisted)
    {
        if (RequiredSaveWindow is not null || persisted.RunId != RunId ||
            persisted.RequestId != RequestId || persisted.SubjectId != SubjectId || persisted.ContextJson != ContextJson ||
            persisted.Terminal != TerminalOutcome.None || persisted.Revision < PersistedRevision)
            throw new InvalidOperationException("ClosedWorkflowRevisionIdentityMismatch");
        var previous = PersistedRevision;
        PersistedRevision = persisted.Revision;
        RuntimeDiagnostics.Record("WorkflowBoundary", "CommittedRevisionObserved", RunId,
            new { previous, persisted.Revision, purpose = "FaultClosureOnly", authorizesAction = false });
    }
    public void FreezeIdentity(WorkflowIdentity identity)
    {
        if (Identity is not null) throw new InvalidOperationException("WorkflowIdentityAlreadyFrozen");
        if (identity.RunId != RunId || !StringComparer.Ordinal.Equals(identity.RequestId, RequestId))
            throw new InvalidOperationException("WorkflowIdentityMismatch");
        Identity = identity;
    }

    public void BindRecipePlan(RecipeRunPlan plan, string planRevision, Guid bindingId)
    {
        if (RecipePlan is not null) throw new InvalidOperationException("RecipePlanAlreadyBound");
        if (string.IsNullOrWhiteSpace(planRevision) || bindingId == Guid.Empty)
            throw new ArgumentException("Recipe plan binding evidence is incomplete.");
        RecipePlan = plan;
        PlanRevision = planRevision;
        RecipeBindingId = bindingId;
    }

    public Task<CommitReceipt> SaveAsync(WriteKind kind, object payload,
        RunState? stateAfter = null, TerminalOutcome candidate = TerminalOutcome.None,
        string? handoffJson = null, Guid? handoffId = null,
        CancellationToken cancellationToken = default) => RuntimeDiagnostics.ObserveAsync(
            "CriticalSave", RunId, new { RequestId, CommandId, kind = kind.ToString(),
                expectedRevision = PersistedRevision, timeoutMs = Config.Budget.BusinessMs.CriticalSave,
                stateAfter = stateAfter?.ToString(), Config.SnapshotId },
            () => SaveCoreAsync(kind, payload, stateAfter, candidate, handoffJson, handoffId, cancellationToken),
            r => new { r.WriteId, state = r.State.ToString(), r.CommittedRevision, r.ErrorCode },
            r => r.State != CommitState.Committed);

    private async Task<CommitReceipt> SaveCoreAsync(WriteKind kind, object payload,
        RunState? stateAfter = null, TerminalOutcome candidate = TerminalOutcome.None,
        string? handoffJson = null, Guid? handoffId = null,
        CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        using var commit = await RunFactCommitCoordinator.Shared.EnterAsync(RunId, cancellationToken);
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        var writeId = Guid.NewGuid();
        var batch = new WriteBatch(writeId, RunId, PersistedRevision, kind, json, digest,
            stateAfter, TerminalOutcome.None, candidate, handoffJson,
            RequestId, SubjectId, ContextJson, handoffId);
        using var limit = SaveLimit(cancellationToken);
        QueuedWrite queued;
        try { queued = writer.SubmitCritical(batch, limit.Token, RequiredSaveWindow); }
        catch (Exception error)
        {
            RuntimeDiagnostics.Record("CriticalSave", "EnqueueFailed", RunId,
                new { writeId, kind = kind.ToString(), disposition = "NoCommitConfirmed" }, error);
            throw new SaveGateException("SaveEnqueueFailed", writeId, error.Message);
        }
        RuntimeDiagnostics.Record("CriticalSave", "Queued", RunId, new { writeId, kind = kind.ToString() });
        if (RequiredSaveWindow is null) await ReportAsync(save: SaveState.Queued);
        CommitReceipt receipt;
        try
        {
            receipt = await queued.Completion.WaitAsync(
                TimeSpan.FromMilliseconds(Config.Budget.BusinessMs.CriticalSave), clock, limit.Token);
        }
        catch (Exception error) when (error is TimeoutException or OperationCanceledException)
        {
            await ReportAsync(save: SaveState.CommitUnknown);
            throw new SaveGateException("CommitUnknown", writeId, "必要保存超时，提交结果未知");
        }
        var received = clock.GetTimestamp();
        using var fact = JsonDocument.Parse(json);
        var purpose = kind == WriteKind.HandoffV2 ? "Handoff" :
            fact.RootElement.TryGetProperty("kind", out var factKind) && factKind.GetString() == "RecipePlanBound" ? "RecipePlanBound" : null;
        RecordRequiredCommit(receipt, received, purpose);
        if (limit.IsCancellationRequested || RequiredSaveWindow is { } total && !total.Contains(received))
            throw new SaveGateException("CommitUnknown", writeId, "提交回执未在当前保存窗口有效取得；不能据此推断回滚");
        if (receipt.State != CommitState.Committed || receipt.CommittedRevision is null)
        {
            await ReportAsync(save: receipt.State == CommitState.ConditionRejected
                ? SaveState.ConditionRejected : SaveState.Failed);
            throw new SaveGateException(receipt.State.ToString(), writeId,
                "必要保存未提交：" + (receipt.ErrorCode ?? receipt.State.ToString()));
        }
        PersistedRevision = receipt.CommittedRevision.Value;
        if (RequiredSaveWindow is null) await ReportAsync(save: SaveState.Committed, persistedRevision: PersistedRevision);
        return receipt;
    }

    public Task<PublicPreparationHandoffV2> SaveHandoffV2Async(
        PublicPreparationHandoffV2 draft, CancellationToken cancellationToken = default) =>
        RuntimeDiagnostics.ObserveAsync("HandoffSave", RunId,
            new { RequestId, CommandId, draft.HandoffId, draft.PlanRevision,
                timeoutMs = Config.Budget.BusinessMs.CriticalSave },
            () => SaveHandoffV2CoreAsync(draft, cancellationToken),
            r => new { r.HandoffId, r.WriteId, r.CommittedRevision, r.PlanRevision });

    private async Task<PublicPreparationHandoffV2> SaveHandoffV2CoreAsync(
        PublicPreparationHandoffV2 draft, CancellationToken cancellationToken = default)
    {
        using var commit = await RunFactCommitCoordinator.Shared.EnterAsync(RunId, cancellationToken);
        if (draft.Identity.RunId != RunId || draft.Identity.TrayId == Guid.Empty ||
            draft.HandoffId == Guid.Empty)
            throw new ArgumentException("HandoffV2IdentityInvalid", nameof(draft));
        var writeId = Guid.NewGuid();
        var prepared = draft with
        {
            WriteId = writeId,
            CommittedRevision = PersistedRevision + 1,
            PersistedAt = clock.GetUtcNow(),
            PayloadDigest = ""
        };
        var finalized = prepared with
        {
            PayloadDigest = PublicPreparationHandoffV2.ComputePayloadDigest(prepared)
        };
        var json = JsonSerializer.Serialize(finalized, JsonOptions);
        var batch = new WriteBatch(writeId, RunId, PersistedRevision, WriteKind.HandoffV2,
            json, finalized.PayloadDigest, RunState.HandoffReady,
            HandoffId: finalized.HandoffId, TrayId: finalized.Identity.TrayId,
            HandoffV2Json: json);
        using var limit = SaveLimit(cancellationToken);
        QueuedWrite queued;
        try { queued = writer.SubmitCritical(batch, limit.Token, RequiredSaveWindow); }
        catch (Exception error)
        {
            RuntimeDiagnostics.Record("HandoffSave", "EnqueueFailed", RunId,
                new { writeId, draft.HandoffId, disposition = "NoCommitConfirmed" }, error);
            throw new SaveGateException("SaveEnqueueFailed", writeId, error.Message);
        }
        RuntimeDiagnostics.Record("HandoffSave", "Queued", RunId, new { writeId, draft.HandoffId });
        if (RequiredSaveWindow is null) await ReportAsync(save: SaveState.Queued, handoff: HandoffState.Saving);
        CommitReceipt receipt;
        try
        {
            receipt = await queued.Completion.WaitAsync(
                TimeSpan.FromMilliseconds(Config.Budget.BusinessMs.CriticalSave), clock, limit.Token);
        }
        catch (Exception error) when (error is TimeoutException or OperationCanceledException)
        {
            await ReportAsync(save: SaveState.CommitUnknown);
            throw new SaveGateException("CommitUnknown", writeId, "v2 handoff提交结果未知");
        }
        var received = clock.GetTimestamp();
        RecordRequiredCommit(receipt, received, "Handoff");
        if (limit.IsCancellationRequested || RequiredSaveWindow is { } total && !total.Contains(received))
            throw new SaveGateException("CommitUnknown", writeId, "handoff回执已失效；实际记录可能已提交");
        if (receipt.State != CommitState.Committed || receipt.CommittedRevision != finalized.CommittedRevision)
            throw new SaveGateException(receipt.State.ToString(), writeId,
                receipt.ErrorCode ?? "v2 handoff未提交");
        PersistedRevision = receipt.CommittedRevision.Value;
        if (RequiredSaveWindow is null) await ReportAsync(save: SaveState.Committed, handoff: RecipeApplicationCorrelation is null ? HandoffState.Ready : HandoffState.Saving,
            persistedRevision: PersistedRevision);
        return finalized;
    }
}
