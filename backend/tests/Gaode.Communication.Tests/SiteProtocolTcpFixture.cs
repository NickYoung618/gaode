using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Gaode.Infrastructure.Devices.Plc;

namespace Gaode.Communication.Tests;

// Independent XLS register fixture: no production encoder or legacy Test map
// supplies expected words. Only confirmed transport and motion-clear behavior.
internal sealed class SiteProtocolTcpFixture : IAsyncDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource lifetime = new();
    private readonly ushort[] registers = new ushort[65536];
    private readonly object sync = new();
    private readonly List<Task> peers = [];
    private readonly Task serving, heartbeat;
    internal ConcurrentQueue<(int Offset, ushort[] Words, bool Accepted)> Writes { get; } = new();
    internal int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
    internal bool ExerciseConfirmedOperations { get; set; }
    internal int ResetEdges { get; private set; }
    internal int StartEdges { get; private set; }
    internal int MotionEdges { get; private set; }
    internal bool KeepSoftStopAsserted { get; set; }
    internal byte? ReadyOnReset { get; set; }
    internal int ResetReadyZeroReads { get; private set; }
    internal int ResetReadyOneReads { get; private set; }
    internal ConcurrentQueue<(byte PcReady, byte SoftStop, int Readbacks)> ResetPreconditions { get; } = new();
    private int resetPreconditionReadbacks;
    internal ConcurrentQueue<(ushort ZLow, ushort ZHigh, byte ZRequest, ushort ZFeedback)> StartClears { get; } = new();
    internal byte Byte(int mb) { lock (sync) return (byte)(registers[mb / 2] >> (mb % 2 * 8)); }

    internal SiteProtocolTcpFixture()
    {
        SetByte(6015, 1); SetByte(6016, 1);
        // Fixture coordinates only. Raw CDAB encoding of 1.25 = 0000 3FA0.
        foreach (var mb in new[] { 6064, 6076, 6084, 6088, 6092 })
        { registers[mb / 2] = 0; registers[mb / 2 + 1] = 0x3fa0; }
        listener.Start(); serving = ServeAsync(); heartbeat = HeartbeatAsync();
    }
    internal ModbusTcpClient Client() => new("127.0.0.1", Port, 1, TimeSpan.FromMilliseconds(1000));
    internal void SetWord(int memoryByte, ushort value) { lock (sync) registers[memoryByte / 2] = value; }
    internal ushort Word(int memoryByte) { lock (sync) return registers[memoryByte / 2]; }
    internal void SetByte(int memoryByte, byte value)
    {
        lock (sync)
        {
            var index = memoryByte / 2;
            registers[index] = memoryByte % 2 == 0 ? (ushort)((registers[index] & 0xff00) | value)
                : (ushort)((registers[index] & 0x00ff) | value << 8);
        }
    }
    private async Task HeartbeatAsync()
    {
        try
        {
            byte value = 0;
            while (true) { SetByte(6038, value ^= 1); await Task.Delay(100, lifetime.Token); }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    }
    private async Task ServeAsync()
    {
        try
        {
            while (true)
            {
                var client = await listener.AcceptTcpClientAsync(lifetime.Token);
                peers.Add(HandleAsync(client));
            }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    }
    private async Task HandleAsync(TcpClient client)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();
                while (true)
                {
                    var header = new byte[7]; await stream.ReadExactlyAsync(header, lifetime.Token);
                    var length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4));
                    if (length is < 2 or > 254 || header[6] != 1) throw new InvalidDataException("FixtureMbapInvalid");
                    var request = new byte[length - 1]; await stream.ReadExactlyAsync(request, lifetime.Token);
                    var response = Process(request);
                    BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(4), (ushort)(response.Length + 1));
                    await stream.WriteAsync(header, lifetime.Token); await stream.WriteAsync(response, lifetime.Token);
                }
            }
            catch (Exception e) when (e is IOException or OperationCanceledException or SocketException) { }
        }
    }
    private byte[] Process(byte[] request)
    {
        var function = request[0];
        var offset = BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(1));
        var value = BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(3));
        lock (sync)
        {
            if (function == 3)
            {
                if (ResetEdges > 0 && Byte(2009) == 1 && offset <= 6015 / 2 && offset + value > 6015 / 2)
                {
                    if (Byte(6015) == 0) ResetReadyZeroReads++;
                    else if (Byte(6015) == 1) ResetReadyOneReads++;
                }
                if (Byte(2006) == 1 && Byte(2008) == 0 && offset <= 2006 / 2 && offset + value > 2008 / 2)
                    resetPreconditionReadbacks++;
                var response = new byte[2 + value * 2]; response[0] = 3; response[1] = checked((byte)(value * 2));
                for (var i = 0; i < value; i++) BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(2 + i * 2), registers[offset + i]);
                return response;
            }
            var words = function == 6 ? new[] { value } : function == 16
                ? Enumerable.Range(0, value).Select(i => BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(6 + 2 * i))).ToArray() : [];
            var accepted = words.Length > 0 && offset >= 1000 && offset + words.Length <= 1028;
            Writes.Enqueue((offset, words, accepted));
            if (!accepted) return [(byte)(function | 0x80), 2];
            var priorReset = Byte(2009); var priorStart = Byte(2007);
            var priorAxes = new[] { 2001, 2002, 2003, 2004, 2005 }.ToDictionary(mb => mb, Byte);
            for (var i = 0; i < words.Length; i++) registers[offset + i] = words[i];
            if (KeepSoftStopAsserted && offset <= 2008 / 2 && offset + words.Length > 2008 / 2)
                SetByte(2008, 1);
            if (ExerciseConfirmedOperations)
            {
                if (priorReset == 0 && Byte(2009) != 0)
                {
                    ResetEdges++;
                    ResetPreconditions.Enqueue((Byte(2006), Byte(2008), resetPreconditionReadbacks));
                    if (ReadyOnReset is { } ready) SetByte(6015, ready);
                }
                if (priorStart == 0 && Byte(2007) != 0) StartEdges++;
                if (priorStart != 0 && Byte(2007) == 0)
                    StartClears.Enqueue((registers[6084 / 2], registers[6084 / 2 + 1], Byte(2003), registers[6044 / 2]));
            }
            // Confirmed axis request/feedback relations. This fixture does not
            // invent the initial/reset/soft-stop safety admission in PLC-Q4.
            foreach (var (mb, feedback) in new[] { (2001, 6040), (2002, 6042), (2003, 6044), (2004, 6046), (2005, 6048) })
                if (mb / 2 >= offset && mb / 2 < offset + words.Length)
                {
                    var requested = Byte(mb) != 0;
                    registers[feedback / 2] = requested && !ExerciseConfirmedOperations ? (ushort)1 : (ushort)0;
                    if (ExerciseConfirmedOperations && requested && priorAxes[mb] == 0)
                    {
                        MotionEdges++;
                        var target = new[] { 2024, 2028, 2032, 2036, 2040 }[mb - 2001];
                        var actual = new[] { 6064, 6076, 6084, 6088, 6092 }[mb - 2001];
                        var targetWords = new[] { registers[target / 2], registers[target / 2 + 1] };
                        _ = CompleteMotionAsync(mb, feedback, actual, targetWords);
                    }
                }
            return function == 6 ? request : [16, request[1], request[2], request[3], request[4]];
        }
    }
    private async Task CompleteMotionAsync(int request, int feedback, int actual, ushort[] target)
    {
        try
        {
            await Task.Delay(250, lifetime.Token);
            lock (sync)
                if (Byte(request) != 0 && Byte(2008) == 0)
                { registers[actual / 2] = target[0]; registers[actual / 2 + 1] = target[1]; registers[feedback / 2] = 1; }
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    }
    public async ValueTask DisposeAsync()
    {
        await lifetime.CancelAsync(); listener.Stop();
        await serving; await heartbeat; await Task.WhenAll(peers); lifetime.Dispose();
    }
}
