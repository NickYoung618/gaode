using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gaode.Application.Station01;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Host.Api;
using Gaode.Integration.Tests.Support;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Gaode.Integration.Tests.Station01;

public sealed class RuntimeLogFlowIntegrationTests
{
    [Fact]
    public async Task Current007ReferencesProduceCorrelatedLogsThroughFinalUnload()
    {
        var log = new CaptureLog();
        using var diagnostic = RecipeBindingTestSupport.Subscribe(log);
        await using var plc = await VirtualPlcFixture.CreateAsync(profile: "runtime-log");
        var port = plc.Port;
        {
            await using var fixture = await Station01HostFixture.CreateAsync(
                simulationId: "s01-sim-virtual-loop", mode: "VirtualPlcIntegration",
                publicId: "s01-public-virtual-loop", budgetId: "s01-budget-virtual-loop", plcPort: port,
                configurationFeature: "007-station01-integrated-loop", publicVersion: "1.2.0",
                budgetVersion: "2.0.0", simulationVersion: "2.0.0");
            var requestId = "runtime-log-" + Guid.NewGuid().ToString("N");
            var response = await fixture.Client.PostAsJsonAsync("/api/v1/station01/runs",
                new StartPublicRequest(requestId, StartRunContextJson.Create(occupiedSlots: ["P01"]),
                    new ConfigReference("s01-public-virtual-loop", "1.2.0"),
                    new ConfigReference("s01-budget-virtual-loop", "3.0.0"),
                    new ConfigReference("s01-sim-virtual-loop", "3.0.0")));
            Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
            var receipt = (await response.Content.ReadFromJsonAsync<StartReceipt>())!;
            try
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
                RunApiSnapshot snapshot;
                do
                {
                    await Task.Delay(50, deadline.Token);
                    snapshot = (await fixture.Client.GetFromJsonAsync<RunApiSnapshot>(
                        $"/api/v1/station01/runs/{receipt.RunId:D}", deadline.Token))!;
                    Assert.False(snapshot.State is RunState.Blocked or RunState.RecoveryRequired,
                        $"Stopped at {snapshot.State}: {snapshot.ErrorCode}");
                } while (snapshot.State != RunState.AwaitingManualRemoval);
                var final = await fixture.Client.PostAsJsonAsync(
                    $"/api/v1/station01/runs/{receipt.RunId:D}/manual-removal-confirmations",
                    new ManualTrayRemovalApiRequest("runtime-log-confirm", snapshot.ObservedRevision,
                        "Isolated Test client confirmation, not a physical operator"), deadline.Token);
                Assert.Equal(HttpStatusCode.Accepted, final.StatusCode);
            }
            finally { log.Save(receipt.RunId); }

            var saved = File.ReadAllLines(log.PathFor(receipt.RunId))
                .Select(line => JsonSerializer.Deserialize<JsonElement>(line)).ToArray();
            foreach (var step in new[] { "RunExecution", "Configuration", "StartupReadiness", "StartAndClamp",
                "FixedMove", "Capture", "Algorithm", "RecipeBinding", "CriticalSave", "HandoffSave",
                "DetectionSortingUnload", "StageEventSave", "UnlockObservation", "ManualRemovalConfirmation" })
                Assert.Contains(saved, x => x.GetProperty("step").GetString() == step);
            Assert.Contains(saved, x => x.GetProperty("step").GetString() == "ManualRemovalConfirmation" &&
                x.GetProperty("outcome").GetString() == "Returned" &&
                x.GetProperty("facts").GetProperty("status").GetString() == "FinalUnloadCompleted");
            Assert.Contains(saved, x => x.GetProperty("step").GetString() == "RunExecution" &&
                x.GetProperty("facts").TryGetProperty("RequestId", out var id) && id.GetString() == requestId);
        }
    }

    private sealed class CaptureLog : ILogger
    {
        private readonly ConcurrentQueue<string> entries = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var text = formatter(state, exception);
            if (text.StartsWith("RuntimeFlow ", StringComparison.Ordinal)) entries.Enqueue(text["RuntimeFlow ".Length..]);
        }
        internal string PathFor(Guid runId) => Path.Combine(Station01HostFixture.FindWorkspace(), "artifacts",
            "station01-007", "runtime-log-20260924", "diagnostic-samples", "normal-loop-" + runId.ToString("N") + ".jsonl");
        internal void Save(Guid runId)
        {
            var path = PathFor(runId);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllLines(path, entries.Where(x => x.Contains(runId.ToString(), StringComparison.Ordinal)));
        }
    }
}
