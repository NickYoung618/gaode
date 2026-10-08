using Gaode.Application.Ports;
using Gaode.Diagnostics;

namespace Gaode.Application.Acquisition;

/// <summary>Formal capture receipt and persistence. No algorithm or motion dependency.</summary>
public sealed class CameraAcquisitionService(ICapturePort camera, IMediaStore media)
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> _requests = new();
    public async Task<ReceivedCapture> ReceiveAsync(CaptureRequest request, CancellationToken ct,
        Action<CaptureEvent>? observer = null)
    {
        var epoch = camera.GetConnectionEpoch(request.CameraBindingId);
        var gate = new CaptureEvidenceGate();
        var completion = new TaskCompletionSource<ReceivedCapture>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnEvent(CaptureEvent e)
        {
            if (!AcquisitionContract.Matches(e, request, epoch) || e.Request.Envelope.Attempt != request.Envelope.Attempt) return;
            observer?.Invoke(e);
            if (e.Kind is CaptureEventKind.Failed or CaptureEventKind.Unknown)
            {
                completion.TrySetException(new IOException(e.ErrorCode ?? "CaptureUnknown_NoAutomaticReplay")); return;
            }
            if (!gate.Observe(e)) return;
            try
            {
                var (bytes, format) = gate.Take();
                var fact = gate.TakeFact(request, epoch);
                if (bytes.LongLength > request.MaxBytes) throw new InvalidDataException("MediaOverLimit");
                completion.TrySetResult(new(bytes, format, fact));
            }
            catch (Exception error) { completion.TrySetException(error); }
        }
        await camera.RequestCaptureAsync(request, OnEvent, ct);
        return await completion.Task.WaitAsync(ct);
    }

    public async Task<MediaRef> CaptureAsync(CaptureRequest request, ICameraCaptureJournal journal, CancellationToken ct)
    {
        var serial = _requests.GetOrAdd(request.CameraBindingId, _ => new(1, 1));
        await serial.WaitAsync(ct);
        try { return await CaptureCoreAsync(request, journal, ct); }
        finally { serial.Release(); }
    }

    private async Task<MediaRef> CaptureCoreAsync(CaptureRequest request, ICameraCaptureJournal journal, CancellationToken ct)
    {
        var role = request.Role == CaptureRole.ThreeD ? "3D" : request.Role == CaptureRole.F ? "F" : request.Role == CaptureRole.E ? "E" : "Detection";
        await journal.RecordIntentAsync(request, ct);
        try
        {
            using var reservation = media.ReserveCapture(request.CaptureId, role, request.MaxBytes);
            var received = await ReceiveAsync(request, ct);
            var reference = await media.SaveCaptureAsync(request.Envelope.RunId, request.CaptureId, role,
                request.PointVersion, request.ScopeVersion ?? "NotApplicable", received.Bytes,
                received.Format, received.Fact.MediaSource, received.Fact, ct);
            reference = reference with { Purpose = request.Envelope.Purpose };
            await journal.CommitAsync(request, reference, received.Fact, ct);
            await media.MarkCommittedAsync(reference, ct);
            RuntimeDiagnostics.Record("CameraCapture", "Committed", request.Envelope.RunId,
                new { request.CaptureId, reference.MediaId, reference.RelativeKey });
            return reference;
        }
        catch (Exception error)
        {
            RuntimeDiagnostics.Record("CameraCapture", "FailedOrUnknown_NoReplay", request.Envelope.RunId,
                new { request.CaptureId, request.Envelope.OperationId }, error);
            using var failureBudget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await journal.RecordFailureAsync(request, error.ToString(), failureBudget.Token); }
            catch (Exception persistenceError)
            {
                RuntimeDiagnostics.Record("CameraCapture", "FailureRecordUnconfirmed", request.Envelope.RunId,
                    new { request.CaptureId }, persistenceError);
            }
            throw;
        }
    }
}
