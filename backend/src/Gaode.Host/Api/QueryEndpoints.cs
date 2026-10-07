using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Application.Station01;
using Gaode.Application.Workflow;
using Gaode.Domain.Station01;
using Gaode.Host.Composition;
using Gaode.Infrastructure.Persistence;
using Gaode.Infrastructure.Media;
using Gaode.Infrastructure.Algorithms;
using Gaode.Infrastructure.Simulation;
using Gaode.Infrastructure.Devices.Plc;
using Microsoft.EntityFrameworkCore;

namespace Gaode.Host.Api;

public static class QueryEndpoints
{
    private static SemanticMotionFact MotionFacts(string payload) => DeviceEvidenceHistoryReader.ReadMotion(payload);

    private static FaultRestartProjection? ReadFaultRestart(IReadOnlyList<PersistedWrite> writes)
    {
        foreach (var write in writes.Where(x => x.State == CommitState.Committed).OrderByDescending(x => x.Revision))
        {
            using var doc = JsonDocument.Parse(write.PayloadJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("kind", out var kind) || kind.GetString() is not ("RecoveryNewRunLinked" or "RecoveryFromFaultRun")) continue;
            JsonElement? Field(string name) => root.EnumerateObject().Where(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
                .Select(x => (JsonElement?)x.Value).FirstOrDefault();
            Guid? Id(string name) => Field(name) is { ValueKind: JsonValueKind.String } v && Guid.TryParse(v.GetString(), out var id) ? id : null;
            if (Id("faultRunId") is not { } faultId) continue;
            return new(faultId, Id("resetId"), Id("initialCheckId"), "NewRunLinked", Id("newRunId") ?? write.RunId, [], write.Revision);
        }
        return null;
    }

    public static RouteGroupBuilder MapQueryEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/runs/{runId:guid}", async (Guid runId, Station01Coordinator coordinator,
            ITraceQuery traces, IStageHandoffQuery handoffs,
            IWholeTrayCompletionStore completions, IStageEventStore stageEvents,
            IServiceProvider services, HttpContext http, CancellationToken ct) =>
        {
            var snapshot = coordinator.Query(runId);
            var persisted = await traces.GetRunAsync(runId, ct);
            if (snapshot is null && persisted is null)
                return Station01ApiResults.NotFound(http, "RunNotFound", "运行不存在");
            var historical = snapshot is null;
            snapshot ??= new RunSnapshot(runId, persisted!.RequestId, persisted.SubjectId,
                persisted.State, persisted.Revision, persisted.Revision, persisted.Terminal,
                persisted.CancelRequested, ActionState.NotRequested, CaptureState.NotRequested,
                AlgorithmState.NotRequested, SaveState.NotQueued, HandoffState.NotReady,
                null, null, null, []);
            var handoff = await handoffs.GetCommittedV2Async(runId, ct);
            IReadOnlyList<RunResultProjection> results = [];
            IReadOnlyList<StageEvent> resultFacts = [];
            IReadOnlyList<StageEvent> dispositionFacts = [];
            if (handoff is { IsVerified: true })
            {
                var detectionFacts = await stageEvents.ReadAsync(runId,
                    handoff.Handoff.Identity.TrayId, WholeTrayWorkflowStage.Detection, ct);
                resultFacts = detectionFacts.Where(f => f.PlanRevision == handoff.Handoff.PlanRevision).ToArray();
                dispositionFacts = resultFacts.Concat(await stageEvents.ReadAsync(runId,
                    handoff.Handoff.Identity.TrayId, WholeTrayWorkflowStage.Sorting, ct)).ToArray();
                results = detectionFacts.Where(fact => fact.EventType == StageEventType.Completed &&
                        fact.PlanRevision == handoff.Handoff.PlanRevision)
                    .SelectMany(ReadCommittedResults).ToArray();
                {
                    var hierarchy = detectionFacts.Where(fact => fact.EventType == StageEventType.Executing &&
                        fact.PlanRevision == handoff.Handoff.PlanRevision).SelectMany(ReadHierarchy).ToArray();
                    if (hierarchy.Length > 0) results = hierarchy;
                }
            }
            var display = CommittedResultProjection.Build(results, resultFacts);
            results = display.Results;
            IReadOnlyList<RunMovementProjection> movements = [];
            if (handoff is { IsVerified: true })
            {
                var disposition = CommittedResultProjection.ApplyDisposition(runId, handoff.Handoff.Identity.TrayId,
                    handoff.Handoff.PlanRevision, results, dispositionFacts);
                results = disposition.Results;
                movements = disposition.Movements;
            }
            var resultRevision = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
                new { results, movements, display.Context, events = dispositionFacts.Select(f => new { f.EventId, f.Sequence }) })));
            var allowedActions = Array.Empty<string>();
            var failedRecovery = services.GetService<FixedMoveRecoveryInteraction>()?.Query(runId);
            var faultRestart = services.GetService<FixedMoveRecoveryInteraction>()?.QueryRestart(runId);
            if (faultRestart is null)
                faultRestart = ReadFaultRestart(await traces.GetWritesAsync(runId, ct));
            if (faultRestart is not null && http.User.HasClaim("permission", Station01Authorization.RecoveryCheck))
                allowedActions = faultRestart.Status == "InitialReady"
                    ? http.User.HasClaim("permission", Station01Authorization.Start) ? ["RestartFullRun"] : []
                    : faultRestart.Status == "NewRunLinked" ? [] : [failedRecovery?.ResetEpoch is null ? "RecoveryReset" : "RecoveryCheck"];
            if (snapshot.State == RunState.AwaitingManualRemoval && !snapshot.CancelRequested)
            {
                var whole = await completions.GetByRunAsync(runId, ct);
                if (whole is not null && whole.Reference.RunId == runId)
                {
                    var unlockFacts = await stageEvents.ReadAsync(runId, whole.Reference.TrayId,
                        WholeTrayWorkflowStage.ManualRemovalAdmission, ct);
                    if (unlockFacts.Any(fact => fact.EventType == StageEventType.ManualRemovalAllowed))
                        allowedActions = ["ConfirmManualTrayRemoval"];
                }
            }
            var stageAdvanced = snapshot.State is RunState.Detection or RunState.Sorting or
                RunState.UnloadPreparation or RunState.ReadyForRemoval or RunState.ReadyForUnlock or RunState.ObservedUnlocked or
                RunState.AwaitingManualRemoval or RunState.FinalUnloadCompleted or
                RunState.RecoveryRequired or RunState.Blocked or RunState.Completed or
                RunState.CompletedWithExceptions or RunState.PauseRequested or RunState.Paused or
                RunState.StopPending or RunState.CancelRequested or RunState.Cancelled or
                RunState.Restricted;
            var projected = snapshot with
            {
                State = stageAdvanced ? snapshot.State : persisted?.State ?? snapshot.State,
                PersistedRevision = persisted?.Revision ?? snapshot.PersistedRevision,
                FinalOutcome = persisted?.Terminal ?? snapshot.FinalOutcome,
                RecipeSelection = persisted is null ? null : snapshot.RecipeSelection,
                RecipeState = snapshot.RecipeState,
                Handoff = snapshot.Handoff,
                PlanRevision = handoff?.Handoff.PlanRevision ?? snapshot.PlanRevision,
                RecipeBindingReference = handoff?.Handoff.RecipeBindingReference ??
                    snapshot.RecipeBindingReference,
                HandoffId = handoff?.Handoff.HandoffId ?? snapshot.HandoffId,
                AllowedActions = historical ? [] : allowedActions,
                ResultContext = display.Context,
                ResultRevision = resultRevision,
                Movements = movements,
                FaultRestart = faultRestart,
                FailedCommandRecovery = failedRecovery is null ? null : new(failedRecovery.OperationId,
                    failedRecovery.ActionId, failedRecovery.FailedEpoch, failedRecovery.Role,
                    failedRecovery.DeadlineUtc, failedRecovery.ResetEpoch, failedRecovery.CheckId),
                Results = results,
                WholeTaskState = snapshot.WholeTaskState
            };
            projected = await RuntimeObservationProjection.ReadAsync(projected, traces, stageEvents, ct);
            resultRevision = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
                results = projected.Results, movements, display.Context, projected.RecipeExecution, projected.ExecutionPhase, projected.SortingState,
                projected.SlotStates, projected.AbnormalPhysicalSlotIndices, projected.ObservationCoverage,
                projected.TrayAnomalyDecision, projected.TrayEndReason, projected.InspectionCompleted,
                events = dispositionFacts.Select(f => new { f.EventId, f.Sequence }) })));
            projected = projected with { ResultRevision = resultRevision };
            return Versioned(http, DeviceSemanticProjection.Run(projected)!,
                $"run-{projected.RunId:N}-{projected.ObservedRevision}-{projected.PersistedRevision}-reset-{failedRecovery?.ResetEpoch}-check-{failedRecovery?.CheckId}-results-{resultRevision}");
        })
            .RequireAuthorization(Station01Authorization.Read);
        group.MapGet("/commands/{commandId:guid}", (Guid commandId, CommandRegistry commands, HttpContext http) =>
            commands.Get(commandId) is { } receipt
                ? Versioned(http, receipt, $"command-{receipt.CommandId:N}-{receipt.ReceiptDurability}")
                : Station01ApiResults.NotFound(http, "CommandNotFound", "命令不存在"))
            .RequireAuthorization(Station01Authorization.Read);
        group.MapGet("/runs/{runId:guid}/handoff", async (Guid runId, IStageHandoffQuery query,
            HttpContext http, CancellationToken ct) =>
            await query.GetCommittedV2Async(runId, ct) is { IsVerified: true } committed
                ? Versioned(http, committed.Handoff,
                    $"handoff-v2-{committed.Handoff.Identity.RunId:N}-{committed.PersistedRevision}")
                : Station01ApiResults.Error(http, StatusCodes.Status409Conflict, "HandoffNotReady", "公共移交尚未提交", "State"))
            .RequireAuthorization(Station01Authorization.Read);
        group.MapGet("/runs/{runId:guid}/media", async (Guid runId,
            DbContextOptions<Station01DbContext> options, MediaStore mediaStore,
            HttpContext http, CancellationToken ct) =>
            await RunMediaCatalog.ReadAsync(runId, options, mediaStore, ct) is { } catalog
                ? Versioned(http, catalog,
                    $"run-media-{runId:N}-{Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(catalog)))[..16]}")
                : Station01ApiResults.NotFound(http, "RunNotFound", "运行不存在"))
            .RequireAuthorization(Station01Authorization.Read);
        group.MapGet("/runs/{runId:guid}/evidence", async (Guid runId,
            DbContextOptions<Station01DbContext> options, HttpContext http, CancellationToken ct) =>
        {
            await using var db = new Station01DbContext(options);
            var run = await db.Runs.AsNoTracking().SingleOrDefaultAsync(x => x.RunId == runId, ct);
            if (run is null)
                return Station01ApiResults.NotFound(http, "RunNotFound", "运行不存在");
            var stageRows = await db.StageEvents.AsNoTracking().Where(x => x.RunId == runId)
                .ToListAsync(ct);
            RunDiagnosticReferences diagnostics;
            try { diagnostics = await DeviceEvidenceHistoryReader.ReadReferencesAsync(db, runId, ct); }
            catch (InvalidDataException)
            {
                return Station01ApiResults.Error(http, StatusCodes.Status409Conflict,
                    "DiagnosticEvidenceInvalid", "通信证据的身份或完整性核验未通过", "Storage");
            }
            var stagesList = new List<StageEvidenceApi>();
            foreach (var x in stageRows.OrderBy(x => x.PersistedUtc).ThenBy(x => x.Sequence))
            {
                var history = await DeviceEvidenceHistoryReader.ReadStageAsync(db, x, diagnostics.References, ct);
                stagesList.Add(new StageEvidenceApi(x.EventId, x.Stage, x.EventType, x.OperationId,
                    x.Attempt, x.ConnectionEpoch, x.PlanRevision, x.Source, x.Quality,
                    x.ErrorCode, x.Sequence, x.PersistedUtc, x.StageStartedUtc,
                    x.StageDeadlineUtc, history.Positions, history.ActionId, history.RecordNature, history.RawAvailability,
                    DiagnosticEvidenceReferences: history.DiagnosticEvidenceReferences));
            }
            var stages = stagesList.ToArray();
            var completion = await db.WholeTrayCompletions.AsNoTracking()
                .SingleOrDefaultAsync(x => x.RunId == runId, ct);
            ComponentSourceMatrixApi? ready = null;
            ComponentSourceMatrixApi? final = null;
            if (completion is not null)
            {
                ready = await ReadMatrixAsync(db, completion.SourceMatrixId, ct);
                var finalEntity = await db.ComponentEvidenceMatrices.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.RunId == runId &&
                        x.Milestone == EvidenceMilestone.FinalUnloadCompletion.ToString(), ct);
                if (finalEntity is not null) final = ToApi(finalEntity);
            }
            var motionWrites = await db.Writes.AsNoTracking().Where(x => x.RunId == runId && x.Kind == "ActionFact").OrderBy(x => x.Revision).ToListAsync(ct);
            var motionEvidence = motionWrites.Select(x => new MotionEvidenceApi(x.WriteId, x.CommittedUtc, MotionFacts(x.PayloadJson)))
                .Where(x => x.Facts.Target is not null).ToList();
            foreach (var binding in await RecipeApplicationHistoryReader.ReadAsync(db, runId, ct))
                motionEvidence.Add(new(binding.WriteId, binding.CommittedAtUtc,
                    MotionFacts("{}") with { Kind = "RecipePlanBound", RecordNature = binding.RecordNature,
                        RecipeApplication = binding.View }));
            var result = new Station01RunEvidenceApi(runId, completion?.TrayId,
                completion?.PersistedRevision ?? run.Revision, stages,
                completion?.WholeTrayCompletionId, ready, final,
                stages.Any(x => x.EventType == StageEventType.FinalUnloadCompleted.ToString())
                    ? "FinalUnloadCompletion" : completion is null ? "NotCompleted" : "AwaitingFinalUnloadCompletion", motionEvidence,
                    DiagnosticEvidenceReferences: diagnostics.References, RawAvailability: diagnostics.RawAvailability);
            return Versioned(http, result,
                $"evidence-{runId:N}-{Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(result, new JsonSerializerOptions(JsonSerializerDefaults.Web))))}");
        }).RequireAuthorization(Station01Authorization.Read);
        group.MapGet("/status", async (Station01Coordinator coordinator, Station01RuntimeOptions options,
            IPlcStatePort plc, ICapturePort cameraPort, IAlgorithmPort algorithmPort,
            MediaStore mediaStore,
            IServiceProvider services, ITraceQuery traces, IStageEventStore stageEvents, HttpContext http, CancellationToken ct) =>
        {
            var observed = plc.Observe();
            var admissionClosed = coordinator.AdmissionClosed;
            var currentRun = coordinator.CurrentRun;
            if (currentRun is not null) currentRun = await RuntimeObservationProjection.ReadAsync(currentRun, traces, stageEvents, ct);
            var activeRunCount = coordinator.ActiveRunCount;
            var plcProjection = DeviceSemanticProjection.Observation(observed);
            var runProjection = DeviceSemanticProjection.Run(currentRun);
            var fileCamera = cameraPort is FileBackedCapture;
            var camera = new { state = fileCamera ? "Ready" :
                    options.Mode == "FullSimulation" ? "Simulated" : "NotIntegrated",
                source = fileCamera ? "Test/FixedImage" :
                    options.Mode == "FullSimulation" ? "Simulated" : "Unknown" };
            var store = StoreCompatibilityProbe.Inspect(options.TestRoot);
            var storage = new { state = store.Compatible ? "Ready" : store.Code,
                maintenance = mediaStore.ActiveJobs == 0 ? "Idle" : "Writing" };
            var maintenance = new { state = "Unknown", maintenance = "NotObserved" };
            var worker = services.GetService<WorkerProcessSupervisor>();
            var workerReady = algorithmPort is PythonWorkerAdapter && worker is { HasExited: false };
            var algorithm = new AlgorithmAvailability(workerReady ? "Ready" : "NotIntegrated",
                workerReady ? "Test/IndependentWorker" :
                    options.Mode == "FullSimulation" ? "Simulated" : "Unknown",
                workerReady ? $"session:{worker!.SessionId:D}" : "算法worker不可用");
            var capabilities = workerReady && fileCamera
                ? new[] { "Station01PublicPreparation", "FixedXY", "FSingleCapture",
                    "Detection", "WholeTrayWorkflow", "FinalUnloadCompletion" }
                : new[] { "Station01PublicPreparation", "FixedXY", "FSingleCapture" };
            var projectionFacts = new
            {
                host = admissionClosed ? "Stopping" : "Ready",
                plc = plcProjection, camera, storage, maintenance, algorithm,
                currentRun = runProjection, capabilities, mode = options.Mode
            };
            var digest = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(projectionFacts,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            var revision = (long)(BinaryPrimitives.ReadUInt64BigEndian(digest.AsSpan(0, 8)) & long.MaxValue);
            var etag = $"\"status-{Convert.ToHexString(digest.AsSpan(0, 12))}\"";
            http.Response.Headers.ETag = etag;
            if (http.Request.Headers.IfNoneMatch.Any(x => string.Equals(x, etag, StringComparison.Ordinal)))
                return Results.StatusCode(StatusCodes.Status304NotModified);
            var result = new StatusSnapshot(
                "s01-status/2.0", revision, etag,
                admissionClosed ? "Stopping" : "Ready",
                plcProjection,
                camera, storage, maintenance, algorithm, runProjection, capabilities,
                DateTimeOffset.UtcNow,
                options.Mode, currentRun?.State.ToString() ?? "Idle",
                currentRun?.RecipeState ?? "Unmatched",
                currentRun?.QualityState ?? "NotEvaluated",
                activeRunCount);
            return Results.Ok(result);
        })
            .RequireAuthorization(Station01Authorization.Read);
        return group;
    }

    private static IResult Versioned(HttpContext http, object value, string opaque)
    {
        var tag = '"' + opaque + '"';
        http.Response.Headers.ETag = tag;
        return http.Request.Headers.IfNoneMatch.Any(x => string.Equals(x, tag, StringComparison.Ordinal))
            ? Results.StatusCode(StatusCodes.Status304NotModified) : Results.Ok(value);
    }

    private static IReadOnlyList<RunResultProjection> ReadHierarchy(StageEvent fact)
    {
        using var document = JsonDocument.Parse(fact.PayloadJson);
        var root = document.RootElement;
        if (!root.TryGetProperty("kind", out var kind) || kind.GetString() != "DetectionUnitDecision" ||
            !root.TryGetProperty("UnitKind", out var unitKind) ||
            unitKind.GetString() is not ("looseGroup" or "assembledEntity")) return [];
        var assembly = unitKind.GetString() == "assembledEntity";
        var unitId = root.GetProperty("UnitId").GetString()!;
        var reference = $"stage-event://{fact.EventId:D}";
        var result = new List<RunResultProjection>
        {
            new(assembly ? "Assembly" : "Group", unitId, root.GetProperty("disposition").GetString()!,
                reference, fact.PlanRevision, fact.EventId, fact.Source.ToString(), fact.Quality.ToString())
        };
        foreach (var part in root.GetProperty("parts").EnumerateArray())
        {
            var objectId = part.GetProperty("objectId").GetString()!;
            result.Add(new(assembly ? "Part" : "Member", objectId, part.GetProperty("disposition").GetString()!,
                reference, fact.PlanRevision, fact.EventId, fact.Source.ToString(), fact.Quality.ToString()) { ParentId = unitId });
            foreach (var face in part.GetProperty("faces").EnumerateArray())
            {
                var localFace = face.GetProperty("LocalFace").GetInt32();
                var stageId = face.TryGetProperty("StageId", out var stage) ? stage.GetString() : null;
                result.Add(new("Face", $"{objectId}:face{localFace}" + (stageId is null ? "" : ":" + stageId), face.GetProperty("Disposition").GetString()!,
                    face.GetProperty("Evidence").GetString()!, fact.PlanRevision, fact.EventId,
                    fact.Source.ToString(), fact.Quality.ToString()) { ParentId = objectId, LocalFace = localFace, StageId = stageId });
            }
        }
        return result;
    }

    private static IReadOnlyList<RunResultProjection> ReadCommittedResults(StageEvent fact)
    {
        using var payload = JsonDocument.Parse(fact.PayloadJson);
        var root = payload.RootElement;
        if (!root.TryGetProperty("ResultReference", out var resultRef) &&
            !root.TryGetProperty("resultReference", out resultRef)) return [];
        if (!root.TryGetProperty("objects", out var objects) &&
            !root.TryGetProperty("Objects", out objects)) return [];
        if (resultRef.ValueKind != JsonValueKind.String || objects.ValueKind != JsonValueKind.Array)
            return [];
        var reference = resultRef.GetString();
        if (string.IsNullOrWhiteSpace(reference)) return [];
        var results = new List<RunResultProjection>();
        foreach (var item in objects.EnumerateArray())
        {
            var id = item.TryGetProperty("ObjectId", out var value) ? value.GetString() :
                item.TryGetProperty("objectId", out value) ? value.GetString() : null;
            var disposition = item.TryGetProperty("Disposition", out value) ? value.GetString() :
                item.TryGetProperty("disposition", out value) ? value.GetString() : null;
            if (string.IsNullOrWhiteSpace(id) || disposition is not ("OK" or "NG" or "Pending"))
                continue;
            results.Add(new("Single", id, disposition, reference, fact.PlanRevision,
                fact.EventId, fact.Source.ToString(), fact.Quality.ToString()));
        }
        return results;
    }

    private static async Task<ComponentSourceMatrixApi> ReadMatrixAsync(Station01DbContext db,
        Guid matrixId, CancellationToken cancellationToken) => ToApi(
            await db.ComponentEvidenceMatrices.AsNoTracking().SingleAsync(x =>
                x.MatrixId == matrixId, cancellationToken));

    private static ComponentSourceMatrixApi ToApi(ComponentEvidenceMatrixEntity entity)
    {
        var components = JsonSerializer.Deserialize<ComponentEvidence[]>(entity.ComponentsJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
        var matrix = ComponentEvidenceMatrix.Create(entity.MatrixId, entity.RunId, entity.TrayId,
            entity.PlanRevision, Enum.Parse<EvidenceMilestone>(entity.Milestone), components);
        return new(entity.MatrixId, entity.SchemaVersion, entity.RunId, entity.TrayId,
            entity.PlanRevision, entity.Milestone, entity.Scope, entity.MatrixDigest,
            matrix.Components, matrix.BlockedComponents.Select(x => x.ToString()).ToArray(),
            entity.CreatedUtc);
    }

}
