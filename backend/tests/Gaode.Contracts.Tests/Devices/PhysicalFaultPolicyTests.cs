using Gaode.Application.Station01;
using Gaode.Contracts.Tests.Support;
using Gaode.Domain.Station01;
using Xunit;

namespace Gaode.Contracts.Tests.Devices;

public sealed class PhysicalFaultPolicyTests
{
    [Fact]
    public void CommunicationRestrictionPreservesPhysicalHoldAndDoesNotResumeAutomatically()
    {
        var restriction = PhysicalFaultPolicy.Evaluate(SemanticDeviceFixture.Ready(),
            heartbeatLocked: true, recoveryExplicitlyAuthorized: false);
        Assert.False(restriction.Allowed);
        Assert.Equal("HeartbeatTimeoutRestricted", restriction.Code);
        var snapshot = new RunSnapshot(Guid.NewGuid(), "request", "actor", RunState.Preparing,
            1, 1, TerminalOutcome.None, false, ActionState.NotRequested, CaptureState.NotRequested,
            AlgorithmState.NotRequested, SaveState.NotQueued, HandoffState.NotReady, null, null, null, []);
        var held = snapshot.RestrictPhysical(restriction.Code);
        Assert.Equal(RunState.Restricted, held.State);
        Assert.True(held.PhysicalAlarmRaised);
        Assert.False(held.AutomaticContinuationAllowed);
    }

    [Fact]
    public void RecoveryRequiresANewCommandEvenAfterDeviceRestrictionClears()
    {
        var restriction = PhysicalFaultPolicy.Evaluate(SemanticDeviceFixture.Ready(),
            heartbeatLocked: false, recoveryExplicitlyAuthorized: true);
        Assert.False(restriction.Allowed);
        Assert.Equal("ManualRestartRequired", restriction.Code);
    }
}
