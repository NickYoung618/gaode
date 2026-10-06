using System.Threading.Channels;
using Gaode.Domain.Station01;

namespace Gaode.Application.Station01;

public sealed class FlowMailbox(int normalCapacity, int controlCapacity, int terminalCapacity)
{
    private readonly Channel<Mutation> _normal = Bounded(normalCapacity);
    private readonly Channel<Mutation> _control = Bounded(controlCapacity);
    private readonly Channel<Mutation> _terminal = Bounded(terminalCapacity);
    private readonly SemaphoreSlim _ready = new(0);

    private static Channel<Mutation> Bounded(int capacity) => Channel.CreateBounded<Mutation>(
        new BoundedChannelOptions(capacity) { SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait });

    public Task<RunSnapshot> Post(Guid runId, Func<RunSnapshot, RunSnapshot> apply,
        bool terminal = false, bool control = false)
    {
        var mutation = new Mutation(runId, apply, new(TaskCreationOptions.RunContinuationsAsynchronously));
        var target = terminal ? _terminal : control ? _control : _normal;
        if (!target.Writer.TryWrite(mutation)) throw new InvalidOperationException("流程事件容量不足");
        _ready.Release();
        return mutation.Completion.Task;
    }

    public async Task DrainAsync(Func<Guid, Func<RunSnapshot, RunSnapshot>, RunSnapshot> apply,
        CancellationToken cancellationToken)
    {
        try
        {
        while (!cancellationToken.IsCancellationRequested)
        {
            await _ready.WaitAsync(cancellationToken);
            if (!_control.Reader.TryRead(out var item) && !_terminal.Reader.TryRead(out item) &&
                !_normal.Reader.TryRead(out item)) continue;
            try { item.Completion.TrySetResult(apply(item.RunId, item.Apply)); }
            catch (Exception error) { item.Completion.TrySetException(error); }
        }
        }
        finally
        {
            foreach (var channel in new[] { _normal, _control, _terminal })
            {
                channel.Writer.TryComplete();
                while (channel.Reader.TryRead(out var pending))
                    pending.Completion.TrySetCanceled();
            }
        }
    }

    private sealed record Mutation(Guid RunId, Func<RunSnapshot, RunSnapshot> Apply,
        TaskCompletionSource<RunSnapshot> Completion);
}
