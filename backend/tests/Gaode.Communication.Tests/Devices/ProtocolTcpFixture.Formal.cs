using System.Diagnostics;
using Gaode.Application.Ports;
using Gaode.Infrastructure.Devices.Plc;
using Gaode.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Gaode.Communication.Tests.Devices;

internal sealed partial class ProtocolTcpFixture
{
    private readonly ComponentDeviceLog componentLog = new();
    public string DeviceDiagnostics => string.Join(Environment.NewLine, componentLog.Entries);
    private sealed class ComponentDeviceLog : ILogger<LatestProtocolPlcDevice>
    {
        public System.Collections.Concurrent.ConcurrentQueue<string> Entries { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Warning;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? error,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(level)) return;
            Entries.Enqueue(formatter(state, error) + Environment.NewLine + error);
            while (Entries.Count > 50) Entries.TryDequeue(out _);
        }
    }
    internal TraceWriter? EvidenceWriter { get; private set; }
    internal CommunicationEvidenceRecorder? EvidenceRecorder { get; private set; }
    public LatestProtocolPlcDevice Device(PlcDefinitionTestInput? input = null, int? communicationPort = null,
        PlcPoseProgram[]? posePrograms = null,
        bool includeSortingSafetyPosition = true, int ioTimeoutMs = 1000)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "global.json"))) root = root.Parent;
        if (root is null) throw new InvalidOperationException("WorkspaceNotFound");
        var directory = Path.Combine(Gaode.Testing.ApprovedTestRoot.Resolve(root.FullName), "009-wire-formal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        EvidenceStorePath = Path.Combine(directory, "component.test.db");
        var connection = new SqliteConnectionStringBuilder { DataSource = EvidenceStorePath, ForeignKeys = true, Pooling = false }.ToString();
        var options = new DbContextOptionsBuilder<Station01DbContext>().UseSqlite(connection).Options;
        var storeId = Guid.NewGuid();
        using (var db = new Station01DbContext(options))
        {
            db.Database.Migrate();
            db.Manifests.Add(new() { StoreId = storeId, SchemaVersion = "s01-store/2", Profile = "Test",
                PrepareOperationId = Guid.NewGuid(), PreparedUtc = DateTimeOffset.UtcNow });
            db.SaveChanges();
        }
        var writer = new TraceWriter(options, TimeProvider.System, 32);
        EvidenceWriter = writer;
        var recorder = new CommunicationEvidenceRecorder(writer, storeId, TimeProvider.System, 2000);
        EvidenceRecorder = recorder;
        disposeEvidence = writer.DisposeAsync;
        return new(new PlcRuntimeOptions
        {
            Provider = "Virtual", Host = "127.0.0.1", Port = communicationPort ?? Port, UnitId = 1,
            // Same supported Test transport limit as SingleFaceDetectionIntegrationTests.
            // The independent business window (including RecipeApplication's 10000 ms) is unchanged.
            IoTimeoutMs = ioTimeoutMs, HeartbeatTimeoutMs = 3000,
            PosePrograms = posePrograms ?? [],
            SortingSafePosition = includeSortingSafetyPosition
                ? new(200, "mm", "SIM_MACHINE", "Test", "Declared communication component input; not site approval") : null
        }, 0.01, componentLog, input, recorder);
    }
    public static PortEnvelope Envelope(int milliseconds = 10000)
    {
        var start = Stopwatch.GetTimestamp();
        return new(Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(), "pd-frozen-test", "pd/1",
            "Test", start, start + checked((long)(Stopwatch.Frequency * milliseconds / 1000d)), "Stopwatch");
    }
}
