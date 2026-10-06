using System.Text.Json;
using Gaode.Application.Workflow;
using Gaode.Application.Ports;
using Microsoft.EntityFrameworkCore;
using Gaode.Diagnostics;

namespace Gaode.Infrastructure.Persistence;

public sealed class StageEventStore(DbContextOptions<Station01DbContext> options, TimeProvider? clock = null)
    : IStageEventStore
{
    private readonly TimeProvider clock = clock ?? TimeProvider.System;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public event Action<Guid>? RunCommitted;

    public async Task<StageEventAppendResult> AppendAsync(StageEventAppendRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await RuntimeDiagnostics.ObserveAsync(
            "StageEventSave", request.RunId, new { request.OperationId, request.EventId,
                stage = request.Stage.ToString(), eventType = request.EventType.ToString(),
                request.Attempt, request.ConnectionEpoch, request.ErrorCode,
                request.StageStartedAtUtc, request.StageDeadlineAtUtc, request.PlanRevision,
                source = request.Source.ToString(), quality = request.Quality.ToString() },
            () => AppendCoreAsync(request, cancellationToken),
            r => new { request.OperationId, request.EventId, stage = request.Stage.ToString(),
                eventType = request.EventType.ToString(), commitState = r.State.ToString(),
                status = r.Projection.Status.ToString(), r.Projection.DeviceHeld,
                r.Projection.AutomaticRetryAllowed, error = r.ErrorCode ?? request.ErrorCode },
            r => !r.IsCommitted || request.ErrorCode is not null);
        if (result.State == StageEventCommitState.Committed)
        {
            try { RunCommitted?.Invoke(request.RunId); }
            catch (Exception error) { RuntimeDiagnostics.Record("RunNotification", "StageCommitHintFailed", request.RunId,
                new { request.EventId, actualCommit = "Committed" }, error); }
        }
        return result;
    }

    private async Task<StageEventAppendResult> AppendCoreAsync(StageEventAppendRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.IsValid) throw new ArgumentException("阶段事件合同不完整", nameof(request));
        await using var db = new Station01DbContext(options);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var commitStarted = false;
        try
        {
        var existing = await db.StageIdempotencies.SingleOrDefaultAsync(x =>
            x.IdempotencyKey == request.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            var state = existing.PayloadDigest == request.PayloadDigest && existing.RunId == request.RunId
                ? StageEventCommitState.Replay : StageEventCommitState.Conflict;
            var existingEvent = await db.StageEvents.SingleAsync(x => x.EventId == existing.EventId, cancellationToken);
            var projection = await db.StageProjections.SingleAsync(x =>
                x.RunId == existing.RunId && x.TrayId == existing.TrayId && x.Stage == existing.Stage,
                cancellationToken);
            commitStarted = true;
            await tx.CommitAsync(cancellationToken);
            return new(state, ToEvent(existingEvent), ToProjection(projection),
                state == StageEventCommitState.Conflict ? "IdempotencyConflict" : null);
        }

        var previous = await db.StageProjections.SingleOrDefaultAsync(x =>
            x.RunId == request.RunId && x.TrayId == request.TrayId && x.Stage == request.Stage.ToString(),
            cancellationToken);
        var sequence = (previous?.Revision ?? 0) + 1;
        var now = clock.GetUtcNow();
        var entity = new StageEventEntity
        {
            EventId = request.EventId, RunId = request.RunId, TrayId = request.TrayId,
            StationId = request.StationId, LineId = request.LineId, Stage = request.Stage.ToString(),
            OperationId = request.OperationId, Attempt = request.Attempt,
            PlanRevision = request.PlanRevision,
            StageStartedUtc = request.StageStartedAtUtc,
            StageDeadlineUtc = request.StageDeadlineAtUtc,
            ConnectionEpoch = request.ConnectionEpoch, EventType = request.EventType.ToString(),
            OccurredUtc = request.OccurredAt, PersistedUtc = now, Source = request.Source.ToString(),
            Quality = request.Quality.ToString(), ErrorCode = request.ErrorCode,
            PayloadDigest = request.PayloadDigest, PayloadJson = request.PayloadJson,
            IdempotencyKey = request.IdempotencyKey, Sequence = sequence,
            RetainUntilUtc = StageEventRetentionPolicy.RetainUntil(now)
        };
        var projected = StageEventProjection.Apply(previous is null ?
            StageEventProjection.Initial(ToEvent(entity)) : ToProjection(previous), ToEvent(entity));
        db.StageEvents.Add(entity);
        db.StageIdempotencies.Add(new StageIdempotencyEntity
        {
            IdempotencyKey = request.IdempotencyKey, PayloadDigest = request.PayloadDigest,
            RunId = request.RunId, TrayId = request.TrayId, Stage = request.Stage.ToString(),
            OperationId = request.OperationId, EventId = request.EventId, CreatedUtc = now
        });
        if (previous is null)
            db.StageProjections.Add(ToEntity(projected));
        else
        {
            previous.Revision = projected.Revision; previous.Status = projected.Status.ToString();
            previous.CurrentOperationId = projected.CurrentOperationId;
            previous.ConnectionEpoch = projected.ConnectionEpoch; previous.DeviceHeld = projected.DeviceHeld;
            previous.NeedsManualReview = projected.NeedsManualReview; previous.LastEventId = projected.LastEventId;
            previous.UpdatedUtc = projected.UpdatedAt; previous.RetainUntilUtc = projected.RetainUntil;
        }
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            commitStarted = true;
            await tx.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException failure) when (!commitStarted)
        {
            await tx.RollbackAsync(CancellationToken.None);
            RuntimeDiagnostics.Record("StageEventSave", "RollbackConfirmed", request.RunId,
                new { request.EventId, request.OperationId, actualCommit = "ConfirmedRolledBack" });
            var replay = await ResolveExistingAsync(request, cancellationToken);
            if (replay is not null) return replay;
            throw new StageEventCommitException(request.EventId, ActualCommitState.ConfirmedRolledBack, failure);
        }
        return new(StageEventCommitState.Committed, ToEvent(entity), projected, null);
        }
        catch (Exception error) when (error is not StageEventCommitException)
        {
            var actual = ActualCommitState.Unknown;
            if (!commitStarted)
            {
                try { await tx.RollbackAsync(CancellationToken.None); actual = ActualCommitState.ConfirmedRolledBack;
                    RuntimeDiagnostics.Record("StageEventSave", "RollbackConfirmed", request.RunId,
                        new { request.EventId, request.OperationId, actualCommit = actual.ToString() }); }
                catch (Exception rollback) { RuntimeDiagnostics.Record("StageEventSave", "RollbackUnconfirmed", request.RunId,
                    new { request.EventId, request.OperationId }, rollback); }
            }
            throw new StageEventCommitException(request.EventId, actual, error);
        }
    }

    private async Task<StageEventAppendResult?> ResolveExistingAsync(StageEventAppendRequest request,
        CancellationToken cancellationToken)
    {
        await using var lookup = new Station01DbContext(options);
        var existing = await lookup.StageIdempotencies.AsNoTracking().SingleOrDefaultAsync(x =>
            x.IdempotencyKey == request.IdempotencyKey, cancellationToken);
        if (existing is null) return null;
        var existingEvent = await lookup.StageEvents.AsNoTracking().SingleAsync(x =>
            x.EventId == existing.EventId, cancellationToken);
        var projection = await lookup.StageProjections.AsNoTracking().SingleAsync(x =>
            x.RunId == existing.RunId && x.TrayId == existing.TrayId && x.Stage == existing.Stage,
            cancellationToken);
        var state = existing.PayloadDigest == request.PayloadDigest && existing.RunId == request.RunId
            ? StageEventCommitState.Replay : StageEventCommitState.Conflict;
        return new(state, ToEvent(existingEvent), ToProjection(projection),
            state == StageEventCommitState.Conflict ? "IdempotencyConflict" : null);
    }

    public async Task<IReadOnlyList<StageEvent>> ReadAsync(Guid runId, Guid trayId,
        WholeTrayWorkflowStage stage, CancellationToken cancellationToken = default)
    {
        await using var db = new Station01DbContext(options);
        var rows = await db.StageEvents.AsNoTracking().Where(x => x.RunId == runId &&
            x.TrayId == trayId && x.Stage == stage.ToString()).OrderBy(x => x.Sequence)
            .ToListAsync(cancellationToken);
        return rows.Select(ToEvent).ToArray();
    }

    public async Task<StageProjection?> GetProjectionAsync(Guid runId, Guid trayId,
        WholeTrayWorkflowStage stage, CancellationToken cancellationToken = default)
    {
        await using var db = new Station01DbContext(options);
        var row = await db.StageProjections.AsNoTracking().SingleOrDefaultAsync(x =>
            x.RunId == runId && x.TrayId == trayId && x.Stage == stage.ToString(), cancellationToken);
        return row is null ? null : ToProjection(row);
    }

    public async Task<StageProjection> RecoverAsync(Guid runId, Guid trayId, WholeTrayWorkflowStage stage,
        CancellationToken cancellationToken = default)
    {
        var events = await ReadAsync(runId, trayId, stage, cancellationToken);
        if (events.Count == 0) throw new InvalidOperationException("没有可重放的已提交阶段事件");
        var projection = events.Aggregate(StageEventProjection.Initial(events[0]),
            StageEventProjection.Apply);
        var isPlcStage = stage is WholeTrayWorkflowStage.Sorting or
            WholeTrayWorkflowStage.UnloadPreparation or WholeTrayWorkflowStage.UnlockObservation;
        bool UnclosedIntent(StageEvent item)
        {
            if (!isPlcStage || item.EventType != StageEventType.IntentRecorded) return false;
            using var payload = JsonDocument.Parse(item.PayloadJson);
            if (!payload.RootElement.TryGetProperty("kind", out var kind) || kind.GetString() != "SortingAssignmentsReserved")
                return true;
            // Reservation belongs to the mapping operation, while each transfer has its
            // own operation. Only actual committed closure of all transfers closes it.
            var assignments = payload.RootElement.GetProperty("assignments").EnumerateArray().ToArray();
            return assignments.Any(a => !events.Any(e =>
                e.OperationId == a.GetProperty("operationId").GetGuid() && e.EventType == StageEventType.Completed));
        }
        var open = events.GroupBy(x => x.OperationId).Where(g =>
            g.Any(x => x.EventType is StageEventType.Started or StageEventType.Accepted or
                StageEventType.Executing || UnclosedIntent(x)) &&
            !g.Any(x => StageEventProjection.IsTerminal(x.EventType))).ToArray();
        foreach (var group in open)
        {
            var last = group.OrderBy(x => x.Sequence).Last();
            var key = $"recovery:{last.OperationId:N}:{last.ConnectionEpoch}";
            var recoveryType = isPlcStage ? StageEventType.UnknownHeld : StageEventType.Disconnected;
            var recoveryError = isPlcStage ? "RecoveryInFlight" : "DetectionRecoveryDisconnected";
            var request = new StageEventAppendRequest(Guid.NewGuid(), runId, trayId, last.StationId,
                last.LineId, stage, last.OperationId, last.Attempt, last.ConnectionEpoch,
                recoveryType, clock.GetUtcNow(), ResultSource.Fallback,
                ResultQuality.Unknown, recoveryError, "recovery-in-flight", "{}", key,
                last.PlanRevision, last.StageStartedAtUtc, last.StageDeadlineAtUtc);
            projection = (await AppendAsync(request, cancellationToken)).Projection;
        }
        return projection;
    }

    private static StageEvent ToEvent(StageEventEntity x) => new(Guid.Parse(x.EventId.ToString()), x.RunId,
        x.TrayId, x.StationId, x.LineId, Enum.Parse<WholeTrayWorkflowStage>(x.Stage), x.OperationId,
        x.Attempt, x.ConnectionEpoch, Enum.Parse<StageEventType>(x.EventType), x.OccurredUtc,
        x.PersistedUtc, Enum.Parse<ResultSource>(x.Source), Enum.Parse<ResultQuality>(x.Quality),
        x.ErrorCode, x.PayloadDigest, x.PayloadJson, x.IdempotencyKey, x.Sequence, x.RetainUntilUtc,
        x.PlanRevision, x.StageStartedUtc, x.StageDeadlineUtc);

    private static StageProjection ToProjection(StageProjectionEntity x) => new(x.RunId, x.TrayId,
        x.StationId, x.LineId, Enum.Parse<WholeTrayWorkflowStage>(x.Stage), x.Revision,
        Enum.Parse<StageProjectionStatus>(x.Status), x.CurrentOperationId, x.ConnectionEpoch,
        x.DeviceHeld, x.NeedsManualReview, x.LastEventId, x.UpdatedUtc, x.RetainUntilUtc);

    private static StageProjectionEntity ToEntity(StageProjection x) => new()
    {
        ProjectionId = Guid.NewGuid(), RunId = x.RunId, TrayId = x.TrayId, StationId = x.StationId,
        LineId = x.LineId, Stage = x.Stage.ToString(), Revision = x.Revision, Status = x.Status.ToString(),
        CurrentOperationId = x.CurrentOperationId, ConnectionEpoch = x.ConnectionEpoch,
        DeviceHeld = x.DeviceHeld, NeedsManualReview = x.NeedsManualReview, LastEventId = x.LastEventId,
        UpdatedUtc = x.UpdatedAt, RetainUntilUtc = x.RetainUntil
    };
}
