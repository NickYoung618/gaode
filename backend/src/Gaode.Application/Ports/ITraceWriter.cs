namespace Gaode.Application.Ports;

public interface ITraceWriter
{
    QueuedWrite SubmitCritical(WriteBatch batch, CancellationToken cancellationToken = default,
        Gaode.Domain.Station01.ActionWindow? window = null);
    Task<CommitReceipt?> ReconcileAsync(Guid writeId, CancellationToken cancellationToken);
}
