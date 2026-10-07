using Gaode.Application.Motion;
using Gaode.Application.Ports;
using Gaode.Application.Timing;
using Gaode.Domain.Station01;
using Gaode.Diagnostics;

namespace Gaode.Application.Station01.Steps;

public sealed record StartPreparationEvidence(Guid OperationId, Guid ActionId, Guid IntentWriteId,
    bool Accepted, bool DeviceReady, long DeviceEpoch, DeviceObservation Observation);

public static class StartupObservationPolicy
{
    public static bool IsCurrentReady(DeviceObservation observation, long expectedEpoch, bool controlClosed) =>
        !controlClosed && observation.HasReliableObservation && observation.ConnectionEpoch == expectedEpoch &&
        observation.OperatingMode == OperatingMode.Automatic && observation.Readiness == DeviceReadiness.Ready &&
        observation.SafetyAssessment == SafetyAssessment.Clear;
}

public sealed class StartPreparationStep(MotionCoordinator motion, OperationIngress ingress)
{
    public Task<StartPreparationEvidence> ExecuteAsync(RunExecution run, ControlLatch control,
        Func<RunState, Task> stage, CancellationToken cancellationToken) => RuntimeDiagnostics.ObserveAsync(
            "StartPreparation", run.RunId, new { run.RequestId, run.CommandId,
                acceptanceMs = run.Config.Budget.BusinessMs.PlcAcceptance },
            () => ExecuteCoreAsync(run, control, stage, cancellationToken), r => r);

    private async Task<StartPreparationEvidence> ExecuteCoreAsync(RunExecution run, ControlLatch control,
        Func<RunState, Task> stage, CancellationToken cancellationToken)
    {
        var operationId = Guid.NewGuid();
        var actionId = Guid.NewGuid();
        var intent = await run.SaveAsync(WriteKind.StartIntent,
            new OperationIntentPayload(operationId, "StartPreparationObservation", 1, actionId,
                null, "StartupReadiness", run.Config.SnapshotId, Guid.Empty),
            RunState.WaitingStartAcceptance, cancellationToken: cancellationToken);
        await run.ReportAsync(action: ActionState.IntentCommitted);
        var key = new OperationKey(run.RunId, operationId, 1, OperationPhase.Acceptance);
        var acceptance = ingress.Register(key, run.Config.Budget.BusinessMs.PlcAcceptance);
        var envelope = new PortEnvelope(run.RunId, operationId, 1, run.SessionId,
            run.Config.SnapshotId, run.Config.Public.Version, run.Config.Public.Purpose,
            acceptance.StartTick, acceptance.DueTick, run.ClockId);
        var epoch = motion.Observe().ConnectionEpoch;
        RuntimeDiagnostics.Record("StartPreparation", "Requesting", run.RunId,
            new { operationId, actionId, epoch, intent.WriteId, acceptance.StartTick, acceptance.DueTick });
        DeviceEvent? response = null;
        void OnEvent(DeviceEvent e)
        {
            if (!DeviceContract.Matches(e, envelope, epoch) || e.ActionId != actionId ||
                e.Kind is not (DeviceEventKind.Accepted or DeviceEventKind.Failed)) return;
            response = e;
            ingress.Receive(key, e.Kind.ToString());
        }
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            await stage(RunState.WaitingStartAcceptance);
            await run.ReportAsync(action: ActionState.Dispatched);
            await motion.RequestStartAsync(run.Config.Public, envelope, actionId, intent.WriteId,
                control.AdmissionClosed, OnEvent, operation.Token);
            var accepted = await acceptance.Completion.WaitAsync(cancellationToken);
            RuntimeDiagnostics.Record("StartPreparation", "AcceptanceDecision", run.RunId,
                new { operationId, actionId, outcome = accepted.Outcome.ToString(), accepted.Reason,
                    responseKind = response?.Kind.ToString(), accepted.ReceivedTick },
                warning: !accepted.IsSuccessful || response?.Kind != DeviceEventKind.Accepted);
            if (!accepted.IsSuccessful || response?.Kind != DeviceEventKind.Accepted)
                throw new InvalidOperationException("StartAcceptanceUnknownHeld");
            // The adapter reports readiness, not an invented clamp/physical-button completion.
            var observed = motion.Observe();
            if (!StartupObservationPolicy.IsCurrentReady(observed, epoch,
                control.CancelRequested || control.SafetyFault))
                throw new InvalidOperationException("StartupReadinessUnconfirmed");
            await run.ReportAsync(action: ActionState.Accepted);
            await run.SaveAsync(WriteKind.ActionFact, new { operationId, actionId,
                eventKind = "StartReadyObserved", observed, observed.Identity, observed.ExecutionOrigin,
                observed.DiagnosticEvidenceReference, deviceEpoch = epoch }, RunState.Running3D,
                cancellationToken: cancellationToken);
            motion.ConfirmCompleted(actionId);
            await run.ReportAsync(action: ActionState.Completed);
            await stage(RunState.Running3D);
            return new(operationId, actionId, intent.WriteId, true, true, epoch, observed);
        }
        catch (Exception error)
        {
            RuntimeDiagnostics.Record("StartPreparation", "FailedBeforeCleanup", run.RunId,
                new { operationId, actionId, epoch, observation = motion.Observe(),
                    disposition = "WillRequestStop_PhysicalStopNotConfirmed" }, error);
            operation.Cancel();
            control.MarkSafetyFault();
            motion.MarkUnknown(actionId);
            await motion.RequestStopAsync(envelope, _ => { }, CancellationToken.None);
            await run.ReportAsync(action: ActionState.Unknown);
            await run.SaveAsync(WriteKind.ActionFact, new { operationId, actionId,
                outcome = "UnknownHeld", error = error.Message, observation = motion.Observe() },
                RunState.Blocked, cancellationToken: CancellationToken.None);
            throw;
        }
    }
}
