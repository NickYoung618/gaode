using Gaode.Plc.Protocol;
using System.Buffers.Binary;
using System.Net.Sockets;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Gaode.Diagnostics;

namespace Gaode.Infrastructure.Devices.Plc;

public sealed record ModbusWriteDispatch(string Channel, long Tick, DateTimeOffset AtUtc, byte Function, ushort Offset, ushort[] Words);
internal sealed record ModbusDispatchSnapshot(long Total, bool Gap, IReadOnlyList<ModbusWriteDispatch> Writes);

public sealed record ModbusExchange(DateTimeOffset ObservedAtUtc, string Request,
    string? Response, string? Error)
{
    public Guid ConnectionId { get; init; }
    public string Channel { get; init; } = "unspecified";
    public DateTimeOffset StartedUtc { get; init; }
    public long Sequence { get; init; }
}
internal sealed record ModbusJournalSnapshot(long LatestSequence, bool Gap, IReadOnlyList<ModbusExchange> Exchanges);
internal sealed class ModbusReadRejectedException(byte function, byte code)
    : IOException($"Modbus exception {code:X2} for read function {function:X2}.");

/// <summary>One serialized TCP session. Addresses are zero-based PDU offsets. Never retries writes.</summary>
public sealed class ModbusTcpClient(string host, int port, byte unit, TimeSpan ioTimeout,
    ILogger? logger = null, string channel = "unspecified") : IAsyncDisposable, IPlcTransport
{
    private TcpClient socket = new() { NoDelay = true };
    private readonly SemaphoreSlim gate = new(1);
    private readonly List<ModbusExchange> exchanges = [];
    private readonly Queue<ModbusExchange> evidenceJournal = new();
    private long exchangeSequence;
    internal long ExchangeSequence { get { lock (exchangeSync) return exchangeSequence; } }
    internal ModbusJournalSnapshot EvidenceSince(long after)
    {
        lock (exchangeSync) return new(exchangeSequence,
            evidenceJournal.Count > 0 && after < evidenceJournal.Peek().Sequence - 1,
            evidenceJournal.Where(e => e.Sequence > after).ToArray());
    }
    private readonly Queue<ModbusWriteDispatch> writeDispatches = new();
    private long totalWriteDispatches;
    internal long TotalWriteDispatches => Interlocked.Read(ref totalWriteDispatches);
    internal bool WriteDispatchGap { get { lock (exchangeSync) return totalWriteDispatches > writeDispatches.Count; } }
    internal IReadOnlyList<ModbusWriteDispatch> WriteDispatches { get { lock (exchangeSync) return writeDispatches.ToArray(); } }
    internal ModbusDispatchSnapshot DispatchSnapshot
    { get { lock (exchangeSync) return new(totalWriteDispatches, totalWriteDispatches > writeDispatches.Count, writeDispatches.ToArray()); } }
    private void RecordWriteDispatch(byte function, ushort offset, ushort[] words)
    {
        lock (exchangeSync)
        {
            Interlocked.Increment(ref totalWriteDispatches);
            writeDispatches.Enqueue(new(channel, Stopwatch.GetTimestamp(), DateTimeOffset.UtcNow, function, offset, words.ToArray()));
            if (writeDispatches.Count > 4096) writeDispatches.Dequeue();
        }
    }
    private readonly object exchangeSync = new();
    private ushort transaction;
    private int lastCompletedTransactionId;
    private long lastSlowSuccessLogTick;
    private int suppressedSlowSuccesses;
    private bool connected;
    private bool unusable;
    private Exception? unusableCause;
    private readonly HeartbeatDiagnosticWindow? diagnostics = channel == "heartbeat" && logger is not null
        ? new("Host/heartbeat-transport", logger) : null;
    private Guid connectionId = Guid.NewGuid();
    private string? localEndpoint;
    private object? inFlight;
    internal void CaptureDiagnosticWindow(string reason, bool critical = false, object? context = null) =>
        diagnostics?.Capture(reason, critical, new { connectionId, localEndpoint, remoteEndpoint = $"{host}:{port}",
            inFlight = Volatile.Read(ref inFlight), context });
    public int LastCompletedTransactionId => Volatile.Read(ref lastCompletedTransactionId);
    public IReadOnlyList<ModbusExchange> Exchanges { get { lock (exchangeSync) return exchanges.ToArray(); } }

    public async Task<ushort[]> ReadRegistersAsync(ushort offset, ushort count, CancellationToken ct = default)
    {
        if (count is < 1 or > 125 || (uint)offset + count > 65536) throw new ArgumentOutOfRangeException(nameof(count));
        var response = await ExchangeAsync(Pair(3, offset, count), ct);
        var values = new ushort[count];
        for (var i = 0; i < count; i++) values[i] = U16(response, 2 + i * 2);
        return values;
    }

    public async Task<bool[]> ReadCoilsAsync(ushort offset, ushort count, CancellationToken ct = default)
    {
        if (count is < 1 or > 2000 || (uint)offset + count > 65536) throw new ArgumentOutOfRangeException(nameof(count));
        var response = await ExchangeAsync(Pair(1, offset, count), ct);
        return Enumerable.Range(0, count).Select(i => (response[2 + i / 8] & (1 << (i % 8))) != 0).ToArray();
    }

    public async Task WriteCoilAsync(ushort offset, bool value, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        RecordWriteDispatch(5, offset, [value ? (ushort)1 : (ushort)0]);
        _ = await ExchangeAsync(Pair(5, offset, value ? (ushort)0xFF00 : (ushort)0), ct);
    }

    public async Task WriteRegisterAsync(ushort offset, ushort value, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        RecordWriteDispatch(6, offset, [value]);
        _ = await ExchangeAsync(Pair(6, offset, value), ct);
    }

    public async Task WriteRegistersAsync(ushort offset, ushort[] values, CancellationToken ct = default)
    {
        if (values.Length is < 1 or > 123 || (uint)offset + values.Length > 65536) throw new ArgumentOutOfRangeException(nameof(values));
        ct.ThrowIfCancellationRequested();
        RecordWriteDispatch(16, offset, values);
        var pdu = new byte[6 + values.Length * 2];
        Pair(16, offset, (ushort)values.Length).CopyTo(pdu, 0);
        pdu[5] = (byte)(values.Length * 2);
        for (var i = 0; i < values.Length; i++) Put(pdu, 6 + i * 2, values[i]);
        _ = await ExchangeAsync(pdu, ct);
    }

    private async Task<byte[]> ExchangeAsync(byte[] pdu, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp();
        var startedUtc = DateTimeOffset.UtcNow;
        var phase = "WaitGate";
        void Progress(string name, int? transactionId = null)
        {
            if (diagnostics is not null) Volatile.Write(ref inFlight,
                new { phase = name, transaction = transactionId, startedUtc, startedTick = started,
                    atUtc = DateTimeOffset.UtcNow, atTick = Stopwatch.GetTimestamp(), function = pdu[0] });
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(ioTimeout);
        try { await gate.WaitAsync(deadline.Token); }
        catch (OperationCanceledException e) when (!ct.IsCancellationRequested)
        {
            diagnostics?.Record("gate-timeout", new { startedUtc, startedTick = started,
                elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds, function = pdu[0],
                error = e.ToString(), requestSent = false });
            CaptureDiagnosticWindow("WaitGateTimeout", true);
            logger?.LogError(e,
                "PLC exchange failed at {FailedAtUtc:o}: channel={Channel}, endpoint={Host}:{Port}, function={Function}, phase=WaitGate, elapsedMs={ElapsedMs}, deadlineMs={DeadlineMs}; request not sent",
                DateTimeOffset.UtcNow, channel, host, port, pdu[0],
                Stopwatch.GetElapsedTime(started).TotalMilliseconds, ioTimeout.TotalMilliseconds);
            throw;
        }
        byte[]? request = null;
        byte[]? response = null;
        var gateWaitMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        double connectMs = 0, writeMs = 0, headerMs = 0, bodyMs = 0;
        long requestWrittenTick = 0, headerReadTick = 0, bodyReadTick = 0;
        long validatedTick = 0, journalWaitingTick = 0, journalAcquiredTick = 0, journalEndedTick = 0;
        var gateAcquiredTick = Stopwatch.GetTimestamp();
        long connectStartedTick = 0, connectedTick = 0, requestWriteStartedTick = 0,
            headerReadStartedTick = 0, bodyReadStartedTick = 0;
        DateTimeOffset? requestWrittenUtc = null, headerReadUtc = null, bodyReadUtc = null;
        object Facts(string result, Exception? error = null) => new
        {
            connectionId, localEndpoint, remoteEndpoint = $"{host}:{port}", transaction,
            function = pdu[0], phase, result, startedUtc, startedTick = started,
            finishedUtc = DateTimeOffset.UtcNow, finishedTick = Stopwatch.GetTimestamp(),
            requestWrittenUtc, requestWrittenTick, headerReadUtc, headerReadTick, bodyReadUtc, bodyReadTick,
            connectStartedTick, connectedTick, requestWriteStartedTick, headerReadStartedTick, bodyReadStartedTick,
            gateWaitMs, connectMs, requestWriteMs = writeMs, responseHeaderMs = headerMs, responseBodyMs = bodyMs,
            elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds,
            deadlineMs = ioTimeout.TotalMilliseconds, deadlineCancelled = deadline.IsCancellationRequested,
            request = request is null ? null : Convert.ToHexString(request),
            response = response is null ? null : Convert.ToHexString(response), error = error?.ToString()
        };
        try
        {
            PlcScheduledTransport.CurrentDeadline.Value?.Check("BeforeConnect");
            phase = "Connect";
            Progress(phase);
            if (unusable) throw new PlcConnectionUnusableException(unusableCause!);
            if (!connected)
            {
                var connectStarted = Stopwatch.GetTimestamp();
                connectStartedTick = connectStarted;
                await socket.ConnectAsync(host, port, deadline.Token);
                PlcScheduledTransport.CurrentDeadline.Value?.Check("AfterConnect");
                connectedTick = Stopwatch.GetTimestamp();
                connectMs = Stopwatch.GetElapsedTime(connectStarted).TotalMilliseconds;
                connected = true;
                localEndpoint = socket.Client.LocalEndPoint?.ToString();
                logger?.LogInformation("PLC channel {Channel} connected {LocalEndpoint} -> {Host}:{Port}; I/O deadline {TimeoutMs} ms",
                    channel, socket.Client.LocalEndPoint, host, port, ioTimeout.TotalMilliseconds);
            }
            request = new byte[7 + pdu.Length];
            Put(request, 0, unchecked(++transaction));
            Put(request, 4, (ushort)(pdu.Length + 1));
            request[6] = unit;
            pdu.CopyTo(request, 7);
            var stream = socket.GetStream();
            phase = "WriteRequest";
            Progress(phase, transaction);
            PlcScheduledTransport.CurrentDeadline.Value?.Check("BeforeWrite");
            var writeStarted = Stopwatch.GetTimestamp();
            requestWriteStartedTick = writeStarted;
            if (PlcExchangeClock.Current.Value is { } measuring)
            { measuring.Sent = writeStarted; measuring.StartedUtc = DateTimeOffset.UtcNow; }
            await stream.WriteAsync(request, deadline.Token);
            requestWrittenTick = Stopwatch.GetTimestamp();
            requestWrittenUtc = DateTimeOffset.UtcNow;
            writeMs = Stopwatch.GetElapsedTime(writeStarted).TotalMilliseconds;
            PlcScheduledTransport.CurrentDeadline.Value?.Check("AfterWrite");
            var header = new byte[7];
            phase = "ReadHeader";
            Progress(phase, transaction);
            var headerStarted = Stopwatch.GetTimestamp();
            headerReadStartedTick = headerStarted;
            await stream.ReadExactlyAsync(header, deadline.Token);
            headerReadTick = Stopwatch.GetTimestamp();
            headerReadUtc = DateTimeOffset.UtcNow;
            headerMs = Stopwatch.GetElapsedTime(headerStarted).TotalMilliseconds;
            response = header;
            PlcScheduledTransport.CurrentDeadline.Value?.Check("AfterHeader");
            var length = U16(header, 4);
            if (U16(header, 0) != transaction || U16(header, 2) != 0 || header[6] != unit || length is < 2 or > 254)
                throw new IOException("Invalid Modbus MBAP header.");
            var body = new byte[length - 1];
            phase = "ReadBody";
            Progress(phase, transaction);
            var bodyStarted = Stopwatch.GetTimestamp();
            bodyReadStartedTick = bodyStarted;
            await stream.ReadExactlyAsync(body, deadline.Token);
            bodyReadTick = Stopwatch.GetTimestamp();
            bodyReadUtc = DateTimeOffset.UtcNow;
            bodyMs = Stopwatch.GetElapsedTime(bodyStarted).TotalMilliseconds;
            response = [.. header, .. body];
            PlcScheduledTransport.CurrentDeadline.Value?.Check("AfterBody");
            phase = "ValidateResponse";
            if (body[0] == (pdu[0] | 0x80) && body.Length == 2)
            {
                if (pdu[0] is 1 or 3) throw new ModbusReadRejectedException(pdu[0], body[1]);
                throw new IOException($"Modbus exception {body[1]:X2} for function {pdu[0]:X2}.");
            }
            if (body[0] != pdu[0]) throw new IOException("Mismatched Modbus function.");
            if (pdu[0] is 1 or 3)
            {
                var bytes = pdu[0] == 1 ? (U16(pdu, 3) + 7) / 8 : U16(pdu, 3) * 2;
                if (body.Length != bytes + 2 || body[1] != bytes) throw new IOException("Invalid Modbus read byte count.");
            }
            else if (!body.SequenceEqual(pdu.Take(5))) throw new IOException("Invalid Modbus write acknowledgement.");
            Volatile.Write(ref lastCompletedTransactionId, transaction);
            validatedTick = Stopwatch.GetTimestamp();
            journalWaitingTick = Stopwatch.GetTimestamp();
            lock (exchangeSync)
            {
            journalAcquiredTick = Stopwatch.GetTimestamp();
            Record(new(DateTimeOffset.UtcNow,
                Convert.ToHexString(request), Convert.ToHexString(response), null)
                { ConnectionId = connectionId, Channel = channel, StartedUtc = startedUtc });
            }
            journalEndedTick = Stopwatch.GetTimestamp();
            var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            diagnostics?.Record("exchange", Facts("Acknowledged"));
            if (elapsedMs >= 250) CaptureDiagnosticWindow("SlowExchange");
            if (channel == "heartbeat" && elapsedMs >= 250)
            {
                var nowTick = Stopwatch.GetTimestamp();
                if (lastSlowSuccessLogTick == 0 ||
                    Stopwatch.GetElapsedTime(lastSlowSuccessLogTick, nowTick) >= TimeSpan.FromSeconds(5))
                {
                    logger?.LogWarning("PLC heartbeat Modbus slow success: startedUtc={StartedUtc:o}, finishedUtc={FinishedUtc:o}, startedTick={StartedTick}, finishedTick={FinishedTick}, tickFrequency={TickFrequency}, channel={Channel}, transaction={TransactionId}, function={Function}, gateWaitMs={GateWaitMs:F1}, connectMs={ConnectMs:F1}, requestWriteMs={RequestWriteMs:F1}, responseHeaderMs={HeaderMs:F1}, responseBodyMs={BodyMs:F1}, totalMs={TotalMs:F1}, suppressedSlowSuccesses={Suppressed}",
                        startedUtc, DateTimeOffset.UtcNow, started, nowTick, Stopwatch.Frequency,
                        channel, transaction, pdu[0], gateWaitMs, connectMs, writeMs, headerMs,
                        bodyMs, elapsedMs, suppressedSlowSuccesses);
                    lastSlowSuccessLogTick = nowTick;
                    suppressedSlowSuccesses = 0;
                }
                else suppressedSlowSuccesses++;
            }
            return body;
        }
        catch (Exception e)
        {
            diagnostics?.Record("exchange", Facts("Failed", e));
            var ioExpired = PlcScheduledTransport.CurrentDeadline.Value is { IsExpired: true, CallerCancelled: false };
            if (!ct.IsCancellationRequested || ioExpired) CaptureDiagnosticWindow("ExchangeFailure", true);
            if (!ct.IsCancellationRequested || ioExpired)
            {
                ThreadPool.GetAvailableThreads(out var availableWorkers, out var availableIo);
                logger?.LogWarning("PLC failure timing: channel={Channel}, connectionId={ConnectionId}, transaction={Transaction}, phase={Phase}, startedUtc={StartedUtc:o}, startedTick={StartedTick}, tickFrequency={TickFrequency}, connectStartedTick={ConnectStartedTick}, connectedTick={ConnectedTick}, requestWriteStartedTick={RequestWriteStartedTick}, requestWrittenTick={RequestWrittenTick}, headerReadStartedTick={HeaderReadStartedTick}, headerReadTick={HeaderReadTick}, bodyReadStartedTick={BodyReadStartedTick}, bodyReadTick={BodyReadTick}",
                    channel, connectionId, transaction, phase, startedUtc, started, Stopwatch.Frequency,
                    connectStartedTick, connectedTick, requestWriteStartedTick, requestWrittenTick,
                    headerReadStartedTick, headerReadTick, bodyReadStartedTick, bodyReadTick);
                logger?.LogError(e,
                    "PLC exchange failed at {FailedAtUtc:o}: channel={Channel}, endpoint={Host}:{Port}, function={Function}, phase={Phase}, elapsedMs={ElapsedMs}, deadlineMs={DeadlineMs}, deadlineCancelled={DeadlineCancelled}, request={Request}, response={Response}, poolThreads={PoolThreads}, pendingWork={PendingWork}, availableWorkers={AvailableWorkers}, availableIo={AvailableIo}, processors={Processors}",
                    DateTimeOffset.UtcNow, channel, host, port, pdu[0], phase,
                    Stopwatch.GetElapsedTime(started).TotalMilliseconds, ioTimeout.TotalMilliseconds,
                    deadline.IsCancellationRequested,
                    request is null ? "" : Convert.ToHexString(request),
                    response is null ? "" : Convert.ToHexString(response),
                    ThreadPool.ThreadCount, ThreadPool.PendingWorkItemCount, availableWorkers, availableIo,
                    Environment.ProcessorCount);
            }
            // A complete, correlated read rejection consumed the entire response and cannot
            // have dispatched a physical write. The existing pre-dispatch policy may retry it.
            // Timeout, malformed frames and write failures still require reconciliation.
            if (e is not ModbusReadRejectedException)
            {
                unusable = true;
                unusableCause ??= e is OperationCanceledException &&
                    (ioExpired ||
                     deadline.IsCancellationRequested && !ct.IsCancellationRequested)
                    ? new TimeoutException("PlcConnectionQueueOrExchangeDeadline", e) : e;
                socket.Dispose();
            }
            lock (exchangeSync) Record(new(DateTimeOffset.UtcNow,
                request is null ? "" : Convert.ToHexString(request),
                response is null ? null : Convert.ToHexString(response),
                $"{e.GetType().Name}: {e.Message}")
                { ConnectionId = connectionId, Channel = channel, StartedUtc = startedUtc });
            if (e is OperationCanceledException && !ct.IsCancellationRequested) throw new TimeoutException("Modbus I/O deadline exceeded; outcome may be unknown.", e);
            throw;
        }
        finally
        {
            var serviceEnded = Stopwatch.GetTimestamp();
            var responseEnded = bodyReadTick != 0 ? bodyReadTick : serviceEnded;
            if (PlcExchangeClock.Current.Value is { } measuring)
            { measuring.Ended = responseEnded; measuring.EndedUtc = bodyReadUtc ?? DateTimeOffset.UtcNow; measuring.ServiceEnded = serviceEnded; }
            // Preserve the original neutral service-end counter. V06 uses the separately
            // identified collection endpoint, with all post-read work retained in C/g.
            PlcCommunicationMeasurement.Exchange(channel, pdu[0], U16(pdu, 1), U16(pdu, 3),
                PlcExchangeClock.Current.Value?.Queued ?? started, requestWriteStartedTick, serviceEnded, request?.Length ?? 0,
                response?.Length ?? 0, phase == "ValidateResponse" && lastCompletedTransactionId == transaction);
            if (PlcTimingTrace.Enabled) PlcTimingTrace.Record("host-exchange", new
            {
                connectionId, localEndpoint, remoteEndpoint = $"{host}:{port}", transaction, channel,
                source = PlcCommunicationMeasurement.Source.Value ?? "unclassified", function = pdu[0],
                offset = U16(pdu, 1), count = U16(pdu, 3), started, gateAcquiredTick,
                queued = PlcExchangeClock.Current.Value?.Queued ?? started,
                granted = PlcExchangeClock.Current.Value?.Granted ?? gateAcquiredTick,
                requestWriteStartedTick, requestWrittenTick, headerReadStartedTick, headerReadTick,
                bodyReadStartedTick, bodyReadTick, validatedTick, journalWaitingTick, journalAcquiredTick,
                journalEndedTick, serviceEnded, responseEnded, startedUtc, requestWrittenUtc, bodyReadUtc,
                success = phase == "ValidateResponse" && lastCompletedTransactionId == transaction,
                // Completed by the same arbiter after this exchange returns; serialized only at final flush.
                dispatch = PlcExchangeClock.Current.Value?.Dispatch,
                absoluteDeadline = PlcScheduledTransport.CurrentDeadline.Value?.Snapshot
            });
            Volatile.Write(ref inFlight, null); gate.Release();
        }
    }

    private void Record(ModbusExchange value)
    {
        value = value with { Sequence = ++exchangeSequence };
        if (exchanges.Count >= 256) exchanges.RemoveAt(0);
        exchanges.Add(value);
        // Bounded per connection owner, with an explicit gap; never hide overwritten action evidence.
        if (evidenceJournal.Count >= 8192) evidenceJournal.Dequeue();
        evidenceJournal.Enqueue(value);
    }

    public async Task ResetConnectionAsync(CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            socket.Dispose();
            socket = new TcpClient { NoDelay = true };
            connected = false;
            unusable = false;
            unusableCause = null;
            transaction = 0;
            connectionId = Guid.NewGuid();
            localEndpoint = null;
        }
        finally { gate.Release(); }
    }

    private static ushort U16(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(offset, 2));
    private static void Put(byte[] bytes, int offset, ushort value) => BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(offset, 2), value);
    private static byte[] Pair(byte function, ushort offset, ushort value)
    {
        var bytes = new byte[5]; bytes[0] = function; Put(bytes, 1, offset); Put(bytes, 3, value); return bytes;
    }
    public async ValueTask DisposeAsync()
    {
        socket.Dispose(); gate.Dispose();
        if (diagnostics is not null) await diagnostics.FlushAsync();
        PlcTimingTrace.Flush();
        PlcCommunicationMeasurement.Flush();
    }
}

// Neutral, bounded measurement only; never supplies device state or controls dispatch.
// Enabled solely for the isolated comparison. Both source roots use this exact hook.
internal static class PlcCommunicationMeasurement
{
    private static readonly string? directory = Environment.GetEnvironmentVariable("GAODE_013_MEASUREMENT_ROOT");
    private static readonly object sync = new();
    private const int Capacity = 262144;
    private static readonly List<ExchangePoint> points = [];
    private static readonly Dictionary<string, long> counters = [];
    private static readonly List<object> rounds = [];
    private static readonly List<object> policies = [];
    private static long dropped;
    internal static readonly AsyncLocal<string?> Source = new();
    internal sealed record ExchangePoint(string Channel, string Source, byte Function, ushort Offset,
        ushort CountOrValue, long Enqueued, long Sent, long Ended, int RequestBytes, int ResponseBytes, bool Success);
    internal static void Count(string name, long amount = 1)
    {
        if (directory is null) return;
        lock (sync) counters[name] = counters.GetValueOrDefault(name) + amount;
    }
    internal static void Exchange(string channel, byte function, ushort offset, ushort count,
        long enqueued, long sent, long ended, int requestBytes, int responseBytes, bool success)
    {
        if (directory is null) return;
        lock (sync)
        {
            var key = channel + "/" + function;
            counters[key] = counters.GetValueOrDefault(key) + (sent == 0 ? 0 : 1);
            if (points.Count == Capacity) { dropped++; return; }
            points.Add(new(channel, Source.Value ?? "unclassified", function, offset, count,
                enqueued, sent, ended, requestBytes, responseBytes, success));
        }
    }
    internal static void Flush()
    {
        if (directory is null) return;
        lock (sync)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, $"transport-{Environment.ProcessId}.json"),
                System.Text.Json.JsonSerializer.Serialize(new { schema = "013-neutral-measurement/1",
                    pid = Environment.ProcessId, frequency = Stopwatch.Frequency,
                    capturedUtc = DateTimeOffset.UtcNow, dropped, counters, points, rounds, policies }));
        }
    }
    internal static void Round(string group, long due, long queued, PlcReadStamp[] blocks, long published,
        long awakened = 0, int generation = 0, long epoch = 0, string? source = null)
    {
        if (directory is null) return;
        lock (sync)
        {
            if (rounds.Count >= 8192) { dropped++; return; }
            rounds.Add(new { group, due, queued, published, awakened, generation, epoch, source = source ?? group,
                blocks = blocks.Select(b => new { b.Queued, b.Sent, b.Ended }).ToArray() });
        }
    }
    internal static void Policy(long tick, int basic, int position, bool axis, bool flip, bool flipFast,
        bool putBack, bool putBackFast, bool transfer)
    {
        if (directory is null) return;
        lock (sync)
        {
            if (policies.Count >= 8192) { dropped++; return; }
            policies.Add(new { tick, basic, position, axis, flip, flipFast, putBack, putBackFast, transfer });
        }
    }
}

internal sealed class PlcConnectionUnusableException(Exception firstCause)
    : InvalidOperationException("Connection requires reconciliation; automatic reconnect is disabled.", firstCause)
{
    internal Exception FirstCause => InnerException!;
}

// This device's finite PDU arbiter, not a business executor. One underlying connection.
internal sealed class PlcIoDeadline(TimeProvider clock, long started, int milliseconds, CancellationToken token,
    CancellationToken callerToken = default)
{
    private long lastChecked;
    private string lastCheckpoint = "NotChecked", lastResult = "NotChecked";
    internal PlcDeadlineTiming Snapshot => new(started, clock.TimestampFrequency, milliseconds,
        lastChecked, lastCheckpoint, lastResult,
        lastChecked == 0 ? null : clock.GetElapsedTime(started, lastChecked).TotalMilliseconds);
    internal bool IsExpired => clock.GetElapsedTime(started).TotalMilliseconds >= milliseconds;
    internal bool CallerCancelled => callerToken.IsCancellationRequested;
    internal void Check(string checkpoint = "Unspecified")
    {
        lastCheckpoint = checkpoint;
        try
        {
            token.ThrowIfCancellationRequested();
            if (IsExpired) throw new TimeoutException("PlcConnectionQueueOrExchangeDeadline");
            lastResult = "Passed";
        }
        catch (Exception error) { lastResult = error.GetType().Name; throw; }
        finally { lastChecked = clock.GetTimestamp(); }
    }
}
internal sealed record PlcDeadlineTiming(long StartedTick, long Frequency, int Milliseconds,
    long LastCheckedTick, string LastCheckpoint, string LastResult, double? LastElapsedMs);

internal sealed class PlcScheduledTransport(ModbusTcpClient wire, int ioTimeoutMs, TimeProvider? timeProvider = null) : IPlcTransport
{
    internal static readonly AsyncLocal<Func<bool>?> Eligibility = new();
    internal static readonly AsyncLocal<PlcIoDeadline?> CurrentDeadline = new();
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private sealed record Request(string Source, long Queued, Func<bool>? Eligible,
        PlcExchangeClock? Clock, Func<CancellationToken, Task<object>> Exchange, CancellationToken Token,
        TaskCompletionSource<object> Completion, PlcIoDeadline Deadline);
    private readonly object sync = new();
    private readonly List<Request> queue = [];
    private bool running, lastCritical, lastFast;
    private int ordinarySlots;
    private string lastOrdinary = "";
    private long nextDiagnosticId;
    internal int PendingCount { get { lock (sync) return queue.Count + (running ? 1 : 0); } }
    private async Task<T> Submit<T>(Func<CancellationToken, Task<T>> exchange, CancellationToken ct)
    {
        var started = clock.GetTimestamp();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(ioTimeoutMs); // Original I/O deadline includes connection queue time.
        var absolute = new PlcIoDeadline(clock, started, ioTimeoutMs, deadline.Token, ct);
        // Completion publication is outside every arbiter lock; avoid an extra worker-queue handoff.
        var completion = new TaskCompletionSource<object>();
        var queued = Stopwatch.GetTimestamp();
        var request = new Request(PlcCommunicationMeasurement.Source.Value ?? "K", queued,
            Eligibility.Value, PlcExchangeClock.Current.Value ?? new PlcExchangeClock { Queued = queued },
            async token => (object)(await exchange(token))!, deadline.Token, completion, absolute);
        var trace = PlcTimingTrace.Enabled ? new PlcDispatchTiming { RequestId = Interlocked.Increment(ref nextDiagnosticId),
            Source = request.Source, SubmitLockWaiting = Stopwatch.GetTimestamp() } : null;
        if (trace is not null) request.Clock!.Dispatch = trace;
        lock (sync)
        {
            if (trace is not null) trace.SubmitLockAcquired = Stopwatch.GetTimestamp();
            queue.Add(request);
            if (trace is not null) trace.Enqueued = Stopwatch.GetTimestamp();
            if (!running) { running = true; _ = DrainAsync(); }
        }
        if (trace is not null) trace.SubmitLockReleased = Stopwatch.GetTimestamp();
        try
        {
            var result = (T)await completion.Task.WaitAsync(deadline.Token);
            if (trace is not null) trace.SubmitResumed = Stopwatch.GetTimestamp();
            absolute.Check("SubmitCompleted");
            if (trace is not null) trace.SubmitOutcome = "Completed";
            return result;
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested && !ct.IsCancellationRequested)
        { if (trace is not null) trace.SubmitOutcome = "TimeoutException"; throw new TimeoutException("PlcConnectionQueueOrExchangeDeadline"); }
        catch (Exception error) { if (trace is not null) trace.SubmitOutcome = error.GetType().Name; throw; }
        finally
        {
            if (trace is not null)
            {
                if (trace.SubmitResumed == 0) trace.SubmitResumed = Stopwatch.GetTimestamp();
                trace.DeadlineAtSubmitEnd = absolute.Snapshot;
            }
        }
    }
    private async Task DrainAsync()
    {
        // Start outside a caller's lock, and never allow a second drain on this connection.
        var tracing = PlcTimingTrace.Enabled;
        var scheduled = tracing ? Stopwatch.GetTimestamp() : 0;
        await Task.Yield();
        var resumed = tracing ? Stopwatch.GetTimestamp() : 0;
        long previousRequest = 0;
        while (true)
        {
            Request? next = null;
            Request[] obsolete;
            var waiting = tracing ? Stopwatch.GetTimestamp() : 0;
            long acquired, selectedStart = 0, selectedEnd = 0, snapshotStart = 0, snapshotEnd = 0;
            PlcPendingTiming[] pending = []; int pendingCount = 0;
            lock (sync)
            {
                acquired = tracing ? Stopwatch.GetTimestamp() : 0;
                if (tracing)
                {
                    snapshotStart = Stopwatch.GetTimestamp(); pendingCount = queue.Count;
                    pending = queue.Take(16).Select(r => new PlcPendingTiming(r.Clock?.Dispatch?.RequestId ?? 0,
                        r.Source, r.Queued, r.Token.IsCancellationRequested, r.Eligible is not null)).ToArray();
                    snapshotEnd = Stopwatch.GetTimestamp();
                }
                obsolete = queue.Where(r => r.Token.IsCancellationRequested || r.Deadline.IsExpired).ToArray();
                foreach (var expired in obsolete) queue.Remove(expired);
                if (queue.Count == 0) running = false;
                else
                {
                    selectedStart = tracing ? Stopwatch.GetTimestamp() : 0;
                    next = Select(); queue.Remove(next);
                    selectedEnd = tracing ? Stopwatch.GetTimestamp() : 0;
                }
            }
            // Never run a completion consumer while holding the queue lock.
            foreach (var expired in obsolete)
            {
                var cancelled = expired.Clock?.Dispatch;
                if (cancelled is not null)
                { cancelled.Outcome = "CancelledBeforeSelection"; cancelled.CompletionSignaled = Stopwatch.GetTimestamp(); }
                if (expired.Token.IsCancellationRequested) expired.Completion.TrySetCanceled();
                else expired.Completion.TrySetException(new TimeoutException("PlcConnectionQueueOrExchangeDeadline"));
                if (cancelled is not null)
                {
                    cancelled.CompletionReturned = Stopwatch.GetTimestamp();
                    PlcTimingTrace.Record("arbiter-no-send", new { dispatch = cancelled });
                }
            }
            if (next is null) return;
            var trace = next.Clock?.Dispatch;
            if (trace is not null)
            {
                trace.QueueLockReleased = Stopwatch.GetTimestamp();
                trace.DrainScheduled = scheduled; trace.DrainResumed = resumed;
                trace.QueueLockWaiting = waiting; trace.QueueLockAcquired = acquired;
                trace.SelectStarted = selectedStart; trace.SelectEnded = selectedEnd;
                trace.Pending = pending; trace.PendingCount = pendingCount; trace.SnapshotTruncated = pendingCount > pending.Length;
                trace.SnapshotStarted = snapshotStart; trace.SnapshotEnded = snapshotEnd;
                trace.PreviousRequestId = previousRequest;
            }
            var previousSource = PlcCommunicationMeasurement.Source.Value;
            var previousClock = PlcExchangeClock.Current.Value;
            var previousDeadline = CurrentDeadline.Value;
            try
            {
                next.Deadline.Check("BeforeEligibility");
                if (trace is not null) trace.EligibilityStarted = Stopwatch.GetTimestamp();
                bool eligible;
                try { eligible = next.Eligible?.Invoke() != false; }
                finally { if (trace is not null) trace.EligibilityEnded = Stopwatch.GetTimestamp(); }
                if (trace is not null) trace.EligibilityPassed = eligible;
                if (!eligible) throw new OperationCanceledException("SupersededAcquisitionOwner");
                next.Deadline.Check("AfterEligibility");
                if (next.Clock is not null) next.Clock.Granted = Stopwatch.GetTimestamp();
                PlcCommunicationMeasurement.Source.Value = next.Source;
                PlcExchangeClock.Current.Value = next.Clock;
                CurrentDeadline.Value = next.Deadline;
                object result;
                try { result = await next.Exchange(next.Token); }
                finally { if (trace is not null) trace.ExchangeResumed = Stopwatch.GetTimestamp(); }
                next.Deadline.Check("AfterExchange");
                if (trace is not null) { trace.Outcome = "Completed"; trace.CompletionSignaled = Stopwatch.GetTimestamp(); }
                next.Completion.TrySetResult(result);
            }
            catch (OperationCanceledException)
            {
                if (trace is not null) { trace.Outcome = "Cancelled"; trace.CompletionSignaled = Stopwatch.GetTimestamp(); }
                next.Completion.TrySetCanceled();
            }
            catch (Exception error)
            {
                if (trace is not null) { trace.Outcome = error.GetType().Name; trace.CompletionSignaled = Stopwatch.GetTimestamp(); }
                next.Completion.TrySetException(error);
            }
            finally
            {
                if (trace is not null) trace.CompletionReturned = Stopwatch.GetTimestamp();
                PlcCommunicationMeasurement.Source.Value = previousSource; PlcExchangeClock.Current.Value = previousClock;
                CurrentDeadline.Value = previousDeadline;
                if (trace is not null)
                {
                    trace.StepEnded = Stopwatch.GetTimestamp(); previousRequest = trace.RequestId;
                    if (next.Clock!.Sent == 0) PlcTimingTrace.Record("arbiter-no-send", new { dispatch = trace });
                }
            }
        }
    }
    private Request Select()
    {
        var stop = queue.FirstOrDefault(r => r.Source == "Stop");
        if (stop is not null) return stop;
        var fast = queue.FirstOrDefault(r => r.Source == "X" || r.Source.EndsWith(":first", StringComparison.Ordinal));
        if (fast is not null && (!lastFast || queue.All(r => r == fast))) { lastFast = true; return fast; }
        lastFast = false;
        var critical = queue.FirstOrDefault(r => r.Source is "K" or "D" || r.Source.StartsWith("D:", StringComparison.Ordinal));
        var regular = queue.Where(r => r.Source is "B" or "P" or "F" or "U" or "T" or "G" or "R").ToArray();
        if (critical is not null && (!lastCritical || regular.Length == 0)) { lastCritical = true; return critical; }
        if (regular.Length != 0)
        {
            lastCritical = false;
            var position = regular.FirstOrDefault(r => r.Source == "P");
            var ordinary = regular.Where(r => r.Source != "P").ToArray();
            if (position is not null && (ordinarySlots >= 2 || ordinary.Length == 0))
            { ordinarySlots = 0; return position; }
            // B and current feedback have equal priority: alternate if both pending;
            // within a source preserve FIFO. The initial tie goes to B.
            var selected = lastOrdinary == "B" ? ordinary.FirstOrDefault(r => r.Source != "B") : ordinary.FirstOrDefault(r => r.Source == "B");
            selected ??= ordinary.MinBy(r => r.Queued)!;
            ordinarySlots++; lastOrdinary = selected.Source; return selected;
        }
        return critical ?? fast ?? queue[0];
    }
    public Task<ushort[]> ReadRegistersAsync(ushort offset, ushort count, CancellationToken ct = default) =>
        Submit(token => wire.ReadRegistersAsync(offset, count, token), ct);
    public Task<bool[]> ReadCoilsAsync(ushort offset, ushort count, CancellationToken ct = default) =>
        Submit(token => wire.ReadCoilsAsync(offset, count, token), ct);
    public async Task WriteCoilAsync(ushort offset, bool value, CancellationToken ct = default) =>
        _ = await Submit(async token => { await wire.WriteCoilAsync(offset, value, token); return true; }, ct);
    public async Task WriteRegisterAsync(ushort offset, ushort value, CancellationToken ct = default) =>
        _ = await Submit(async token => { await wire.WriteRegisterAsync(offset, value, token); return true; }, ct);
    public async Task WriteRegistersAsync(ushort offset, ushort[] values, CancellationToken ct = default) =>
        _ = await Submit(async token => { await wire.WriteRegistersAsync(offset, values, token); return true; }, ct);
}

// Finite optional diagnostic data for this adapter's existing arbiter, never a scheduling policy.
internal sealed class PlcDispatchTiming
{
    public long RequestId { get; internal set; }
    public long PreviousRequestId { get; internal set; }
    public string Source { get; internal set; } = "";
    public long SubmitLockWaiting { get; internal set; }
    public long SubmitLockAcquired { get; internal set; }
    public long Enqueued { get; internal set; }
    public long SubmitLockReleased { get; internal set; }
    public long DrainScheduled { get; internal set; }
    public long DrainResumed { get; internal set; }
    public long QueueLockWaiting { get; internal set; }
    public long QueueLockAcquired { get; internal set; }
    public long QueueLockReleased { get; internal set; }
    public long SelectStarted { get; internal set; }
    public long SelectEnded { get; internal set; }
    public long SnapshotStarted { get; internal set; }
    public long SnapshotEnded { get; internal set; }
    public long EligibilityStarted { get; internal set; }
    public long EligibilityEnded { get; internal set; }
    public bool? EligibilityPassed { get; internal set; }
    public long ExchangeResumed { get; internal set; }
    public long CompletionSignaled { get; internal set; }
    public long CompletionReturned { get; internal set; }
    public long StepEnded { get; internal set; }
    public long SubmitResumed { get; internal set; }
    public string SubmitOutcome { get; internal set; } = "NotCompleted";
    public PlcDeadlineTiming? DeadlineAtSubmitEnd { get; internal set; }
    public int PendingCount { get; internal set; }
    public bool SnapshotTruncated { get; internal set; }
    public PlcPendingTiming[] Pending { get; internal set; } = [];
    public string Outcome { get; internal set; } = "NotCompleted";
}
internal sealed record PlcPendingTiming(long RequestId, string Source, long Queued, bool Cancelled, bool HasEligibility);
