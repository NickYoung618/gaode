using Gaode.Application.Ports;
using Gaode.Application.Station01;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Devices.Plc;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Gaode.Communication.Tests.Devices;

public sealed class HeartbeatInterlockTests
{
    [Fact]
    public void TwoThousandNineHundredNinetyNineMillisecondsDoesNotTrip()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 23, 4, 0, 0, TimeSpan.Zero));
        var interlock = new HeartbeatInterlock(clock);
        interlock.Observe(false);

        clock.Advance(TimeSpan.FromMilliseconds(2999));

        var state = interlock.Evaluate();
        Assert.False(state.ActionLocked);
        Assert.False(state.AlarmRaised);
        Assert.False(state.AutomaticContinuationAllowed);
    }

    [Fact]
    public void ThreeSecondsWithoutEdgeLocksActionsRaisesAlarmAndProjectsRestricted()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 23, 4, 0, 0, TimeSpan.Zero));
        var interlock = new HeartbeatInterlock(clock);
        interlock.Observe(false);
        clock.Advance(TimeSpan.FromSeconds(3));

        var state = interlock.Evaluate();
        var observation = ReadyObservation();
        var restriction = PhysicalFaultPolicy.Evaluate(observation,
            state.ActionLocked, recoveryExplicitlyAuthorized: false);

        Assert.True(state.ActionLocked);
        Assert.True(state.AlarmRaised);
        Assert.True(state.RequiresManualRecovery);
        Assert.False(state.AutomaticContinuationAllowed);
        Assert.False(restriction.Allowed);
        Assert.Equal("HeartbeatTimeoutRestricted", restriction.Code);
        var projected = Snapshot().RestrictPhysical(restriction.Code);
        Assert.Equal(RunState.Restricted, projected.State);
        Assert.True(projected.PhysicalAlarmRaised);
        Assert.False(projected.AutomaticContinuationAllowed);
    }

    [Fact]
    public void ReconnectAndResetNeverClearLatchOrResumeAutomatically()
    {
        var clock = new FakeTimeProvider();
        var interlock = new HeartbeatInterlock(clock);
        interlock.Observe(false);
        clock.Advance(TimeSpan.FromSeconds(3));
        Assert.True(interlock.Evaluate().ActionLocked);

        var reconnected = interlock.ObserveReconnect(true);
        var reset = interlock.ObserveReset();

        Assert.True(reconnected.ActionLocked);
        Assert.True(reset.ActionLocked);
        Assert.False(reconnected.AutomaticContinuationAllowed);
        Assert.False(reset.AutomaticContinuationAllowed);
    }

    [Fact]
    public void ManualRecoveryRequiresANewHeartbeatEdgeAndStillRequiresNewCommandAdmission()
    {
        var clock = new FakeTimeProvider();
        var interlock = new HeartbeatInterlock(clock);
        interlock.Observe(false);
        clock.Advance(TimeSpan.FromSeconds(3));
        Assert.True(interlock.Evaluate().ActionLocked);

        Assert.True(interlock.ConfirmManualRecovery(false).ActionLocked);
        var recovered = interlock.ConfirmManualRecovery(true);
        var restriction = PhysicalFaultPolicy.Evaluate(ReadyObservation(),
            recovered.ActionLocked, recoveryExplicitlyAuthorized: true);

        Assert.False(recovered.ActionLocked);
        Assert.False(recovered.AutomaticContinuationAllowed);
        Assert.False(restriction.Allowed);
        Assert.Equal("ManualRestartRequired", restriction.Code);
    }

    private static DeviceObservation ReadyObservation()
    {
        var now = DateTimeOffset.UtcNow;
        return new(DeviceReliability.Reliable, DeviceConnection.Connected, 1, OperatingMode.Automatic,
            DeviceReadiness.Ready, SafetyAssessment.Clear, ClampState.Secured, MotionAvailability.Available,
            AcquisitionReadiness.Available, ManualAreaState.Clear, ManualHandlingState.Unconfirmed,
            null, null, [], [], new(DeviceProvider.Simulated, "SemanticUnitFixture/1", EvidenceQuality.Derived),
            new(Guid.NewGuid(), 1, now, now, DeviceReliability.Reliable));
    }

    private static RunSnapshot Snapshot() => new(Guid.NewGuid(), "request", "actor",
        RunState.Preparing, 1, 1, TerminalOutcome.None, false, ActionState.NotRequested,
        CaptureState.NotRequested, AlgorithmState.NotRequested, SaveState.NotQueued,
        HandoffState.NotReady, null, null, null, []);
}
