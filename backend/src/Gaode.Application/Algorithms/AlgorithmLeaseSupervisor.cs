using Gaode.Application.Ports;

namespace Gaode.Application.Algorithms;

public sealed class AlgorithmLeaseSupervisor(IMediaStore media)
{
    public IDisposable HoldInputs(IReadOnlyList<MediaRef> inputs, string consumer)
    {
        var leases = inputs.Select(x => media.Lease(x.MediaId, consumer)).ToArray();
        return new Group(leases);
    }

    private sealed class Group(IDisposable[] leases) : IDisposable
    {
        private int _done;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _done, 1) == 0)
                foreach (var lease in leases) lease.Dispose();
        }
    }
}
