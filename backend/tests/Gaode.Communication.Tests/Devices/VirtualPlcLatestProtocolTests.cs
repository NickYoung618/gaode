using Gaode.Plc.Protocol;
using Gaode.Application.Ports;
using Gaode.Application.Timing;
using Gaode.Domain.Station01;
using System.Net;
using System.Net.Sockets;
using Gaode.Domain.Configuration;
using Gaode.Infrastructure.Devices.Plc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VirtualPlc;
using Xunit;

namespace Gaode.Communication.Tests.Devices;

[Collection("CommunicationTcp")]
public sealed class VirtualPlcLatestProtocolTests
{
    [Fact]
    public async Task GripperReuseChangeInvalidationAndRotationUseTheExistingTcpPump()
    {
        var port=FreePort();
        var simulation=new SimulationOptions {ScanPeriodMs=2,HeartbeatPeriodMs=20,HeartbeatTimeoutMs=3000,
            MotionDurationMs=150,ActionDurationJitterMs=0};
        var store=new PlcDataStore(Options.Create(simulation));
        using var engine=new VirtualPlcEngine(store,Options.Create(simulation),NullLogger<VirtualPlcEngine>.Instance);
        using var server=new ModbusTcpServer(store,Options.Create(new ModbusOptions {ListenAddress="127.0.0.1",Port=port,UnitId=1}),NullLogger<ModbusTcpServer>.Instance);
        await engine.StartAsync(default);await server.StartAsync(default);await WaitForPortAsync(port);
        var basis=new RotationExecutionBasis(.01,"Test","014 communication component: explicit virtual R basis");
        await using var device=new LatestProtocolPlcDevice(new PlcRuntimeOptions {Provider="Virtual",Host="127.0.0.1",Port=port,UnitId=1,
            IoTimeoutMs=1000,HeartbeatTimeoutMs=3000,RotationBasis=basis},.01);
        try
        {
            await device.StartAsync(default);await device.ResetAsync(default);
            await WaitAsync(()=>Task.FromResult(device.Observe().HasReliableObservation));
            PlcStageActionRequest Request() {var now=DateTimeOffset.UtcNow;return new(new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),1,
                Guid.NewGuid(),device.Observe().ConnectionEpoch,"component-snapshot","component-plan",Guid.NewGuid()),Guid.NewGuid(),Guid.NewGuid(),
                PlcWorkflowStage.Sorting,"component-parameters",ActionWindows.FromUtc(TimeProvider.System,now,now.AddSeconds(2),"component-clock"),
                Guid.NewGuid().ToString("N"),TargetPurpose:"Test");}
            int Writes()=>store.GetWriteAudit().Count(w=>w.Accepted&&w.DocumentNumber==store.Definition[SignalId.GrabId].DocumentNumber);
            // No selection exists yet: the new validity feedback must not add a startup B read.
            var activeOffset=store.Definition[SignalId.GrabActiveId].DocumentNumber-1;
            Assert.DoesNotContain(device.BusinessExchanges,exchange=> {
                var bytes=Convert.FromHexString(exchange.Request);
                if(bytes.Length<12||bytes[7]!=3)return false;
                var offset=System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(8,2));
                var count=System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(10,2));
                return offset<=activeOffset&&activeOffset<offset+count;
            });
            await device.SelectStageGripperAsync(Request(),1,default);var first=Writes();
            await device.SelectStageGripperAsync(Request(),1,default);Assert.Equal(first,Writes());
            await device.SelectStageGripperAsync(Request(),2,default);Assert.Equal(first+1,Writes());
            store.SetHoldingRegisterFromPlc(store.Definition[SignalId.GrabActiveId].DocumentNumber,0);
            await Task.Delay(150);await device.SelectStageGripperAsync(Request(),2,default);Assert.Equal(first+2,Writes());
            var rotation=Request() with {Stage=PlcWorkflowStage.Rotate,RotationTarget=new(90,"stage:1",basis.AngleToleranceDeg,basis.SourceReference)};
            await device.BeginStageActionAsync(rotation,rotation.Correlation.ActionId,default);
            var reached=await device.RotateStageAsync(rotation,default);device.EndStageAction();Assert.True(reached.Angle.Matched);Assert.Equal(90,reached.Angle.ActualAngleDeg);
            Assert.Equal(first+2,Writes()); // Rotation never re-selects a gripper.
            var previousEpoch=device.Observe().ConnectionEpoch;var beforeReset=Writes();
            await device.ResetAsync(default);Assert.True(device.Observe().ConnectionEpoch>previousEpoch);
            await device.SelectStageGripperAsync(Request(),2,default);Assert.Equal(beforeReset+1,Writes());
            // Controlled negative component input after the real simulator handler;
            // never used by the joint representative or as a successful producer.
            void WrongSelection(PcWriteEvent write)
            {
                if(write.Area==PlcArea.HoldingRegister&&write.DocumentNumber==store.Definition[SignalId.GrabId].DocumentNumber)
                    store.SetHoldingRegisterFromPlc(store.Definition[SignalId.GrabActiveId].DocumentNumber,3);
            }
            store.PcValueWritten+=WrongSelection;
            try
            {
                var error=await Assert.ThrowsAsync<IOException>(()=>device.SelectStageGripperAsync(Request(),1,default));
                Assert.Equal("GripperSelectionFeedbackMismatch",error.Message);
            }
            finally {store.PcValueWritten-=WrongSelection;}

        }
        catch(Exception error) {throw new IOException(error.Message+"; actual device failure="+device.Failure+"; observation="+System.Text.Json.JsonSerializer.Serialize(device.Observe()),error);}
        finally {await engine.StopAsync(default);server.Dispose();}
    }

    [Fact]
    public async Task FormalHostPortObservesConnectionEpochChangeWhenTcpServiceDisconnects()
    {
        var port = FreePort();
        var simulation = new SimulationOptions
        {
            ScanPeriodMs = 2, HeartbeatPeriodMs = 20, HeartbeatTimeoutMs = 3000,
            MotionDurationMs = 10, SortingDurationMs = 10, InterActionGapMs = 2, ActionDurationJitterMs = 0
        };
        var store = new PlcDataStore(Options.Create(simulation));
        var engine = new VirtualPlcEngine(store, Options.Create(simulation),
            NullLogger<VirtualPlcEngine>.Instance);
        var server = new ModbusTcpServer(store, Options.Create(new ModbusOptions
        {
            ListenAddress = "127.0.0.1", Port = port, UnitId = 1
        }), NullLogger<ModbusTcpServer>.Instance);
        await engine.StartAsync(default);
        await server.StartAsync(default);
        await WaitForPortAsync(port);
        await using var device = new LatestProtocolPlcDevice(new PlcRuntimeOptions
        {
            Provider = "Virtual", Host = "127.0.0.1", Port = port, UnitId = 1,
            IoTimeoutMs = 100, HeartbeatTimeoutMs = 500
        }, 0.01);
        try
        {
            await device.StartAsync(default);
            await device.ResetAsync(default);
            await WaitAsync(() => Task.FromResult(device.Observe().HasReliableObservation));
            var connectedEpoch = device.Observe().ConnectionEpoch;

            await server.StopAsync(default);
            await WaitAsync(() => Task.FromResult(
                !device.Observe().HasReliableObservation && device.Observe().ConnectionEpoch > connectedEpoch));
            Assert.False(string.IsNullOrWhiteSpace(device.Failure));
            var firstFailure=device.Failure;
            var stopEvents=new List<DeviceEvent>();
            var stopStarted=TimeProvider.System.GetTimestamp();
            var stopEnvelope=new PortEnvelope(Guid.NewGuid(),Guid.NewGuid(),1,Guid.NewGuid(),"stop-failure-component","component-1","Test",stopStarted,stopStarted+2*TimeProvider.System.TimestampFrequency,"component-clock");
            Assert.True(stopEnvelope.IsValid);
            await device.RequestStopAsync(stopEnvelope,stopEvents.Add,default);
            await Task.Delay(150);
            Assert.Equal(firstFailure,device.Failure);Assert.False(device.Observe().HasReliableObservation);
            Assert.Empty(stopEvents); // Neither stop acknowledgement nor workflow completion is invented.
            await device.DisposeAsync(); // Failed transport stop must not fault the owned lifecycle task.
        }
        finally
        {
            await engine.StopAsync(default);
            server.Dispose();
            engine.Dispose();
        }
    }

    [Fact]
    public async Task HeartbeatFailureKeepsFirstCodeAndLastReliableObservationWhileBusinessPollContinues()
    {
        var port = FreePort();
        var simulation = new SimulationOptions
        {
            ScanPeriodMs = 2, HeartbeatPeriodMs = 20, HeartbeatTimeoutMs = 3000
        };
        var store = new PlcDataStore(Options.Create(simulation));
        var engine = new VirtualPlcEngine(store, Options.Create(simulation),
            NullLogger<VirtualPlcEngine>.Instance);
        var server = new ModbusTcpServer(store, Options.Create(new ModbusOptions
        {
            ListenAddress = "127.0.0.1", Port = port, UnitId = 1
        }), NullLogger<ModbusTcpServer>.Instance);
        await engine.StartAsync(default);
        await server.StartAsync(default);
        await WaitForPortAsync(port);
        await using var device = new LatestProtocolPlcDevice(new PlcRuntimeOptions
        {
            Provider = "Virtual", Host = "127.0.0.1", Port = port, UnitId = 1,
            IoTimeoutMs = 100, HeartbeatTimeoutMs = 500
        }, 0.01);
        try
        {
            await device.StartAsync(default);
            await WaitAsync(() => Task.FromResult(device.Observe().HasReliableObservation));
            engine.InjectFault(SimulationFault.PauseHeartbeat);
            await WaitAsync(() => Task.FromResult(device.Observe().ReasonCodes.Contains("PlcHeartbeatLost")));
            var first = device.Observe();
            await Task.Delay(100);
            var later = device.Observe();
            Assert.False(later.HasReliableObservation);
            Assert.Contains("PlcHeartbeatLost", later.ReasonCodes);
            Assert.Equal("HeartbeatStoppedChanging", device.Failure);
            Assert.Equal(first.Identity?.SampleEndedUtc, later.Identity?.SampleEndedUtc);
            Assert.True(later.ConnectionEpoch > 1);
        }
        finally
        {
            await engine.StopAsync(default);
            server.Dispose();
            engine.Dispose();
        }
    }

    [Fact]
    public async Task StaleObservationNeverReusesOldSafetyClearAsCurrent()
    {
        var port = FreePort();
        var simulation = new SimulationOptions { ScanPeriodMs = 2, HeartbeatPeriodMs = 20 };
        var store = new PlcDataStore(Options.Create(simulation));
        var engine = new VirtualPlcEngine(store, Options.Create(simulation),
            NullLogger<VirtualPlcEngine>.Instance);
        var server = new ModbusTcpServer(store, Options.Create(new ModbusOptions
        {
            ListenAddress = "127.0.0.1", Port = port, UnitId = 1
        }), NullLogger<ModbusTcpServer>.Instance);
        await engine.StartAsync(default);
        await server.StartAsync(default);
        await WaitForPortAsync(port);
        var device = new LatestProtocolPlcDevice(new PlcRuntimeOptions
        {
            Provider = "Virtual", Host = "127.0.0.1", Port = port, UnitId = 1,
            IoTimeoutMs = 100, HeartbeatTimeoutMs = 500
        }, 0.01);
        try
        {
            await device.StartAsync(default);
            await WaitAsync(() => Task.FromResult(device.Observe().HasReliableObservation && (device.Observe().SafetyAssessment == SafetyAssessment.Clear)));
            var lastReliable = device.Observe();
            await device.DisposeAsync(); // Ends polling without fabricating an unsafe PLC response.
            await Task.Delay(650);
            var stale = device.Observe();
            Assert.False(stale.HasReliableObservation);
            Assert.False((stale.SafetyAssessment == SafetyAssessment.Clear));
            Assert.Contains("ProtocolSampleStale", stale.ReasonCodes);
            Assert.Equal(DeviceReliability.Stale, stale.Reliability);
            Assert.Equal(lastReliable.Identity?.SampleEndedUtc, stale.Identity?.SampleEndedUtc);
        }
        finally
        {
            await device.DisposeAsync();
            await engine.StopAsync(default);
            server.Dispose();
            engine.Dispose();
        }
    }

    private static async Task WaitAsync(Func<Task<bool>> condition)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!await condition()) await Task.Delay(4, deadline.Token);
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task WaitForPortAsync(int port)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (true)
        {
            try
            {
                using var socket = new TcpClient();
                await socket.ConnectAsync(IPAddress.Loopback, port, deadline.Token);
                return;
            }
            catch (SocketException)
            {
                await Task.Delay(5, deadline.Token);
            }
        }
    }
}
