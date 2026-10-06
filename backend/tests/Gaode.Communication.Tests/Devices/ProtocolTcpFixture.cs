using System.Net;
using System.Net.Sockets;
using Gaode.Infrastructure.Devices.Plc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VirtualPlc;

namespace Gaode.Communication.Tests.Devices;

// Real TCP and the actual simulator engine in this process. Not independent-process acceptance.
internal sealed partial class ProtocolTcpFixture : IAsyncDisposable
{
    public PlcDataStore Store { get; }
    public VirtualPlcEngine Engine { get; }
    public ModbusTcpServer Server { get; }
    public int Port { get; }
    public string? EvidenceStorePath { get; private set; }
    private Func<ValueTask>? disposeEvidence;
    public ProtocolTcpFixture(int motionDurationMs = 3000)
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
        if (disposeEvidence is not null) await disposeEvidence();
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await Server.StopAsync(cleanup.Token); }
        finally { await Engine.StopAsync(cleanup.Token); Server.Dispose(); Engine.Dispose(); }
    }
}

[Xunit.CollectionDefinition("CommunicationTcp", DisableParallelization = true)]
public sealed class CommunicationTcpCollection { }
