extern alias virtualplc;

using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ModbusOptions = virtualplc::VirtualPlc.ModbusOptions;
using ModbusTcpServer = virtualplc::VirtualPlc.ModbusTcpServer;
using PlcDataStore = virtualplc::VirtualPlc.PlcDataStore;
using SimulationOptions = virtualplc::VirtualPlc.SimulationOptions;
using VirtualPlcEngine = virtualplc::VirtualPlc.VirtualPlcEngine;
using SimulationFault = virtualplc::VirtualPlc.SimulationFault;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Gaode.Integration.Tests.Support;

/// <summary>Actual in-process TCP device fixture; never product success evidence.</summary>
internal sealed class VirtualPlcFixture : IAsyncDisposable
{
    private readonly VirtualPlcEngine engine;
    private readonly ModbusTcpServer server;
    private readonly PlcDataStore store;
    public int Port { get; }
    private VirtualPlcFixture(VirtualPlcEngine engine, ModbusTcpServer server, PlcDataStore store, int port)
    { this.engine=engine; this.server=server; this.store=store; Port=port; }

    public static async Task<VirtualPlcFixture> CreateAsync(bool currentRecipe=false,
        string profile="loop")
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        // The former 30 ms target dwell was shorter than the formal 50 ms poll
        // and could hide an entire fresh transfer observation (regression269).
        // Give the actual simulator an observable 200 ms dwell; no Host budget,
        // current-cycle/position assertion, production timing or PLC feedback is changed.
        var settings = profile switch {
            "signalr" => new SimulationOptions(),
            "final-unload" => new SimulationOptions {
                ScanPeriodMs=3, HeartbeatPeriodMs=100, HeartbeatTimeoutMs=3000,
                // The old 40 ms shortcut can finish between the formal polling
                // batches and hides Executing. Use the existing 200 ms legacy
                // loop profile; keep all Host/business deadlines unchanged.
                MotionDurationMs=200, FlipDurationMs=30,
                SortingDurationMs=200, ActionDurationJitterMs=0, InterActionGapMs=10 },
            "runtime-log" or "loop" => new SimulationOptions {
                ScanPeriodMs=profile=="runtime-log" ? 5 : 20, HeartbeatPeriodMs=1000, HeartbeatTimeoutMs=3000,
                MotionDurationMs=currentRecipe ? 3000 : 200, FlipDurationMs=30,
                SortingDurationMs=200, ActionDurationJitterMs=0, InterActionGapMs=10 },
            _ => throw new ArgumentException("Unknown existing fixture profile",nameof(profile))
        };
        var simulation=Options.Create(settings);
        var store=new PlcDataStore(simulation);
        var engine=new VirtualPlcEngine(store,simulation,NullLogger<VirtualPlcEngine>.Instance);
        var server=new ModbusTcpServer(store,Options.Create(new ModbusOptions {
            ListenAddress="127.0.0.1",Port=port,UnitId=1 }),NullLogger<ModbusTcpServer>.Instance);
        var fixture=new VirtualPlcFixture(engine,server,store,port);
        try {
            await engine.StartAsync(default); await server.StartAsync(default);
            await fixture.WaitForResponseAsync();
            return fixture;
        } catch { await fixture.DisposeAsync(); throw; }
    }
    public void InjectFault(string fault)=>engine.InjectFault(Enum.Parse<SimulationFault>(fault,true));
    public void ClearControlledPhysicalFaultsForTest()=>engine.ResetSimulation();
    public async Task SaveDiagnosticsAsync(string folder) {
        await File.WriteAllTextAsync(Path.Combine(folder,"plc-write-audit.json"),JsonSerializer.Serialize(store.GetWriteAudit()));
        await File.WriteAllTextAsync(Path.Combine(folder,"plc-action-audit.json"),JsonSerializer.Serialize(engine.GetActionAudit()));
    }
    private async Task WaitForResponseAsync() {
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while(true) {
            try {
                using var socket=new TcpClient(); await socket.ConnectAsync(IPAddress.Loopback,Port,deadline.Token);
                var stream=socket.GetStream(); byte[] request=[0,1,0,0,0,6,1,1,0,0,0,1];
                await stream.WriteAsync(request,deadline.Token);
                var header=new byte[7]; await stream.ReadExactlyAsync(header,deadline.Token);
                var body=new byte[(header[4]<<8|header[5])-1]; await stream.ReadExactlyAsync(body,deadline.Token);
                if(body.Length==3&&body[0]==1&&body[1]==1)return;
            } catch(Exception error) when(error is SocketException or IOException) {}
            await Task.Delay(5,deadline.Token);
        }
    }
    public async ValueTask DisposeAsync() {
        await server.StopAsync(default); await engine.StopAsync(default); server.Dispose(); engine.Dispose();
    }
}
