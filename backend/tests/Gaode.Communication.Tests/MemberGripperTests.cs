using System.Diagnostics;
using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Recipes;
using Gaode.Plc.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Gaode.Communication.Tests.Devices;

public sealed class MemberGripperTests
{
    private static RecipeDefinition Group()
    {
        var basis=Recipe011Data.ForSlots(1,1,2);
        var members=new[] { "A", "B" };
        var recipe=basis with {
            UnitKind="looseGroup",PrimaryMaterial="A",SortingGripperId=null,
            Composition=members.Select((m,i)=>new RecipeMaterial(m,[1]) {SortingGripperId=i+1}).ToArray(),
            Positions=[new("s1","{TrayRunId}/s1",members.Select((m,i)=>new RecipeMember(m,"{UnitId}/"+m,"individual")
                {CellId=$"r1:c{i+4}",PhysicalSlotIndex=i+1}).ToArray()) {CellId="r1:c4",PhysicalSlotIndex=1}],
            Stages=[new(1,"none",null,members.Select(m=>new RecipeTarget(m,1,"CD","detect","defect")).ToArray())],
            Disposition=basis.Disposition with {PhysicalUnit="problemMembersOnly"},
            Approval=basis.Approval with {AllowedSlots=["s1"]},
            ExecutionPositions=new Dictionary<string,SlotExecutionInputs> { ["s1"]=new("s1",basis.ExecutionPositions["s1"].PhysicalEntity,
                members.Select((m,i)=>KeyValuePair.Create(m,basis.ExecutionPositions[$"s{i+1}"].PhysicalEntity with {
                    Coordinates=basis.ExecutionPositions[$"s{i+1}"].PhysicalEntity.Coordinates.Select(c=>c with {
                        SlotId="s1",ObjectPattern="{UnitId}/"+m }).ToArray() })).ToDictionary()) }
        };
        return recipe with {DefinitionDigest=RecipeDefinitionIdentity.ComputeDefinitionDigest(recipe)};
    }
    private static SortingActionPlan Action(RecipeRunPlan plan,RecipeStep step) =>
        new(Guid.NewGuid(),Guid.NewGuid(),"1",step.Sequence,step.MemberId!,new("p","1",1,2,"mm","test-frame",3),
            "classification-is-not-selection",Guid.NewGuid(),1,step.SlotId!,step.MemberId) {PhysicalSlotIndex=step.PhysicalSlotIndex};

    [Fact]
    public async Task SaveReloadFreezeAndResolveMembersWithoutChangingSinglePart()
    {
        var root=Path.Combine(Path.GetTempPath(),"gaode-member-gripper-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            var storeRoot=Path.Combine(root,"store");
            RecipeStoreSchema.Prepare(root,storeRoot);
            using var store=new SqliteRecipeStore(new() {DatabasePath=Path.Combine(storeRoot,"recipes.db"),
                ReadWriteTimeoutMs=10000,DbLockTimeoutSeconds=5},root,NullLogger<SqliteRecipeStore>.Instance,()=>"core-test");
            var saved=await store.SaveAsync(new(Group() with {RecipeId="",Version=""},null,null,Guid.NewGuid().ToString()),default);
            Assert.True(saved.Status==RecipeSaveStatus.Saved,string.Join(",",saved.Issues?.Select(i=>i.Code)??[]));
            var reloaded=store.GetSnapshot().Definitions.Single();
            Assert.Equal(new int?[]{1,2},reloaded.Composition.Select(m=>m.SortingGripperId));
            // Persistence deliberately keeps a new draft unapproved; planner uses explicit Test fixture approval.
            var plan=RecipeRunPlanner.BuildExecutable(reloaded with {Approval=Group().Approval},Guid.NewGuid().ToString(),["s1"]);
            var steps=plan.Steps.Where(s=>s.Kind==RecipeStepKind.SortUnit).ToArray();
            Assert.Equal(new[]{1,2},steps.Select(s=>RecipeSortingGripperSelection.Resolve(plan,Action(plan,s))));
            var mutable=plan.SortingGrippersByMaterial!.ToDictionary();
            var frozen=RecipeCatalogSnapshots.Freeze(plan with {SortingGrippersByMaterial=mutable});
            var digest=RecipePlanRevision.Compute(frozen);mutable["A"]=2;
            Assert.Equal(1,frozen.SortingGrippersByMaterial!["A"]);
            Assert.Equal(digest,RecipePlanRevision.Compute(frozen));
            Assert.NotEqual(digest,RecipePlanRevision.Compute(plan with {SortingGrippersByMaterial=mutable}));
            var single=RecipeRunPlanner.BuildExecutable(Recipe011Data.ForSlots(1,1),Guid.NewGuid().ToString(),["s1"]);
            Assert.Equal(1,RecipeSortingGripperSelection.Resolve(single,Action(single,single.Steps.Single(s=>s.Kind==RecipeStepKind.SortUnit))));
        } finally {Directory.Delete(root,true);}
    }

    [Fact]
    public void OldGroupCanBeReadButCannotSaveOrStartWithMissingMemberGripper()
    {
        var recipe=Group();
        var old=RecipeDefinitionSerialization.Deserialize(RecipeDefinitionSerialization.Serialize(recipe with {
            SortingGripperId=1,Composition=recipe.Composition.Select(m=>m with {SortingGripperId=null}).ToArray()}));
        RecipeDefinitionValidator.Validate(old);
        Assert.Equal("RecipeMemberGripperRequired",RecipeDefinitionValidator.ExecutionProblem(old,["s1"]));
        Assert.Contains(RecipeDefinitionValidator.ValidateForSave(old,new("1","",[])).Issues,i=>i.Code=="RecipeMemberGripperRequired");
        Assert.Throws<InvalidOperationException>(()=>RecipeRunPlanner.BuildExecutable(old,Guid.NewGuid().ToString(),["s1"]));
        var plan=RecipeRunPlanner.BuildExecutable(recipe,Guid.NewGuid().ToString(),["s1"]);
        Assert.Throws<InvalidOperationException>(()=>RecipeSortingGripperSelection.Resolve(plan with {
            SortingGrippersByMaterial=null,SortingGripperId=1},Action(plan,plan.Steps.First(s=>s.Kind==RecipeStepKind.SortUnit))));
    }

    [Fact]
    public async Task ExistingCommunicationHandshakeSwitchesOneTwoAndReusesTwo()
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await using var plc=new CommissioningProtocolTcpFixture();await plc.StartAsync(timeout.Token);
        await using var device=plc.Device();await device.StartAsync(timeout.Token);await device.ResetAsync(timeout.Token);
        var state=device.Observe();var tick=Stopwatch.GetTimestamp();var utc=DateTimeOffset.UtcNow;
        var request=new PlcStageActionRequest(new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,Guid.NewGuid(),state.ConnectionEpoch,"test"),
            Guid.NewGuid(),Guid.NewGuid(),PlcWorkflowStage.Sorting,"digest",new(tick,tick+Stopwatch.Frequency*10,"Stopwatch",utc,utc.AddSeconds(10)),"test");
        foreach(var grip in new[]{1,2,2})await device.SelectStageGripperAsync(request,grip,timeout.Token);
        var writes=plc.Store.GetWriteAudit().Where(w=>w.Accepted && w.DocumentNumber==plc.Store.Definition[SignalId.GrabId].DocumentNumber).ToArray();
        Assert.Equal(new ushort[]{1,2},writes.Select(w=>w.Value));
        Assert.Equal(2,plc.Store.ReadHoldingRegisterByDocumentNumber(plc.Store.Definition[SignalId.GrabActiveId].DocumentNumber));
        Assert.DoesNotContain(plc.Engine.GetActionAudit().Actions,a=>a.Kind=="AxisMove");
    }
}
