using Gaode.Domain.Station01;
using Xunit;

namespace Gaode.Contracts.Tests.Station01;

public sealed class TrayObservationParticipationTests
{
    [Fact]
    public void AbnormalSlotStaysExcludedAndPhysicalNumbersNeverShift()
    {
        var initial = SlotParticipation.Apply([], [new(2, TrayPresence.Present, TrayPose.Abnormal, "tilt"),
            new(7, TrayPresence.Present, TrayPose.Normal, null), new(9, TrayPresence.Absent, TrayPose.Unknown, "empty")]);
        Assert.Equal(SlotParticipationState.PoseExcluded, initial[2].State);
        Assert.Equal(SlotParticipationState.Participating, initial[7].State);
        Assert.Equal(SlotParticipationState.Absent, initial[9].State);
        var later = SlotParticipation.Apply(initial.Values, [new(2, TrayPresence.Present, TrayPose.Normal, null),
            new(7, TrayPresence.Present, TrayPose.Abnormal, "moved"), new(9, TrayPresence.Absent, TrayPose.Unknown, "empty")]);
        Assert.Equal(new[] { 2, 7 }, later.Values.Where(x => x.State == SlotParticipationState.PoseExcluded).Select(x => x.PhysicalSlotIndex).Order());
    }

    [Fact]
    public void UnknownOrMissingObservationCannotBecomeNormal()
    {
        var known = SlotParticipation.Apply([], [new(3, TrayPresence.Present, TrayPose.Normal, null)]);
        Assert.Equal(SlotParticipationState.Unknown, SlotParticipation.Apply(known.Values, []).Single().Value.State);
        Assert.Equal(SlotParticipationState.Unknown, SlotParticipation.Apply([], [new(5, TrayPresence.Unknown, TrayPose.Unknown, "unreadable")])[5].State);
        Assert.Throws<InvalidDataException>(() => SlotParticipation.Apply([], [new(3, TrayPresence.Present, TrayPose.Normal, null), new(3, TrayPresence.Absent, TrayPose.Unknown, null)]));
    }
}
