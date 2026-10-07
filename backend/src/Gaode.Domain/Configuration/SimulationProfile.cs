namespace Gaode.Domain.Configuration;

public sealed record ConfigReference(string Id, string Version);
public sealed record SimStage(int DelayMs, string Strategy, string Outcome,
    string? FailureCode, int[] DuplicateOffsetsMs, bool IgnoreCancel);
public sealed record SimStages(SimStage PlcAcceptance, SimStage ClampCompletion,
    SimStage XyCompletion, SimStage Capture3d, SimStage HeightAlgorithm,
    SimStage CaptureF, SimStage FDecode);
public sealed record SimDeviceInitial(bool Connected, bool Automatic, bool SafetyClear,
    bool Clamped, double X, double Y);
public sealed record SimStop(int AcceptedDelayMs, int CompletedDelayMs, bool Respond);
public sealed record SimHeightSample(string SourceElementId, double Value, string Unit, string Datum);
public sealed record SimFixtures(SimHeightSample[] HeightSamples, string[] RawCodes, string MediaSource);
public sealed record SimulationProfile(string SchemaVersion, string Id, string Version, string Purpose,
    string Source, string ClockMode, DateTimeOffset VirtualStartUtc,
    ConfigReference PublicConfigRef, ConfigReference BudgetRef,
    SimStages Stages, SimDeviceInitial DeviceInitial, SimStop Stop,
    int LateObservationWindowMs, SimFixtures Fixtures);
