// Historical payload shapes only. Current execution uses RecipeBindingReceipt and has no PLC recipe binding port.
using Gaode.Domain.Station01;
using System.Text.Json.Serialization;

namespace Gaode.Application.Ports;

public sealed record RecipeApplicationRequest(ActionCorrelation Correlation, string BindingId,
    Guid IntentWriteId, int DisplayRecipeId, int? NgCapacity, int? PendingCapacity,
    ActionWindow Window, int CriticalSaveBudgetMs)
{
    public bool IsValid => Correlation.IsValid && !string.IsNullOrWhiteSpace(BindingId) &&
        IntentWriteId != Guid.Empty && DisplayRecipeId > 0 && Window.IsValid && CriticalSaveBudgetMs > 0 &&
        (NgCapacity is > 0 && PendingCapacity is > 0 || NgCapacity is null && PendingCapacity is null);
}

[JsonConverter(typeof(JsonStringEnumConverter<RecipeApplicationState>))]
public enum RecipeApplicationState { Requested, DeviceApplied, Completed, Rejected, TimedOut, Cancelled, UnknownHeld }

public sealed record RecipeApplicationEvidence(RecipeApplicationRequest Request,
    RecipeApplicationState State, DeviceActionEvidence DeviceEvidence, long ReceivedTick)
{
    public RequiredCommitEvidence? RequiredEvidenceCommit { get; init; }
}

public sealed record RecipeApplicationBudgetSource(string ConfigurationId, string Version,
    string SchemaVersion, string Purpose, string Source, string Digest, string SnapshotId, int BudgetMs);
public sealed record RecipeDeadlineReference(string Stage, DateTimeOffset StartedAtUtc, DateTimeOffset DeadlineAtUtc);
// Recorded business clock coordinates, not another active window or a PLC clock.
// Decimal strings preserve precision through storage and JavaScript consumers.
public sealed record RecipeApplicationRegistration(string ClockId, string ClockFrequency, string StartTick,
    string BudgetDueTick, string EffectiveDueTick, DateTimeOffset StartedAtUtc, DateTimeOffset DeadlineAtUtc,
    IReadOnlyList<RecipeDeadlineReference>? ApplicableDeadlineReferences);
public sealed record RecipeApplicationReceipt(ActionCorrelation Correlation, string BindingId,
    RecipeApplicationBudgetSource BudgetSource, ActionWindow Window,
    IReadOnlyList<RequiredCommitEvidence> RequiredCommits, long ReceivedTick,
    ReceiptValidity Validity, RecipeApplicationState State)
{
    public DeviceActionEvidence? DeviceEvidence { get; init; }
    public RecipeApplicationRegistration? Registration { get; init; }
    public RequiredCommitEvidence? IntentCommit { get; init; }
    public RequiredCommitEvidence? RequiredEvidenceCommit { get; init; }
    public bool WasCompletedInWindow => State == RecipeApplicationState.Completed &&
        Validity == ReceiptValidity.ValidCurrent && Window.Contains(ReceivedTick) &&
        DeviceEvidence is { IsCorrelated: true, Meaning: DeviceCompletionMeaning.DeviceRecipeApplied } &&
        DeviceEvidence.Correlation == Correlation &&
        RequiredEvidenceCommit is { ActualCommit: ActualCommitState.Committed, Validity: ReceiptValidity.ValidCurrent,
            RecordKind: BusinessCommitRecordKind.CommunicationEvidence, CommittedUtc: not null, ReceivedTick: { } evidenceReceived } evidenceCommit &&
        evidenceCommit.Correlation == Correlation && evidenceCommit.WriteId != Guid.Empty && Window.Contains(evidenceReceived) &&
        RequiredCommits.Count > 0 && RequiredCommits.All(c => c.Correlation == Correlation && c.ActualCommit == ActualCommitState.Committed &&
            c.Validity == ReceiptValidity.ValidCurrent && c.WriteId != Guid.Empty &&
            c.ReceivedTick is { } received && Window.Contains(received));
}

