using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Gaode.Communication.Tests.Devices;

// One finite receiver fault: reject only the stage preflight's XY-state read.
// All other packets (including observation and heartbeat) go to the actual VirtualPlc.
internal sealed class PreflightReadFaultProxy(int upstreamPort) : IAsyncDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stop = new();
    private readonly ConcurrentBag<Task> sessions = [];
    private Task accept = Task.CompletedTask;
    public ConcurrentQueue<long> RejectedAt { get; } = new();
    public bool Armed { get; set; }
    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
    public void Start() { listener.Start(); accept = AcceptAsync(); }
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
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
    }
    private async Task RelayAsync(TcpClient downstream)
    {
        using (downstream)
        using (var upstream = new TcpClient { NoDelay = true })
        {
            downstream.NoDelay = true;
            try
            {
                await upstream.ConnectAsync(IPAddress.Loopback, upstreamPort, stop.Token);
                var input = downstream.GetStream(); var output = upstream.GetStream();
                while (!stop.IsCancellationRequested)
                {
                    var header = new byte[7];
                    await input.ReadExactlyAsync(header, stop.Token);
                    var body = new byte[BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2)) - 1];
                    await input.ReadExactlyAsync(body, stop.Token);
                    if (Armed && body is [3, 0, 1, 0, 1])
                    {
                        RejectedAt.Enqueue(Stopwatch.GetTimestamp());
                        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(4, 2), 3);
                        await input.WriteAsync(header, stop.Token);
                        await input.WriteAsync(new byte[] { 0x83, 4 }, stop.Token);
                    }
                    else
                    {
                        await output.WriteAsync(header.Concat(body).ToArray(), stop.Token);
                        await output.ReadExactlyAsync(header, stop.Token);
                        var response = new byte[BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2)) - 1];
                        await output.ReadExactlyAsync(response, stop.Token);
                        await input.WriteAsync(header.Concat(response).ToArray(), stop.Token);
                    }
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
            catch (IOException) { /* Peer close is expected after formal device disposal. */ }
        }
    }
    public async ValueTask DisposeAsync()
    {
        stop.Cancel();
        await accept;
        listener.Stop();
        await Task.WhenAll(sessions);
        stop.Dispose();
    }
}
