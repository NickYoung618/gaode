using System.Net;
using System.Net.Sockets;
using Gaode.Infrastructure.Devices.Plc;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Gaode.Communication.Tests.Devices;

[Collection("CommunicationTcp")]
public sealed partial class TransportFeedbackTests(ITestOutputHelper output)
{
    [Fact]
    public async Task MissingReadResponseLogsOriginalTimeoutAndChannelWithoutRetry()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var received = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var server = Task.Run(async () =>
        {
            using var socket = await listener.AcceptTcpClientAsync(cleanup.Token);
            var request = new byte[12];
            await socket.GetStream().ReadExactlyAsync(request, cleanup.Token);
            received.SetResult(request);
            try { await Task.Delay(Timeout.Infinite, cleanup.Token); }
            catch (OperationCanceledException) when (cleanup.IsCancellationRequested) { }
        });
        var log = new CapturingLogger();
        try
        {
            await using var client = new ModbusTcpClient("127.0.0.1", port, 1,
                TimeSpan.FromMilliseconds(1000), log, "business");
            var exception = await Assert.ThrowsAsync<TimeoutException>(() => client.ReadCoilsAsync(0, 16));
            Assert.IsAssignableFrom<OperationCanceledException>(exception.InnerException);
            var request = await received.Task.WaitAsync(cleanup.Token);
            Assert.Equal(1, request[7]);
            var failure = Assert.Single(log.Entries, x => x.Level == LogLevel.Error);
            Assert.IsAssignableFrom<OperationCanceledException>(failure.Exception);
            Assert.Contains("channel=business", failure.Message);
            Assert.Contains("phase=ReadHeader", failure.Message);
            Assert.Contains("deadlineMs=1000", failure.Message);
            Assert.Contains("deadlineCancelled=True", failure.Message);
            Assert.Contains("request=" + Convert.ToHexString(request), failure.Message);
            Assert.Single(client.Exchanges);
            Assert.Contains("CanceledException", client.Exchanges[0].Error!);
            var timing = Assert.Single(log.Entries, x => x.Message.StartsWith("PLC failure timing:"));
            var readStart = System.Text.RegularExpressions.Regex.Match(timing.Message,
                @"headerReadStartedTick=(\d+), headerReadTick=(\d+)");
            Assert.True(readStart.Success);
            Assert.True(long.Parse(readStart.Groups[1].Value) > 0);
            Assert.Equal("0", readStart.Groups[2].Value);
            Assert.Contains("channel=business", timing.Message);
            output.WriteLine(timing.Message);
            output.WriteLine(failure.Message);
            output.WriteLine(failure.Exception!.ToString());
        }
        finally
        {
            cleanup.Cancel();
            listener.Stop();
            await server;
        }
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
    }
}
