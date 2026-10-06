using Gaode.Application.Recipes;

namespace Gaode.Application.Workflow;

// Deterministic call counts are the input to a later deadline calculation.
// This does not assign a deadline or authorize a restricted recipe.
public sealed record RecipeWorkload(int CaptureCount, int FusionCount,
    int DetectionWorkerCalls, int HeightRescanCount)
{
    public int FlipCount { get; init; }
    public int ECodeCount { get; init; }
}

public static class RecipeWorkloadCounter
{
    public static RecipeWorkload Count(RecipeRunPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var captures = plan.Steps.Where(step => step.Kind == RecipeStepKind.Capture).ToArray();
        var faces = captures.Select(step => new
        {
            step.UnitId, step.MemberId, step.LocalFace, step.CoordinateEpoch, step.StageId
        }).Distinct().Count();
        return new RecipeWorkload(captures.Length, faces, captures.Length + faces,
            plan.Steps.Count(step => step.Kind == RecipeStepKind.RescanWholeTray))
        {
            FlipCount = plan.Steps.Count(step => step.Kind == RecipeStepKind.FlipMember),
            ECodeCount = plan.Steps.Count(step => step.Kind == RecipeStepKind.ReadECode)
        };
    }
}
