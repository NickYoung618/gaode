using Gaode.Application.Ports;
using Gaode.Application.Workflow;
using Gaode.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Gaode.Contracts.Tests.Persistence;

public sealed class StageEventStoreTests : IAsyncLifetime
{
    private SqliteConnection connection = null!;
    private DbContextOptions<Station01DbContext> options = null!;
    private readonly Guid runId = Guid.NewGuid();
    private readonly Guid trayId = Guid.NewGuid();
    private readonly Guid operationId = Guid.NewGuid();
    private StageEventStore store = null!;

    public async Task InitializeAsync()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        options = new DbContextOptionsBuilder<Station01DbContext>().UseSqlite(connection).Options;
        await using (var db = new Station01DbContext(options))
        {
            await db.Database.MigrateAsync();
            db.Runs.Add(new RunEntity { RunId = runId, RequestId = "stage-events", SubjectId = "test", CreatedUtc = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        store = new StageEventStore(options, TimeProvider.System);
    }

    public async Task DisposeAsync() => await connection.DisposeAsync();

    [Fact]
    public async Task AppendProjectsInOneShortTransactionAndReplaysByIdempotency()
    {
        var request = Request(StageEventType.Accepted, "k-1", "digest-1");
        var committed = await store.AppendAsync(request);
        var replay = await store.AppendAsync(request with { EventId = Guid.NewGuid() });
        var conflict = await store.AppendAsync(request with { EventId = Guid.NewGuid(), PayloadDigest = "digest-2" });

        Assert.Equal(StageEventCommitState.Committed, committed.State);
        Assert.Equal(StageEventCommitState.Replay, replay.State);
        Assert.Equal(StageEventCommitState.Conflict, conflict.State);
        Assert.Equal(committed.Event.EventId, replay.Event.EventId);
        Assert.Equal(StageProjectionStatus.Accepted, replay.Projection.Status);
        Assert.Equal(WholeTrayWorkflowStage.Sorting, committed.Event.Stage);
        Assert.Equal(operationId, committed.Event.OperationId);
        Assert.Equal(17, committed.Event.ConnectionEpoch);
        Assert.Single(await store.ReadAsync(runId, trayId, WholeTrayWorkflowStage.Sorting));
    }

    [Fact]
    public async Task EnumParseRoundTripsWorkflowStagesAndUnknownHeldProjection()
    {
        foreach (var stage in new[]
        {
            WholeTrayWorkflowStage.Detection,
            WholeTrayWorkflowStage.Sorting,
            WholeTrayWorkflowStage.UnloadPreparation,
            WholeTrayWorkflowStage.UnlockObservation
        })
        {
            var request = Request(StageEventType.Accepted, $"stage-{stage}", $"digest-{stage}") with { Stage = stage };
            var committed = await store.AppendAsync(request);
            var loaded = await store.GetProjectionAsync(runId, trayId, stage);

            Assert.Equal(stage, committed.Event.Stage);
            Assert.Equal(stage, loaded!.Stage);
            Assert.Equal(stage, Enum.Parse<WholeTrayWorkflowStage>(stage.ToString()));
        }

        var unknown = await store.AppendAsync(Request(StageEventType.UnknownHeld, "unknown-unload", "digest-unknown") with
        {
            Stage = WholeTrayWorkflowStage.UnloadPreparation,
            OperationId = operationId,
            ConnectionEpoch = 18
        });

        Assert.Equal(WholeTrayWorkflowStage.UnloadPreparation, unknown.Projection.Stage);
        Assert.Equal(StageProjectionStatus.UnknownHeld, unknown.Projection.Status);
        Assert.True(unknown.Projection.DeviceHeld);
        Assert.False(unknown.Projection.AutomaticRetryAllowed);
    }

    [Fact]
    public async Task RestartRecoversAcceptedExecutingWithoutTerminalAsUnknownHeld()
    {
        await store.AppendAsync(Request(StageEventType.Started, "k-start", "d-start"));
        await store.AppendAsync(Request(StageEventType.Accepted, "k-accepted", "d-accepted"));
        await store.AppendAsync(Request(StageEventType.Executing, "k-executing", "d-executing"));

        var recovered = await store.RecoverAsync(runId, trayId, WholeTrayWorkflowStage.Sorting);
        var events = await store.ReadAsync(runId, trayId, WholeTrayWorkflowStage.Sorting);

        Assert.Equal(StageProjectionStatus.UnknownHeld, recovered.Status);
        Assert.True(recovered.DeviceHeld);
        Assert.False(recovered.AutomaticRetryAllowed);
        Assert.Contains(events, x => x.EventType == StageEventType.UnknownHeld &&
            x.ErrorCode == "RecoveryInFlight");
        Assert.Equal(4, events.Count);
    }

    [Fact]
    public async Task RestartKeepsCommittedIntentHeldWhenNoLaterFactCouldBeSaved()
    {
        await store.AppendAsync(Request(StageEventType.IntentRecorded, "intent-only", "intent-digest"));
        var recovered = await new StageEventStore(options).RecoverAsync(runId, trayId, WholeTrayWorkflowStage.Sorting);
        Assert.Equal(StageProjectionStatus.UnknownHeld, recovered.Status);
        Assert.True(recovered.DeviceHeld);
        Assert.False(recovered.AutomaticRetryAllowed);
        var actual = await store.ReadAsync(runId, trayId, WholeTrayWorkflowStage.Sorting);
        Assert.DoesNotContain(actual, e => e.EventType == StageEventType.Completed);
        Assert.Equal(2, actual.Count);
        Assert.Equal(2, (await store.RecoverAsync(runId, trayId, WholeTrayWorkflowStage.Sorting)).Revision);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReservationRecoveryUsesCommittedActionClosureRatherThanMissingPickRecord(bool completed)
    {
        var assignmentOperation = Guid.NewGuid();
        var payload = System.Text.Json.JsonSerializer.Serialize(new {
            kind = "SortingAssignmentsReserved", assignments = new[] { new { operationId = assignmentOperation } }
        });
        await store.AppendAsync(Request(StageEventType.IntentRecorded, "reserved", "reservation-digest") with { PayloadJson = payload });
        if (completed)
            await store.AppendAsync(Request(StageEventType.Completed, "assignment-complete", "completed-digest") with { OperationId = assignmentOperation });
        var recovered = await new StageEventStore(options).RecoverAsync(runId, trayId, WholeTrayWorkflowStage.Sorting);
        Assert.Equal(!completed, recovered.DeviceHeld);
        Assert.Equal(completed ? StageProjectionStatus.Completed : StageProjectionStatus.UnknownHeld, recovered.Status);
        var actual = await store.ReadAsync(runId, trayId, WholeTrayWorkflowStage.Sorting);
        Assert.Equal(completed ? 0 : 1, actual.Count(e => e.EventType == StageEventType.UnknownHeld));
    }

    [Fact]
    public async Task RestartRecoversDetectionInFlightAsDisconnectedWithoutHoldingDevice()
    {
        var detectionOperation = Guid.NewGuid();
        await store.AppendAsync(Request(StageEventType.Accepted, "detection-accepted", "detection-a") with
        {
            Stage = WholeTrayWorkflowStage.Detection,
            OperationId = detectionOperation
        });
        await store.AppendAsync(Request(StageEventType.Executing, "detection-executing", "detection-e") with
        {
            Stage = WholeTrayWorkflowStage.Detection,
            OperationId = detectionOperation
        });

        var recovered = await store.RecoverAsync(runId, trayId, WholeTrayWorkflowStage.Detection);
        var events = await store.ReadAsync(runId, trayId, WholeTrayWorkflowStage.Detection);

        Assert.Equal(StageProjectionStatus.Disconnected, recovered.Status);
        Assert.False(recovered.DeviceHeld);
        Assert.True(recovered.AutomaticRetryAllowed);
        Assert.Contains(events, x => x.EventType == StageEventType.Disconnected &&
            x.OperationId == detectionOperation && x.ConnectionEpoch == 17);
        Assert.DoesNotContain(events, x => x.EventType == StageEventType.UnknownHeld);
    }

    [Fact]
    public async Task CommittedEventsCarrySevenYearRetentionAndNoBusinessCompletionIsSynthesized()
    {
        var result = await store.AppendAsync(Request(StageEventType.Executing, "k-retain", "d-retain"));
        Assert.True(result.Event.RetainUntil >= result.Event.PersistedAt.AddYears(7));
        Assert.DoesNotContain(await store.ReadAsync(runId, trayId, WholeTrayWorkflowStage.Sorting),
            x => x.EventType is StageEventType.FinalUnloadCompleted or StageEventType.ObservedUnlocked);
    }

    [Fact]
    public async Task FailedProjectionWriteDoesNotExposeASecondEvent()
    {
        var first = Request(StageEventType.Accepted, "k-atomic-1", "d-atomic-1");
        await store.AppendAsync(first);
        var failed = await Assert.ThrowsAsync<StageEventCommitException>(() => store.AppendAsync(first with
        {
            IdempotencyKey = "k-atomic-2"
        }));
        Assert.Equal(ActualCommitState.ConfirmedRolledBack, failed.ActualCommit);
        Assert.IsType<DbUpdateException>(failed.InnerException);

        var events = await store.ReadAsync(runId, trayId, WholeTrayWorkflowStage.Sorting);
        var projection = await store.GetProjectionAsync(runId, trayId, WholeTrayWorkflowStage.Sorting);
        Assert.Single(events);
        Assert.Equal(first.EventId, events[0].EventId);
        Assert.Equal(1, projection!.Revision);
    }

    [Fact]
    public async Task DispatchGateDispatchesOnlyAfterCommittedIntentAndNeverOnReplay()
    {
        var gate = new StageDispatchGate(store);
        var calls = 0;
        var request = Request(StageEventType.Started, "k-dispatch", "d-dispatch");
        var first = await gate.CommitIntentThenDispatchAsync(request, async _ =>
        {
            Assert.NotNull(await store.GetProjectionAsync(runId, trayId, WholeTrayWorkflowStage.Sorting));
            calls++;
        });
        var replay = await gate.CommitIntentThenDispatchAsync(request with { EventId = Guid.NewGuid() }, _ =>
        {
            calls++;
            return Task.CompletedTask;
        });

        Assert.Equal(StageEventCommitState.Committed, first.State);
        Assert.Equal(StageEventCommitState.Replay, replay.State);
        Assert.Equal(1, calls);
    }

    private StageEventAppendRequest Request(StageEventType type, string key, string digest) =>
        new(Guid.NewGuid(), runId, trayId, "Station01", "LineA", WholeTrayWorkflowStage.Sorting,
            operationId, 1, 17, type, DateTimeOffset.UtcNow, ResultSource.Simulated,
            ResultQuality.Degraded, null, digest, "{}", key);
}
