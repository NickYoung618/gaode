using Gaode.Domain.Station01;

namespace Gaode.Application.Recipes;

// Business resolution only: no PLC fields, transport, feedback or mutable device state.
public static class RecipeSortingGripperSelection
{
    public static int Resolve(RecipeRunPlan plan, SortingActionPlan action)
    {
        int? gripper = plan.SortingGripperId;
        if (plan.UnitKind == "looseGroup")
        {
            var step = plan.Steps.SingleOrDefault(s => s.Kind == RecipeStepKind.SortUnit &&
                s.SlotId == action.SlotId && s.MemberId == action.MemberId &&
                (s.MemberId ?? s.UnitId) == action.ObjectId);
            if (step?.Material is not { } material || plan.SortingGrippersByMaterial is null ||
                !plan.SortingGrippersByMaterial.TryGetValue(material, out var selected))
                throw new InvalidOperationException("SortingMemberGripperUnconfigured");
            gripper = selected;
        }
        return gripper is 1 or 2 ? gripper.Value : throw new InvalidOperationException("SortingGripperUnconfigured");
    }
}
