using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Acquisition;
using Gaode.Application.Ports;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Media;
using Gaode.Infrastructure.Persistence;
using Gaode.Infrastructure.Simulation;
using Xunit;

namespace Gaode.Communication.Tests;

public sealed class CameraServiceSimulationTests
{
    [Fact]
    public async Task FixedImageDeadlineCancelsOnceWithoutAutomaticReplay()
    {
        var root = Path.Combine(Path.GetTempPath(), "gaode-capture-timeout-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aD1sAAAAASUVORK5CYII=");
            await File.WriteAllBytesAsync(Path.Combine(root, "fixture.png"), png);
            var digest = Convert.ToHexString(SHA256.HashData(png));
            var manifest = Path.Combine(root, "manifest.json");
            await File.WriteAllTextAsync(manifest, JsonSerializer.Serialize(new
            {
                purpose = "Test", captureDelayMs = 3000,
                images = new[]
                {
                    new { role = "ThreeD", relativePath = "fixture.png", sha256 = digest },
                    new { role = "F", relativePath = "fixture.png", sha256 = digest }
                }
            }));
            var camera = new FileBackedCapture(manifest);
            var store = new MediaStore(root, new MediaCapacity(1024, 0, 1024, 1024), new MediaLeaseRegistry(), 1);
            var service = new CameraAcquisitionService(camera, store);
            var start = Stopwatch.GetTimestamp();
            var envelope = new PortEnvelope(Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(), "fixed-image-fixture",
                "fixture/1", "Test", start, start + Stopwatch.Frequency, "stopwatch");
            var request = new CaptureRequest(envelope, Guid.NewGuid(), CaptureRole.ThreeD, "fixture", "fixture/1",
                null, null, "ThreeD", null, Guid.NewGuid(), 1024);
            using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
            var error = await Record.ExceptionAsync(() => service.ReceiveAsync(request, deadline.Token));
            // Either the caller's deadline or the cancelled device callback may win the race.
            Assert.True(error is OperationCanceledException || error is IOException { Message: "CaptureCancelled" },
                error?.ToString() ?? "Capture unexpectedly succeeded");
            using var drainBudget = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            while (camera.ActiveExecutions != 0) await Task.Delay(10, drainBudget.Token);
            Assert.Equal(1, camera.TriggerCount(CaptureRole.ThreeD));
            Assert.Equal(0, camera.TriggerCount(CaptureRole.F));
            Assert.Equal(0, store.ActiveJobs);
            Assert.False(Directory.Exists(Path.Combine(root, "media")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task FixedImageUsesFormalReceiptAndDurableMediaWithoutRealFrameMetadata()
    {
        var root = Path.Combine(Path.GetTempPath(), "gaode-simulation-capture-" + Guid.NewGuid().ToString("N"));
        try
        {
            var options = CameraCaptureJournal.Prepare(root);
            var fixtures = Path.Combine(root, "fixtures");
            Directory.CreateDirectory(fixtures);
            // Frozen one-pixel PNG, explicitly Test evidence rather than a real device frame.
            var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aD1sAAAAASUVORK5CYII=");
            await File.WriteAllBytesAsync(Path.Combine(fixtures, "three-d.png"), png);
            await File.WriteAllBytesAsync(Path.Combine(fixtures, "f.png"), png);
            var digest = Convert.ToHexString(SHA256.HashData(png));
            var manifest = Path.Combine(fixtures, "manifest.json");
            var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            await File.WriteAllTextAsync(manifest, JsonSerializer.Serialize(new
            {
                purpose = "Test", captureDelayMs = 3000,
                images = new[]
                {
                    new { role = "ThreeD", relativePath = "three-d.png", sha256 = digest },
                    new { role = "F", relativePath = "f.png", sha256 = digest }
                }
            }, jsonOptions));
            var camera = new FileBackedCapture(manifest);
            MediaStore Store() => new(root, new MediaCapacity(1024 * 1024, 0, 1024 * 1024, 1024 * 1024),
                new MediaLeaseRegistry(), 1);
            var store = Store();
            var service = new CameraAcquisitionService(camera, store);
            var runId = Guid.NewGuid();
            var started = Stopwatch.GetTimestamp();
            var envelope = new PortEnvelope(runId, Guid.NewGuid(), 1, Guid.NewGuid(), "fixed-image-fixture",
                "fixture/1", "Test", started, started + 10 * Stopwatch.Frequency, "stopwatch");
            var request = new CaptureRequest(envelope, Guid.NewGuid(), CaptureRole.ThreeD, "fixture", "fixture/1",
                null, null, "ThreeD", null, Guid.NewGuid(), 1024);
            using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            await using var writer = new TraceWriter(options, TimeProvider.System, 8);
            long revision = 0;
            async Task Commit(WriteKind kind, object payload, Guid? id = null)
            {
                var json = JsonSerializer.Serialize(payload, jsonOptions);
                var receipt = await writer.SubmitCritical(new WriteBatch(id ?? Guid.NewGuid(), runId, revision,
                    kind, json, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)))), budget.Token).Completion;
                Assert.Equal(CommitState.Committed, receipt.State);
                revision = receipt.CommittedRevision!.Value;
            }
            var fixtureConfig = JsonSerializer.Serialize(new { purpose = "Test", manifest }, jsonOptions);
            var fixtureDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fixtureConfig)));
            await Commit(WriteKind.RunCreated, new RunCreatedPayload(Guid.NewGuid(), request.CaptureId.ToString("N"),
                "simulation-regression", fixtureConfig, fixtureConfig, fixtureConfig, fixtureConfig,
                envelope.SnapshotId, fixtureDigest, fixtureDigest, fixtureDigest));
            await Commit(WriteKind.CaptureIntent, new OperationIntentPayload(envelope.OperationId, "Capture3D", 1,
                null, request.CaptureId, "ThreeD", envelope.SnapshotId, request.IntentWriteId), request.IntentWriteId);

            using var reservation = store.ReserveCapture(request.CaptureId, "3D", request.MaxBytes);
            var received = await service.ReceiveAsync(request, budget.Token);
            Assert.Equal("png", received.Format);
            Assert.Equal(png, received.Bytes);
            Assert.Equal("Test/FixedImage", received.Fact.MediaSource);
            Assert.Equal(ComponentEvidenceSource.Test, received.Fact.CameraOrigin.Source);
            Assert.True(received.Fact.Replayed);
            Assert.Null(received.Fact.FrameMetadata);
            Assert.Equal(1, camera.TriggerCount(CaptureRole.ThreeD));
            var media = await store.SaveCaptureAsync(runId, request.CaptureId, "3D", "fixture/1", "fixture/1",
                received.Bytes, received.Format, received.Fact.MediaSource, received.Fact, budget.Token);
            Assert.False(store.IsReady(media.MediaId));
            await Commit(WriteKind.Media, media);
            await Commit(WriteKind.CaptureFact, new { media.MediaId, media.CaptureId, captureFact = received.Fact });
            await store.MarkCommittedAsync(media, budget.Token);
            Assert.True(store.IsReady(media.MediaId));

            var restarted = Store();
            var index = new CameraCaptureJournal(options);
            await index.RestoreAsync(restarted, budget.Token);
            Assert.True(restarted.TryGetReference(media.MediaId, out var restored));
            Assert.Equal("Test/FixedImage", restored.Source);
            Assert.Null(await index.GetMetadataAsync(media.MediaId, budget.Token));
            var fact = await index.GetFactAsync(media.MediaId, budget.Token);
            Assert.Equal(ComponentEvidenceSource.Test, fact!.CameraOrigin.Source);
            Assert.True(fact.Replayed);
            await using var stream = await restarted.OpenReadAsync(media.MediaId, budget.Token);
            using var content = new MemoryStream();
            await stream.CopyToAsync(content, budget.Token);
            Assert.Equal(png, content.ToArray());
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
