using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Host.Api;
using Gaode.Infrastructure.Media;
using Gaode.Infrastructure.Persistence;
using Gaode.Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Gaode.Integration.Tests.Api;

public sealed class RunMediaCatalogTests
{
    [Fact]
    public async Task MultiFaceMediaKeepsCommittedFaceRoundAndRescanIdentity()
    {
        await using var fixture = await Station01HostFixture.CreateAsync();
        var options = fixture.Host.Services.GetRequiredService<DbContextOptions<Station01DbContext>>();
        var store = fixture.Host.Services.GetRequiredService<MediaStore>();
        var runId = Guid.NewGuid();
        await using (var db = new Station01DbContext(options))
        {
            db.Runs.Add(NewRun(runId, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }
        byte[] bytes = [137, 80, 78, 71, 13, 10, 26, 10, 3, 1, 2];
        var first = await SaveAndCommitAsync(store, options, runId, "Detection",
            "DetectionCapture", 3, bytes, "q03-plan", "A", 2, 1, 1, "q03-p01");
        var rescan = await SaveAndCommitAsync(store, options, runId, "3D",
            "RescanWholeTray", 5, bytes, "q03-plan", localFace: 2, heightRound: 2);
        var second = await SaveAndCommitAsync(store, options, runId, "Detection",
            "DetectionCapture", 7, bytes, "q03-plan", "A", 7, 2, 2, "q03-p01");
        // The real workflow also commits an algorithm fact for the same media.
        // It must not make the capture's object/face/round identity ambiguous.
        await using (var db = new Station01DbContext(options))
        {
            var now = DateTimeOffset.UtcNow;
            db.StageEvents.Add(new StageEventEntity
            {
                EventId = Guid.NewGuid(), RunId = runId, TrayId = Guid.NewGuid(),
                StationId = "station-01", LineId = "line-01", Stage = "Detection",
                OperationId = Guid.NewGuid(), Attempt = 1, PlanRevision = "q03-plan",
                EventType = "Executing", OccurredUtc = now, PersistedUtc = now,
                Source = "Test/Simulated", Quality = "Derived", PayloadDigest = "test",
                PayloadJson = JsonSerializer.Serialize(new
                {
                    kind = "DetectionImageCommitted", callId = Guid.NewGuid(),
                    first.CaptureId, first.MediaId, camera = "A", stepSequence = 2,
                    objectId = "q03-p01", localFace = 1
                }),
                IdempotencyKey = Guid.NewGuid().ToString("N"), Sequence = 10,
                RetainUntilUtc = now.AddDays(1)
            });
            await db.SaveChangesAsync();
        }
        var catalog = (await fixture.Client.GetFromJsonAsync<RunMediaCatalogApi>(
            $"/api/v1/station01/runs/{runId:D}/media"))!;
        Assert.Equal(3, catalog.Items.Count);
        Assert.Equal((first.MediaId, "q03-p01", 1, 1),
            (catalog.Items[0].MediaId, catalog.Items[0].ObjectId,
                catalog.Items[0].LocalFace, catalog.Items[0].HeightRound));
        Assert.Equal((rescan.MediaId, "ThreeD", 2, 2),
            (catalog.Items[1].MediaId, catalog.Items[1].Role,
                catalog.Items[1].LocalFace, catalog.Items[1].HeightRound));
        Assert.Equal((second.MediaId, "q03-p01", 2, 2),
            (catalog.Items[2].MediaId, catalog.Items[2].ObjectId,
                catalog.Items[2].LocalFace, catalog.Items[2].HeightRound));
        Assert.All(catalog.Items, item => Assert.Equal("Ready", item.Readiness));
    }

    [Fact]
    public async Task CommittedMediaListKeepsRunAndCaptureIdentityAndReadsOriginalBytes()
    {
        await using var fixture = await Station01HostFixture.CreateAsync();
        var options = fixture.Host.Services.GetRequiredService<DbContextOptions<Station01DbContext>>();
        var store = fixture.Host.Services.GetRequiredService<MediaStore>();
        var runId = Guid.NewGuid();
        var otherRunId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var bytes = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 1, 2, 3 };
        await using (var db = new Station01DbContext(options))
        {
            db.Runs.AddRange(NewRun(runId, now), NewRun(otherRunId, now));
            await db.SaveChangesAsync();
        }

        var threeD = await SaveAndCommitAsync(store, options, runId, "3D", "Capture3D", 1, bytes);
        var detection = await SaveAndCommitAsync(store, options, runId, "Detection", "DetectionCapture", 3, bytes,
            "plan-v1", "A", 2);
        var legacy = await SaveAndCommitAsync(store, options, runId, "Detection", "DetectionCapture", 5, bytes,
            "plan-v1");
        var latestA = await SaveAndCommitAsync(store, options, runId, "Detection", "DetectionCapture", 7, bytes,
            "plan-v1", "A", 4);
        var f = await SaveAndCommitAsync(store, options, runId, "F", "CaptureF", 9, bytes);
        var b = await SaveAndCommitAsync(store, options, runId, "Detection", "DetectionCapture", 11, bytes,
            "plan-v1", "B", 6);
        var other = await SaveAndCommitAsync(store, options, otherRunId, "F", "CaptureF", 1, bytes);

        var response = await fixture.Client.GetAsync($"/api/v1/station01/runs/{runId:D}/media");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var catalog = (await response.Content.ReadFromJsonAsync<RunMediaCatalogApi>())!;
        Assert.Equal(runId, catalog.RunId);
        Assert.Equal(6, catalog.Items.Count);
        Assert.DoesNotContain(other.MediaId, catalog.Items.Select(x => x.MediaId));
        Assert.Equal(("ThreeD", "ThreeD", (int?)null),
            (catalog.Items[0].Role, catalog.Items[0].BusinessCamera, catalog.Items[0].StepSequence));
        Assert.Equal(("Detection", "A", (int?)2),
            (catalog.Items[1].Role, catalog.Items[1].BusinessCamera, catalog.Items[1].StepSequence));
        Assert.Equal(("Detection", (string?)null, (int?)null),
            (catalog.Items[2].Role, catalog.Items[2].BusinessCamera, catalog.Items[2].StepSequence));
        Assert.Equal(latestA.MediaId, catalog.Items.Where(x => x.BusinessCamera == "A")
            .MaxBy(x => x.CommittedRevision)!.MediaId);
        Assert.Equal((f.MediaId, "F"),
            (catalog.Items[4].MediaId, catalog.Items[4].BusinessCamera));
        Assert.Equal((b.MediaId, "B"),
            (catalog.Items[5].MediaId, catalog.Items[5].BusinessCamera));
        Assert.All(catalog.Items, x =>
        {
            Assert.Equal("Ready", x.Readiness);
            Assert.Equal("Test/FixedImage", x.Source);
        });
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("relativeKey", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(fixture.StoreRoot, body, StringComparison.OrdinalIgnoreCase);
        var image = await fixture.Client.GetAsync($"/api/v1/station01/media/{detection.MediaId:D}");
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)),
            Convert.ToHexString(SHA256.HashData(await image.Content.ReadAsByteArrayAsync())));
        Assert.Equal(HttpStatusCode.NotFound,
            (await fixture.Client.GetAsync($"/api/v1/station01/runs/{Guid.NewGuid():D}/media")).StatusCode);
        Assert.Equal(threeD.CaptureId, catalog.Items[0].CaptureId);
        Assert.Equal(legacy.MediaId, catalog.Items[2].MediaId);
    }

    [Fact]
    public async Task AnonymousAndUncommittedMediaAreNotExposed()
    {
        await using var fixture = await Station01HostFixture.CreateAsync();
        var options = fixture.Host.Services.GetRequiredService<DbContextOptions<Station01DbContext>>();
        var runId = Guid.NewGuid();
        await using (var db = new Station01DbContext(options))
        {
            db.Runs.Add(NewRun(runId, DateTimeOffset.UtcNow));
            var captureId = Guid.NewGuid();
            var mediaId = Guid.NewGuid();
            db.Media.Add(new MediaEntity
            {
                MediaId = mediaId, RunId = runId, CaptureId = captureId,
                RelativeKey = "media/not-committed.png", ByteLength = 3, Format = "png",
                Source = "Test/FixedImage", State = "FileCompleted"
            });
            db.Media.Add(new MediaEntity
            {
                MediaId = Guid.NewGuid(), RunId = runId, CaptureId = Guid.NewGuid(),
                RelativeKey = "media/stray.png", ByteLength = 3, Format = "png",
                Source = "Test/FixedImage", State = "FileCompleted"
            });
            var intent = new OperationIntentPayload(Guid.NewGuid(), "CaptureF", 1, null,
                captureId, "scope", "plan-v1", Guid.NewGuid());
            db.Writes.AddRange(
                new WriteEntity { WriteId = Guid.NewGuid(), RunId = runId, Revision = 1,
                    Kind = "CaptureIntent", PayloadJson = JsonSerializer.Serialize(intent),
                    PayloadDigest = "test", CommittedUtc = DateTimeOffset.UtcNow },
                new WriteEntity { WriteId = Guid.NewGuid(), RunId = runId, Revision = 2,
                    Kind = "Media", PayloadJson = JsonSerializer.Serialize(new
                    { runId, mediaId, captureId }), PayloadDigest = "test",
                    CommittedUtc = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        using var anonymous = fixture.Host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/v1/station01/runs/{runId:D}/media")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/v1/station01/media/{Guid.NewGuid():D}")).StatusCode);
        var catalog = (await fixture.Client.GetFromJsonAsync<RunMediaCatalogApi>(
            $"/api/v1/station01/runs/{runId:D}/media"))!;
        var item = Assert.Single(catalog.Items);
        Assert.Equal("NotReady", item.Readiness);
        Assert.Equal("F", item.BusinessCamera);
    }

    private static RunEntity NewRun(Guid runId, DateTimeOffset now) => new()
    {
        RunId = runId, RequestId = Guid.NewGuid().ToString("N"), SubjectId = "media-test",
        ContextJson = "{}", CreatedUtc = now
    };

    private static async Task<MediaRef> SaveAndCommitAsync(MediaStore store,
        DbContextOptions<Station01DbContext> options, Guid runId, string saveRole,
        string intentKind, long mediaRevision, byte[] bytes, string plan = "plan-v1",
        string? camera = null, int? stepSequence = null,
        int? localFace = null, int? heightRound = null, string? objectId = null)
    {
        var captureId = Guid.NewGuid();
        using var reservation = store.ReserveCapture(captureId, saveRole, bytes.Length);
        var media = await store.SaveAsync(runId, captureId, saveRole, "1.0", plan,
            bytes, "png", "Test/FixedImage", CancellationToken.None);
        var now = DateTimeOffset.UtcNow;
        var intent = new OperationIntentPayload(Guid.NewGuid(), intentKind, 1, null,
            captureId, "scope", plan, Guid.NewGuid());
        await using var db = new Station01DbContext(options);
        db.Writes.AddRange(
            new WriteEntity { WriteId = Guid.NewGuid(), RunId = runId,
                Revision = mediaRevision - 1, Kind = "CaptureIntent",
                PayloadJson = JsonSerializer.Serialize(intent), PayloadDigest = "test", CommittedUtc = now },
            new WriteEntity { WriteId = Guid.NewGuid(), RunId = runId,
                Revision = mediaRevision, Kind = "Media",
                PayloadJson = JsonSerializer.Serialize(media), PayloadDigest = "test", CommittedUtc = now });
        db.Media.Add(new MediaEntity { MediaId = media.MediaId, RunId = runId,
            CaptureId = captureId, RelativeKey = media.RelativeKey, ByteLength = media.ByteLength,
            Format = media.Format, Source = media.Source, State = media.StorageState });
        if (camera is not null && stepSequence is not null || intentKind == "RescanWholeTray")
            db.StageEvents.Add(new StageEventEntity
            {
                EventId = Guid.NewGuid(), RunId = runId, TrayId = Guid.NewGuid(),
                StationId = "station-01", LineId = "line-01", Stage = "Detection",
                OperationId = intent.OperationId, Attempt = 1, PlanRevision = plan,
                EventType = "Executing", OccurredUtc = now, PersistedUtc = now,
                Source = "Test/Simulated", Quality = "Confirmed",
                PayloadDigest = "test", PayloadJson = JsonSerializer.Serialize(new
                { kind = intentKind == "RescanWholeTray" ? "RescanMediaCommitted" : "DetectionCapture",
                    captureId, media.MediaId, camera, stepSequence,
                    localFace, heightRound, objectId }),
                IdempotencyKey = Guid.NewGuid().ToString("N"), Sequence = mediaRevision,
                RetainUntilUtc = now.AddDays(1)
            });
        await db.SaveChangesAsync();
        return media;
    }
}
