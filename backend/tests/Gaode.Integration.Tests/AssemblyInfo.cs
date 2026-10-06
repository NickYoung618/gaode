using System.Runtime.CompilerServices;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

internal static class IntegrationTestRuntime
{
    [ModuleInitializer]
    internal static void ConfigureThreadPool()
    {
        ThreadPool.GetMinThreads(out var workerThreads, out var completionPortThreads);
        ThreadPool.SetMinThreads(Math.Max(workerThreads, 16),
            Math.Max(completionPortThreads, 16));
    }
}
