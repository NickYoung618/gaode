using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Motion;
using Gaode.Application.Ports;
using Gaode.Diagnostics;
using Gaode.Domain.Station01;

namespace Gaode.Application.Station01;

public sealed record CommissioningRestartFrom(Guid RunId, Guid RecoveryWriteId);
public sealed record CommissioningResetResult(bool Reset, bool ManualStartRequired,
    bool RecoveryClosed, Guid? RunId, Guid? RecoveryWriteId);

// An explicit full reset closes a failed run; it never resumes its steps or claims inspection completion.
public sealed class CommissioningRecoveryService(IPlcResetPort reset, MotionCoordinator motion,
    CommandRegistry commands, Station01Coordinator coordinator, ITraceWriter writer, ITraceQuery traces,
    Func<Guid, bool> executionExited, Func<bool> resourcesReleased, int resetBudgetMs, int saveBudgetMs)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    public static bool CanReset(RunState state) => state is RunState.Blocked or RunState.RecoveryRequired or
        RunState.Restricted or RunState.Cancelled;
    public static CommissioningRecoveryProjection? ReadProof(PersistedRun? run, IReadOnlyList<PersistedWrite> writes)
    {
        if (run?.Terminal != TerminalOutcome.Cancelled) return null;
        foreach (var write in writes.Where(w => w.State == CommitState.Committed && w.Kind == WriteKind.Cancel))
        {
            using var doc = JsonDocument.Parse(write.PayloadJson);
            var data = doc.RootElement;
            if (data.TryGetProperty("kind", out var kind) && kind.GetString() == "CommissioningRecoveryClosed" &&
                data.TryGetProperty("resetId", out var id) && id.TryGetGuid(out var resetId))
                return new(write.WriteId, resetId, "ClosedAfterVerifiedReset");
        }
        return null;
    }
    public async Task ValidateRestartAsync(string subject, CommissioningRestartFrom from, CancellationToken ct)
    {
        var old = await traces.GetRunAsync(from.RunId, ct);
        var proof = ReadProof(old, await traces.GetWritesAsync(from.RunId, ct));
        if (old?.SubjectId != subject || proof?.RecoveryWriteId != from.RecoveryWriteId)
            throw new InvalidOperationException("CommissioningRecoveryReferenceInvalid");
    }
    public async Task<CommissioningResetResult> ResetAsync(string actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(actor)) throw new ArgumentException("RecoveryActorRequired");
        if (!await gate.WaitAsync(0, ct)) throw new InvalidOperationException("RecoveryInProgress");
        var maintenance = false;
        Guid? owner = null;
        var resetId = Guid.NewGuid();
        try
        {
            owner = commands.BeginMaintenance(); maintenance = true;
            PersistedRun? old = null;
            CommissioningRecoveryProjection? priorProof = null;
            if (owner is { } runId)
            {
                var snapshot = coordinator.Query(runId) ?? throw new InvalidOperationException("RecoveryRunNotFound");
                if (!CanReset(snapshot.State) || !executionExited(runId))
                    throw new InvalidOperationException("RecoveryExecutionStillActive");
                old = await traces.GetRunAsync(runId, ct) ?? throw new InvalidOperationException("RecoveryRunNotPersisted");
                priorProof = ReadProof(old, await traces.GetWritesAsync(runId, ct));
                if (old.Terminal != TerminalOutcome.None && priorProof is null)
                    throw new InvalidOperationException("RecoveryTerminalProofUnavailable");
            }
            else if (coordinator.ActiveRunCount != 0) throw new InvalidOperationException("RecoveryExecutionStillActive");
            RequireReleased(owner);
            var failedEpoch = motion.Observe().ConnectionEpoch;
            if (old is { Terminal: TerminalOutcome.None })
            {
                await SaveAsync(old, WriteKind.Audit, new { kind = "CommissioningResetRequested", resetId,
                    actor, failedEpoch, oldRunId = old.RunId, automaticRetry = false }, ct);
                old = await traces.GetRunAsync(old.RunId, ct) ?? throw new InvalidOperationException("RecoveryRunNotPersisted");
            }
            RuntimeDiagnostics.Record("CommissioningRecovery", "ResetRequested", owner, new { resetId, actor, failedEpoch });
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(resetBudgetMs);
            await reset.ResetAsync(deadline.Token);
            InitialReadinessAssessment initial;
            while (true)
            {
                initial = await reset.ReadInitialStateAsync(deadline.Token);
                if (initial.Passed) break;
                if (initial.BlockedReasons.Any(r => r != "DeviceWorkNotReleased"))
                    throw new InvalidOperationException("RecoveryInitialStateIncomplete:" + string.Join(',', initial.BlockedReasons));
                // Scheduling only. Every pass requires a new device read, never a delay-based acknowledgement.
                await Task.Delay(50, deadline.Token);
            }
            if (initial.ResetGeneration is not { } resetEpoch || resetEpoch <= failedEpoch ||
                initial.Observation.ConnectionEpoch != resetEpoch)
                throw new InvalidOperationException("RecoveryResetEpochNotChanged");
            RequireReleased(owner);
            Guid? recoveryWriteId = priorProof?.RecoveryWriteId;
            if (old is { Terminal: TerminalOutcome.None })
            {
                var receipt = await SaveAsync(old, WriteKind.Cancel, new { kind = "CommissioningRecoveryClosed",
                    resetId, actor, failedEpoch, initial, oldRunId = old.RunId,
                    physicalBasis = "User:2026-10-08-system-reset-restores-workpiece-gripper-flip",
                    oldExecutionExited = true, softwareResourcesReleased = true,
                    noOldStepContinuation = true, inspectionCompleted = false, manualStartRequired = true }, ct);
                recoveryWriteId = receipt.WriteId;
            }
            if (owner is { } closedRun)
            {
                // Durable cancellation precedes releasing either software owner.
                motion.ReleaseAfterVerifiedSystemReset(closedRun, initial);
                await coordinator.SetAsync(closedRun, s => s.Next(RunState.Cancelled) with {
                    FinalOutcome = TerminalOutcome.Cancelled, WholeTaskState = "ClosedAfterVerifiedReset",
                    PersistedRevision = old!.Revision + (priorProof is null ? 1 : 0),
                    ErrorCode = null, StartupDiagnostic = null, AutomaticContinuationAllowed = false,
                    Events = [..s.Events, "CommissioningRecoveryClosed"] }, terminal: true);
                commands.ReleaseAfterCommissioningRecovery(closedRun);
            }
            RuntimeDiagnostics.Record("CommissioningRecovery", "ClosedAfterVerifiedReset", owner,
                new { resetId, recoveryWriteId, initial, manualStartRequired = true });
            return new(true, true, owner is not null, owner, recoveryWriteId);
        }
        catch (Exception error)
        {
            RuntimeDiagnostics.Record("CommissioningRecovery", "Blocked", owner,
                new { resetId, actor, automaticRetry = false }, error);
            throw;
        }
        finally { if (maintenance) commands.EndMaintenance(); gate.Release(); }
    }
    private void RequireReleased(Guid? owner)
    {
        if (owner is { } id && !executionExited(id) || !resourcesReleased())
            throw new InvalidOperationException("RecoverySoftwareResourcesNotReleased");
    }
    private async Task<CommitReceipt> SaveAsync(PersistedRun run, WriteKind kind, object payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var batch = new WriteBatch(Guid.NewGuid(), run.RunId, run.Revision, kind, json,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))),
            kind == WriteKind.Cancel ? RunState.Cancelled : RunState.RecoveryRequired,
            CandidateTerminal: kind == WriteKind.Cancel ? TerminalOutcome.Cancelled : TerminalOutcome.None);
        var queued = writer.SubmitCritical(batch, ct);
        var receipt = await queued.Completion.WaitAsync(TimeSpan.FromMilliseconds(saveBudgetMs), ct);
        if (receipt.State != CommitState.Committed || receipt.CommittedRevision is null)
            throw new InvalidOperationException("RecoveryCommitNotConfirmed:" + receipt.State);
        return receipt;
    }
}
