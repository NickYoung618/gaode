using Gaode.Application.Ports;

namespace Gaode.Infrastructure.Integrations;

public sealed record NotIntegratedPort(string Name) : IReservedIntegration
{
    public ValueTask<string> StatusAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult("NotIntegrated");
}
