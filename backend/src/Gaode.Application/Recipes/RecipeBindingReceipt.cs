using Gaode.Application.Ports;
using Gaode.Domain.Station01;

namespace Gaode.Application.Recipes;

public sealed record RecipeBindingReceipt(ActionCorrelation Correlation, string BindingId,
    string RecipeId, string RecipeVersion, string DefinitionDigest,
    RecipeApplicationBudgetSource BudgetSource, ActionWindow Window,
    IReadOnlyList<RequiredCommitEvidence> RequiredCommits, long ReceivedTick,
    ReceiptValidity Validity, RecipeApplicationState State)
{
    public RecipeApplicationRegistration? Registration { get; init; }
    public RequiredCommitEvidence? IntentCommit { get; init; }
    public bool WasCompletedInWindow => State == RecipeApplicationState.Completed && Correlation.IsValid &&
        new[] { BindingId, RecipeId, RecipeVersion, DefinitionDigest }.All(v => !string.IsNullOrWhiteSpace(v)) &&
        Validity == ReceiptValidity.ValidCurrent && Window.Contains(ReceivedTick) &&
        IntentCommit is { ActualCommit: ActualCommitState.Committed, Validity: ReceiptValidity.ValidCurrent,
            CommittedUtc: not null, ReceivedTick: not null } intent && intent.WriteId != Guid.Empty && intent.Correlation == Correlation &&
        RequiredCommits.Any(c => c.SavePurpose == "RecipePlanBound") &&
        RequiredCommits.All(c => c.Correlation == Correlation && c.ActualCommit == ActualCommitState.Committed &&
            c.Validity == ReceiptValidity.ValidCurrent && c.WriteId != Guid.Empty && c.CommittedUtc is not null &&
            c.ReceivedTick is { } received && Window.Contains(received));
}
