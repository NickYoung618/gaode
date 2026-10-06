using Gaode.Application.Ports;
using Gaode.Application.Station01;
using Gaode.Application.Timing;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Diagnostics;

namespace Gaode.Application.Acquisition;

public sealed class AcquisitionCoordinator(ICapturePort camera, IMediaStore media,
    OperationIngress ingress)
{
    public Task<MediaRef> CaptureAsync(RunExecution run, CaptureRole role,
        FixedPoint point, string? scopeId, string? scopeVersion,
        string cameraBinding, string lightBinding, long maxBytes,
        CancellationToken cancellationToken) => RuntimeDiagnostics.ObserveAsync(
            "Capture", run.RunId, new { run.RequestId, role = role.ToString(), point.Id,
                point.Version, scopeId, scopeVersion, cameraBinding, lightBinding, maxBytes,
                connectionEpoch = camera.ConnectionEpoch },
            () => CaptureCoreAsync(run, role, point, scopeId, scopeVersion, cameraBinding,
                lightBinding, maxBytes, cancellationToken),
            r => new { r.MediaId, r.CaptureId, r.RelativeKey, disposition = "MediaSaved" });

    private async Task<MediaRef> CaptureCoreAsync(RunExecution run, CaptureRole role,
        FixedPoint point, string? scopeId, string? scopeVersion,
        string cameraBinding, string lightBinding, long maxBytes,
        CancellationToken cancellationToken)
    {
        var captureId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var intent = new OperationIntentPayload(operationId, role == CaptureRole.F ? "CaptureF" : "Capture3D",
            1, null, captureId, scopeId ?? point.Id, run.Config.SnapshotId, Guid.Empty);
        var intentReceipt = await run.SaveAsync(WriteKind.CaptureIntent, intent, cancellationToken: cancellationToken);
        await run.ReportAsync(capture: CaptureState.Reserved);
        using var reservation = media.ReserveCapture(captureId, role == CaptureRole.ThreeD ? "3D" : "F", maxBytes);
        var budget = role == CaptureRole.ThreeD
            ? run.Config.Budget.BusinessMs.Capture3d : run.Config.Budget.BusinessMs.CaptureF;
        var key = new OperationKey(run.RunId, operationId, 1, OperationPhase.Completion);
        var window = ingress.Register(key, budget);
        var envelope = new PortEnvelope(run.RunId, operationId, 1, run.SessionId,
            run.Config.SnapshotId, run.Config.Public.Version, run.Config.Public.Purpose,
            window.StartTick, window.DueTick, run.ClockId);
        var request = new CaptureRequest(envelope, captureId, role, point.Id, point.Version,
            scopeId, scopeVersion, cameraBinding, lightBinding, intentReceipt.WriteId, maxBytes);
        var gate = new CaptureEvidenceGate();
        var expectedEpoch = camera.ConnectionEpoch;
        var callbackLogs = 0;
        RuntimeDiagnostics.Record("Capture", "Requesting", run.RunId,
            new { operationId, captureId, expectedEpoch, window.StartTick, window.DueTick,
                budgetMs = budget, intentReceipt.WriteId });
        void OnEvent(CaptureEvent e)
        {
            var matched = AcquisitionContract.Matches(e, request, expectedEpoch) &&
                e.Request.Envelope.Attempt == request.Envelope.Attempt;
            if (Interlocked.Increment(ref callbackLogs) <= 16)
                RuntimeDiagnostics.Record("CaptureFeedback", matched ? "Received" : "IgnoredMismatch", run.RunId,
                    new { operationId, captureId, kind = e.Kind.ToString(), e.ErrorCode,
                        expectedEpoch, actualEpoch = e.ConnectionEpoch,
                        actualOperationId = e.Request.Envelope.OperationId,
                        actualCaptureId = e.Request.CaptureId }, warning: !matched || e.Kind == CaptureEventKind.Failed);
            if (!matched) return;
            if (e.Kind == CaptureEventKind.Failed)
            {
                ingress.Receive(key, e.ErrorCode ?? "CaptureFailed");
                return;
            }
            if (e.Kind == CaptureEventKind.Capturing) _ = run.ReportAsync(capture: CaptureState.Capturing);
            if (e.Kind == CaptureEventKind.Ended) _ = run.ReportAsync(capture: CaptureState.Ended);
            if (gate.Observe(e)) ingress.Receive(key, "EndedAndMediaTaken");
        }
        await run.ReportAsync(capture: CaptureState.Requested);
        await camera.RequestCaptureAsync(request, OnEvent, cancellationToken);
        var decision = await window.Completion;
        RuntimeDiagnostics.Record("Capture", "Decision", run.RunId,
            new { operationId, captureId, outcome = decision.Outcome.ToString(), decision.Reason,
                window.StartTick, window.DueTick, decision.ReceivedTick,
                callbackCount = Volatile.Read(ref callbackLogs), callbackLogLimit = 16 },
            warning: !decision.IsSuccessful || decision.Reason != "EndedAndMediaTaken");
        if (!decision.IsSuccessful || decision.Reason != "EndedAndMediaTaken")
            throw new InvalidOperationException($"采集完成未知或超时，禁止依赖算法与运动; role={role}; " +
                $"operation={operationId}; start={window.StartTick}; due={window.DueTick}; " +
                $"received={decision.ReceivedTick}; outcome={decision.Outcome}; reason={decision.Reason}");
        var (buffer, format) = gate.Take();
        var captureFact = gate.TakeFact(request, expectedEpoch);
        RuntimeDiagnostics.Record("MediaFileSave", "Started", run.RunId, new { operationId, captureId, format });
        var reference = await media.SaveAsync(run.RunId, captureId,
            role == CaptureRole.ThreeD ? "3D" : "F", point.Version,
            scopeVersion ?? "NotApplicable", buffer, format,
            captureFact.MediaSource, cancellationToken);
        reference = reference with { Purpose = run.Config.Public.Purpose };
        RuntimeDiagnostics.Record("MediaFileSave", "Returned", run.RunId,
            new { operationId, captureId, reference.MediaId, reference.RelativeKey });
        await run.SaveAsync(WriteKind.Media, reference, cancellationToken: cancellationToken);
        var captureCommit = await run.SaveAsync(WriteKind.CaptureFact,
            new { captureId, operationId, role = role.ToString(), ended = true,
                mediaTaken = true, mediaId = reference.MediaId, reference.RelativeKey,
                pointVersion = point.Version, scopeVersion, triggerCount = camera.TriggerCount(role),
                requestedCapture = request, captureFact },
            cancellationToken: cancellationToken);
        if (role == CaptureRole.ThreeD) {
            run.InitialThreeDCapture = captureFact;
            run.InitialThreeDCaptureWriteId = captureCommit.WriteId;
        }
        await run.ReportAsync(capture: CaptureState.MediaTaken);
        return reference;
    }
}
