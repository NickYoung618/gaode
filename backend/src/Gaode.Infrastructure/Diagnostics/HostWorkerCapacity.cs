using Gaode.Diagnostics;

namespace Gaode.Infrastructure.Diagnostics;

// Extracted unchanged from Host startup. Not a recipe budget or device policy.
public static class HostWorkerCapacity
{
    public static (int PreviousMinimum, int EffectiveMinimum, int IoMinimum, bool WindowsNative) Ensure()
    {
        ThreadPool.GetMinThreads(out var previousMinimum, out var ioMinimum);
        ThreadPool.GetMaxThreads(out var workerMaximum, out _);
        var native = ThreadPoolRuntimePolicy.WindowsNative;
        var minimum = native ? previousMinimum : Math.Min(workerMaximum,
            Math.Max(previousMinimum, Math.Max(8, Environment.ProcessorCount + 4)));
        if (minimum > previousMinimum && !ThreadPool.SetMinThreads(minimum, ioMinimum))
            throw new InvalidOperationException("Cannot reserve Host worker capacity for PLC heartbeat continuations.");
        return (previousMinimum, minimum, ioMinimum, native);
    }
}
