using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Application.Workflow;
using Gaode.Domain.Station01;

namespace Gaode.Application.Recipes;

// This is the independent binding's actual business save channel. It leaves the
// original terminal run and handoff immutable and uses existing StageEvent transactions.
public sealed class IndependentBindingEventWriter(IStageEventStore store, Guid runId, Guid trayId,
    string stationId, string lineId, Guid bindingId, long epoch, TimeProvider clock) : ITraceWriter
{
    private Guid operationId;
    public QueuedWrite SubmitCritical(WriteBatch batch, CancellationToken token = default, ActionWindow? window = null)
    {
        token.ThrowIfCancellationRequested();
        if (batch.RunId != runId || batch.StateAfter is not null || batch.CandidateTerminal != TerminalOutcome.None ||
            batch.HandoffJson is not null || batch.HandoffV2Json is not null || window is not null && !window.Contains(clock.GetTimestamp()))
            throw new InvalidOperationException("IndependentBindingSaveAdmissionRejected");
        using var document = JsonDocument.Parse(batch.PayloadJson);
        var payload = document.RootElement;
        var name = payload.GetProperty("kind").GetString();
        var type = (batch.Kind, name) switch
        {
            (WriteKind.ActionIntent, "RecipePlanAndBindingIntent") => StageEventType.IntentRecorded,
            (WriteKind.ActionFact, "RecipePlanBound") => StageEventType.Executing,
            (WriteKind.Audit, "RecipeApplicationReceiptObserved") => StageEventType.Completed,
            _ => throw new InvalidOperationException("IndependentBindingSaveKindRejected")
        };
        if (type == StageEventType.IntentRecorded)
        {
            if (payload.GetProperty("bindingId").GetGuid() != bindingId) throw new InvalidOperationException("BindingSaveIdentityMismatch");
            operationId = payload.GetProperty("operationId").GetGuid();
        }
        if (operationId == Guid.Empty) throw new InvalidOperationException("BindingIntentMissing");
        var request = new StageEventAppendRequest(batch.WriteId, runId, trayId, stationId, lineId,
            WholeTrayWorkflowStage.RecipeApplication, operationId, 1, epoch, type, clock.GetUtcNow(),
            ResultSource.HostDerived, ResultQuality.Derived, null, batch.PayloadDigest, batch.PayloadJson,
            $"recipe-application:{bindingId:D}:{batch.WriteId:D}",
            payload.TryGetProperty("planRevision", out var revision) ? revision.GetString()! : "",
            window?.StartedUtc, window?.DeadlineUtc);
        return new(new(batch.WriteId, runId, CommitState.Queued, null, TerminalOutcome.None, null), CommitAsync(request, token));
    }
    private async Task<CommitReceipt> CommitAsync(StageEventAppendRequest request, CancellationToken token)
    {
        try
        {
            var result = await store.AppendAsync(request, token);
            return new(request.EventId, runId, result.IsCommitted ? CommitState.Committed : CommitState.ConditionRejected,
                result.Event.Sequence, TerminalOutcome.None, result.ErrorCode, result.Event.PersistedAt) { RecordKind = BusinessCommitRecordKind.StageEvent };
        }
        catch (StageEventCommitException error)
        { return new(request.EventId, runId, error.ActualCommit == ActualCommitState.ConfirmedRolledBack ?
            CommitState.Failed : CommitState.CommitUnknown, null, TerminalOutcome.None, error.Message); }
    }
    public async Task<CommitReceipt?> ReconcileAsync(Guid writeId, CancellationToken token)
    {
        var record = (await store.ReadAsync(runId, trayId, WholeTrayWorkflowStage.RecipeApplication, token))
            .SingleOrDefault(e => e.EventId == writeId);
        return record is null ? null : new(record.EventId, runId, CommitState.Committed, record.Sequence,
            TerminalOutcome.None, null, record.PersistedAt) { RecordKind = BusinessCommitRecordKind.StageEvent };
    }
}
