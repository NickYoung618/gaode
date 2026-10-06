using System.Collections.Concurrent;
using Gaode.Application.Station01;
using Gaode.Domain.Station01;
using Gaode.Host.Lifecycle;
using Gaode.Integration.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Gaode.Integration.Tests.Hosting;

public sealed class ShutdownDiagnosticsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShutdownLogUsesFinalSnapshotAndSeparatesSignalFromCompletion(bool alreadyExpired)
    {
        var logger = new CapturingLogger();
        await using var fixture = await Station01HostFixture.CreateAsync(services =>
        {
            services.RemoveAll<ILogger<Station01HostedService>>();
            services.AddSingleton<ILogger<Station01HostedService>>(logger);
        });
        var lifecycle = fixture.Host.Services.GetRequiredService<Station01HostedService>();
        var coordinator = fixture.Host.Services.GetRequiredService<Station01Coordinator>();
        using var releaseConsumer = new ManualResetEventSlim(false);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task<RunSnapshot>? mutation = null;
        if (alreadyExpired)
        {
            // Deterministically separate stopped run producers from a still-running consumer.
            var id = Guid.NewGuid();
            Assert.True(coordinator.TryRegister(new(id, "log-test", "test", RunState.Created,
                0, 0, TerminalOutcome.None, false, default, default, default, default, default,
                null, null, null, [])));
            mutation = coordinator.SetAsync(id, value =>
            {
                entered.TrySetResult();
                if (!releaseConsumer.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Consumer test watchdog");
                return value;
            });
        }
        try
        {
        if (mutation is not null) await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        if (alreadyExpired) budget.Cancel();
        await lifecycle.StopAsync(budget.Token).WaitAsync(TimeSpan.FromSeconds(5));
        var snapshot = Assert.IsType<Station01ShutdownSnapshot>(lifecycle.LastShutdown);
        var row = Assert.Single(logger.Rows, x => x.ContainsKey("Flows"));
        Assert.Equal(snapshot.FlowsStopped, row["Flows"]);
        Assert.Equal(snapshot.RunsStopped, row["Runs"]);
        Assert.Equal(snapshot.ConsumerStopped, row["Consumer"]);
        Assert.Equal(snapshot.ConsumerStopRequested, row["ConsumerStopRequested"]);
        Assert.Equal(snapshot.WritesDrained, row["Writes"]);
        Assert.Equal(snapshot.ResourcesDrained, row["Resources"]);
        Assert.Equal(snapshot.RemainingAlgorithmExecutions, row["AlgorithmsRemaining"]);
        if (alreadyExpired)
        {
            Assert.True(snapshot.RunsStopped);
            Assert.True(snapshot.ConsumerStopRequested);
            Assert.False(snapshot.ConsumerStopped);
            Assert.False(snapshot.FlowsStopped);
        }
        Assert.False((bool)row["PhysicalStop"]!);
        Assert.False((bool)row["Released"]!);
        await File.WriteAllTextAsync(Path.Combine(fixture.StoreRoot, "shutdown-log-evidence.json"),
            System.Text.Json.JsonSerializer.Serialize(new { alreadyExpired, snapshot, log = row }));
        }
        finally
        {
            releaseConsumer.Set();
            if (mutation is not null) await mutation.WaitAsync(TimeSpan.FromSeconds(5));
            try { await coordinator.ConsumerCompletion.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (OperationCanceledException) when (coordinator.ConsumerStopped) { }
        }
    }

    private sealed class CapturingLogger : ILogger<Station01HostedService>
    {
        public ConcurrentQueue<Dictionary<string, object?>> Rows { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (state is IEnumerable<KeyValuePair<string, object?>> values)
                Rows.Enqueue(values.ToDictionary(x => x.Key, x => x.Value));
        }
    }
}
