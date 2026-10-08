using System.Security.Claims;
using Gaode.Application.Station01;
using Gaode.Application.Ports;
using Gaode.Application.Workflow;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Diagnostics;
using Gaode.Host.Composition;
using Microsoft.EntityFrameworkCore;

namespace Gaode.Host.Api;

public static class RunEndpoints
{
    public static IEndpointRouteBuilder MapStation01Api(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/station01");
        group.MapIdentityEndpoints();
        group.MapPost("/runs", async (StartPublicRequest request, StartPublicPreparation start,
            StructuredStageDiagnostics diagnostics, ClaimsPrincipal user, HttpContext http, IServiceProvider services,
            CancellationToken ct) =>
        {
            try
            {
                if (request.CommissioningRestartFrom is { } from)
                    await (services.GetService<CommissioningRecoveryService>() ??
                        throw new InvalidOperationException("CommissioningRecoveryUnavailable"))
                        .ValidateRestartAsync(user.FindFirstValue(ClaimTypes.NameIdentifier)!, from, ct);
                var receipt = start.Start(user.FindFirstValue(ClaimTypes.NameIdentifier)!, request);
                diagnostics.Record(request.RequestId, receipt.CommandId, receipt.RunId,
                    "HttpStart", "Accepted", "ReceiptOnly_NotCompletion", source: http.TraceIdentifier);
                return Results.Accepted(receipt.StatusUrl, receipt);
            }
            catch (InvalidOperationException e) when (e.Message is "RequestConflict" or "PhysicalRunHeld" or "RecoveryInProgress")
            {
                diagnostics.Record(request.RequestId, null, null, "Admission", e.Message,
                    "RejectedNoRunCreated", exception: e);
                return Station01ApiResults.Error(http, 409, e.Message,
                    "请求冲突、正在恢复或设备仍被当前运行占用", "Admission", details: new
                    { requestId = request.RequestId, runCreated = false, action = "查询当前运行并由授权人员核查" });
            }
            catch (InvalidOperationException e) when (e.Message is "RunCapacityUnavailable")
            {
                diagnostics.Record(request.RequestId, null, null, "Admission", e.Message,
                    "RejectedNoRunCreated", exception: e);
                return Station01ApiResults.Error(http, 429, e.Message, "流程容量不足", "Admission",
                    details: new { requestId = request.RequestId, runCreated = false, action = "稍后查询状态或联系授权人员" });
            }
            catch (InvalidOperationException e) when (e.Message is "HostStopping")
            {
                diagnostics.Record(request.RequestId, null, null, "Admission", e.Message,
                    "RejectedNoRunCreated", exception: e);
                return Station01ApiResults.Error(http, 503, e.Message,
                    "Host正在有界关闭，已拒绝新动作准入", "Admission",
                    details: new { requestId = request.RequestId, runCreated = false, action = "查询Host状态" });
            }
            catch (InvalidOperationException e) when (
                e.Message.StartsWith("ExpectedRecipeRestricted:", StringComparison.Ordinal))
            {
                diagnostics.Record(request.RequestId, null, null, "Admission", "RecipeRestricted",
                    "RejectedNoRunCreated", exception: e);
                return Station01ApiResults.Error(http, 409, "RecipeRestricted",
                    "所选配方缺少执行所需配置，未创建运行", "Recipe",
                    details: new { requestId = request.RequestId, runCreated = false,
                        restriction = e.Message["ExpectedRecipeRestricted:".Length..] });
            }
            catch (ArgumentException e) when (e.Message.StartsWith("ExpectedRecipeRef", StringComparison.Ordinal))
            {
                diagnostics.Record(request.RequestId, null, null, "Admission", "ExpectedRecipeRefInvalid",
                    "RejectedNoRunCreated", exception: e);
                return Station01ApiResults.Error(http, 400, "ExpectedRecipeRefInvalid",
                    "所选配方版本或目录摘要无效，未创建运行", "Recipe",
                    details: new { requestId = request.RequestId, runCreated = false });
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException)
            {
                diagnostics.Record(request.RequestId, null, null, "Admission", "StartRequestInvalid",
                    "RejectedNoRunCreated", exception: e);
                return Station01ApiResults.Error(http, 400, "StartRequestInvalid",
                    "启动请求配置或身份无效，未下发设备动作", "Admission",
                    details: new { requestId = request.RequestId, runCreated = false,
                        disposition = "RejectedBeforeRunAdmission",
                        action = "核查请求与配置后由授权人员处理" });
            }
        }).RequireAuthorization(Station01Authorization.Start);
        group.MapControlEndpoints();
        group.MapPost("/runs/{runId:guid}/tray-anomaly-decision", async (Guid runId,
            TrayAnomalyChoiceApiRequest request, TrayAnomalyDecisionService decisions, ClaimsPrincipal user,
            HttpContext http, CancellationToken token) => {
                try { return Results.Ok(await decisions.ChooseAsync(runId, request.DecisionId, request.Choice,
                    user.FindFirstValue(ClaimTypes.NameIdentifier)!, token)); }
                catch (Exception error) when (error is InvalidOperationException or ArgumentException)
                { return Station01ApiResults.Error(http, 409, error.Message, "本次异常选择条件不满足", "State"); }
            }).RequireAuthorization(Station01Authorization.Start);
        group.MapPost("/runs/{runId:guid}/manual-removal-confirmations", async (
            Guid runId, ManualTrayRemovalApiRequest request, Station01Coordinator coordinator,
            IWholeTrayCompletionStore completions, IStageEventStore events,
            WholeTrayWorkflowOrchestrator workflow, Station01RuntimeOptions options,
            StructuredStageDiagnostics diagnostics,
            ClaimsPrincipal user, HttpContext http,
            CancellationToken ct) =>
        {
            var snapshot = coordinator.Query(runId);
            if (snapshot is null)
                return Station01ApiResults.NotFound(http, "RunNotFound", "运行不存在");
            if (string.IsNullOrWhiteSpace(request.RequestId) || string.IsNullOrWhiteSpace(request.Reason))
                return Station01ApiResults.Error(http, StatusCodes.Status400BadRequest,
                    "ManualConfirmationInvalid", "请求号和确认原因不能为空");
            var whole = await completions.GetByRunAsync(runId, ct);
            var unlockEvents = whole is null ? [] : await events.ReadAsync(whole.Reference.RunId,
                whole.Reference.TrayId, WholeTrayWorkflowStage.ManualRemovalAdmission, ct);
            var manualEvents = whole is null ? [] : await events.ReadAsync(whole.Reference.RunId,
                whole.Reference.TrayId, WholeTrayWorkflowStage.ManualTrayRemovalConfirmation, ct);
            var replayKey = $"manual:{runId:N}:{request.RequestId}:final";
            if (manualEvents.LastOrDefault(x => x.EventType == StageEventType.FinalUnloadCompleted &&
                    x.IdempotencyKey == replayKey) is { } replay)
            {
                if (whole is null || !await completions.ReconcileFinalAsync(runId, whole.Reference.TrayId, ct))
                    return Station01ApiResults.Error(http, 409, "FinalProofUnavailable", "最终保存证明不完整，继续保持占用", "Storage");
                return Results.Accepted($"/api/v1/station01/runs/{runId:D}", new
                {
                    finalEventId = replay.EventId,
                    state = "FinalUnloadCompletion",
                    replay = true,
                    statusUrl = $"/api/v1/station01/runs/{runId:D}"
                });
            }
            if (snapshot.ObservedRevision != request.ExpectedRevision)
                return Station01ApiResults.Error(http, StatusCodes.Status409Conflict,
                    "RevisionConflict", "运行版本已变化", currentRevision: snapshot.ObservedRevision);
            if (snapshot.State != RunState.AwaitingManualRemoval || snapshot.CancelRequested)
                return Station01ApiResults.Error(http, StatusCodes.Status409Conflict,
                    "ManualRemovalAllowanceRequired", "尚未取得人工取盘允许，不允许确认取盘", "State");
            var unlock = unlockEvents.LastOrDefault(x =>
                x.EventType == StageEventType.ManualRemovalAllowed);
            if (whole is null || unlock is null)
                return Station01ApiResults.Error(http, StatusCodes.Status409Conflict,
                    "ManualRemovalAllowanceRequired", "持久化人工取盘允许不存在", "State");
            var actor = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var now = DateTimeOffset.UtcNow;
            var simulated = options.Mode is "VirtualPlcIntegration" or "FullSimulation";
            var manualEvidence = new ComponentEvidence(ComponentKind.ManualActor,
                ComponentEvidenceState.Verified,
                simulated ? ComponentEvidenceSource.Test : ComponentEvidenceSource.AuthenticatedHuman,
                simulated ? "ControlledTestClient" : "Authenticated",
                simulated ? "host-test-auth/1" : "host-auth/1",
                simulated ? [$"test-client://{actor}", $"request://{request.RequestId}"] :
                    [$"actor://{actor}"], now,
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes($"{actor}:{request.RequestId}:{request.Reason}"))));
            try
            {
                var result = await workflow.ConfirmManualRemovalAsync(
                    new ManualTrayRemovalConfirmationRequest(Guid.NewGuid(), whole.Reference,
                        unlock.EventId, actor, now, request.Reason,
                        $"manual:{runId:N}:{request.RequestId}", manualEvidence), ct);
                await coordinator.SetAsync(runId, s => s.Next(RunState.Completed) with
                {
                    FinalOutcome = TerminalOutcome.Completed,
                    WholeTaskState = "FinalUnloadCompletion",
                    TrayEndReason = whole.Reference.EndReason.ToString(),
                    InspectionCompleted = whole.Reference.InspectionCompleted,
                    PersistedRevision = Math.Max(s.PersistedRevision, s.PersistedRevision + 1),
                    Events = [..s.Events, "ManualTrayRemovalConfirmed", "FinalUnloadCompleted"]
                });
                return Results.Accepted($"/api/v1/station01/runs/{runId:D}", new
                {
                    result.FinalCompletion!.FinalCompletionId,
                    state = "FinalUnloadCompletion",
                    statusUrl = $"/api/v1/station01/runs/{runId:D}"
                });
            }
            catch (DbUpdateException exception)
            {
                diagnostics.Record(request.RequestId, null, runId, "ManualRemovalConfirmation",
                    "PersistenceFailed", "QueryBeforeRetry_NoCompletionClaim", exception: exception);
                return Station01ApiResults.Error(http, StatusCodes.Status503ServiceUnavailable,
                    "ManualConfirmationPersistenceFailed", "人工确认未提交，请通过GET核对后重试",
                    "Storage", true);
            }
            catch (InvalidOperationException exception)
            {
                diagnostics.Record(request.RequestId, null, runId, "ManualRemovalConfirmation",
                    "ConditionRejected", "NoNewCompletionClaim", exception: exception);
                return Station01ApiResults.Error(http, StatusCodes.Status409Conflict,
                    exception.Message, "人工确认条件不满足", "State");
            }
        }).RequireAuthorization(Station01Authorization.Start);
        group.MapConfigurationEndpoints();
        group.MapPost("/reset", async (IPlcResetPort reset, IServiceProvider services, ClaimsPrincipal user, CancellationToken ct) =>
        {
            try
            {
                if (services.GetService<CommissioningRecoveryService>() is { } commissioningRecovery)
                    return Results.Ok(await commissioningRecovery.ResetAsync(user.FindFirstValue(ClaimTypes.NameIdentifier)!, ct));
                if (services.GetService<FixedMoveRecoveryInteraction>()?.HasFaults == true)
                    throw new InvalidOperationException("FaultRequiresScopedReset");
                await reset.ResetAsync(ct);
                return Results.Ok(new { reset = true, manualStartRequired = true });
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return Results.Conflict(new { error = "PlcResetFailed", message = "RecoveryResetDeadlineExceeded" });
            }
            catch (Exception e) when (e is InvalidOperationException or IOException or TimeoutException)
            {
                return Results.Conflict(new { error = "PlcResetFailed", message = e.Message });
            }
        }).RequireAuthorization(Station01Authorization.Start);
        group.MapQueryEndpoints();
        group.MapCommunicationDiagnostics();
        group.MapMediaEndpoints();
        app.MapHub<Station01Hub>("/hubs/station01").RequireAuthorization(Station01Authorization.Read);
        return app;
    }
}
