using Gaode.Infrastructure.Algorithms;

namespace Gaode.Host.Lifecycle;

public sealed class VirtualWorkerHostedService(WorkerProcessSupervisor worker) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => worker.StartAsync(cancellationToken);
    public async Task StopAsync(CancellationToken cancellationToken) =>
        await worker.RequestStopAsync(cancellationToken);
}
