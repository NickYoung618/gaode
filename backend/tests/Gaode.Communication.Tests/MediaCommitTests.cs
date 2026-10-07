using System.Security.Cryptography;
using Gaode.Application.Ports;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Media;
using Gaode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Gaode.Communication.Tests;

public sealed class MediaCommitTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "gaode-media-test-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task DurableCommitPublishesRawDataAndRestoresMetadataAfterRestart()
    {
        var options = CameraCaptureJournal.Prepare(_root);
        var journal = new CameraCaptureJournal(options);
        var store = Store();
        var (request, fact, data) = Capture();
        await journal.RecordIntentAsync(request, default);
        using (store.ReserveCapture(request.CaptureId, "A", data.LongLength))
        {
            var reference = await Save(store, request, fact, data);
            Assert.EndsWith(".raw", reference.RelativeKey);
            Assert.False(store.IsReady(reference.MediaId));
            await Assert.ThrowsAsync<FileNotFoundException>(() => store.OpenReadAsync(reference.MediaId, default).AsTask());
            await journal.CommitAsync(request, reference, fact, default);
            await store.MarkCommittedAsync(reference, default);
            Assert.True(store.IsReady(reference.MediaId));
            var restarted = Store();
            await new CameraCaptureJournal(options).RestoreAsync(restarted, default);
            Assert.True(restarted.IsReady(reference.MediaId));
            await using var content = await restarted.OpenReadAsync(reference.MediaId, default);
            using var copy = new MemoryStream();
            await content.CopyToAsync(copy);
            Assert.Equal(data, copy.ToArray());
            var metadata = await journal.GetMetadataAsync(reference.MediaId, default);
            Assert.Equal(fact.FrameMetadata!.WorkerSessionId, metadata!.WorkerSessionId);
            Assert.Equal(fact.FrameMetadata.FrameId, metadata.FrameId);
            await content.DisposeAsync();

            // Same byte length must still fail the stored SHA-256 check after restart.
            await File.WriteAllBytesAsync(Path.Combine(_root, reference.RelativeKey), [9, 8, 7, 6]);
            var corruptRestart = Store();
            await journal.RestoreAsync(corruptRestart, default);
            Assert.False(corruptRestart.IsReady(reference.MediaId));
        }
    }

    [Fact]
    public async Task IndexCommitFailureDoesNotPublishOrRestoreOrphanFile()
    {
        var options = CameraCaptureJournal.Prepare(_root);
        var journal = new CameraCaptureJournal(options);
        var store = Store();
        var (request, fact, data) = Capture();
        await journal.RecordIntentAsync(request, default);
        using var reservation = store.ReserveCapture(request.CaptureId, "A", data.LongLength);
        var reference = await Save(store, request, fact, data);
        await using (var db = new Station01DbContext(options))
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_media BEFORE INSERT ON Media BEGIN SELECT RAISE(ABORT, 'injected index failure'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => journal.CommitAsync(request, reference, fact, default));
        await journal.RecordFailureAsync(request, "IndexCommitFailed", default);
        Assert.False(store.IsReady(reference.MediaId));
        Assert.True(File.Exists(Path.Combine(_root, reference.RelativeKey)));
        var restarted = Store();
        await journal.RestoreAsync(restarted, default);
        Assert.False(restarted.IsReady(reference.MediaId));
        await using var verify = new Station01DbContext(options);
        Assert.Empty(await verify.Media.ToArrayAsync());
        Assert.Empty(await verify.Writes.Where(x => x.Kind == "CaptureFact").ToArrayAsync());
        Assert.Equal(RunState.RecoveryRequired, (await verify.Runs.SingleAsync()).State);
    }

    [Fact]
    public async Task FileWriteFailureCannotProduceReadyMedia()
    {
        CameraCaptureJournal.Prepare(_root);
        Directory.Delete(Path.Combine(_root, "media"));
        await File.WriteAllTextAsync(Path.Combine(_root, "media"), "path obstruction");
        var store = Store();
        var (request, fact, data) = Capture();
        using (store.ReserveCapture(request.CaptureId, "A", data.LongLength))
            await Assert.ThrowsAsync<IOException>(() => Save(store, request, fact, data).AsTask());
        Assert.Equal(0, store.ActiveReservations);
        Assert.Equal(0, store.ActiveJobs);
        Assert.Empty(await new CameraCaptureJournal(CameraCaptureJournal.Options(_root)).ListCommittedAsync(default));
    }

    private MediaStore Store() => new(_root, new MediaCapacity(1024 * 1024, 0, 1024 * 1024, 1024 * 1024),
        new MediaLeaseRegistry(), 2);

    private static ValueTask<MediaRef> Save(MediaStore store, CaptureRequest request, CorrelatedCaptureFact fact, byte[] data) =>
        store.SaveCaptureAsync(request.Envelope.RunId, request.CaptureId, "A", "device-current", "capture-only",
            data, "raw", "RealCamera", fact, default);

    private static (CaptureRequest, CorrelatedCaptureFact, byte[]) Capture()
    {
        var session = Guid.NewGuid();
        var envelope = new PortEnvelope(Guid.NewGuid(), Guid.NewGuid(), 1, session, "capture-only", "site",
            "Production", 1, 10, "test-clock");
        var request = new CaptureRequest(envelope, Guid.NewGuid(), CaptureRole.Detection, "A", "device-current",
            "capture-only", "capture-only", "A", null, Guid.NewGuid(), 1024);
        byte[] data = [1, 2, 3, 4];
        var now = DateTimeOffset.UtcNow;
        var metadata = new CaptureFrameMetadata("capture/1", "A", "test-serial", "001122334455", "192.0.2.2",
            "192.0.2.1", session, 42, 123, 1, now, now, 2, 2, "Mono8", data.LongLength,
            new Dictionary<string, string> { ["ExposureUs"] = "1000" },
            [new FramePayload("image", 4, 1, 4, Convert.ToHexString(SHA256.HashData(data)))]);
        var fact = new CorrelatedCaptureFact(envelope.RunId, request.CaptureId, envelope.OperationId, 1,
            AcquisitionContract.RequestedSettingsDigest(request), "RealCamera",
            new ComponentExecutionOrigin(ComponentEvidenceSource.Real, "offline-storage-test", "Verified"),
            ComponentExecutionOrigin.Unknown, CaptureApplicationState.NotApplied, null, false, [])
            { FrameMetadata = metadata };
        return (request, fact, data);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
