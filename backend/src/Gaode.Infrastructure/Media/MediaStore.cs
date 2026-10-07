using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Diagnostics;
using Gaode.Domain.Station01;

namespace Gaode.Infrastructure.Media;

public sealed class MediaStore : IMediaStore
{
    private readonly string _root;
    private readonly MediaCapacity _capacity;
    private readonly MediaLeaseRegistry _leases;
    private readonly SemaphoreSlim _jobs;
    private readonly ConcurrentDictionary<Guid, MediaRef> _ready = new();
    private readonly ConcurrentDictionary<Guid, MediaCapacity.Reservation> _reservations = new();
    private int _activeJobs;
    public int ActiveReservations => _reservations.Count;
    public int ActiveJobs => Volatile.Read(ref _activeJobs);
    public int ActiveLeases => _leases.TotalCount;

    public bool TryGetReference(Guid mediaId, out MediaRef reference) =>
        _ready.TryGetValue(mediaId, out reference!);

    public MediaStore(string root, MediaCapacity capacity, MediaLeaseRegistry leases, int jobs)
    {
        _root = Path.GetFullPath(root);
        if (!Directory.Exists(_root) || (File.GetAttributes(_root) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("媒体根未准备或为链接");
        _capacity = capacity;
        _leases = leases;
        _jobs = new(Math.Max(1, jobs), Math.Max(1, jobs));
    }

    public ValueTask<MediaRef> SaveAsync(Guid runId, Guid captureId, string role,
        string pointVersion, string scopeVersion, byte[] buffer, string format,
        string source, CancellationToken cancellationToken) => new(RuntimeDiagnostics.ObserveAsync(
            "MediaStorage", runId, new { captureId, role, pointVersion, scopeVersion, format,
                source, byteLength = buffer.LongLength, ActiveJobs, ActiveReservations },
            () => SaveCoreAsync(runId, captureId, role, pointVersion, scopeVersion, buffer, format,
                source, null, cancellationToken).AsTask(),
            r => new { r.MediaId, r.CaptureId, r.RelativeKey, r.StorageState, r.ByteLength }));

    public ValueTask<MediaRef> SaveCaptureAsync(Guid runId, Guid captureId, string role,
        string pointVersion, string scopeVersion, byte[] buffer, string format,
        string source, CorrelatedCaptureFact fact, CancellationToken cancellationToken) => new(RuntimeDiagnostics.ObserveAsync(
            "MediaStorage", runId, new { captureId, role, format, source, byteLength = buffer.LongLength,
                workerSessionId = fact.FrameMetadata?.WorkerSessionId },
            () => SaveCoreAsync(runId, captureId, role, pointVersion, scopeVersion, buffer, format,
                source, fact, cancellationToken).AsTask(),
            r => new { r.MediaId, r.CaptureId, r.RelativeKey, r.StorageState, r.ByteLength }));

    private async ValueTask<MediaRef> SaveCoreAsync(Guid runId, Guid captureId, string role,
        string pointVersion, string scopeVersion, byte[] buffer, string format,
        string source, CorrelatedCaptureFact? fact, CancellationToken cancellationToken)
    {
        if (runId == Guid.Empty || captureId == Guid.Empty || role is not ("3D" or "A" or "B" or "C" or "D" or "F" or "Detection" or "E") ||
            string.IsNullOrWhiteSpace(format) || format.Length > 32 ||
            format.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_') || buffer.Length == 0)
            throw new ArgumentException("媒体身份、格式或内容无效");
        if (fact is not null && (fact.RunId != runId || fact.CaptureId != captureId || fact.MediaSource != source))
            throw new ArgumentException("媒体与采集事实身份不符");
        if (source.StartsWith("Real", StringComparison.OrdinalIgnoreCase) && fact?.FrameMetadata is null)
            throw new ArgumentException("真实媒体必须提供帧事实和元数据");
        if (fact?.CameraOrigin.Source == ComponentEvidenceSource.Real &&
            (fact.FrameMetadata is null || fact.FrameMetadata.PayloadBytes != buffer.LongLength))
            throw new ArgumentException("真实帧元数据缺失或长度不符");
        if (!_reservations.ContainsKey(captureId))
            throw new InvalidOperationException("采集前未预约媒体容量");
        await _jobs.WaitAsync(cancellationToken);
        Interlocked.Increment(ref _activeJobs);
        try
        {
            var mediaId = Guid.NewGuid();
            var relative = Path.Combine("media", runId.ToString("N"), captureId.ToString("N"),
                mediaId.ToString("N") + "." + format);
            var full = Resolve(relative);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            full = Resolve(relative);
            var temp = full + ".partial";
            try
            {
                await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write,
                    FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    await stream.WriteAsync(buffer, cancellationToken);
                    await stream.FlushAsync(cancellationToken);
                    stream.Flush(flushToDisk: true);
                }
                File.Move(temp, full);
            }
            catch (Exception error)
            {
                RuntimeDiagnostics.Record("MediaStorage", "FileSaveFailed", runId,
                    new { captureId, mediaId, relativeKey = relative,
                        partialFile = temp, disposition = "PartialFileRetained_NoMediaReadyClaim" }, error);
                throw;
            }
            var reference = new MediaRef(mediaId, runId, captureId,
                role == "3D" ? "PointCloud" : role == "Detection" ? "DetectionImage" : "Image",
                relative.Replace('\\', '/'), buffer.LongLength, format, source,
                scopeVersion, pointVersion, "FileCompleted");
            var evidence = new StoredEvidence(reference, fact, Convert.ToHexString(SHA256.HashData(buffer)));
            await WriteEvidenceAsync(full + ".metadata.json", evidence, cancellationToken);
            _reservations[captureId].Commit(buffer.LongLength);
            return reference;
        }
        finally { Interlocked.Decrement(ref _activeJobs); _jobs.Release(); }
    }

    private sealed record StoredEvidence(MediaRef Reference, CorrelatedCaptureFact? Fact, string Sha256);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static async Task WriteEvidenceAsync(string path, StoredEvidence evidence, CancellationToken ct)
    {
        await using (var stream = new FileStream(path + ".partial", FileMode.CreateNew, FileAccess.Write,
            FileShare.None, 65536, FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, evidence, JsonOptions, ct);
            await stream.FlushAsync(ct);
            stream.Flush(true);
        }
        File.Move(path + ".partial", path);
    }

    public ValueTask MarkCommittedAsync(MediaRef reference, CancellationToken cancellationToken) =>
        RestoreCommittedAsync(reference, null, cancellationToken);

    // Only callers holding a durable Media write may publish. Restore never guesses from files.
    public async ValueTask RestoreCommittedAsync(MediaRef reference, CorrelatedCaptureFact? indexedFact,
        CancellationToken cancellationToken)
    {
        if (reference.StorageState != "FileCompleted") throw new InvalidDataException("媒体索引未完成");
        var full = Resolve(reference.RelativeKey);
        if (new FileInfo(full).Length != reference.ByteLength) throw new InvalidDataException("媒体文件长度不符");
        var sidecar = full + ".metadata.json";
        if (File.Exists(sidecar))
        {
            await using var input = File.OpenRead(sidecar);
            var evidence = await JsonSerializer.DeserializeAsync<StoredEvidence>(input, JsonOptions, cancellationToken)
                ?? throw new InvalidDataException("媒体元数据无效");
            if ((evidence.Reference with { Purpose = null }) != (reference with { Purpose = null }) || (indexedFact is not null &&
                JsonSerializer.Serialize(evidence.Fact, JsonOptions) != JsonSerializer.Serialize(indexedFact, JsonOptions)))
                throw new InvalidDataException("媒体元数据与索引不符");
            await using var data = File.OpenRead(full);
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(data, cancellationToken));
            if (hash != evidence.Sha256) throw new InvalidDataException("媒体摘要不符");
            if (evidence.Fact?.CameraOrigin.Source == ComponentEvidenceSource.Real &&
                (evidence.Fact.FrameMetadata is null || evidence.Fact.FrameMetadata.PayloadBytes != reference.ByteLength))
                throw new InvalidDataException("真实帧元数据不完整");
            if (reference.Source.StartsWith("Real", StringComparison.OrdinalIgnoreCase) && evidence.Fact?.FrameMetadata is null)
                throw new InvalidDataException("真实媒体缺少帧元数据");
        }
        else if (indexedFact?.CameraOrigin.Source == ComponentEvidenceSource.Real ||
            reference.Source.StartsWith("Real", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("真实媒体元数据缺失");
        cancellationToken.ThrowIfCancellationRequested();
        _ready[reference.MediaId] = reference;
    }

    private string Resolve(string relative)
    {
        if (Path.IsPathRooted(relative)) throw new UnauthorizedAccessException("媒体路径必须相对");
        var full = Path.GetFullPath(Path.Combine(_root, relative));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("媒体路径越界");
        for (var current = full; current != _root; current = Path.GetDirectoryName(current)!)
            if ((File.Exists(current) || Directory.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new UnauthorizedAccessException("媒体路径不允许链接");
        return full;
    }

    public ValueTask<Stream> OpenReadAsync(Guid mediaId, CancellationToken cancellationToken)
    {
        if (!_ready.TryGetValue(mediaId, out var reference)) throw new FileNotFoundException("媒体未完成或未知");
        var full = Resolve(reference.RelativeKey);
        Stream stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read,
            65536, FileOptions.Asynchronous);
        return ValueTask.FromResult(stream);
    }

    public IDisposable Lease(Guid mediaId, string consumer)
    {
        if (!_ready.ContainsKey(mediaId)) throw new FileNotFoundException("媒体未完成或未知");
        return _leases.Lease(mediaId, consumer);
    }
    public bool IsReady(Guid mediaId) => _ready.ContainsKey(mediaId);

    public IDisposable ReserveCapture(Guid captureId, string role, long maxBytes)
    {
        if (captureId == Guid.Empty) throw new ArgumentException("采集身份缺失");
        var inner = _capacity.Reserve(role, maxBytes);
        if (!_reservations.TryAdd(captureId, inner))
        {
            inner.Dispose();
            throw new InvalidOperationException("重复采集预约");
        }
        return new CaptureReservation(this, captureId);
    }

    public async Task<bool> WaitForIdleAsync(CancellationToken cancellationToken)
    {
        while (ActiveJobs != 0 || ActiveReservations != 0 || ActiveLeases != 0)
        {
            try { await Task.Delay(10, cancellationToken); }
            catch (OperationCanceledException) { return false; }
        }
        return true;
    }

    private sealed class CaptureReservation(MediaStore store, Guid captureId) : IDisposable
    {
        private int _done;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _done, 1) != 0) return;
            if (store._reservations.TryRemove(captureId, out var inner)) inner.Dispose();
        }
    }
}
