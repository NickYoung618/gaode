using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using System.Text.Json.Serialization;

namespace Gaode.Application.Ports;

// Read-only old persisted auxiliary-action payloads for CommittedResultProjection.
// No current device port accepts these requests or authorizes execution from them.
[JsonConverter(typeof(JsonStringEnumConverter<AuxiliaryHandlingKind>))]
public enum AuxiliaryHandlingKind { TransferToRotation, Rotate, Exit }
public sealed record AuxiliaryHandlingRequest(ActionCorrelation Correlation, AuxiliaryHandlingKind Kind,
    int StepSequence, string PoseId, string CoordinateEvidenceReference, FixedPoint Target,
    string Purpose, Guid IntentWriteId, ActionWindow Window);
public sealed record AuxiliaryHandlingResult(AuxiliaryHandlingRequest Request,
    DeviceActionEvidence Evidence, string? OccupiedEntityId, string? PoseId);

