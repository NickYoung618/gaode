using Gaode.Application.Ports;
using System.Collections.Concurrent;

namespace Gaode.Infrastructure.Integrations;

public sealed class NotIntegratedAlgorithm : IAlgorithmPort
{
    private readonly ConcurrentDictionary<AlgorithmRole, int> counts = new();
    public int CallCount(AlgorithmRole role) => counts.GetValueOrDefault(role);

    public ValueTask<AlgorithmDispatch> RequestAsync(AlgorithmRequest request,
        Action<AlgorithmEvent> onEvent, CancellationToken cancellationToken)
    {
        counts.AddOrUpdate(request.Role, 1, (_, count) => count + 1);
        throw new AlgorithmNotDispatchedException(request.CallId, "真实算法适配器未接入");
    }
}
