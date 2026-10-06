using Gaode.Application.Station01;
using Xunit;

namespace Gaode.Rules.Tests.Station01;

public sealed class FlowMailboxTests
{
    [Fact]
    public void ControlLatchClosesAdmissionWithoutWaitingForFlowConsumer()
    {
        var latch = new ControlLatch();
        Assert.False(latch.AdmissionClosed);
        latch.RequestCancel();
        Assert.True(latch.AdmissionClosed);
        Assert.True(latch.CancelRequested);
    }

    [Fact]
    public void TerminalReservationIsBounded()
    {
        var reservation = new TerminalReservation(1);
        Assert.True(reservation.TryReserve());
        Assert.False(reservation.TryReserve());
        reservation.Release();
        Assert.True(reservation.TryReserve());
    }

    [Fact]
    public async Task FullNormalQueueCannotConsumeControlOrTerminalCapacityAndControlIsDrainedFirst()
    {
        var mailbox = new FlowMailbox(1, 1, 1);
        var run = Guid.NewGuid();
        var order = new List<string>();
        var normal = mailbox.Post(run, snapshot =>
        {
            order.Add("normal");
            return snapshot;
        });
        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = mailbox.Post(run, snapshot => snapshot);
        });
        var control = mailbox.Post(run, snapshot =>
        {
            order.Add("control");
            return snapshot;
        }, control: true);
        var current = new Gaode.Domain.Station01.RunSnapshot(run, "r", "s",
            Gaode.Domain.Station01.RunState.Created, 0, 0,
            Gaode.Domain.Station01.TerminalOutcome.None, false,
            Gaode.Domain.Station01.ActionState.NotRequested,
            Gaode.Domain.Station01.CaptureState.NotRequested,
            Gaode.Domain.Station01.AlgorithmState.NotRequested,
            Gaode.Domain.Station01.SaveState.NotQueued,
            Gaode.Domain.Station01.HandoffState.NotReady, null, null, null, []);
        using var stop = new CancellationTokenSource();
        var drain = mailbox.DrainAsync((_, apply) => apply(current), stop.Token);
        await Task.WhenAll(normal, control).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(["control", "normal"], order);
        await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => drain);
    }
}
