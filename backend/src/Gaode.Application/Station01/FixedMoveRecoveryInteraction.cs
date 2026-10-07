using System.Collections.Concurrent;
using Gaode.Application.Motion;
using Gaode.Application.Ports;
using Gaode.Application.Station01.Steps;
using Gaode.Domain.Station01;

namespace Gaode.Application.Station01;

public sealed class RecoverableMoveFailure(Guid operationId, Guid actionId, long epoch,
    string role, Exception error) : InvalidOperationException(error.Message, error)
{
    public Guid OperationId { get; } = operationId;
    public Guid ActionId { get; } = actionId;
    public long Epoch { get; } = epoch;
    public string Role { get; } = role;
}

public sealed record FailedMoveRecoveryPrompt(Guid RunId, Guid OperationId, Guid ActionId,
    long FailedEpoch, string Role, DateTimeOffset DeadlineUtc, long? ResetEpoch, Guid? CheckId);

/// <summary>USR-D: close the fault run, reset/check, explicitly start a complete new run.</summary>
public sealed class FixedMoveRecoveryInteraction(Station01Coordinator coordinator,
    MotionCoordinator motion, IPlcResetPort reset, CommandRegistry commands, ITraceQuery traces,
    int testRecoveryWaitMs = 120000, Func<bool>? softwareResourcesReleased = null)
{
    private sealed class Pending(RunExecution run, RecoverableMoveFailure failure)
    {
        public RunExecution Run { get; } = run;
        public RecoverableMoveFailure Failure { get; } = failure;
        public TaskCompletionSource<bool> Exited { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public SemaphoreSlim Gate { get; } = new(1);
        public Guid ResetId { get; set; }
        public long? ResetEpoch { get; set; }
        public Guid? CheckId { get; set; }
        public InitialReadinessAssessment? Initial { get; set; }
        public Guid? NewRunId { get; set; }
        public bool ResourcesReleased { get; set; }
        public IReadOnlyList<string> BlockedReasons { get; set; } = [];
    }
    private readonly ConcurrentDictionary<Guid, Pending> pending = new();
    public FailedMoveRecoveryPrompt? Query(Guid runId) => pending.TryGetValue(runId, out var p)
        ? new(runId, p.Failure.OperationId, p.Failure.ActionId, p.Failure.Epoch, p.Failure.Role,
            DateTimeOffset.MaxValue, p.ResetEpoch, p.CheckId) : null;
    public FaultRestartProjection? QueryRestart(Guid runId) => pending.TryGetValue(runId, out var p)
        ? new(runId, p.ResetId == Guid.Empty ? null : p.ResetId, p.CheckId,
            p.NewRunId is not null ? "NewRunLinked" : p.CheckId is not null ? "InitialReady" :
            p.ResetEpoch is not null ? "InitialBlocked" : "ResetRequested", p.NewRunId,
            p.BlockedReasons,
            p.Run.PersistedRevision) : null;
    public bool HasFaults => pending.Values.Any(p => p.NewRunId is null);
    public void ExecutionExited(Guid runId)
    {
        if (pending.TryGetValue(runId, out var p)) p.Exited.TrySetResult(true);
    }
    public async Task CloseWorkflowFaultAsync(RunExecution run, ControlLatch control, string role, string error, CancellationToken ct)
    {
        // The awaited downstream workflow owns its committed writes. Transfer
        // that real revision back once, solely for fault closure; never retry CAS
        // conflicts or reinterpret late records as action/recipe authorization.
        control.MarkSafetyFault();
        var persisted = await traces.GetRunAsync(run.RunId, ct)
            .WaitAsync(TimeSpan.FromMilliseconds(run.Config.Budget.BusinessMs.Query), run.Clock, ct)
            ?? throw new InvalidOperationException("FaultClosureRunNotFound");
        run.AdoptClosedWorkflowRevision(persisted);
        await WaitAsync(run, control, new RecoverableMoveFailure(Guid.NewGuid(), motion.CurrentAction ?? Guid.Empty,
            motion.Observe().ConnectionEpoch, role, new InvalidOperationException(error)), ct);
    }
    public async Task WaitAsync(RunExecution run, ControlLatch control, RecoverableMoveFailure failure, CancellationToken ct)
    {
        control.MarkSafetyFault();
        await run.ReportAsync(action: ActionState.Unknown);
        await run.SaveAsync(WriteKind.ActionFact, new { kind = "FailedMoveRecoveryRequired",
            failure.OperationId, failure.ActionId, failure.Epoch, failure.Role, error = failure.Message,
            actual = motion.Observe(), run.Config.SnapshotId, noAutomaticRetry = true },
            RunState.RecoveryRequired, cancellationToken: ct);
        await run.SaveAsync(WriteKind.Audit, new { kind = "RecoveryOldExecutionClosed",
            run.RunId, failure.OperationId, failure.ActionId, dispatchClosed = true,
            noOldStepContinuation = true, evidenceRetained = true }, RunState.RecoveryRequired, cancellationToken: ct);
        if (!pending.TryAdd(run.RunId, new(run, failure))) throw new InvalidOperationException("RecoveryAlreadyWaiting");
        await coordinator.SetAsync(run.RunId, s => s.Next(RunState.RecoveryRequired) with {
            ErrorCode = failure.Message, Events = [..s.Events, "FaultRequiresNewRun", "RecoveryOldExecutionClosed"] });
        throw new FaultRunClosedException();
    }
    public async Task<FailedMoveRecoveryPrompt> ResetAsync(Guid runId, string subject, string requestId, string reason, CancellationToken ct)
    {
        var p = Get(runId);
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(requestId) || string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("RecoveryActorReasonAndRequestRequired");
        await p.Gate.WaitAsync(ct);
        try
        {
            await p.Exited.Task.WaitAsync(TimeSpan.FromMilliseconds(testRecoveryWaitMs), ct);
            if (p.NewRunId is not null) throw new InvalidOperationException("RecoveryCheckConsumed");
            await p.Run.SaveAsync(WriteKind.Audit, new { kind = "RecoveryResetRequested", subject, requestId, reason,
                resetId = p.ResetId = Guid.NewGuid(), faultRunId = runId }, cancellationToken: ct);
            p.CheckId = null; p.Initial = null;
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(p.Run.Config.Budget.BusinessMs.XyCompletion);
            await reset.ResetAsync(deadline.Token);
            var initial = await reset.ReadInitialStateAsync(deadline.Token);
            if (initial.Observation.ConnectionEpoch <= p.Failure.Epoch) throw new InvalidOperationException("RecoveryResetEpochNotChanged");
            p.Initial = initial;
            p.BlockedReasons = initial.BlockedReasons.ToArray();
            p.ResetEpoch = initial.Observation.ConnectionEpoch;
            await p.Run.SaveAsync(WriteKind.Audit, new { kind = "RecoveryResetObserved", subject, requestId,
                p.ResetId, initial, oldExecutionExited = p.Exited.Task.IsCompletedSuccessfully },
                RunState.RecoveryRequired, cancellationToken: ct);
            return Query(runId)!;
        }
        finally { p.Gate.Release(); }
    }
    private static bool InRange(RunExecution run, DeviceObservation actual)
    {
        var limits = run.Config.Public.Motion.Limits;
        var position = actual.Position;
        return actual.HasReliableObservation && position is { IsReliable: true, ActualX: { } x, ActualY: { } y, ActualZ: { } z } &&
            x >= limits.XMin && x <= limits.XMax && y >= limits.YMin && y <= limits.YMax && z >= limits.ZMin && z <= limits.ZMax;
    }
    public async Task CheckAsync(Guid runId, Guid checkId, string subject, string requestId,
        bool sameTray, bool loadingUnchanged, bool snapshotApplicable, string? reason,
        IReadOnlyList<string>? evidenceRefs, CancellationToken ct)
    {
        var p = Get(runId);
        await p.Gate.WaitAsync(ct);
        try
        {
            var initial = await reset.ReadInitialStateAsync(ct);
            var passed = p.ResetEpoch is not null && initial.Observation.ConnectionEpoch == p.ResetEpoch &&
                initial.Passed && InRange(p.Run, initial.Observation) && p.Exited.Task.IsCompletedSuccessfully &&
                softwareResourcesReleased?.Invoke() == true &&
                sameTray && loadingUnchanged && snapshotApplicable && !string.IsNullOrWhiteSpace(reason) &&
                evidenceRefs is { Count: > 0 } && evidenceRefs.All(x => !string.IsNullOrWhiteSpace(x));
            p.Initial = initial;
            await p.Run.SaveAsync(WriteKind.Audit, new { kind = passed ? "RecoveryInitialCheckAccepted" : "RecoveryInitialCheckBlocked",
                checkId, subject, requestId, p.ResetId, initial, sameTray, loadingUnchanged, snapshotApplicable,
                reason, evidenceRefs, initialRange = p.Run.Config.Public.Motion.Limits,
                oldExecutionExited = p.Exited.Task.IsCompletedSuccessfully, softwareResourcesReleased = softwareResourcesReleased?.Invoke() == true }, cancellationToken: ct);
            p.BlockedReasons = initial.BlockedReasons
                .Concat(!InRange(p.Run, initial.Observation) ? ["PositionOutsideFrozenRange"] : Array.Empty<string>())
                .Concat(!sameTray || !loadingUnchanged || !snapshotApplicable ? ["TrayOrConfigurationNotVerified"] : Array.Empty<string>())
                .Concat(softwareResourcesReleased?.Invoke() != true ? ["SoftwareResourcesNotReleased"] : Array.Empty<string>())
                .Concat(p.ResetEpoch is null ? ["ResetNotObserved"] : Array.Empty<string>()).ToArray();
            if (!passed) { p.CheckId = null; throw new InvalidOperationException("RecoveryInitialStateIncomplete"); }
            if (!p.ResourcesReleased)
            {
                motion.ReconcileVerifiedReset(runId, p.Failure.ActionId, p.ResetEpoch!.Value, p.Failure.Epoch);
                motion.ReleaseAfterObservedUnlock(runId);
                commands.ReleaseForFaultRestart(runId);
                p.ResourcesReleased = true;
            }
            p.CheckId = checkId;
        }
        finally { p.Gate.Release(); }
    }
    public Task ContinueAsync(Guid runId, Guid checkId, string subject, string requestId, CancellationToken ct) =>
        Task.FromException(new InvalidOperationException("FaultRequiresNewRun"));
    public void ValidateRestart(FaultRestartFrom request)
    {
        var p = Get(request.FaultRunId);
        if (p.ResetId != request.ResetId || p.CheckId != request.InitialCheckId || p.NewRunId is not null ||
            p.Initial is not { Passed: true } || !p.Exited.Task.IsCompletedSuccessfully ||
            coordinator.Query(request.FaultRunId)?.ObservedRevision != request.ExpectedFaultRevision)
            throw new InvalidOperationException("RecoveryInitialCheckNotApplicable");
    }
    public async Task LinkNewRunAsync(FaultRestartFrom request, Guid newRunId, Guid commandId, string requestId, RunExecution newRun, CancellationToken ct)
    {
        var p = Get(request.FaultRunId);
        await p.Gate.WaitAsync(ct);
        try
        {
            if (p.NewRunId == newRunId) return;
            if (p.NewRunId is not null || p.ResetId != request.ResetId || p.CheckId != request.InitialCheckId)
                throw new InvalidOperationException("RecoveryCheckConsumed");
            var initial = await reset.ReadInitialStateAsync(ct);
            if (!initial.Passed || !InRange(p.Run, initial.Observation) || initial.Observation.ConnectionEpoch != p.ResetEpoch ||
                initial.ResetGeneration != p.Initial?.ResetGeneration || motion.IsHeld(request.FaultRunId) || motion.Unknown || softwareResourcesReleased?.Invoke() != true)
                throw new InvalidOperationException("RecoveryInitialStateChanged");
            // Existing conditional Writer transaction: failure/unknown prevents returning to StartClamp.
            try
            {
                await p.Run.SaveAsync(WriteKind.Audit, new { kind = "RecoveryNewRunLinked", faultRunId = request.FaultRunId,
                    newRunId, commandId, requestId, p.ResetId, initialCheckId = p.CheckId,
                    checkConsumed = true, initial, oldEvidenceRetained = true,
                    newRunExpectedRevision = newRun.PersistedRevision, newRunLinkWriteId = Guid.NewGuid() }, cancellationToken: ct);
            }
            catch
            {
                p.CheckId = null; p.Initial = null;
                p.BlockedReasons = ["RecoveryLinkCommitUnconfirmed"];
                throw; // no physical start or reusable authorization after failed/unknown commit
            }
            await newRun.ApplyCompanionCommitAsync(newRun.PersistedRevision, newRun.PersistedRevision + 1);
            p.NewRunId = newRunId;
            await coordinator.SetAsync(request.FaultRunId, s => s with { FaultRestart = QueryRestart(request.FaultRunId),
                Events = [..s.Events, "RecoveryNewRunLinked"], ObservedRevision = s.ObservedRevision + 1 });
        }
        finally { p.Gate.Release(); }
    }
    private Pending Get(Guid id) => pending.TryGetValue(id, out var p) ? p : throw new InvalidOperationException("NoLiveFaultRestart");
}
public sealed class FaultRunClosedException : InvalidOperationException
{
    public FaultRunClosedException() : base("FaultRequiresNewRun") { }
}
public sealed record FaultRestartFrom(Guid FaultRunId, Guid ResetId, Guid InitialCheckId, long ExpectedFaultRevision);
