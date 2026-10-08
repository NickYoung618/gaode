namespace Gaode.Application.Recipes;

/// <summary>One business validator for candidates, saved definitions and execution.</summary>
public static class RecipeDefinitionValidator
{
    public static string AuthoringRoute(string unitKind,RecipeInspectionKind kind) => (unitKind,kind) switch {
        ("independentPart",RecipeInspectionKind.SpecialRotation)=>"specialType1Part",
        ("independentPart" or "looseGroup",RecipeInspectionKind.Ordinary)=>"ordinaryBatch",
        ("assembledEntity",RecipeInspectionKind.Ordinary)=>"ordinaryAssembly",
        _=>throw new System.Text.Json.JsonException("场景和检测类型不相容。")
    };

    public static RecipeValidationResult ValidateForSave(RecipeDefinition candidate, RecipeCatalogSnapshot current)
    {
        try
        {
            Require(candidate.SchemaVersion == RecipeDefinitionSerialization.CurrentSchema,
                "RecipeCurrentSchemaRequiredForSave", "schemaVersion");
            Require(candidate.UnitKind == "looseGroup" || candidate.SortingGripperId is 1 or 2, "RecipeSortingGripperRequired", "sortingGripperId");
            ValidateStructure(candidate);
            Require(!current.Definitions.Any(r => r.FCode == candidate.FCode &&
                (string.IsNullOrEmpty(candidate.RecipeId) || r.RecipeId != candidate.RecipeId)),
                "RecipeFCodeOccupied", "fCode");
            var issue = ExecutionIssue(candidate, candidate.Positions.Select(p => p.SlotId), forSave: true);
            return new(issue is null ? [] : [issue]);
        }
        catch (InvalidRecipe error) { return new([error.Issue]); }
    }

    public static void Validate(RecipeDefinition recipe)
    {
        try { ValidateStructure(recipe); }
        catch (InvalidRecipe error) { throw new InvalidDataException($"{recipe.RecipeId}:{error.Issue.Code}", error); }
    }

    private static void ValidateStructure(RecipeDefinition recipe)
    {
        Require(recipe.SchemaVersion is RecipeDefinitionSerialization.CurrentSchema or RecipeDefinitionSerialization.LayoutHistoricalSchema or RecipeDefinitionSerialization.PreviousSchema or RecipeDefinitionSerialization.HistoricalSchema,
            "RecipeDefinitionSchemaUnsupported", "schemaVersion");
        if (recipe.SchemaVersion != RecipeDefinitionSerialization.HistoricalSchema && recipe.UnitKind != "looseGroup")
            Require(recipe.SortingGripperId is 1 or 2, "RecipeSortingGripperRequired", "sortingGripperId");
        if (recipe.SchemaVersion == RecipeDefinitionSerialization.CurrentSchema) ValidateLayout(recipe);
        Require(recipe.Capacity > 0, "RecipeCapacityInvalid", "capacity");
        Require(new[] { recipe.Model, recipe.FCode, recipe.ScenarioId, recipe.LayoutProfile, recipe.MotionProfile, recipe.QualityProfile }
            .All(v => !string.IsNullOrWhiteSpace(v)), "RecipeIdentityOrConfigurationMissing", "model");
        Require(recipe.Composition.Count > 0 && recipe.Composition.Select(m => m.Material).Distinct(StringComparer.Ordinal).Count() == recipe.Composition.Count &&
            recipe.Composition.All(m => !string.IsNullOrWhiteSpace(m.Material) && m.LocalFaces.Count > 0 && m.LocalFaces.All(f => f > 0) &&
                m.LocalFaces.Distinct().Count() == m.LocalFaces.Count), "RecipeCompositionInvalid", "composition");
        Require(recipe.Composition.All(m => m.SortingGripperId is null or 1 or 2),
            "RecipeMemberGripperInvalid", "composition");
        var materials = recipe.Composition.ToDictionary(m => m.Material, StringComparer.Ordinal);
        Require(materials.ContainsKey(recipe.PrimaryMaterial), "RecipePrimaryMaterialMissing", "primaryMaterial");
        Require((recipe.UnitKind == "looseGroup" && recipe.SchemaVersion == RecipeDefinitionSerialization.CurrentSchema
                ? recipe.Positions.Sum(p => p.Members.Count) : recipe.Positions.Count) == recipe.Capacity &&
            recipe.Positions.Select(p => p.SlotId).Distinct(StringComparer.Ordinal).Count() == recipe.Positions.Count &&
            recipe.Positions.All(p => !string.IsNullOrWhiteSpace(p.SlotId) && p.Members.Count == materials.Count &&
                p.Members.Select(m => m.Material).ToHashSet(StringComparer.Ordinal).SetEquals(materials.Keys) &&
                p.UnitPattern.Contains("{TrayRunId}", StringComparison.Ordinal) &&
                p.Members.All(m => m.MemberPattern.Contains("{UnitId}", StringComparison.Ordinal)) &&
                p.Members.Select(m => m.MemberPattern).Distinct(StringComparer.Ordinal).Count() == p.Members.Count), "RecipePositionsInvalid", "positions");
        Require(recipe.Positions.All(p => recipe.SchemaVersion == RecipeDefinitionSerialization.CurrentSchema
                ? p.PhysicalSlotIndex is null or > 0
                : p.PhysicalSlotIndex >= 1 && p.PhysicalSlotIndex <= recipe.Capacity) &&
            !recipe.Positions.Where(p => p.PhysicalSlotIndex is not null).GroupBy(p => p.PhysicalSlotIndex).Any(g => g.Count() > 1),
            "RecipePhysicalSlotInvalid", "positions");
        Require(recipe.Route switch
        {
            "ordinaryBatch" => recipe.UnitKind is "independentPart" or "looseGroup",
            "ordinaryAssembly" or "specialType1WholeAssembly" => recipe.UnitKind == "assembledEntity",
            "specialType1Part" => recipe.UnitKind == "independentPart",
            _ => false
        }, "RecipeRouteInvalid", "route");
        Require(recipe.Stages.Count > 0 && recipe.Stages.Select((s, i) => s.Number == i + 1 && s.Targets.Count > 0).All(x => x),
            "RecipeStageOrderInvalid", "stages");
        var targets = recipe.Stages.SelectMany(s => s.Targets).ToArray();
        Require(targets.All(t => materials.TryGetValue(t.Material, out var m) && m.LocalFaces.Contains(t.LocalFace) &&
            t.CameraPair is "AB" or "CD" && recipe.CaptureProfiles.ContainsKey(t.CaptureProfile) &&
            recipe.AlgorithmRequirements.TryGetValue(t.AlgorithmProfile, out var algorithm) && algorithm.Purpose == AlgorithmPurpose.SingleDetection &&
            recipe.AlgorithmRequirements.TryGetValue(t.AlgorithmProfile + "/fusion", out var fusion) && fusion.Purpose == AlgorithmPurpose.FaceFusion),
            "RecipeTargetInvalid", "stages");
        var expected = recipe.Composition.SelectMany(m => m.LocalFaces.Select(f => (m.Material, f))).ToHashSet();
        var special = recipe.InspectionKind == RecipeInspectionKind.SpecialRotation;
        Require((special || targets.Length == expected.Count) && targets.Select(t => (t.Material, t.LocalFace)).ToHashSet().SetEquals(expected),
            "RecipeRequiredTargetsIncompleteOrDuplicate", "stages");
        Require(special || targets.GroupBy(t => t.Material).All(g => g.Count() != 4 || g.Count(t => t.CameraPair == "AB") == 1),
            "FourFaceRequiresOneABThreeCD", "stages");
        Require(recipe.CaptureProfiles.All(p => p.Key == p.Value.Id && !string.IsNullOrWhiteSpace(p.Value.Version) &&
            p.Value.Settings is { ExposureUs: > 0 } settings &&
            double.IsFinite(settings.Gain) && settings.Gain > 0 && settings.RoiPixels.Length == 4 &&
            settings.RoiPixels.All(v => v >= 0) && settings.RoiPixels[2] > 0 && settings.RoiPixels[3] > 0 &&
            (recipe.LightExecution?.IsSimulated == true ||
                !string.IsNullOrWhiteSpace(settings.LightChannel) && settings.BrightnessPercent is >= 0 and <= 100 &&
                settings.SettleMs is >= 0 and <= 1000)), "RecipeCaptureProfileInvalid", "captureProfiles");
        Require(recipe.CommissioningFPosition is null || recipe.CommissioningFPosition is { SchemaVersion: "commissioning-f-position/1", X: { } fx, Y: { } fy } &&
            double.IsFinite(fx) && double.IsFinite(fy), "CommissioningFPositionInvalid", "commissioningFPosition");
        Require(recipe.LightExecution is null || recipe.LightExecution.IsValid, "RecipeLightModeInvalid", "lightExecution");
        Require(recipe.AlgorithmRequirements.All(p => p.Key == p.Value.Id &&
            !string.IsNullOrWhiteSpace(p.Value.ParametersVersion) && !string.IsNullOrWhiteSpace(p.Value.ResultContract) &&
            p.Value.InputCount == (p.Value.Purpose == AlgorithmPurpose.FaceFusion ? 2 : 1)), "RecipeAlgorithmRequirementInvalid", "algorithmRequirements");
        var ordinary = recipe.Route is "ordinaryBatch" or "ordinaryAssembly";
        Require(recipe.Stages.Select((s, i) => ordinary
            ? s.Action == (i == 0 ? "none" : "flipAffectedMembersOneByOne")
            : s.Action == (special ? "rotate" : i == 0 ? "transferWholeAssemblyToRotationStationAndRotate" : "rotateWholeAssembly") &&
                s.AngleDeg is { } angle && double.IsFinite(angle)).All(x => x), "RecipeStageSemanticsInvalid", "stages");
        var e = recipe.ECode;
        if (e.Enabled)
        {
            Require(e.RepresentativeMaterial is { } representative && materials.ContainsKey(representative) &&
                !string.IsNullOrWhiteSpace(e.BindTo), "RecipeEntityCodeRuleInvalid", "eCode");
            if (e.ExtraPose is { } extra)
                Require(ordinary && materials[e.RepresentativeMaterial!].LocalFaces.Count == 4 && e.ReadAt is null &&
                    e.ScanPointRef is null && !string.IsNullOrWhiteSpace(extra.PoseId) && ValidPose(extra.TargetPose) &&
                    extra.TargetPose.ProfileId == recipe.MotionProfile, "RecipeExtraPoseInvalid", "eCode.extraPose");
            else Require(e.ReadAt == "firstAccessibleFace" || recipe.Stages.Any(s => e.ReadAt == RecipeStageIdentity.Create(s.Number)),
                "RecipeEntityCodeRuleInvalid", "eCode.readAt");
            var capture = e.ExtraPose?.CaptureProfile ?? e.CaptureProfile;
            var algorithm = e.ExtraPose?.AlgorithmProfile ?? e.AlgorithmProfile;
            Require(capture is not null && recipe.CaptureProfiles.ContainsKey(capture) && algorithm is not null &&
                recipe.AlgorithmRequirements.TryGetValue(algorithm, out var requirement) && requirement.Purpose == AlgorithmPurpose.EntityCode,
                "EntityCodeCapabilityMissing", "eCode");
        }
        else Require(e.ExtraPose is null && e.ScanPointRef is null, "DisabledEntityCodeHasExtraPose", "eCode.extraPose");
        var physical = recipe.UnitKind switch
        {
            "independentPart" => "singlePart", "looseGroup" => "allGroupMembersIndividuallyToSameReservedGroupCell",
            "assembledEntity" => "wholeAssembly", _ => ""
        };
        Require(recipe.Disposition.PhysicalUnit == physical || recipe.UnitKind == "looseGroup" &&
            recipe.Disposition.PhysicalUnit == "problemMembersOnly", "RecipeDispositionUnitInvalid", "disposition.physicalUnit");
        Require(recipe.Disposition.Ok == "originalSlot", "RecipeOkMustRemainInOriginalSlot", "disposition.ok");
        Require(recipe.SchemaVersion == RecipeDefinitionSerialization.CurrentSchema ||
            (recipe.NgCapacity is null && recipe.PendingCapacity is null) ||
            recipe.NgCapacity > 0 && recipe.PendingCapacity > 0, "RecipeZoneCapacityInvalid", "ngCapacity");
    }

    public static string? ExecutionProblem(RecipeDefinition recipe, IEnumerable<string> selectedSlots) =>
        ExecutionIssue(recipe, selectedSlots)?.Code;

    private static RecipeValidationIssue? ExecutionIssue(RecipeDefinition recipe, IEnumerable<string> selectedSlots, bool forSave = false)
    {
        if (!forSave && recipe.SchemaVersion != RecipeDefinitionSerialization.CurrentSchema)
            return new("RecipeNeedsLayoutMigration", "trayLayout", "历史配方需明确配置实际布局及适用抓手后才能新绑定；原冻结运行按原版本继续。");
        if (recipe.UnitKind == "looseGroup")
            foreach (var member in recipe.Composition)
                if (member.SortingGripperId is not (1 or 2))
                    return new("RecipeMemberGripperRequired", "composition", $"请配置零件{member.Material}的分拣夹爪（1或2）。", member.Material);
        foreach (var slot in selectedSlots)
        {
            RecipeValidationIssue Issue(string code, string suffix = "") => new(code,
                $"executionPositions.{slot}{suffix}", code, slot);
            var position = recipe.Positions.SingleOrDefault(p => p.SlotId == slot);
            if (position is null || !recipe.ExecutionPositions.TryGetValue(slot, out var input) || input.SlotId != slot)
                return Issue("ProductPointMappingMissing");
            if (!forSave && position.PhysicalSlotIndex is not > 0)
                return Issue("ProductPhysicalSlotMappingMissing");
            foreach (var stage in recipe.Stages)
            foreach (var target in stage.Targets)
            {
                var member = position.Members.Single(m => m.Material == target.Material);
                if (recipe.UnitKind != "independentPart" && !input.Members.ContainsKey(target.Material))
                    return Issue("ProductMemberTargetMissing");
                var physical = input.ForDetection(target.Material);
                if (physical.Coordinates.Select(c => c.PointRef).Distinct(StringComparer.Ordinal).Count() != physical.Coordinates.Count)
                    return Issue("ProductPointReferenceDuplicate", ".coordinates");
                foreach (var camera in target.CameraPair.Select(c => c.ToString()))
                {
                    var matching = physical.Coordinates.Where(c => c.LocalFace == target.LocalFace && c.Camera == camera && c.StageId == RecipeStageIdentity.Create(stage.Number)).ToArray();
                    if (matching.Length != 1) return Issue("ProductTargetMissingOrDuplicate", ".coordinates");
                    var c = matching[0];
                    if (recipe.SchemaVersion != RecipeDefinitionSerialization.HistoricalSchema &&
                        (c.CaptureProfile is null || !recipe.CaptureProfiles.ContainsKey(c.CaptureProfile)))
                        return Issue("ProductCaptureProfileMissing", ".coordinates");
                    if (c.ObjectPattern != member.MemberPattern || c.SlotId != slot || c.PhysicalSlotIndex !=
                        (recipe.UnitKind == "looseGroup" ? member.PhysicalSlotIndex : position.PhysicalSlotIndex) ||
                        string.IsNullOrWhiteSpace(c.PointRef) || string.IsNullOrWhiteSpace(c.ConfigurationVersion))
                        return Issue("ProductTargetIdentityMismatch", ".coordinates");
                    if (!ValidPoint(c.Point, c.Fixed) || string.IsNullOrWhiteSpace(c.SourceFactReference))
                        return Issue("ProductTargetSourceMissing", ".coordinates");
                }
                if (recipe.UnitKind == "looseGroup" && !input.Members.ContainsKey(target.Material)) return Issue("ProductMemberTargetMissing");
                var handling = input.ForObject(recipe.UnitKind, target.Material);
                if (!ValidHandling(handling.Source)) return Issue("ProductSourcePointMissing");
                if (recipe.SchemaVersion == RecipeDefinitionSerialization.CurrentSchema)
                {
                    if (handling.Sorting.Count != 0 || handling.Rotation is not null)
                        return Issue("ProductLegacyHandlingNotWritable");
                    foreach (var region in new[] { RecipeTrayRegion.NG, RecipeTrayRegion.Pending })
                    {
                        var key = region.ToString();
                        if (handling.SortingCellIds is null || !handling.SortingCellIds.TryGetValue(key, out var cell) ||
                            recipe.TrayLayout!.Cells.SingleOrDefault(c => c.CellId == cell)?.Region != region ||
                            recipe.SortingTargets is null || !recipe.SortingTargets.TryGetValue(cell, out var targetPoint) || !ValidHandling(targetPoint))
                            return Issue("ProductSortingTargetMissing");
                    }
                    if (recipe.InspectionKind == RecipeInspectionKind.SpecialRotation && !ValidHandling(handling.OriginPutBack))
                        return Issue("ProductOriginPutBackMissing");
                }
                else if (!handling.Sorting.TryGetValue("NG", out var ng) || !ValidHandling(ng) ||
                    !handling.Sorting.TryGetValue("Pending", out var pending) || !ValidHandling(pending)) return Issue("ProductSortingTargetMissing");
                if (stage.Action == "flipAffectedMembersOneByOne")
                {
                    if (handling.Flip is null || !handling.Flip.Stages.TryGetValue(RecipeStageIdentity.Create(stage.Number), out var flip) ||
                        !ValidPose(flip.TargetPose) || flip.TargetPose.ProfileId != recipe.MotionProfile)
                        return Issue("ProductFlipTargetMissing", ".flip");
                    if (!ValidPurpose(handling, flip.PickPointRef, RecipePointPurpose.FlipPick) ||
                        !ValidPurpose(handling, flip.PutBackPointRef, RecipePointPurpose.FlipPutBack)) return Issue("ProductFlipPointInvalid", ".purposePoints");
                }
                if (recipe.SchemaVersion != RecipeDefinitionSerialization.CurrentSchema &&
                    recipe.Route is "specialType1Part" or "specialType1WholeAssembly" && input.PhysicalEntity.Rotation is null)
                    return Issue("ProductRotationTargetMissing");
            }
            if (recipe.ECode.Enabled)
            {
                var e = recipe.ECode;
                var handling = input.ForObject(recipe.UnitKind, e.RepresentativeMaterial);
                if (!ValidPurpose(input.ForDetection(e.RepresentativeMaterial), e.ExtraPose?.ScanPointRef ?? e.ScanPointRef, RecipePointPurpose.EScan))
                    return Issue("EntityCodePointInvalid", ".purposePoints");
                if (recipe.SchemaVersion != RecipeDefinitionSerialization.HistoricalSchema)
                {
                    var point = input.ForDetection(e.RepresentativeMaterial).PurposePoints[e.ExtraPose?.ScanPointRef ?? e.ScanPointRef!];
                    if (point.CaptureProfile is null || !recipe.CaptureProfiles.ContainsKey(point.CaptureProfile))
                        return Issue("EntityCodeCaptureProfileMissing", ".purposePoints");
                }
                if (e.ExtraPose is { } extra && (!ValidPurpose(handling, extra.PickPointRef, RecipePointPurpose.FlipPick) ||
                    !ValidPurpose(handling, extra.PutBackPointRef, RecipePointPurpose.FlipPutBack)))
                    return Issue("EntityCodePosePointInvalid", ".purposePoints");
            }
        }
        return null;
    }

    private static void ValidateLayout(RecipeDefinition recipe)
    {
        Require(recipe.TrayLayout is { Rows: 10, Columns: 10 }, "RecipeTrayLayoutRequired", "trayLayout");
        var cells = recipe.TrayLayout!.Cells;
        Require(cells.All(c => c.Row is >= 1 and <= 10 && c.Column is >= 1 and <= 10 &&
            c.CellId == RecipeTrayLayout.CellIdentity(c.Row, c.Column) && Enum.IsDefined(c.Region)) &&
            cells.Select(c => c.CellId).Distinct(StringComparer.Ordinal).Count() == cells.Count,
            "RecipeTrayCellInvalidOrDuplicate", "trayLayout.cells");
        var ok = cells.Where(c => c.Region == RecipeTrayRegion.OK).Select(c => c.CellId).ToHashSet(StringComparer.Ordinal);
        Require(recipe.Capacity == ok.Count && recipe.NgCapacity == cells.Count(c => c.Region == RecipeTrayRegion.NG) &&
            recipe.PendingCapacity == cells.Count(c => c.Region == RecipeTrayRegion.Pending), "RecipeTrayCountsMismatch", "trayLayout");
        var physicalCells = recipe.UnitKind == "looseGroup" ? recipe.Positions.SelectMany(p => p.Members).Select(m => m.CellId).ToArray()
            : recipe.Positions.Select(p => p.CellId).ToArray();
        Require(physicalCells.All(c => c is not null && ok.Contains(c)) &&
            physicalCells.Distinct(StringComparer.Ordinal).Count() == physicalCells.Length && physicalCells.Length == ok.Count,
            "RecipeTrayPositionLinkInvalid", "positions");
        Require(recipe.InspectionKind switch
        {
            RecipeInspectionKind.Ordinary => recipe.UnitKind is "independentPart" or "looseGroup"
                ? recipe.Route == "ordinaryBatch" : recipe.UnitKind == "assembledEntity" && recipe.Route == "ordinaryAssembly",
            RecipeInspectionKind.SpecialRotation => recipe.UnitKind == "independentPart" && recipe.Route == "specialType1Part",
            _ => false
        }, "RecipeInspectionKindRouteMismatch", "inspectionKind");
        if (recipe.InspectionKind == RecipeInspectionKind.SpecialRotation)
        {
            Require(recipe.RotationLoadingGripperId is 1 or 2, "RecipeRotationLoadingGripperRequired", "rotationLoadingGripperId");
            Require(recipe.RotationWorkstation is { } station && ValidHandling(station.Place) && ValidHandling(station.Pick),
                "RecipeRotationWorkstationRequired", "rotationWorkstation");
            Require(recipe.Stages.Count == 2 && recipe.Stages.All(s => s.Targets.Count == 1),
                "RecipeRotationTwoGroupsRequired", "stages");
        }
        else Require(recipe.RotationLoadingGripperId is null && recipe.RotationWorkstation is null,
            "RecipeOrdinaryHasRotationConfiguration", "rotationWorkstation");
        Require(recipe.SortingTargets is not null && recipe.SortingTargets.All(t =>
            cells.Any(c => c.CellId == t.Key && c.Region != RecipeTrayRegion.OK) && ValidHandling(t.Value)) &&
            cells.Where(c => c.Region != RecipeTrayRegion.OK).All(c => recipe.SortingTargets.ContainsKey(c.CellId)),
            "RecipeTraySortingTargetInvalid", "sortingTargets");
        if (recipe.TraySlotMapping is { } mapping)
        {
            Require(new[] { mapping.Id, mapping.Version, mapping.EvidenceReference }.All(s => !string.IsNullOrWhiteSpace(s)) &&
                mapping.Bindings.All(b => ok.Contains(b.CellId) && b.PhysicalSlotIndex > 0) &&
                mapping.Bindings.Select(b => b.CellId).Distinct().Count() == mapping.Bindings.Count &&
                mapping.Bindings.Select(b => b.PhysicalSlotIndex).Distinct().Count() == mapping.Bindings.Count,
                "RecipeTrayPhysicalMappingInvalid", "traySlotMapping");
        }
        Require(recipe.Positions.All(p => p.PhysicalSlotIndex is null ||
            recipe.TraySlotMapping?.Bindings.Any(b => b.CellId == p.CellId && b.PhysicalSlotIndex == p.PhysicalSlotIndex) == true),
            "RecipeTrayPhysicalMappingMissingOrMismatch", "traySlotMapping");
        if (recipe.UnitKind == "looseGroup")
            Require(recipe.Positions.SelectMany(p => p.Members).All(m => m.PhysicalSlotIndex is > 0 &&
                recipe.TraySlotMapping?.Bindings.Any(b => b.CellId == m.CellId && b.PhysicalSlotIndex == m.PhysicalSlotIndex) == true),
                "RecipeMemberPhysicalMappingMissingOrMismatch", "positions.members");
    }

    private static bool ValidPose(RecipeTargetPose pose) =>
        new[] { pose.ProfileId, pose.ProfileVersion, pose.PoseKey }.All(v => !string.IsNullOrWhiteSpace(v));
    private static bool ValidPoint(PlanarPoint point, ApprovedFixedBasis z) =>
        double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(z.Z) && point.Unit == "mm" &&
        z.Unit == point.Unit && z.Datum == point.Frame &&
        new[] { point.Id, point.Version, point.Frame, z.ApprovalReference, z.ConfigurationVersion }.All(v => !string.IsNullOrWhiteSpace(v));
    private static bool ValidHandling(HandlingPoint? point) => point is not null &&
        ValidPoint(new(point.Point.Id, point.Point.Version, point.Point.X, point.Point.Y, point.Point.Unit, point.Point.Frame),
            new(point.Point.Z, point.Point.Unit, point.Point.Frame, point.CoordinateEvidenceReference, point.Point.Version));
    private static bool ValidPurpose(ObjectExecutionInputs input, string? reference, RecipePointPurpose purpose) =>
        reference is not null && input.PurposePoints.TryGetValue(reference, out var point) && point.Purpose == purpose &&
        ValidPoint(point.Point, point.Fixed) && !string.IsNullOrWhiteSpace(point.CoordinateEvidenceReference);
    private static void Require(bool condition, string code, string path)
    {
        if (!condition) throw new InvalidRecipe(new(code, path, code));
    }
    private sealed class InvalidRecipe(RecipeValidationIssue issue) : Exception(issue.Code)
    {
        public RecipeValidationIssue Issue { get; } = issue;
    }
}
