using System.Text.Json;
using System.Reflection;
using Gaode.Application.Ports;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Persistence;
using Gaode.Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Gaode.Integration.Tests.Storage;

public sealed class TraceStoreTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DurableTerminalCompareAndSwapKeepsRunAndHandoffAtomic(bool completeFirst)
    {
        var approved = Gaode.Testing.ApprovedTestRoot.Resolve(Station01HostFixture.FindWorkspace());
        Directory.CreateDirectory(approved);
        var root = await StorePreparation.PrepareEmptyTestStoreAsync(approved,
            Path.Combine(approved, "cas-" + Guid.NewGuid().ToString("N")));
        using var guard = StoreAccessGuard.Acquire(root, approved);
        var options = new DbContextOptionsBuilder<Station01DbContext>()
            .UseSqlite(StoreCompatibilityProbe.ReadWriteConnectionString(root)).Options;
        await using var writer = new TraceWriter(options, TimeProvider.System, 8);
        var runId = Guid.NewGuid();
        var created = new RunCreatedPayload(Guid.NewGuid(), "cas-request", "test:Operator",
            "{}", "{}", "{}", "{}", "snapshot", "public", "budget", "simulation");
        var initial = new WriteBatch(Guid.NewGuid(), runId, 0, WriteKind.RunCreated,
            JsonSerializer.Serialize(created, new JsonSerializerOptions(JsonSerializerDefaults.Web)), "digest-initial");
        Assert.Equal(CommitState.Committed, (await writer.SubmitCritical(initial).Completion).State);
        var completed = new WriteBatch(Guid.NewGuid(), runId, 1, WriteKind.Complete,
            "{}", "digest-complete", CandidateTerminal: TerminalOutcome.Completed,
            HandoffJson: "{}", HandoffId: Guid.NewGuid());
        var cancelled = new WriteBatch(Guid.NewGuid(), runId, 1, WriteKind.Cancel,
            "{}", "digest-cancel", CandidateTerminal: TerminalOutcome.Cancelled);
        var winner = completeFirst ? completed : cancelled;
        var loser = completeFirst ? cancelled : completed;
        Assert.Equal(CommitState.Committed, (await writer.SubmitCritical(winner).Completion).State);
        Assert.Equal(CommitState.ConditionRejected, (await writer.SubmitCritical(loser).Completion).State);
        await using var db = new Station01DbContext(options);
        var run = await db.Runs.AsNoTracking().SingleAsync(x => x.RunId == runId);
        Assert.Equal(2, run.Revision);
        Assert.Equal(2, run.TerminalRevision);
        var handoffs = await db.Handoffs.AsNoTracking().Where(x => x.RunId == runId).ToArrayAsync();
        if (completeFirst)
        {
            Assert.Equal(TerminalOutcome.Completed, run.Terminal);
            Assert.Single(handoffs);
            Assert.Equal(run.TerminalRevision, handoffs[0].Revision);
            Assert.Equal(completed.WriteId, handoffs[0].WriteId);
        }
        else
        {
            Assert.Equal(TerminalOutcome.Cancelled, run.Terminal);
            Assert.Empty(handoffs);
        }
        Assert.Equal(CommitState.Committed, (await writer.ReconcileAsync(winner.WriteId, default))!.State);
    }

    [Fact]
    public async Task CompletedWritesAreEvictedWhileDurableIdempotenceAndConflictChecksRemain()
    {
        var approved = Gaode.Testing.ApprovedTestRoot.Resolve(Station01HostFixture.FindWorkspace());
        Directory.CreateDirectory(approved);
        var root = await StorePreparation.PrepareEmptyTestStoreAsync(approved,
            Path.Combine(approved, "writer-index-" + Guid.NewGuid().ToString("N")));
        using var guard = StoreAccessGuard.Acquire(root, approved);
        var options = new DbContextOptionsBuilder<Station01DbContext>()
            .UseSqlite(StoreCompatibilityProbe.ReadWriteConnectionString(root)).Options;
        await using var writer = new TraceWriter(options, TimeProvider.System, 8);
        var runId = Guid.NewGuid();
        var created = new RunCreatedPayload(Guid.NewGuid(), "writer-index", "test:Operator",
            "{}", "{}", "{}", "{}", "snapshot", "public", "budget", "simulation");
        var initial = new WriteBatch(Guid.NewGuid(), runId, 0, WriteKind.RunCreated,
            JsonSerializer.Serialize(created, new JsonSerializerOptions(JsonSerializerDefaults.Web)), "digest-initial");
        Assert.Equal(CommitState.Committed, (await writer.SubmitCritical(initial).Completion).State);

        WriteBatch? last = null;
        for (var revision = 1; revision <= 128; revision++)
        {
            last = new WriteBatch(Guid.NewGuid(), runId, revision, WriteKind.Audit,
                "{\"sequence\":" + revision + "}", "digest-" + revision);
            Assert.Equal(CommitState.Committed, (await writer.SubmitCritical(last).Completion).State);
        }

        Assert.Equal(0, TrackedWriteCount(writer));
        var duplicate = await writer.SubmitCritical(last!).Completion;
        Assert.Equal(CommitState.Committed, duplicate.State);
        Assert.Equal(129, duplicate.CommittedRevision);
        var conflict = last! with { PayloadJson = "{\"sequence\":999}", PayloadDigest = "different" };
        Assert.Equal(CommitState.Failed, (await writer.SubmitCritical(conflict).Completion).State);
        Assert.Equal(0, TrackedWriteCount(writer));
        Assert.Equal(CommitState.Committed, (await writer.ReconcileAsync(last.WriteId, default))!.State);
    }

    [Fact]
    public async Task DurableCommitCanBeReconciledWhileOriginalReceiptIsDelayed()
    {
        var approved = Gaode.Testing.ApprovedTestRoot.Resolve(Station01HostFixture.FindWorkspace());
        Directory.CreateDirectory(approved);
        var root = await StorePreparation.PrepareEmptyTestStoreAsync(approved,
            Path.Combine(approved, "receipt-delay-" + Guid.NewGuid().ToString("N")));
        using var guard = StoreAccessGuard.Acquire(root, approved);
        var options = new DbContextOptionsBuilder<Station01DbContext>()
            .UseSqlite(StoreCompatibilityProbe.ReadWriteConnectionString(root)).Options;
        var gate = new CommitInterleavingGate();
        gate.HoldReceipt(WriteKind.RunCreated);
        await using var writer = new TraceWriter(options, TimeProvider.System, 8,
            gate.BeforeCommitAsync, gate.AfterCommitBeforeReceiptAsync);
        var runId = Guid.NewGuid();
        var created = new RunCreatedPayload(Guid.NewGuid(), "receipt-delay", "test:Operator",
            "{}", "{}", "{}", "{}", "snapshot", "public", "budget", "simulation");
        var batch = new WriteBatch(Guid.NewGuid(), runId, 0, WriteKind.RunCreated,
            JsonSerializer.Serialize(created, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            "receipt-delay-created");
        var queued = writer.SubmitCritical(batch);
        using var wait = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var committed = await gate.WaitUntilCommittedAsync(WriteKind.RunCreated, wait.Token);
        Assert.Equal(CommitState.Committed, committed.State);
        Assert.False(queued.Completion.IsCompleted);
        var reconciled = await writer.ReconcileAsync(batch.WriteId, wait.Token);
        Assert.Equal(CommitState.Committed, reconciled?.State);
        Assert.Equal(committed.CommittedRevision, reconciled?.CommittedRevision);
        gate.ReleaseReceipt(WriteKind.RunCreated);
        Assert.Equal(CommitState.Committed, (await queued.Completion).State);
    }

    private static int TrackedWriteCount(TraceWriter writer)
    {
        var field = typeof(TraceWriter).GetField("_pending", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var value = field.GetValue(writer)!;
        return (int)value.GetType().GetProperty("Count")!.GetValue(value)!;
    }
}
