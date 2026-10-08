using Gaode.Application.Capabilities;
using Gaode.Application.Configuration;

namespace Gaode.Application.Recipes;

public interface IExecutionCostProvider
{
    ExecutionCostProfile Resolve(FrozenConfiguration configuration);
}

public static class RecipeAdmission
{
    public static bool MatchesRunPurpose(FrozenExecutionInputs inputs, Gaode.Domain.Station01.RunPurpose purpose) =>
        inputs.CostProfile.Purpose == Gaode.Domain.Configuration.RuntimePurposes.RealDeviceCommissioning
            ? purpose == Gaode.Domain.Station01.RunPurpose.Commissioning
            : inputs.CostProfile.Purpose == purpose.ToString() && inputs.Plan.Approval.Purpose == purpose.ToString();
    public static AdmissionDecision Evaluate(RecipeDefinition definition,
        IEnumerable<string> occupiedSlots, string purpose)
    {
        RecipeDefinitionValidator.Validate(definition);
        var slots = occupiedSlots.ToArray();
        var approval = definition.Approval;
        string? problem = null;
        if (purpose == Gaode.Domain.Configuration.RuntimePurposes.RealDeviceCommissioning)
        {
            // Saved software-validated content, never a synthetic human approval.
            if (string.IsNullOrWhiteSpace(definition.RecipeId) || string.IsNullOrWhiteSpace(definition.Version) ||
                definition.DefinitionDigest != RecipeDefinitionIdentity.ComputeDefinitionDigest(definition))
                problem = "RecipeSavedIdentityInvalid";
            if (problem is null && (slots.Distinct(StringComparer.Ordinal).Count() != slots.Length ||
                slots.Any(s => !definition.Positions.Any(p => p.SlotId == s)))) problem = "RecipeSlotUnknown";
        }
        else
        {
            if (approval.Purpose != purpose || string.IsNullOrWhiteSpace(approval.Digest) ||
                string.IsNullOrWhiteSpace(approval.EvidenceReference)) problem = "RecipeApprovalMissing";
            if (problem is null && slots.Any(s => !approval.AllowedSlots.Contains(s, StringComparer.Ordinal)))
                problem = "RecipeSlotNotApproved";
        }
        problem ??= RecipeDefinitionValidator.ExecutionProblem(definition, slots);
        return new(problem is null, problem, purpose == Gaode.Domain.Configuration.RuntimePurposes.RealDeviceCommissioning
            ? "SoftwareValidatedSavedRecipe:" + definition.Version : approval.EvidenceReference, definition.DefinitionDigest);
    }

    public static FrozenExecutionInputs Freeze(Guid runId, Guid trayId, RecipeRunPlan plan,
        CapabilityRegistry capabilities, ExecutionCostProfile cost, string purpose,
        Gaode.Domain.Configuration.AlgorithmConfiguration? trayPose = null)
    {
        if (cost.Purpose != purpose ||
            (purpose != Gaode.Domain.Configuration.RuntimePurposes.RealDeviceCommissioning && plan.Approval.Purpose != purpose))
            throw new InvalidOperationException("ExecutionApprovalPurposeMismatch");
        var requirements = plan.Steps.Where(s => s.Kind is RecipeStepKind.Capture or RecipeStepKind.ReadECode)
            .SelectMany(s => s.Kind == RecipeStepKind.Capture ? new[] { s.AlgorithmProfile!, s.AlgorithmProfile + "/fusion" }
                : new[] { s.AlgorithmProfile! }).Distinct(StringComparer.Ordinal).ToArray();
        var bindings = requirements.ToDictionary(key => key,
            key => capabilities.Bind(plan.AlgorithmRequirements[key], purpose), StringComparer.Ordinal);
        var frozenPlan = RecipeCatalogSnapshots.Freeze(plan);
        BoundCapability? poseBinding = null;
        if (plan.Steps.Any(s => s.Kind == RecipeStepKind.RescanWholeTray))
        {
            if (trayPose?.Capability is null || string.IsNullOrWhiteSpace(trayPose.ParametersVersion))
                throw new InvalidOperationException("TrayPoseCapabilityNotConfigured");
            poseBinding = capabilities.Bind(new("tray-observation", trayPose.ParametersVersion,
                AlgorithmPurpose.TrayPose, 1, "tray-observation/2"), purpose);
            if (poseBinding.CapabilityId != trayPose.Capability.Id || poseBinding.CapabilityVersion != trayPose.Capability.ContractVersion)
                throw new InvalidOperationException("TrayPoseCapabilityMismatch");
        }
        var input = new FrozenExecutionInputs(FrozenExecutionInputs.CurrentSchema, runId, trayId,
            RecipePlanRevision.Compute(frozenPlan), frozenPlan, RecipeCatalogSnapshots.Map(bindings), cost, "")
            { TrayPoseCapability = poseBinding };
        return input with { SemanticDigest = input.ComputeDigest() };
    }
}
