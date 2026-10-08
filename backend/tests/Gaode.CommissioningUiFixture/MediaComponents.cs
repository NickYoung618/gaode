using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Acquisition;
using Gaode.Application.Configuration;
using Gaode.Application.Ports;
using Gaode.Application.Station01;
using Gaode.Application.Workflow;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Media;

internal sealed partial class OfflineFixture
{
    // Component preparation and capture only; never a successful production Start or Final.
    private async Task<Guid[]> CreateMediaRunsAsync()
    {
        var publicConfig = App.Services.GetRequiredService<IPublicConfiguration>();
        var frozen = ConfigurationFreezer.Freeze(publicConfig.LoadPublic(options.PublicReference),
            publicConfig.LoadBudget(options.BudgetReference), null,
            App.Services.GetRequiredService<Gaode.Application.Capabilities.CapabilityRegistry>().Versions, inputs.Loaded);
        var ids = new List<Guid>();
        foreach (var cameras in new[] { new[] { "C", "D", "A", "B", "E", "ThreeD", "F", "C" }, new[] { "C" } })
        {
            var id = Guid.NewGuid(); var tray = Guid.NewGuid();
            var context = JsonSerializer.Serialize(new { schemaVersion = StartRunContext.RecipeSchemaVersion, trayId = tray,
                stationId = "OFFLINE-media-station", lineId = "OFFLINE-media-line", scenarioId = recipe.ScenarioId,
                occupiedSlots = new[] { "s1" }, purpose = "Commissioning", source = "OFFLINE:021-media-component/1;not-machine-flow" }, Json);
            var run = new RunExecution(id, Guid.NewGuid(), "OFFLINE-media-" + id.ToString("N"), "offline:operator", context,
                frozen, App.Services.GetRequiredService<ITraceWriter>(), TimeProvider.System, Guid.NewGuid(), "OFFLINE-system-clock");
            await run.SaveAsync(WriteKind.RunCreated, new RunCreatedPayload(run.CommandId, run.RequestId, run.SubjectId, context, "OFFLINE:component-public", "OFFLINE:component-budget", "OFFLINE:no-Test-simulation", frozen.SnapshotId, "", "", ""), cancellationToken: lifetime.Token);
            await run.SaveAsync(WriteKind.Audit, new { kind = "OfflineMediaComponentPrepared", source = "OFFLINE:021/1", authorizesMotion = false }, cancellationToken: lifetime.Token);
            var media = App.Services.GetRequiredService<MediaStore>();
            var camera = new OfflineImageCapture();
            var acquisition = new CameraAcquisitionService(camera, media);
            var sequence = 0;
            foreach (var binding in cameras)
            {
                sequence++;
                var role = binding == "ThreeD" ? CaptureRole.ThreeD : binding == "F" ? CaptureRole.F : binding == "E" ? CaptureRole.E : CaptureRole.Detection;
                var start = Stopwatch.GetTimestamp();
                var request = new CaptureRequest(new(id, Guid.NewGuid(), 1, run.SessionId, "OFFLINE-media-plan/1", "1", "RealDeviceCommissioning",
                    start, start + Stopwatch.Frequency * 5, run.ClockId), Guid.NewGuid(), role, "OFFLINE-component-point", "1", "OFFLINE-component-scope", "1", binding, "OFFLINE-virtual-light", Guid.NewGuid(), 65536) { PublicSettings = new("OFFLINE-capture-settings", "1", 1000, 0) };
                await acquisition.CaptureAsync(request, new ComponentJournal(run, tray, sequence, App.Services.GetRequiredService<IStageEventStore>()), lifetime.Token);
            }
            ids.Add(id);
        }
        return ids.ToArray();
    }
    private sealed class OfflineImageCapture : ICapturePort
    {
        private int calls;
        public long ConnectionEpoch => 1;
        public int TriggerCount(CaptureRole role) => calls;
        public ComponentExecutionOrigin CameraOrigin => new(ComponentEvidenceSource.Test, "OFFLINE:021-PNG-capture/1", "DeclaredOfflineFixture");
        public ComponentExecutionOrigin LightOrigin => new(ComponentEvidenceSource.Simulated, "OFFLINE:021-light/1", "Virtual");
        public string MediaSource => "OFFLINE:021-PNG-capture/1";
        public ValueTask RequestCaptureAsync(CaptureRequest request, Action<CaptureEvent> callback, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); calls++;
            // Declared PNG test input. Returned only in response to an actual isolated port request.
            var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a2ioAAAAASUVORK5CYII=");
            var fact = new CorrelatedCaptureFact(request.Envelope.RunId, request.CaptureId, request.Envelope.OperationId, 1,
                AcquisitionContract.RequestedSettingsDigest(request), MediaSource, CameraOrigin, LightOrigin,
                CaptureApplicationState.Applied, null, false, ["OFFLINE:021-PNG-input/1;no-real-camera-claim"])
            { ActualPublicSettings = request.PublicSettings, ActualCameraSettings = new(request.PublicSettings!.ExposureUs, 0, 1, 1, 0, 0),
              CameraApplicationState = CaptureApplicationState.Applied, LightApplicationState = CaptureApplicationState.ConfiguredOnly };
            callback(new(request, CaptureEventKind.Accepted, 1));
            callback(new(request, CaptureEventKind.Ended, 1));
            callback(new(request, CaptureEventKind.MediaTaken, 1, bytes, "png") { Fact = fact });
            return ValueTask.CompletedTask;
        }
    }
    private sealed class ComponentJournal(RunExecution run, Guid tray, int sequence, IStageEventStore stages) : ICameraCaptureJournal
    {
        public async Task RecordIntentAsync(CaptureRequest r, CancellationToken ct)
        {
            var kind = r.Role switch { CaptureRole.ThreeD => "Capture3D", CaptureRole.F => "CaptureF", CaptureRole.E => "CaptureE", _ => "DetectionCapture" };
            await run.SaveAsync(WriteKind.CaptureIntent, new { kind, r.CaptureId, operationId = r.Envelope.OperationId, attempt = 1, targetOrScope = r.ScopeId, snapshotId = r.Envelope.SnapshotId, requestedCapture = r }, cancellationToken: ct);
        }
        public async Task CommitAsync(CaptureRequest r, MediaRef media, CorrelatedCaptureFact fact, CancellationToken ct)
        {
            await run.SaveAsync(WriteKind.Media, media, cancellationToken: ct);
            await run.SaveAsync(WriteKind.CaptureFact, new { r.CaptureId, media.MediaId, captureFact = fact, requestedCapture = r }, cancellationToken: ct);
            var json = JsonSerializer.Serialize(new { r.CaptureId, media.MediaId, stepSequence = sequence,
                camera = r.CameraBindingId, objectId = "OFFLINE-component-object", localFace = 1, heightRound = 1,
                source = "OFFLINE:actual-capture-and-media-commit;not-detection-result" }, Json);
            var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
            await stages.AppendAsync(new(Guid.NewGuid(), run.RunId, tray, "OFFLINE-media-station", "OFFLINE-media-line",
                WholeTrayWorkflowStage.Detection, r.Envelope.OperationId, 1, 1, StageEventType.Executing, DateTimeOffset.UtcNow,
                ResultSource.Test, ResultQuality.Unknown, null, digest, json, "OFFLINE-media-" + r.CaptureId, r.Envelope.SnapshotId), ct);
        }
        public async Task RecordFailureAsync(CaptureRequest r, string error, CancellationToken ct)
        { await run.SaveAsync(WriteKind.Audit, new { kind = "OfflineCaptureFailed", r.CaptureId, errorType = "CaptureFailure" }, cancellationToken: ct); }
    }
}
