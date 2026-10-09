using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Application.Workflow;
using Gaode.Domain.Station01;
using Microsoft.EntityFrameworkCore;

namespace Gaode.Infrastructure.Persistence;

public sealed class WholeTrayCompletionStore(
    DbContextOptions<Station01DbContext> options,
    TimeProvider? clock = null,
    Func<string, CancellationToken, Task>? beforeCommit = null, Gaode.Application.Station01.CommandRegistry? commands = null) : IWholeTrayCompletionStore
{
    private readonly TimeProvider clock = clock ?? TimeProvider.System;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<WholeTrayCompletionRecord> CreateAsync(WholeTrayCompletionCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.IsValid) throw new ArgumentException("整盘完成请求合同不完整", nameof(request));
        if (!request.SourceMatrix.IsComplete)
            throw new InvalidOperationException("WholeTraySourceMatrixIncomplete:" +
                string.Join(',', request.SourceMatrix.BlockedComponents));

        await using var db = new Station01DbContext(options);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var replay = await db.WholeTrayCompletions.AsNoTracking().SingleOrDefaultAsync(x =>
            x.RunId == request.RunId && x.TrayId == request.TrayId, cancellationToken);
        if (replay is not null)
        {
            await tx.CommitAsync(cancellationToken);
            if (replay.WholeTrayCompletionId != request.CompletionId)
                throw new InvalidOperationException("WholeTrayCompletionConflict");
            return await LoadAsync(db, replay, cancellationToken);
        }

        var detection = request.EndReason == TrayEndReason.NormalCompletion
            ? await CompletedAsync(db, request, WholeTrayWorkflowStage.Detection, cancellationToken) : null;
        var sorting = request.EndReason == TrayEndReason.NormalCompletion
            ? await CompletedAsync(db, request, WholeTrayWorkflowStage.Sorting, cancellationToken) : null;
        var unload = await CompletedAsync(db, request, WholeTrayWorkflowStage.UnloadPreparation, cancellationToken);
        if (request.EndReason != TrayEndReason.NormalCompletion)
            await RequireEarlyEndBasisAsync(db, request, cancellationToken);
        var run = await db.Runs.SingleAsync(x => x.RunId == request.RunId, cancellationToken);
        if (run.Terminal != TerminalOutcome.None)
            throw new InvalidOperationException("WholeTrayRunAlreadyTerminal");
        var reference = new WholeTrayCompletionReference(request.CompletionId, request.RunId,
            request.TrayId, request.StationId, request.LineId, request.PlanRevision,
            detection?.EventId ?? Guid.Empty, sorting?.EventId ?? Guid.Empty, unload.EventId) {
                EndReason = request.EndReason, InspectionCompleted = request.InspectionCompleted,
                RecipePlanRevision = request.RecipePlanRevision, EndBasisReference = request.EndBasisReference };
        var now = clock.GetUtcNow();
        var retain = StageEventRetentionPolicy.RetainUntil(now);
        var matrix = MatrixEntity(request.SourceMatrix, now, retain);
        var recordDigest = Digest(JsonSerializer.Serialize(new
        {
            reference, matrix.MatrixDigest, scope = request.SourceMatrix.Scope.ToString()
        }, JsonOptions));
        var payload = JsonSerializer.Serialize(new CompletionPayload(reference,
            request.SourceMatrix.MatrixId, request.CreatedAtUtc, now, recordDigest), JsonOptions);

        run.Revision++;
        run.State = RunState.ReadyForRemoval;
        db.ComponentEvidenceMatrices.Add(matrix);
        db.WholeTrayCompletions.Add(new WholeTrayCompletionEntity
        {
            WholeTrayCompletionId = request.CompletionId, RunId = request.RunId,
            TrayId = request.TrayId, PlanRevision = request.PlanRevision,
            DetectionCompletedEventId = detection?.EventId,
            SortingCompletedEventId = sorting?.EventId,
            UnloadPreparationCompletedEventId = unload.EventId,
            SourceMatrixId = matrix.MatrixId, PersistedRevision = run.Revision,
            CreatedUtc = now, RetainUntilUtc = retain
        });
        Append(db, reference, WholeTrayWorkflowStage.ManualRemovalAdmission, request.CompletionId,
            unload.ConnectionEpoch, StageEventType.WholeTrayCompleted, now,
            ResultSource.HostDerived,
            ResultQuality.Derived, null, payload, request.IdempotencyKey + ":evidence");
        if (beforeCommit is not null) await beforeCommit("WholeTrayAggregate", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return new(reference, request.SourceMatrix, request.CreatedAtUtc, now,
            recordDigest, retain);
    }

    public async Task<WholeTrayCompletionRecord?> GetAsync(WholeTrayCompletionReference reference,
        CancellationToken cancellationToken = default)
    {
        await using var db = new Station01DbContext(options);
        var entity = await db.WholeTrayCompletions.AsNoTracking().SingleOrDefaultAsync(x =>
            x.WholeTrayCompletionId == reference.CompletionId && x.RunId == reference.RunId &&
            x.TrayId == reference.TrayId, cancellationToken);
        if (entity is null) return null;
        var value = await LoadAsync(db, entity, cancellationToken);
        return value.Reference == reference ? value : null;
    }

    public async Task<WholeTrayCompletionRecord?> GetByRunAsync(Guid runId,
        CancellationToken cancellationToken = default)
    {
        await using var db = new Station01DbContext(options);
        var entity = await db.WholeTrayCompletions.AsNoTracking().SingleOrDefaultAsync(x =>
            x.RunId == runId, cancellationToken);
        return entity is null ? null : await LoadAsync(db, entity, cancellationToken);
    }

    public async Task<FinalUnloadCompletion> ConfirmManualRemovalAsync(
        ManualTrayRemovalConfirmationRequest request, CancellationToken cancellationToken = default)
    {
        if (!request.IsValid) throw new ArgumentException("人工取盘确认请求合同不完整", nameof(request));
        await using var db = new Station01DbContext(options);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var completion = await db.WholeTrayCompletions.SingleOrDefaultAsync(x =>
            x.WholeTrayCompletionId == request.WholeTray.CompletionId &&
            x.RunId == request.WholeTray.RunId && x.TrayId == request.WholeTray.TrayId,
            cancellationToken) ?? throw new InvalidOperationException("WholeTrayCompletionReferenceNotPersisted");
        var unlock = await db.StageEvents.AsNoTracking().SingleOrDefaultAsync(x =>
            x.EventId == request.ManualRemovalAllowedEventId && x.RunId == request.WholeTray.RunId &&
            x.TrayId == request.WholeTray.TrayId &&
            x.EventType == StageEventType.ManualRemovalAllowed.ToString(), cancellationToken);
        if (unlock is null) throw new InvalidOperationException("ManualRemovalAllowanceRequired");

        var replay = await db.StageEvents.AsNoTracking().SingleOrDefaultAsync(x =>
            x.IdempotencyKey == request.IdempotencyKey + ":final", cancellationToken);
        if (replay is not null)
        {
            await tx.CommitAsync(cancellationToken);
            await ReconcileFinalAsync(request.WholeTray.RunId, request.WholeTray.TrayId, cancellationToken);
            return ParseFinal(replay.PayloadJson);
        }

        await RequireAlgorithmResourcesAsync(db, request.WholeTray.RunId, cancellationToken);
        var readyEntity = await db.ComponentEvidenceMatrices.AsNoTracking().SingleAsync(x =>
            x.MatrixId == completion.SourceMatrixId, cancellationToken);
        var ready = ParseMatrix(readyEntity);
        var finalMatrix = ComponentEvidenceMatrix.Create(Guid.NewGuid(), ready.RunId, ready.TrayId,
            ready.PlanRevision, EvidenceMilestone.FinalUnloadCompletion,
            ready.Components.Where(x => x.Component != ComponentKind.ManualActor)
                .Append(request.ManualActorEvidence));
        if (!finalMatrix.IsComplete)
            throw new InvalidOperationException("FinalSourceMatrixIncomplete:" +
                string.Join(',', finalMatrix.BlockedComponents));

        var final = new FinalUnloadCompletion(request.FinalCompletionId, request.WholeTray,
            request.ManualRemovalAllowedEventId, request.OperatorId, request.ConfirmedAtUtc,
            "ManualTrayRemovalConfirmation", true, request.Reason, finalMatrix.MatrixId);
        var payload = JsonSerializer.Serialize(new FinalPayload(final, finalMatrix.Scope), JsonOptions);
        var retain = StageEventRetentionPolicy.RetainUntil(request.ConfirmedAtUtc);
        var testActor = request.ManualActorEvidence.Source == ComponentEvidenceSource.Test;
        var eventSource = testActor ? ResultSource.Test : ResultSource.Real;
        var eventQuality = testActor ? ResultQuality.Derived : ResultQuality.Measured;
        db.ComponentEvidenceMatrices.Add(MatrixEntity(finalMatrix, request.ConfirmedAtUtc, retain));
        Append(db, request.WholeTray, WholeTrayWorkflowStage.ManualTrayRemovalConfirmation,
            Guid.NewGuid(), unlock.ConnectionEpoch, StageEventType.ManualTrayRemovalConfirmed,
            request.ConfirmedAtUtc, eventSource, eventQuality, null, payload,
            request.IdempotencyKey + ":manual");
        Append(db, request.WholeTray, WholeTrayWorkflowStage.ManualTrayRemovalConfirmation,
            Guid.NewGuid(), unlock.ConnectionEpoch, StageEventType.FinalUnloadCompleted,
            request.ConfirmedAtUtc, ResultSource.HostDerived, ResultQuality.Derived, null, payload,
            request.IdempotencyKey + ":final");
        var run = await db.Runs.SingleAsync(x => x.RunId == request.WholeTray.RunId, cancellationToken);
        run.Revision++;
        run.State = RunState.Completed;
        run.Terminal = TerminalOutcome.Completed;
        run.TerminalRevision = run.Revision;
        if (beforeCommit is not null) await beforeCommit("ManualAndFinal", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        await ReconcileFinalAsync(request.WholeTray.RunId, request.WholeTray.TrayId, cancellationToken);
        return final;
    }

    public async Task<bool> ReconcileFinalAsync(Guid runId, Guid trayId, CancellationToken cancellationToken)
    {
        await using var db = new Station01DbContext(options);
        var run = await db.Runs.AsNoTracking().SingleOrDefaultAsync(x => x.RunId == runId, cancellationToken);
        if (run is null || run.State != RunState.Completed || run.Terminal != TerminalOutcome.Completed) return false;
        await RequireAlgorithmResourcesAsync(db, runId, cancellationToken);
        var facts = await db.StageEvents.AsNoTracking().Where(x => x.RunId == runId && x.TrayId == trayId &&
            x.Stage == WholeTrayWorkflowStage.ManualTrayRemovalConfirmation.ToString()).ToListAsync(cancellationToken);
        var finalEvent = facts.LastOrDefault(x => x.EventType == StageEventType.FinalUnloadCompleted.ToString());
        if (finalEvent is null) return false;
        var final = ParseFinal(finalEvent.PayloadJson);
        if (!final.IsValid || final.WholeTray.RunId != runId || final.WholeTray.TrayId != trayId ||
            !facts.Any(x => x.EventType == StageEventType.ManualTrayRemovalConfirmed.ToString() &&
                x.PayloadJson == finalEvent.PayloadJson)) return false;
        var matrix = await db.ComponentEvidenceMatrices.AsNoTracking().SingleOrDefaultAsync(x =>
            x.MatrixId == final.FinalSourceMatrixId && x.RunId == runId && x.TrayId == trayId, cancellationToken);
        if (matrix is null || !ParseMatrix(matrix).IsComplete ||
            ParseMatrix(matrix).Milestone != EvidenceMilestone.FinalUnloadCompletion) return false;
        commands?.ReleaseAfterCommittedFinal(runId);
        Gaode.Diagnostics.RuntimeDiagnostics.Record("CompletionAdmission", "CommittedFinalReconciled", runId,
            new { trayId, final.FinalCompletionId, final.FinalSourceMatrixId });
        return true;
    }

    private static async Task RequireAlgorithmResourcesAsync(Station01DbContext db, Guid runId, CancellationToken token)
    {
        var kind = nameof(StageEventType.AlgorithmLifecycleRecorded);
        var unknown = await db.StageEvents.FromSqlInterpolated($"""
            SELECT e.* FROM StageEvents e
            WHERE e.RunId = {runId} AND e.EventType = {kind}
              AND NOT EXISTS (SELECT 1 FROM StageEvents n WHERE n.EventType = {kind}
                AND n.RunId = e.RunId
                AND json_extract(n.PayloadJson, '$.callId') = json_extract(e.PayloadJson, '$.callId')
                AND n.Sequence > e.Sequence)
              AND COALESCE(json_extract(e.PayloadJson, '$.reclaimed'), 0) = 0
            """).AsNoTracking().AnyAsync(token);
        if (unknown) throw new InvalidOperationException("AlgorithmResourcesUnconfirmed");
    }

    private static async Task<StageEventEntity> CompletedAsync(Station01DbContext db,
        WholeTrayCompletionCreateRequest request, WholeTrayWorkflowStage stage,
        CancellationToken cancellationToken)
    {
        var rows = await db.StageEvents.AsNoTracking().Where(x => x.RunId == request.RunId &&
            x.TrayId == request.TrayId && x.Stage == stage.ToString())
            .OrderBy(x => x.Sequence).ToListAsync(cancellationToken);
        var completed = rows.LastOrDefault();
        if (completed is null || completed.EventType != StageEventType.Completed.ToString())
            throw new InvalidOperationException($"{stage}CompletedEvidenceMissing");
        if (completed.StationId != request.StationId.ToString() ||
            completed.LineId != request.LineId.ToString() ||
            completed.PlanRevision != request.PlanRevision)
            throw new InvalidOperationException(
                $"WholeTrayIdentityOrPlanMismatch:{stage}:" +
                $"station={completed.StationId}/{request.StationId:D};" +
                $"line={completed.LineId}/{request.LineId:D};" +
                $"plan={completed.PlanRevision}/{request.PlanRevision}");
        return completed;
    }

    private static async Task RequireEarlyEndBasisAsync(Station01DbContext db,
        WholeTrayCompletionCreateRequest request, CancellationToken token)
    {
        if (request.EndBasisReference is null) throw new InvalidOperationException("TrayEndBasisMissing");
        var parts = request.EndBasisReference.Split("://");
        if (parts.Length != 2 || !Guid.TryParse(parts[1], out var id)) throw new InvalidOperationException("TrayEndBasisInvalid");
        string payload;
        if (parts[0] == "write")
        {
            var write = await db.Writes.AsNoTracking().SingleOrDefaultAsync(w => w.WriteId == id && w.RunId == request.RunId, token)
                ?? throw new InvalidOperationException("TrayEndBasisNotPersisted");
            payload = write.PayloadJson;
            if (Digest(payload) != write.PayloadDigest) throw new InvalidOperationException("TrayEndBasisDigestInvalid");
        }
        else if (parts[0] == "stage-event")
        {
            var fact = await db.StageEvents.AsNoTracking().SingleOrDefaultAsync(w => w.EventId == id && w.RunId == request.RunId && w.TrayId == request.TrayId, token)
                ?? throw new InvalidOperationException("TrayEndBasisNotPersisted");
            payload = fact.PayloadJson;
            if (Digest(payload) != fact.PayloadDigest) throw new InvalidOperationException("TrayEndBasisDigestInvalid");
        }
        else throw new InvalidOperationException("TrayEndBasisInvalid");
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        if (request.EndReason == TrayEndReason.ManualIntervention)
        {
            var decision = root.GetProperty("decision").Deserialize<TrayAnomalyDecisionProjection>(JsonOptions);
            if (decision is not { State: "Decided", Choice: "ManualIntervention" } || decision.RunId != request.RunId)
                throw new InvalidOperationException("InterventionDecisionRequired");
        }
        else
        {
            var fact = root.Deserialize<AlgorithmFactPayload>(JsonOptions);
            var observation = fact is { State: AlgorithmState.Success } ? JsonSerializer.Deserialize<TrayObservation>(fact.RawResultJson) : null;
            if (observation is not { IsEmptyTray: true } || observation.RunId != request.RunId || observation.TrayId != request.TrayId)
                throw new InvalidOperationException("CompleteEmptyTrayObservationRequired");
        }
    }

    private async Task<WholeTrayCompletionRecord> LoadAsync(Station01DbContext db,
        WholeTrayCompletionEntity entity, CancellationToken cancellationToken)
    {
        var matrixEntity = await db.ComponentEvidenceMatrices.AsNoTracking().SingleAsync(x =>
            x.MatrixId == entity.SourceMatrixId, cancellationToken);
        var evt = await db.StageEvents.AsNoTracking().SingleAsync(x => x.RunId == entity.RunId &&
            x.TrayId == entity.TrayId && x.EventType == StageEventType.WholeTrayCompleted.ToString(),
            cancellationToken);
        var payload = JsonSerializer.Deserialize<CompletionPayload>(evt.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException("WholeTrayCompletionPayloadInvalid");
        return new(payload.Reference, ParseMatrix(matrixEntity), payload.CreatedAtUtc,
            payload.PersistedAtUtc, payload.RecordDigest, entity.RetainUntilUtc);
    }

    private void Append(Station01DbContext db, WholeTrayCompletionReference reference,
        WholeTrayWorkflowStage stage, Guid operationId, long epoch, StageEventType type,
        DateTimeOffset at, ResultSource source, ResultQuality quality, string? error,
        string payload, string key)
    {
        var previous = db.StageProjections.Local.SingleOrDefault(x => x.RunId == reference.RunId &&
            x.TrayId == reference.TrayId && x.Stage == stage.ToString()) ??
            db.StageProjections.SingleOrDefault(x => x.RunId == reference.RunId &&
                x.TrayId == reference.TrayId && x.Stage == stage.ToString());
        var sequence = (previous?.Revision ?? 0) + 1;
        var id = Guid.NewGuid();
        var retain = StageEventRetentionPolicy.RetainUntil(at);
        var digest = Digest(payload);
        db.StageEvents.Add(new StageEventEntity
        {
            EventId = id, RunId = reference.RunId, TrayId = reference.TrayId,
            StationId = reference.StationId.ToString(), LineId = reference.LineId.ToString(),
            Stage = stage.ToString(), OperationId = operationId, Attempt = 1,
            PlanRevision = reference.PlanRevision, ConnectionEpoch = epoch,
            EventType = type.ToString(), OccurredUtc = at, PersistedUtc = clock.GetUtcNow(),
            Source = source.ToString(), Quality = quality.ToString(), ErrorCode = error,
            PayloadDigest = digest, PayloadJson = payload, IdempotencyKey = key,
            Sequence = sequence, RetainUntilUtc = retain
        });
        db.StageIdempotencies.Add(new StageIdempotencyEntity
        {
            IdempotencyKey = key, PayloadDigest = digest, RunId = reference.RunId,
            TrayId = reference.TrayId, Stage = stage.ToString(), OperationId = operationId,
            EventId = id, CreatedUtc = clock.GetUtcNow()
        });
        if (previous is null)
        {
            db.StageProjections.Add(new StageProjectionEntity
            {
                ProjectionId = Guid.NewGuid(), RunId = reference.RunId, TrayId = reference.TrayId,
                StationId = reference.StationId.ToString(), LineId = reference.LineId.ToString(),
                Stage = stage.ToString(), Revision = sequence,
                Status = type == StageEventType.FinalUnloadCompleted
                    ? StageProjectionStatus.Completed.ToString() : StageProjectionStatus.NotStarted.ToString(),
                CurrentOperationId = operationId, ConnectionEpoch = epoch, LastEventId = id,
                UpdatedUtc = clock.GetUtcNow(), RetainUntilUtc = retain
            });
        }
        else
        {
            previous.Revision = sequence;
            previous.CurrentOperationId = operationId;
            previous.ConnectionEpoch = epoch;
            previous.LastEventId = id;
            previous.UpdatedUtc = clock.GetUtcNow();
            previous.RetainUntilUtc = retain;
            if (type == StageEventType.FinalUnloadCompleted)
                previous.Status = StageProjectionStatus.Completed.ToString();
        }
    }

    private static ComponentEvidenceMatrixEntity MatrixEntity(ComponentEvidenceMatrix matrix,
        DateTimeOffset created, DateTimeOffset retain) => new()
    {
        MatrixId = matrix.MatrixId, RunId = matrix.RunId, TrayId = matrix.TrayId,
        PlanRevision = matrix.PlanRevision, Milestone = matrix.Milestone.ToString(),
        ComponentsJson = JsonSerializer.Serialize(matrix.Components, JsonOptions),
        MatrixDigest = Digest(JsonSerializer.Serialize(new
        {
            matrix.RunId, matrix.TrayId, matrix.PlanRevision, matrix.Milestone, matrix.Components
        }, JsonOptions)),
        Scope = matrix.Scope.ToString(), CreatedUtc = created, RetainUntilUtc = retain
    };

    private static ComponentEvidenceMatrix ParseMatrix(ComponentEvidenceMatrixEntity entity) =>
        ComponentEvidenceMatrix.Create(entity.MatrixId, entity.RunId, entity.TrayId,
            entity.PlanRevision, Enum.Parse<EvidenceMilestone>(entity.Milestone),
            JsonSerializer.Deserialize<ComponentEvidence[]>(entity.ComponentsJson, JsonOptions)
                ?? throw new InvalidOperationException("ComponentEvidenceMatrixPayloadInvalid"));

    private static FinalUnloadCompletion ParseFinal(string payload) =>
        JsonSerializer.Deserialize<FinalPayload>(payload, JsonOptions)?.Completion
        ?? throw new InvalidOperationException("FinalUnloadPayloadInvalid");

    private sealed record CompletionPayload(WholeTrayCompletionReference Reference, Guid SourceMatrixId,
        DateTimeOffset CreatedAtUtc, DateTimeOffset PersistedAtUtc, string RecordDigest)
    {
        public string SchemaVersion { get; init; } = "tray-end/2";
    }
    private sealed record FinalPayload(FinalUnloadCompletion Completion, EvidenceScope Scope)
    {
        public string SchemaVersion { get; init; } = "tray-end/2";
    }
    private static string Digest(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
