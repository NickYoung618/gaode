using Gaode.Application.Ports;
using Gaode.Domain.Station01;

namespace Gaode.Infrastructure.Integrations;

public sealed class NotIntegratedPlcStageActionPort(TimeProvider? clock = null) : IPlcStageActionPort
{
    private readonly TimeProvider clock = clock ?? TimeProvider.System;

    public ValueTask<PlcStageActionResult> ExecuteAsync(PlcStageActionRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new PlcStageActionResult(
            request, StageActionKind.Failed, request.Correlation.ActionId, request.ConnectionEpoch,
            "NotIntegrated", false, false, clock.GetUtcNow(), new(DeviceProvider.Unavailable, null, EvidenceQuality.Unknown), null));
    }
}
