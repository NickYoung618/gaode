using System.Diagnostics;
using Gaode.Application.Ports;
using Gaode.Domain.Station01;

namespace Gaode.Infrastructure.Devices.Plc;

public sealed partial class LatestProtocolPlcDevice
{
    public async ValueTask<AcquisitionSession> OpenCaptureWindowAsync(CaptureWindowRequest request, CancellationToken token)
    {
        RequireKnownPosition(request.PositionEvidence);
        if (request.Correlation != request.PositionEvidence.Correlation || request.Target != request.PositionEvidence.Target ||
            !request.Window.IsValid || !request.Window.Contains(Stopwatch.GetTimestamp()))
            throw new InvalidOperationException("CaptureRequestIdentityInvalid");
        using var deadline = LimitTo(request.Window, token);
        var context = new CommunicationCaptureContext(request.Correlation.RunId, request.Correlation.OperationId,
            request.Correlation.ConnectionEpoch, request.Target) { Role = request.Role == CaptureRole.ThreeD ? "3D" : request.Role.ToString() };
        await BeginInspectionAsync(context, deadline.Token);
        var evidence = await CompleteEvidenceAsync(request.Correlation, DeviceCompletionMeaning.CaptureAllowed,
            request.Window, deadline.Token, [request.PositionEvidence], sampledObservation: Observe().Position?.Identity);
        var session = new AcquisitionSession(Guid.NewGuid(), request, AcquisitionState.CaptureAllowed, evidence);
        lock (sync) captures.Add(session.SessionId, session);
        return session;
    }
    public Task<CaptureCycleResult> FinishCaptureWindowAsync(AcquisitionSession session, CaptureWorkCommit work,
        ActionWindow releaseWindow, CancellationToken token)
    {
        if (work.RunId != session.Request.Correlation.RunId || work.OperationId != session.Request.Correlation.OperationId ||
            !work.MediaReleased || work.WriteIds.Count == 0 || work.WriteIds.Any(id => id == Guid.Empty))
            throw new InvalidOperationException("CaptureWorkCommitInvalid");
        return ReleaseCaptureAsync(session, releaseWindow, null, token);
    }
    public Task<CaptureCycleResult> CloseFailedCaptureWindowAsync(AcquisitionSession session, string failureReason,
        ActionWindow cleanupWindow, CancellationToken token)
    {
        if (session.Request.Role != CaptureRole.ThreeD || string.IsNullOrWhiteSpace(failureReason))
            throw new InvalidOperationException("FailureCleanupOnlyForPublicThreeD");
        return ReleaseCaptureAsync(session, cleanupWindow, failureReason, token);
    }
    private async Task<CaptureCycleResult> ReleaseCaptureAsync(AcquisitionSession session, ActionWindow window,
        string? originalFailure, CancellationToken token)
    {
        lock (sync)
            if (!captures.TryGetValue(session.SessionId, out var active) || active != session || session.State != AcquisitionState.CaptureAllowed)
                throw new InvalidOperationException("CaptureSessionNotCurrent");
        using var deadline = LimitTo(window, token);
        try
        {
            var context = activeInspection ?? throw new InvalidOperationException("CaptureSessionNotActive");
            if (context.OperationId != session.Request.Correlation.OperationId || context.ConnectionEpoch != session.Request.Correlation.ConnectionEpoch)
                throw new InvalidOperationException("CaptureSessionIdentityMismatch");
            // Opening evidence is already durable. Normal polling during camera/worker
            // processing is bounded; retain a separately identified closing segment.
            var segmentStart = (wire.ExchangeSequence, heartbeat.ExchangeSequence);
            var remaining = TimeSpan.FromSeconds((window.DueTick - Stopwatch.GetTimestamp()) / (double)Stopwatch.Frequency);
            await CompleteInspectionAsync(context, remaining, deadline.Token);
            var evidence = await CompleteEvidenceAsync(session.Request.Correlation, DeviceCompletionMeaning.CaptureReleased,
                window, deadline.Token, [session.Request.PositionEvidence],
                sampledObservation: Observe().Identity,
                segmentStart: segmentStart, precedingEvidence: session.Evidence);
            lock (sync) captures.Remove(session.SessionId);
            return new(session, originalFailure is null ? AcquisitionState.Released : AcquisitionState.Blocked, evidence, originalFailure);
        }
        catch (Exception error)
        {
            LatchFailure(error.Message);
            stopRequested = true;
            return new(session, AcquisitionState.HeldUnknown, null, originalFailure ?? error.Message);
        }
    }
    private void RequireKnownPosition(PositionReachedEvidence position)
    {
        lock (sync)
            if (!position.Matched || position.Correlation.ConnectionEpoch != epoch ||
                !reachedPositions.TryGetValue(position.Correlation.ActionId, out var actual) || !actual.Positions.Contains(position))
                throw new InvalidOperationException("PositionEvidenceNotFromCurrentDeviceAction");
    }
    private static CancellationTokenSource LimitTo(ActionWindow window, CancellationToken token)
    {
        if (!window.IsValid || !window.Contains(Stopwatch.GetTimestamp())) throw new TimeoutException("ActionWindowClosed");
        return new WindowCancellation(window, token);
    }
    // A platform timer is a wake-up hint, not proof that the monotonic deadline
    // has arrived. Dispatch still checks the original window independently.
    private sealed class WindowCancellation : CancellationTokenSource
    {
        private readonly long due;
        private readonly Timer timer;
        private readonly CancellationTokenRegistration parent;
        private int closed;
        public WindowCancellation(ActionWindow window, CancellationToken token)
        {
            due = window.DueTick;
            timer = new Timer(_ => Wake(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            parent = token.Register(() => CancelSafely());
            Wake();
        }
        private void Wake()
        {
            if (Volatile.Read(ref closed) != 0 || IsCancellationRequested) return;
            var remaining = due - Stopwatch.GetTimestamp();
            if (remaining <= 0) { CancelSafely(); return; }
            try { timer.Change(TimeSpan.FromMilliseconds(Math.Max(1, Math.Ceiling(remaining * 1000d / Stopwatch.Frequency))), Timeout.InfiniteTimeSpan); }
            catch (ObjectDisposedException) { }
        }
        private void CancelSafely()
        {
            try { Cancel(); } catch (ObjectDisposedException) { }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Exchange(ref closed, 1) == 0)
            { timer.Dispose(); parent.Dispose(); }
            base.Dispose(disposing);
        }
    }
    private FlipRequest? awaitingPutBack;
    public async Task<DeviceActionEvidence> FlipAsync(FlipRequest request, CancellationToken token)
    {
        RequireKnownPosition(request.PositionEvidence);
        if (!request.Correlation.IsValid || request.Correlation.ConnectionEpoch != epoch || request.IntentWriteId == Guid.Empty ||
            request.Correlation.RunId != request.PositionEvidence.Correlation.RunId || request.TransitionId == Guid.Empty ||
            request.Correlation.ObjectId is null || request.Correlation.PhysicalSlotIndex is null)
            throw new InvalidOperationException("FlipIdentityInvalid");
        lock (sync)
        {
            if (awaitingPutBack is not null) throw new InvalidOperationException("PreviousPutBackRequired");
            if (!flipPreparations.Remove(request.PositionEvidence.Correlation.ActionId, out var prepared) ||
                prepared != new FlipMovePreparation(request.TransitionId, request.Model, request.TargetPose,
                    request.Correlation.ObjectId, request.Correlation.PhysicalSlotIndex.Value))
                throw new InvalidOperationException("FlipPreparedProgramIdentityMismatch");
            putBackPositionActionId = null;
        }
        BeginEvidence(request.Correlation);
        using var deadline = LimitTo(request.Window, token);
        await EnsureAdmissionObservationsAsync(true, deadline.Token);
        var ownsStage = false;
        try
        {
            var observed = await ExecuteTransitionCommandAsync(request.Correlation, request.Window, false, deadline.Token, () => ownsStage = true);
            var result = await CompleteEvidenceAsync(request.Correlation, DeviceCompletionMeaning.FlipCompleted,
                request.Window, deadline.Token, sampledObservation: observed);
            lock (sync) awaitingPutBack = request;
            return result with { TransitionId = request.TransitionId };
        }
        catch (Exception error) { if (ownsStage) HoldUnknownStageAction(request.Correlation.ConnectionEpoch, error.Message); throw; }
        finally { if (ownsStage) EndStageAction(); }
    }
    public async Task<DeviceActionEvidence> PutBackAsync(PutBackRequest request, CancellationToken token)
    {
        RequireKnownPosition(request.PositionEvidence);
        lock (sync)
        {
            if (putBackPositionActionId != request.PositionEvidence.Correlation.ActionId ||
                awaitingPutBack is not { } flip || request.TransitionId != flip.TransitionId || request.IntentWriteId == Guid.Empty ||
                !request.Correlation.IsValid || request.Correlation.RunId != flip.Correlation.RunId ||
                request.Correlation.TrayId != flip.Correlation.TrayId || request.Correlation.ObjectId != flip.Correlation.ObjectId ||
                request.Correlation.PhysicalSlotIndex != flip.Correlation.PhysicalSlotIndex ||
                request.Correlation.PlanRevision != flip.Correlation.PlanRevision || request.Correlation.ConnectionEpoch != epoch)
                throw new InvalidOperationException("PutBackTransitionIdentityMismatch");
        }
        BeginEvidence(request.Correlation);
        using var deadline = LimitTo(request.Window, token);
        await EnsureAdmissionObservationsAsync(true, deadline.Token);
        var ownsStage = false;
        try
        {
            var observed = await ExecuteTransitionCommandAsync(request.Correlation, request.Window, true, deadline.Token, () => ownsStage = true);
            var result = await CompleteEvidenceAsync(request.Correlation, DeviceCompletionMeaning.PutBackCompleted,
                request.Window, deadline.Token, sampledObservation: observed);
            lock (sync) { awaitingPutBack = null; putBackPositionActionId = null; }
            return result with { TransitionId = request.TransitionId };
        }
        catch (Exception error) { if (ownsStage) HoldUnknownStageAction(request.Correlation.ConnectionEpoch, error.Message); throw; }
        finally { if (ownsStage) EndStageAction(); }

    }
}
