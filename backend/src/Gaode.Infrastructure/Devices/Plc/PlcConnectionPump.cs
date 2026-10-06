namespace Gaode.Infrastructure.Devices.Plc;

public sealed record HeartbeatInterlockState(
    bool ActionLocked,
    bool AlarmRaised,
    bool RequiresManualRecovery,
    bool AutomaticContinuationAllowed,
    string Code,
    DateTimeOffset? LastValidEdgeAt);

public sealed class HeartbeatInterlock(TimeProvider clock, TimeSpan timeout)
{
    private DateTimeOffset? lastEdgeAt;
    private bool? lastValue;
    private bool latched;

    public HeartbeatInterlock(TimeProvider clock) : this(clock, TimeSpan.FromSeconds(3)) { }

    public void Observe(bool value)
    {
        if (lastValue is null || lastValue != value)
        {
            lastValue = value;
            lastEdgeAt = clock.GetUtcNow();
        }
    }

    public HeartbeatInterlockState Evaluate()
    {
        if (lastEdgeAt is null || clock.GetUtcNow() - lastEdgeAt.Value >= timeout)
            latched = true;
        return latched
            ? new(true, true, true, false, "HeartbeatTimeoutRestricted", lastEdgeAt)
            : new(false, false, false, false, "HeartbeatValid", lastEdgeAt);
    }

    public HeartbeatInterlockState ObserveReconnect(bool value)
    {
        Observe(value);
        return Evaluate();
    }

    public HeartbeatInterlockState ObserveReset() => Evaluate();

    public HeartbeatInterlockState ConfirmManualRecovery(bool currentHeartbeat)
    {
        if (lastEdgeAt is null || lastValue == currentHeartbeat)
            return Evaluate();
        Observe(currentHeartbeat);
        latched = false;
        return Evaluate();
    }
}

// Formal polling and connection ownership are implemented by LatestProtocolPlcDevice.
// The unused raw-array pump was removed during 009; this file retains the existing
// independently tested finite heartbeat policy without a second transport entry.
