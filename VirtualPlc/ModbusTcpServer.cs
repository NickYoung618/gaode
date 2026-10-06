using Gaode.Plc.Protocol;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using Gaode.Diagnostics;

namespace VirtualPlc;

public sealed class ModbusTcpServer : BackgroundService
{
    private readonly PlcDataStore _store;
    private readonly ModbusOptions _options;
    private readonly ILogger<ModbusTcpServer> _logger;
    private TcpListener? _listener;
    private volatile bool _stopRequested;
    private readonly HeartbeatDiagnosticWindow _diagnostics;

    public ModbusTcpServer(
        PlcDataStore store,
        IOptions<ModbusOptions> options,
        ILogger<ModbusTcpServer> logger)
    {
        _store = store;
        _options = options.Value;
        _logger = logger;
        _diagnostics = store.HeartbeatDiagnostics(logger);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var address = IPAddress.Parse(_options.ListenAddress);
        _listener = new TcpListener(address, _options.Port);
        _listener.Start();
        _logger.LogInformation(
            "Virtual PLC Modbus TCP listening on {Address}:{Port}, UnitId={UnitId}",
            address, _options.Port, _options.UnitId);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(stoppingToken);
                _ = HandleClientSafelyAsync(client, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (SocketException) when (stoppingToken.IsCancellationRequested || _stopRequested)
        {
        }
        finally
        {
            _listener.Stop();
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _stopRequested = true;
        _listener?.Stop();
        await base.StopAsync(cancellationToken);
        await _diagnostics.FlushAsync();
    }

    private async Task HandleClientSafelyAsync(TcpClient client, CancellationToken stoppingToken)
    {
        var remote = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
        _logger.LogInformation("Modbus client connected at {AtUtc:o}: {Remote}", DateTimeOffset.UtcNow, remote);

        using (client)
        {
            client.NoDelay = true;
            try
            {
                await HandleClientAsync(client, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (EndOfStreamException)
            {
                _logger.LogInformation("Modbus peer closed stream at {AtUtc:o}: {Remote}", DateTimeOffset.UtcNow, remote);
            }
            catch (IOException exception)
            {
                _diagnostics.Record("socket-failure", new { remote, error = exception.ToString() });
                _diagnostics.Capture("SocketFailure", true, new { remote });
                _logger.LogWarning(exception, "Modbus I/O failed at {AtUtc:o}: {Remote}", DateTimeOffset.UtcNow, remote);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Modbus client failed: {Remote}", remote);
            }
        }

        _logger.LogInformation("Modbus client disconnected at {AtUtc:o}: {Remote}", DateTimeOffset.UtcNow, remote);
        // The harness terminates the server after its clients close. Persist the bounded
        // diagnostic adjunct here, outside request handling and without changing the API.
        Gaode.Diagnostics.PlcTimingTrace.Flush();
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        var stream = client.GetStream();
        var connectionId = Guid.NewGuid();
        var remoteEndpoint = client.Client.RemoteEndPoint?.ToString();
        var localEndpoint = client.Client.LocalEndPoint?.ToString();
        var header = new byte[7];
        long lastHeartbeatRead = 0, lastHeartbeatWrite = 0;
        long lastEchoGapLog = 0;
        int suppressedEchoGaps = 0;
        DateTimeOffset? lastHeartbeatWriteUtc = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            var headerReadStartedTick = Stopwatch.GetTimestamp();
            await ReadExactlyAsync(stream, header, cancellationToken);
            var headerReadUtc = DateTimeOffset.UtcNow;
            var headerReadTick = Stopwatch.GetTimestamp();

            var transactionId = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(0, 2));
            var protocolId = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2, 2));
            var length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2));
            var unitId = header[6];

            if (protocolId != 0 || length < 2 || length > 254)
            {
                throw new IOException("Invalid Modbus TCP header.");
            }

            var pdu = new byte[length - 1];
            await ReadExactlyAsync(stream, pdu, cancellationToken);
            var receivedAtUtc = DateTimeOffset.UtcNow;
            var processingStarted = Stopwatch.GetTimestamp();
            var heartbeatRead = pdu.Length == 5 && pdu[0] == 1 &&
                BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(1, 2)) == 0 &&
                BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(3, 2)) == 1;
            var heartbeatWrite = pdu.Length == 5 && pdu[0] == 5 &&
                BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(1, 2)) ==
                    PlcAddressMap.ToPduOffset(PlcAddressMap.Coils.PcHeartbeatResp);
            var requestGapMs = heartbeatRead && lastHeartbeatRead != 0
                ? Stopwatch.GetElapsedTime(lastHeartbeatRead, processingStarted).TotalMilliseconds : 0;
            var responseGapMs = heartbeatWrite && lastHeartbeatWrite != 0
                ? Stopwatch.GetElapsedTime(lastHeartbeatWrite, processingStarted).TotalMilliseconds : 0;
            var previousHeartbeatWriteUtc = lastHeartbeatWriteUtc;
            if (heartbeatRead) lastHeartbeatRead = processingStarted;
            if (heartbeatWrite) { lastHeartbeatWrite = processingStarted; lastHeartbeatWriteUtc = receivedAtUtc; }

            byte[] responsePdu;
            if (unitId != _options.UnitId)
            {
                responsePdu = ExceptionResponse(pdu[0], 0x0B);
            }
            else
            {
                _store.SetTransportContext(connectionId, transactionId, pdu[0]);
                try { responsePdu = ProcessRequest(pdu, transactionId); }
                finally { _store.ClearTransportContext(); }
            }

            var processedTick = Stopwatch.GetTimestamp();
            var response = new byte[7 + responsePdu.Length];
            BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(0, 2), transactionId);
            BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(2, 2), 0);
            BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(4, 2), (ushort)(responsePdu.Length + 1));
            response[6] = unitId;
            responsePdu.CopyTo(response, 7);
            // Record the received and processed write independently of whether a
            // response is delivered. A Test disconnect must not erase a real write
            // or claim that an unsent reply was sent.
            _store.RecordWriteResponse(pdu, responsePdu, connectionId, transactionId, receivedAtUtc, null, [..header, ..pdu], null);
            var injected = _store.FeedbackFaults.AfterProcessedRequest(pdu, responsePdu, connectionId, transactionId, receivedAtUtc, response);
            if (injected == TestResponseDisposition.Close) return;
            if (injected == TestResponseDisposition.Delay)
            {
                await Task.Delay(1500, cancellationToken); // Test delay; Host I/O budget remains 1000ms.
                _store.FeedbackFaults.MarkDelayElapsed(connectionId, transactionId);
            }
            var responseWriteStartedTick = Stopwatch.GetTimestamp();
            await stream.WriteAsync(response, cancellationToken);
            var responseSentTick = Stopwatch.GetTimestamp();
            var responseSentUtc = DateTimeOffset.UtcNow;
            if (Gaode.Diagnostics.PlcTimingTrace.Enabled) Gaode.Diagnostics.PlcTimingTrace.Record("plc-exchange", new
            {
                connectionId, remoteEndpoint, localEndpoint, transaction = transactionId, unitId, function = pdu[0],
                offset = pdu.Length >= 3 ? BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(1, 2)) : 0,
                count = pdu.Length >= 5 ? BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(3, 2)) : 0,
                headerReadStartedTick, headerReadTick, receivedTick = processingStarted,
                processedTick, responseWriteStartedTick, responseSentTick, receivedAtUtc, responseSentUtc
            });
            _store.RecordWriteResponse(pdu, responsePdu, connectionId, transactionId, receivedAtUtc, responseSentUtc, [..header, ..pdu], response);
            var elapsed = Stopwatch.GetElapsedTime(processingStarted, responseSentTick).TotalMilliseconds;
            // Keep business responses in the same bounded ring so a failed business
            // exchange can be compared with actual server receipt and response times.
            _diagnostics.Record("modbus-response", new { connectionId, remoteEndpoint, localEndpoint,
                    transaction = transactionId, unitId, function = pdu[0], headerReadStartedTick, headerReadUtc, headerReadTick,
                    receivedAtUtc, receivedTick = processingStarted, responseSentUtc, responseSentTick,
                    processedTick, responseWriteStartedTick, requestGapMs, responseGapMs, processingMs = elapsed,
                    requestProcessingMs = Stopwatch.GetElapsedTime(processingStarted, processedTick).TotalMilliseconds,
                    responseWriteMs = Stopwatch.GetElapsedTime(responseWriteStartedTick, responseSentTick).TotalMilliseconds,
                    request = Convert.ToHexString(pdu), response = Convert.ToHexString(response) });
            if (heartbeatRead || heartbeatWrite)
            {
                // Arrival spacing is an observation, not a missed Host plannedDue.
                // Normal 300ms reads must not create a false delay window.
                if (responseGapMs >= 1500 || elapsed >= 250)
                    _diagnostics.Capture("ModbusHeartbeatDelay", context: new { connectionId, remoteEndpoint, transactionId });
            }
            if (heartbeatWrite && responseGapMs >= 1500)
            {
                if (lastEchoGapLog == 0 || Stopwatch.GetElapsedTime(lastEchoGapLog, responseSentTick) >= TimeSpan.FromSeconds(5))
                {
                    _logger.LogWarning("Heartbeat Modbus echo arrival gap: previousReceivedUtc={PreviousReceivedUtc:o}, receivedAtUtc={ReceivedAtUtc:o}, responseSentUtc={ResponseSentUtc:o}, receivedTick={ReceivedTick}, responseSentTick={ResponseSentTick}, tickFrequency={TickFrequency}, transaction={Transaction}, gapMs={GapMs:F1}, processingMs={ProcessingMs:F1}, value={Value}, suppressedEchoGaps={Suppressed}",
                        previousHeartbeatWriteUtc, receivedAtUtc, responseSentUtc, processingStarted,
                        responseSentTick, Stopwatch.Frequency, transactionId, responseGapMs, elapsed,
                        BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(3, 2)) == 0xFF00, suppressedEchoGaps);
                    lastEchoGapLog = responseSentTick;
                    suppressedEchoGaps = 0;
                }
                else suppressedEchoGaps++;
            }
            if (elapsed >= 250)
                _logger.LogWarning("Slow Modbus response: receivedAtUtc={ReceivedAtUtc:o}, sentAtUtc={SentAtUtc:o}, peer={Remote}, transaction={Transaction}, function={Function}, processingMs={ProcessingMs}, response={Response}",
                    receivedAtUtc, DateTimeOffset.UtcNow, client.Client.RemoteEndPoint,
                    transactionId, pdu[0], elapsed, Convert.ToHexString(response));
        }
    }

    private byte[] ProcessRequest(ReadOnlySpan<byte> pdu, ushort transactionId)
    {
        if (pdu.Length == 0)
        {
            return ExceptionResponse(0, 0x03);
        }

        try
        {
            return pdu[0] switch
            {
                0x01 => ReadCoils(pdu),
                0x03 => ReadHoldingRegisters(pdu),
                0x05 => WriteSingleCoil(pdu, transactionId),
                0x06 => WriteSingleRegister(pdu),
                0x0F => WriteMultipleCoils(pdu),
                0x10 => WriteMultipleRegisters(pdu),
                _ => ExceptionResponse(pdu[0], 0x01)
            };
        }
        catch (ArgumentOutOfRangeException)
        {
            return ExceptionResponse(pdu[0], 0x02);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Invalid Modbus request, function {Function}", pdu[0]);
            return ExceptionResponse(pdu[0], 0x03);
        }
    }

    private byte[] ReadCoils(ReadOnlySpan<byte> pdu)
    {
        if (pdu.Length != 5)
        {
            return ExceptionResponse(0x01, 0x03);
        }

        var start = BinaryPrimitives.ReadUInt16BigEndian(pdu.Slice(1, 2));
        var count = BinaryPrimitives.ReadUInt16BigEndian(pdu.Slice(3, 2));
        if (count is < 1 or > 2000)
        {
            return ExceptionResponse(0x01, 0x03);
        }

        var values = _store.ReadCoils(start, count);
        var byteCount = (values.Length + 7) / 8;
        var response = new byte[2 + byteCount];
        response[0] = 0x01;
        response[1] = (byte)byteCount;
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i])
            {
                response[2 + i / 8] |= (byte)(1 << (i % 8));
            }
        }

        return response;
    }

    private byte[] ReadHoldingRegisters(ReadOnlySpan<byte> pdu)
    {
        if (pdu.Length != 5)
        {
            return ExceptionResponse(0x03, 0x03);
        }

        var start = BinaryPrimitives.ReadUInt16BigEndian(pdu.Slice(1, 2));
        var count = BinaryPrimitives.ReadUInt16BigEndian(pdu.Slice(3, 2));
        if (count is < 1 or > 125)
        {
            return ExceptionResponse(0x03, 0x03);
        }

        var values = _store.ReadHoldingRegisters(start, count);
        var response = new byte[2 + values.Length * 2];
        response[0] = 0x03;
        response[1] = (byte)(values.Length * 2);
        for (var i = 0; i < values.Length; i++)
        {
            BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(2 + i * 2, 2), values[i]);
        }

        return response;
    }

    private byte[] WriteSingleCoil(ReadOnlySpan<byte> pdu, ushort transactionId)
    {
        if (pdu.Length != 5)
        {
            return ExceptionResponse(0x05, 0x03);
        }

        var offset = BinaryPrimitives.ReadUInt16BigEndian(pdu.Slice(1, 2));
        var encoded = BinaryPrimitives.ReadUInt16BigEndian(pdu.Slice(3, 2));
        if (encoded is not (0x0000 or 0xFF00))
        {
            return ExceptionResponse(0x05, 0x03);
        }

        if (!_store.TryWriteCoilsFromPc(offset, new[] { encoded == 0xFF00 }, transactionId))
        {
            return ExceptionResponse(0x05, 0x02);
        }

        return pdu.ToArray();
    }

    private byte[] WriteSingleRegister(ReadOnlySpan<byte> pdu)
    {
        if (pdu.Length != 5)
        {
            return ExceptionResponse(0x06, 0x03);
        }

        var offset = BinaryPrimitives.ReadUInt16BigEndian(pdu.Slice(1, 2));
        var value = BinaryPrimitives.ReadUInt16BigEndian(pdu.Slice(3, 2));
        if (!_store.TryWriteHoldingRegistersFromPc(offset, new[] { value }))
        {
            return ExceptionResponse(0x06, 0x02);
        }

        return pdu.ToArray();
    }

    private byte[] WriteMultipleCoils(ReadOnlySpan<byte> pdu)
    {
        if (pdu.Length < 6)
        {
            return ExceptionResponse(0x0F, 0x03);
        }

        var start = BinaryPrimitives.ReadUInt16BigEndian(pdu.Slice(1, 2));
        var count = BinaryPrimitives.ReadUInt16BigEndian(pdu.Slice(3, 2));
        var byteCount = pdu[5];
        if (count is < 1 or > 1968 || byteCount != (count + 7) / 8 || pdu.Length != 6 + byteCount)
        {
            return ExceptionResponse(0x0F, 0x03);
        }

        var values = new bool[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = (pdu[6 + i / 8] & (1 << (i % 8))) != 0;
        }

        if (!_store.TryWriteCoilsFromPc(start, values))
        {
            return ExceptionResponse(0x0F, 0x02);
        }

        return WriteAcknowledgement(0x0F, start, count);
    }

    private byte[] WriteMultipleRegisters(ReadOnlySpan<byte> pdu)
    {
        if (pdu.Length < 6)
        {
            return ExceptionResponse(0x10, 0x03);
        }

        var start = BinaryPrimitives.ReadUInt16BigEndian(pdu.Slice(1, 2));
        var count = BinaryPrimitives.ReadUInt16BigEndian(pdu.Slice(3, 2));
        var byteCount = pdu[5];
        if (count is < 1 or > 123 || byteCount != count * 2 || pdu.Length != 6 + byteCount)
        {
            return ExceptionResponse(0x10, 0x03);
        }

        var values = new ushort[count];
        for (var i = 0; i < count; i++)
        {
            values[i] = BinaryPrimitives.ReadUInt16BigEndian(pdu.Slice(6 + i * 2, 2));
        }

        if (!_store.TryWriteHoldingRegistersFromPc(start, values))
        {
            return ExceptionResponse(0x10, 0x02);
        }

        return WriteAcknowledgement(0x10, start, count);
    }

    private static byte[] WriteAcknowledgement(byte function, ushort start, ushort count)
    {
        var response = new byte[5];
        response[0] = function;
        BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(1, 2), start);
        BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(3, 2), count);
        return response;
    }

    private static byte[] ExceptionResponse(byte function, byte exceptionCode) =>
        new[] { (byte)(function | 0x80), exceptionCode };

    private static async Task ReadExactlyAsync(
        NetworkStream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var read = 0;
        while (read < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer[read..], cancellationToken);
            if (count == 0)
            {
                throw new EndOfStreamException();
            }

            read += count;
        }
    }
}
