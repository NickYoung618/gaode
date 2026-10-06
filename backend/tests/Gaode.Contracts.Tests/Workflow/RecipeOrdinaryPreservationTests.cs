using Gaode.Application.Recipes;
using Gaode.Contracts.Tests.Recipes;
using Xunit;
namespace Gaode.Contracts.Tests.Workflow;
// Planning component only. The ordinary actual motion/capture/save rhythm is checked by the shared representative.
public sealed class RecipeOrdinaryPreservationTests
{
    [Theory]
    [InlineData("looseGroup")]
    [InlineData("assembledEntity")]
    public void OrdinaryStagesKeepOrderedCellsAndIndependentMembersVersusWholeAssembly(string unitKind)
    {
        var basis=Recipe011Data.ForSlots(2,1,2);var materials=new[]{"base","shaft"};
        var recipe=basis with {UnitKind=unitKind,PrimaryMaterial="base",Route=unitKind=="assembledEntity"?"ordinaryAssembly":"ordinaryBatch",
            Disposition=basis.Disposition with {PhysicalUnit=unitKind=="assembledEntity"?"wholeAssembly":"problemMembersOnly"},
            Positions=basis.Positions.Reverse().Select(p=>p with {Members=materials.Select(m=>new RecipeMember(m,"{UnitId}/"+m,unitKind=="assembledEntity"?"attached":"individual")).ToArray()}).ToArray(),
            Composition=materials.Select(m=>new RecipeMaterial(m,[1,2])).ToArray(),
            Stages=basis.Stages.Select(s=>s with {Targets=materials.Select(m=>s.Targets[0] with {Material=m}).ToArray()}).ToArray(),
            ExecutionPositions=basis.ExecutionPositions.ToDictionary(p=>p.Key,p=>p.Value with {Members=materials.ToDictionary(m=>m,m=>p.Value.PhysicalEntity with {
                Coordinates=p.Value.PhysicalEntity.Coordinates.Select(c=>c with {ObjectPattern="{UnitId}/"+m,PointRef=p.Key+"-"+m+"-"+c.PointRef}).ToArray()})})};
        if(unitKind=="looseGroup")
        {
            var mapped=Recipe011Data.ForSlots(2,1,2,3,4);
            var positions=recipe.Positions.Select(p=> {
                var first=p.SlotId=="s1"?1:3;
                return p with {PhysicalSlotIndex=first,CellId=mapped.Positions[first-1].CellId,
                    Members=p.Members.Select((m,i)=>m with {PhysicalSlotIndex=first+i,CellId=mapped.Positions[first+i-1].CellId}).ToArray()};
            }).ToArray();
            recipe=recipe with {Capacity=4,TrayLayout=mapped.TrayLayout,TraySlotMapping=mapped.TraySlotMapping,Positions=positions,
                ExecutionPositions=recipe.ExecutionPositions.ToDictionary(p=>p.Key,p=>p.Value with {
                    Members=p.Value.Members.ToDictionary(m=>m.Key,m=>m.Value with {
                        Coordinates=m.Value.Coordinates.Select(c=>c with {PhysicalSlotIndex=positions.Single(x=>x.SlotId==p.Key).Members.Single(x=>x.Material==m.Key).PhysicalSlotIndex!.Value}).ToArray()}) })};
        }
        recipe=recipe with {DefinitionDigest=RecipeDefinitionIdentity.ComputeDefinitionDigest(recipe)};
        var plan=RecipeRunPlanner.BuildExecutable(recipe,Guid.NewGuid().ToString("D"),["s2","s1"]);
        var captures=plan.Steps.Where(s=>s.Kind==RecipeStepKind.Capture).ToArray();
        Assert.Equal(16,captures.Length);
        foreach(var group in captures.GroupBy(s=>new{s.StageId,s.Camera,s.Material}))Assert.Equal(new[]{"s1","s2"},group.Select(s=>s.SlotId));
        var sorting=plan.Steps.Where(s=>s.Kind==RecipeStepKind.SortUnit).ToArray();
        Assert.All(sorting,s=>Assert.True(s.Sequence>captures.Max(c=>c.Sequence)));
        Assert.DoesNotContain(plan.Steps,s=>s.Kind is RecipeStepKind.TransferToRotation or RecipeStepKind.ReturnUnit or RecipeStepKind.Rotate);
        var flips=plan.Steps.Where(s=>s.Kind==RecipeStepKind.FlipMember).ToArray();
        if(unitKind=="looseGroup")
        {
            Assert.Equal(4,flips.Length);Assert.Equal(4,sorting.Length);
            Assert.All(flips,s=>Assert.Equal(s.MemberId,s.PhysicalEntityId));
        }
        else
        {
            Assert.Equal(2,flips.Length);Assert.Equal(2,sorting.Length);
            Assert.All(flips,s=>{Assert.Null(s.MemberId);Assert.Equal(s.UnitId,s.PhysicalEntityId);});
            Assert.All(captures,s=>Assert.Equal(s.UnitId,s.PhysicalEntityId));
        }
    }
}
