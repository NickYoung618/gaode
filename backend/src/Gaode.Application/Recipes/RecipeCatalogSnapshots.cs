using System.Collections.ObjectModel;

namespace Gaode.Application.Recipes;

/// <summary>Copies caller-owned collections before exposing a catalog or frozen run.</summary>
public static class RecipeCatalogSnapshots
{
    public static RecipeCatalogSnapshot Create(string catalogDigest, IEnumerable<RecipeDefinition> definitions) =>
        new(RecipeCatalogSnapshot.CurrentSchema, catalogDigest, List(definitions.Select(Freeze)));

    public static RecipeDefinition Freeze(RecipeDefinition definition) => definition with
    {
        TrayLayout = Layout(definition.TrayLayout), TraySlotMapping = Mapping(definition.TraySlotMapping, definition.TrayLayout),
        SortingTargets = definition.SortingTargets is null ? null : Map(definition.SortingTargets),
        Positions = List(definition.Positions.Select(p => p with { Members = List(p.Members) })),
        Composition = List(definition.Composition.Select(m => m with { LocalFaces = List(m.LocalFaces) })),
        Stages = List(definition.Stages.Select(s => s with { Targets = List(s.Targets) })),
        CaptureProfiles = Map(definition.CaptureProfiles),
        ExecutionPositions = Positions(definition.ExecutionPositions),
        AlgorithmRequirements = Map(definition.AlgorithmRequirements),
        Approval = definition.Approval with { AllowedSlots = List(definition.Approval.AllowedSlots) }
    };

    public static RecipeRunPlan Freeze(RecipeRunPlan plan) => plan with
    {
        TrayLayout = Layout(plan.TrayLayout), TraySlotMapping = Mapping(plan.TraySlotMapping, plan.TrayLayout),
        SortingGrippersByMaterial = plan.SortingGrippersByMaterial is null ? null : Map(plan.SortingGrippersByMaterial),
        OriginalSlots = plan.OriginalSlots is null ? null : Map(plan.OriginalSlots),
        CaptureProfiles = Map(plan.CaptureProfiles),
        ExecutionPositions = Positions(plan.ExecutionPositions),
        AlgorithmRequirements = Map(plan.AlgorithmRequirements),
        Approval = plan.Approval with { AllowedSlots = List(plan.Approval.AllowedSlots) },
        MissingSlots = List(plan.MissingSlots), Steps = List(plan.Steps)
    };

    internal static IReadOnlyDictionary<string, T> Map<T>(IEnumerable<KeyValuePair<string, T>> values) =>
        new ReadOnlyDictionary<string, T>(values.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal));
    private static IReadOnlyList<T> List<T>(IEnumerable<T> values) => Array.AsReadOnly(values.ToArray());
    private static RecipeTrayLayout? Layout(RecipeTrayLayout? value) => value is null ? null : value with
    { Cells = List(value.Cells.OrderBy(c => c.Row).ThenBy(c => c.Column)) };
    private static RecipeTraySlotMapping? Mapping(RecipeTraySlotMapping? value, RecipeTrayLayout? layout) => value is null ? null : value with
    { Bindings = List(value.Bindings.OrderBy(b => layout?.Cells.FirstOrDefault(c => c.CellId == b.CellId)?.Row)
        .ThenBy(b => layout?.Cells.FirstOrDefault(c => c.CellId == b.CellId)?.Column)) };
    private static IReadOnlyDictionary<string, SlotExecutionInputs> Positions(IReadOnlyDictionary<string, SlotExecutionInputs> values) =>
        Map(values.Select(p => KeyValuePair.Create(p.Key, p.Value with
        {
            PhysicalEntity = Object(p.Value.PhysicalEntity),
            Members = Map(p.Value.Members.Select(m => KeyValuePair.Create(m.Key, Object(m.Value))))
        })));
    private static ObjectExecutionInputs Object(ObjectExecutionInputs value) => value with
    {
        Coordinates = List(value.Coordinates),
        Flip = value.Flip is null ? null : value.Flip with { Stages = Map(value.Flip.Stages) },
        Sorting = Map(value.Sorting), PurposePoints = Map(value.PurposePoints),
        SortingCellIds = value.SortingCellIds is null ? null : Map(value.SortingCellIds),
        Rotation = value.Rotation is null ? null : value.Rotation with
        {
            Poses = new ReadOnlyDictionary<int, AuxiliaryTarget>(value.Rotation.Poses.ToDictionary(p => p.Key, p => p.Value)),
            Exits = Map(value.Rotation.Exits)
        }
    };
}
