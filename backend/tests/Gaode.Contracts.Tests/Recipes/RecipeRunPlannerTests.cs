using Gaode.Application.Recipes;
using Xunit;

namespace Gaode.Contracts.Tests.Recipes;

public sealed class RecipeRunPlannerTests
{
    [Fact]
    public void SpecialAbAbUsesStableOkOrderAndTwoIndependentStagesPerUnit()
    {
        var recipe=Recipe011Data.ForSlots(1,1,2);
        var target=new RecipeTarget("part",1,"AB","detect","defect");
        var station=new HandlingPoint(new("station","test-1",40,50,"mm","test-frame",3),"Test:explicit station");
        recipe=recipe with {InspectionKind=RecipeInspectionKind.SpecialRotation,Route="specialType1Part",
            RotationLoadingGripperId=2,RotationWorkstation=new(station,station with {Point=station.Point with {Id="station-pick",Z=4}}),
            Positions=recipe.Positions.Reverse().ToArray(),Stages=[new(1,"rotate",90,[target]),new(2,"rotate",180,[target])],
            ExecutionPositions=recipe.ExecutionPositions.ToDictionary(p=>p.Key,p=>p.Value with {
                PhysicalEntity=p.Value.PhysicalEntity with {
                    OriginPutBack=p.Value.PhysicalEntity.Source! with {Point=p.Value.PhysicalEntity.Source!.Point with {Id=p.Key+"-original-put",Z=4}},
                    Coordinates=new[]{1,2}.SelectMany(stage=>"AB".Select(camera=>p.Value.PhysicalEntity.Coordinates[0] with {
                        StageId="stage:"+stage,Camera=camera.ToString(),PointRef=$"{p.Key}-g{stage}-{camera}"})).ToArray() } })};
        var tray=Guid.NewGuid();
        var plan=RecipeRunPlanner.BuildExecutable(recipe,tray.ToString("D"),["s2","s1"]);
        Assert.Equal(new[]{"s1","s2"},plan.Steps.Where(s=>s.Kind==RecipeStepKind.TransferToRotation).Select(s=>s.SlotId));
        foreach(var slot in new[]{"s1","s2"}) {
            var captures=plan.Steps.Where(s=>s.Kind==RecipeStepKind.Capture&&s.SlotId==slot).ToArray();
            Assert.Equal(new[]{"stage:1:A","stage:1:B","stage:2:A","stage:2:B"},captures.Select(s=>s.StageId+":"+s.Camera));
            Assert.Equal(tray,plan.OriginalSlots![slot].TrayId);
            Assert.Equal(recipe.Positions.Single(p=>p.SlotId==slot).CellId,plan.OriginalSlots[slot].CellId);
        }
        Assert.Single(plan.Steps,s=>s.Kind==RecipeStepKind.UnloadTray);
    }
    [Fact]
    public void SameFrozenInputsCreateStableOrderedPlan()
    {
        var recipe = Recipe011Data.ForSlots(1, 1, 3);
        var tray = Guid.NewGuid().ToString("D");
        var first = RecipeRunPlanner.BuildExecutable(recipe, tray, ["s3", "s1"]);
        var second = RecipeRunPlanner.BuildExecutable(recipe, tray, ["s3", "s1"]);
        Assert.Equal(first.RecipeId, second.RecipeId);
        Assert.Equal(first.RecipeVersion, second.RecipeVersion);
        Assert.Equal(first.MissingSlots, second.MissingSlots);
        Assert.Equal(first.Steps, second.Steps);
        Assert.Equal(first.Steps.Select(x => x.Sequence).Order(), first.Steps.Select(x => x.Sequence));
        Assert.Equal(recipe.CatalogDigest, first.CatalogDigest);
        Assert.Equal(new[] { "C:s1", "C:s3", "D:s1", "D:s3" },
            first.Steps.Where(x => x.Kind == RecipeStepKind.Capture).Select(x => $"{x.Camera}:{x.SlotId}"));
        Assert.Equal(new int?[] { 1, 3 }, first.Steps.Where(x => x.Kind == RecipeStepKind.Capture && x.Camera == "C").Select(x => x.PhysicalSlotIndex));
    }

    [Fact]
    public void DuplicateUnknownOrEmptyOccupiedSlotsAreRejected()
    {
        var recipe = Recipe011Data.ForSlots(1, 1);
        var tray = Guid.NewGuid().ToString("D");
        Assert.Throws<ArgumentException>(() => RecipeRunPlanner.BuildExecutable(recipe, tray, ["s1", "s1"]));
        Assert.Throws<ArgumentException>(() => RecipeRunPlanner.BuildExecutable(recipe, tray, ["unknown"]));
        Assert.Throws<ArgumentException>(() => RecipeRunPlanner.BuildExecutable(recipe, tray, []));
    }
}
