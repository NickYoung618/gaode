using System.Security.Claims;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Application.Station01;
using Gaode.Application.Workflow;
using Gaode.Domain.Station01;

namespace Gaode.Host.Api;

public sealed record ControlRequest(string RequestId, long ExpectedRevision, string? Reason);
public sealed record RecoveryCheckRequest(string RequestId, long ExpectedRevision, bool SameTray,
    bool LoadingUnchanged, bool SnapshotStillApplicable, IReadOnlyList<string>? EvidenceRefs, string? Reason);
public sealed record ContinueRequest(string RequestId, long ExpectedRevision, Guid CheckId);
public sealed record ControlledRecoveryDecisionRequest(string RequestId, long ExpectedRevision,
    Guid TrayId, string OriginalTaskId, Guid? OriginalOperationId, string OriginalStage,
    ControlledRecoveryAction Decision, string Reason, IReadOnlyList<string> EvidenceReferences);

public static class ControlEndpoints
{
    public static RouteGroupBuilder MapControlEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/runs/{runId:guid}/failed-command-recovery", async (Guid runId, IServiceProvider services,
            ITraceQuery traces, CancellationToken ct) =>
        {
            var writes = await traces.GetWritesAsync(runId, ct);
            var facts = writes.Where(x => x.State == CommitState.Committed && IsRecoveryFact(x.PayloadJson)).ToArray();
            return Results.Ok(new { pending = services.GetService<FixedMoveRecoveryInteraction>()?.Query(runId), facts });
        })
            .RequireAuthorization(Station01Authorization.Read);
        group.MapPost("/runs/{runId:guid}/recovery-reset", async (Guid runId, ControlRequest request,
            IServiceProvider services, Station01Coordinator coordinator, ClaimsPrincipal user, HttpContext http, CancellationToken ct) =>
            await Execute(http, async () =>
            {
                var snapshot = coordinator.Query(runId) ?? throw new InvalidOperationException("RunNotFound");
                if (snapshot.ObservedRevision != request.ExpectedRevision) throw new InvalidOperationException("RevisionConflict");
                var service = services.GetService<FixedMoveRecoveryInteraction>() ?? throw new InvalidOperationException("NoLiveFailedCommandRecovery");
                return await service.ResetAsync(runId, Subject(user), request.RequestId, request.Reason ?? "", ct);
            })).RequireAuthorization(Station01Authorization.RecoveryCheck);
        group.MapPost("/runs/{runId:guid}/pause", async (Guid runId, ControlRequest request,
            Station01ControlCommandService service, ClaimsPrincipal user, HttpContext http, CancellationToken ct) =>
            await Execute(http, () => service.RequestAsync(Subject(user), runId, request.RequestId,
                request.ExpectedRevision, "Pause", request.Reason, ct)))
            .RequireAuthorization(Station01Authorization.Pause);
        group.MapPost("/runs/{runId:guid}/cancel", async (Guid runId, ControlRequest request,
            Station01ControlCommandService service, ClaimsPrincipal user, HttpContext http, CancellationToken ct) =>
            await Execute(http, () => service.RequestAsync(Subject(user), runId, request.RequestId,
                request.ExpectedRevision, "Cancel", request.Reason, ct)))
            .RequireAuthorization(Station01Authorization.Cancel);
        group.MapPost("/runs/{runId:guid}/recovery-checks", async (Guid runId, RecoveryCheckRequest request,
            Station01ControlCommandService service, ClaimsPrincipal user, HttpContext http, CancellationToken ct) =>
            await Execute(http, () => service.CheckAsync(Subject(user), runId, request.RequestId,
                request.ExpectedRevision, request.SameTray, request.LoadingUnchanged,
                request.SnapshotStillApplicable, ct, request.Reason, request.EvidenceRefs)))
            .RequireAuthorization(Station01Authorization.RecoveryCheck);
        group.MapGet("/runs/{runId:guid}/recovery-checks/{checkId:guid}",
            (Guid runId, Guid checkId, Station01ControlCommandService service, HttpContext http) =>
            service.GetCheck(runId, checkId) is { } check
                ? Results.Ok(check)
                : Station01ApiResults.NotFound(http, "RecoveryCheckNotFound", "恢复核对不存在"))
            .RequireAuthorization(Station01Authorization.Read);
        group.MapPost("/runs/{runId:guid}/continue", async (Guid runId, ContinueRequest request,
            Station01ControlCommandService service, ClaimsPrincipal user, HttpContext http, CancellationToken ct) =>
            await Execute(http, () => service.ContinueAsync(Subject(user), runId, request.RequestId,
                request.ExpectedRevision, request.CheckId, ct)))
            .RequireAuthorization(Station01Authorization.Continue);
        group.MapPost("/runs/{runId:guid}/controlled-recovery-decisions", async (
            Guid runId, ControlledRecoveryDecisionRequest request,
            ControlledRecoveryService service, Station01Coordinator coordinator,
            ClaimsPrincipal user, HttpContext http, CancellationToken ct) =>
        {
            var snapshot = coordinator.Query(runId);
            if (snapshot is null)
                return Station01ApiResults.NotFound(http, "RunNotFound", "运行不存在");
            if (snapshot.ObservedRevision != request.ExpectedRevision)
                return Station01ApiResults.Error(http, StatusCodes.Status409Conflict,
                    "RecoveryRevisionConflict", "运行版本已变化", currentRevision: snapshot.ObservedRevision);
            try
            {
                var actorId = Subject(user);
                var actorRole = user.FindFirstValue(ClaimTypes.Role) ?? "Unknown";
                var decision = await service.RecordDecisionAsync(request.RequestId,
                    request.ExpectedRevision, request.OriginalTaskId, runId, request.TrayId,
                    request.OriginalOperationId, request.OriginalStage, request.Decision,
                    actorId, actorRole, request.Reason, request.EvidenceReferences, ct);
                return Results.Accepted($"/api/v1/station01/runs/{runId:D}", new
                {
                    decision.DecisionId,
                    decision = decision.Decision.ToString(),
                    decision.ActorId,
                    decision.DecidedAt,
                    createsAlgorithmFact = false,
                    createsPlcFact = false,
                    createsCompletionFact = false
                });
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException)
            {
                return Station01ErrorMapping.Map(http, error);
            }
        }).RequireAuthorization(Station01Authorization.RecoveryCheck);
        return group;
    }

    private static string Subject(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();

    private static bool IsRecoveryFact(string json)
    {
        using var value = JsonDocument.Parse(json);
        return value.RootElement.TryGetProperty("kind", out var kind) &&
            kind.ValueKind == JsonValueKind.String && (kind.GetString()!.StartsWith("Recovery", StringComparison.Ordinal) ||
            kind.GetString() == "FailedMoveRecoveryRequired");
    }

    private static async Task<IResult> Execute<T>(HttpContext http, Func<Task<T>> action)
    {
        try { return Results.Accepted(value: await action()); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or UnauthorizedAccessException)
        { return Station01ErrorMapping.Map(http, error); }
    }
}
