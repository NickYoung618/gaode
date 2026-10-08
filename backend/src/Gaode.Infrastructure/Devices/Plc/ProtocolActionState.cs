using Gaode.Domain.Configuration;

namespace Gaode.Infrastructure.Devices.Plc;

// Communication implementation state. Neither these fields nor the handshake context cross a business port.
internal sealed record ProtocolSample(bool Connected, bool Automatic, bool SafetyClear,
    double X, double Y, long ConnectionEpoch, string MotionStatus, DateTimeOffset ObservedUtc,
    double Z = 0, string? Source = null, ushort AlarmBits = 0, ushort AlarmSeverity = 0,
    bool PlcSystemFault = false, bool PlcReady = false, string? DiagnosticCode = null,
    string? FailureOrigin = null, bool ManualZoneOccupied = false)
{
    public Guid ObservationId { get; init; }
    public long SampleConnectionEpoch { get; init; }
    public DateTimeOffset SampleStartedUtc { get; init; }
    public double? ScanZ { get; init; }
    public double? GrabZ { get; init; }
    public Gaode.Domain.Station01.ObservationIdentity? PositionIdentity { get; init; }
    public bool SafetyUnconfirmed { get; init; }
    public bool ManualAreaUnconfirmed { get; init; }
    public IReadOnlyList<string> IndependentSafetyFaults { get; init; } = [];
}
internal sealed record CommunicationCaptureContext(Guid RunId, Guid OperationId,
    long ConnectionEpoch, FixedPoint Target)
{
    public string Role { get; init; } = "3D";
    public bool IsValid => RunId != Guid.Empty && OperationId != Guid.Empty && ConnectionEpoch > 0 &&
        Role is "3D" or "F" or "E" or "Detection" && Target is not null;
}
