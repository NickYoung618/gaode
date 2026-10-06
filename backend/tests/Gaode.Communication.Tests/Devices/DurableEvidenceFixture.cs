using Gaode.Infrastructure.Devices.Plc;
using Gaode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Gaode.Communication.Tests.Devices;

// Real, isolated Test storage for migrated formal-port tests. No fabricated durable references.
internal sealed class DurableEvidenceFixture : IAsyncDisposable
{
    private readonly TraceWriter writer;
    public CommunicationEvidenceRecorder Recorder { get; }
    public string DatabasePath { get; }
    public DurableEvidenceFixture()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "global.json"))) root = root.Parent;
        if (root is null) throw new InvalidOperationException("WorkspaceNotFound");
        var directory = Path.Combine(Gaode.Testing.ApprovedTestRoot.Resolve(root.FullName), "009-migrated-wire-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        DatabasePath = Path.Combine(directory, "component.test.db");
        var options = new DbContextOptionsBuilder<Station01DbContext>().UseSqlite($"Data Source={DatabasePath};Pooling=False").Options;
        var storeId = Guid.NewGuid();
        using (var db = new Station01DbContext(options))
        {
            db.Database.Migrate();
            db.Manifests.Add(new() { StoreId = storeId, SchemaVersion = "s01-store/2", Profile = "Test",
                PrepareOperationId = Guid.NewGuid(), PreparedUtc = DateTimeOffset.UtcNow });
            db.SaveChanges();
        }
        writer = new(options, TimeProvider.System, 32);
        Recorder = new(writer, storeId, TimeProvider.System, 2000);
    }
    public ValueTask DisposeAsync() => writer.DisposeAsync();
}
