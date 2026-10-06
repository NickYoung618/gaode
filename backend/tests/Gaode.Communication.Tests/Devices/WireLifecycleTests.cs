using System.Net;
using System.Net.Sockets;
using Gaode.Infrastructure.Devices.Plc;
using Gaode.Plc.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VirtualPlc;
using Xunit;

namespace Gaode.Communication.Tests.Devices;

// Migrated T15 real TCP wire sequence; numeric expectations remain independent oracle literals.
// This device-side sequence is additional evidence, not a substitute for formal Host port acceptance.
[Collection("CommunicationTcp")]
public sealed class WireLifecycleTests
{
    [Fact]
    public async Task TcpProtocolExposesHeartbeatClampMoveSortUnloadUnlockAndFailureFacts()
    {
        var port = FreePort();
        var simulation = new SimulationOptions
        {
            ScanPeriodMs = 2, HeartbeatPeriodMs = 20, HeartbeatTimeoutMs = 3000,
            MotionDurationMs = 12, SortingDurationMs = 12, InterActionGapMs = 4, ActionDurationJitterMs = 0,
            RandomSeed = 1
        };
        var store = new PlcDataStore(Options.Create(simulation));
        var engine = new VirtualPlcEngine(store, Options.Create(simulation),
            NullLogger<VirtualPlcEngine>.Instance);
        var server = new ModbusTcpServer(store, Options.Create(new ModbusOptions
        {
            ListenAddress = IPAddress.Loopback.ToString(), Port = port, UnitId = 1
        }), NullLogger<ModbusTcpServer>.Instance);
        await engine.StartAsync(default);
        await server.StartAsync(default);
        await WaitForPortAsync(port);
        await using var wire = new ModbusTcpClient("127.0.0.1", port, 1, TimeSpan.FromSeconds(1));
        await using var heartbeat = new ModbusTcpClient("127.0.0.1", port, 1, TimeSpan.FromSeconds(1));
        using var stop = new CancellationTokenSource();
        var echo = EchoHeartbeatAsync(heartbeat, stop.Token);
        try
        {
            await ChangedAsync(wire, O(0x0001));
            await wire.WriteCoilAsync(O(0x0003), true);

            await wire.WriteCoilAsync(O(0x0009), true);
            await RegisterAsync(wire, 0x0024, 1);
            await wire.WriteCoilAsync(O(0x0009), false);

            await wire.WriteRegistersAsync(O(0x0025), [15, 15]);
            await wire.WriteRegisterAsync(O(0x0027), 1);
            await RegisterAsync(wire, 0x0028, 1);

            // Command 4 follows the completed inspection/Z-reset handshake and
            // performs a fresh XYZ move using Grab_Target_Z.
            await WriteFloatAsync(wire, 0x0003, 11);
            await WriteFloatAsync(wire, 0x0005, 22);
            await WriteFloatAsync(wire, 0x0007, 110);
            await wire.WriteRegisterAsync(O(0x0001), 2);
            await RegisterAsync(wire, 0x0002, 1);
            await wire.WriteRegisterAsync(O(0x0001), 0);
            await wire.WriteRegisterAsync(O(0x0052), 1);
            await wire.WriteRegisterAsync(O(0x0052), 2);
            await RegisterAsync(wire, 0x0053, 2);
            await wire.WriteRegisterAsync(O(0x0052), 0);
            await WriteFloatAsync(wire, 0x0003, 11);
            await WriteFloatAsync(wire, 0x0005, 22);
            await WriteFloatAsync(wire, 0x000B, 150);
            await wire.WriteRegisterAsync(O(0x0001), 4);
            await WaitAsync(async () => {
                var words = await wire.ReadRegistersAsync(O(0x0011), 2);
                return Float32Codec.Decode(words[0], words[1],
                    Float32ByteOrder.Abcd) == 150f;
            });
            await RegisterAsync(wire, 0x0002, 1);
            var actualZ = await wire.ReadRegistersAsync(O(0x0011), 2);
            Assert.Equal(150f, Float32Codec.Decode(actualZ[0], actualZ[1],
                Float32ByteOrder.Abcd));
            await wire.WriteRegisterAsync(O(0x0001), 0);
            await RegisterAsync(wire, 0x0002, 1);

            await WriteFloatAsync(wire, 0x0003, 11);
            await WriteFloatAsync(wire, 0x0005, 22);
            await WriteFloatAsync(wire, 0x000B, 150);
            await wire.WriteRegisterAsync(O(0x0021), 1);
            await RegisterAsync(wire, 0x0022, 2);
            await wire.WriteRegisterAsync(O(0x0021), 0);
            await Task.Delay(40);
            await wire.WriteRegisterAsync(O(0x0020), 3);
            await WriteFloatAsync(wire, 0x0003, 400);
            await WriteFloatAsync(wire, 0x0005, 300);
            await WriteFloatAsync(wire, 0x000B, 150);
            await wire.WriteRegisterAsync(O(0x0021), 2);
            await RegisterAsync(wire, 0x0022, 3);
            await wire.WriteRegisterAsync(O(0x0021), 0);
            await wire.WriteRegisterAsync(O(0x0054), 1);
            await RegisterAsync(wire, 0x0022, 0);
            await wire.WriteRegisterAsync(O(0x0054), 0);

            await wire.WriteRegisterAsync(O(0x0023), 0);
            await RegisterAsync(wire, 0x0024, 0);

            await wire.WriteCoilAsync(O(0x0009), true);
            await RegisterAsync(wire, 0x0024, 1);
            await wire.WriteCoilAsync(O(0x0009), false);
            engine.InjectFault(SimulationFault.SortingFailure);
            await WriteFloatAsync(wire, 0x0003, 11);
            await WriteFloatAsync(wire, 0x0005, 22);
            await WriteFloatAsync(wire, 0x000B, 150);
            await wire.WriteRegisterAsync(O(0x0021), 1);
            await RegisterAsync(wire, 0x0022, 4);

            // A TCP session ending never manufactures a stage/business completion. A fresh
            // Modbus session observes the last PLC-owned physical facts verbatim.
            await using (var disconnected = new ModbusTcpClient(
                "127.0.0.1", port, 1, TimeSpan.FromSeconds(1)))
                _ = await disconnected.ReadRegistersAsync(O(0x0022), 1);
            await using var reconnected = new ModbusTcpClient("127.0.0.1", port, 1, TimeSpan.FromSeconds(1));
            Assert.Equal((ushort)1, (await reconnected.ReadRegistersAsync(
                O(0x0024), 1))[0]);
            Assert.Equal((ushort)4, (await reconnected.ReadRegistersAsync(
                O(0x0022), 1))[0]);
            await reconnected.WriteRegisterAsync(O(0x0021), 0);
        }
        finally
        {
            stop.Cancel();
            try { await echo; } catch (OperationCanceledException) { }
            await server.StopAsync(default);
            await engine.StopAsync(default);
            server.Dispose();
            engine.Dispose();
        }
    }

    private static async Task EchoHeartbeatAsync(ModbusTcpClient wire, CancellationToken ct)
    {
        bool? last = null;
        while (!ct.IsCancellationRequested)
        {
            var value = (await wire.ReadCoilsAsync(O(0x0001), 1, ct))[0];
            if (value != last)
            {
                await wire.WriteCoilAsync(O(0x0002), value, ct);
                last = value;
            }
            await Task.Delay(4, ct);
        }
    }

    private static async Task ChangedAsync(ModbusTcpClient wire, ushort offset)
    {
        var first = (await wire.ReadCoilsAsync(offset, 1))[0];
        await WaitAsync(async () => (await wire.ReadCoilsAsync(offset, 1))[0] != first);
    }

    private static async Task RegisterAsync(ModbusTcpClient wire, ushort point, ushort expected) =>
        await WaitAsync(async () => (await wire.ReadRegistersAsync(O(point), 1))[0] == expected);

    private static async Task WaitAsync(Func<Task<bool>> condition)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!await condition()) await Task.Delay(4, deadline.Token);
    }

    private static Task WriteFloatAsync(ModbusTcpClient wire, ushort point, float value) =>
        wire.WriteRegistersAsync(O(point), Float32Codec.Encode(value,
            Float32ByteOrder.Abcd));

    private static ushort O(ushort point) => checked((ushort)(point - 1));

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
