using Gaode.Application.Station01;
using Gaode.Domain.Station01;
using Xunit;

namespace Gaode.Rules.Tests.Station01;

public sealed class CoordinatorNotificationTests
{
    [Fact]
    public async Task PublishesSnapshotAfterSingleOwnerAppliesMutation()
    {
        var coordinator = new Station01Coordinator(4, 2, 2);
        coordinator.Start();
        var run = Guid.NewGuid();
        Assert.True(coordinator.TryRegister(new RunSnapshot(run, "request", "subject",
            RunState.Created, 0, 0, TerminalOutcome.None, false,
            ActionState.NotRequested, CaptureState.NotRequested, AlgorithmState.NotRequested,
            SaveState.NotQueued, HandoffState.NotReady, null, null, null, [])));
        var observed = new TaskCompletionSource<RunSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.SnapshotChanged += value => observed.TrySetResult(value);
        await coordinator.SetAsync(run, current => current.Next(RunState.Preparing));
        Assert.Equal(RunState.Preparing, (await observed.Task.WaitAsync(TimeSpan.FromSeconds(1))).State);
        Assert.Equal(1, coordinator.ActiveRunCount);
    }
}
