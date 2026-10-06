using Gaode.Plc.Protocol;
using Gaode.Domain.Station01;
using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using System.Diagnostics;
using Gaode.Application.Ports;
using Gaode.Domain.Configuration;
using Gaode.Infrastructure.Devices.Plc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VirtualPlc;
using Xunit;
using Xunit.Abstractions;

namespace Gaode.Communication.Tests.Devices;

[Collection("CommunicationTcp")]
public sealed class T065OriginalDeadlineTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealOriginalDeadlineStillLatchesAndRejectsNewMotion(bool heartbeatFault)
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start(); var serverPort = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
        var options = Options.Create(new SimulationOptions { ScanPeriodMs = 20, HeartbeatPeriodMs = 1000, HeartbeatTimeoutMs = 3000 });
        var store = new PlcDataStore(options);
        using var engine = new VirtualPlcEngine(store, options, NullLogger<VirtualPlcEngine>.Instance);
        using var server = new ModbusTcpServer(store, Options.Create(new ModbusOptions { ListenAddress = "127.0.0.1", Port = serverPort }), NullLogger<ModbusTcpServer>.Instance);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var proxy = new ResponseGate(serverPort, deadline.Token);
        var log = new CaptureLog();
        var faultApplied = false;
        await engine.StartAsync(default); await server.StartAsync(default);
        await using var device = new LatestProtocolPlcDevice(new PlcRuntimeOptions { Host = "127.0.0.1", Port = proxy.Port, Provider = "Virtual", IoTimeoutMs = 1000, HeartbeatTimeoutMs = 3000 }, 0.01, log);
        try
        {
            log.ReadObservation = device.Observe;
            await device.StartAsync(deadline.Token);
            while (device.HeartbeatEdges < 1) await Task.Delay(20, deadline.Token);
            var initialEpoch = device.Observe().ConnectionEpoch;
            var started = Stopwatch.GetTimestamp();
            if (heartbeatFault) engine.InjectFault(SimulationFault.PauseHeartbeat);
            else proxy.DropResponses = true; // Business response only; independent heartbeat must not win this I/O case.
            faultApplied = true;
            while (device.Failure is null || log.FailureObservations.IsEmpty) await Task.Delay(20, deadline.Token);
            var atLogEntry = Assert.Single(log.FailureObservations);
            Assert.True(atLogEntry.ConnectionEpoch > initialEpoch);
            Assert.False(atLogEntry.HasReliableObservation);
            Assert.Contains(heartbeatFault ? "PlcHeartbeatLost" : "PlcCommunicationUnknown", atLogEntry.ReasonCodes);
            var observed = device.Observe();
            Assert.False(observed.HasReliableObservation); Assert.False((observed.SafetyAssessment == SafetyAssessment.Clear));
            Assert.True(observed.ConnectionEpoch > initialEpoch);
            Assert.Contains(heartbeatFault ? "PlcHeartbeatLost" : "PlcCommunicationUnknown", observed.ReasonCodes);
            Assert.Contains(log.Messages, s => s.Contains("New physical actions are blocked; no automatic resend."));
            if (!heartbeatFault) Assert.Contains(log.Messages, s => s.Contains("Modbus I/O deadline exceeded") || s.Contains("PlcConnectionQueueOrExchangeDeadline"));
            var tick = Stopwatch.GetTimestamp();
            var envelope = new PortEnvelope(Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(), "t065-original-deadline", "1", "Test", tick, tick + Stopwatch.Frequency * 5, "Stopwatch");
            var callbacks = 0;
            var move = new MoveRequest(envelope, Guid.NewGuid(), new FixedPoint("t065-rejected", "1", 0, 0, "mm", "Test", 0), Guid.NewGuid(), "virtual-plc", "3D");
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => device.RequestMoveAsync(move, _ => callbacks++, deadline.Token).AsTask());
            Assert.Equal("PlcUnavailableOrActionInFlight", error.Message); Assert.Equal(0, callbacks);
            if (Environment.GetEnvironmentVariable("GAODE_009_EVIDENCE_ROOT") is { } proofRoot)
            {
                Directory.CreateDirectory(proofRoot);
                File.WriteAllText(Path.Combine(proofRoot, heartbeatFault ? "I-HB-04-publication.json" : "I-HB-03-publication.json"),
                    System.Text.Json.JsonSerializer.Serialize(new { initialEpoch, failure = device.Failure,
                        logEntryEpoch = atLogEntry.ConnectionEpoch, observedEpoch = observed.ConnectionEpoch,
                        atLogEntry.HasReliableObservation, atLogEntry.ReasonCodes, newMotionRejected = true,
                        elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds, ioMs = 1000, heartbeatMs = 3000 }));
            }
            output.WriteLine("{0}: runtime={1}; nativeConfigured={2}; elapsedMs={3:F3}; io=1000; heartbeat=3000; acquisition=013; epoch={4}->{5}; diagnostic={6}; newMotionRejected=true", heartbeatFault ? "Heartbeat" : "IO", Environment.Version, typeof(ThreadPool).GetProperty("UseWindowsThreadPool", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)?.GetValue(null), Stopwatch.GetElapsedTime(started).TotalMilliseconds, initialEpoch, observed.ConnectionEpoch, string.Join(",", observed.ReasonCodes));
        }
        finally
        {
            var diagnostic = System.Text.Json.JsonSerializer.Serialize(new
            {
                heartbeatFault, faultApplied, device.HeartbeatEdges, device.Failure,
                observed = device.Observe(), capturedAtUtc = DateTimeOffset.UtcNow,
                messages = log.Messages.TakeLast(64).ToArray()
            });
            output.WriteLine(diagnostic);
            if (Environment.GetEnvironmentVariable("GAODE_009_EVIDENCE_ROOT") is { } diagnosticRoot)
            {
                Directory.CreateDirectory(diagnosticRoot);
                File.WriteAllText(Path.Combine(diagnosticRoot, heartbeatFault ? "I-HB-04-diagnostic.json" : "I-HB-03-diagnostic.json"), diagnostic);
            }
            deadline.Cancel(); proxy.Dispose(); await server.StopAsync(default); await engine.StopAsync(default);
        }
    }

    private sealed class ResponseGate : IDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly ConcurrentBag<TcpClient> sockets = new();
        public volatile bool DropResponses;
        public int Port { get; }
        public ResponseGate(int target, CancellationToken token)
        {
            listener.Start(); Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            _ = Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        var incoming = await listener.AcceptTcpClientAsync(token); sockets.Add(incoming);
                        _ = Task.Run(async () =>
                        {
                            using var upstream = new TcpClient(); sockets.Add(upstream);
                            try
                            {
                                await upstream.ConnectAsync(IPAddress.Loopback, target, token);
                                await Relay(incoming.GetStream(), upstream.GetStream());
                            }
                            catch (Exception e) when (e is IOException or OperationCanceledException or SocketException or ObjectDisposedException) { }
                            finally { incoming.Dispose(); }
                        }, token);
                    }
                }
                catch (Exception e) when (e is OperationCanceledException or SocketException or ObjectDisposedException) { }
            }, token);
            async Task Relay(NetworkStream from, NetworkStream to)
            {
                while (!token.IsCancellationRequested)
                {
                    var request = await Frame(from);
                    await to.WriteAsync(request, token);
                    var response = await Frame(to);
                    var offset = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(8, 2));
                    var heartbeat = request[7] == 1 && offset == 0 || request[7] == 5 && offset == 1;
                    if (!DropResponses || heartbeat) await from.WriteAsync(response, token);
                }
            }
            async Task<byte[]> Frame(NetworkStream stream)
            {
                var header = new byte[7]; await stream.ReadExactlyAsync(header, token);
                var frame = new byte[6 + System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2))];
                header.CopyTo(frame, 0); await stream.ReadExactlyAsync(frame.AsMemory(7), token); return frame;
            }
        }
        public void Dispose() { listener.Stop(); foreach (var socket in sockets) socket.Dispose(); }
    }
    private sealed class CaptureLog : ILogger<LatestProtocolPlcDevice>
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public Func<DeviceObservation>? ReadObservation { get; set; }
        public ConcurrentQueue<DeviceObservation> FailureObservations { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, error);
            if (message.Contains("New physical actions are blocked; no automatic resend.") && ReadObservation is { } observe)
            {
                var atEntry = observe();
                Messages.Enqueue(message);
                FailureObservations.Enqueue(atEntry);
                return;
            }
            Messages.Enqueue(message);
        }
    }
}

