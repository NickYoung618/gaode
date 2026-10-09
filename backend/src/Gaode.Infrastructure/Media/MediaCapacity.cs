namespace Gaode.Infrastructure.Media;

public sealed class MediaCapacity(long memoryLimit, long fReserve, long runFileLimit, long dataLimit)
{
    private readonly object _gate = new();
    private long _memory;
    private long _files;
    private long _diskReserved;
    private long _working;
    private bool _inventoryRestored;
    public long MemoryUsed { get { lock (_gate) return _memory; } }
    public long FilesUsed { get { lock (_gate) return _files; } }
    public long DiskReserved { get { lock (_gate) return _diskReserved; } }
    public long WorkingUsed { get { lock (_gate) return _working; } }

    // All retained files, including sidecars and partial writes. Reservations are separate.
    public void RestoreFilesUsed(long bytes)
    {
        if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
        lock (_gate)
        {
            if (_inventoryRestored) return;
            if (_memory != 0) throw new InvalidOperationException("Restore media inventory before reserving captures");
            _files = checked(_files + bytes);
            _inventoryRestored = true;
        }
    }

    public Reservation Reserve(string role, long bytes)
    {
        lock (_gate)
        {
            var allowedMemory = role == "3D" ? memoryLimit - fReserve : memoryLimit;
            if (bytes <= 0 || bytes > allowedMemory - _memory ||
                bytes > runFileLimit - _files - _diskReserved || bytes > dataLimit - _files - _diskReserved)
                throw new InvalidOperationException("媒体容量不足，禁止新采集");
            _memory += bytes;
            _diskReserved += bytes;
            return new Reservation(this, bytes, true);
        }
    }

    public Reservation ReserveFile(long bytes)
    {
        lock (_gate)
        {
            if (bytes <= 0 || bytes > runFileLimit - _files - _diskReserved || bytes > dataLimit - _files - _diskReserved)
                throw new InvalidOperationException("持久媒体磁盘额度不足");
            _diskReserved += bytes;
            return new(this, bytes, false);
        }
    }
    public IDisposable ReserveBuffer(long bytes)
    {
        lock (_gate)
        {
            if (bytes <= 0 || bytes > memoryLimit - _memory) throw new InvalidOperationException("媒体转换内存额度不足");
            _memory += bytes; return new BufferReservation(this, bytes);
        }
    }
    private sealed class BufferReservation(MediaCapacity owner, long bytes) : IDisposable
    {
        private int done;
        public void Dispose() { if (Interlocked.Exchange(ref done, 1) == 0) lock(owner._gate) owner._memory -= bytes; }
    }
    public IDisposable RetainWorking(long bytes)
    {
        lock (_gate)
        {
            if (bytes <= 0 || bytes > runFileLimit - _working) throw new InvalidOperationException("算法工作文件保留额度不足");
            _working += bytes;
            return new WorkingReservation(this, bytes);
        }
    }
    private sealed class WorkingReservation(MediaCapacity owner, long bytes) : IDisposable
    {
        private int done;
        public void Dispose() { if (Interlocked.Exchange(ref done, 1) == 0) lock(owner._gate) owner._working -= bytes; }
    }

    public sealed class Reservation(MediaCapacity owner, long bytes, bool memory) : IDisposable
    {
        public long MaxBytes => bytes;
        private int _done;
        private int _committed;
        public void Commit(long actualBytes)
        {
            if (actualBytes <= 0 || actualBytes > bytes) throw new ArgumentOutOfRangeException(nameof(actualBytes));
            if (Interlocked.Exchange(ref _committed, 1) != 0) throw new InvalidOperationException("媒体容量已提交");
            lock (owner._gate) { owner._diskReserved -= bytes; owner._files = checked(owner._files + actualBytes); }
        }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _done, 1) != 0) return;
            lock (owner._gate)
            {
                if (memory) owner._memory -= bytes;
                if (Volatile.Read(ref _committed) == 0) owner._diskReserved -= bytes;
            }
        }
    }
}
