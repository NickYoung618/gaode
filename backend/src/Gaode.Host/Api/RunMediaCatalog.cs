using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Infrastructure.Media;
using Gaode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Gaode.Host.Api;

public sealed record RunMediaItemApi(Guid MediaId, Guid CaptureId, string Role,
    int? StepSequence, string? BusinessCamera, long CommittedRevision,
    DateTimeOffset CommittedAtUtc, string Readiness, string Source,
    string? ObjectId = null, int? LocalFace = null, int? HeightRound = null)
{
    public JsonElement? RequestedCaptureSettings { get; init; }
    public CorrelatedCaptureFact? CaptureFact { get; init; }
    public string? Purpose { get; init; }
}

public sealed record RunMediaCatalogApi(Guid RunId, IReadOnlyList<RunMediaItemApi> Items);

public static class RunMediaCatalog
{
    public static async Task<RunMediaCatalogApi?> ReadAsync(Guid runId,
        DbContextOptions<Station01DbContext> options, MediaStore store,
        CancellationToken cancellationToken)
    {
        await using var db = new Station01DbContext(options);
        if (!await db.Runs.AsNoTracking().AnyAsync(x => x.RunId == runId, cancellationToken))
            return null;
        var media = await db.Media.AsNoTracking().Where(x => x.RunId == runId)
            .ToArrayAsync(cancellationToken);
        var writes = await db.Writes.AsNoTracking().Where(x => x.RunId == runId &&
            (x.Kind == "Media" || x.Kind == "CaptureIntent" || x.Kind == "CaptureFact"))
            .ToArrayAsync(cancellationToken);
        var events = await db.StageEvents.AsNoTracking().Where(x => x.RunId == runId &&
            x.Stage == "Detection" && x.EventType == "Executing")
            .ToArrayAsync(cancellationToken);
        var results = new List<RunMediaItemApi>();
        foreach (var item in media)
        {
            var mediaWrites = writes.Where(x => x.Kind == "Media" &&
                JsonGuid(x.PayloadJson, "mediaId") == item.MediaId &&
                JsonGuid(x.PayloadJson, "runId") == runId &&
                JsonGuid(x.PayloadJson, "captureId") == item.CaptureId).ToArray();
            if (mediaWrites.Length != 1) continue;
            var intent = writes.Where(x => x.Kind == "CaptureIntent" &&
                JsonGuid(x.PayloadJson, "captureId") == item.CaptureId)
                .ToArray();
            if (intent.Length != 1) continue;
            var kind = JsonString(intent[0].PayloadJson, "kind");
            var role = kind switch
            {
                "Capture3D" => "ThreeD",
                "RescanWholeTray" => "ThreeD",
                "CaptureF" => "F",
                "DetectionCapture" => "Detection",
                "CaptureE" => "E",
                _ => null
            };
            if (role is null) continue;
            string? camera = role == "Detection" ? null : role;
            int? step = null;
            string? objectId = null;
            int? localFace = null, heightRound = null;
            if (role is "Detection" or "E")
            {
                var matches = events.Where(x =>
                    x.PlanRevision == JsonString(intent[0].PayloadJson, "snapshotId") &&
                    JsonGuid(x.PayloadJson, "captureId") == item.CaptureId &&
                    JsonGuid(x.PayloadJson, "mediaId") == item.MediaId &&
                    JsonGuid(x.PayloadJson, "callId") is null &&
                    !string.IsNullOrWhiteSpace(JsonString(x.PayloadJson, "camera")))
                    .Select(x => (Camera: JsonString(x.PayloadJson, "camera"),
                        Step: JsonInt(x.PayloadJson, "stepSequence"),
                        ObjectId: JsonString(x.PayloadJson, "objectId"),
                        LocalFace: JsonInt(x.PayloadJson, "localFace"),
                        HeightRound: JsonInt(x.PayloadJson, "heightRound")))
                    .Distinct().ToArray();
                if (matches.Length == 1 && matches[0].Step is > 0)
                {
                    camera = matches[0].Camera;
                    step = matches[0].Step;
                    objectId = matches[0].ObjectId;
                    localFace = matches[0].LocalFace;
                    heightRound = matches[0].HeightRound;
                }
            }
            else if (role == "ThreeD" && kind == "RescanWholeTray")
            {
                var matches = events.Where(x =>
                    x.PlanRevision == JsonString(intent[0].PayloadJson, "snapshotId") &&
                    JsonString(x.PayloadJson, "kind") == "RescanMediaCommitted" &&
                    JsonGuid(x.PayloadJson, "captureId") == item.CaptureId &&
                    JsonGuid(x.PayloadJson, "mediaId") == item.MediaId)
                    .Select(x => (Face: JsonInt(x.PayloadJson, "localFace"),
                        Round: JsonInt(x.PayloadJson, "heightRound")))
                    .Distinct().ToArray();
                if (matches.Length == 1 && matches[0].Face is > 0 && matches[0].Round is > 0)
                {
                    localFace = matches[0].Face;
                    heightRound = matches[0].Round;
                }
            }
            var ready = item.State == "FileCompleted" &&
                store.TryGetReference(item.MediaId, out var reference) &&
                reference.RunId == runId && reference.CaptureId == item.CaptureId;
            var captureFacts = writes.Where(x => x.Kind == "CaptureFact" &&
                JsonGuid(x.PayloadJson, "captureId") == item.CaptureId && JsonGuid(x.PayloadJson, "mediaId") == item.MediaId)
                .Select(x => Property(x.PayloadJson, "captureFact"))
                .Where(x => x is { ValueKind: JsonValueKind.Object }).ToArray();
            var captureFact = captureFacts.Length == 1 ? captureFacts[0]!.Value.Deserialize<CorrelatedCaptureFact>(
                new JsonSerializerOptions(JsonSerializerDefaults.Web)) : null;
            if (captureFact is not null && (captureFact.RunId != runId || captureFact.CaptureId != item.CaptureId)) captureFact = null;
            results.Add(new(item.MediaId, item.CaptureId, role, step, camera,
                mediaWrites[0].Revision, mediaWrites[0].CommittedUtc,
                ready ? "Ready" : "NotReady", item.Source,
                objectId, localFace, heightRound)
            {
                CaptureFact = captureFact,
                Purpose = JsonString(mediaWrites[0].PayloadJson, "purpose"),
                RequestedCaptureSettings = events.Where(x =>
                    JsonGuid(x.PayloadJson, "captureId") == item.CaptureId &&
                    JsonGuid(x.PayloadJson, "mediaId") == item.MediaId)
                    .Select(x => Property(x.PayloadJson, "requestedCaptureSettings"))
                    .FirstOrDefault(x => x is { ValueKind: JsonValueKind.Object })
            });
        }
        return new(runId, results.OrderBy(x => x.CommittedRevision).ToArray());
    }

    private static JsonElement? Property(string json, string name)
    {
        using var document = JsonDocument.Parse(json);
        foreach (var property in document.RootElement.EnumerateObject())
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                return property.Value.Clone();
        return null;
    }

    private static string? JsonString(string json, string name) =>
        Property(json, name) is { ValueKind: JsonValueKind.String } value
            ? value.GetString() : null;

    private static Guid? JsonGuid(string json, string name) =>
        Guid.TryParse(JsonString(json, name), out var value) ? value : null;

    private static int? JsonInt(string json, string name) =>
        Property(json, name) is { ValueKind: JsonValueKind.Number } value &&
        value.TryGetInt32(out var result) ? result : null;
}
