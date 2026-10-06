namespace Gaode.Application.Ports;

public interface ITraceQuery
{
    Task<PersistedRun?> GetRunAsync(Guid runId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PersistedWrite>> GetWritesAsync(Guid runId, CancellationToken cancellationToken);
    Task<PersistedWrite?> GetWriteAsync(Guid writeId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PersistedRun>> GetUnfinishedRunsAsync(CancellationToken cancellationToken);
}
