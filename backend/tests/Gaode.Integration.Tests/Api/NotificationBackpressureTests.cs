using Gaode.Integration.Tests.Support;
using Gaode.Application.Station01;
using Gaode.Domain.Station01;
using Gaode.Host.Api;
using Microsoft.AspNetCore.SignalR;
using Xunit;

namespace Gaode.Integration.Tests.Api;

public sealed class NotificationBackpressureTests
{
    [Fact]
    public async Task SlowSignalRConsumerCannotBlockCoordinatorControlOrQueryPath()
    {
        var coordinator = new Station01Coordinator(32, 8, 4);
        coordinator.Start();
        var runId = Guid.NewGuid();
        Assert.True(coordinator.TryRegister(new RunSnapshot(runId, "notify", "test:Operator",
            RunState.Created, 0, 0, TerminalOutcome.None, false,
            ActionState.NotRequested, CaptureState.NotRequested, AlgorithmState.NotRequested,
            SaveState.NotQueued, HandoffState.NotReady, null, null, null, [])));
        var proxy = new BlockingClientProxy();
        await using var persistence = await NotificationPersistence.CreateAsync();
        using var service = persistence.CreateService(coordinator,
            new TestHubContext(proxy), capacity: 1);
        await service.StartAsync(CancellationToken.None);

        await coordinator.SetAsync(runId, s => s with { ObservedRevision = 1 });
        await proxy.FirstSend.Task.WaitAsync(TimeSpan.FromSeconds(2));
        for (var revision = 2; revision <= 12; revision++)
            await coordinator.SetAsync(runId, s => s with { ObservedRevision = revision });
        coordinator.SignalCancel(runId);

        var queried = coordinator.Query(runId);
        Assert.Equal(12, queried?.ObservedRevision);
        Assert.True(coordinator.Control(runId)?.AdmissionClosed);
        Assert.True(service.RejectedSnapshots > 0);

        proxy.Release();
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await service.StopAsync(stop.Token);
        await coordinator.StopConsumerAsync(stop.Token);
    }

    private sealed class BlockingClientProxy : IClientProxy
    {
        private readonly TaskCompletionSource release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource FirstSend { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task SendCoreAsync(string method, object?[] args,
            CancellationToken cancellationToken = default)
        {
            FirstSend.TrySetResult();
            await release.Task.WaitAsync(cancellationToken);
        }
        public void Release() => release.TrySetResult();
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
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => proxy;
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
