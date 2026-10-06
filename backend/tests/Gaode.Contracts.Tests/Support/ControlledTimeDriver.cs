using Microsoft.Extensions.Time.Testing;

namespace Gaode.Contracts.Tests.Support;

public sealed class ControlledTimeDriver
{
    public FakeTimeProvider Clock { get; } = new();

    public async Task AdvanceAndDrainAsync(TimeSpan amount)
    {
        Clock.Advance(amount);
        for (var i = 0; i < 8; i++) await Task.Yield();
    }

    public async Task DrainReadyAsync()
    {
        for (var i = 0; i < 8; i++) await Task.Yield();
    }
}
