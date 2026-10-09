using System.Collections.Concurrent;
using Gaode.Application.Ports;
using Gaode.Diagnostics;

namespace Gaode.Application.Algorithms;

// One owner for already submitted calls, including terminal business runs restored from storage.
public sealed class AlgorithmResourceSupervisor(IAlgorithmResourceStore store, TimeProvider clock, int criticalSaveMs)
{
    private sealed class Entry(AlgorithmResourceState state)
    {
        public readonly object Gate = new();
        public AlgorithmResourceState State = state;
        public Task Saved = Task.CompletedTask;
        public bool Restored;
        public IDisposable? RestoredInputLease;
    }
    private readonly ConcurrentDictionary<Guid, Entry> calls = new();
    private readonly object admissionGate = new();
    private int closing;
    public bool AdmissionClosed => Volatile.Read(ref closing) != 0;
    public IReadOnlyList<AlgorithmResourceState> States => calls.Values.Select(e => { lock(e.Gate) return e.State; }).ToArray();
    public bool HasUnreclaimedResources => calls.Values.Any(e => { lock(e.Gate) return !e.State.Reclaimed || !e.Saved.IsCompletedSuccessfully; });
    public async Task RegisterAsync(AlgorithmResourceState state, CancellationToken token)
    {
        var initialSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entry = new Entry(state) { Saved = initialSave.Task };
        lock(admissionGate)
        {
            if (AdmissionClosed || !state.IsValid) throw new InvalidOperationException("AlgorithmResourceAdmissionClosedOrInvalid");
            if (!calls.TryAdd(state.CallId, entry)) throw new InvalidOperationException("AlgorithmCallAlreadyRegistered");
        }
        // Updates queued by shutdown wait for revision 1; storage never runs under admissionGate.
        try
        {
            await store.AppendResourceAsync(state, token);
            initialSave.TrySetResult();
        }
        catch (Exception error)
        {
            initialSave.TrySetException(error);
            _ = initialSave.Task.Exception;
            throw;
        }
    }
    internal bool TryEnter(IsolatedAlgorithmCall call, long tick)
    {
        lock(admissionGate)
        {
            if (closing != 0) return false;
            call.MarkEntered(tick); // In-memory only: no diagnostics, storage or adapter callbacks.
            return true;
        }
    }
    public void Restore(AlgorithmResourceState state, IDisposable? inputLease = null)
    {
        if (!state.IsValid) throw new InvalidDataException("AlgorithmResourceRestoreInvalid");
        if (!state.Reclaimed && !calls.TryAdd(state.CallId, new(state) { Restored = true, RestoredInputLease = inputLease }))
            throw new InvalidDataException("AlgorithmResourceRestoreDuplicate");
    }
    public void Observe(AlgorithmExecutionEvidence proof) => Update(proof.CallId, old => old with {
        WorkerSessionId = old.WorkerSessionId ?? proof.WorkerSessionId,
        Dispatch = proof.Dispatch, InputsReleased = old.InputsReleased || proof.InputsReleased,
        ExecutionEnded = old.ExecutionEnded || proof.ExecutionEnded, DispatchReturned = old.DispatchReturned || proof.DispatchReturned,
        BusinessEnded = old.BusinessEnded || proof.BusinessEnded,
        CancelCallbacksEnded = proof.Cancellation is "NotRequested" or "CallbacksCompleted" or "CallbacksFailed"
    });
    public TimeSpan? RemainingReleaseWait(Guid callId)
    {
        if (!calls.TryGetValue(callId, out var entry)) return null;
        lock(entry.Gate) return entry.State.ReleaseDueUtc.HasValue
            ? TimeSpan.FromTicks(Math.Max(0, (entry.State.ReleaseDueUtc.Value - clock.GetUtcNow()).Ticks)) : null;
    }
    public void StartObservation(Guid callId, string reason) => Update(callId, old => Start(old, reason));
    public void Terminal(Guid callId, string terminal) => Update(callId, old => old.TechnicalTerminal is null
        ? Start(old with { TechnicalTerminal = terminal }, terminal) : old);
    private AlgorithmResourceState Start(AlgorithmResourceState state, string reason)
    {
        if (state.ReleaseStartUtc.HasValue || state.Reclaimed || state.Dispatch == "NotDispatched") return state;
        var now = clock.GetUtcNow(); var tick = clock.GetTimestamp();
        return state with { ReleaseStartUtc = now, ReleaseDueUtc = now.AddMilliseconds(state.ReleaseBudgetMs),
            ReleaseStartTick = tick, ReleaseDueTick = checked(tick + (long)Math.Ceiling(state.ReleaseBudgetMs * clock.TimestampFrequency / 1000d)),
            ReleaseTrigger = reason };
    }
    public void BeginShutdown(DateTimeOffset? start = null, DateTimeOffset? due = null)
    {
        lock(admissionGate)
        {
            if (closing != 0) return;
            Volatile.Write(ref closing, 1);
        }
        foreach (var entry in calls.Values)
        {
            // An old ClockId/session remains unknown; a new Host must never restart its observation budget.
            if (!entry.Restored) StartObservation(entry.State.CallId, "HostClosing");
            if (start.HasValue && due.HasValue)
                Update(entry.State.CallId, old => old with { HostShutdownStartUtc = start, HostShutdownDueUtc = due });
            AlgorithmResourceState observed;
            lock(entry.Gate) observed = entry.State;
            RuntimeDiagnostics.Record("AlgorithmAdmission", "Closed", observed.RunId,
                new { observed.CallId, observed.OperationId, observed.Dispatch, observed.ReleaseStartUtc,
                    observed.ReleaseDueUtc, disposition = "PreserveOwnershipAndOriginalReleaseWindow" });
        }
    }
    public async Task FlushAsync(CancellationToken token)
    {
        foreach (var entry in calls.Values)
        {
            Update(entry.State.CallId, old => old.ReleaseDueUtc <= clock.GetUtcNow() && !old.Reclaimed
                ? old with { ObservationExpired = true } : old);
            Task saved; lock(entry.Gate) saved = entry.Saved;
            await saved.WaitAsync(token);
            lock(entry.Gate)
                if (entry.State.Reclaimed && entry.Saved == saved && saved.IsCompletedSuccessfully)
                {
                    if(calls.TryRemove(new KeyValuePair<Guid, Entry>(entry.State.CallId, entry))) entry.RestoredInputLease?.Dispose();
                }
        }
    }
    private void Update(Guid id, Func<AlgorithmResourceState, AlgorithmResourceState> change)
    {
        if (!calls.TryGetValue(id, out var entry)) return;
        lock(entry.Gate)
        {
            var next = change(entry.State);
            if (next == entry.State) return;
            next = next with { Revision = entry.State.Revision + 1 };
            entry.State = next;
            entry.Saved = SaveAfterAsync(entry.Saved, next);
        }
    }
    private async Task SaveAfterAsync(Task previous, AlgorithmResourceState next)
    {
        await previous;
        // Shutdown tokens cannot abandon the durable late-release write. Host waiting remains separately bounded.
        if (criticalSaveMs <= 0) throw new InvalidOperationException("AlgorithmResourceSaveBudgetInvalid");
        using var limit = new CancellationTokenSource(TimeSpan.FromMilliseconds(criticalSaveMs), clock);
        await store.AppendResourceAsync(next, limit.Token);
    }
}
