using System.Text.Json;
using Gaode.Application.Recipes;
using Gaode.Contracts.Tests.Support;
using Xunit;

namespace Gaode.Contracts.Tests.Workflow;

public sealed class PublicTrayAffected016Tests
{
    [Fact]
    public void LooseMembersKeepIndependentCellsThroughSaveSerializationFreezeAndPlanning()
    {
        var path=Path.Combine(TestConfiguration.Workspace(),"specs/016-public-preparation-tray-check-unload/examples/mixed/catalog.json");
        using var doc=JsonDocument.Parse(File.ReadAllText(path));
        var original=RecipeDefinitionSerialization.Deserialize(doc.RootElement.GetProperty("definitions")[0].GetRawText());
        var materials=new[]{"upper","middle","lower"};
        var members=original.Positions.Select((p,i)=>new RecipeMember(materials[i],"{UnitId}/"+materials[i],"individual") {
            CellId=p.CellId,PhysicalSlotIndex=p.PhysicalSlotIndex }).ToArray();
        var objects=original.Positions.Select((p,i)=>KeyValuePair.Create(materials[i],original.ExecutionPositions[p.SlotId].PhysicalEntity with {
            Coordinates=original.ExecutionPositions[p.SlotId].PhysicalEntity.Coordinates.Select(c=>c with {
                SlotId="group",ObjectPattern=members[i].MemberPattern }).ToArray() })).ToDictionary();
        var recipe=original with { UnitKind="looseGroup",PrimaryMaterial="upper",
            Positions=[new("group","{TrayRunId}/group",members) { CellId=members[0].CellId,PhysicalSlotIndex=members[0].PhysicalSlotIndex }],
            Composition=materials.Select(m=>new RecipeMaterial(m,[1])).ToArray(),
            Stages=[original.Stages[0] with { Targets=materials.Select(m=>original.Stages[0].Targets[0] with {Material=m}).ToArray() }],
            ExecutionPositions=new Dictionary<string,SlotExecutionInputs> { ["group"]=new("group",original.ExecutionPositions["s1"].PhysicalEntity,objects) },
            Disposition=original.Disposition with {PhysicalUnit="problemMembersOnly"},
            Approval=original.Approval with {AllowedSlots=["group"]} };
        Assert.True(RecipeDefinitionValidator.ValidateForSave(recipe,new(RecipeCatalogSnapshot.CurrentSchema,"test",[])).Valid);
        var saved=RecipeDefinitionSerialization.Deserialize(RecipeDefinitionSerialization.Serialize(recipe));
        var plan=RecipeRunPlanner.BuildExecutable(saved,Guid.NewGuid().ToString("D"),["group"]);
        Assert.Equal([1,2,3],plan.Steps.Where(s=>s.Kind==RecipeStepKind.Capture).Select(s=>s.PhysicalSlotIndex!.Value).Distinct().Order());
        Assert.Equal(3,plan.Steps.Where(s=>s.Kind==RecipeStepKind.SortUnit).Select(s=>s.MemberId).Distinct().Count());
        Assert.Equal(3,saved.ExecutionPositions["group"].Members.Values.Select(x=>x.Coordinates[0].Point.X).Distinct().Count());
        var invalid=recipe with {Positions=[recipe.Positions[0] with {Members=[members[0],members[1] with {CellId=members[0].CellId,PhysicalSlotIndex=1},members[2]]}]};
        Assert.False(RecipeDefinitionValidator.ValidateForSave(invalid,new(RecipeCatalogSnapshot.CurrentSchema,"test",[])).Valid);
        var assembled=recipe with { UnitKind="assembledEntity",Route="ordinaryAssembly",Capacity=1,
            TrayLayout=recipe.TrayLayout! with {Cells=recipe.TrayLayout.Cells.Where(c=>c.Region!=RecipeTrayRegion.OK||c.CellId==members[0].CellId).ToArray()},
            TraySlotMapping=recipe.TraySlotMapping! with {Bindings=[recipe.TraySlotMapping.Bindings[0]]},
            Disposition=recipe.Disposition with {PhysicalUnit="wholeAssembly"},
            ExecutionPositions=new Dictionary<string,SlotExecutionInputs> { ["group"]=recipe.ExecutionPositions["group"] with {
                Members=objects.ToDictionary(p=>p.Key,p=>p.Value with {Coordinates=p.Value.Coordinates.Select(c=>c with {PhysicalSlotIndex=1}).ToArray()}) } } };
        Assert.True(RecipeDefinitionValidator.ValidateForSave(assembled,new(RecipeCatalogSnapshot.CurrentSchema,"test",[])).Valid);
        var assemblyPlan=RecipeRunPlanner.BuildExecutable(assembled,Guid.NewGuid().ToString("D"),["group"]);
        var sort=Assert.Single(assemblyPlan.Steps,s=>s.Kind==RecipeStepKind.SortUnit);
        Assert.Null(sort.MemberId);
        Assert.All(assemblyPlan.Steps.Where(s=>s.Kind==RecipeStepKind.Capture),s=>Assert.Equal(sort.UnitId,s.PhysicalEntityId));
    }
}
