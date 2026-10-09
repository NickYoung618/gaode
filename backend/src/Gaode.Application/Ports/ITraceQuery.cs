namespace Gaode.Application.Ports;

public interface ITraceQuery
{
    Task<Gaode.Application.Station01.StartReceipt?> GetStartReceiptAsync(string subject, string requestId, CancellationToken cancellationToken);
    Task<PersistedRun?> GetRunAsync(Guid runId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PersistedWrite>> GetWritesAsync(Guid runId, CancellationToken cancellationToken);
    Task<PersistedWrite?> GetWriteAsync(Guid writeId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PersistedRun>> GetUnfinishedRunsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AlgorithmResourceState>> GetUnreclaimedResourcesAsync(int offset, int limit,
        CancellationToken cancellationToken) => throw new NotSupportedException("AlgorithmResourceQueryNotIntegrated");
}
