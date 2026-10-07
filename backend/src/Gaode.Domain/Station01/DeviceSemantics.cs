using System.Text.Json.Serialization;
using Gaode.Domain.Configuration;

namespace Gaode.Domain.Station01;

// These categories describe decisions made by the station. No member is assigned a wire value.
[JsonConverter(typeof(JsonStringEnumConverter<DeviceReliability>))]
public enum DeviceReliability { Unavailable, Stale, Reliable }
[JsonConverter(typeof(JsonStringEnumConverter<DeviceConnection>))]
public enum DeviceConnection { Disconnected, Connected }
[JsonConverter(typeof(JsonStringEnumConverter<OperatingMode>))]
public enum OperatingMode { Unconfirmed, NonAutomatic, Automatic }
[JsonConverter(typeof(JsonStringEnumConverter<DeviceReadiness>))]
public enum DeviceReadiness { Unconfirmed, NotReady, Ready }
[JsonConverter(typeof(JsonStringEnumConverter<SafetyAssessment>))]
public enum SafetyAssessment { Unconfirmed, ExplicitUnsafe, Clear }
[JsonConverter(typeof(JsonStringEnumConverter<ClampState>))]
public enum ClampState { Unconfirmed, Released, Secured }
[JsonConverter(typeof(JsonStringEnumConverter<MotionAvailability>))]
public enum MotionAvailability { HeldUnknown, InUse, Available }
[JsonConverter(typeof(JsonStringEnumConverter<AcquisitionReadiness>))]
public enum AcquisitionReadiness { Unconfirmed, Unavailable, Available }
[JsonConverter(typeof(JsonStringEnumConverter<ManualAreaState>))]
public enum ManualAreaState { Unconfirmed, Occupied, Clear }
[JsonConverter(typeof(JsonStringEnumConverter<ManualHandlingState>))]
public enum ManualHandlingState { Unconfirmed, Waiting, Confirmed }
[JsonConverter(typeof(JsonStringEnumConverter<DeviceProvider>))]
public enum DeviceProvider { Unavailable, Real, Virtual, Simulated }
[JsonConverter(typeof(JsonStringEnumConverter<EvidenceQuality>))]
public enum EvidenceQuality { Unknown, Measured, Derived, Degraded }
[JsonConverter(typeof(JsonStringEnumConverter<AlarmLevel>))]
public enum AlarmLevel { Unknown, Warning, Fault, Critical }
[JsonConverter(typeof(JsonStringEnumConverter<FaceSource>))]
public enum FaceSource { Unconfirmed, DeviceObserved, CommandDefaultManualConfirmed }
[JsonConverter(typeof(JsonStringEnumConverter<DeviceCompletionMeaning>))]
public enum DeviceCompletionMeaning
{
    RequestSubmitted, PositionReached, RegionsPrepared, CaptureAllowed, CaptureReleased,
    FaceEstablished, MaterialPicked, MaterialTransferred, UnloadPrepared, Unlocked, ResetObserved,
    DeviceRecipeApplied, FlipCompleted, PutBackCompleted
}

public sealed record ExecutionOrigin(DeviceProvider Provider, string? ComponentVersion, EvidenceQuality Quality);

// This is an opaque durable identity. Possessing it is not permission to perform an action.
public sealed record DiagnosticEvidenceReference(Guid StoreId, Guid EvidenceId)
{
    public string SchemaVersion => "plc-evidence/1";
    public bool IsValid => StoreId != Guid.Empty && EvidenceId != Guid.Empty;
}

public sealed record ActionCorrelation(Guid RunId, Guid OperationId, Guid ActionId, int Attempt,
    Guid SessionId, long ConnectionEpoch, string SnapshotId, string? PlanRevision = null,
    Guid? TrayId = null, string? ObjectId = null, string? GroupId = null, string? AssemblyId = null,
    int? LocalFace = null, int? PhysicalSlotIndex = null)
{
    public bool IsValid => RunId != Guid.Empty && OperationId != Guid.Empty && ActionId != Guid.Empty &&
        Attempt > 0 && SessionId != Guid.Empty && ConnectionEpoch > 0 && !string.IsNullOrWhiteSpace(SnapshotId) &&
        (LocalFace is null or > 0) && (PhysicalSlotIndex is null or > 0);
}

public sealed record ActionWindow(long StartTick, long DueTick, string ClockId,
    DateTimeOffset StartedUtc, DateTimeOffset DeadlineUtc)
{
    public bool IsValid => DueTick > StartTick && DeadlineUtc > StartedUtc && !string.IsNullOrWhiteSpace(ClockId);
    public bool Contains(long receivedTick) => receivedTick >= StartTick && receivedTick < DueTick;
}

public sealed record ObservationIdentity(Guid ObservationId, long ConnectionEpoch,
    DateTimeOffset SampleStartedUtc, DateTimeOffset SampleEndedUtc, DeviceReliability Reliability)
{
    public bool IsValid => ObservationId != Guid.Empty && ConnectionEpoch > 0 && SampleEndedUtc >= SampleStartedUtc;
}

public sealed record PositionObservation(double? ActualX, double? ActualY, double? ActualZ,
    string? AxisPurpose, string? CoordinateFrame, string? UnitBasis,
    ObservationIdentity Identity, ExecutionOrigin Origin)
{
    public bool IsReliable => Identity.IsValid && Identity.Reliability == DeviceReliability.Reliable &&
        ActualX is { } x && ActualY is { } y && double.IsFinite(x) && double.IsFinite(y) &&
        (AxisPurpose == "XY" ? ActualZ is null : ActualZ is { } z && double.IsFinite(z)) &&
        !string.IsNullOrWhiteSpace(AxisPurpose) && !string.IsNullOrWhiteSpace(CoordinateFrame);
}

public sealed record AxisPositionSet(double? X, double? Y, double? DetectionZ, double? ScanZ, double? GrabZ);

public sealed record FaceObservation(int? ActualFace, FaceSource Source,
    ActionCorrelation Correlation, ObservationIdentity Identity);
public sealed record AlarmAssessment(string Name, AlarmLevel Severity, DeviceReliability Reliability);

public sealed record DeviceObservation(DeviceReliability Reliability, DeviceConnection Connection,
    long ConnectionEpoch, OperatingMode OperatingMode, DeviceReadiness Readiness,
    SafetyAssessment SafetyAssessment, ClampState Clamp, MotionAvailability MotionAvailability,
    AcquisitionReadiness AcquisitionReadiness, ManualAreaState ManualArea, ManualHandlingState ManualHandling,
    PositionObservation? Position, FaceObservation? Face, IReadOnlyList<AlarmAssessment>? Alarms,
    IReadOnlyList<string> ReasonCodes, ExecutionOrigin ExecutionOrigin, ObservationIdentity? Identity,
    DiagnosticEvidenceReference? DiagnosticEvidenceReference = null)
{
    public string SchemaVersion => "device-semantics/1";
    public AxisPositionSet? AxisPositions { get; init; }
    public PositionObservation? PositionForPurpose(string purpose)
    {
        if (Position is null || AxisPositions is not { } axes) return null;
        double? z = purpose switch
        {
            "XY" => null, "DetectionZ" => axes.DetectionZ,
            "ScanZ" => axes.ScanZ, "GrabZ" => axes.GrabZ,
            _ => throw new ArgumentException("PositionAxisPurposeUnknown", nameof(purpose))
        };
        return Position with { ActualX = axes.X, ActualY = axes.Y, ActualZ = z, AxisPurpose = purpose };
    }
    public bool HasReliableObservation => Reliability == DeviceReliability.Reliable &&
        Connection == DeviceConnection.Connected && Identity is { IsValid: true } &&
        Identity.ConnectionEpoch == ConnectionEpoch && Identity.Reliability == DeviceReliability.Reliable;
}

public sealed record PositionReachedEvidence(ActionCorrelation Correlation, FixedPoint Target,
    PositionObservation Actual, double Tolerance)
{
    public bool Matched => Correlation.IsValid && Actual.IsReliable &&
        Actual.Identity.ConnectionEpoch == Correlation.ConnectionEpoch && double.IsFinite(Tolerance) && Tolerance >= 0 &&
        Math.Abs(Actual.ActualX!.Value - Target.X) <= Tolerance &&
        Math.Abs(Actual.ActualY!.Value - Target.Y) <= Tolerance &&
        (Actual.AxisPurpose == "XY" || Math.Abs(Actual.ActualZ!.Value - Target.Z) <= Tolerance);
}

public sealed record AngleReachedEvidence(ActionCorrelation Correlation, double TargetAngleDeg,
    double ActualAngleDeg, double ToleranceDeg, DateTimeOffset ObservedAtUtc)
{
    public bool Matched => Correlation.IsValid && double.IsFinite(TargetAngleDeg) && double.IsFinite(ActualAngleDeg) &&
        double.IsFinite(ToleranceDeg) && ToleranceDeg >= 0 &&
        Math.Abs(ActualAngleDeg - TargetAngleDeg) <= ToleranceDeg && ObservedAtUtc > DateTimeOffset.MinValue;
}

public sealed record DeviceActionEvidence(ActionCorrelation Correlation, DeviceCompletionMeaning Meaning,
    IReadOnlyList<ObservationIdentity> Observations, IReadOnlyList<PositionReachedEvidence> Positions,
    FaceObservation? Face, ExecutionOrigin ExecutionOrigin,
    IReadOnlyList<DiagnosticEvidenceReference> DiagnosticEvidenceReferences)
{
    public Guid? TransitionId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AngleReachedEvidence? AngleReached { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PositionReachedEvidence? SafeReached { get; init; }
    public bool IsCorrelated => Correlation.IsValid && Observations.Count > 0 &&
        Observations.All(x => x.IsValid && x.ConnectionEpoch == Correlation.ConnectionEpoch &&
            x.Reliability == DeviceReliability.Reliable) &&
        Positions.All(x => x.Correlation == Correlation && x.Matched) &&
        (Face is null || Face.Correlation == Correlation) &&
        (AngleReached is null || AngleReached.Correlation == Correlation && AngleReached.Matched) &&
        (SafeReached is null || SafeReached.Correlation == Correlation && SafeReached.Matched) &&
        DiagnosticEvidenceReferences.Count > 0 && DiagnosticEvidenceReferences.All(x => x.IsValid) &&
        ExecutionOrigin.Provider != DeviceProvider.Unavailable && ExecutionOrigin.Quality != EvidenceQuality.Unknown;
}

[JsonConverter(typeof(JsonStringEnumConverter<InitialReadiness>))]
public enum InitialReadiness { Unconfirmed, Blocked, Ready }
public sealed record InitialReadinessAssessment(InitialReadiness Readiness,
    DeviceObservation Observation, IReadOnlyList<string> BlockedReasons, long? ResetGeneration = null)
{
    public bool Passed => Readiness == InitialReadiness.Ready && Observation.HasReliableObservation &&
        Observation.Readiness == DeviceReadiness.Ready && BlockedReasons.Count == 0;
}
