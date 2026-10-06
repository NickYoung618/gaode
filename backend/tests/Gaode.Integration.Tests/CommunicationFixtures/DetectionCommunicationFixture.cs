extern alias virtualplc;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Devices.Plc;
using Gaode.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VirtualPlcEngine = virtualplc::VirtualPlc.VirtualPlcEngine;
using ModbusTcpServer = virtualplc::VirtualPlc.ModbusTcpServer;
using PlcDataStore = virtualplc::VirtualPlc.PlcDataStore;
using SimulationOptions = virtualplc::VirtualPlc.SimulationOptions;
using ModbusOptions = virtualplc::VirtualPlc.ModbusOptions;
namespace Gaode.Integration.Tests.Support;

// Existing AB/CD component setup, extracted verbatim in behavior. All protocol
// setup, internal fault injection and raw exports stay on this communication side.
internal sealed class DetectionCommunicationFixture : IAsyncDisposable
{
    private readonly PlcDataStore store;
    private readonly VirtualPlcEngine engine;
    private readonly ModbusTcpServer server;
    private readonly LatestProtocolPlcDevice device;
    public IPlcStatePort State => device;
    public IPlcActionPort Action => device;
    public IMotionPort Motion => device;
    public IPhysicalHandlingPort Handling => device;
    public IPlcStageActionPort StageActions(BusinessDurations budget, IPickCommitPort commits) =>
        new LatestProtocolStageActionAdapter(device, budget, commits);
    public IAcquisitionCyclePort Acquisition { get; private set; }
    private DetectionCommunicationFixture(PlcDataStore store,VirtualPlcEngine engine,
        ModbusTcpServer server,LatestProtocolPlcDevice device)
    { this.store=store;this.engine=engine;this.server=server;this.device=device;Acquisition=device; }
    internal static async Task<DetectionCommunicationFixture> CreateAsync(TraceWriter writer,
        Guid storeId,int criticalSaveMs,Guid runId,bool releaseUnavailable)
    {
        using var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
        var port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
        var simulation=Options.Create(new SimulationOptions {
            ScanPeriodMs=2,HeartbeatPeriodMs=100,HeartbeatTimeoutMs=3000,MotionDurationMs=3000,
            InterActionGapMs=5,ActionDurationJitterMs=0,RandomSeed=61 });
        var store=new PlcDataStore(simulation);
        var engine=new VirtualPlcEngine(store,simulation,NullLogger<VirtualPlcEngine>.Instance);
        var server=new ModbusTcpServer(store,Options.Create(new ModbusOptions {
            ListenAddress=IPAddress.Loopback.ToString(),Port=port,UnitId=1 }),NullLogger<ModbusTcpServer>.Instance);
        var device=new LatestProtocolPlcDevice(new PlcRuntimeOptions {
            Provider="Virtual",Host="127.0.0.1",Port=port,UnitId=1,IoTimeoutMs=1000,
            HeartbeatTimeoutMs=3000 },0.01,
            recorder:new CommunicationEvidenceRecorder(writer,storeId,TimeProvider.System,criticalSaveMs));
        var fixture=new DetectionCommunicationFixture(store,engine,server,device);
        try {
            await engine.StartAsync(default);await server.StartAsync(default);
            await Until(()=> {try {using var client=new TcpClient();client.Connect(IPAddress.Loopback,port);return true;}catch{return false;}});
            await device.StartAsync(default);await device.ResetAsync(default);
            await Until(()=>device.Observe().HasReliableObservation&&device.Observe().SafetyAssessment==SafetyAssessment.Clear);
            var now=System.Diagnostics.Stopwatch.GetTimestamp();
            var envelope=new PortEnvelope(runId,Guid.NewGuid(),1,runId,"component-test","1.0","Test",now,
                now+System.Diagnostics.Stopwatch.Frequency*2,"component-system-clock");
            var started = new TaskCompletionSource<DeviceEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
            var actionId=Guid.NewGuid();
            var expectedEpoch=device.Observe().ConnectionEpoch;
            await device.RequestStartAsync(envelope,actionId,Guid.NewGuid(),e=> {
                if(e.ActionId!=actionId || !DeviceContract.Matches(e,envelope,expectedEpoch)) return;
                if(e.Kind==DeviceEventKind.Accepted) started.TrySetResult(e);
                if(e.Kind is DeviceEventKind.Failed or DeviceEventKind.UnknownHeld)
                    started.TrySetException(new InvalidOperationException("ComponentStartRejected:"+e.ErrorCode));
            },default);
            // The formal start contract releases write ownership at Accepted.
            // It does not emit Completed or claim physical clamp from that receipt.
            // Both this current receipt and the separate real ready observation are required.
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Until(()=>device.Observe().HasReliableObservation && device.Observe().Readiness==DeviceReadiness.Ready);
            if(releaseUnavailable) fixture.Acquisition = new DisconnectOnRelease(device, server);
            return fixture;
        } catch {await fixture.DisposeAsync();throw;}
    }
    // Communication-only fault: the actual owned TCP connection is lost at release.
    // Device code still determines the failure; no capture result is fabricated.
    private sealed class DisconnectOnRelease(LatestProtocolPlcDevice device, ModbusTcpServer server) : IAcquisitionCyclePort
    {
        public ValueTask<AcquisitionSession> OpenCaptureWindowAsync(CaptureWindowRequest request, CancellationToken token) =>
            device.OpenCaptureWindowAsync(request, token);
        public async Task<CaptureCycleResult> FinishCaptureWindowAsync(AcquisitionSession session, CaptureWorkCommit work,
            ActionWindow window, CancellationToken token)
        {
            await server.StopAsync(token);
            return await device.FinishCaptureWindowAsync(session, work, window, token);
        }
        public Task<CaptureCycleResult> CloseFailedCaptureWindowAsync(AcquisitionSession session, string reason,
            ActionWindow window, CancellationToken token) => device.CloseFailedCaptureWindowAsync(session, reason, window, token);
    }
    public async Task SaveDiagnosticsAsync(string root) {
        await File.WriteAllTextAsync(Path.Combine(root,"plc-write-audit.json"),JsonSerializer.Serialize(store.GetWriteAudit()));
        await File.WriteAllTextAsync(Path.Combine(root,"plc-action-audit.json"),JsonSerializer.Serialize(engine.GetActionAudit()));
        var changes=store.GetChanges(0,10000);
        await File.WriteAllTextAsync(Path.Combine(root,"component-wire.json"),JsonSerializer.Serialize(
            new {changes=changes.Changes,deviceFailure=device.Failure}));
    }
    private static async Task Until(Func<bool> predicate) {
        var until=DateTimeOffset.UtcNow.AddSeconds(5);
        while(DateTimeOffset.UtcNow<until) {if(predicate())return;await Task.Delay(10);}
        throw new TimeoutException("ComponentSetupTimedOut");
    }
    public async ValueTask DisposeAsync() {
        await device.DisposeAsync();await server.StopAsync(default);await engine.StopAsync(default);
        server.Dispose();engine.Dispose();
    }
}
