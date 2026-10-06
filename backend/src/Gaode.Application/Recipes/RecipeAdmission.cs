using Gaode.Application.Capabilities;
using Gaode.Application.Configuration;

namespace Gaode.Application.Recipes;

public interface IExecutionCostProvider
{
    ExecutionCostProfile Resolve(FrozenConfiguration configuration);
}

public static class RecipeAdmission
{
    public static AdmissionDecision Evaluate(RecipeDefinition definition,
        IEnumerable<string> occupiedSlots, string purpose)
    {
        RecipeDefinitionValidator.Validate(definition);
        var slots = occupiedSlots.ToArray();
        var approval = definition.Approval;
        string? problem = null;
        if (problem is null && (approval.Purpose != purpose || string.IsNullOrWhiteSpace(approval.Digest) ||
            string.IsNullOrWhiteSpace(approval.EvidenceReference))) problem = "RecipeApprovalMissing";
        if (problem is null && slots.Any(s => !approval.AllowedSlots.Contains(s, StringComparer.Ordinal)))
            problem = "RecipeSlotNotApproved";
        problem ??= RecipeDefinitionValidator.ExecutionProblem(definition, slots);
        return new(problem is null, problem, approval.EvidenceReference, definition.DefinitionDigest);
    }

    public static FrozenExecutionInputs Freeze(Guid runId, Guid trayId, RecipeRunPlan plan,
        CapabilityRegistry capabilities, ExecutionCostProfile cost, string purpose,
        Gaode.Domain.Configuration.AlgorithmConfiguration? trayPose = null)
    {
        if (cost.Purpose != purpose || plan.Approval.Purpose != purpose)
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
