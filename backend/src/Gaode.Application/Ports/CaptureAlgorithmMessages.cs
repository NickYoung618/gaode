using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;

namespace Gaode.Application.Ports;

public enum CaptureRole { ThreeD, F, Detection, E }
public enum CaptureEventKind { Accepted, Capturing, Ended, MediaTaken, Failed, Unknown }
public sealed record DetectionCaptureSettings(string ProfileId, int ExposureUs, double Gain,
    int[] RoiPixels, string? LightChannel = null, int? BrightnessPercent = null, int? SettleMs = null);
public sealed record PublicCaptureSettings(string ConfigurationId, string ConfigurationVersion,
    int ExposureUs, int? LightLevel);
public sealed record CameraImagingSettings(int ExposureUs, double? Gain = null, int[]? RoiPixels = null);
public sealed record ActualCameraSettings(int ExposureUs, double Gain, int Width, int Height, int OffsetX, int OffsetY);
public sealed record CaptureRequest(PortEnvelope Envelope, Guid CaptureId, CaptureRole Role,
    string PointId, string PointVersion, string? ScopeId, string? ScopeVersion,
    string CameraBindingId, string? LightBindingId, Guid IntentWriteId, long MaxBytes)
{
    public Guid? AcquisitionSessionId { get; init; }
    public Guid? AcquisitionOperationId { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Gaode.Domain.Configuration.LightExecutionConfiguration? LightExecution { get; init; }
    public DetectionCaptureSettings? DetectionSettings { get; init; }
    public PublicCaptureSettings? PublicSettings { get; init; }
}
public sealed record CaptureEvent(CaptureRequest Request, CaptureEventKind Kind,
    long ConnectionEpoch, byte[]? Buffer = null, string? Format = null,
    string? ErrorCode = null)
{
    public CorrelatedCaptureFact? Fact { get; init; }
}

public sealed record MediaRef(Guid MediaId, Guid RunId, Guid CaptureId, string Kind,
    string RelativeKey, long ByteLength, string Format, string Source,
    string ScopeVersion, string PointVersion, string StorageState)
{
    public string ContentType => Format switch
    {
        "img" => "image/jpeg",
        "png" => "image/png",
        "bin" => "application/octet-stream",
        "CameraProFrameZipV1" => "application/zip",
        "GalaxyRaw" => "application/octet-stream",
        _ => "application/octet-stream"
    };
    public string? Purpose { get; init; }
    public AlgorithmInputProvenance? AlgorithmInput { get; init; }
    public string Readiness => StorageState == "FileCompleted" ? "Ready" : "NotReady";
    public string ETag => $"\"media-{MediaId:N}-{ByteLength}-{StorageState}\"";
}

public sealed record AlgorithmInputProvenance(string SchemaVersion, Guid RawMediaId, string RawRelativeKey,
    string RawFormat, string RawSha256, string InputSha256, string ConverterId, string ConverterVersion,
    int Width, int Height, string PixelFormat, long? PointCount,
    string? PointUnitSource, string? CoordinateSource);

public enum CaptureApplicationState { Unknown, Applied, ConfiguredOnly, NotApplied }
public sealed record CorrelatedCaptureFact(Guid RunId, Guid CaptureId, Guid OperationId,
    long ConnectionEpoch, string RequestedSettingsDigest, string MediaSource,
    ComponentExecutionOrigin CameraOrigin, ComponentExecutionOrigin LightOrigin,
    CaptureApplicationState ApplicationState, DetectionCaptureSettings? ActualSettings,
    bool Replayed, IReadOnlyList<string> EvidenceReferences)
{
    public CaptureFrameMetadata? FrameMetadata { get; init; }
    public CaptureApplicationState CameraApplicationState { get; init; } = CaptureApplicationState.Unknown;
    public CaptureApplicationState LightApplicationState { get; init; } = CaptureApplicationState.Unknown;
    public ActualCameraSettings? ActualCameraSettings { get; init; }
    public PublicCaptureSettings? ActualPublicSettings { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Gaode.Domain.Configuration.LightExecutionConfiguration? LightExecution { get; init; }
    public bool PhysicalLightApplied { get; init; }
}

public enum AlgorithmRole { Height, FDecode, Detection, EDecode, TrayPose }
public enum AlgorithmEventKind { Accepted, Running, Result, Failed, InputReleased, WorkerExited }
public sealed record WorkerTargetIdentity(string ObjectId, int LocalFace,
    int HeightRound, string Camera)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? StageId { get; init; }
}
public sealed record AlgorithmRequest(PortEnvelope Envelope, Guid CallId, Guid CaptureId,
    AlgorithmRole Role, IReadOnlyList<MediaRef> Inputs, string ParametersVersion,
    string CapabilityId, string CapabilityVersion, Guid IntentWriteId,
    string InvocationBasis, WorkerTargetIdentity? TargetIdentity = null)
{
    public Gaode.Application.Configuration.AlgorithmModuleReference? FrozenModule { get; init; }
    public string? AlgorithmConfigurationDigest { get; init; }
    public TrayObservationContext? ObservationContext { get; init; }
    public IReadOnlyList<WorkerTargetIdentity>? InputIdentities { get; init; }
}
public sealed record TrayObservationContext(Guid TrayId, TrayObservationPurpose Purpose, int CheckRound, Guid? RelatedTransitionId);
public sealed record AlgorithmEvent(AlgorithmRequest Request, AlgorithmEventKind Kind,
    IReadOnlyList<HeightSample>? HeightSamples = null,
    IReadOnlyList<string>? RawCodes = null, string? ErrorCode = null,
    Guid? WorkerSessionId = null, string? DetectionDisposition = null)
{
    public TrayObservation? Observation { get; init; }
}

public static class AcquisitionContract
{
    public static string RequestedSettingsDigest(CaptureRequest request) => request.LightExecution is not null
        ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
            { request.Role, request.PointId, request.PointVersion, request.CameraBindingId, request.LightBindingId,
                request.DetectionSettings, request.PublicSettings, request.LightExecution }))) : request.PublicSettings is null ? Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
        { request.Role, request.PointId, request.PointVersion, request.CameraBindingId,
            request.LightBindingId, request.DetectionSettings }))) : Convert.ToHexString(
        System.Security.Cryptography.SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
        { request.Role, request.PointId, request.PointVersion, request.CameraBindingId,
            request.LightBindingId, request.DetectionSettings, request.PublicSettings })));

    public static bool MatchesFact(CorrelatedCaptureFact fact, CaptureRequest request, long epoch) =>
        fact.RunId == request.Envelope.RunId && fact.CaptureId == request.CaptureId &&
        fact.OperationId == request.Envelope.OperationId && fact.ConnectionEpoch == epoch &&
        fact.RequestedSettingsDigest == RequestedSettingsDigest(request);

    public static bool Matches(CaptureEvent value, CaptureRequest expected, long epoch) =>
        value.Request.Envelope.IsValid && value.Request.CaptureId == expected.CaptureId &&
        value.Request.Envelope.RunId == expected.Envelope.RunId &&
        value.Request.Envelope.OperationId == expected.Envelope.OperationId &&
        value.Request.Envelope.SessionId == expected.Envelope.SessionId &&
        value.ConnectionEpoch == epoch;

    public static bool Matches(AlgorithmEvent value, AlgorithmRequest expected) =>
        value.Request.Envelope.IsValid && value.Request.CallId == expected.CallId &&
        value.Request.CaptureId == expected.CaptureId &&
        value.Request.Envelope.RunId == expected.Envelope.RunId &&
        value.Request.Envelope.OperationId == expected.Envelope.OperationId &&
        value.Request.Envelope == expected.Envelope && value.Request.Role == expected.Role &&
        value.Request.ParametersVersion == expected.ParametersVersion && value.Request.CapabilityId == expected.CapabilityId &&
        value.Request.CapabilityVersion == expected.CapabilityVersion && value.Request.IntentWriteId == expected.IntentWriteId &&
        value.Request.InvocationBasis == expected.InvocationBasis && value.Request.Inputs.SequenceEqual(expected.Inputs) &&
        value.Request.TargetIdentity == expected.TargetIdentity && value.Request.ObservationContext == expected.ObservationContext &&
        value.Request.AlgorithmConfigurationDigest == expected.AlgorithmConfigurationDigest &&
        value.Request.FrozenModule == expected.FrozenModule &&
        ((value.Request.InputIdentities is null && expected.InputIdentities is null) ||
         (value.Request.InputIdentities is not null && expected.InputIdentities is not null && value.Request.InputIdentities.SequenceEqual(expected.InputIdentities)));
}
