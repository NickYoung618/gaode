using Gaode.Application.Ports;

namespace Gaode.Application.Algorithms;

public sealed class ManagedAlgorithmCall : IAsyncDisposable
{
    private readonly Func<ValueTask> end;
    private int ended;
    private readonly long due;
    private readonly Func<TimeSpan> releaseWait;
    internal ManagedAlgorithmCall(Task exited, Task<AlgorithmEvent> result, Task<AlgorithmEvent> lateResult, long due, Func<TimeSpan> releaseWait, Func<ValueTask> end) { Exited = exited; Result = result; LateResult = lateResult; this.due = due; this.releaseWait = releaseWait; this.end = end; }
    public Task Exited { get; }
    public Task<AlgorithmEvent> Result { get; }
    public Task<AlgorithmEvent> LateResult { get; }
    public TimeSpan RemainingResultWait => TimeSpan.FromSeconds(Math.Max(0, due-TimeProvider.System.GetTimestamp())/(double)TimeProvider.System.TimestampFrequency);
    public TimeSpan RemainingReleaseWait => releaseWait();
    public ValueTask DisposeAsync() => Interlocked.Exchange(ref ended,1) == 0 ? end() : ValueTask.CompletedTask;
}

public sealed partial class AlgorithmRuntime
{
    public async Task<ManagedAlgorithmCall> DispatchSynchronousAsync(AlgorithmRequest request, Guid trayId,
        int algorithmWaitMs, int releaseMs, int saveMs, Action<AlgorithmEvent> consumer, CancellationToken token)
    {
        if (!request.Envelope.IsValid || trayId == Guid.Empty || algorithmWaitMs <= 0 || releaseMs <= 0 || saveMs <= 0 ||
            request.Inputs.Count == 0 || request.Inputs.Any(x => x.RunId != request.Envelope.RunId || !media.IsReady(x.MediaId)))
            throw new InvalidOperationException("SynchronousAlgorithmIdentityOrBudgetInvalid");
        if (resources?.AdmissionClosed == true) throw new InvalidOperationException("AlgorithmAdmissionClosed");
        // One prepared synchronous consumer at a time. The original absolute envelope
        // deadline and algorithm budget include this wait; no batch admission is enabled.
        var clock = TimeProvider.System;
        var due = Math.Min(request.Envelope.DueTick, checked(request.Envelope.StartTick +
            (long)Math.Ceiling(algorithmWaitMs * clock.TimestampFrequency / 1000d)));
        TimeSpan Remaining() => TimeSpan.FromSeconds(Math.Max(0, due-clock.GetTimestamp())/(double)clock.TimestampFrequency);
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(token);
        wait.CancelAfter(Remaining());
        await synchronousDetection.WaitAsync(wait.Token);
        IDisposable inputLease;
        try { inputLease = leases.HoldInputs(request.Inputs,"algorithm:"+request.CallId); }
        catch { synchronousDetection.Release(); throw; }
        if (resources is not null)
        {
            try { await resources.RegisterAsync(new(request.Envelope.RunId,trayId,request.CallId,request.Envelope.OperationId,
                request.Envelope.ClockId,request.Envelope.SnapshotId,request.Inputs,releaseMs) { Request = request },wait.Token); }
            catch { inputLease.Dispose(); synchronousDetection.Release(); throw; }
        }
        var execution = new IsolatedAlgorithmCall(request,inputLease,clock.GetTimestamp,finished =>
        {
            completed.Enqueue(finished.Snapshot()); while(completed.Count>64) completed.TryDequeue(out _);
            active.TryRemove(request.CallId,out _); synchronousDetection.Release();
        },resources is null ? null : resources.Observe);
        if (!active.TryAdd(request.CallId,execution)) { inputLease.Dispose(); synchronousDetection.Release(); throw new InvalidOperationException("AlgorithmCallAlreadyActive"); }
        long releaseDue = 0;
        void StartRelease()
        {
            var candidate = checked(clock.GetTimestamp() + (long)Math.Ceiling(releaseMs * clock.TimestampFrequency / 1000d));
            Interlocked.CompareExchange(ref releaseDue, candidate, 0);
        }
        TimeSpan ReleaseRemaining() => resources?.RemainingReleaseWait(request.CallId) ??
            TimeSpan.FromSeconds(Math.Max(0, Volatile.Read(ref releaseDue)-clock.GetTimestamp())/(double)clock.TimestampFrequency);
        var terminalGate = new object();
        var result = new TaskCompletionSource<AlgorithmEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        var lateResult = new TaskCompletionSource<AlgorithmEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        string? terminal = null;
        void Settle(string reason, AlgorithmEvent? value = null, Exception? error = null)
        {
            lock (terminalGate)
            {
                if (terminal is not null) return;
                terminal = reason;
                StartRelease(); resources?.Terminal(request.CallId, reason);
                if (value is not null) result.TrySetResult(value);
                else if (reason == "Cancelled") result.TrySetCanceled(token);
                else
                {
                    result.TrySetException(error ?? new TimeoutException("OriginalAlgorithmDeadlineExceeded"));
                    _ = result.Task.Exception; // dispatch may fail before a handle reaches its business consumer
                }
            }
        }
        var cancellation = token.Register(() => Settle("Cancelled"));
        async ValueTask End()
        {
            Settle("Cancelled"); // ending a consumer cannot accept a later result; an existing winner is preserved
            cancellation.Dispose();
            StartRelease();
            execution.EndBusiness(!execution.InputAndExecutionEnded.IsCompletedSuccessfully);
            resources?.StartObservation(request.CallId,token.IsCancellationRequested ? "Cancelled" : "BusinessEnded");
            if(resources is not null) { using var limit = new CancellationTokenSource(TimeSpan.FromMilliseconds(saveMs),clock); await resources.FlushAsync(limit.Token); }
        }
        void OnEvent(AlgorithmEvent value)
        {
            if (!AcquisitionContract.Matches(value,request) || value.Request.Envelope.Attempt != request.Envelope.Attempt || !execution.Observe(value)) return;
            if(value.Kind is AlgorithmEventKind.Result or AlgorithmEventKind.Failed)
            {
                lock (terminalGate)
                {
                    if (token.IsCancellationRequested) Settle("Cancelled");
                    else if (clock.GetTimestamp() >= due) Settle("TimedOut");
                    if (terminal is not null)
                    {
                        if (terminal is "TimedOut" or "Cancelled") lateResult.TrySetResult(value);
                        return;
                    }
                    Settle(value.Kind.ToString(), value);
                    consumer(value);
                }
                return;
            }
            consumer(value); // resource events remain independent of business arbitration
        }
        execution.Start(algorithm,OnEvent,() => !wait.IsCancellationRequested && clock.GetTimestamp()<due,resources);
        try
        {
            var dispatch = await execution.Dispatched.WaitAsync(wait.Token);
            if(dispatch.Error is not null) { Settle("DispatchError", error:dispatch.Error); throw dispatch.Error; }
            // The caller retains its existing result wait and decision semantics. The
            // supervisor observes the same absolute algorithm deadline independently.
            _ = ObserveDeadlineAsync();
            return new(execution.InputAndExecutionEnded,result.Task,lateResult.Task,due,ReleaseRemaining,End);
        }
        catch
        {
            Settle(token.IsCancellationRequested ? "Cancelled" : "TimedOut");
            await End(); throw;
        }
        async Task ObserveDeadlineAsync()
        {
            // This observer is bounded by one occupied call, never launches inference,
            // and terminates on resource completion or the original absolute deadline.
            using var timer = new CancellationTokenSource();
            var delay = Task.Delay(Remaining(),timer.Token);
            if(await Task.WhenAny(delay,result.Task)==delay)
            { Settle(token.IsCancellationRequested ? "Cancelled" : "TimedOut"); }
            else await timer.CancelAsync();
        }
    }
}
