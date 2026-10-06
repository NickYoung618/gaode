using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace Gaode.Communication.Tests.Devices;

// Finite feedback faults. Only selected read replies are held at independently
// specified values; actual writes, execution, heartbeat and all other responses are relayed.
internal sealed class StageFeedbackFaultProxy(int upstreamPort, StageFeedbackFault fault) : IAsyncDisposable
{
    private readonly TcpListener listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource stop = new();
    private readonly ConcurrentBag<Task> sessions = [];
    private Task accept = Task.CompletedTask;
    public ConcurrentQueue<string> MutatedReplies { get; } = new();
    private volatile bool armed;
    public bool Armed { get => armed; set => armed = value; }
    public ConcurrentQueue<FlipWireRequest> FlipWireRequests { get; } = new();
    public ConcurrentQueue<FlipWireReply> FlipWireReplies { get; } = new();
    private bool pickDispatched;
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
                var connection = Guid.NewGuid();
                var peer = downstream.Client.RemoteEndPoint!.ToString()!;
                while (!stop.IsCancellationRequested)
                {
                    var header = new byte[7];
                    await input.ReadExactlyAsync(header, stop.Token);
                    var body = new byte[BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2)) - 1];
                    await input.ReadExactlyAsync(body, stop.Token);
                    var request = header.Concat(body).ToArray();
                    if (fault == StageFeedbackFault.OldFlipCompleted)
                        FlipWireRequests.Enqueue(new(connection, peer, Stopwatch.GetTimestamp(),
                            BinaryPrimitives.ReadUInt16BigEndian(header), Convert.ToHexString(request)));
                    if (Armed && body.Length == 5 && body[0] == 6 &&
                        BinaryPrimitives.ReadUInt16BigEndian(body.AsSpan(1, 2)) == 0x20 &&
                        BinaryPrimitives.ReadUInt16BigEndian(body.AsSpan(3, 2)) == 1)
                        pickDispatched = true;
                    {
                        await output.WriteAsync(request, stop.Token);
                        await output.ReadExactlyAsync(header, stop.Token);
                        var response = new byte[BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2)) - 1];
                        await output.ReadExactlyAsync(response, stop.Token);
                        if (fault == StageFeedbackFault.OldFlipCompleted && body.Length == 5 && body[0] == 3)
                        {
                            var offset = BinaryPrimitives.ReadUInt16BigEndian(body.AsSpan(1, 2));
                            var count = BinaryPrimitives.ReadUInt16BigEndian(body.AsSpan(3, 2));
                            // Test protocol document 22 => offset 21. Completed=2 is the independent fault oracle.
                            if (count is >= 1 and <= 125 && offset <= 21 && offset + count > 21 &&
                                response.Length == 2 + count * 2 && response[0] == 3 && response[1] == count * 2 &&
                                header.AsSpan(0, 4).SequenceEqual(request.AsSpan(0, 4)) && header[6] == request[6])
                            {
                                var original = Convert.ToHexString(header.Concat(response).ToArray());
                                var injected = Armed;
                                if (injected)
                                {
                                    BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(2 + (21 - offset) * 2, 2), 2);
                                    MutatedReplies.Enqueue("Controlled test fault: old Flip Completed=2; not a PLC fact");
                                }
                                // Record the exact buffer about to enter TCP; the product journal proves receipt.
                                FlipWireReplies.Enqueue(new(connection, peer, Stopwatch.GetTimestamp(),
                                    BinaryPrimitives.ReadUInt16BigEndian(header), Convert.ToHexString(request),
                                    original, Convert.ToHexString(header.Concat(response).ToArray()), injected));
                            }
                        }
                        if (Armed && fault == StageFeedbackFault.DropReadResponse && body[0] == 3)
                        {
                            MutatedReplies.Enqueue("Actual read response withheld; heartbeat relayed independently");
                            await Task.Delay(Timeout.InfiniteTimeSpan, stop.Token);
                        }
                        if (Armed && body.Length == 5 && body[0] == 3 && response[0] == 3)
                        {
                            var offset = BinaryPrimitives.ReadUInt16BigEndian(body.AsSpan(1, 2));
                            var count = BinaryPrimitives.ReadUInt16BigEndian(body.AsSpan(3, 2));
                            if (fault == StageFeedbackFault.FrozenUnloadCycle && offset <= 0x7F && offset + count > 0x7F)
                            {
                                BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(2 + (0x7F - offset) * 2, 2), 1);
                                MutatedReplies.Enqueue("X remains previous Arrived");
                            }
                            if (fault == StageFeedbackFault.MissingPickTarget && pickDispatched)
                                for (var number = 0xD; number <= 0x12; number++)
                                    if (number - 1 >= offset && number - 1 < offset + count)
                                    {
                                        // Old XYZ = 0, never the current pick point (100,100,150).
                                        BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(2 + (number - 1 - offset) * 2, 2), 0);
                                        MutatedReplies.Enqueue("Old XYZ held until/after completion");
                                    }
                        }
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

internal enum StageFeedbackFault { FrozenUnloadCycle, MissingPickTarget, DropReadResponse, OldFlipCompleted }

internal sealed record FlipWireRequest(Guid Connection, string Peer, long ReceivedTick, ushort Transaction, string Frame);
internal sealed record FlipWireReply(Guid Connection, string Peer, long ForwardAttemptTick, ushort Transaction,
    string Request, string Before, string After, bool ControlledFault);
