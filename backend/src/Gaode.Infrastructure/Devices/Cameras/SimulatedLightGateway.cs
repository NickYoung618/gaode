using System.Collections.Concurrent;
using Gaode.Domain.Station01;

namespace Gaode.Infrastructure.Devices.Cameras;

public sealed record SimulatedLightState(string Channel, int BrightnessPercent, bool Enabled);

// Consumes the same light commands as a physical adapter. It never claims that
// a physical controller applied them.
public sealed class SimulatedLightGateway : ILightGateway
{
    private readonly ConcurrentDictionary<string, SimulatedLightState> states = new(StringComparer.Ordinal);
    public ComponentExecutionOrigin Origin => new(ComponentEvidenceSource.Simulated,
        typeof(SimulatedLightGateway).Assembly.FullName!, "SimulatedExternalLight/1");
    public SimulatedLightState? State(string binding) => states.GetValueOrDefault(binding);
    public Task SetBrightnessAsync(string binding, string channel, int brightnessPercent, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(binding) || string.IsNullOrWhiteSpace(channel) || brightnessPercent is < 0 or > 100)
            throw new ArgumentException("LightSettingsInvalid");
        states.AddOrUpdate(binding, new SimulatedLightState(channel, brightnessPercent, false), (_, old) => old with { Channel = channel, BrightnessPercent = brightnessPercent });
        return Task.CompletedTask;
    }
    public Task SetAsync(string binding, bool enabled, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!states.TryGetValue(binding, out var old)) throw new InvalidOperationException("LightBrightnessNotConfigured");
        states[binding] = old with { Enabled = enabled };
        return Task.CompletedTask;
    }
}
