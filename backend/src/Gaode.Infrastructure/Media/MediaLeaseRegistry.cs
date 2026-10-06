using System.Collections.Concurrent;

namespace Gaode.Infrastructure.Media;

public sealed class MediaLeaseRegistry
{
    private readonly ConcurrentDictionary<Guid, int> _counts = new();
    public int Count(Guid mediaId) => _counts.TryGetValue(mediaId, out var value) ? value : 0;
    public int TotalCount => _counts.Values.Sum();
    public IDisposable Lease(Guid mediaId, string consumer)
    {
        if (string.IsNullOrWhiteSpace(consumer)) throw new ArgumentException("消费者身份缺失");
        _counts.AddOrUpdate(mediaId, 1, (_, n) => n + 1);
        return new Release(this, mediaId);
    }
    private sealed class Release(MediaLeaseRegistry owner, Guid id) : IDisposable
    {
        private int _done;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _done, 1) == 0)
                owner._counts.AddOrUpdate(id, 0, (_, n) => Math.Max(0, n - 1));
        }
    }
}
