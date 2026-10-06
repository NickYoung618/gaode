namespace Gaode.Application.Ports;

public interface IReservedIntegration
{
    string Name { get; }
    ValueTask<string> StatusAsync(CancellationToken cancellationToken);
}
