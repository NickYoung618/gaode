using Gaode.Application.Recipes;
using Gaode.Domain.Configuration;

namespace Gaode.Application.Workflow;

public sealed record RecipeExecutionDeadlines(DateTimeOffset StartedUtc,
    DateTimeOffset DetectionDeadlineUtc, DateTimeOffset SortingDeadlineUtc,
    DateTimeOffset UnloadDeadlineUtc, int CaptureCount, int FusionCount,
    int HeightRescanCount)
{
    public required string FormulaVersion { get; init; }
    public int FlipCount { get; init; }
    public int OrdinarySortingUnits { get; init; }
}

/// <summary>Freezes absolute route deadlines from the selected plan and versioned budget.</summary>
public static class RecipeExecutionBudget
{
    public static RecipeExecutionDeadlines Freeze(RecipeRunPlan plan,
        BusinessBudget budget, DateTimeOffset startedUtc, ExecutionCostProfile cost)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(budget);
        var work = RecipeWorkloadCounter.Count(plan);
        if (cost.BudgetReference != $"{budget.Id}/{budget.Version}" || string.IsNullOrWhiteSpace(cost.BudgetDigest) ||
            cost.CaptureMs <= 0 || cost.AlgorithmMs <= 0 || cost.AcquisitionReleaseMs <= 0)
            throw new InvalidOperationException("RecipeCostProfileMismatch");
        if (work.FlipCount > 0 && (budget.BusinessMs.FlipCompletion is null or <= 0 || budget.BusinessMs.PutBackCompletion is null or <= 0))
            throw new InvalidOperationException("FlipOrPutBackBudgetMissing");
        if (work.HeightRescanCount > 0 && budget.BusinessMs.TrayPoseAlgorithm is null or <= 0)
            throw new InvalidOperationException("TrayPoseBudgetMissing");
        var motionCount = plan.Steps.Count(step => step.Kind == RecipeStepKind.PositionForCapture);
        var detectionSaves = checked(plan.Steps.Count(step => step.OwnerStage == "Detection") +
            work.CaptureCount * 3 + work.FusionCount + motionCount * 2);
        var detectionMs = checked((long)motionCount *
                (budget.BusinessMs.PlcAcceptance + budget.BusinessMs.XyCompletion + cost.AcquisitionReleaseMs) +
            (long)work.CaptureCount * cost.CaptureMs +
            (long)work.DetectionWorkerCalls *
                (cost.AlgorithmMs + budget.BusinessMs.WorkerReleaseGrace) +
            (long)work.ECodeCount * (cost.CaptureMs + cost.AlgorithmMs +
                budget.BusinessMs.WorkerReleaseGrace + budget.BusinessMs.CriticalSave * 4) +
            (long)work.HeightRescanCount * (budget.BusinessMs.PlcAcceptance + budget.BusinessMs.XyCompletion +
                cost.AcquisitionReleaseMs + 10L * budget.BusinessMs.CriticalSave + budget.BusinessMs.Capture3d +
                budget.BusinessMs.TrayPoseAlgorithm.GetValueOrDefault() + budget.BusinessMs.WorkerReleaseGrace) +
            (long)work.FlipCount * (budget.BusinessMs.PlcAcceptance * 4L +
                budget.BusinessMs.XyCompletion * 2L + budget.BusinessMs.FlipCompletion.GetValueOrDefault() +
                budget.BusinessMs.PutBackCompletion.GetValueOrDefault() + budget.BusinessMs.CriticalSave * 14L) +
            (long)detectionSaves * budget.BusinessMs.CriticalSave);
        if (plan.InspectionKind == RecipeInspectionKind.SpecialRotation)
        {
            // These are the existing approved axis/transfer/save allowances, accumulated once for the full tray.
            // Per-unit scoping never restarts this deadline. Mechanical admission remains a separate gate.
            var transfers = plan.Steps.Count(step => step.Kind is RecipeStepKind.TransferToRotation or RecipeStepKind.SortUnit);
            var rotations = plan.Steps.Count(step => step.Kind == RecipeStepKind.Rotate);
            detectionMs = checked(detectionMs + (long)transfers *
                (8L * budget.BusinessMs.XyCompletion + cost.OrdinarySortDeviceAllowanceMs + 7L * budget.BusinessMs.CriticalSave) +
                (long)rotations * (budget.BusinessMs.PlcAcceptance + budget.BusinessMs.XyCompletion + 3L * budget.BusinessMs.CriticalSave));
        }
        var sortingCount = plan.Steps.Count(step => step.Kind == RecipeStepKind.SortUnit);
        // Approved device allowance and business save obligations remain separate.
        var sortingMs = checked((long)sortingCount *
            (8L * budget.BusinessMs.XyCompletion +
             cost.OrdinarySortDeviceAllowanceMs + 7L * budget.BusinessMs.CriticalSave));
        if (sortingCount == 0) sortingMs = budget.BusinessMs.CriticalSave;
        var unloadMs = checked((long)budget.BusinessMs.XyCompletion +
            cost.UnloadDeviceAllowanceMs +
            (sortingCount > 0 ? 5L : 3L) * budget.BusinessMs.CriticalSave);
        if (detectionMs <= 0 || sortingMs < 0 || unloadMs <= 0)
            throw new InvalidOperationException("RecipeBudgetInvalid");
        detectionMs = checked(detectionMs + (long)work.HeightRescanCount *
            Gaode.Application.Station01.TrayAnomalyDecisionService.DecisionWindowMs);
        var detection = startedUtc.AddMilliseconds(detectionMs);
        var sorting = detection.AddMilliseconds(sortingMs);
        var unload = sorting.AddMilliseconds(unloadMs);
        return new(startedUtc, detection, sorting, unload,
            work.CaptureCount, work.FusionCount, work.HeightRescanCount)
        { FormulaVersion = cost.Version + ":tray-anomaly/1", FlipCount = work.FlipCount, OrdinarySortingUnits = sortingCount };
    }
}
