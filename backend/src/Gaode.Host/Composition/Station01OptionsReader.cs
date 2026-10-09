using Gaode.Domain.Configuration;

namespace Gaode.Host.Composition;

public static class Station01OptionsReader
{
    public static Station01RuntimeOptions Read(IConfiguration configuration)
    {
        var section = configuration.GetSection("Gaode");
        var mode = section["Mode"] ?? throw new InvalidOperationException("Gaode:Mode缺失");
        var commissioningMode = mode == RuntimePurposes.RealDeviceCommissioning;
        string RequiredCommissioning(string key, string legacyDefault) => section[key] ??
            (commissioningMode ? throw new InvalidOperationException("Gaode:" + key + "缺失") : legacyDefault);
        T RequiredCommissioningValue<T>(string key, T legacyDefault) where T : struct =>
            section.GetValue<T?>(key) ?? (commissioningMode ? throw new InvalidOperationException("Gaode:" + key + "缺失") : legacyDefault);
        return new Station01RuntimeOptions(
            mode,
            section["TestRoot"] ?? throw new InvalidOperationException("Gaode:TestRoot缺失"),
            section["AllowedTestRoot"] ?? throw new InvalidOperationException("Gaode:AllowedTestRoot缺失"),
            section["ConfigRoot"] ?? throw new InvalidOperationException("Gaode:ConfigRoot缺失"),
            section["SchemaRoot"] ?? throw new InvalidOperationException("Gaode:SchemaRoot缺失"),
            new ConfigReference(RequiredCommissioning("PublicId", "s01-public-dev"), RequiredCommissioning("PublicVersion", "1.0.0")),
            new ConfigReference(RequiredCommissioning("BudgetId", "s01-budget-dev"), RequiredCommissioning("BudgetVersion", "3.0.0")),
            commissioningMode
                ? new ConfigReference(RequiredCommissioning("CommissioningId", ""), RequiredCommissioning("CommissioningVersion", ""))
                : new ConfigReference(section["SimulationId"] ?? "s01-sim-normal", section["SimulationVersion"] ?? "3.0.0"),
            RequiredCommissioning("PlcHost", "127.0.0.1"), RequiredCommissioningValue("PlcPort", 1502),
            RequiredCommissioningValue("PlcUnitId", (byte)1), RequiredCommissioningValue("PositionTolerance", 0.001),
            RequiredCommissioning("PlcProvider", "Virtual"),
            RequiredCommissioningValue("PlcIoTimeoutMs", 200),
            section["ImageManifestPath"], section["WorkerExecutablePath"],
            section["WorkerScriptPath"], section["WorkerManifestPath"],
            section.GetValue<int?>("TestRecoveryWaitMs") ?? 120000, section["TestPersistenceFaultCase"],
            PlcMechanicsPath: section["PlcMechanicsPath"],
            PlcFieldProfilePath: section["PlcFieldProfilePath"],
            Cameras: section.GetValue("Cameras:Enabled", false) ? RealCameraRegistration.ReadOptions(configuration) : null,
            CommissioningPath: section["CommissioningPath"], CommissioningSha256: section["CommissioningSha256"],
            RealAlgorithmConfigPath: section["RealAlgorithmConfigPath"], RealAlgorithmConfigSha256: section["RealAlgorithmConfigSha256"]);
    }
}
