using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Gaode.Communication.Tests.Devices;

// Independent TCP capture and a finite delayed-request fault. No device-state fabrication.
internal sealed class PlcPolling013WireProbe(int target) : IAsyncDisposable
{
    internal sealed record Exchange(Guid Connection, byte Function, ushort Offset, ushort Count,
        long Received, long Forwarded, long Completed, string Request, string Response);
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stop = new();
    private readonly ConcurrentBag<Task> sessions = [];
    private Task accept = Task.CompletedTask;
    internal ConcurrentQueue<Exchange> Exchanges { get; } = new();
    internal Func<byte, ushort, int>? DelayBeforeForward { get; set; }
    internal int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
    internal void Start() { listener.Start(); accept = AcceptAsync(); }
    private async Task AcceptAsync()
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(stop.Token);
                sessions.Add(RelayAsync(client));
            }
        }
        catch (Exception e) when (e is OperationCanceledException or SocketException or ObjectDisposedException) { }
    }
    private async Task RelayAsync(TcpClient incoming)
    {
        using (incoming)
        using (var upstream = new TcpClient { NoDelay = true })
        {
            var connection = Guid.NewGuid();
            incoming.NoDelay = true;
            try
            {
                await upstream.ConnectAsync(IPAddress.Loopback, target, stop.Token);
                var from = incoming.GetStream(); var to = upstream.GetStream();
                while (!stop.IsCancellationRequested)
                {
                    var request = await Frame(from, stop.Token);
                    var received = Stopwatch.GetTimestamp();
                    var function = request[7]; var offset = BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(8, 2));
                    var count = BinaryPrimitives.ReadUInt16BigEndian(request.AsSpan(10, 2));
                    var delay = DelayBeforeForward?.Invoke(function, offset) ?? 0;
                    if (delay > 0) await Task.Delay(delay, stop.Token);
                    var forwarded = Stopwatch.GetTimestamp();
                    await to.WriteAsync(request, stop.Token);
                    var response = await Frame(to, stop.Token);
                    await from.WriteAsync(response, stop.Token);
                    Exchanges.Enqueue(new(connection, function, offset, count, received, forwarded,
                        Stopwatch.GetTimestamp(), Convert.ToHexString(request), Convert.ToHexString(response)));
                }
            }
            catch (Exception e) when (e is IOException or OperationCanceledException or SocketException or ObjectDisposedException) { }
        }
    }
    private static async Task<byte[]> Frame(NetworkStream stream, CancellationToken token)
    {
        var header = new byte[7]; await stream.ReadExactlyAsync(header, token);
        var frame = new byte[6 + BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2))];
        header.CopyTo(frame, 0); await stream.ReadExactlyAsync(frame.AsMemory(7), token); return frame;
    }
    internal void Save(string caseId)
    {
        var root = Environment.GetEnvironmentVariable("GAODE_009_EVIDENCE_ROOT")
            ?? throw new InvalidOperationException("013ComponentEvidenceRootRequired");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, caseId + "-" + Guid.NewGuid().ToString("N") + ".json"),
            System.Text.Json.JsonSerializer.Serialize(new { caseId, frequency = Stopwatch.Frequency, exchanges = Exchanges.ToArray() }));
    }
    public async ValueTask DisposeAsync()
    { stop.Cancel(); listener.Stop(); await accept; await Task.WhenAll(sessions); stop.Dispose(); }
}
