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
        if (budget.Purpose != "Test" || configuration.Public.Purpose != "Test" ||
            string.IsNullOrWhiteSpace(budget.Source) || string.IsNullOrWhiteSpace(configuration.BudgetDigest))
            throw new InvalidOperationException("RecipeExecutionBudgetNotApproved");
        return new("approved-recipe-cost", "011-normal-actions/1", budget.Purpose, budget.Source,
            $"{budget.Id}/{budget.Version}", configuration.BudgetDigest, 5000, 10000, 5000,
            checked(3L * budget.BusinessMs.PlcIo), 0)
        { CaptureWaitMs = 8000, AlgorithmWaitMs = 15000, InputReleaseWaitMs = budget.BusinessMs.WorkerReleaseGrace };
    }
}
