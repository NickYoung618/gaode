using Gaode.Application.Ports;
namespace Gaode.Infrastructure.Algorithms;

public sealed record WorkerMediaInput(string LeaseId, Guid MediaId, Guid CaptureId,
    string InputKey, string ContentType, long ByteLength, string Sha256,
    string? ObjectId = null, int? LocalFace = null, int? HeightRound = null,
    string? Camera = null)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? StageId { get; init; }
}

/// <summary>NDJSON messages exchanged with a controlled algorithm worker.</summary>
public sealed record WorkerMessage(
    string Type,
    string ContractVersion,
    Guid WorkerSessionId,
    Guid CallId,
    int Attempt,
    string? Role = null,
    string? LeaseId = null,
    string? InputKey = null,
    string? OutputKey = null,
    string? ErrorCode = null,
    string? Reason = null,
    long? ByteLength = null,
    string? ContentType = null,
    string? ResultJson = null,
    IReadOnlyList<WorkerMediaInput>? Inputs = null,
    int? InputIndex = null, TrayObservationContext? ObservationContext = null)
{
    public bool IsControl => Type is "Hello" or "Ready" or "Health" or "Shutdown";
    public bool IsExecution => Type is "Execute" or "Accepted" or "Result" or "Cancel" or "InputReleased" or "WorkerExited";
}

public sealed record WorkerExecutionInput(
    Guid CallId,
    int Attempt,
    string Role,
    string LeaseId,
    IReadOnlyList<WorkerMediaInput> Inputs,
    string ParametersVersion,
    string CapabilityId,
    string CapabilityVersion)
{
    public TrayObservationContext? ObservationContext { get; init; }
}
