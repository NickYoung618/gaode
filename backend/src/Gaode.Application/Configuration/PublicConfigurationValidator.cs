using Gaode.Application.Capabilities;
using Gaode.Domain.Configuration;

namespace Gaode.Application.Configuration;

public sealed record ConfigurationValidation(IReadOnlyList<string> BlockingControlErrors,
    IReadOnlyList<string> AlgorithmIssues, IReadOnlyList<string> Warnings)
{
    public bool CanStart => BlockingControlErrors.Count == 0;
}

public sealed class PublicConfigurationValidator(CapabilityRegistry capabilities)
{
    public ConfigurationValidation Validate(PublicConfiguration config, BusinessBudget budget,
        SimulationProfile? simulation, bool fullSimulation, bool externalVirtualPlc = false,
        string externalPlcProvider = "Virtual", bool realDeviceCommissioning = false,
        CommissioningConfiguration? commissioning = null, RealAlgorithmConfiguration? realAlgorithm = null)
    {
        var blocks = new List<string>();
        var algorithms = new List<string>();
        var warnings = new List<string>();
        if (config.SchemaVersion != "1.0" || budget.SchemaVersion != "2.0" || config.StopAfter != "PublicHandoff" ||
            config.QualityState != "NotEvaluated") blocks.Add("PublicContractMismatch");
        if ((config.Motion.Axes is not ["X", "Y"] && config.Motion.Axes is not ["X", "Y", "Z"]) || config.Motion.Unit != "mm" ||
            !capabilities.IsCompatible(config.Motion.Capability, config.Purpose, "Motion")) blocks.Add("MotionCapabilityInvalid");
        ValidateLimits(config.Motion.Limits, "MotionLimitsInvalid", blocks);
        ValidatePoint(config.Motion.Points.ThreeD, config.Motion, "ThreeDPointInvalid", blocks);
        ValidatePoint(config.Motion.Points.F, config.Motion, "FPointInvalid", blocks);
        if (config.Motion.Points.Unload is { } unload)
            ValidatePoint(unload, config.Motion, "UnloadPointInvalid", blocks);
        if (!capabilities.IsCompatible(config.Capture3d.Capability, config.Purpose, "Capture3D") ||
            config.Capture3d.Scope.Kind != "WholeTray" || config.Capture3d.Scope.Unit != "mm" ||
            config.Capture3d.MaxCaptureBytes <= 0) blocks.Add("Capture3DInvalid");
        if (!capabilities.IsCompatible(config.CaptureF.Capability, config.Purpose, "CaptureF") ||
            config.CaptureF.FrameCount != 1 || config.CaptureF.AutomaticRetry ||
            config.CaptureF.MaxCaptureBytes <= 0) blocks.Add("CaptureFInvalid");
        ValidateLimits(config.Capture3d.Scope.Bounds, "WholeTrayBoundsInvalid", blocks);
        if (string.IsNullOrWhiteSpace(config.Capture3d.Scope.Id) ||
            string.IsNullOrWhiteSpace(config.Capture3d.Scope.Version)) blocks.Add("WholeTrayIdentityMissing");
        var bindings = config.Bindings.GroupBy(b => b.Id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToArray());
        if (bindings.Any(kv => kv.Value.Length != 1)) blocks.Add("BindingDuplicate");
        Require(config.Capture3d.BindingId, "Camera3D");
        if (config.LightExecution is not null && !config.LightExecution.IsValid) blocks.Add("PublicLightModeInvalid");
        if (config.Capture3d.Parameters.ExposureUs <= 0 || config.CaptureF.Parameters.ExposureUs <= 0)
            blocks.Add("PublicExposureInvalid");
        if (config.LightExecution?.IsSimulated != true)
        {
            Require(config.Capture3d.LightBindingId, "Light3D");
            if (config.Capture3d.Parameters.LightLevel is null or < 0 or > 100 ||
                config.CaptureF.Parameters.LightLevel is null or < 0 or > 100) blocks.Add("PublicLightParametersInvalid");
        }
        Require(config.CaptureF.BindingId, "CameraF");
        if (config.LightExecution?.IsSimulated != true) Require(config.CaptureF.LightBindingId, "LightF");
        if (!config.Bindings.Any(b => b.Role == "PLC")) blocks.Add("PlcBindingMissing");
        if (!capabilities.IsCompatible(config.Algorithms.TrayPose?.Capability, config.Purpose, "TrayPose") ||
            budget.BusinessMs.TrayPoseAlgorithm is null or <= 0) algorithms.Add("TrayPoseNotConfigured");
        if (!capabilities.IsCompatible(config.Algorithms.FDecode.Capability, config.Purpose, "FDecode") ||
            budget.BusinessMs.FDecode is null) algorithms.Add("FDecodeNotConfigured");
        if (config.Parser.Capability is not null &&
            !capabilities.IsCompatible(config.Parser.Capability, config.Purpose, "Parser")) algorithms.Add("ParserNotDefined");
        if (budget.Purpose != config.Purpose || budget.TimeoutBoundary != "ResponseBeforeDeadline" ||
            budget.PhysicalButtonWait != "ExplicitHumanInput") blocks.Add("BudgetPurposeOrBoundaryInvalid");
        if (budget.BusinessMs.RecipeApplication is null or <= 0 || string.IsNullOrWhiteSpace(budget.Id) ||
            string.IsNullOrWhiteSpace(budget.Version) || string.IsNullOrWhiteSpace(budget.Source))
            blocks.Add("RecipeApplicationBudgetInvalid");
        if (budget.Purpose == "Production") blocks.Add("RecipeApplicationProductionBudgetUnapproved");
        if (budget.BusinessMs.PlcAcceptance <= 0 ||
            budget.BusinessMs.XyCompletion <= 0 || budget.BusinessMs.Capture3d <= 0 ||
            budget.BusinessMs.CaptureF <= 0 || budget.BusinessMs.CriticalSave <= 0 ||
            budget.Limits.FReservedMemoryBytes < config.CaptureF.MaxCaptureBytes ||
            budget.Limits.MediaMemoryBytes <= config.Capture3d.MaxCaptureBytes) blocks.Add("BudgetCapacityInvalid");
        if (realDeviceCommissioning)
        {
            Require(config.Algorithms.FDecode.BindingId ?? "", "FDecode");
            Require(config.Algorithms.TrayPose?.BindingId ?? "", "TrayPose");
            if (fullSimulation || externalVirtualPlc || simulation is not null ||
                config.Purpose != RuntimePurposes.RealDeviceCommissioning || externalPlcProvider != "Real" ||
                config.Bindings.Any(b => b.Provider != (b.Role == "PLC" || b.Role.StartsWith("Camera", StringComparison.Ordinal) ||
                    realAlgorithm is not null && b.Role is "TrayPose" or "FDecode" or "EDecode" or "Detection" ? "Real" : "Simulated")))
                blocks.Add("CommissioningProviderMatrixInvalid");
            if (realAlgorithm is not null ? realAlgorithm.Purpose != config.Purpose ||
                realAlgorithm.PublicRef != new ConfigReference(config.Id,config.Version) || realAlgorithm.BudgetRef != new ConfigReference(budget.Id,budget.Version) :
                commissioning is null || commissioning.Purpose != config.Purpose ||
                commissioning.PublicConfigRef != new ConfigReference(config.Id, config.Version) ||
                commissioning.BudgetRef != new ConfigReference(budget.Id, budget.Version))
                blocks.Add("CommissioningConfigurationReferenceMismatch");
            if (realAlgorithm is not null && config.LightExecution?.IsSimulated != true) blocks.Add("RealAlgorithmExplicitSimulatedLightRequired");
            if (budget.RecipeExecution is not { IsValid: true }) blocks.Add("CommissioningRecipeExecutionBudgetMissingOrInvalid");
        }
        else if (config.Purpose == RuntimePurposes.RealDeviceCommissioning) blocks.Add("CommissioningModeRequired");
        else if (fullSimulation)
        {
            if (simulation is null || config.Purpose != "Test" || budget.Purpose != "Test" ||
                simulation.Purpose != "Test" || simulation.PublicConfigRef.Id != config.Id ||
                simulation.PublicConfigRef.Version != config.Version || simulation.BudgetRef.Id != budget.Id ||
                simulation.BudgetRef.Version != budget.Version ||
                config.Bindings.Any(b => b.Provider != "Simulated")) blocks.Add("SimulationPurposeOrReferenceInvalid");
        }
        else if (externalVirtualPlc)
        {
            if (config.Purpose != "Test" || simulation?.ClockMode != "RealElapsed" ||
                config.Motion.Axes is not ["X", "Y", "Z"] || config.Motion.Capability.Id != "xyz.fixed" ||
                config.Bindings.Any(b => b.Provider != (b.Role == "PLC" ? externalPlcProvider : "Simulated")) ||
                simulation.PublicConfigRef != new ConfigReference(config.Id, config.Version) ||
                simulation.BudgetRef != new ConfigReference(budget.Id, budget.Version)) blocks.Add("ExternalVirtualBindingInvalid");
        }
        else
        {
            if (config.Purpose == "Production" && config.Bindings.Any(b => b.Provider == "Simulated"))
                blocks.Add("ProductionSimulationFallbackForbidden");
            if (config.Purpose == "Test" || config.Bindings.Any(b => b.Provider == "Real"))
                blocks.Add("RealAdapterNotIntegrated");
        }
        return new(blocks.Distinct().ToArray(), algorithms.Distinct().ToArray(), warnings);

        void Require(string id, string role)
        {
            if (!bindings.TryGetValue(id, out var matches) || matches.Length != 1 || matches[0].Role != role)
                blocks.Add(role + "BindingInvalid");
        }
    }

    private static void ValidateLimits(AxisLimits limits, string error, List<string> blocks)
    {
        if (!double.IsFinite(limits.XMin) || !double.IsFinite(limits.XMax) ||
            !double.IsFinite(limits.YMin) || !double.IsFinite(limits.YMax) ||
            !double.IsFinite(limits.ZMin) || !double.IsFinite(limits.ZMax) || limits.ZMin > limits.ZMax ||
            limits.XMin > limits.XMax || limits.YMin > limits.YMax) blocks.Add(error);
    }

    private static void ValidatePoint(FixedPoint point, MotionConfiguration motion, string error, List<string> blocks)
    {
        if (string.IsNullOrWhiteSpace(point.Id) || string.IsNullOrWhiteSpace(point.Version) ||
            !double.IsFinite(point.X) || !double.IsFinite(point.Y) || !double.IsFinite(point.Z) ||
            !double.IsFinite(motion.PositionTolerance) || motion.PositionTolerance < 0 ||
            point.Z < motion.Limits.ZMin || point.Z > motion.Limits.ZMax ||
            point.Unit != motion.Unit || point.Frame != motion.Frame ||
            point.X < motion.Limits.XMin || point.X > motion.Limits.XMax ||
            point.Y < motion.Limits.YMin || point.Y > motion.Limits.YMax) blocks.Add(error);
    }
}
