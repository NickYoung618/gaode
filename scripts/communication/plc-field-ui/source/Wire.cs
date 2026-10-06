using System.Buffers.Binary;
using System.Diagnostics;
using System.Net.Sockets;

namespace FieldUi;

// The Engine owns serialization across reads, byte read/modify/write, heartbeat and manual operations.
public sealed class Wire(Connection settings, Action<string, string, string?, int, int, string, double, string?> record) : IDisposable
{
    readonly TcpClient socket = new() { NoDelay = true };
    ushort transaction;
    public async Task Connect() { using var ct = new CancellationTokenSource(settings.TimeoutMs); await socket.ConnectAsync(settings.Host, settings.Port!.Value, ct.Token); }
    static ushort Word(byte[] bytes, int index) => BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(index, 2));
    static void Put(byte[] bytes, int index, ushort value) => BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(index, 2), value);
    static byte[] Pair(byte function, int address, int value) { byte[] p = new byte[5]; p[0] = function; Put(p, 1, checked((ushort)address)); Put(p, 3, checked((ushort)value)); return p; }
    public async Task<ushort[]> Read(int address, int count, string area, string context, string? op = null)
    {
        if (count is < 1 or > 125) throw new ArgumentOutOfRangeException(nameof(count));
        var response = await Exchange(Pair(area == "InputRegister" ? (byte)4 : (byte)3, address, count), context, op);
        if (response.Length != 2 + count * 2 || response[1] != count * 2) throw new IOException("Modbus读响应长度不匹配。");
        return Enumerable.Range(0, count).Select(i => Word(response, i * 2 + 2)).ToArray();
    }
    public async Task Write(int address, ushort[] values, string context, string? op)
    {
        byte[] p;
        if (values.Length == 1) p = Pair(6, address, values[0]);
        else
        {
            p = new byte[6 + values.Length * 2]; Pair(16, address, values.Length).CopyTo(p, 0); p[5] = (byte)(values.Length * 2);
            for (var i = 0; i < values.Length; i++) Put(p, 6 + i * 2, values[i]);
        }
        var response = await Exchange(p, context, op);
        if (!response.SequenceEqual(p.Take(5))) throw new IOException("Modbus写应答与请求不匹配，执行结果未知。");
    }
    async Task<byte[]> Exchange(byte[] pdu, string context, string? op)
    {
        var watch = Stopwatch.StartNew();
        var tid = unchecked(++transaction);
        byte[] request = new byte[7 + pdu.Length]; Put(request, 0, tid); Put(request, 4, (ushort)(pdu.Length + 1)); request[6] = (byte)settings.UnitId!.Value; pdu.CopyTo(request, 7);
        var received = new List<byte>();
        using var timeout = new CancellationTokenSource(settings.TimeoutMs);
        try
        {
            record("TX", context, op, tid, pdu[0], Convert.ToHexString(request), 0, null);
            await socket.GetStream().WriteAsync(request, timeout.Token);
            async Task<byte[]> ReadExact(int size)
            {
                var buffer = new byte[size]; var offset = 0;
                while (offset < size)
                {
                    var n = await socket.GetStream().ReadAsync(buffer.AsMemory(offset), timeout.Token);
                    if (n == 0) throw new IOException("PLC关闭连接。");
                    received.AddRange(buffer.AsSpan(offset, n).ToArray()); offset += n;
                }
                return buffer;
            }
            var h = await ReadExact(7);
            if (Word(h, 0) != tid || Word(h, 2) != 0 || h[6] != request[6] || Word(h, 4) is < 2 or > 254) throw new IOException("Modbus事务号/协议号/站号/长度不匹配。");
            var body = await ReadExact(Word(h, 4) - 1);
            record("RX", context, op, tid, body[0], Convert.ToHexString(received.ToArray()), watch.Elapsed.TotalMilliseconds, null);
            if (body[0] == (pdu[0] | 128)) throw new IOException($"PLC异常响应：功能码0x{pdu[0]:X2}，异常码0x{(body.Length > 1 ? body[1] : 0):X2}。");
            if (body[0] != pdu[0]) throw new IOException("Modbus响应功能码不匹配。");
            return body;
        }
        catch (Exception e)
        {
            var message = e is OperationCanceledException ? $"PLC通信超过{settings.TimeoutMs}ms，写入结果可能未知。" : e.Message;
            record("ERROR", context, op, tid, pdu[0], Convert.ToHexString(received.ToArray()), watch.Elapsed.TotalMilliseconds, message);
            throw new IOException(message, e);
        }
    }
    public void Dispose() => socket.Dispose();
}
