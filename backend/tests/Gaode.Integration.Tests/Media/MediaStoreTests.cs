using Gaode.Infrastructure.Media;
using Gaode.Integration.Tests.Support;
using Xunit;

namespace Gaode.Integration.Tests.Media;

public sealed class MediaStoreTests
{
    [Fact]
    public async Task WritesAtomicFileKeepsLeaseAndReservesFMemory()
    {
        var approved = Environment.GetEnvironmentVariable("GAODE_TEST_ROOT");
        var approvedRoot = string.IsNullOrWhiteSpace(approved)
            ? Path.Combine(Station01HostFixture.FindWorkspace(), "artifacts", "station01", "test-stores")
            : Path.GetFullPath(approved);
        Directory.CreateDirectory(approvedRoot);
        var root = Path.Combine(approvedRoot, "media-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var leases = new MediaLeaseRegistry();
        var capacity = new MediaCapacity(10, 4, 100, 100);
        var store = new MediaStore(root, capacity, leases, 1);
        Assert.Throws<InvalidOperationException>(() => store.ReserveCapture(Guid.NewGuid(), "3D", 7));
        var capture = Guid.NewGuid();
        using (store.ReserveCapture(capture, "F", 7))
        {
            var media = await store.SaveAsync(Guid.NewGuid(), capture, "F", "1", "NotApplicable",
                [1, 2, 3], "img", "SyntheticFixture", CancellationToken.None);
            Assert.True(store.IsReady(media.MediaId));
            Assert.DoesNotContain("..", media.RelativeKey, StringComparison.Ordinal);
            Assert.Empty(Directory.EnumerateFiles(root, "*.partial", SearchOption.AllDirectories));
            using (store.Lease(media.MediaId, "test-consumer"))
                Assert.Equal(1, leases.Count(media.MediaId));
            Assert.Equal(0, leases.Count(media.MediaId));
            await using var stream = await store.OpenReadAsync(media.MediaId, CancellationToken.None);
            Assert.Equal(3, stream.Length);
        }
        Assert.Equal(0, capacity.MemoryUsed);
        Assert.Equal(3, capacity.FilesUsed);
    }
}
