using Gaode.Application.Ports;

namespace Gaode.Integration.Tests.Support;

public sealed class CommitInterleavingGate
{
    private readonly object gate = new();
    private readonly Dictionary<WriteKind, TaskCompletionSource> releases = [];
    private readonly Dictionary<WriteKind, TaskCompletionSource<WriteBatch>> arrivals = [];
    private readonly Dictionary<WriteKind, TaskCompletionSource> receiptReleases = [];
    private readonly Dictionary<WriteKind, TaskCompletionSource<CommitReceipt>> committed = [];

    public void Hold(WriteKind kind)
    {
        lock (gate)
        {
            releases[kind] = NewSignal();
            arrivals[kind] = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public Task<WriteBatch> WaitUntilArrivedAsync(WriteKind kind, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (!arrivals.TryGetValue(kind, out var arrival))
                throw new InvalidOperationException($"{kind} 未配置提交闸门");
            return arrival.Task.WaitAsync(cancellationToken);
        }
    }

    public void Release(WriteKind kind)
    {
        lock (gate)
        {
            if (!releases.TryGetValue(kind, out var release))
                throw new InvalidOperationException($"{kind} 未配置提交闸门");
            release.TrySetResult();
        }
    }

    public void HoldReceipt(WriteKind kind)
    {
        lock (gate)
        {
            receiptReleases[kind] = NewSignal();
            committed[kind] = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public Task<CommitReceipt> WaitUntilCommittedAsync(WriteKind kind,
        CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (!committed.TryGetValue(kind, out var arrival))
                throw new InvalidOperationException($"{kind} 未配置回执闸门");
            return arrival.Task.WaitAsync(cancellationToken);
        }
    }

    public void ReleaseReceipt(WriteKind kind)
    {
        lock (gate)
        {
            if (!receiptReleases.TryGetValue(kind, out var release))
                throw new InvalidOperationException($"{kind} 未配置回执闸门");
            release.TrySetResult();
        }
    }

    public async Task BeforeCommitAsync(WriteBatch batch, CancellationToken cancellationToken)
    {
        Task? wait = null;
        lock (gate)
        {
            if (arrivals.TryGetValue(batch.Kind, out var arrival))
            {
                arrival.TrySetResult(batch);
                wait = releases[batch.Kind].Task;
            }
        }
        if (wait is not null) await wait.WaitAsync(cancellationToken);
    }

    public async Task AfterCommitBeforeReceiptAsync(WriteBatch batch, CommitReceipt receipt,
        CancellationToken cancellationToken)
    {
        Task? wait = null;
        lock (gate)
        {
            if (committed.TryGetValue(batch.Kind, out var arrival))
            {
                arrival.TrySetResult(receipt);
                wait = receiptReleases[batch.Kind].Task;
            }
        }
        if (wait is not null) await wait.WaitAsync(cancellationToken);
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
