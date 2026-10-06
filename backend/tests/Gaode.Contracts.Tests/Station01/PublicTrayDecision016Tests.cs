using Gaode.Application.Station01;
using Gaode.Domain.Station01;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Gaode.Contracts.Tests.Station01;

public sealed class PublicTrayDecision016Tests
{
    public static TrayObservation Observation(Guid run, TrayPresence presence = TrayPresence.Present, TrayPose pose = TrayPose.Abnormal) =>
        new(Guid.NewGuid(), run, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow,
            TrayObservationPurpose.InitialPreparation, 1, null,
            [new(7, presence, pose, "explicit sample") { CellId="r2:c3",Region="OK",Row=2,Column=3 }], null,
            new(ComponentEvidenceSource.Test,"ContentSampleWorker/1","Test"), ["TestMedia:016"]) {
            SchemaVersion="tray-observation/2", MappingSourceReference="Test:016-declared-camera-map", ExpectedPhysicalSlotIndices=[7] };

    [Fact]
    public void EmptyRequiresExplicitCompleteKnownCoverage()
    {
        var empty=Observation(Guid.NewGuid(),TrayPresence.Absent,TrayPose.Unknown);
        Assert.True(empty.IsEmptyTray);
        Assert.False((empty with { ExpectedPhysicalSlotIndices=[7,8] }).IsEmptyTray);
        Assert.False((empty with { Slots=[] }).IsEmptyTray);
        Assert.False(Observation(Guid.NewGuid(),TrayPresence.Unknown,TrayPose.Unknown).IsEmptyTray);
        Assert.False((empty with { Slots=[empty.Slots[0] with { CellId=null }] }).IsEmptyTray);
        Assert.False(Observation(Guid.NewGuid()).IsEmptyTray);
    }

    [Fact]
    public void RecheckChangesPresenceAndPoseButCannotChangePhysicalIdentity()
    {
        var first=Observation(Guid.NewGuid(),TrayPresence.Present,TrayPose.Normal);
        var recheck=first with { ObservationId=Guid.NewGuid(),CaptureId=Guid.NewGuid(),CallId=Guid.NewGuid(),
            Purpose=TrayObservationPurpose.PostPlacementCheck,CheckRound=2,RelatedTransitionId=Guid.NewGuid(),
            Slots=[first.Slots[0] with {Pose=TrayPose.Abnormal}] };
        Assert.True(recheck.HasSamePhysicalMapping(first));
        Assert.True((recheck with {Slots=[recheck.Slots[0] with {Presence=TrayPresence.Absent}]}).HasSamePhysicalMapping(first));
        Assert.False((recheck with {Slots=[recheck.Slots[0] with {CellId="r2:c4",Column=4}]}).HasSamePhysicalMapping(first));
        Assert.False((recheck with {ExpectedPhysicalSlotIndices=[7,8]}).HasSamePhysicalMapping(first));
        var excluded=SlotParticipation.Apply([],recheck.Slots);
        Assert.Equal(SlotParticipationState.PoseExcluded,SlotParticipation.Apply(excluded.Values,first.Slots)[7].State);
        Assert.Equal(SlotParticipationState.Absent,SlotParticipation.Apply(excluded.Values,
            [first.Slots[0] with {Presence=TrayPresence.Absent}])[7].State);
    }

    [Fact]
    public async Task OriginalDeadlineTimeoutCommitsOnceAndReplayDoesNotReopen()
    {
        var clock=new FakeTimeProvider(DateTimeOffset.UtcNow);
        var run=Guid.NewGuid();var coordinator=Coordinator(run);var service=new TrayAnomalyDecisionService(coordinator,clock);
        var saved=new List<TrayAnomalyDecisionProjection>();
        var opened=new TaskCompletionSource<TrayAnomalyDecisionProjection>();
        Task<string> Persist(TrayAnomalyDecisionProjection value,CancellationToken ct) {
            saved.Add(value);if(value.State=="Pending")opened.TrySetResult(value);return Task.FromResult("write://"+Guid.NewGuid()); }
        try {
            var observation=Observation(run);var wait=service.WaitAsync(observation,coordinator.Control(run)!,Persist,CancellationToken.None);
            var original=await opened.Task;await WaitPublished(coordinator,run);
            clock.Advance(TimeSpan.FromSeconds(10));var decided=await wait.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal("Continue",decided.Choice);Assert.Equal("Timeout",decided.ChoiceSource);
            Assert.Equal(original.DeadlineUtc,decided.DeadlineUtc);Assert.Equal(2,saved.Count);
            Assert.Equal(decided,await service.WaitAsync(observation,coordinator.Control(run)!,Persist,CancellationToken.None));
            Assert.Equal(2,saved.Count);
        } finally {await coordinator.StopConsumerAsync(CancellationToken.None);}
    }

    [Theory]
    [InlineData("Stop")]
    [InlineData("Cancel")]
    [InlineData("Fault")]
    public async Task ClosedControlCannotTimeoutContinue(string mode)
    {
        var run=Guid.NewGuid();var clock=new FakeTimeProvider(DateTimeOffset.UtcNow);var coordinator=Coordinator(run);
        var service=new TrayAnomalyDecisionService(coordinator,clock);var saved=new List<TrayAnomalyDecisionProjection>();
        Task<string> Persist(TrayAnomalyDecisionProjection value,CancellationToken ct) {saved.Add(value);return Task.FromResult("write://"+Guid.NewGuid());}
        try {
            var control=coordinator.Control(run)!;var task=service.WaitAsync(Observation(run),control,Persist,CancellationToken.None);
            await WaitPublished(coordinator,run);
            if(mode=="Stop")control.RequestStop();else if(mode=="Cancel")control.RequestCancel();else control.MarkSafetyFault();
            clock.Advance(TimeSpan.FromSeconds(10));
            await Assert.ThrowsAsync<InvalidOperationException>(()=>task);
            Assert.Equal("Closed",saved[^1].State);Assert.DoesNotContain(saved,x=>x.Choice=="Continue");
        } finally {await coordinator.StopConsumerAsync(CancellationToken.None);}
    }

    private static Station01Coordinator Coordinator(Guid run) {
        var c=new Station01Coordinator(32,16,16);c.Start();
        Assert.True(c.TryRegister(new(run,"016","test",RunState.Created,0,0,TerminalOutcome.None,false,
            ActionState.NotRequested,CaptureState.NotRequested,AlgorithmState.NotRequested,SaveState.NotQueued,HandoffState.NotReady,null,null,null,[])));
        return c;
    }
    private static async Task WaitPublished(Station01Coordinator c,Guid run) {
        for(var i=0;i<100&&c.Query(run)?.TrayAnomalyDecision is null;i++)await Task.Delay(10);
        Assert.NotNull(c.Query(run)?.TrayAnomalyDecision);
    }
}
