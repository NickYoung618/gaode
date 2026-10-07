using Gaode.Domain.Station01;

namespace Gaode.Infrastructure.Persistence;

// Finite, read-only business view. Stored claims never grant current action permission.
public sealed record EvidenceCoordinates(double? X, double? Y, double? Z);
public sealed record EvidenceTarget(double? X, double? Y, double? Z, string? PointRef = null,
    string? Version = null);
public sealed record SemanticPositionEvidence(string Kind, string AxisRole, EvidenceTarget? Target,
    EvidenceCoordinates? Actual, double? Tolerance, bool? Matched, DateTimeOffset? ObservedAtUtc,
    Guid? ObservationId, DiagnosticEvidenceReference? DiagnosticEvidenceReference,
    string RecordNature, string SchemaVersion = "device-semantics/1");
public sealed record DevicePositionHistory(string RecordNature, string RawAvailability,
    string? RecordedSource, IReadOnlyList<SemanticPositionEvidence>? Positions,
    IReadOnlyList<DiagnosticEvidenceReference> DiagnosticEvidenceReferences);

public sealed record StageDeviceHistory(Guid? ActionId, string RecordNature, string RawAvailability,
    IReadOnlyList<SemanticPositionEvidence>? Positions, IReadOnlyList<DiagnosticEvidenceReference> DiagnosticEvidenceReferences);
public sealed record RunDiagnosticReferences(string RawAvailability, IReadOnlyList<DiagnosticEvidenceReference> References);

public sealed record BusinessMotionPoint(string? Id, string? Version, string? Unit, string? Frame,
    double? X, double? Y, double? Z);
public sealed record BusinessMotionTarget(int? StepSequence, string? ObjectId, string? SlotId,
    int? PhysicalSlotIndex, int? LocalFace, int? HeightRound, string? Camera, string? PointRef,
    BusinessMotionPoint Point, string? CoordinateSource, string? ZBasis);
public sealed record SemanticMotionFact(string? Kind, Guid? OperationId, Guid? ActionId, int? Attempt,
    string? PointId, string? PointVersion, string? Role, BusinessMotionTarget? Target,
    EvidenceCoordinates? Actual, string? ZAxis, double? Tolerance, bool? Matched, bool? Accepted,
    bool? Completed, DateTimeOffset? ObservedAtUtc, long? ConnectionEpoch, Guid? ObservationId,
    string RecordNature, string SchemaVersion = "device-semantics/1",
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    RecipeApplicationView? RecipeApplication = null);
