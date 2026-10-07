namespace Gaode.Infrastructure.Media;

public sealed class MediaCapacity(long memoryLimit, long fReserve, long runFileLimit, long dataLimit)
{
    private readonly object _gate = new();
    private long _memory;
    private long _files;
    private bool _inventoryRestored;
    public long MemoryUsed { get { lock (_gate) return _memory; } }
    public long FilesUsed { get { lock (_gate) return _files; } }

    // Payload bytes only. Startup inventory is separate from in-flight memory/disk reservations.
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
                bytes > runFileLimit - _files || bytes > dataLimit - _files)
                throw new InvalidOperationException("媒体容量不足，禁止新采集");
            _memory += bytes;
            _files += bytes;
            return new Reservation(this, bytes);
        }
    }

    public sealed class Reservation(MediaCapacity owner, long bytes) : IDisposable
    {
        public long MaxBytes => bytes;
        private int _done;
        private int _committed;
        public void Commit(long actualBytes)
        {
            if (actualBytes <= 0 || actualBytes > bytes) throw new ArgumentOutOfRangeException(nameof(actualBytes));
            if (Interlocked.Exchange(ref _committed, 1) != 0) throw new InvalidOperationException("媒体容量已提交");
            lock (owner._gate) owner._files -= bytes - actualBytes;
        }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _done, 1) != 0) return;
            lock (owner._gate)
            {
                owner._memory -= bytes;
                if (Volatile.Read(ref _committed) == 0) owner._files -= bytes;
            }
        }
    }
}
