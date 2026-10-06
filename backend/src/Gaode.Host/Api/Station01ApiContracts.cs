using Gaode.Application.Ports;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Persistence;

namespace Gaode.Host.Api;

public sealed record ErrorContract(
    string Code,
    string Message,
    string Category,
    string TraceId,
    bool Retryable,
    object? Details = null,
    long? CurrentRevision = null);

public sealed record ApiCommandReceipt(
    Guid CommandId,
    Guid? RunId,
    string? RequestId,
    bool RequestAccepted,
    bool AdmissionClosed,
    string StopState,
    string ReceiptDurability,
    string TerminalDecision,
    Guid? DecisionWriteId,
    bool? Applied,
    string? StatusUrl,
    ErrorContract? Error = null);

public sealed record AlgorithmAvailability(string State, string? Source, string? Reason);

public sealed record ManualTrayRemovalApiRequest(
    string RequestId,
    long ExpectedRevision,
    string Reason);

public sealed record StageEvidenceApi(
    Guid EventId,
    string Stage,
    string EventType,
    Guid OperationId,
    int Attempt,
    long ConnectionEpoch,
    string PlanRevision,
    string Source,
    string Quality,
    string? ErrorCode,
    long Sequence,
    DateTimeOffset PersistedAtUtc,
    DateTimeOffset? StageStartedAtUtc,
    DateTimeOffset? StageDeadlineAtUtc,
    IReadOnlyList<SemanticPositionEvidence>? PositionEvidence = null,
    Guid? ActionId = null, string RecordNature = "Unavailable", string RawAvailability = "RawUnavailable",
    string SchemaVersion = "device-semantics/1",
    IReadOnlyList<DiagnosticEvidenceReference>? DiagnosticEvidenceReferences = null);

public sealed record ComponentSourceMatrixApi(
    Guid MatrixId,
    string SchemaVersion,
    Guid RunId,
    Guid TrayId,
    string PlanRevision,
    string Milestone,
    string Scope,
    string MatrixDigest,
    IReadOnlyList<ComponentEvidence> Components,
    IReadOnlyList<string> BlockedComponents,
    DateTimeOffset CreatedAtUtc);

public sealed record Station01RunEvidenceApi(
    Guid RunId,
    Guid? TrayId,
    long PersistedRevision,
    IReadOnlyList<StageEvidenceApi> Stages,
    Guid? WholeTrayCompletionId,
    ComponentSourceMatrixApi? ReadyForRemovalSourceMatrix,
    ComponentSourceMatrixApi? FinalSourceMatrix,
    string FinalResult,
    IReadOnlyList<MotionEvidenceApi>? MotionEvidence = null,
    string DeviceSchemaVersion = "device-semantics/1",
    IReadOnlyList<DiagnosticEvidenceReference>? DiagnosticEvidenceReferences = null,
    string RawAvailability = "RawUnavailable");

public sealed record StatusSnapshot(
    string SchemaVersion,
    long Revision,
    string ETag,
    string Host,
    object? Plc,
    object? Camera,
    object? Storage,
    object? Maintenance,
    AlgorithmAvailability Algorithm,
    object? CurrentRun,
    IReadOnlyList<string> Capabilities,
    DateTimeOffset ObservedAt,
    string? Mode = null,
    string? Stage = null,
    string? Recipe = null,
    string? Quality = null,
    int ActiveRuns = 0);

public sealed record NotificationEnvelope(
    string EventType,
    string SchemaVersion,
    Guid RunId,
    long Revision,
    long PersistedRevision,
    IReadOnlyList<string> ChangedFields,
    NotificationSummary? Summary,
    DateTimeOffset OccurredAt);

public sealed record NotificationSummary(string ExecutionState, string WholeTaskState, string? ErrorCode);

public sealed record MediaReference(
    Guid MediaId,
    Guid RunId,
    Guid CaptureId,
    string Kind,
    string ContentType,
    string ETag,
    string Readiness,
    string Source,
    string Purpose,
    long ByteLength);

public static class Station01ApiResults
{
    public static IResult Error(HttpContext context, int status, string code, string message,
        string category = "Request", bool retryable = false, object? details = null,
        long? currentRevision = null)
    {
        var body = new ErrorContract(code, message, category,
            context.TraceIdentifier, retryable, details, currentRevision);
        return Results.Json(body, statusCode: status);
    }

    public static IResult NotFound(HttpContext context, string code, string message) =>
        Error(context, StatusCodes.Status404NotFound, code, message);
}

public sealed record MotionEvidenceApi(Guid WriteId, DateTimeOffset CommittedAtUtc, SemanticMotionFact Facts);
