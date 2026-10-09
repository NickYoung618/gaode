using Gaode.Domain.Configuration;

namespace Gaode.Host.Composition;

public sealed record Station01RuntimeOptions(string Mode, string TestRoot,
    string AllowedTestRoot, string ConfigRoot, string SchemaRoot,
    ConfigReference PublicReference, ConfigReference BudgetReference,
    ConfigReference SimulationReference, string PlcHost = "127.0.0.1", int PlcPort = 1502,
    byte PlcUnitId = 1, double PositionTolerance = 0.001,
    string PlcProvider = "Virtual", int PlcIoTimeoutMs = 200,
    string? ImageManifestPath = null,
    string? WorkerExecutablePath = null, string? WorkerScriptPath = null,
    string? WorkerManifestPath = null, int TestRecoveryWaitMs = 120000,
    string? TestPersistenceFaultCase = null, string? PlcMechanicsPath = null,
    string? PlcFieldProfilePath = null,
    Gaode.Infrastructure.Devices.Cameras.RealCameraOptions? Cameras = null,
    string? CommissioningPath = null, string? CommissioningSha256 = null,
    string? RealAlgorithmConfigPath = null, string? RealAlgorithmConfigSha256 = null)
{
    public string StoreProfile => Mode == RuntimePurposes.RealDeviceCommissioning ? RuntimePurposes.RealDeviceCommissioning : "Test";
}
