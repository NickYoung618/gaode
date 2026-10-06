using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;

namespace Gaode.Infrastructure.Simulation;

public sealed class SimulatedDeviceState(SimDeviceInitial initial, TimeProvider clock)
{
    private readonly object gate = new();
    private bool connected = initial.Connected, automatic = initial.Automatic, safety = initial.SafetyClear;
    private ClampState clamp = initial.Clamped ? ClampState.Secured : ClampState.Released;
    private MotionAvailability motion = MotionAvailability.Available;
    private AcquisitionReadiness acquisition = AcquisitionReadiness.Available;
    private double x = initial.X, y = initial.Y, z, scanZ, grabZ;
    private long epoch = 1, heartbeats;
    private DateTimeOffset observed = clock.GetUtcNow();
    private Guid observationId = Guid.NewGuid();
    private bool clampFailed;
    public long HeartbeatCount { get { lock (gate) return heartbeats; } }
    public static ExecutionOrigin Origin { get; } = new(DeviceProvider.Simulated, "FullSimulation/semantic-009", EvidenceQuality.Derived);
    public DeviceObservation Observe()
    {
        lock (gate)
        {
            var reliable = connected ? DeviceReliability.Reliable : DeviceReliability.Unavailable;
            var identity = new ObservationIdentity(observationId, epoch, observed, observed, reliable);
            return new(reliable, connected ? DeviceConnection.Connected : DeviceConnection.Disconnected, epoch,
                automatic ? OperatingMode.Automatic : OperatingMode.NonAutomatic, connected ? DeviceReadiness.Ready : DeviceReadiness.Unconfirmed,
                safety ? SafetyAssessment.Clear : SafetyAssessment.ExplicitUnsafe, clamp, motion, acquisition,
                ManualAreaState.Clear, ManualHandlingState.Unconfirmed,
                new(x, y, z, "SimulatedMachineAxes", "SIM_MACHINE", "FullSimulation:mm", identity, Origin),
                null, [], clampFailed ? ["ClampFailed"] : [], Origin, identity)
                { AxisPositions = new(x, y, z, scanZ, grabZ) };
        }
    }
    public void SetClamp(ClampState value) { lock (gate) { clamp = value; Stamp(); } }
    public void FailClamp() { lock (gate) { clamp = ClampState.Unconfirmed; clampFailed = true; Stamp(); } }
    public void MoveTo(double targetX, double targetY, double targetZ, string axisPurpose)
    {
        lock (gate)
        {
            x = targetX; y = targetY;
            switch (axisPurpose)
            {
                case "XY": break;
                case "DetectionZ": z = targetZ; break;
                case "ScanZ": scanZ = targetZ; break;
                case "GrabZ": grabZ = targetZ; break;
                default: throw new ArgumentException("Unknown axis purpose", nameof(axisPurpose));
            }
            motion = MotionAvailability.Available; Stamp();
        }
    }
    public void SetMotion(MotionAvailability value) { lock (gate) { motion = value; Stamp(); } }
    public void SetAcquisition(AcquisitionReadiness value) { lock (gate) { acquisition = value; Stamp(); } }
    public void SetConnected(bool value) { lock (gate) { connected = value; if (!value) epoch++; Stamp(); } }
    public void PulseHeartbeat() { lock (gate) { if (connected) { heartbeats++; Stamp(); } } }
    public void Reset()
    {
        lock (gate) { connected = automatic = safety = true; clamp = ClampState.Released; clampFailed = false;
            x = y = z = scanZ = grabZ = 0; epoch++; motion = MotionAvailability.Available; acquisition = AcquisitionReadiness.Available; Stamp(); }
    }
    private void Stamp() { observed = clock.GetUtcNow(); observationId = Guid.NewGuid(); }
}
