using Gaode.Application.Motion;
using Gaode.Application.Ports;
using Gaode.Application.Timing;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Diagnostics;

namespace Gaode.Application.Station01.Steps;

public sealed record MoveEvidence(Guid OperationId, Guid ActionId, Guid IntentWriteId,
    string PointId, string PointVersion, long DeviceEpoch, DeviceActionEvidence Evidence, ActionWindow Window);

public sealed class FixedMoveStep(MotionCoordinator motion, OperationIngress ingress,
    FixedMoveRecoveryInteraction? recovery = null)
{
    public Task<MoveEvidence> ExecuteAsync(RunExecution run, ControlLatch control,
        FixedPoint point, string role, CancellationToken cancellationToken) => RuntimeDiagnostics.ObserveAsync(
            "FixedMove", run.RunId, new { run.RequestId, point, role,
                acceptanceMs = run.Config.Budget.BusinessMs.PlcAcceptance,
                completionMs = run.Config.Budget.BusinessMs.XyCompletion },
            () => ExecuteWithRecoveryAsync(run, control, point, role, cancellationToken), r => r);

    private async Task<MoveEvidence> ExecuteWithRecoveryAsync(RunExecution run, ControlLatch control,
        FixedPoint point, string role, CancellationToken ct)
    {
        var operationId = Guid.NewGuid();
        try { return await ExecuteCoreAsync(run, control, point, role, operationId, 1, ct); }
        catch (RecoverableMoveFailure failure) when (recovery is not null &&
            run.Config.Public.Purpose == "Test" && run.RecipePlan is null &&
            motion.CanRecoverFailedAction(run.RunId, failure.ActionId) && !control.CancelRequested && !ct.IsCancellationRequested)
        {
            await recovery.WaitAsync(run, control, failure, ct);
            throw new FaultRunClosedException();
        }
    }

    private async Task<MoveEvidence> ExecuteCoreAsync(RunExecution run, ControlLatch control,
        FixedPoint point, string role, Guid operationId, int attempt, CancellationToken cancellationToken)
    {
        var actionId = Guid.NewGuid();
        var intentReceipt = await run.SaveAsync(WriteKind.ActionIntent,
            new OperationIntentPayload(operationId, "FixedXYZ" + role, attempt, actionId, null,
                $"{point.Id}@{point.Version}:X={point.X};Y={point.Y};Z={point.Z};{point.Unit}/{point.Frame}",
                run.Config.SnapshotId, Guid.Empty), cancellationToken: cancellationToken);
        await run.ReportAsync(action: ActionState.IntentCommitted);
        var acceptKey = new OperationKey(run.RunId, operationId, attempt, OperationPhase.Acceptance);
        var completeKey = new OperationKey(run.RunId, operationId, attempt, OperationPhase.Completion);
        var accepted = ingress.Register(acceptKey, run.Config.Budget.BusinessMs.PlcAcceptance);
        var completed = ingress.Register(completeKey, run.Config.Budget.BusinessMs.XyCompletion);
        var envelope = new PortEnvelope(run.RunId, operationId, attempt, run.SessionId,
            run.Config.SnapshotId, run.Config.Public.Version, run.Config.Public.Purpose,
            completed.StartTick, completed.DueTick, run.ClockId);
        var epoch = motion.Observe().ConnectionEpoch;
        try
        {
        var feedbackLogs = 0;
        RuntimeDiagnostics.Record("FixedMove", "Requesting", run.RunId,
            new { operationId, actionId, attempt, epoch, point, role, intentReceipt.WriteId,
                completed.StartTick, completed.DueTick });
        DeviceEvent? acceptEvent = null, completeEvent = null;
        void OnEvent(DeviceEvent e)
        {
            var matched = DeviceContract.Matches(e, envelope, epoch) && e.ActionId == actionId;
            if (Interlocked.Increment(ref feedbackLogs) <= 16)
                RuntimeDiagnostics.Record("MotionFeedback", matched ? "Received" : "IgnoredMismatch", run.RunId,
                    new { operationId, actionId, expectedEpoch = epoch, kind = e.Kind.ToString(),
                        actualActionId = e.ActionId }, warning: !matched || e.Kind == DeviceEventKind.Failed);
            if (!matched) return;
            if (e.Kind is DeviceEventKind.Accepted or DeviceEventKind.Failed)
            {
                acceptEvent = e;
                if (e.Kind == DeviceEventKind.Accepted) _ = run.ReportAsync(action: ActionState.Accepted);
                ingress.Receive(acceptKey, e.Kind.ToString());
            }
            if (e.Kind == DeviceEventKind.Executing) _ = run.ReportAsync(action: ActionState.Executing);
            if (e.Kind is DeviceEventKind.Completed or DeviceEventKind.Failed)
            {
                completeEvent = e;
                if (e.Kind == DeviceEventKind.Completed) _ = run.ReportAsync(action: ActionState.Completed);
                ingress.Receive(completeKey, e.Kind.ToString());
            }
        }
        var binding = run.Config.Public.Bindings.Single(x => x.Role == "PLC").Id;
        var request = new MoveRequest(envelope, actionId, point, intentReceipt.WriteId, binding, role);
        await run.ReportAsync(action: ActionState.Dispatched);
        await motion.RequestMoveAsync(run.Config.Public, request, control.AdmissionClosed, OnEvent, cancellationToken);
        var acceptDecision = await accepted.Completion;
        RuntimeDiagnostics.Record("FixedMove", "AcceptanceDecision", run.RunId,
            new { operationId, actionId, outcome = acceptDecision.Outcome.ToString(),
                acceptDecision.Reason, acceptDecision.ReceivedTick, response = acceptEvent?.Kind.ToString() },
            warning: !acceptDecision.IsSuccessful || acceptEvent?.Kind != DeviceEventKind.Accepted);
        if (!acceptDecision.IsSuccessful || acceptEvent?.Kind != DeviceEventKind.Accepted)
        {
            motion.MarkUnknown(actionId);
            throw new InvalidOperationException("XY命令受理未知或超时");
        }
        var completeDecision = await completed.Completion;
        RuntimeDiagnostics.Record("FixedMove", "CompletionDecision", run.RunId,
            new { operationId, actionId, outcome = completeDecision.Outcome.ToString(),
                completeDecision.Reason, completeDecision.ReceivedTick, control.AdmissionClosed,
                response = completeEvent?.Kind.ToString(), observation = motion.Observe(),
                point, tolerance = run.Config.Public.Motion.PositionTolerance },
            warning: !completeDecision.IsSuccessful || completeEvent?.Kind != DeviceEventKind.Completed || control.CancelRequested || control.SafetyFault);
        if (!completeDecision.IsSuccessful || completeEvent?.Kind != DeviceEventKind.Completed ||
            (control.CancelRequested || control.SafetyFault))
        {
            motion.MarkUnknown(actionId);
            throw new InvalidOperationException("XY动作完成未知或超时");
        }
        var observed = motion.Observe();
        var tolerance = run.Config.Public.Motion.PositionTolerance;
        var evidence = completeEvent.Evidence;
        var reached = evidence?.Positions.SingleOrDefault();
        if (!observed.HasReliableObservation || observed.SafetyAssessment != SafetyAssessment.Clear || observed.ConnectionEpoch != epoch ||
            evidence is not { IsCorrelated: true, Meaning: DeviceCompletionMeaning.PositionReached } ||
            evidence.Correlation.ActionId != actionId || evidence.Correlation.OperationId != operationId ||
            reached is not { Matched: true } || reached.Target != point)
        {
            motion.MarkUnknown(actionId);
            throw new InvalidOperationException("XY到位证据与当前固定点动作不匹配");
        }
        await run.SaveAsync(WriteKind.ActionFact, new { schemaVersion = "device-semantics/1", kind = "FixedMoveCompleted", operationId, actionId, attempt,
            pointId = point.Id, pointVersion = point.Version, point.X, point.Y, point.Z,
            accepted = true, completed = true, deviceEpoch = epoch, role,
            target = new { point.X, point.Y, point.Z }, actual = reached.Actual, evidence,
            tolerance, matched = true, observedAtUtc = reached.Actual.Identity.SampleEndedUtc },
            cancellationToken: cancellationToken);
        motion.ConfirmCompleted(actionId);
        return new(operationId, actionId, intentReceipt.WriteId, point.Id, point.Version, epoch, evidence,
            ActionWindows.From(run.Clock, completed.StartTick, completed.DueTick, run.ClockId));
        }
        catch (InvalidOperationException error)
        {
            motion.MarkUnknown(actionId);
            throw new RecoverableMoveFailure(operationId, actionId, epoch, role, error);
        }
    }
}
