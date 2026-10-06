using Gaode.Application.Ports;
using Gaode.Contracts.Tests.Support;
using Gaode.Infrastructure.Simulation;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Gaode.Contracts.Tests.Simulation;

public sealed class SimulatedAlgorithmTests
{
    [Fact]
    public async Task CancellationEndsTrackedWorkerWithoutPublishingResult()
    {
        var (_, budget, profile) = TestConfiguration.Normal();
        var clock = new FakeTimeProvider(profile.VirtualStartUtc);
        var algorithm = new SimulatedAlgorithm(profile,
            new SimulationEventScheduler(clock, budget.Limits.MaxPendingTimerEvents));
        using var cancellation = new CancellationTokenSource();
        var events = new List<AlgorithmEvent>();
        var dispatch = await algorithm.RequestAsync(Request(), events.Add, cancellation.Token);
        await cancellation.CancelAsync();
        for (var i = 0; i < 8; i++) await Task.Yield();
        await dispatch.Exited;
        Assert.DoesNotContain(events, x => x.Kind == AlgorithmEventKind.Result);
        Assert.Contains(events, x => x.Kind == AlgorithmEventKind.Accepted);
    }

    [Fact]
    public async Task NoResponseHasFiniteTrackedExitAndNoSyntheticTimeout()
    {
        var (_, budget, normal) = TestConfiguration.Normal();
        var profile = normal with { Stages = normal.Stages with
        { FDecode = normal.Stages.FDecode with { Strategy = "NoResponse" } } };
        var algorithm = new SimulatedAlgorithm(profile,
            new SimulationEventScheduler(new FakeTimeProvider(), budget.Limits.MaxPendingTimerEvents));
        var events = new List<AlgorithmEvent>();
        var dispatch = await algorithm.RequestAsync(Request(), events.Add, CancellationToken.None);
        Assert.True(dispatch.Exited.IsCompletedSuccessfully);
        Assert.DoesNotContain(events, x => x.Kind is AlgorithmEventKind.Result or AlgorithmEventKind.Failed);
    }

    [Fact]
    public async Task DecodePreservesRawValuesAndUnintegratedRolesCannotPublishEmptyResults()
    {
        var (_, budget, profile) = TestConfiguration.Normal();
        var clock = new FakeTimeProvider(profile.VirtualStartUtc);
        var algorithm = new SimulatedAlgorithm(profile,
            new SimulationEventScheduler(clock, budget.Limits.MaxPendingTimerEvents));
        var events = new List<AlgorithmEvent>();
        foreach (var role in new[] { AlgorithmRole.Height, AlgorithmRole.TrayPose })
        {
            var rejected = Assert.Throws<AlgorithmNotDispatchedException>(() =>
                algorithm.RequestAsync(Request(role), events.Add, CancellationToken.None));
            Assert.Equal("SimulatedAlgorithmCapabilityNotIntegrated", rejected.Evidence);
            Assert.Equal(0, algorithm.CallCount(role));
        }
        Assert.Empty(events);
        var dispatch = await algorithm.RequestAsync(Request(), events.Add, CancellationToken.None);
        clock.Advance(TimeSpan.FromMilliseconds(profile.Stages.FDecode.DelayMs));
        await dispatch.Exited;
        var decode = Assert.Single(events, x => x.Kind == AlgorithmEventKind.Result);
        Assert.Equal(profile.Fixtures.RawCodes, decode.RawCodes);
        Assert.Contains(events, x => x.Kind == AlgorithmEventKind.InputReleased);
        Assert.Equal(1, algorithm.CallCount(AlgorithmRole.FDecode));
    }

    private static AlgorithmRequest Request(AlgorithmRole role = AlgorithmRole.FDecode)
    {
        var run = Guid.NewGuid();
        var capture = Guid.NewGuid();
        var envelope = new PortEnvelope(run, Guid.NewGuid(), 1, Guid.NewGuid(),
            "snapshot", "1.0.0", "Test", 1, 100000, "clock");
        var media = new MediaRef(Guid.NewGuid(), run, capture,
            role == AlgorithmRole.Height ? "PointCloud" : "Image", "media/test.img",
            1, role == AlgorithmRole.Height ? "bin" : "img", "SyntheticFixture",
            role == AlgorithmRole.Height ? "whole/1" : "NotApplicable", "1", "FileCompleted");
        return new(envelope, Guid.NewGuid(), capture, role, [media],
            "1", role == AlgorithmRole.Height ? "algorithm.height" : "algorithm.f-decode",
            "1", Guid.NewGuid(), "CommittedMedia");
    }
}
