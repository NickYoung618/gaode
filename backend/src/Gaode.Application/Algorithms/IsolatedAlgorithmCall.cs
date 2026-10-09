using Gaode.Application.Ports;
using Gaode.Diagnostics;

namespace Gaode.Application.Algorithms;

public sealed record AlgorithmExecutionEvidence(Guid RunId, Guid CallId, AlgorithmRole Role,
    string Dispatch, long DispatchTick, long? AcceptedTick, Guid? WorkerSessionId,
    bool InputsReleased, bool ExecutionEnded, bool DispatchReturned,
    bool BusinessEnded, string Cancellation, string? Error, bool Reclaimed);

internal sealed record DispatchCompletion(AlgorithmDispatch? Dispatch, Exception? Error,
    bool NotDispatched);

// Exactly one instance per acquired execution slot. No replacement slot is released until
// dispatch, actual execution and the (at most one) cancellation callback job have ended.
internal sealed class IsolatedAlgorithmCall(AlgorithmRequest request, IDisposable inputLease,
    Func<long> timestamp, Action<IsolatedAlgorithmCall> reclaim,
    Action<AlgorithmExecutionEvidence>? observeResources = null)
{
    private readonly object gate = new();
    private readonly CancellationTokenSource cancellation = new(); // Never linked to a caller.
    private readonly TaskCompletionSource<DispatchCompletion> dispatched = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource reclaimed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource actualEnded = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool inputsReleased, executionEnded, dispatchReturned, businessEnded, released;
    private bool inputObserved, cancelRequested, cancelFinished;
    private string dispatchState = "Scheduled", cancellationState = "NotRequested";
    private string? error;
    private long dispatchTick;
    private long? acceptedTick;
    private Guid? workerSession;

    public Task<DispatchCompletion> Dispatched => dispatched.Task;
    public Task Reclaimed => reclaimed.Task;
    public Task InputAndExecutionEnded => actualEnded.Task;
    public AlgorithmExecutionEvidence Snapshot()
    {
        lock (gate) return new(request.Envelope.RunId, request.CallId, request.Role,
            dispatchState, dispatchTick, acceptedTick, workerSession, inputsReleased,
            executionEnded, dispatchReturned, businessEnded, cancellationState, error, released);
    }

    internal void MarkEntered(long tick)
    {
        lock (gate) { dispatchState = "Entered"; dispatchTick = tick; }
    }

    public void Start(IAlgorithmPort port, Action<AlgorithmEvent> onEvent, Func<bool> mayEnter,
        AlgorithmResourceSupervisor? resources)
    {
        // Bounded by the already acquired role slot, even when RequestAsync never returns.
        // A dedicated synchronous prefix prevents a blocked adapter from occupying the
        // thread needed for the F role or business deadline/control processing.
        _ = Task.Factory.StartNew(async () =>
        {
            AlgorithmDispatch? result = null;
            Exception? failure = null;
            var rejected = false;
            try
            {
                var eligible = mayEnter();
                RuntimeDiagnostics.Record("AlgorithmAdmission", "EligibilityObserved", request.Envelope.RunId,
                    new { request.CallId, request.Envelope.OperationId, request.Role, eligible });
                if (!eligible)
                {
                    RuntimeDiagnostics.Record("AlgorithmAdmission", "Rejected", request.Envelope.RunId,
                        new { request.CallId, request.Envelope.OperationId, request.Role, reason = "OriginalWindowClosedBeforeEntry" }, warning: true);
                    throw new AlgorithmNotDispatchedException(request.CallId, "OriginalWindowClosedBeforeEntry");
                }
                var tick = timestamp();
                if (resources is null) MarkEntered(tick);
                else if (!resources.TryEnter(this, tick))
                {
                    RuntimeDiagnostics.Record("AlgorithmAdmission", "Rejected", request.Envelope.RunId,
                        new { request.CallId, request.Envelope.OperationId, request.Role, reason = "HostClosedBeforeEntry" }, warning: true);
                    throw new AlgorithmNotDispatchedException(request.CallId, "HostClosedBeforeEntry");
                }
                RuntimeDiagnostics.Record("AlgorithmAdmission", "PermissionGranted", request.Envelope.RunId,
                    new { request.CallId, request.Envelope.OperationId, request.Role, dispatchTick });
                observeResources?.Invoke(Snapshot());
                RuntimeDiagnostics.Record("AlgorithmDispatch", "Entered", request.Envelope.RunId,
                    new { request.CallId, request.Envelope.OperationId, request.Role, dispatchTick,
                        disposition = "AdapterMayOwn;NotAcceptanceOrCompletion" });
                result = await port.RequestAsync(request, onEvent, cancellation.Token).ConfigureAwait(false);
                lock (gate) dispatchState = "Returned";
                _ = ObserveExitAsync(result.Exited);
            }
            catch (Exception caught)
            {
                failure = caught;
                lock (gate)
                {
                    rejected = caught is AlgorithmNotDispatchedException refusal &&
                        refusal.CallId == request.CallId && !inputObserved;
                    error = caught.GetType().Name;
                    dispatchState = rejected ? "NotDispatched" : "Unknown";
                    if (rejected) { ReleaseInputs(); executionEnded = true; }
                }
            }
            finally
            {
                lock (gate) { dispatchReturned = true; TryReclaim(); }
                // This task never faults: late dispatch exceptions are observed here.
                dispatched.TrySetResult(new(result, failure, rejected));
            }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();
    }

    public bool Observe(AlgorithmEvent value)
    {
        lock (gate)
        {
            if (workerSession is not null &&
                workerSession != value.WorkerSessionId) return false;
            workerSession ??= value.WorkerSessionId;
            if (released) return true; // Late evidence may still be recorded by ingress.
            switch (value.Kind)
            {
                case AlgorithmEventKind.Accepted:
                    inputObserved = true;
                    acceptedTick ??= timestamp();
                    workerSession ??= value.WorkerSessionId;
                    break;
                case AlgorithmEventKind.Running:
                case AlgorithmEventKind.Result:
                case AlgorithmEventKind.Failed:
                    inputObserved = true;
                    break;
                case AlgorithmEventKind.InputReleased:
                    inputObserved = true;
                    ReleaseInputs(); // Does not establish actual execution end.
                    break;
                case AlgorithmEventKind.WorkerExited:
                    inputObserved = true;
                    ReleaseInputs();
                    executionEnded = true;
                    break;
            }
            TryReclaim();
            return true;
        }
    }

    private async Task ObserveExitAsync(Task exited)
    {
        try
        {
            await exited.ConfigureAwait(false);
            lock (gate) { executionEnded = true; ReleaseInputs(); TryReclaim(); }
        }
        catch (Exception failure)
        {
            lock (gate) error = "ExitUnconfirmed:" + failure.GetType().Name;
            observeResources?.Invoke(Snapshot());
        }
    }

    public void EndBusiness(bool requestCancellation)
    {
        lock (gate)
        {
            if (requestCancellation && !executionEnded && !cancelRequested)
            {
                cancelRequested = true;
                cancellationState = "Requested";
                // At most one cancellation job per occupied slot. Never await it on a
                // business/control thread, and never release the slot while it is hung.
                _ = Task.Factory.StartNew(() =>
                {
                    lock (gate) cancellationState = "CallbacksRunning";
                    try
                    {
                        cancellation.Cancel();
                        lock (gate) cancellationState = "CallbacksCompleted";
                    }
                    catch (Exception failure)
                    {
                        lock (gate)
                        {
                            cancellationState = "CallbacksFailed";
                            error = "Cancellation:" + failure.GetType().Name;
                        }
                    }
                    finally
                    {
                        lock (gate) { cancelFinished = true; TryReclaim(); }
                    }
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
            businessEnded = true;
            TryReclaim();
        }
    }

    private void ReleaseInputs()
    {
        if (inputsReleased) return;
        inputsReleased = true;
        inputLease.Dispose();
    }

    private void TryReclaim()
    {
        observeResources?.Invoke(Snapshot());
        if (inputsReleased && executionEnded && dispatchReturned && (!cancelRequested || cancelFinished)) actualEnded.TrySetResult();
        if (released || !businessEnded || !dispatchReturned || !executionEnded ||
            (cancelRequested && !cancelFinished)) return;
        released = true;
        cancellation.Dispose();
        reclaim(this);
        reclaimed.TrySetResult();
        observeResources?.Invoke(Snapshot());
    }
}
