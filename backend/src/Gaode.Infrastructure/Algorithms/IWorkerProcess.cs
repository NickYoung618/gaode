namespace Gaode.Infrastructure.Algorithms;

public interface IWorkerProcess : IAsyncDisposable
{
    Guid SessionId { get; }
    bool HasExited { get; }
    Task ExitTask { get; }
    Task StartAsync(CancellationToken cancellationToken = default);
    Task SendAsync(WorkerMessage message, CancellationToken cancellationToken = default);
    Task<string?> ReadLineAsync(CancellationToken cancellationToken = default);
    Task RequestStopAsync(CancellationToken cancellationToken = default);
}
