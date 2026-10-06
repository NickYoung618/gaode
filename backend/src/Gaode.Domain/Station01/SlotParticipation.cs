using System.Collections.ObjectModel;

namespace Gaode.Domain.Station01;

public enum SlotParticipationState { Unknown, Absent, Participating, PoseExcluded }
public sealed record SlotParticipation(int PhysicalSlotIndex, SlotParticipationState State, string? Reason)
{
    public static IReadOnlyDictionary<int, SlotParticipation> Apply(IEnumerable<SlotParticipation> previous,
        IReadOnlyList<TraySlotObservation> observations)
    {
        if (observations.Any(o => o.PhysicalSlotIndex < 1) || observations.GroupBy(o => o.PhysicalSlotIndex).Any(g => g.Count() > 1))
            throw new InvalidDataException("TrayPhysicalSlotIdentityInvalid");
        var result = previous.ToDictionary(p => p.PhysicalSlotIndex, p => p.State == SlotParticipationState.PoseExcluded ? p :
            p with { State = SlotParticipationState.Unknown, Reason = "ObservationMissing" });
        foreach (var observation in observations)
        {
            if (observation.Presence == TrayPresence.Present && result.TryGetValue(observation.PhysicalSlotIndex, out var before) &&
                before.State == SlotParticipationState.PoseExcluded) continue;
            var state = observation.Presence switch
            {
                TrayPresence.Absent => SlotParticipationState.Absent,
                TrayPresence.Present when observation.Pose == TrayPose.Normal => SlotParticipationState.Participating,
                TrayPresence.Present when observation.Pose == TrayPose.Abnormal => SlotParticipationState.PoseExcluded,
                _ => SlotParticipationState.Unknown
            };
            result[observation.PhysicalSlotIndex] = new(observation.PhysicalSlotIndex, state, observation.Reason);
        }
        return new ReadOnlyDictionary<int, SlotParticipation>(result);
    }
}
