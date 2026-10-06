using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Application.Station01;
using Gaode.Application.Motion;
using Gaode.Application.Timing;
using Gaode.Application.Workflow;
using Gaode.Application.Configuration;
using Gaode.Infrastructure.Persistence;

namespace Gaode.Host.Api;

public sealed record RecipePlanRequest(Guid RunId, string ScenarioId,
    IReadOnlyList<string> OccupiedSlots);

public static partial class RecipeEndpoints
{
    public static IEndpointRouteBuilder MapRecipeEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/recipes");
        MapAuthoring(group);

        group.MapPost("/plan", async (RecipePlanRequest request,
            [FromServices] IStageHandoffQuery handoffs,
            [FromServices] ITraceQuery traces,
            [FromServices] ILogger<IndependentRecipeApplication> logger, CancellationToken ct) =>
        {
            try
            {
                var frozen = await new CommittedRecipePlanReader(traces, handoffs)
                    .ReadAsync(request.RunId, request.ScenarioId, request.OccupiedSlots, ct);
                return frozen is null
                    ? Results.Conflict(new { error = "HandoffNotReady", message = "F相机交接尚未完成，不能选择配方" })
                    : Results.Ok(frozen.Plan);
            }
            catch (Exception e) when (e is InvalidOperationException or JsonException)
            {
                logger.LogWarning(e, "RecipeApplication.PlanRejected RunId={RunId} Reason={Reason}", request.RunId, e.Message);
                return Results.Conflict(new { error = "RecipeBindingRejected", message = e.Message });
            }
        })
            .RequireAuthorization(Station01Authorization.Read);

        group.MapPost("/bind", async (RecipePlanRequest request,
            [FromServices] IStageHandoffQuery handoffs,
            [FromServices] ITraceQuery traces,
            [FromServices] MotionCoordinator motion,
            [FromServices] Station01Coordinator coordinator,
            [FromServices] DeadlineScheduler time, [FromServices] IStageEventStore stageEvents,
            [FromServices] ILogger<IndependentRecipeApplication> logger, CancellationToken ct) =>
        {
            using var bindingCancellation = CancellationTokenSource.CreateLinkedTokenSource(ct,
                coordinator.Control(request.RunId)?.Cancellation ?? CancellationToken.None);
            try
            {
                var frozen = await new CommittedRecipePlanReader(traces, handoffs)
                    .ReadAsync(request.RunId, request.ScenarioId, request.OccupiedSlots, bindingCancellation.Token);
                if (frozen is null)
                    return Results.Conflict(new { error = "HandoffNotReady", message = "F相机交接尚未完成，不能绑定配方" });
                var plan = frozen.Plan;
                var receipt = await new IndependentRecipeApplication(traces, handoffs, motion, time, stageEvents)
                    .ExecuteAsync(request.RunId, plan, plan.NgCapacity, plan.PendingCapacity, bindingCancellation.Token);
                var bindingResult = RecipeApplicationProjection.Current(receipt);
                return Results.Ok(new { plan, deviceSchemaVersion = "device-semantics/1",
                    bindingResult, productContinuationAuthorized = false });
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested && bindingCancellation.IsCancellationRequested)
            {
                logger.LogWarning("RecipeApplication.ApiCancelled RunId={RunId} ProductContinuationAuthorized=false", request.RunId);
                return Results.Conflict(new { error = "RecipeBindingRejected", message = "RecipeApplicationCancelled" });
            }
            catch (Exception e) when (e is InvalidOperationException or TimeoutException or SaveGateException or ConfigurationException or JsonException)
            {
                var reason = e is ConfigurationException configurationError ? configurationError.Code :
                    e is JsonException ? "FrozenBindingConfigurationInvalid" : e.Message;
                logger.LogWarning(e, "RecipeApplication.ApiRejected RunId={RunId} Reason={Reason} ProductContinuationAuthorized={ProductContinuationAuthorized}",
                    request.RunId, reason, false);
                return Results.Conflict(new { error = "RecipeBindingRejected", message = reason });
            }
        }).RequireAuthorization(Station01Authorization.Start);
        return app;
    }

}
