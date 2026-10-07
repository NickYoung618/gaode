using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Diagnostics;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Media;
using Microsoft.EntityFrameworkCore;

namespace Gaode.Infrastructure.Persistence;

/// <summary>Capture-only journal, using the station's existing durable schema.</summary>
public sealed class CameraCaptureJournal(DbContextOptions<Station01DbContext> options) : ICameraCaptureJournal
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static DbContextOptions<Station01DbContext> Options(string absoluteRoot)
    {
        if (!Path.IsPathFullyQualified(absoluteRoot)) throw new ArgumentException("存储根须为绝对路径");
        return new DbContextOptionsBuilder<Station01DbContext>()
            .UseSqlite(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
            { DataSource = Path.Combine(absoluteRoot, "camera.db"), Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadWrite,
                ForeignKeys = true, DefaultTimeout = 30 }.ToString()).Options;
    }

    // Explicit provisioning only; normal Host startup never creates or migrates a database.
    public static DbContextOptions<Station01DbContext> Prepare(string absoluteRoot)
    {
        if (!Path.IsPathFullyQualified(absoluteRoot)) throw new ArgumentException("存储根须为绝对路径");
        if (File.Exists(Path.Combine(absoluteRoot, "camera.db"))) throw new InvalidOperationException("相机库已存在，禁止重新准备");
        Directory.CreateDirectory(absoluteRoot);
        if ((File.GetAttributes(absoluteRoot) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("存储根不允许链接");
        var creation = new DbContextOptionsBuilder<Station01DbContext>().UseSqlite(
            new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = Path.Combine(absoluteRoot, "camera.db"),
                ForeignKeys = true, DefaultTimeout = 30 }.ToString()).Options;
        using var db = new Station01DbContext(creation);
        db.Database.Migrate();
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
        db.Database.ExecuteSqlRaw("PRAGMA synchronous=FULL;");
        Directory.CreateDirectory(Path.Combine(absoluteRoot, "media"));
        return Options(absoluteRoot);
    }

    public async Task RecordIntentAsync(CaptureRequest request, CancellationToken cancellationToken)
    {
        if (!request.Envelope.IsValid || request.IntentWriteId == Guid.Empty || request.CaptureId == Guid.Empty)
            throw new ArgumentException("采集意图身份无效");
        await using var db = new Station01DbContext(options);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Runs.Add(new RunEntity
        {
            RunId = request.Envelope.RunId, RequestId = request.CaptureId.ToString("N"),
            SubjectId = request.CameraBindingId,
            ContextJson = JsonSerializer.Serialize(new { purpose = "RealCameraCapture", request.CameraBindingId }, JsonOptions),
            State = RunState.Created, Revision = 1, CreatedUtc = DateTimeOffset.UtcNow
        });
        db.Writes.Add(Write(request.IntentWriteId, request.Envelope.RunId, 1, "CaptureIntent",
            new { kind = "RealCameraCapture", request.CaptureId, requestedCapture = request }));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        RuntimeDiagnostics.Record("CameraCaptureJournal", "IntentCommitted", request.Envelope.RunId,
            new { request.CaptureId, request.IntentWriteId, request.CameraBindingId });
    }

    public async Task CommitAsync(CaptureRequest request, MediaRef media, CorrelatedCaptureFact fact,
        CancellationToken cancellationToken)
    {
        if (media.RunId != request.Envelope.RunId || media.CaptureId != request.CaptureId ||
            media.StorageState != "FileCompleted" || media.Source != fact.MediaSource ||
            !AcquisitionContract.MatchesFact(fact, request, fact.ConnectionEpoch) || fact.Replayed)
            throw new InvalidDataException("采集提交身份或事实不符");
        if (fact.CameraOrigin.Source == ComponentEvidenceSource.Real && fact.FrameMetadata is null)
            throw new InvalidDataException("真实采集元数据缺失");
        await using var db = new Station01DbContext(options);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var run = await db.Runs.SingleAsync(x => x.RunId == media.RunId, cancellationToken);
        if (run.Terminal != TerminalOutcome.None || !await db.Writes.AnyAsync(x =>
            x.WriteId == request.IntentWriteId && x.RunId == run.RunId && x.Kind == "CaptureIntent", cancellationToken))
            throw new InvalidOperationException("采集意图未提交或已经结束");
        db.Media.Add(new MediaEntity { MediaId = media.MediaId, RunId = media.RunId, CaptureId = media.CaptureId,
            RelativeKey = media.RelativeKey, ByteLength = media.ByteLength, Format = media.Format,
            Source = media.Source, State = media.StorageState });
        db.Writes.Add(Write(Guid.NewGuid(), run.RunId, ++run.Revision, "Media", media));
        db.Writes.Add(Write(Guid.NewGuid(), run.RunId, ++run.Revision, "CaptureFact",
            new { media.MediaId, media.CaptureId, captureFact = fact }));
        db.Writes.Add(Write(Guid.NewGuid(), run.RunId, ++run.Revision, "Complete",
            new { purpose = "RealCameraCapture", media.MediaId, media.CaptureId }));
        run.State = RunState.Completed;
        run.Terminal = TerminalOutcome.Completed;
        run.TerminalRevision = run.Revision;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        RuntimeDiagnostics.Record("CameraCaptureJournal", "MediaCommitted", run.RunId,
            new { media.MediaId, media.CaptureId, run.Revision });
    }

    public async Task RecordFailureAsync(CaptureRequest request, string reason, CancellationToken cancellationToken)
    {
        await using var db = new Station01DbContext(options);
        var run = await db.Runs.SingleAsync(x => x.RunId == request.Envelope.RunId, cancellationToken);
        // A lost commit acknowledgement must never overwrite a durable success.
        if (run.Terminal != TerminalOutcome.None)
        {
            RuntimeDiagnostics.Record("CameraCaptureJournal", "DurableCommitExists_ReadinessUnconfirmed", run.RunId,
                new { request.CaptureId, reason, run.Terminal });
            return;
        }
        db.Writes.Add(Write(Guid.NewGuid(), run.RunId, ++run.Revision, "Audit",
            new { kind = "CaptureFailure", request.CaptureId, state = "Unknown", reason }));
        run.State = RunState.RecoveryRequired;
        await db.SaveChangesAsync(cancellationToken);
        RuntimeDiagnostics.Record("CameraCaptureJournal", "CaptureUnknown", run.RunId,
            new { request.CaptureId, reason });
    }

    public async Task<IReadOnlyList<MediaRef>> ListCommittedAsync(CancellationToken cancellationToken)
    {
        await using var db = new Station01DbContext(options);
        var media = await db.Media.AsNoTracking().ToArrayAsync(cancellationToken);
        var writes = await db.Writes.AsNoTracking().Where(x => x.Kind == "Media").ToArrayAsync(cancellationToken);
        var result = new List<MediaRef>();
        foreach (var write in writes)
        {
            try
            {
                if (!DigestMatches(write)) throw new InvalidDataException("媒体索引写摘要不符");
                var reference = JsonSerializer.Deserialize<MediaRef>(write.PayloadJson, JsonOptions)
                    ?? throw new InvalidDataException("媒体索引无效");
                var row = media.SingleOrDefault(x => x.MediaId == reference.MediaId);
                if (row is null || write.RunId != reference.RunId || row.RunId != reference.RunId ||
                    row.CaptureId != reference.CaptureId || row.RelativeKey != reference.RelativeKey ||
                    row.ByteLength != reference.ByteLength || row.Format != reference.Format ||
                    row.Source != reference.Source || row.State != reference.StorageState)
                    throw new InvalidDataException("媒体索引与投影不符");
                result.Add(reference);
            }
            catch (Exception error) when (error is InvalidDataException or JsonException)
            {
                RuntimeDiagnostics.Record("CameraCaptureJournal", "IndexRejected_NotReady", write.RunId,
                    new { write.WriteId, write.Revision }, error);
            }
        }
        return result;
    }

    public async Task<CorrelatedCaptureFact?> GetFactAsync(Guid mediaId, CancellationToken cancellationToken)
    {
        await using var db = new Station01DbContext(options);
        var media = await db.Media.AsNoTracking().SingleOrDefaultAsync(x => x.MediaId == mediaId, cancellationToken);
        if (media is null) return null;
        var writes = await db.Writes.AsNoTracking().Where(x => x.RunId == media.RunId && x.Kind == "CaptureFact")
            .ToArrayAsync(cancellationToken);
        foreach (var write in writes)
        {
            using var json = JsonDocument.Parse(write.PayloadJson);
            if (!TryProperty(json.RootElement, "mediaId", out var id) || id.GetGuid() != mediaId) continue;
            if (!DigestMatches(write)) throw new InvalidDataException("采集事实摘要不符");
            if (!TryProperty(json.RootElement, "captureFact", out var value)) return null;
            var fact = value.Deserialize<CorrelatedCaptureFact>(JsonOptions);
            if (fact is null || fact.RunId != media.RunId || fact.CaptureId != media.CaptureId || fact.MediaSource != media.Source)
                throw new InvalidDataException("采集事实身份不符");
            return fact;
        }
        return null;
    }

    public async Task<CaptureFrameMetadata?> GetMetadataAsync(Guid mediaId, CancellationToken cancellationToken) =>
        (await GetFactAsync(mediaId, cancellationToken))?.FrameMetadata;

    public async Task RestoreAsync(MediaStore store, CancellationToken cancellationToken)
    {
        foreach (var reference in await ListCommittedAsync(cancellationToken))
        {
            try
            {
                var fact = await GetFactAsync(reference.MediaId, cancellationToken);
                if (reference.Source.StartsWith("Real", StringComparison.OrdinalIgnoreCase) && fact?.FrameMetadata is null)
                    throw new InvalidDataException("真实媒体缺少已提交帧事实");
                await store.RestoreCommittedAsync(reference, fact, cancellationToken);
            }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
            {
                RuntimeDiagnostics.Record("CameraCaptureJournal", "RestoreRejected_NotReady", reference.RunId,
                    new { reference.MediaId, reference.CaptureId, reference.RelativeKey }, error);
            }
        }
    }

    private static bool TryProperty(JsonElement value, string name, out JsonElement result)
    {
        foreach (var item in value.EnumerateObject())
            if (string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase)) { result = item.Value; return true; }
        result = default;
        return false;
    }

    private static WriteEntity Write(Guid id, Guid runId, long revision, string kind, object payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        return new WriteEntity { WriteId = id, RunId = runId, Revision = revision, Kind = kind,
            PayloadJson = json, PayloadDigest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))),
            CommittedUtc = DateTimeOffset.UtcNow };
    }

    private static bool DigestMatches(WriteEntity write) =>
        string.Equals(write.PayloadDigest, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(write.PayloadJson))),
            StringComparison.OrdinalIgnoreCase);
}

