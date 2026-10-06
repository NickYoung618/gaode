using Gaode.Domain.Station01;

namespace Gaode.Contracts.Tests.Support;

// Business fixtures construct business observations; they know no transport or wire codes.
internal static class SemanticDeviceFixture
{
    public static DeviceObservation Ready(ClampState clamp = ClampState.Secured)
    {
        var now = DateTimeOffset.UtcNow;
        return new(DeviceReliability.Reliable, DeviceConnection.Connected, 1, OperatingMode.Automatic,
            DeviceReadiness.Ready, SafetyAssessment.Clear, clamp, MotionAvailability.Available,
            AcquisitionReadiness.Available, ManualAreaState.Clear, ManualHandlingState.Unconfirmed,
            null, null, [], [], new(DeviceProvider.Simulated, "SemanticUnitFixture/1", EvidenceQuality.Derived),
            new(Guid.NewGuid(), 1, now, now, DeviceReliability.Reliable));
    }
}
