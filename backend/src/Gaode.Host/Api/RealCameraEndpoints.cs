using System.Diagnostics;
using Gaode.Application.Acquisition;
using Gaode.Application.Ports;
using Gaode.Infrastructure.Devices.Cameras;
using Gaode.Infrastructure.Media;
using Gaode.Infrastructure.Persistence;

namespace Gaode.Host.Api;

public static class RealCameraEndpoints
{
    public static void MapRealCameraEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/cameras", (PersistentCameraGateway gateway) =>
            Results.Ok(new { cameras = gateway.Status, productionReady = false,
                notIntegrated = new[] { "Algorithm", "ExternalLight", "ProductionWorkflow" } }))
            .RequireAuthorization(Station01Authorization.Read);
        app.MapPost("/api/v1/cameras/{role}/captures", async (string role, CameraAcquisitionService service,
            PersistentCameraGateway gateway, CameraCaptureJournal journal, HttpContext http, CancellationToken ct) =>
        {
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
            budget.CancelAfter(TimeSpan.FromSeconds(60));
            try
            {
                var status = gateway.Status.SingleOrDefault(x => x.Role == role);
                if (status is null) return Results.NotFound(new { error = "CameraNotConfigured" });
                if (status.State != "Ready") return Results.Conflict(new { error = "CameraNotReady", status });
                var runId = Guid.NewGuid(); var captureId = Guid.NewGuid(); var tick = Stopwatch.GetTimestamp();
                var envelope = new PortEnvelope(runId, Guid.NewGuid(), 1, Guid.NewGuid(), "real-camera-site",
                    "1", "Production", tick, tick + 60 * Stopwatch.Frequency, "host-stopwatch");
                var request = new CaptureRequest(envelope, captureId, role == "3D" ? CaptureRole.ThreeD :
                    role == "F" ? CaptureRole.F : role == "E" ? CaptureRole.E : CaptureRole.Detection,
                    "ManualCapture", "1", null, null, role, null, Guid.NewGuid(), status.MaxBytes);
                var media = await service.CaptureAsync(request, journal, budget.Token);
                return Results.Ok(new { media, metadata = await journal.GetMetadataAsync(media.MediaId, budget.Token) });
            }
            catch (OperationCanceledException) { return Results.Json(new { error = "CaptureTimeoutOrCancelledUnknown", automaticReplay = false }, statusCode: 504); }
            catch (InvalidOperationException e) { return Results.Conflict(new { error = e.Message, automaticReplay = false }); }
            catch (Exception e)
            {
                http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("CameraCapture").LogError(e, "Capture/save failed role={Role}", role);
                return Results.Json(new { error = "CaptureOrSaveFailedOrUnknown", detail = e.Message, automaticReplay = false }, statusCode: 500);
            }
        }).RequireAuthorization(Station01Authorization.Start);
        app.MapPost("/api/v1/cameras/{role}/recover", async (string role, PersistentCameraGateway gateway, CancellationToken ct) =>
        {
            await gateway.RecoverAsync(role, ct);
            return Results.Ok(gateway.Status.Single(x => x.Role == role));
        }).RequireAuthorization(Station01Authorization.RecoveryCheck);
        app.MapGet("/api/v1/camera-media", async (CameraCaptureJournal journal, CancellationToken ct) =>
            Results.Ok(await journal.ListCommittedAsync(ct))).RequireAuthorization(Station01Authorization.MediaRead);
        app.MapGet("/api/v1/camera-media/{id:guid}", async (Guid id, MediaStore store, CameraCaptureJournal journal, CancellationToken ct) =>
            store.TryGetReference(id, out var media) ? Results.Ok(new { media, metadata = await journal.GetMetadataAsync(id, ct) }) : Results.NotFound())
            .RequireAuthorization(Station01Authorization.MediaRead);
        app.MapGet("/api/v1/camera-media/{id:guid}/content", async (Guid id, MediaStore store, CancellationToken ct) =>
        {
            if (!store.TryGetReference(id, out var media) || !store.IsReady(id)) return Results.NotFound();
            var stream = await store.OpenReadAsync(id, ct);
            return Results.Stream(stream, media.ContentType, enableRangeProcessing: false);
        }).RequireAuthorization(Station01Authorization.MediaRead);
    }
}
