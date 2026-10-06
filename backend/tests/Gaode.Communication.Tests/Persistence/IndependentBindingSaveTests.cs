using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Application.Workflow;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Gaode.Communication.Tests.Persistence;

// Actual writer/database acceptance; no device or independent-process claim.
public sealed class IndependentBindingSaveTests
{
    [Theory]
    [InlineData(TerminalOutcome.Completed)]
    [InlineData(TerminalOutcome.CompletedWithExceptions)]
    public async Task BindingFactsAppendWithoutReopeningExistingHandoff(TerminalOutcome terminal)
    {
        var f = await CommunicationEvidenceStoreTests.CreateAsync();
        await using var writer = new TraceWriter(f.Options, TimeProvider.System, 8);
        var runId = Guid.NewGuid();
        var created = new RunCreatedPayload(Guid.NewGuid(), runId.ToString(), "Test", "{}", "{}", "{}", "{}", "snapshot", "p", "b", "s");
        Assert.Equal(CommitState.Committed, (await writer.SubmitCritical(new(Guid.NewGuid(), runId, 0, WriteKind.RunCreated,
            JsonSerializer.Serialize(created, new JsonSerializerOptions(JsonSerializerDefaults.Web)), "created")).Completion).State);
        Assert.Equal(CommitState.Committed, (await writer.SubmitCritical(new(Guid.NewGuid(), runId, 1, WriteKind.Complete,
            "{}", "completed", CandidateTerminal: terminal, HandoffJson: "{\"unchanged\":true}", HandoffId: Guid.NewGuid())).Completion).State);
        var bindingId = Guid.NewGuid();
        var trayId = Guid.NewGuid();
        var saves = new IndependentBindingEventWriter(new StageEventStore(f.Options), runId, trayId,
            "Station01", "TestLine", bindingId, 1, TimeProvider.System);
        long revision = 0;
        foreach (var (kind, name) in new[] { (WriteKind.ActionIntent, "RecipePlanAndBindingIntent"),
            (WriteKind.ActionFact, "RecipePlanBound"), (WriteKind.Audit, "RecipeApplicationReceiptObserved") })
        {
            var payload = JsonSerializer.Serialize(new { kind = name, operationId = Guid.NewGuid(), actionId = Guid.NewGuid(), attempt = 1,
                snapshotId = "snapshot", targetOrScope = "recipe", bindingId = bindingId });
            var batch = new WriteBatch(Guid.NewGuid(), runId, revision, kind, payload, name);
            var receipt = await saves.SubmitCritical(batch).Completion;
            Assert.Equal(CommitState.Committed, receipt.State);
            Assert.NotNull(receipt.CommittedUtc);
            Assert.Equal(BusinessCommitRecordKind.StageEvent, receipt.RecordKind);
            revision++;
        }
        var disallowed = new WriteBatch(Guid.NewGuid(), runId, revision, WriteKind.Audit,
            "{\"kind\":\"unrelated\"}", "unrelated");
        Assert.Throws<InvalidOperationException>(() => saves.SubmitCritical(disallowed));
        Assert.Throws<InvalidOperationException>(() => saves.SubmitCritical(disallowed with { WriteId = Guid.NewGuid(),
            Kind = WriteKind.ActionFact, PayloadJson = "{\"kind\":\"RecipePlanBound\"}", StateAfter = RunState.Detection }));
        // The original writer and database continue to reject changes to the terminal run.
        Assert.Equal(CommitState.ConditionRejected, (await writer.SubmitCritical(disallowed with { ExpectedRevision = 2 }).Completion).State);
        await using var reopened = new Station01DbContext(f.Options);
        var run = await reopened.Runs.SingleAsync(x => x.RunId == runId);
        Assert.Equal(terminal, run.Terminal);
        Assert.Equal(2, run.TerminalRevision);
        Assert.Equal(2, run.Revision);
        var facts = await new StageEventStore(f.Options).ReadAsync(runId, trayId, WholeTrayWorkflowStage.RecipeApplication);
        Assert.Equal(3, facts.Count);
        Assert.Equal(new[] { StageEventType.IntentRecorded, StageEventType.Executing, StageEventType.Completed }, facts.Select(x => x.EventType));
        Assert.Equal(2, await reopened.Writes.CountAsync(x => x.RunId == runId));
        var handoff = await reopened.Handoffs.SingleAsync(x => x.RunId == runId);
        Assert.Equal(2, handoff.Revision);
        Assert.Equal("{\"unchanged\":true}", handoff.PayloadJson);
        Assert.Equal(terminal, handoff.Terminal);
    }
}
