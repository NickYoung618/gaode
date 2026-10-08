namespace Gaode.Domain.Configuration;

public sealed record CapabilityRef(string Id, string ContractVersion);
public sealed record DeviceBinding(string Id, string Provider, string Role);
public sealed record AxisLimits(double XMin, double XMax, double YMin, double YMax, double ZMin = 0, double ZMax = 1000);
public sealed record FixedPoint(string Id, string Version, double X, double Y, string Unit, string Frame, double Z = 0);
public sealed record FixedPoints(FixedPoint ThreeD, FixedPoint F, FixedPoint? Unload = null);
public sealed record MotionConfiguration(CapabilityRef Capability, string[] Axes, string Frame,
    string Unit, AxisLimits Limits, FixedPoints Points, double PositionTolerance = 0,
    string CoordinateDigest = "", string CoordinateSource = "");
public sealed record TrayScope(string Id, string Version, string Kind, string Unit, string Frame, AxisLimits Bounds);
public sealed record CaptureParameters(int ExposureUs, int? LightLevel);
public sealed record Capture3DConfiguration(CapabilityRef Capability, string BindingId, string LightBindingId,
    TrayScope Scope, CaptureParameters Parameters, long MaxCaptureBytes);
public sealed record CaptureFConfiguration(CapabilityRef Capability, string BindingId, string LightBindingId,
    int FrameCount, bool AutomaticRetry, CaptureParameters Parameters, long MaxCaptureBytes);
public sealed record AlgorithmConfiguration(CapabilityRef? Capability, string? BindingId, string? ParametersVersion);
public sealed record AlgorithmConfigurations(AlgorithmConfiguration? Height, AlgorithmConfiguration FDecode,
    AlgorithmConfiguration? TrayPose = null);
public sealed record ParserConfiguration(CapabilityRef? Capability, string? ParametersVersion);

public sealed record PublicConfiguration(string SchemaVersion, string Id, string Version, string Purpose,
    string Source, DeviceBinding[] Bindings, MotionConfiguration Motion,
    Capture3DConfiguration Capture3d, CaptureFConfiguration CaptureF,
    AlgorithmConfigurations Algorithms, ParserConfiguration Parser,
    string StopAfter, string QualityState)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Gaode.Domain.Configuration.LightExecutionConfiguration? LightExecution { get; init; }
}

