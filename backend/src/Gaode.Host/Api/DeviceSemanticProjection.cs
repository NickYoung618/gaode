using System.Text.Json;
using System.Text.Json.Nodes;
using Gaode.Domain.Station01;

namespace Gaode.Host.Api;

public sealed record DevicePositionApi(double? ActualX, double? ActualY, double? ActualZ,
    string? AxisPurpose, string? CoordinateFrame, string? UnitBasis, Guid ObservationId,
    DateTimeOffset SampleStartedUtc, DateTimeOffset SampleEndedUtc, long ConnectionEpoch, string Reliability);
public sealed record DeviceObservationApi(string SchemaVersion, string Reliability, string Connection,
    long ConnectionEpoch, string OperatingMode, string Readiness, string SafetyAssessment, string Clamp,
    string MotionAvailability, string AcquisitionReadiness, string ManualArea, string ManualHandling,
    DevicePositionApi? Position, IReadOnlyList<AlarmAssessment>? Alarms, IReadOnlyList<string> ReasonCodes,
    ExecutionOrigin ExecutionOrigin, Guid ObservationId, DateTimeOffset SampleStartedUtc,
    DateTimeOffset SampleEndedUtc, DiagnosticEvidenceReference? DiagnosticEvidenceReference)
{
    public IReadOnlyList<AxisObservationProjection> AxisObservations { get; init; } = [];
}
public sealed record StartupDiagnosticApi(string SchemaVersion, string RecordNature, string RawAvailability,
    IReadOnlyList<string> ReasonCodes, string SafetyAssessment, string StopStage, string Disposition,
    long? ConnectionEpoch, DateTimeOffset? ObservedAtUtc, ExecutionOrigin ExecutionOrigin,
    DeviceObservationApi? SemanticObservation, DiagnosticEvidenceReference? DiagnosticEvidenceReference);

public static class DeviceSemanticProjection
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static DeviceObservationApi? Observation(DeviceObservation? observation)
    {
        if (observation?.Identity is not { IsValid: true } identity) return null;
        var p = observation.Position;
        return new("device-semantics/1.2", observation.Reliability.ToString(), observation.Connection.ToString(),
            observation.ConnectionEpoch, observation.OperatingMode.ToString(), observation.Readiness.ToString(),
            observation.SafetyAssessment.ToString(), observation.Clamp.ToString(), observation.MotionAvailability.ToString(),
            observation.AcquisitionReadiness.ToString(), observation.ManualArea.ToString(), observation.ManualHandling.ToString(),
            p is null ? null : new(p.ActualX, p.ActualY, p.ActualZ, p.AxisPurpose, p.CoordinateFrame, p.UnitBasis, p.Identity.ObservationId,
                p.Identity.SampleStartedUtc, p.Identity.SampleEndedUtc, p.Identity.ConnectionEpoch, p.Identity.Reliability.ToString()),
            observation.HasReliableObservation ? observation.Alarms : null, observation.ReasonCodes,
            observation.ExecutionOrigin, identity.ObservationId, identity.SampleStartedUtc, identity.SampleEndedUtc,
            observation.DiagnosticEvidenceReference)
        { AxisObservations = AxisObservationProjectionBuilder.From(observation, null) };
    }
    public static StartupDiagnosticApi? Startup(StartupDiagnostic? diagnostic) => diagnostic is null ? null :
        new("device-semantics/1", "Derived",
            diagnostic.DiagnosticEvidenceReference is { IsValid: true } && diagnostic.ExecutionOrigin.Provider is DeviceProvider.Real or DeviceProvider.Virtual
                ? "CapturedRaw" : "RawUnavailable",
            diagnostic.ReasonCodes, diagnostic.SafetyAssessment, diagnostic.StopStage, diagnostic.Disposition,
            diagnostic.ConnectionEpoch, diagnostic.ObservedAtUtc, diagnostic.ExecutionOrigin,
            Observation(diagnostic.SemanticObservation), diagnostic.DiagnosticEvidenceReference);

    public static JsonElement? Run(RunSnapshot? snapshot)
    {
        if (snapshot is null) return null;
        // The unchanged business run contract stays intact. Only the explicitly
        // versioned device sub-object is projected; no untyped device payload escapes.
        var node = JsonSerializer.SerializeToNode(snapshot, Json)!.AsObject();
        node["startupDiagnostic"] = JsonSerializer.SerializeToNode(Startup(snapshot.StartupDiagnostic), Json);
        return JsonSerializer.SerializeToElement(node, Json);
    }
}
