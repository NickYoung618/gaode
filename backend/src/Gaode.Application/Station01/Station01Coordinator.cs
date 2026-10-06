using System.Collections.Concurrent;
using Gaode.Domain.Station01;
using Gaode.Diagnostics;

namespace Gaode.Application.Station01;

public sealed class Station01Coordinator(int normalCapacity, int controlCapacity, int terminalCapacity)
{
    private readonly ConcurrentDictionary<Guid, RunSnapshot> _runs = new();
    private readonly FlowMailbox _mailbox = new(normalCapacity, controlCapacity, terminalCapacity);
    private readonly ConcurrentDictionary<Guid, ControlLatch> _latches = new();
    private readonly TerminalReservation _terminals = new(terminalCapacity);
    private readonly CancellationTokenSource _lifetime = new();
    private Task? _consumer;
    private int _admissionClosed;
    public event Action<RunSnapshot>? SnapshotChanged;
    public int ActiveRunCount => _runs.Values.Count(x => !RunStateRules.IsTerminal(x.State) && x.FaultRestart?.NewRunId is null);
    public RunSnapshot? CurrentRun => _runs.Values.Where(x => !RunStateRules.IsTerminal(x.State) && x.FaultRestart?.NewRunId is null)
        .OrderByDescending(x => x.ObservedRevision).FirstOrDefault();
    public bool AdmissionClosed => Volatile.Read(ref _admissionClosed) != 0;
    public bool ConsumerStopped => _consumer is null || _consumer.IsCompleted;
    public Task ConsumerCompletion => _consumer ?? Task.CompletedTask;
    public bool ConsumerStopRequested => _lifetime.IsCancellationRequested;

    public void Start() => _consumer ??= Task.Run(() => _mailbox.DrainAsync(Apply, _lifetime.Token));
    public bool TryRegister(RunSnapshot snapshot)
    {
        if (AdmissionClosed) return false;
        if (!_terminals.TryReserve()) return false;
        if (!_runs.TryAdd(snapshot.RunId, snapshot)) { _terminals.Release(); return false; }
        _latches[snapshot.RunId] = new();
        return true;
    }
    public RunSnapshot? Query(Guid runId) => _runs.TryGetValue(runId, out var value) ? value : null;
    public ControlLatch? Control(Guid runId) => _latches.TryGetValue(runId, out var value) ? value : null;
    public Task<RunSnapshot> SetAsync(Guid runId, Func<RunSnapshot, RunSnapshot> change,
        bool terminal = false, bool control = false) => _mailbox.Post(runId, change, terminal, control);

    public void SignalCancel(Guid runId)
    {
        if (!_latches.TryGetValue(runId, out var latch)) return;
        latch.RequestCancel();
        _ = SetAsync(runId, s => s with { CancelRequested = true, ObservedRevision = s.ObservedRevision + 1 }, control: true);
    }

    public IReadOnlyList<RunSnapshot> BeginShutdown()
    {
        Interlocked.Exchange(ref _admissionClosed, 1);
        foreach (var latch in _latches.Values) latch.RequestStop();
        return _runs.Values.Where(x => !RunStateRules.IsTerminal(x.State)).ToArray();
    }

    public async Task StopConsumerAsync(CancellationToken cancellationToken)
    {
        _lifetime.Cancel();
        if (_consumer is not null)
        {
            try { await _consumer.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) when (_consumer.IsCompleted) { }
        }
    }

    private RunSnapshot Apply(Guid id, Func<RunSnapshot, RunSnapshot> change)
    {
        if (!_runs.TryGetValue(id, out var previous)) throw new InvalidOperationException("未知运行");
        var next = change(previous);
        if (RunStateRules.IsTerminal(previous.State) && next.State != previous.State)
            throw new InvalidOperationException("最终运行状态不可改写");
        _runs[id] = next;
        if (previous.State != next.State || previous.Action != next.Action ||
            previous.Capture != next.Capture || previous.Algorithm != next.Algorithm ||
            previous.ErrorCode != next.ErrorCode || previous.WholeTaskState != next.WholeTaskState)
            RuntimeDiagnostics.Record("RunState", "Changed", id, new
            {
                next.RequestId, previous = previous.State.ToString(), current = next.State.ToString(),
                action = next.Action.ToString(), capture = next.Capture.ToString(),
                algorithm = next.Algorithm.ToString(), next.ErrorCode, next.WholeTaskState,
                next.ObservedRevision, next.PersistedRevision, finalOutcome = next.FinalOutcome.ToString()
            }, warning: next.ErrorCode is not null);
        if (RunStateRules.IsTerminal(next.State) && !RunStateRules.IsTerminal(previous.State)) _terminals.Release();
        SnapshotChanged?.Invoke(next);
        return next;
    }
}
