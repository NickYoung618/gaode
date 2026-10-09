namespace Gaode.Application.Station01;

// A short commit boundary only. Callers must never hold it while invoking a device or engine.
public sealed class RunFactCommitCoordinator
{
    public static RunFactCommitCoordinator Shared { get; } = new();
    private readonly object gate = new();
    private readonly Dictionary<Guid, Entry> entries = [];
    private sealed class Entry { public readonly SemaphoreSlim Mutex = new(1); public int Users; }
    public async ValueTask<IDisposable> EnterAsync(Guid runId, CancellationToken token)
    {
        Entry entry;
        lock (gate)
        {
            if (!entries.TryGetValue(runId, out entry!)) entries.Add(runId, entry = new());
            entry.Users++;
        }
        try { await entry.Mutex.WaitAsync(token); }
        catch { Release(runId, entry, false); throw; }
        return new Lease(this, runId, entry);
    }
    private void Release(Guid runId, Entry entry, bool acquired)
    {
        if (acquired) entry.Mutex.Release();
        lock (gate) if (--entry.Users == 0) { entries.Remove(runId); entry.Mutex.Dispose(); }
    }
    private sealed class Lease(RunFactCommitCoordinator owner, Guid id, Entry entry) : IDisposable
    {
        private int released;
        public void Dispose() { if (Interlocked.Exchange(ref released, 1) == 0) owner.Release(id, entry, true); }
    }
}
