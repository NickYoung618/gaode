using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Gaode.Infrastructure.Devices.Plc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtualPlc;
using Xunit;

namespace Gaode.Communication.Tests.Devices;

[Collection("CommunicationTcp")]
public sealed class HeartbeatWindowDiagnosticsTests
{
    [Fact]
    public async Task BusinessSafetyAlarmAlsoSavesHeartbeatWindowsWithoutHeartbeatException()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        var options = Options.Create(new SimulationOptions { HeartbeatPeriodMs = 1000, HeartbeatTimeoutMs = 3000 });
        var store = new PlcDataStore(options);
        using var engine = new VirtualPlcEngine(store, options, new CaptureLog<VirtualPlcEngine>());
        using var server = new ModbusTcpServer(store, Options.Create(new ModbusOptions
            { ListenAddress = "127.0.0.1", Port = port }), new CaptureLog<ModbusTcpServer>());
        var log = new CaptureLog<LatestProtocolPlcDevice>();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await engine.StartAsync(default);
        await server.StartAsync(default);
        try
        {
            while (true)
            {
                using var probeClient = new TcpClient();
                try { await probeClient.ConnectAsync(IPAddress.Loopback, port, deadline.Token); break; }
                catch (SocketException) { await Task.Delay(20, deadline.Token); }
            }
            await using (var device = new LatestProtocolPlcDevice(new PlcRuntimeOptions
                { Host = "127.0.0.1", Port = port, Provider = "Virtual", IoTimeoutMs = 1000, HeartbeatTimeoutMs = 3000 }, 0.01, log))
            {
                await device.StartAsync(deadline.Token);
                await device.ResetAsync(deadline.Token);
                engine.InjectFault(SimulationFault.EmergencyAlarm);
                while (device.Failure is null) await Task.Delay(20, deadline.Token);
                Assert.Equal("SafetyInterlockLost", device.Failure);
            }
            var failures = Windows(log).Where(x => x.GetProperty("reason").GetString() ==
                "FailureLatched:action:SafetyInterlockLost").ToArray();
            Assert.Contains(failures, x => x.GetProperty("source").GetString() == "Host/heartbeat-loop");
            Assert.Contains(failures, x => x.GetProperty("source").GetString() == "Host/heartbeat-transport");
            Assert.DoesNotContain(log.Entries, line => line.Contains("HeartbeatStoppedChanging", StringComparison.Ordinal));
        }
        finally { await server.StopAsync(default); await engine.StopAsync(default); }
    }

    [Fact]
    public async Task SlowSuccessRetainsPriorExchangeAndFailureBypassesWindowRateLimit()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var server = Task.Run(async () =>
        {
            using var socket = await listener.AcceptTcpClientAsync(cleanup.Token);
            for (var i = 0; i < 3; i++)
            {
                var request = new byte[12];
                await socket.GetStream().ReadExactlyAsync(request, cleanup.Token);
                if (i == 1) await Task.Delay(350, cleanup.Token);
                if (i == 2)
                {
                    try { await Task.Delay(Timeout.Infinite, cleanup.Token); }
                    catch (OperationCanceledException) when (cleanup.IsCancellationRequested) { }
                    return;
                }
                var response = ReadResponse(request);
                await socket.GetStream().WriteAsync(response, cleanup.Token);
            }
        });
        var log = new CaptureLog<object>();
        try
        {
            await using (var client = new ModbusTcpClient("127.0.0.1", port, 1,
                TimeSpan.FromSeconds(1), log, "heartbeat"))
            {
                await client.ReadCoilsAsync(0, 1, cleanup.Token);
                await client.ReadCoilsAsync(0, 1, cleanup.Token);
                await Assert.ThrowsAsync<TimeoutException>(() => client.ReadCoilsAsync(0, 1, cleanup.Token));
                Assert.Equal(3, client.Exchanges.Count); // no automatic retry
            }
            var windows = Windows(log);
            var slow = Assert.Single(windows, x => x.GetProperty("reason").GetString() == "SlowExchange");
            Assert.Contains(slow.GetProperty("records").EnumerateArray(), x =>
                x.GetProperty("Facts").GetProperty("transaction").GetInt32() == 1);
            var failure = Assert.Single(windows, x => x.GetProperty("reason").GetString() == "ExchangeFailure");
            // Initial connect may itself be slow on a loaded machine. Even when that
            // consumes the rate-limited window, the later critical dump must retain #2.
            var transaction = Assert.Single(failure.GetProperty("records").EnumerateArray(), x =>
                x.GetProperty("Facts").GetProperty("transaction").GetInt32() == 2).GetProperty("Facts");
            Assert.Equal(2, transaction.GetProperty("transaction").GetInt32());
            Assert.True(transaction.GetProperty("responseHeaderMs").GetDouble() >= 250);
            Assert.True(transaction.GetProperty("headerReadTick").GetInt64() > transaction.GetProperty("requestWrittenTick").GetInt64());
            Assert.False(string.IsNullOrWhiteSpace(transaction.GetProperty("localEndpoint").GetString()));
            Assert.True(failure.GetProperty("critical").GetBoolean());
            var failed = failure.GetProperty("records").EnumerateArray().Last().GetProperty("Facts");
            Assert.Equal("ReadHeader", failed.GetProperty("phase").GetString());
            Assert.Equal(3, failed.GetProperty("transaction").GetInt32());
            Assert.Contains("CanceledException", failed.GetProperty("error").GetString());
            Assert.True(failure.GetProperty("runtime").TryGetProperty("totalGcPauseMs", out _));
        }
        finally { cleanup.Cancel(); listener.Stop(); await server; }
    }

    [Fact]
    public async Task WindowKeepsOnly256TransactionsAndReportsOverwrittenHistory()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var server = Task.Run(async () =>
        {
            using var socket = await listener.AcceptTcpClientAsync(cleanup.Token);
            for (var i = 0; i < 301; i++)
            {
                var request = new byte[12];
                await socket.GetStream().ReadExactlyAsync(request, cleanup.Token);
                if (i == 300)
                {
                    try { await Task.Delay(Timeout.Infinite, cleanup.Token); }
                    catch (OperationCanceledException) when (cleanup.IsCancellationRequested) { }
                    return;
                }
                await socket.GetStream().WriteAsync(ReadResponse(request), cleanup.Token);
            }
        });
        var log = new CaptureLog<object>();
        try
        {
            await using (var client = new ModbusTcpClient("127.0.0.1", port, 1,
                TimeSpan.FromSeconds(2), log, "heartbeat"))
            {
                for (var i = 0; i < 300; i++) await client.ReadCoilsAsync(0, 1, cleanup.Token);
                await Assert.ThrowsAsync<TimeoutException>(() => client.ReadCoilsAsync(0, 1, cleanup.Token));
            }
            var window = Windows(log).Last(x => x.GetProperty("reason").GetString() == "ExchangeFailure");
            Assert.Equal(256, window.GetProperty("records").GetArrayLength());
            Assert.Equal(301, window.GetProperty("totalRecords").GetInt64());
            Assert.Equal(45, window.GetProperty("overwrittenRecords").GetInt64());
        }
        finally { cleanup.Cancel(); listener.Stop(); await server; }
    }

    [Fact]
    public async Task PlcWindowRetainsFastResponsesAndDistinguishesInvalidEchoFromAcceptedWrite()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        var options = Options.Create(new SimulationOptions { HeartbeatPeriodMs = 1000, HeartbeatTimeoutMs = 3000 });
        var log = new CaptureLog<VirtualPlcEngine>();
        var serverLog = new CaptureLog<ModbusTcpServer>();
        var store = new PlcDataStore(options);
        using var engine = new VirtualPlcEngine(store, options, log);
        using var server = new ModbusTcpServer(store, Options.Create(new ModbusOptions
            { ListenAddress = "127.0.0.1", Port = port }), serverLog);
        await engine.StartAsync(default);
        await server.StartAsync(default);
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            while (true)
            {
                using var probeClient = new TcpClient();
                try { await probeClient.ConnectAsync(IPAddress.Loopback, port, deadline.Token); break; }
                catch (SocketException) { await Task.Delay(20, deadline.Token); }
            }
            // Freeze the request bit only in this isolated fixture, never in the running station.
            engine.InjectFault(SimulationFault.PauseHeartbeat);
            await using var client = new ModbusTcpClient("127.0.0.1", port, 1, TimeSpan.FromSeconds(2));
            var bit = (await client.ReadCoilsAsync(0, 1, deadline.Token))[0];
            // Actual confirmed 300ms reads, with the original simulator timing untouched.
            for (var i = 0; i < 2; i++)
            {
                await Task.Delay(300, deadline.Token);
                Assert.Equal(bit, (await client.ReadCoilsAsync(0, 1, deadline.Token))[0]);
            }
            await client.WriteCoilAsync(1, !bit, deadline.Token); // accepted Modbus write, invalid heartbeat echo
        }
        finally { await server.StopAsync(default); await engine.StopAsync(default); }
        var window = Assert.Single(Windows(log), x => x.GetProperty("reason").GetString() == "EchoBitMismatch");
        var records = window.GetProperty("records").EnumerateArray().ToArray();
        Assert.Equal(0, serverLog.WarningCount);
        Assert.DoesNotContain(Windows(log), w => w.GetProperty("reason").GetString() == "ModbusHeartbeatDelay");
        var reads = records.Where(x => x.GetProperty("Kind").GetString() == "modbus-response" &&
            x.GetProperty("Facts").GetProperty("function").GetByte() == 1).Select(x => x.GetProperty("Facts")).ToArray();
        Assert.Equal(3, reads.Length);
        Assert.All(reads.Skip(1), x => Assert.True(x.GetProperty("requestGapMs").GetDouble() >= 250));
        Assert.Contains(records, x => x.GetProperty("Kind").GetString() == "modbus-response" &&
            x.GetProperty("Facts").GetProperty("transaction").GetInt32() == 1);
        var echo = Assert.Single(records, x => x.GetProperty("Kind").GetString() == "echo-observed").GetProperty("Facts");
        Assert.False(echo.GetProperty("valid").GetBoolean());
        Assert.Equal(4, echo.GetProperty("TransactionId").GetInt32());
        SaveDiagnosticProof("normal-period-and-invalid-echo", new { window, serverLog.WarningCount });
    }

    [Fact]
    public async Task ActualDelayedServerResponseStillProducesSlowResponseDiagnostic()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0); probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
        var options = Options.Create(new SimulationOptions { HeartbeatPeriodMs = 1000, HeartbeatTimeoutMs = 3000 });
        var store = new PlcDataStore(options);
        using var engine = new VirtualPlcEngine(store, options, new CaptureLog<VirtualPlcEngine>());
        var log = new CaptureLog<ModbusTcpServer>();
        using var server = new ModbusTcpServer(store, Options.Create(new ModbusOptions { ListenAddress = "127.0.0.1", Port = port }), log);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await server.StartAsync(deadline.Token);
        try
        {
            while (true)
            {
                using var ready = new TcpClient();
                try { await ready.ConnectAsync(IPAddress.Loopback, port, deadline.Token); break; }
                catch (SocketException) { await Task.Delay(20, deadline.Token); }
            }
            Assert.True(engine.InjectFault(SimulationFault.AxisResponseDelayed).Accepted);
            // Isolated raw transport diagnosis: existing finite 1500ms response fault,
            // no motion-duration changes and no change to the product's 1000ms I/O deadline.
            await using var client = new ModbusTcpClient("127.0.0.1", port, 1, TimeSpan.FromSeconds(2));
            await client.WriteCoilAsync(33, true, deadline.Token);
            await client.ReadRegistersAsync(127, 1, deadline.Token);
            await log.FirstWarning.Task.WaitAsync(deadline.Token);
            var fault = Assert.Single(store.GetFeedbackFaults());
            Assert.Equal("Delay", fault.Disposition); Assert.NotNull(fault.DelayElapsedAtUtc);
            var slow = Assert.Single(log.Events, e => e.Template.StartsWith("Slow Modbus response:", StringComparison.Ordinal));
            Assert.Equal(fault.TransactionId, Convert.ToUInt16(slow.Values["Transaction"]));
            // Task.Delay's configured value is not a QPC lower-bound assertion.
            // The actual finite fault must still exceed the original product I/O
            // deadline and produce the existing real slow-response diagnostic.
            Assert.True((fault.DelayElapsedAtUtc.Value - fault.AppliedAtUtc).TotalMilliseconds > 1000);
            Assert.True(Convert.ToDouble(slow.Values["ProcessingMs"]) >= 250);
            Assert.Equal(2, client.Exchanges.Count);
            Assert.Equal(fault.ProcessedResponseHex, client.Exchanges.Last().Response);
            SaveDiagnosticProof("actual-slow-response", new { fault,
                slow = new { slow.Level, slow.Template, transaction = slow.Values["Transaction"],
                    processingMs = slow.Values["ProcessingMs"], peer = slow.Values["Remote"]?.ToString(),
                    response = slow.Values["Response"] }, raw = client.Exchanges });
        }
        finally { await server.StopAsync(default); }
    }

    [Fact]
    public async Task ArbiterTimingSeparatesEligibilityWaitFromQueuedActualTcpWork()
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await using var plc = new ProtocolTcpFixture(150); await plc.StartAsync(stop.Token);
        await using var wire = plc.Client();
        var scheduled = new PlcScheduledTransport(wire, 1000);
        var first = new PlcExchangeClock { Queued = System.Diagnostics.Stopwatch.GetTimestamp() };
        var second = new PlcExchangeClock { Queued = System.Diagnostics.Stopwatch.GetTimestamp() };
        Task<ushort[]>? secondWork = null;
        PlcExchangeClock.Current.Value = first;
        PlcScheduledTransport.Eligibility.Value = () =>
        {
            var previousClock = PlcExchangeClock.Current.Value;
            var previousEligibility = PlcScheduledTransport.Eligibility.Value;
            try
            {
                PlcExchangeClock.Current.Value = second;
                PlcScheduledTransport.Eligibility.Value = null;
                secondWork = scheduled.ReadRegistersAsync(127, 1, stop.Token);
            }
            finally
            {
                PlcExchangeClock.Current.Value = previousClock;
                PlcScheduledTransport.Eligibility.Value = previousEligibility;
            }
            return true;
        };
        try
        {
            await scheduled.ReadCoilsAsync(0, 1, stop.Token);
            Assert.NotNull(secondWork);
            await secondWork;
            var a = Assert.IsType<PlcDispatchTiming>(first.Dispatch);
            var b = Assert.IsType<PlcDispatchTiming>(second.Dispatch);
            Assert.True(a.EligibilityStarted <= b.Enqueued && b.Enqueued <= a.EligibilityEnded);
            Assert.True(a.EligibilityPassed);
            Assert.True(first.Ended <= a.ExchangeResumed && a.ExchangeResumed <= a.CompletionSignaled);
            Assert.True(a.StepEnded <= b.QueueLockWaiting && b.QueueLockWaiting <= b.QueueLockAcquired);
            Assert.True(b.SelectStarted <= b.SelectEnded && b.SelectEnded <= b.EligibilityStarted);
            Assert.True(b.EligibilityEnded <= second.Granted && second.Granted <= second.Sent);
            Assert.Equal(a.RequestId, b.PreviousRequestId);
            Assert.Contains(b.Pending, p => p.RequestId == b.RequestId && !p.Cancelled);
            Assert.False(a.SnapshotTruncated || b.SnapshotTruncated);
            Assert.Equal(2, wire.Exchanges.Count); // Both requests reached the real TCP server.
            SaveDiagnosticProof("arbiter-stages", new { first = a, second = b, raw = wire.Exchanges });
        }
        finally { PlcExchangeClock.Current.Value = null; PlcScheduledTransport.Eligibility.Value = null; }
    }

    private static void SaveDiagnosticProof(string name, object value)
    {
        var root = Environment.GetEnvironmentVariable("GAODE_009_EVIDENCE_ROOT");
        if (root is null) return;
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "I-DIAG-" + name + ".json"), JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AbsoluteIoDeadlineRejectsExpiredWorkWithoutWaitingForCancellation(bool lateResponse)
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await using var plc = new ProtocolTcpFixture(150); await plc.StartAsync(stop.Token);
        await using var probe = new PlcPolling013WireProbe(plc.Port); probe.Start();
        await using var wire = new ModbusTcpClient("127.0.0.1", probe.Port, 1, TimeSpan.FromMilliseconds(1000));
        var clock = new ControlledMonotonicClock();
        var scheduled = new PlcScheduledTransport(wire, 1000, clock);
        await scheduled.ReadCoilsAsync(0, 1, stop.Token); // Same actual TCP path before the controlled clock fault.
        await ProtocolTcpFixture.UntilAsync(() => probe.Exchanges.Count == 1, stop.Token);
        if (lateResponse)
            probe.DelayBeforeForward = (_, _) => { clock.AdvancePastDeadline(); return 0; };
        else
            PlcScheduledTransport.Eligibility.Value = () => { clock.AdvancePastDeadline(); return true; };
        try
        {
            var error = await Assert.ThrowsAsync<TimeoutException>(() => scheduled.ReadCoilsAsync(0, 1, stop.Token));
            Assert.Equal("PlcConnectionQueueOrExchangeDeadline", error.Message);
            Assert.False(stop.IsCancellationRequested);
            if (lateResponse)
            {
                await ProtocolTcpFixture.UntilAsync(() => probe.Exchanges.Count == 2, stop.Token);
                Assert.NotNull(wire.Exchanges.Last().Error);
                probe.DelayBeforeForward = null;
                await Assert.ThrowsAsync<InvalidOperationException>(() => scheduled.ReadCoilsAsync(0, 1, stop.Token));
                Assert.Equal(2, probe.Exchanges.Count); // No automatic reconnect or blind resend after the late reply.
            }
            else
            {
                Assert.Single(wire.Exchanges);
                Assert.Single(probe.Exchanges); // Expiry in the eligibility callback did not issue a request.
                PlcScheduledTransport.Eligibility.Value = null;
                await scheduled.ReadCoilsAsync(0, 1, stop.Token);
                await ProtocolTcpFixture.UntilAsync(() => probe.Exchanges.Count == 2, stop.Token);
            }
            probe.Save(lateResponse ? "I-TIME-03" : "I-TIME-02");
            SaveDiagnosticProof(lateResponse ? "absolute-io-inflight" : "absolute-io-admission",
                new { controlledMonotonicClock = true, originalIoMilliseconds = 1000, lateResponse, error.Message, raw = wire.Exchanges });
        }
        finally { PlcScheduledTransport.Eligibility.Value = null; }
    }

    private sealed class ControlledMonotonicClock : TimeProvider
    {
        private long advance;
        public override long TimestampFrequency => global::System.Diagnostics.Stopwatch.Frequency;
        public override long GetTimestamp() => global::System.Diagnostics.Stopwatch.GetTimestamp() + Interlocked.Read(ref advance);
        internal void AdvancePastDeadline() => Interlocked.Add(ref advance, TimestampFrequency * 1001 / 1000);
    }

    private static byte[] ReadResponse(byte[] request)
    {
        var response = new byte[10];
        Array.Copy(request, response, 7);
        response[4] = 0; response[5] = 4;
        response[7] = 1; response[8] = 1; response[9] = 1;
        return response;
    }

    [Fact]
    public async Task ResponseCollectionPrecedesSlowDiagnosticButServiceEndIncludesIt()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var peer = Task.Run(async () =>
        {
            using var socket = await listener.AcceptTcpClientAsync(stop.Token);
            var request = new byte[12];
            await socket.GetStream().ReadExactlyAsync(request, stop.Token);
            await Task.Delay(300, stop.Token); // Existing slow-success diagnostic path, no device state extension.
            await socket.GetStream().WriteAsync(ReadResponse(request), stop.Token);
        });
        var log = new DelayedDiagnosticLog();
        var clock = new PlcExchangeClock { Queued = System.Diagnostics.Stopwatch.GetTimestamp() };
        PlcExchangeClock.Current.Value = clock;
        try
        {
            await using var client = new ModbusTcpClient("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port,
                1, TimeSpan.FromSeconds(1), log, "heartbeat");
            Assert.True((await client.ReadCoilsAsync(0, 1, stop.Token))[0]);
            Assert.True(log.Delayed);
            Assert.True(clock.Ended > clock.Sent);
            Assert.True(System.Diagnostics.Stopwatch.GetElapsedTime(clock.Ended, clock.ServiceEnded).TotalMilliseconds >= 80);
            Assert.Single(client.Exchanges); // Original evidence still recorded, no retry or discarded slow sample.
        }
        finally { PlcExchangeClock.Current.Value = null; listener.Stop(); await peer; }
    }

    private sealed class DelayedDiagnosticLog : ILogger
    {
        internal bool Delayed;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!formatter(state, exception).StartsWith("PLC heartbeat Modbus slow success:", StringComparison.Ordinal)) return;
            Thread.Sleep(80); Delayed = true;
        }
    }

    private static JsonElement[] Windows<T>(CaptureLog<T> log) => log.Entries
        .Where(line => line.StartsWith("PLC diagnostic window: ", StringComparison.Ordinal))
        .Select(line => JsonSerializer.Deserialize<JsonElement>(line["PLC diagnostic window: ".Length..])).ToArray();

    private sealed class CaptureLog<T> : ILogger<T>
    {
        internal ConcurrentQueue<string> Entries { get; } = new();
        internal ConcurrentQueue<LogEvent> Events { get; } = new();
        internal TaskCompletionSource FirstWarning { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int WarningCount;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Enqueue(formatter(state, exception));
            var values = state is IEnumerable<KeyValuePair<string, object?>> named ? named.ToDictionary(p => p.Key, p => p.Value) : [];
            Events.Enqueue(new(level, values.GetValueOrDefault("{OriginalFormat}")?.ToString() ?? "", values));
            if (level >= LogLevel.Warning) { Interlocked.Increment(ref WarningCount); FirstWarning.TrySetResult(); }
        }
    }
    private sealed record LogEvent(LogLevel Level, string Template, Dictionary<string, object?> Values);
}
