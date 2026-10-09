using Gaode.Application.Ports;
using Gaode.Contracts.Tests.Support;
using Gaode.Infrastructure.Simulation;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using Gaode.Domain.Station01;

namespace Gaode.Contracts.Tests.Simulation;

public sealed class SimulatedPlcTests
{
    [Fact]
    public async Task StartConfirmsReadinessWithoutInventingClampOrHostButtonEvents()
    {
        var (_, budget, profile) = TestConfiguration.Normal();
        var clock = new FakeTimeProvider(profile.VirtualStartUtc);
        using var plc = new SimulatedPlc(profile,
            new SimulationEventScheduler(clock, budget.Limits.MaxPendingTimerEvents));
        var events = new List<DeviceEventKind>();
        var envelope = Envelope(clock);
        await plc.RequestStartAsync(envelope, Guid.NewGuid(), Guid.NewGuid(),
            e => events.Add(e.Kind), CancellationToken.None);
        clock.Advance(TimeSpan.FromMilliseconds(profile.Stages.PlcAcceptance.DelayMs));
        await Drain();
        Assert.Contains(DeviceEventKind.Accepted, events);
        Assert.Equal(ClampState.Released, plc.Observe().Clamp);
        clock.Advance(TimeSpan.FromMilliseconds(profile.Stages.ClampCompletion.DelayMs));
        await Drain();
        Assert.Equal(ClampState.Released, plc.Observe().Clamp);
        Assert.Equal(DeviceReadiness.Ready, plc.Observe().Readiness);
        Assert.DoesNotContain(DeviceEventKind.ButtonPressed, events);
        Assert.DoesNotContain(DeviceEventKind.ClampStarted, events);
        Assert.DoesNotContain(DeviceEventKind.ClampCompleted, events);
    }

    [Fact]
    public async Task StartReadinessDoesNotInventZoneConfiguration()
    {
        await using var harness = Station01StepHarness.Create();
        await harness.CompleteStartAsync();
        Assert.Equal(DeviceReadiness.Ready, harness.Plc.Observe().Readiness);
        Assert.Equal(ClampState.Released, harness.Plc.Observe().Clamp);
        Assert.False(harness.Motion.Unknown);
        Assert.Equal(0, harness.Plc.ZoneConfigurations);
    }

    [Fact]
    public async Task UnusedClampProfileCannotOverrideReadinessObservation()
    {
        var (_, budget, source) = TestConfiguration.Normal();
        var profile = source with { Stages = source.Stages with
        {
            ClampCompletion = source.Stages.ClampCompletion with { Strategy = "Fail", Outcome = "Failure" }
        }};
        var clock = new FakeTimeProvider(profile.VirtualStartUtc);
        using var plc = new SimulatedPlc(profile,
            new SimulationEventScheduler(clock, budget.Limits.MaxPendingTimerEvents));
        await plc.RequestStartAsync(Envelope(clock), Guid.NewGuid(), Guid.NewGuid(), _ => { }, CancellationToken.None);
        clock.Advance(TimeSpan.FromMilliseconds(profile.Stages.PlcAcceptance.DelayMs));
        await Drain();
        clock.Advance(TimeSpan.FromMilliseconds(profile.Stages.ClampCompletion.DelayMs));
        await Drain();
        Assert.Equal(ClampState.Released, plc.Observe().Clamp);
        Assert.Equal(DeviceReadiness.Ready, plc.Observe().Readiness);
        Assert.DoesNotContain("ClampFailed", plc.Observe().ReasonCodes);
        Assert.Equal(0, plc.MoveCommands);
    }

    [Fact]
    public async Task HeartbeatAndStopProgressIndependentlyFromPendingBusinessAction()
    {
        var (_, budget, profile) = TestConfiguration.Normal();
        var clock = new FakeTimeProvider(profile.VirtualStartUtc);
        using var plc = new SimulatedPlc(profile,
            new SimulationEventScheduler(clock, budget.Limits.MaxPendingTimerEvents),
            budget.BusinessMs.HeartbeatFlip);
        var initialHeartbeat = plc.HeartbeatCount;
        await plc.RequestStartAsync(Envelope(clock), Guid.NewGuid(), Guid.NewGuid(), _ => { },
            CancellationToken.None);
        clock.Advance(TimeSpan.FromMilliseconds(budget.BusinessMs.HeartbeatFlip));
        await Drain();
        Assert.True(plc.HeartbeatCount > initialHeartbeat);
        Assert.Equal(1, plc.StartCommands);

        var stopEvents = new List<DeviceEventKind>();
        await plc.RequestStopAsync(Envelope(clock), e => stopEvents.Add(e.Kind), CancellationToken.None);
        clock.Advance(TimeSpan.FromMilliseconds(profile.Stop.AcceptedDelayMs));
        await Drain();
        Assert.Contains(DeviceEventKind.Accepted, stopEvents);
        Assert.DoesNotContain(DeviceEventKind.Stopped, stopEvents);
        clock.Advance(TimeSpan.FromMilliseconds(profile.Stop.CompletedDelayMs));
        await Drain();
        Assert.Contains(DeviceEventKind.Stopped, stopEvents);
        Assert.Equal(MotionAvailability.HeldUnknown, plc.Observe().MotionAvailability);
    }

    [Fact]
    public void HeartbeatPauseDisconnectsAndLeavesPalletStatusHeld()
    {
        var (_, budget, profile) = TestConfiguration.Normal();
        var clock = new FakeTimeProvider(profile.VirtualStartUtc);
        using var plc = new SimulatedPlc(profile,
            new SimulationEventScheduler(clock, budget.Limits.MaxPendingTimerEvents));
        plc.PauseHeartbeat();
        clock.Advance(TimeSpan.FromSeconds(3.1));
        Assert.False(plc.Observe().HasReliableObservation);
        Assert.Equal(DeviceConnection.Disconnected, plc.Observe().Connection);
        Assert.Equal(ClampState.Released, plc.Observe().Clamp);
    }

    [Fact]
    public async Task InspectionHandshakeRunsOneWayAndClearsOnlyAfterResetSuccess()
    {
        await using var h = Station01StepHarness.Create();
        await h.CompleteStartAsync();
        var tick = h.Clock.GetTimestamp();
        var envelope = new PortEnvelope(h.Run.RunId, Guid.NewGuid(), 1, h.Run.SessionId,
            h.Config.SnapshotId, h.Config.Public.Version, "Test", tick, tick + h.Clock.TimestampFrequency * 5, h.Run.ClockId);
        var request = new MoveRequest(envelope, Guid.NewGuid(),
            new("sim", "1.0.0", 100, 100, "mm", "SIM", 150), Guid.NewGuid(), "sim-plc", "3D");
        var completed = new TaskCompletionSource<DeviceEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        await h.Plc.RequestMoveAsync(request, e => { if (e.Kind is DeviceEventKind.Completed or DeviceEventKind.UnknownHeld) completed.TrySetResult(e); }, default);
        var moved = await h.DriveAsync(completed.Task);
        Assert.Equal(DeviceEventKind.Completed, moved.Kind);
        Assert.Equal(100d, h.Plc.Observe().Position!.ActualX);
        var evidence = Assert.IsType<DeviceActionEvidence>(moved.Evidence);
        var position = Assert.Single(evidence.Positions);
        var window = new ActionWindow(envelope.StartTick, envelope.DueTick, envelope.ClockId,
            h.Clock.GetUtcNow(), h.Clock.GetUtcNow().AddSeconds(5));
        var capture = await h.DriveAsync(h.Plc.OpenCaptureWindowAsync(new(evidence.Correlation, CaptureRole.ThreeD,
            request.Target, position, window), default).AsTask());
        Assert.Equal(AcquisitionState.CaptureAllowed, capture.State);
        using var saved = await Gaode.Communication.Tests.CaptureWorkFixture.CreateAsync(capture);
        var release = await h.DriveAsync(h.Plc.FinishCaptureWindowAsync(capture, saved.Work, window, default));
        Assert.Equal(AcquisitionState.Released, release.State);
        Assert.Equal(new[] { AcquisitionState.CaptureAllowed, AcquisitionState.Released }, h.Plc.AcquisitionTransitions);
        Assert.Equal(AcquisitionReadiness.Available, h.Plc.Observe().AcquisitionReadiness);
        Assert.True(release.Evidence!.IsCorrelated);
    }

    private static PortEnvelope Envelope(TimeProvider clock) => new(Guid.NewGuid(), Guid.NewGuid(), 1,
        Guid.NewGuid(), "snapshot", "1.0.0", "Test", clock.GetTimestamp(),
        clock.GetTimestamp() + clock.TimestampFrequency * 5, "clock");
    private static async Task Drain() { for (var i = 0; i < 8; i++) await Task.Yield(); }
}
