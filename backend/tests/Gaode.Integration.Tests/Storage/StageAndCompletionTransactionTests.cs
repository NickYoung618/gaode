using Gaode.Application.Ports;
using Gaode.Application.Workflow;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Gaode.Integration.Tests.Storage;

public sealed class StageAndCompletionTransactionTests : IAsyncLifetime
{
    private SqliteConnection connection = null!;
    private DbContextOptions<Station01DbContext> options = null!;
    private readonly Guid runId = Guid.NewGuid();
    private readonly Guid trayId = Guid.NewGuid();
    private readonly Guid stationId = Guid.NewGuid();
    private readonly Guid lineId = Guid.NewGuid();
    private readonly string planRevision = "plan-transaction-1";

    public async Task InitializeAsync()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        options = new DbContextOptionsBuilder<Station01DbContext>().UseSqlite(connection).Options;
        await using var db = new Station01DbContext(options);
        await db.Database.MigrateAsync();
        db.Runs.Add(new RunEntity
        {
            RunId = runId, RequestId = "transaction-test", SubjectId = "operator-test",
            State = RunState.UnloadPreparation, CreatedUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        await SeedCompletedStagesAsync();
    }

    public async Task DisposeAsync() => await connection.DisposeAsync();

    [Fact]
    public async Task AggregateFailureRollsBackMatrixCompletionEventProjectionAndRunState()
    {
        var store = new WholeTrayCompletionStore(options, TimeProvider.System,
            (phase, _) => phase == "WholeTrayAggregate"
                ? Task.FromException(new IOException("injected-before-aggregate-commit"))
                : Task.CompletedTask);

        await Assert.ThrowsAsync<IOException>(() => store.CreateAsync(CreateRequest()));

        await using var db = new Station01DbContext(options);
        Assert.Empty(await db.WholeTrayCompletions.ToListAsync());
        Assert.Empty(await db.ComponentEvidenceMatrices.ToListAsync());
        Assert.Empty(await db.StageEvents.Where(x =>
            x.EventType == StageEventType.WholeTrayCompleted.ToString()).ToListAsync());
        Assert.Empty(await db.StageProjections.Where(x =>
            x.Stage == WholeTrayWorkflowStage.ManualRemovalAdmission.ToString()).ToListAsync());
        var run = await db.Runs.SingleAsync(x => x.RunId == runId);
        Assert.Equal(RunState.UnloadPreparation, run.State);
        Assert.Equal(0, run.Revision);
    }

    [Fact]
    public async Task IntentAndFeedbackUseSeparateCommittedTransactionsAroundExternalDispatch()
    {
        var events = new StageEventStore(options);
        var now = DateTimeOffset.UtcNow;
        var operationId = Guid.NewGuid();
        var common = new StageEventAppendRequest(Guid.NewGuid(), runId, trayId,
            stationId.ToString(), lineId.ToString(), WholeTrayWorkflowStage.Sorting,
            operationId, 1, 7, StageEventType.IntentRecorded, now, ResultSource.Fallback,
            ResultQuality.Derived, null, "intent-digest", "{\"intent\":true}",
            "separate-intent", planRevision, now, now.AddSeconds(120));
        var intent = await events.AppendAsync(common);

        // This query represents the external dispatch boundary. It can observe the
        // committed intent because StageEventStore no longer owns a transaction.
        await using (var dispatchObservation = new Station01DbContext(options))
        {
            Assert.True(await dispatchObservation.StageEvents.AsNoTracking().AnyAsync(x =>
                x.EventId == intent.Event.EventId));
        }

        var feedback = await events.AppendAsync(common with
        {
            EventId = Guid.NewGuid(), EventType = StageEventType.Completed,
            PayloadDigest = "feedback-digest", PayloadJson = "{\"status\":2}",
            IdempotencyKey = "separate-feedback", OccurredAt = now.AddMilliseconds(1),
            Source = ResultSource.Virtual, Quality = ResultQuality.Measured
        });

        Assert.Equal(StageEventCommitState.Committed, intent.State);
        Assert.Equal(StageEventCommitState.Committed, feedback.State);
        Assert.True(intent.Event.Sequence >= 1);
        Assert.Equal(intent.Event.Sequence + 1, feedback.Event.Sequence);
        Assert.Equal(StageProjectionStatus.Completed, feedback.Projection.Status);
    }

    [Fact]
    public async Task ManualFailureRollsBackConfirmationFinalEventMatrixAndTerminalStateTogether()
    {
        var store = new WholeTrayCompletionStore(options, TimeProvider.System,
            (phase, _) => phase == "ManualAndFinal"
                ? Task.FromException(new IOException("injected-before-final-commit"))
                : Task.CompletedTask);
        var completion = await store.CreateAsync(CreateRequest());
        var unlockEvent = await AppendAsync(WholeTrayWorkflowStage.ManualRemovalAdmission,
            StageEventType.ManualRemovalAllowed, "unlock-observed", ResultSource.Virtual);
        var manual = Verified(ComponentKind.ManualActor,
            ComponentEvidenceSource.AuthenticatedHuman, "operator://operator-test");

        await Assert.ThrowsAsync<IOException>(() => store.ConfirmManualRemovalAsync(new(
            Guid.NewGuid(), completion.Reference, unlockEvent.Event.EventId, "operator-test",
            DateTimeOffset.UtcNow, "托盘已人工移除", "manual-final-1", manual)));

        await using var db = new Station01DbContext(options);
        Assert.Single(await db.ComponentEvidenceMatrices.ToListAsync());
        Assert.Empty(await db.StageEvents.Where(x => x.EventType ==
            StageEventType.ManualTrayRemovalConfirmed.ToString()).ToListAsync());
        Assert.Empty(await db.StageEvents.Where(x => x.EventType ==
            StageEventType.FinalUnloadCompleted.ToString()).ToListAsync());
        var run = await db.Runs.SingleAsync(x => x.RunId == runId);
        Assert.Equal(RunState.ReadyForRemoval, run.State);
        Assert.Equal(TerminalOutcome.None, run.Terminal);
    }

    private async Task SeedCompletedStagesAsync()
    {
        await AppendAsync(WholeTrayWorkflowStage.Detection, StageEventType.Completed,
            "detection-completed", ResultSource.Simulated);
        await AppendAsync(WholeTrayWorkflowStage.Sorting, StageEventType.Completed,
            "sorting-completed", ResultSource.Virtual);
        await AppendAsync(WholeTrayWorkflowStage.UnloadPreparation, StageEventType.Completed,
            "unload-completed", ResultSource.Virtual);
    }

    private async Task<StageEventAppendResult> AppendAsync(WholeTrayWorkflowStage stage,
        StageEventType type, string key, ResultSource source)
    {
        var now = DateTimeOffset.UtcNow;
        return await new StageEventStore(options).AppendAsync(new StageEventAppendRequest(
            Guid.NewGuid(), runId, trayId, stationId.ToString(), lineId.ToString(), stage,
            Guid.NewGuid(), 1, 7, type, now, source, ResultQuality.Derived, null,
            $"digest-{key}", "{}", key, planRevision, now.AddSeconds(-1), now.AddSeconds(119)));
    }

    private WholeTrayCompletionCreateRequest CreateRequest()
    {
        var now = DateTimeOffset.UtcNow;
        var evidence = new[]
        {
            Verified(ComponentKind.Host, ComponentEvidenceSource.Test, "host://run"),
            Verified(ComponentKind.Plc, ComponentEvidenceSource.Virtual, "plc://event"),
            Verified(ComponentKind.Camera, ComponentEvidenceSource.Simulated, "camera://capture"),
            Verified(ComponentKind.Light, ComponentEvidenceSource.Simulated, "light://config"),
            Verified(ComponentKind.Algorithm, ComponentEvidenceSource.Simulated, "algorithm://result"),
            ComponentEvidence.NotYetRequired(ComponentKind.ManualActor)
        };
        var matrix = ComponentEvidenceMatrix.Create(Guid.NewGuid(), runId, trayId,
            planRevision, EvidenceMilestone.ReadyForRemoval, evidence);
        return new(Guid.NewGuid(), runId, trayId, stationId, lineId, planRevision,
            matrix, now, "aggregate-1");
    }

    private static ComponentEvidence Verified(ComponentKind component,
        ComponentEvidenceSource source, string reference) => new(component,
        ComponentEvidenceState.Verified, source, "Derived", "test-component/1",
        [reference], DateTimeOffset.UtcNow, "sha256:test-evidence");
}
