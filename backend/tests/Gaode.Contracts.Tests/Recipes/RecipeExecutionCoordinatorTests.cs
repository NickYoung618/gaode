using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Application.Workflow;
using Gaode.Contracts.Tests.Support;
using Gaode.Domain.Configuration;
using Xunit;

namespace Gaode.Contracts.Tests.Recipes;

public sealed class RecipeExecutionCoordinatorTests
{
    private static RecipeRunPlan Plan(int faces = 1, params int[] slots) => Recipe011Data.Plan(Guid.NewGuid(), faces, slots);

    [Fact]
    public void SingleFacePlanHasCompleteSteps() => Assert.Null(RecipeExecutionCoordinator.ValidateDetectionPlan(Plan()));

    [Fact]
    public void CaptureMustFollowMatchingPositionAndUnknownStepsCannotBeSkipped()
    {
        var plan = Plan();
        var steps = plan.Steps.ToArray();
        var position = Array.FindIndex(steps, s => s.Kind == RecipeStepKind.PositionForCapture);
        var capture = position + 1;
        steps[capture] = steps[capture] with { Camera = "A" };
        Assert.Equal("CaptureWithoutMatchingPosition", RecipeExecutionCoordinator.ValidateDetectionPlan(plan with { Steps = steps }));
        steps[capture] = plan.Steps[capture];
        steps[position] = steps[position] with { Kind = (RecipeStepKind)999 };
        Assert.Equal("DetectionStepUnsupported:999", RecipeExecutionCoordinator.ValidateDetectionPlan(plan with { Steps = steps }));
    }

    [Fact]
    public void DeadlinesAreAbsoluteAndSortingPrecedesUnload()
    {
        var budget = Budget();
        var start = new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero);
        var deadlines = RecipeExecutionBudget.Freeze(Plan(), budget, start, Cost(budget));
        Assert.Equal((2, 1, 0), (deadlines.CaptureCount, deadlines.FusionCount, deadlines.HeightRescanCount));
        Assert.Equal(start, deadlines.StartedUtc);
        Assert.Equal(TimeSpan.FromMilliseconds(2L * (budget.BusinessMs.PlcAcceptance + budget.BusinessMs.XyCompletion + 5000) +
            2L * 5000 + 3L * (10000 + budget.BusinessMs.WorkerReleaseGrace) + 16L * budget.BusinessMs.CriticalSave),
            deadlines.DetectionDeadlineUtc - start);
        Assert.True(deadlines.SortingDeadlineUtc > deadlines.DetectionDeadlineUtc);
        Assert.True(deadlines.UnloadDeadlineUtc > deadlines.SortingDeadlineUtc);
        Assert.Equal(1, deadlines.OrdinarySortingUnits);
    }

    [Fact]
    public void TwoFacesBudgetIncludesFlipPutBackAndOneRecheck()
    {
        var budget = Budget();
        var start = DateTimeOffset.UtcNow;
        var plan = Plan(2);
        var frozen = RecipeExecutionBudget.Freeze(plan, budget, start, Cost(budget));
        var single = RecipeExecutionBudget.Freeze(Plan(), budget, start, Cost(budget));
        Assert.Equal((4, 2, 1), (frozen.CaptureCount, frozen.FusionCount, frozen.HeightRescanCount));
        Assert.Equal(1, frozen.FlipCount);
        Assert.Single(plan.Steps, s => s.Kind == RecipeStepKind.RescanWholeTray);
        Assert.True(frozen.DetectionDeadlineUtc > single.DetectionDeadlineUtc);
        Assert.Throws<InvalidOperationException>(() => RecipeExecutionBudget.Freeze(plan,
            budget with { BusinessMs = budget.BusinessMs with { PutBackCompletion = null } }, start, Cost(budget)));
    }

    [Fact]
    public void TwoSlotsAllowConsecutiveEntityFlipsThenOneRecheckBeforeCapture()
    {
        var plan = Plan(2, 1, 3);
        Assert.Null(RecipeExecutionCoordinator.ValidateDetectionPlan(plan));
        var flips = plan.Steps.Where(s => s.Kind == RecipeStepKind.FlipMember).ToArray();
        Assert.Equal(2, flips.Length);
        Assert.NotEqual(flips[0].PhysicalEntityId, flips[1].PhysicalEntityId);
        var rescan = Assert.Single(plan.Steps, s => s.Kind == RecipeStepKind.RescanWholeTray);
        Assert.All(flips, step => Assert.True(step.Sequence < rescan.Sequence));
        Assert.All(plan.Steps.Where(s => s.Kind == RecipeStepKind.Capture && s.StageId == "stage:2"),
            step => Assert.True(step.Sequence > rescan.Sequence));
        var missing = plan with { Steps = plan.Steps.Where(s => s != rescan).ToArray() };
        Assert.NotNull(RecipeExecutionCoordinator.ValidateFaceRoundOrder(missing));
    }

    [Fact]
    public void StrictRecipeRequestRejectsLegacyPlaceholderPosition()
    {
        var request = new DetectionRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            WholeTrayWorkflowStage.Detection, Guid.NewGuid(), "plan-digest", 1, DateTimeOffset.UtcNow.AddMinutes(2),
            ["media://3d", "media://f"], "Test", "strict", ExpectedObjects:
                [new("part", new FixedPoint("s1", "plan:digest", 0, 0, "mm", "frozen-plan"), "part")]);
        Assert.False(request.IsValid);
        Assert.False((request with { Purpose = "Production" }).IsValid);
    }

    private static BusinessBudget Budget()
    {
        var budget = TestConfiguration.Normal().Budget;
        // Explicit component-only durations. No site parameter approval.
        return budget with { BusinessMs = budget.BusinessMs with { FlipCompletion = 1000, PutBackCompletion = 1000, TrayPoseAlgorithm = 1000 } };
    }
    private static ExecutionCostProfile Cost(BusinessBudget b) => new("component-cost", "component/1", "Test", "component only",
        $"{b.Id}/{b.Version}", "component-input", 5000, 10000, 5000, 24100, 23000)
        { CaptureWaitMs = 8000, AlgorithmWaitMs = 15000, InputReleaseWaitMs = 2000 };
}
