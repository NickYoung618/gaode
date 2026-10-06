using Gaode.Domain.Station01;
using Gaode.Application.Ports;
using Gaode.Domain.Configuration;
using Gaode.Diagnostics;

namespace Gaode.Application.Motion;

public sealed class MotionCoordinator(IPlcStatePort state, IPlcActionPort action,
    IMotionPort motion, IAcquisitionCyclePort acquisition, ResourceLease lease)
{
    public DeviceObservation Observe() => state.Observe();
    public bool IsHeld(Guid runId) => lease.Owner == runId;
    public bool Unknown => lease.Unknown;
    public Guid? CurrentAction => lease.CurrentAction;
    public bool CanRecoverFailedAction(Guid runId, Guid actionId) =>
        lease.Owner == runId && lease.Unknown && lease.CurrentAction == actionId;

    public async ValueTask RequestStartAsync(PublicConfiguration config, PortEnvelope envelope,
        Guid actionId, Guid committedIntentId, bool controlClosed,
        Action<DeviceEvent> onEvent, CancellationToken cancellationToken)
    {
        var error = MotionAdmission.CheckStart(config, state.Observe(), controlClosed, committedIntentId);
        if (error is not null || !lease.TryHold(envelope.RunId) || !lease.TryBeginAction(envelope.RunId, actionId))
            throw new InvalidOperationException(error ?? "MotionResourceHeld");
        try { await action.RequestStartAsync(envelope, actionId, committedIntentId, onEvent, cancellationToken); }
        catch { lease.MarkUnknown(actionId); throw; }
    }

    public async ValueTask RequestMoveAsync(PublicConfiguration config, MoveRequest request,
        bool controlClosed, Action<DeviceEvent> onEvent, CancellationToken cancellationToken)
    {
        var error = MotionAdmission.CheckMove(config, state.Observe(), request.Target, controlClosed, request.IntentWriteId);
        if (error is not null || !lease.TryBeginAction(request.Envelope.RunId, request.ActionId))
            throw new InvalidOperationException(error ?? "MotionResourceHeld");
        try { await motion.RequestMoveAsync(request, onEvent, cancellationToken); }
        catch { lease.MarkUnknown(request.ActionId); throw; }
    }

    public void ConfirmCompleted(Guid actionId) => lease.Complete(actionId);
    public void ReconcileVerifiedReset(Guid runId, Guid failedActionId, long resetEpoch, long failedEpoch)
    {
        var actual = state.Observe();
        if (resetEpoch <= failedEpoch || actual.ConnectionEpoch != resetEpoch || !actual.HasReliableObservation ||
            actual.Readiness != DeviceReadiness.Ready || actual.OperatingMode != OperatingMode.Automatic || actual.SafetyAssessment != SafetyAssessment.Clear)
            throw new InvalidOperationException("RecoveryResetNotObserved");
        lease.ReconcileVerifiedReset(runId, failedActionId);
    }
    public void BeginSpecialAction(Guid runId, Guid actionId)
    {
        if (!lease.TryBeginAction(runId, actionId)) throw new InvalidOperationException("MotionResourceHeld");
    }
    public void ReleaseAfterObservedUnlock(Guid runId) => lease.ReleaseOnlyAfterVerifiedPhysicalClear(runId);
    public void MarkUnknown(Guid actionId) => lease.MarkUnknown(actionId);
    public ValueTask RequestStopAsync(PortEnvelope envelope, Action<DeviceEvent> onEvent,
        CancellationToken cancellationToken) => action.RequestStopAsync(envelope, onEvent, cancellationToken);

    public async ValueTask<AcquisitionSession> OpenCaptureWindowAsync(CaptureWindowRequest request,
        bool controlClosed, CancellationToken cancellationToken)
    {
        if (controlClosed) throw new InvalidOperationException("ControlClosed");
        var actual = state.Observe();
        if (!actual.HasReliableObservation || actual.OperatingMode != OperatingMode.Automatic ||
            actual.SafetyAssessment != SafetyAssessment.Clear || actual.Readiness != DeviceReadiness.Ready ||
            actual.AcquisitionReadiness != AcquisitionReadiness.Available ||
            request.Correlation.ConnectionEpoch != actual.ConnectionEpoch || !lease.IsOwner(request.Correlation.RunId))
            throw new InvalidOperationException("CaptureAdmissionRejected");
        RuntimeDiagnostics.Record("CaptureWindow", "Requested", request.Correlation.RunId, request);
        var session = await acquisition.OpenCaptureWindowAsync(request, cancellationToken);
        if (session.State != AcquisitionState.CaptureAllowed || !session.Evidence.IsCorrelated)
            throw new InvalidOperationException("CaptureNotAuthorized");
        return session;
    }
    public async Task FinishCaptureWindowAsync(AcquisitionSession session, CaptureWorkCommit work,
        bool controlClosed, ActionWindow releaseWindow, CancellationToken cancellationToken)
    {
        if (controlClosed) throw new InvalidOperationException("ControlClosed");
        var result = await acquisition.FinishCaptureWindowAsync(session, work, releaseWindow, cancellationToken);
        RuntimeDiagnostics.Record("CaptureWindow", result.State.ToString(), session.Request.Correlation.RunId, result);
        if (result.State != AcquisitionState.Released || result.Evidence is not { IsCorrelated: true })
            throw new InvalidOperationException(result.FailureReason ?? "CaptureReleaseUnconfirmed");
    }
    public async Task CloseFailedCaptureWindowAsync(AcquisitionSession session, string failureReason,
        ActionWindow cleanupWindow, CancellationToken cancellationToken)
    {
        var result = await acquisition.CloseFailedCaptureWindowAsync(session, failureReason, cleanupWindow, cancellationToken);
        RuntimeDiagnostics.Record("CaptureWindow", "FailureCleanupObserved", session.Request.Correlation.RunId, result,
            warning: result.State == AcquisitionState.HeldUnknown);
        if (result.State == AcquisitionState.HeldUnknown) throw new InvalidOperationException(result.FailureReason ?? "CaptureCleanupUnconfirmed");
    }
}
