using Gaode.Application.Station01;
using Gaode.Contracts.Tests.Support;
using Gaode.Domain.Station01;
using Xunit;

namespace Gaode.Contracts.Tests.Station01;

public sealed class NormalPauseBoundaryTests
{
    [Fact]
    public async Task PauseAtSavedBoundaryRequiresCheckAndContinuesSameRunAndStage()
    {
        await using var h = Station01StepHarness.Create();
        var c = Coordinator(h.Run.RunId);
        try
        {
            var service = new Station01ControlCommandService(c);
            var snapshot = c.Query(h.Run.RunId)!;
            await service.RequestAsync("Operator", h.Run.RunId, "pause", snapshot.ObservedRevision, "Pause", null, default);
            var writes = new List<string>();
            var boundary = new NormalPauseBoundary(c, h.Motion, TimeProvider.System);
            var waiting = boundary.WaitAsync(h.Run.RunId, RunState.RunningF, "3D-reset-saved",
                h.Motion.Observe().ConnectionEpoch, DateTimeOffset.UtcNow.AddSeconds(5),
                (kind, _, _) => { writes.Add(kind); return Task.CompletedTask; }, default);
            await Until(() => c.Query(h.Run.RunId)!.State == RunState.Paused);
            Assert.False(waiting.IsCompleted);
            snapshot = c.Query(h.Run.RunId)!;
            var check = await service.CheckAsync("Operator", h.Run.RunId, "check", snapshot.ObservedRevision,
                true, true, true, default);
            await service.ContinueAsync("Operator", h.Run.RunId, "continue", snapshot.ObservedRevision, check.CheckId, default);
            await waiting.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(RunState.RunningF, c.Query(h.Run.RunId)!.State);
            Assert.Equal(new[] { "NormalPaused", "NormalContinued" }, writes);
            Assert.Equal(h.Run.RunId, c.Query(h.Run.RunId)!.RunId);
        }
        finally { await c.StopConsumerAsync(default); }
    }

    [Fact]
    public async Task PausedTimeStillConsumesFrozenDeadlineAndDoesNotContinue()
    {
        await using var h = Station01StepHarness.Create();
        var c = Coordinator(h.Run.RunId);
        try
        {
            c.Control(h.Run.RunId)!.RequestStop();
            var writes = new List<string>();
            var boundary = new NormalPauseBoundary(c, h.Motion, TimeProvider.System);
            await Assert.ThrowsAsync<TimeoutException>(() => boundary.WaitAsync(h.Run.RunId,
                RunState.Detection, "saved-face", h.Motion.Observe().ConnectionEpoch,
                DateTimeOffset.UtcNow.AddMilliseconds(150),
                (kind, _, _) => { writes.Add(kind); return Task.CompletedTask; }, default));
            Assert.Equal(new[] { "NormalPaused" }, writes);
            Assert.True(c.Control(h.Run.RunId)!.AdmissionClosed);
        }
        finally { await c.StopConsumerAsync(default); }
    }

    private static Station01Coordinator Coordinator(Guid runId)
    {
        var c = new Station01Coordinator(8, 8, 2);
        c.Start();
        Assert.True(c.TryRegister(new(runId, "pause-test", "Operator", RunState.Running3D, 1, 1,
            TerminalOutcome.None, false, ActionState.Completed, CaptureState.NotRequested,
            AlgorithmState.NotRequested, SaveState.Committed, HandoffState.NotReady, null, null, null, [])));
        return c;
    }

    private static async Task Until(Func<bool> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (!predicate()) await Task.Delay(10, timeout.Token);
    }
}
