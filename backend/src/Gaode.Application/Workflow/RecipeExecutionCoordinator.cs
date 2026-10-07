using Gaode.Application.Recipes;
using Gaode.Application.Ports;

namespace Gaode.Application.Workflow;

/// <summary>
/// Checks ownership and order before the detection adapter may touch a camera or PLC.
/// A point reference alone is not an approved XYZ target or a height conversion.
/// </summary>
public static class RecipeExecutionCoordinator
{
    public static string? ValidateTargets(DetectionRequest request)
    {
        if (request.Plan is null || request.Targets is null || request.MotionConfiguration is null)
            return "ProductTargetResolutionMissing";
        if (request.Scope is { } scope && !request.Plan.Steps.Any(scope.Includes))
            return "DetectionScopeIdentityMismatch";
        var positions = request.Plan.Steps.Where(step => step.Kind == RecipeStepKind.PositionForCapture)
            .Where(step => request.Scope is null || request.Scope.Includes(step))
            .ToArray();
        if (positions.Length != request.Targets.Count ||
            request.Targets.Select(target => target.StepSequence).Distinct().Count() != positions.Length)
            return "ProductTargetCountMismatch";
        foreach (var step in positions)
        {
            var target = request.Targets.SingleOrDefault(value => value.StepSequence == step.Sequence);
            if (target is null || !target.IsValid || target.ObjectId != (step.MemberId ?? step.UnitId) ||
                target.SlotId != step.SlotId || target.PhysicalSlotIndex != step.PhysicalSlotIndex ||
                target.LocalFace != step.LocalFace || target.CoordinateEpoch != step.CoordinateEpoch ||
                target.StageId != step.StageId || target.ScanPoseId != step.ScanPoseId ||
                target.Camera != step.Camera || target.PointRef != step.PointRef)
                return "ProductTargetIdentityMismatch";
            var point = target.Point;
            var motion = request.MotionConfiguration.Motion;
            if (point.Unit != motion.Unit || point.Frame != motion.Frame ||
                point.X < motion.Limits.XMin || point.X > motion.Limits.XMax ||
                point.Y < motion.Limits.YMin || point.Y > motion.Limits.YMax ||
                point.Z < motion.Limits.ZMin || point.Z > motion.Limits.ZMax)
                return "ProductTargetOutsideConfiguredLimits";
        }
        return null;
    }

    public static string? ValidateDetectionPlan(RecipeRunPlan plan)
    {
        var faceProblem = ValidateFaceRoundOrder(plan);
        if (faceProblem is not null) return faceProblem;
        if (plan.Steps.Count == 0 || plan.Steps.Where((step, index) => step.Sequence != index + 1).Any())
            return "FrozenStepSequenceInvalid";

        RecipeStep? position = null;
        var captures = 0;
        foreach (var step in plan.Steps)
        {
            if (step.OwnerStage != "Detection")
            {
                if (step.OwnerStage is "Sorting" or "Unload" &&
                    step.Kind is RecipeStepKind.SortUnit or RecipeStepKind.ReturnUnit or RecipeStepKind.UnloadTray)
                    continue;
                return "FrozenStepOwnerInvalid";
            }
            switch (step.Kind)
            {
                case RecipeStepKind.PositionForCapture:
                    if (position is not null) return "PositionWithoutCapture";
                    if (string.IsNullOrWhiteSpace(step.PointRef) || step.PhysicalSlotIndex is null)
                        return "ProductPointMappingMissing";
                    position = step;
                    break;
                case RecipeStepKind.Capture:
                case RecipeStepKind.ReadECode:
                    if (position is null || position.UnitId != step.UnitId ||
                        position.MemberId != step.MemberId || position.LocalFace != step.LocalFace ||
                        position.Camera != step.Camera || position.CoordinateEpoch != step.CoordinateEpoch ||
                        position.StageId != step.StageId || position.ScanPoseId != step.ScanPoseId)
                        return "CaptureWithoutMatchingPosition";
                    position = null;
                    if (step.Kind == RecipeStepKind.Capture) captures++;
                    break;
                case RecipeStepKind.DecideUnit:
                    if (position is not null) return "PositionWithoutCapture";
                    break;
                case RecipeStepKind.FlipMember:
                case RecipeStepKind.TransferToRotation:
                case RecipeStepKind.Rotate:
                    if (position is not null) return "PositionWithoutCapture";
                    break;
                case RecipeStepKind.RescanWholeTray:
                    if (position is not null) return "PositionWithoutCapture";
                    break;
                default:
                    return $"DetectionStepUnsupported:{step.Kind}";
            }
        }
        if (position is not null) return "PositionWithoutCapture";
        return captures == 0 ? "DetectionStepsMissing" : null;
    }

    /// <summary>Every new observation epoch follows completion of the planned put-backs.</summary>
    public static string? ValidateFaceRoundOrder(RecipeRunPlan plan)
    {
        var epoch = plan.InitialCoordinateEpoch;
        var pending = false;
        string? transitionStage = null;
        foreach (var step in plan.Steps.Where(s => s.OwnerStage == "Detection"))
        {
            if (step.Kind == RecipeStepKind.FlipMember)
            {
                var key = step.StageId ?? step.ScanPoseId;
                if (step.CoordinateEpoch != epoch || step.TargetPose is null || key is null || pending && key != transitionStage)
                    return "FaceFlipOrderInvalid";
                pending = true;
                transitionStage = key;
            }
            else if (step.Kind == RecipeStepKind.RescanWholeTray)
            {
                if (!pending || step.CoordinateEpoch != epoch + 1 || (step.StageId ?? step.ScanPoseId) != transitionStage)
                    return "PoseObservationOrderInvalid";
                epoch = step.CoordinateEpoch;
                pending = false;
            }
            else if (step.Kind is RecipeStepKind.PositionForCapture or RecipeStepKind.Capture or RecipeStepKind.ReadECode)
            {
                if (pending || step.CoordinateEpoch != epoch) return "PoseObservationRequiredBeforeCapture";
            }
        }
        return pending ? "PutBackObservationMissing" : null;
    }
}
