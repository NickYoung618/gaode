using Gaode.Application.Motion;
using Gaode.Application.Ports;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Xunit;

namespace Gaode.Rules.Tests.Motion;

public sealed class MotionAdmissionTests
{
    [Fact]
    public void RejectsMissingIntentUnsafeStateAndInvalidOrRealTarget()
    {
        var config = Configuration();
        var ready = Observation();
        Assert.Equal("IntentNotCommitted",
            MotionAdmission.CheckStart(config, ready, false, Guid.Empty));
        Assert.Equal("PlcNotReady", MotionAdmission.CheckStart(config,
            ready with { SafetyAssessment = SafetyAssessment.ExplicitUnsafe }, false, Guid.NewGuid()));
        Assert.Equal("ClampNotSecured", MotionAdmission.CheckMove(config,
            ready with { Clamp = ClampState.Released }, config.Motion.Points.ThreeD, false, Guid.NewGuid()));
        Assert.Equal("MotionTargetInvalid", MotionAdmission.CheckMove(config, ready,
            config.Motion.Points.ThreeD with { X = 999 }, false, Guid.NewGuid()));
        var realBinding = config with { Bindings = [new("plc", "Real", "PLC")] };
        Assert.Equal("TestConfigRealActionDenied",
            MotionAdmission.CheckStart(realBinding, ready, false, Guid.NewGuid()));
    }

    [Fact]
    public void UnknownActionKeepsExclusivePhysicalOwnershipAndCannotBeBlindlyReleased()
    {
        var lease = new ResourceLease();
        var run = Guid.NewGuid();
        var action = Guid.NewGuid();
        Assert.True(lease.TryHold(run));
        Assert.True(lease.TryBeginAction(run, action));
        lease.MarkUnknown(action);
        Assert.True(lease.Unknown);
        Assert.Equal(run, lease.Owner);
        Assert.False(lease.TryBeginAction(run, Guid.NewGuid()));
        Assert.Throws<InvalidOperationException>(() => lease.ReleaseOnlyAfterVerifiedPhysicalClear(run));
    }

    private static DeviceObservation Observation()
    {
        var now = DateTimeOffset.UtcNow;
        return new(DeviceReliability.Reliable, DeviceConnection.Connected, 1, OperatingMode.Automatic,
            DeviceReadiness.Ready, SafetyAssessment.Clear, ClampState.Secured, MotionAvailability.Available,
            AcquisitionReadiness.Available, ManualAreaState.Clear, ManualHandlingState.Unconfirmed,
            null, null, [], [], new(DeviceProvider.Simulated, "SemanticFixture/1", EvidenceQuality.Derived),
            new(Guid.NewGuid(), 1, now, now, DeviceReliability.Reliable));
    }

    private static PublicConfiguration Configuration()
    {
        var point3d = new FixedPoint("3d", "1", 1, 2, "mm", "SIM");
        var pointF = new FixedPoint("f", "1", 3, 4, "mm", "SIM");
        var motion = new MotionConfiguration(new("xy.fixed", "1.0"), ["X", "Y"],
            "SIM", "mm", new(0, 10, 0, 10), new(point3d, pointF));
        var scope = new TrayScope("whole", "1", "WholeTray", "mm", "TRAY", new(0, 1, 0, 1));
        return new("1", "p", "1", "Test", "test",
            [new("plc", "Simulated", "PLC")], motion,
            new(new("capture.whole-tray", "1"), "3d", "l3", scope, new(1, 1), 10),
            new(new("capture.single-frame", "1"), "f", "lf", 1, false, new(1, 1), 10),
            new(new(null, null, null), new(null, null, null)), new(null, null),
            "BeforeRecipeMatching", "NotEvaluated");
    }
}
