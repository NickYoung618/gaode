using Gaode.Application.Ports;

namespace Gaode.Infrastructure.Persistence;

public sealed class CommitReconciler(ITraceWriter writer, ITraceQuery query)
{
    public async Task<CommitReceipt?> ReconcileAsync(Guid writeId, CancellationToken cancellationToken)
    {
        var receipt = await writer.ReconcileAsync(writeId, cancellationToken);
        if (receipt is not null) return receipt;
        var row = await query.GetWriteAsync(writeId, cancellationToken);
        return row is null ? null : new(writeId, row.RunId, CommitState.Committed,
            row.Revision, Gaode.Domain.Station01.TerminalOutcome.None, null);
    }
}
