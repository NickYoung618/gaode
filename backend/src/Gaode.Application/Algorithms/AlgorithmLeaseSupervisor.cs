using Gaode.Application.Ports;

namespace Gaode.Application.Algorithms;

public sealed class AlgorithmLeaseSupervisor(IMediaStore media)
{
    public IDisposable HoldInputs(IReadOnlyList<MediaRef> inputs, string consumer)
    {
        var leases = new List<IDisposable>();
        try
        {
            foreach (var input in inputs) leases.Add(media.Lease(input.MediaId, consumer));
            return new Group(leases.ToArray());
        }
        catch { foreach (var lease in leases) lease.Dispose(); throw; }
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
