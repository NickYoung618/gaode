using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Application.Station01;
using Gaode.Domain.Station01;

namespace Gaode.Contracts.Tests.Support;

// Declared component input, never actual database/device evidence.
internal static class SemanticRecipeReceiptFixture
{
    public static RecipeBindingReceipt For(PublicPreparationHandoffV2 handoff, long epoch = 1)
    {
        const string prefix = "recipe-binding://";
        if (!handoff.RecipeBindingReference.StartsWith(prefix, StringComparison.Ordinal))
            throw new ArgumentException("Fixture requires a declared binding identity");
        var now = DateTimeOffset.UtcNow;
        var c = new ActionCorrelation(handoff.Identity.RunId, Guid.NewGuid(), Guid.NewGuid(), 1,
            Guid.NewGuid(), epoch, "semantic-unit-snapshot", handoff.PlanRevision, handoff.Identity.TrayId);
        var window = new ActionWindow(1, 10001, "semantic-unit-clock", now, now.AddMilliseconds(10000));
        return new(c, handoff.RecipeBindingReference[prefix.Length..], "component-recipe", "component-version", "component-digest",
            new("unit-budget", "2.0.0", "1.1", "Test", "SemanticUnitFixture", "unit-digest", c.SnapshotId, 10000), window,
            [new(Guid.NewGuid(), c, ActualCommitState.Committed, ReceiptValidity.ValidCurrent, 4, now, 2, null)
                { SavePurpose = "RecipePlanBound" },
             new(handoff.WriteId, c, ActualCommitState.Committed, ReceiptValidity.ValidCurrent, handoff.CommittedRevision, handoff.PersistedAt, 3, null)],
            3, ReceiptValidity.ValidCurrent, RecipeApplicationState.Completed)
        {
            IntentCommit = new(Guid.NewGuid(), c, ActualCommitState.Committed, ReceiptValidity.ValidCurrent, 3, now, 1, null)
                { RecordKind = BusinessCommitRecordKind.RunWrite, SavePurpose = "BindingIntent" }
        };
    }
}
