using Gaode.Integration.Tests.Support;
using Gaode.Application.Station01;
using Gaode.Domain.Station01;
using Gaode.Host.Api;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
using Xunit;

namespace Gaode.Integration.Tests.Api;

public sealed class Station01MainFlowNotificationTests
{
    [Fact]
    public async Task NotificationsExposeCommittedReferencesAndNeverReplaceGetReconciliation()
    {
        var coordinator = new Station01Coordinator(32, 8, 4);
        coordinator.Start();
        var proxy = new RecordingClientProxy();
        await using var persistence = await NotificationPersistence.CreateAsync();
        using var service = persistence.CreateService(coordinator,
            new TestHubContext(proxy), capacity: 16);
        await service.StartAsync(CancellationToken.None);
        var runId = Guid.NewGuid();
        var completionId = Guid.NewGuid();
        var matrixId = Guid.NewGuid();
        var unlockId = Guid.NewGuid();
        Assert.True(coordinator.TryRegister(Snapshot(runId)));

        await coordinator.SetAsync(runId, value => value with
        {
            State = RunState.ReadyForRemoval, ObservedRevision = 1, PersistedRevision = 7,
            WholeTaskState = "ReadyForRemoval", SortingState = "Completed", WholeTrayCompletionId = completionId,
            ReadyForRemovalSourceMatrixId = matrixId
        });
        await proxy.WaitForAsync("WholeTrayCompleted");
        await coordinator.SetAsync(runId, value => value with
        {
            State = RunState.AwaitingManualRemoval, ObservedRevision = 2,
            PersistedRevision = 8, WholeTaskState = "AwaitingManualTrayRemoval",
            ManualRemovalAllowedEventId = unlockId
        });
        await proxy.WaitForAsync("ManualRemovalAllowed");
        await coordinator.SetAsync(runId, value => value with
        {
            State = RunState.Completed, ObservedRevision = 3, PersistedRevision = 9,
            FinalOutcome = TerminalOutcome.Completed,
            WholeTaskState = "FinalUnloadCompletion"
        });
        await proxy.WaitForAsync("FinalUnloadCompleted");

        var notifications = proxy.Envelopes.ToArray();
        var whole = Assert.Single(notifications, x =>
            x.EventType == "WholeTrayCompleted");
        Assert.Equal(7, whole.PersistedRevision);
        Assert.Equal("s01/notification/2.0", whole.SchemaVersion);
        var summary = JsonSerializer.SerializeToElement(whole.Summary, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(new[] { "errorCode", "executionState", "wholeTaskState" }, summary.EnumerateObject().Select(p => p.Name).Order().ToArray());
        Assert.Equal("ReadyForRemoval", summary.GetProperty("executionState").GetString());
        Assert.Contains("wholeTrayCompletionId", whole.ChangedFields);
        Assert.Contains("sortingState", whole.ChangedFields);
        Assert.Contains("readyForRemovalSourceMatrixId", whole.ChangedFields);
        var final = Assert.Single(notifications, x =>
            x.EventType == "FinalUnloadCompleted");
        Assert.Equal(9, final.PersistedRevision);

        // Notification ordering/delivery is advisory. The coordinator/query fact
        // remains complete even after the captured messages are discarded.
        proxy.Envelopes.Clear();
        var reconciled = coordinator.Query(runId)!;
        Assert.Equal(RunState.Completed, reconciled.State);
        Assert.Equal(completionId, reconciled.WholeTrayCompletionId);
        Assert.Equal(matrixId, reconciled.ReadyForRemovalSourceMatrixId);
        Assert.Equal(unlockId, reconciled.ManualRemovalAllowedEventId);
        Assert.Equal("FinalUnloadCompletion", reconciled.WholeTaskState);

        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await service.StopAsync(stop.Token);
        await coordinator.StopConsumerAsync(stop.Token);
    }

    [Fact]
    public async Task DiagnosticNotificationUsesSemanticFieldNamesAndRequiresGetForDetails()
    {
        var coordinator = new Station01Coordinator(32, 8, 4);
        coordinator.Start();
        var proxy = new RecordingClientProxy();
        await using var persistence = await NotificationPersistence.CreateAsync();
        using var service = persistence.CreateService(coordinator, new TestHubContext(proxy));
        await service.StartAsync(CancellationToken.None);
        var runId = Guid.NewGuid();
        Assert.True(coordinator.TryRegister(Snapshot(runId) with { Handoff = HandoffState.NotReady }));
        await coordinator.SetAsync(runId, value => value with { ObservedRevision = 1 });
        await proxy.WaitForAsync("StateChanged");
        await coordinator.SetAsync(runId, value => value with { State = RunState.Blocked, ObservedRevision = 2,
            ErrorCode = "DeviceNotReady", StartupDiagnostic = new(["DeviceNotReady"], "Unconfirmed", "BeforeStart",
                "BlockedNoDeviceAction", null, null, new(DeviceProvider.Unavailable, null, EvidenceQuality.Unknown)) });
        await proxy.WaitForAsync("DiagnosticChanged");
        var message = Assert.Single(proxy.Envelopes, x => x.EventType == "DiagnosticChanged");
        Assert.Contains("errorCode", message.ChangedFields);
        Assert.Contains("startupDiagnostic", message.ChangedFields);
        var summary = JsonSerializer.SerializeToElement(message.Summary, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(3, summary.EnumerateObject().Count());
        Assert.Equal("DeviceNotReady", summary.GetProperty("errorCode").GetString());
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await service.StopAsync(stop.Token);
        await coordinator.StopConsumerAsync(stop.Token);
    }

    private static RunSnapshot Snapshot(Guid runId) => new(runId, "notify-main-flow",
        "test:Operator", RunState.HandoffReady, 0, 6, TerminalOutcome.None, false,
        ActionState.Completed, CaptureState.MediaTaken, AlgorithmState.Success,
        SaveState.Committed, HandoffState.Ready, "public-1", "budget-1", "simulation-1", []);

    private sealed class RecordingClientProxy : IClientProxy
    {
        private readonly object gate = new();
        public List<NotificationEnvelope> Envelopes { get; } = [];
        public Task SendCoreAsync(string method, object?[] args,
            CancellationToken cancellationToken = default)
        {
            lock (gate)
            {
                Envelopes.Add(Assert.IsType<NotificationEnvelope>(args[0]));
                Monitor.PulseAll(gate);
            }
            return Task.CompletedTask;
        }

        public async Task WaitForAsync(string eventType)
        {
            var end = DateTimeOffset.UtcNow.AddSeconds(2);
            while (DateTimeOffset.UtcNow < end)
            {
                lock (gate)
                    if (Envelopes.Any(x => x.EventType == eventType)) return;
                await Task.Delay(10);
            }
            throw new TimeoutException("Notification not observed: " + eventType);
        }
    }

    private sealed class TestHubContext(IClientProxy proxy) : IHubContext<Station01Hub>
    {
        public IHubClients Clients { get; } = new TestHubClients(proxy);
        public IGroupManager Groups { get; } = new TestGroupManager();
    }

    private sealed class TestHubClients(IClientProxy proxy) : IHubClients
    {
        public IClientProxy All => proxy;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => proxy;
        public IClientProxy Client(string connectionId) => proxy;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => proxy;
        public IClientProxy Group(string groupName) => proxy;
        public IClientProxy GroupExcept(string groupName,
            IReadOnlyList<string> excludedConnectionIds) => proxy;
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => proxy;
        public IClientProxy User(string userId) => proxy;
        public IClientProxy Users(IReadOnlyList<string> userIds) => proxy;
    }

    private sealed class TestGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveFromGroupAsync(string connectionId, string groupName,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
