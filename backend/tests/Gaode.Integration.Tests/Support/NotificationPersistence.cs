using Gaode.Application.Station01;
using Gaode.Host.Api;
using Gaode.Infrastructure.Persistence;
using Gaode.Integration.Tests.Storage;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gaode.Integration.Tests.Support;

// Actual isolated read/write backing for notification delivery components.
// This does not replace hardware or claim a completed business run.
internal sealed class NotificationPersistence : IAsyncDisposable
{
    private readonly TraceWriter writer;
    private readonly TraceQuery query;
    private readonly StageEventStore stages;

    private NotificationPersistence(DbContextOptions<Station01DbContext> options)
    {
        writer = new(options, TimeProvider.System, 128);
        query = new(options, TimeProvider.System, 2000);
        stages = new(options);
    }

    internal static async Task<NotificationPersistence> CreateAsync()
    {
        var allowed = Gaode.Testing.ApprovedTestRoot.Resolve(Station01HostFixture.FindWorkspace());
        var root = Path.Combine(allowed, "notification-" + Guid.NewGuid().ToString("N"));
        await StorePreparation.PrepareEmptyTestStoreAsync(allowed, root);
        return new(new DbContextOptionsBuilder<Station01DbContext>()
            .UseSqlite($"Data Source={Path.Combine(root, "station01.test.db")};Pooling=False").Options);
    }

    internal Station01NotificationService CreateService(Station01Coordinator coordinator,
        IHubContext<Station01Hub> hub, int capacity = 64) =>
        new(coordinator, hub, query, stages, writer, stages,
            NullLogger<Station01NotificationService>.Instance, capacity);

    public ValueTask DisposeAsync() => writer.DisposeAsync();
}
