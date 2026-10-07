namespace Gaode.Application.Ports;

using Gaode.Application.Station01;

public interface IStageHandoffQuery
{
    Task<PersistedHandoff?> GetHandoffAsync(Guid runId, CancellationToken cancellationToken);
    Task<CommittedPublicPreparationHandoffV2?> GetCommittedV2Async(
        Guid runId, CancellationToken cancellationToken);
}
