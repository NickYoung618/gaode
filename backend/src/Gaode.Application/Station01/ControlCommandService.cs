using System.Collections.Concurrent;
using Gaode.Domain.Station01;

namespace Gaode.Application.Station01;

public sealed record ControlReceipt(Guid CommandId, Guid RunId, string Operation,
    string RequestId, bool RequestAccepted, bool AdmissionClosed, string StopState,
    string TerminalDecision, bool? Applied, long Revision, string? CheckId = null);

public sealed record RecoveryCheckReceipt(Guid CheckId, Guid RunId, string RequestId,
    long Revision, bool SameTray, bool LoadingUnchanged, bool SnapshotStillApplicable,
    string Result, IReadOnlyList<string> ReusableSteps);

public sealed class Station01ControlCommandService(Station01Coordinator coordinator,
    FixedMoveRecoveryInteraction? recovery = null)
{
    private readonly ConcurrentDictionary<(string Subject, string RequestId, string Operation), ControlReceipt> _commands = new();
    private readonly ConcurrentDictionary<Guid, RecoveryCheckReceipt> _checks = new();

    public async Task<ControlReceipt> RequestAsync(string subject, Guid runId, string requestId,
        long expectedRevision, string operation, string? reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(requestId))
            throw new ArgumentException("主体和requestId必需");
        if (operation is not ("Pause" or "Cancel")) throw new ArgumentException("控制操作无效");
        if (_commands.TryGetValue((subject, requestId, operation), out var existing)) return existing;
        var snapshot = coordinator.Query(runId) ?? throw new InvalidOperationException("RunNotFound");
        if (RunStateRules.IsTerminal(snapshot.State)) throw new InvalidOperationException("RunAlreadyTerminal");
        if (snapshot.ObservedRevision != expectedRevision)
            throw new InvalidOperationException("RevisionConflict");
        var latch = coordinator.Control(runId) ?? throw new InvalidOperationException("RunNotFound");
        if (operation == "Cancel") latch.RequestCancel(); else latch.RequestStop();
        var next = operation == "Cancel" ? RunState.CancelRequested : RunState.PauseRequested;
        var updated = await coordinator.SetAsync(runId, value => value.Next(next) with
        {
            CancelRequested = operation == "Cancel" || value.CancelRequested,
            ErrorCode = reason,
            Events = [..value.Events, operation + "Requested"]
        }, control: true).WaitAsync(cancellationToken);
        var receipt = new ControlReceipt(Guid.NewGuid(), runId, operation, requestId,
            true, true, operation == "Cancel" ? "StopPending" : "Requested",
            "Pending", null, updated.ObservedRevision);
        return _commands.GetOrAdd((subject, requestId, operation), receipt);
    }

    public async Task<RecoveryCheckReceipt> CheckAsync(string subject, Guid runId, string requestId,
        long expectedRevision, bool sameTray, bool loadingUnchanged, bool snapshotStillApplicable,
        CancellationToken cancellationToken, string? reason = null, IReadOnlyList<string>? evidenceRefs = null)
    {
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(requestId))
            throw new ArgumentException("主体和requestId必需");
        var snapshot = coordinator.Query(runId) ?? throw new InvalidOperationException("RunNotFound");
        if (snapshot.ObservedRevision != expectedRevision)
            throw new InvalidOperationException("RevisionConflict");
        var existing = _checks.Values.FirstOrDefault(x => x.RunId == runId && x.RequestId == requestId);
        if (existing is not null) return existing;
        var faultRestart = recovery?.Query(runId) is not null;
        var reusable = !faultRestart && sameTray && loadingUnchanged && snapshotStillApplicable
            ? new[] { "CompletedAndPersistedStepsOnly" } : Array.Empty<string>();
        var receipt = new RecoveryCheckReceipt(Guid.NewGuid(), runId, requestId,
            snapshot.ObservedRevision, sameTray, loadingUnchanged, snapshotStillApplicable,
            faultRestart || reusable.Length != 0 ? "Accepted" : "Rejected", reusable);
        if (recovery?.Query(runId) is not null)
            await recovery.CheckAsync(runId, receipt.CheckId, subject, requestId, sameTray,
                loadingUnchanged, snapshotStillApplicable, reason, evidenceRefs, cancellationToken);
        _checks[receipt.CheckId] = receipt;
        await Task.CompletedTask;
        return receipt;
    }

    public async Task<ControlReceipt> ContinueAsync(string subject, Guid runId, string requestId,
        long expectedRevision, Guid checkId, CancellationToken cancellationToken)
    {
        var snapshot = coordinator.Query(runId) ?? throw new InvalidOperationException("RunNotFound");
        if (snapshot.ObservedRevision != expectedRevision)
            throw new InvalidOperationException("RevisionConflict");
        if (!_checks.TryGetValue(checkId, out var check) || check.RunId != runId || check.Result != "Accepted")
            throw new InvalidOperationException("RecoveryCheckNotAccepted");
        if (snapshot.CancelRequested || snapshot.State is RunState.Cancelled or RunState.Completed or RunState.CompletedWithExceptions)
            throw new InvalidOperationException("ContinueNotAllowed");
        if (recovery?.Query(runId) is not null)
        {
            await recovery.ContinueAsync(runId, checkId, subject, requestId, cancellationToken);
            throw new InvalidOperationException("FaultRequiresNewRun");
        }
        if (snapshot.State != RunState.Paused)
            throw new InvalidOperationException("NoLiveFailedCommandRecovery");
        var updated = await coordinator.SetAsync(runId, value => value.Next(RunState.WaitingPhysicalStart) with
        {
            ErrorCode = null,
            Events = [..value.Events, "ContinueAccepted"]
        }, control: true).WaitAsync(cancellationToken);
        coordinator.Control(runId)?.Resume();
        var receipt = new ControlReceipt(Guid.NewGuid(), runId, "Continue", requestId,
            true, false, "NotRequested", "Pending", null, updated.ObservedRevision, checkId.ToString("D"));
        return _commands.GetOrAdd((subject, requestId, "Continue"), receipt);
    }

    public RecoveryCheckReceipt? GetCheck(Guid runId, Guid checkId) =>
        _checks.TryGetValue(checkId, out var value) && value.RunId == runId ? value : null;
}
