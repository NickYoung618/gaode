using Gaode.Domain.Station01;

namespace Gaode.Application.Ports;
public interface IPlcResetPort
{
    Task ResetAsync(CancellationToken cancellationToken);
    Task<InitialReadinessAssessment> ReadInitialStateAsync(CancellationToken cancellationToken);
}
