using System.Globalization;
using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Domain.Station01;

namespace Gaode.Infrastructure.Persistence;

// Closed semantic public shapes. A recorded receipt is evidence, never restart permission.
public sealed record RecipeBudgetReference(string Id, string Version, string Purpose, string Source,
    string Digest, string SnapshotId, int BudgetMs);
public sealed record RecipeCommitProjection(Guid WriteId, string Kind, string ActualCommit, string? ReceiptValidity,
    DateTimeOffset? CommittedAtUtc, string? HostReceivedTick, string RecordKind);
public sealed record RecipeApplicationView(string BindingId, string Outcome, RecipeBudgetReference? BudgetReference,
    RecipeApplicationRegistration? Window, bool? DeviceApplied, string? HostValidatedTick,
    IReadOnlyList<RecipeCommitProjection> RequiredCommits,
    IReadOnlyList<DiagnosticEvidenceReference> DiagnosticEvidenceReferences);

public static class RecipeApplicationProjection
{
    public static RecipeApplicationView Current(RecipeBindingReceipt receipt)
    {
        if (!receipt.WasCompletedInWindow || receipt.Registration is null || receipt.IntentCommit is null)
            throw new InvalidOperationException("RecipeApplicationPublicReceiptIncomplete");
        return new(receipt.BindingId, "Completed", Budget(receipt.BudgetSource), receipt.Registration, null,
            Tick(receipt.ReceivedTick), new[] { Commit(receipt.IntentCommit) }.Concat(receipt.RequiredCommits.Select(Commit)).ToArray(),
            []);
    }
    public static RecipeBudgetReference? Budget(RecipeApplicationBudgetSource source) =>
        source.BudgetMs > 0 && new[] { source.ConfigurationId, source.Version, source.Purpose, source.Source, source.Digest, source.SnapshotId }
            .All(value => !string.IsNullOrWhiteSpace(value)) ? new(source.ConfigurationId,
                source.Version, source.Purpose, source.Source, source.Digest, source.SnapshotId, source.BudgetMs) : null;
    public static string? Tick(long? value) => value?.ToString(CultureInfo.InvariantCulture);
    private static RecipeCommitProjection Commit(RequiredCommitEvidence record) => new(record.WriteId,
        record.SavePurpose ?? "NotRecorded", record.ActualCommit.ToString(), record.Validity.ToString(),
        record.CommittedUtc, Tick(record.ReceivedTick), record.RecordKind.ToString());
}
