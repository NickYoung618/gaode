using System.Collections.Concurrent;
using Gaode.Application.Ports;
using Gaode.Diagnostics;

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
                source, cancellationToken).AsTask(),
            r => new { r.MediaId, r.CaptureId, r.RelativeKey, r.StorageState, r.ByteLength }));

    private async ValueTask<MediaRef> SaveCoreAsync(Guid runId, Guid captureId, string role,
        string pointVersion, string scopeVersion, byte[] buffer, string format,
        string source, CancellationToken cancellationToken)
    {
        if (runId == Guid.Empty || captureId == Guid.Empty || role is not ("3D" or "F" or "Detection" or "E") ||
            format is not ("bin" or "img" or "png") || buffer.Length == 0)
            throw new ArgumentException("媒体身份、格式或内容无效");
        if (!_reservations.ContainsKey(captureId))
            throw new InvalidOperationException("采集前未预约媒体容量");
        await _jobs.WaitAsync(cancellationToken);
        Interlocked.Increment(ref _activeJobs);
        try
        {
            var mediaId = Guid.NewGuid();
            var relative = Path.Combine("media", runId.ToString("N"), captureId.ToString("N"),
                mediaId.ToString("N") + "." + format);
            var full = Path.GetFullPath(Path.Combine(_root, relative));
            if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("媒体路径越界");
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
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
            _reservations[captureId].Commit(buffer.LongLength);
            _ready[mediaId] = reference;
            return reference;
        }
        finally { Interlocked.Decrement(ref _activeJobs); _jobs.Release(); }
    }

    public ValueTask<Stream> OpenReadAsync(Guid mediaId, CancellationToken cancellationToken)
    {
        if (!_ready.TryGetValue(mediaId, out var reference)) throw new FileNotFoundException("媒体未完成或未知");
        var full = Path.GetFullPath(Path.Combine(_root, reference.RelativeKey));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("媒体路径越界");
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
