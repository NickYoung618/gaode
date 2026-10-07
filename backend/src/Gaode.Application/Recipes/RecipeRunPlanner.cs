namespace Gaode.Application.Recipes;

using System.Security.Cryptography;
using System.Text.Json;

public static class RecipePlanRevision
{
    public static string Compute(RecipeRunPlan plan) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(plan, new JsonSerializerOptions(JsonSerializerDefaults.Web))));
}

/// <summary>Plans only the single matched definition; it never reads the catalog.</summary>
public static class RecipeRunPlanner
{
    public static RecipeRunPlan BuildExecutable(RecipeDefinition definition, string trayRunId,
        IReadOnlyList<string> occupiedSlots)
    {
        var recipe = RecipeCatalogSnapshots.Freeze(definition);
        if (string.IsNullOrWhiteSpace(trayRunId)) throw new ArgumentException("TrayIdentityMissing", nameof(trayRunId));
        RecipeDefinitionValidator.Validate(recipe);
        if (occupiedSlots.Count == 0 || occupiedSlots.Any(string.IsNullOrWhiteSpace) ||
            occupiedSlots.Distinct(StringComparer.Ordinal).Count() != occupiedSlots.Count ||
            occupiedSlots.Any(s => !recipe.Positions.Any(p => p.SlotId == s)))
            throw new ArgumentException("OccupiedSlotsMustBeUniqueKnownAndNonempty", nameof(occupiedSlots));
        var admission = RecipeAdmission.Evaluate(recipe, occupiedSlots, recipe.Approval.Purpose);
        if (!admission.Eligible) throw new InvalidOperationException(admission.Reason);
        var orderedPositions = recipe.UnitKind != "looseGroup" && recipe.TrayLayout is { } layout
            ? layout.Ordered(RecipeTrayRegion.OK).Select(c => recipe.Positions.Single(p => p.CellId == c.CellId))
            : recipe.Positions.OrderBy(p => recipe.TrayLayout!.Cells.Where(c =>
                p.Members.Any(m => m.CellId == c.CellId)).Min(c => c.Row * 10 + c.Column));
        var units = orderedPositions.Where(p => occupiedSlots.Contains(p.SlotId, StringComparer.Ordinal))
            .Select(p => Unit.Create(trayRunId, p)).ToArray();
        var steps = new List<RecipeStep>();
        var epoch = 1;
        void Add(RecipeStepKind kind, Unit unit, Member? member = null, RecipeStage? stage = null,
            int? face = null, string? camera = null, string? point = null, string? capture = null,
            string? algorithm = null, RecipeExtraScanPose? scan = null, RecipeTargetPose? pose = null,
            string when = "Always") => steps.Add(new(steps.Count + 1, kind, unit.Id, member?.Id, unit.Slot,
                member?.Material, face, camera, stage?.AngleDeg, epoch, when, capture, algorithm)
            {
                OwnerStage = kind is RecipeStepKind.SortUnit or RecipeStepKind.ReturnUnit ? "Sorting" :
                    kind == RecipeStepKind.UnloadTray ? "Unload" : "Detection",
                PhysicalEntityId = recipe.UnitKind == "assembledEntity" ? unit.Id : member?.Id ?? unit.Id,
                PhysicalSlotIndex = recipe.UnitKind == "looseGroup" && member is not null ? member.PhysicalSlotIndex : unit.PhysicalSlotIndex, PointRef = point,
                StageId = stage is null ? null : RecipeStageIdentity.Create(stage.Number), ScanPoseId = scan?.PoseId,
                TargetPose = pose ?? scan?.TargetPose
            });
        void CaptureStage(RecipeStage stage, IEnumerable<Unit> batch)
        {
            foreach (var pair in stage.Targets.Select(t => t.CameraPair).Distinct(StringComparer.Ordinal))
            foreach (var camera in pair.Select(c => c.ToString()))
            foreach (var target in stage.Targets.Where(t => t.CameraPair == pair))
            foreach (var unit in batch)
            {
                var member = unit.Members.Single(m => m.Material == target.Material);
                var coordinate = recipe.ExecutionPositions[unit.Slot].ForDetection(member.Material).Coordinates.Single(c =>
                    c.StageId == RecipeStageIdentity.Create(stage.Number) && c.LocalFace == target.LocalFace && c.Camera == camera);
                var capture = recipe.SchemaVersion == RecipeDefinitionSerialization.HistoricalSchema
                    ? target.CaptureProfile : coordinate.CaptureProfile ?? throw new InvalidDataException("ProductCaptureProfileMissing");
                Add(RecipeStepKind.PositionForCapture, unit, member, stage, target.LocalFace, camera, coordinate.PointRef,
                    capture, target.AlgorithmProfile);
                Add(RecipeStepKind.Capture, unit, member, stage, target.LocalFace, camera, coordinate.PointRef,
                    capture, target.AlgorithmProfile);
            }
        }
        void ReadE(IEnumerable<Unit> batch, RecipeStage? stage, RecipeExtraScanPose? scan = null)
        {
            foreach (var unit in batch)
            {
                var member = unit.Members.Single(m => m.Material == recipe.ECode.RepresentativeMaterial);
                var face = stage?.Targets.Single(t => t.Material == member.Material).LocalFace;
                var point = scan?.ScanPointRef ?? recipe.ECode.ScanPointRef;
                var capture = recipe.SchemaVersion == RecipeDefinitionSerialization.HistoricalSchema
                    ? scan?.CaptureProfile ?? recipe.ECode.CaptureProfile
                    : recipe.ExecutionPositions[unit.Slot].ForDetection(member.Material).PurposePoints[point!].CaptureProfile
                        ?? throw new InvalidDataException("EntityCodeCaptureProfileMissing");
                foreach (var kind in new[] { RecipeStepKind.PositionForCapture, RecipeStepKind.ReadECode })
                    Add(kind, unit, member, stage, face, "E", point,
                        capture, scan?.AlgorithmProfile ?? recipe.ECode.AlgorithmProfile, scan);
            }
        }
        if (recipe.Route is "ordinaryBatch" or "ordinaryAssembly")
        {
            foreach (var stage in recipe.Stages)
            {
                if (stage.Action == "flipAffectedMembersOneByOne")
                {
                    foreach (var unit in units)
                    {
                        var members = recipe.UnitKind == "assembledEntity" ? new Member?[] { null } :
                            unit.Members.Where(m => stage.Targets.Any(t => t.Material == m.Material)).Cast<Member?>();
                        foreach (var member in members)
                        {
                            var transition = recipe.ExecutionPositions[unit.Slot].ForObject(recipe.UnitKind, member?.Material).Flip!.Stages[RecipeStageIdentity.Create(stage.Number)];
                            Add(RecipeStepKind.FlipMember, unit, member, stage, pose: transition.TargetPose);
                        }
                    }
                    epoch++;
                    Add(RecipeStepKind.RescanWholeTray, units[0], stage: stage);
                }
                CaptureStage(stage, units);
                if (ShouldReadE(recipe.ECode, stage.Number)) ReadE(units, stage);
            }
            if (recipe.ECode is { Enabled: true, ExtraPose: { } extra })
            {
                foreach (var unit in units)
                    Add(RecipeStepKind.FlipMember, unit, recipe.UnitKind == "assembledEntity" ? null :
                        unit.Members.Single(m => m.Material == recipe.ECode.RepresentativeMaterial), scan: extra);
                epoch++;
                Add(RecipeStepKind.RescanWholeTray, units[0], scan: extra);
                ReadE(units, null, extra);
            }
            foreach (var unit in units)
            {
                Add(RecipeStepKind.DecideUnit, unit);
                if (recipe.UnitKind == "assembledEntity") Add(RecipeStepKind.SortUnit, unit, when: "NG|Pending");
                else foreach (var member in recipe.UnitKind == "looseGroup" ? unit.Members : [unit.Members[0]])
                    Add(RecipeStepKind.SortUnit, unit, member, when: "NG|Pending");
            }
        }
        else
        {
            // Existing rotation semantics remain explicit; transport mapping and
            // actual admission must be present before this route can be dispatched.
            foreach (var unit in units)
            {
                var member = recipe.UnitKind == "independentPart" ? unit.Members[0] : null;
                Add(RecipeStepKind.TransferToRotation, unit, member);
                foreach (var stage in recipe.Stages)
                {
                    Add(RecipeStepKind.Rotate, unit, member, stage);
                    CaptureStage(stage, [unit]);
                    if (ShouldReadE(recipe.ECode, stage.Number)) ReadE([unit], stage);
                }
                Add(RecipeStepKind.DecideUnit, unit);
                Add(RecipeStepKind.ReturnUnit, unit, member, when: "OK");
                Add(RecipeStepKind.SortUnit, unit, member, when: "NG|Pending");
            }
        }
        Add(RecipeStepKind.UnloadTray, units[0]);
        return RecipeCatalogSnapshots.Freeze(new RecipeRunPlan(trayRunId, recipe.ScenarioId, recipe.FCode, recipe.RecipeId, recipe.Version,
            recipe.CatalogDigest, recipe.ReleaseStatus, recipe.UnitKind, 1, recipe.ECode.BindTo, recipe.ECode.RequiredForOk,
            recipe.Disposition, recipe.MotionProfile, recipe.QualityProfile, recipe.CaptureProfiles, ResolveSorting(recipe),
            recipe.Positions.Where(p => !occupiedSlots.Contains(p.SlotId, StringComparer.Ordinal)).Select(p => p.SlotId).ToArray(),
            steps, recipe.PlcRecipeId, recipe.NgCapacity, recipe.PendingCapacity)
        { Model = recipe.Model, SortingGripperId = recipe.SortingGripperId, DefinitionDigest = recipe.DefinitionDigest, ECode = recipe.ECode,
            InspectionKind = recipe.InspectionKind, TrayLayout = recipe.TrayLayout, TraySlotMapping = recipe.TraySlotMapping,
            RotationLoadingGripperId = recipe.RotationLoadingGripperId, RotationWorkstation = recipe.RotationWorkstation,
            OriginalSlots = recipe.InspectionKind == RecipeInspectionKind.SpecialRotation
                ? units.ToDictionary(u => u.Slot, u => {
                    var position = recipe.Positions.Single(p => p.SlotId == u.Slot);
                    var cell = recipe.TrayLayout!.Cells.Single(c => c.CellId == position.CellId);
                    return new OriginalSlotReference(Guid.Parse(trayRunId), cell.CellId, cell.Row, cell.Column,
                        u.Slot, u.PhysicalSlotIndex, u.Members[0].Id);
                }, StringComparer.Ordinal) : null,
            Approval = recipe.Approval, AlgorithmRequirements = recipe.AlgorithmRequirements });
    }

    private static IReadOnlyDictionary<string, SlotExecutionInputs> ResolveSorting(RecipeDefinition recipe)
    {
        if (recipe.SchemaVersion != RecipeDefinitionSerialization.CurrentSchema) return recipe.ExecutionPositions;
        ObjectExecutionInputs Resolve(ObjectExecutionInputs input) => input with {
            Sorting = input.SortingCellIds is null ? new Dictionary<string, HandlingPoint>() :
                input.SortingCellIds.ToDictionary(p => p.Key, p => recipe.SortingTargets![p.Value], StringComparer.Ordinal) };
        return recipe.ExecutionPositions.ToDictionary(p => p.Key, p => p.Value with {
            PhysicalEntity = Resolve(p.Value.PhysicalEntity),
            Members = p.Value.Members.ToDictionary(m => m.Key, m => Resolve(m.Value), StringComparer.Ordinal)
        }, StringComparer.Ordinal);
    }

    private static bool ShouldReadE(RecipeCodeRule rule, int stage) => rule.Enabled && rule.ExtraPose is null &&
        (rule.ReadAt == "firstAccessibleFace" && stage == 1 || rule.ReadAt == RecipeStageIdentity.Create(stage));
    private sealed record Member(string Id, string Material, int? PhysicalSlotIndex);
    private sealed record Unit(string Id, string Slot, int PhysicalSlotIndex, IReadOnlyList<Member> Members)
    {
        public static Unit Create(string runId, RecipePosition position)
        {
            var id = position.UnitPattern.Replace("{TrayRunId}", runId, StringComparison.Ordinal);
            return new(id, position.SlotId, position.PhysicalSlotIndex ?? throw new InvalidOperationException("ProductPhysicalSlotMappingMissing"),
                position.Members.Select(m => new Member(m.MemberPattern.Replace("{UnitId}", id, StringComparison.Ordinal), m.Material, m.PhysicalSlotIndex)).ToArray());
        }
    }
}
