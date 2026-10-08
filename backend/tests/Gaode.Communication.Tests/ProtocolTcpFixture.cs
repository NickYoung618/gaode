using Gaode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Sockets;
using Gaode.Infrastructure.Devices.Plc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VirtualPlc;

namespace Gaode.Communication.Tests.Devices;

// Real TCP and the actual simulator engine in this process. Not independent-process acceptance.
internal sealed partial class CommissioningProtocolTcpFixture : IAsyncDisposable
{
    private TraceWriter? evidenceWriter;
    private string? evidenceDirectory;
    internal TraceWriter EvidenceWriter => evidenceWriter!;
    internal DbContextOptions<Station01DbContext> DatabaseOptions { get; private set; } = null!;
    public string EvidenceDirectory => evidenceDirectory!;
    public List<string> DeviceLogs { get; } = [];
    public LatestProtocolPlcDevice Device(Action<PlcRuntimeOptions>? configure = null, [System.Runtime.CompilerServices.CallerMemberName] string testName = "")
    {
        evidenceDirectory = Path.Combine(Path.GetTempPath(), "gaode-stage-a-" + testName + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(evidenceDirectory);
        var options = new DbContextOptionsBuilder<Station01DbContext>()
            .UseSqlite($"Data Source={Path.Combine(evidenceDirectory, "test.db")};Pooling=False").Options;
        DatabaseOptions = options;
        var storeId = Guid.NewGuid();
        using (var db = new Station01DbContext(options))
        {
            db.Database.Migrate();
            db.Manifests.Add(new() { StoreId=storeId, SchemaVersion="s01-store/2", Profile="Test",
                PrepareOperationId=Guid.NewGuid(), PreparedUtc=DateTimeOffset.UtcNow });
            db.SaveChanges();
        }
        evidenceWriter = new TraceWriter(options, TimeProvider.System, 32);
        var recorder = new CommunicationEvidenceRecorder(evidenceWriter, storeId, TimeProvider.System, 2000);
        var runtime = new PlcRuntimeOptions { Provider="Virtual", Host="127.0.0.1", Port=Port, UnitId=1,
            IoTimeoutMs=1000, HeartbeatTimeoutMs=3000 };
        configure?.Invoke(runtime);
        return new(runtime, .01, new FixtureLogger(DeviceLogs), recorder: recorder);
    }
    public PlcDataStore Store { get; }
    public VirtualPlcEngine Engine { get; }
    public ModbusTcpServer Server { get; }
    public int Port { get; }
    public CommissioningProtocolTcpFixture(int motionDurationMs = 3000)
    {
        using (var listener = new TcpListener(IPAddress.Loopback, 0))
        { listener.Start(); Port = ((IPEndPoint)listener.LocalEndpoint).Port; }
        var simulation = Options.Create(new SimulationOptions
        {
            ScanPeriodMs = 5, HeartbeatPeriodMs = 100, HeartbeatTimeoutMs = 3000,
            // Existing strict-recipe Test timing; the adapter must actually observe Executing.
            // Business acceptance/completion deadlines are not changed to accommodate polling.
            MotionDurationMs = motionDurationMs, SortingDurationMs = 80, FlipDurationMs = 80, PutBackDurationMs = 80, FlipPutBackSafeZ = 150,
            InterActionGapMs = 10, ActionDurationJitterMs = 0
        });
        Store = new(simulation);
        Engine = new(Store, simulation, NullLogger<VirtualPlcEngine>.Instance);
        Server = new(Store, Options.Create(new ModbusOptions
        { ListenAddress = "127.0.0.1", Port = Port, UnitId = 1 }), NullLogger<ModbusTcpServer>.Instance);
    }
    public ModbusTcpClient Client() => new("127.0.0.1", Port, 1, TimeSpan.FromMilliseconds(200));
    public async Task StartAsync(CancellationToken token)
    {
        await Engine.StartAsync(token);
        await Server.StartAsync(token);
        while (true)
        {
            token.ThrowIfCancellationRequested();
            using var socket = new TcpClient();
            try { await socket.ConnectAsync(IPAddress.Loopback, Port, token); return; }
            catch (SocketException) { await Task.Delay(10, token); }
        }
    }
    public static async Task UntilAsync(Func<bool> predicate, CancellationToken token)
    {
        while (!predicate()) { token.ThrowIfCancellationRequested(); await Task.Delay(10, token); }
    }
    public async ValueTask DisposeAsync()
    {
        if (evidenceWriter is not null) await evidenceWriter.DisposeAsync();
        if (evidenceDirectory is not null)
        {
            if (Environment.GetEnvironmentVariable("GAODE_STAGE_A_EVIDENCE") is { Length: > 0 } export)
            {
                var destination=Path.Combine(export,Path.GetFileName(evidenceDirectory));
                Directory.CreateDirectory(destination);
                foreach(var file in Directory.GetFiles(evidenceDirectory)) File.Copy(file,Path.Combine(destination,Path.GetFileName(file)));
                File.WriteAllLines(Path.Combine(destination,"device.log"),DeviceLogs);
                File.WriteAllText(Path.Combine(destination,"plc-audit.json"),System.Text.Json.JsonSerializer.Serialize(new {writes=Store.GetWriteAudit(), actions=Engine.GetActionAudit()},new System.Text.Json.JsonSerializerOptions {WriteIndented=true}));
            }
            Directory.Delete(evidenceDirectory, true);
        }
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await Server.StopAsync(cleanup.Token); }
        finally { await Engine.StopAsync(cleanup.Token); Server.Dispose(); Engine.Dispose(); }
    }
}


internal sealed class FixtureLogger(List<string> entries) : Microsoft.Extensions.Logging.ILogger<LatestProtocolPlcDevice>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel level) => true;
    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel level, Microsoft.Extensions.Logging.EventId id,
        TState state, Exception? error, Func<TState,Exception?,string> formatter)
    { lock(entries) entries.Add(DateTimeOffset.UtcNow.ToString("O")+" "+level+" "+formatter(state,error)+" "+error?.Message); }
}
