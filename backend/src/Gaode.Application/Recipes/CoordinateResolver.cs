using Gaode.Application.Ports;
using Gaode.Application.Station01;
using Gaode.Domain.Configuration;

namespace Gaode.Application.Recipes;

/// <summary>Resolves frozen configuration values and preserves object/purpose identities.</summary>
public static class CoordinateResolver
{
    public static (IReadOnlyList<DetectionStepTarget>, IReadOnlyList<DetectionObjectExpectation>) Resolve(
        RecipeRunPlan plan, PublicPreparationHandoffV2 handoff)
    {
        if (!handoff.IsComplete) throw new InvalidOperationException("CurrentHandoffInvalid");
        var steps = plan.Steps.Where(s => s.Kind == RecipeStepKind.PositionForCapture).ToArray();
        if (steps.Length == 0) throw new InvalidOperationException("ConfiguredPositionsMissing");
        var targets = new List<DetectionStepTarget>();
        var expected = new Dictionary<string, DetectionObjectExpectation>(StringComparer.Ordinal);
        foreach (var step in steps)
        {
            if (step.SlotId is null || !plan.ExecutionPositions.TryGetValue(step.SlotId, out var slot))
                throw new InvalidOperationException("ProductPointCoordinatesMissing");
            var input = slot.ForDetection(step.Material);
            DetectionStepTarget target;
            if (step.Camera == "E")
            {
                var point = Purpose(input, step.PointRef, RecipePointPurpose.EScan);
                target = Target(step, point.Point, point.CoordinateEvidenceReference,
                    input.PurposePoints[step.PointRef!].Fixed);
            }
            else
            {
                var matches = input.Coordinates.Where(c => c.StageId == step.StageId && c.LocalFace == step.LocalFace && c.Camera == step.Camera).ToArray();
                if (matches.Length != 1) throw new InvalidOperationException("ProductFaceTargetIdentityMismatch");
                var d = matches[0];
                if (d.PointRef != step.PointRef || d.ObjectPattern.Replace("{UnitId}", step.UnitId, StringComparison.Ordinal) != (step.MemberId ?? step.UnitId) ||
                    d.SlotId != step.SlotId || d.PhysicalSlotIndex != step.PhysicalSlotIndex || string.IsNullOrWhiteSpace(d.ConfigurationVersion))
                    throw new InvalidOperationException("ProductFaceTargetIdentityMismatch");
                target = Target(step, Point(d.Point, d.Fixed), d.SourceFactReference, d.Fixed);
            }
            if (!target.IsValid) throw new InvalidOperationException("ProductTargetInvalid");
            targets.Add(target);
            var physicalId = plan.UnitKind == "assembledEntity" ? step.UnitId : step.MemberId ?? step.UnitId;
            var origin = slot.ForObject(plan.UnitKind, step.Material).Source;
            if (origin is null || string.IsNullOrWhiteSpace(origin.CoordinateEvidenceReference))
                throw new InvalidOperationException("ProductSourcePointEvidenceMissing");
            var expectation = new DetectionObjectExpectation(physicalId, origin.Point,
                plan.UnitKind == "assembledEntity" ? "Assembly" : step.Material ?? throw new InvalidOperationException("ProductMaterialMissing"));
            if (!expectation.IsApprovedPosition) throw new InvalidOperationException("ProductSourcePointInvalid");
            expected.TryAdd(physicalId, expectation);
        }
        return (targets.AsReadOnly(), expected.Values.ToArray());
    }

    public static HandlingPoint Purpose(ObjectExecutionInputs input, string? reference, RecipePointPurpose purpose)
    {
        if (reference is null || !input.PurposePoints.TryGetValue(reference, out var configured) || configured.Purpose != purpose ||
            string.IsNullOrWhiteSpace(configured.CoordinateEvidenceReference)) throw new InvalidOperationException("PurposePointMissingOrMismatched");
        return new(Point(configured.Point, configured.Fixed), configured.CoordinateEvidenceReference);
    }
    private static FixedPoint Point(PlanarPoint p, ApprovedFixedBasis z)
    {
        if (!double.IsFinite(p.X) || !double.IsFinite(p.Y) || !double.IsFinite(z.Z) || p.Unit != z.Unit || p.Frame != z.Datum ||
            string.IsNullOrWhiteSpace(z.ApprovalReference) || string.IsNullOrWhiteSpace(z.ConfigurationVersion))
            throw new InvalidOperationException("ConfiguredCoordinateBasisInvalid");
        return new(p.Id, p.Version, p.X, p.Y, p.Unit, p.Frame, z.Z);
    }
    private static DetectionStepTarget Target(RecipeStep step, FixedPoint point, string source, ApprovedFixedBasis basis) =>
        new(step.Sequence, step.MemberId ?? step.UnitId, step.SlotId!, step.PhysicalSlotIndex!.Value, step.LocalFace,
            step.CoordinateEpoch, step.Camera!, step.PointRef!, point, source, $"{basis.ApprovalReference}|{basis.ConfigurationVersion}")
        { ResolutionKind = CoordinateResolutionKind.ApprovedFixed, StageId = step.StageId, ScanPoseId = step.ScanPoseId };
}
