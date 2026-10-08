using Gaode.Application.Configuration;
using Gaode.Application.Recipes;

namespace Gaode.Infrastructure.Configuration;

// Existing Test capture/algorithm allowances and exact frozen business budgets.
// Six sorting moves and two completion waits are counted by the common budget.
// Only the three standalone pick/place/clear writes are additional I/O here.
public sealed class ApprovedExecutionCostProvider : IExecutionCostProvider
{
    public ExecutionCostProfile Resolve(FrozenConfiguration configuration)
    {
        var budget = configuration.Budget;
        if (budget.Purpose == Gaode.Domain.Configuration.RuntimePurposes.RealDeviceCommissioning)
        {
            if (configuration.Public.Purpose != budget.Purpose || budget.RecipeExecution is not { IsValid: true } cost ||
                string.IsNullOrWhiteSpace(budget.Source) || string.IsNullOrWhiteSpace(budget.Id) || string.IsNullOrWhiteSpace(budget.Version) ||
                string.IsNullOrWhiteSpace(configuration.BudgetDigest))
                throw new InvalidOperationException("CommissioningRecipeExecutionBudgetMissingOrInvalid");
            return new("configured-recipe-cost", "020-configured/1", budget.Purpose, budget.Source,
                $"{budget.Id}/{budget.Version}", configuration.BudgetDigest, cost.CaptureMs, cost.AlgorithmMs, cost.AcquisitionReleaseMs,
                checked(3L * budget.BusinessMs.PlcIo), 0)
                { CaptureWaitMs = cost.CaptureWaitMs, AlgorithmWaitMs = cost.AlgorithmWaitMs, InputReleaseWaitMs = cost.InputReleaseWaitMs };
        }
        if (budget.Purpose != "Test" || configuration.Public.Purpose != "Test" ||
            string.IsNullOrWhiteSpace(budget.Source) || string.IsNullOrWhiteSpace(configuration.BudgetDigest))
            throw new InvalidOperationException("RecipeExecutionBudgetNotApproved");
        return new("approved-recipe-cost", "011-normal-actions/1", budget.Purpose, budget.Source,
            $"{budget.Id}/{budget.Version}", configuration.BudgetDigest, 5000, 10000, 5000,
            checked(3L * budget.BusinessMs.PlcIo), 0)
        { CaptureWaitMs = 8000, AlgorithmWaitMs = 15000, InputReleaseWaitMs = budget.BusinessMs.WorkerReleaseGrace };
    }
}
