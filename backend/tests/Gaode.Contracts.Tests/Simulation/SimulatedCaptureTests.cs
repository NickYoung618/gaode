using Gaode.Application.Ports;
using Gaode.Contracts.Tests.Support;
using Gaode.Infrastructure.Simulation;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Gaode.Contracts.Tests.Simulation;

public sealed class SimulatedCaptureTests
{
    [Fact]
    public async Task FProducesOneEndedSingleFrameAndCannotRetrigger()
    {
        var (_, budget, profile) = TestConfiguration.Normal();
        var clock = new FakeTimeProvider(profile.VirtualStartUtc);
        var capture = new SimulatedCapture(profile,
            new SimulationEventScheduler(clock, budget.Limits.MaxPendingTimerEvents));
        var request = Request();
        var events = new List<CaptureEvent>();
        await capture.RequestCaptureAsync(request, events.Add, CancellationToken.None);
        clock.Advance(TimeSpan.FromMilliseconds(profile.Stages.CaptureF.DelayMs));
        for (var i = 0; i < 8; i++) await Task.Yield();
        Assert.Single(events, x => x.Kind == CaptureEventKind.Ended);
        Assert.Single(events, x => x.Kind == CaptureEventKind.MediaTaken);
        Assert.NotEmpty(events.Single(x => x.Kind == CaptureEventKind.MediaTaken).Buffer!);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await capture.RequestCaptureAsync(Request(), _ => { }, CancellationToken.None));
    }

    [Fact]
    public async Task MissingCommittedIntentIsRejectedBeforeAnyCaptureEvent()
    {
        var (_, budget, profile) = TestConfiguration.Normal();
        var capture = new SimulatedCapture(profile,
            new SimulationEventScheduler(new FakeTimeProvider(), budget.Limits.MaxPendingTimerEvents));
        var events = new List<CaptureEvent>();
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await capture.RequestCaptureAsync(Request() with { IntentWriteId = Guid.Empty },
                events.Add, CancellationToken.None));
        Assert.Empty(events);
        Assert.Equal(0, capture.TriggerCount(CaptureRole.F));
    }

    private static CaptureRequest Request() => new(
        new(Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(), "snapshot", "1.0.0", "Test", 1, 100000, "clock"),
        Guid.NewGuid(), CaptureRole.F, "F", "1", null, null, "camera", "light", Guid.NewGuid(), 4096);
}
