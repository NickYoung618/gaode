using Gaode.Application.Ports;
using Gaode.Domain.Station01;
using Gaode.Plc.Protocol;
using Gaode.Domain.Configuration;

namespace Gaode.Infrastructure.Devices.Plc;

public sealed partial class LatestProtocolPlcDevice
{
    public RotationExecutionBasis? RotationBasis => options.RotationBasis;
    private int? selectedGripper;
    private long selectedGripperEpoch, selectedGripperConfirmedTick;
    internal async Task SelectStageGripperAsync(PlcStageActionRequest request, int gripper, CancellationToken token)
    {
        if (gripper is not (1 or 2)) throw new ArgumentException("GripperNotConfigured");
        lock (sync)
            if (selectedGripper == gripper && selectedGripperEpoch == request.ConnectionEpoch) return;
        var expected = SignalCodes.Value(SignalId.GrabId, gripper == 1 ? "Gripper1" : "Gripper2");
        var previous = selectedGripper;
        using (var eligibility = ActionDispatchEligibility(request.Window, request.ConnectionEpoch, token))
            await signals.WriteWordAsync(SignalId.GrabId, expected, token);
        var after = System.Diagnostics.Stopwatch.GetTimestamp();
        SetFeedback("G", true, true);
        try
        {
            while (true)
            {
                CheckAxisWindow(request.Window, request.ConnectionEpoch, token);
                var sample = await WaitGroupAsync("G", after, false, token);
                if (sample.Epoch != request.ConnectionEpoch) throw new IOException("GripperSelectionEpochChanged");
                var actual = sample.Values.Word(SignalId.GrabActiveId);
                if (actual == expected)
                {
                    lock (sync) { selectedGripper = gripper; selectedGripperEpoch = request.ConnectionEpoch; selectedGripperConfirmedTick=sample.Ended; }
                    return;
                }
                if (actual > 2 || actual != 0 && actual != previous) throw new IOException("GripperSelectionFeedbackMismatch");
                SetFeedback("G", true); after = sample.Ended + 1;
            }
        }
        catch { lock (sync) selectedGripper = null; throw; }
        finally { SetFeedback("G", false); }
    }
    internal async Task<(AngleReachedEvidence Angle, ObservationIdentity Observation)> RotateStageAsync(
        PlcStageActionRequest request, CancellationToken token)
    {
        var target = request.RotationTarget ?? throw new InvalidOperationException("RotationTargetMissing");
        if (options.RotationBasis is not { } basis || basis.Purpose != request.TargetPurpose ||
            basis.SourceReference != target.MechanicalEvidenceReference || basis.AngleToleranceDeg != target.AngleToleranceDeg)
            throw new InvalidOperationException("RotationMechanicalBasisMissingOrMismatched");
        using (var eligibility = ActionDispatchEligibility(request.Window, request.ConnectionEpoch, token))
        {
            var baseline = await signals.ReadAsync(PreparedPlcReadPlans.Rotation, token);
            if (baseline.Bit(SignalId.RotateStart) || baseline.Word(SignalId.RPosConfirmed) != 0)
                throw new IOException("PreviousRotationNotCleared");
            await signals.WriteFloatAsync(SignalId.RotateTargetR, checked((float)target.AngleDeg), token);
            await signals.WriteBitAsync(SignalId.RotateStart, true, token);
        }
        var after = System.Diagnostics.Stopwatch.GetTimestamp(); var movingObserved = false;
        SetFeedback("R", true, true);
        try
        {
            while (true)
            {
                CheckAxisWindow(request.Window, request.ConnectionEpoch, token);
                var sample = await WaitGroupAsync("R", after, false, token);
                if (sample.Epoch != request.ConnectionEpoch) throw new IOException("RotationEpochChanged");
                var status = sample.Values.Word(SignalId.RPosConfirmed);
                if (status == SignalCodes.Value(SignalId.RPosConfirmed, "Moving")) movingObserved = true;
                else if (status == SignalCodes.Value(SignalId.RPosConfirmed, "Arrived") && movingObserved)
                {
                    var angle = new AngleReachedEvidence(request.Correlation, target.AngleDeg,
                        sample.Values.Float(SignalId.MachineCurrentPosR), target.AngleToleranceDeg, sample.Identity.SampleEndedUtc);
                    if (!angle.Matched) throw new IOException("RotationActualAngleMismatch");
                    using var eligibility = ActionDispatchEligibility(request.Window, request.ConnectionEpoch, token);
                    await ClearAndConfirmAsync("R", "R", SignalId.RotateStart, true,
                        [SignalId.RotateStart, SignalId.RPosConfirmed], request.Window, request.ConnectionEpoch, token);
                    return (angle, sample.Identity);
                }
                else if (status != SignalCodes.Value(SignalId.RPosConfirmed, "Arrived")) throw new IOException("RotationFeedbackFailure");
                SetFeedback("R", true); after = sample.Ended + 1;
            }
        }
        finally { SetFeedback("R", false); }
    }
    internal PlcSignalAccessor StageSignals => signals;
    internal ExecutionOrigin StageOrigin => Origin;
    internal string StageConfigurationPurpose => options.ConfigurationPurpose;
    internal FixedPoint SortingSafetyTarget(FixedPoint at, string purpose)
    {
        var position = options.SortingSafePosition;
        if (position is null || !double.IsFinite(position.GrabZ) || string.IsNullOrWhiteSpace(position.SourceReference) ||
            position.Purpose != purpose || purpose != options.ConfigurationPurpose ||
            position.Unit != at.Unit || position.Frame != at.Frame)
            throw new InvalidOperationException("SortingSafetyPositionNotConfigured");
        return at with { Id = at.Id + "/safe", Z = position.GrabZ };
    }
    internal async Task<PositionReachedEvidence> MoveStageAxesAsync(PlcStageActionRequest request,
        FixedPoint target, bool grabOnly, CancellationToken token)
    {
        if (grabOnly)
        {
            await DriveAxesAsync([new(SignalId.GrabTargetZ, SignalId.ZGrabMoveStart, SignalId.ZGrapPosConfirmed,
                SignalId.FlipGrapCurrentPosZ, target.Z)], request.Window, request.ConnectionEpoch, null, token);
            CheckAxisWindow(request.Window, request.ConnectionEpoch, token);
        }
        else await DriveTargetAsync(target, "Unload", request.Window, request.ConnectionEpoch, null, token);
        var actual = completedMoveObservation?.PositionForPurpose(grabOnly ? "GrabZ" : "XY")
            ?? throw new IOException("StagePositionUnavailable");
        var evidence = new PositionReachedEvidence(request.Correlation, target, actual, request.PositionTolerance);
        if (!evidence.Matched) throw new IOException("StageTargetNotReached");
        return evidence;
    }
    private sealed record PickEvidenceSegment(long Business, long Heartbeat, DeviceActionEvidence Evidence);
    private readonly Dictionary<Guid, PickEvidenceSegment> pickEvidenceSegments = [];
    internal void BeginStageEvidence(ActionCorrelation correlation) => BeginEvidence(correlation);
    internal async Task<DeviceActionEvidence> SaveStageEvidenceAsync(PlcStageActionRequest request,
        DeviceCompletionMeaning meaning, IReadOnlyList<PositionReachedEvidence> positions,
        ObservationIdentity observation, CancellationToken token, PositionReachedEvidence? safe = null,
        AngleReachedEvidence? angle = null)
    {
        // Capture before saving: traffic during the actual commit belongs to the
        // continuation too. Only a valid durable pick permits this checkpoint.
        var next = (Business: wire.ExchangeSequence, Heartbeat: heartbeat.ExchangeSequence);
        PickEvidenceSegment? pick;
        lock (sync) pickEvidenceSegments.TryGetValue(request.Correlation.ActionId, out pick);
        if (meaning == DeviceCompletionMeaning.MaterialTransferred && pick is null)
            throw new InvalidOperationException("CommittedPickEvidenceMissing");
        var evidence = await CompleteEvidenceAsync(request.Correlation, meaning, request.Window, token, positions,
            sampledObservation: observation,
            segmentStart: meaning == DeviceCompletionMeaning.MaterialTransferred ? (pick!.Business, pick.Heartbeat) : null,
            precedingEvidence: meaning == DeviceCompletionMeaning.MaterialTransferred ? pick!.Evidence : null);
        evidence = evidence with { SafeReached = safe, AngleReached = angle };
        if (meaning == DeviceCompletionMeaning.MaterialPicked)
            lock (sync) pickEvidenceSegments[request.Correlation.ActionId] = new(next.Business, next.Heartbeat, evidence);
        return evidence;
    }

    internal void SetTransferSampling(bool enabled) => SetFeedback("T", enabled);
    internal async Task<(ushort Status, PositionObservation Position, long Ended)> ReadStageSampleAsync(
        long after, long expectedEpoch, CancellationToken token)
    {
        var sample = await WaitGroupAsync("T", after, false, token);
        token.ThrowIfCancellationRequested();
        var values = sample.Values;
        var state = Observe();
        if (!state.HasReliableObservation || state.ConnectionEpoch != expectedEpoch)
            throw new IOException("StageSampleConnectionChanged");
        var positionValues = SelectValues(values, PreparedPlcReadPlans.Position);
        var identity = new GroupObservation("P", sample.Version, expectedEpoch, positionValues,
            sample.PlannedDue, sample.Enqueued, sample.Published).Identity;
        if (positionValues.Stamps.Values.Min(s => s.Sent) < values.Stamps[SignalId.SortingExecStatus].Ended)
            throw new IOException("TransferPositionPrecedesFeedback");
        return (values.Word(SignalId.SortingExecStatus), new(values.Float(SignalId.MachineCurrentPosX),
            values.Float(SignalId.MachineCurrentPosY), values.Float(SignalId.FlipGrapCurrentPosZ),
            "GrabZ", state.Position?.CoordinateFrame, state.Position?.UnitBasis, identity, Origin), sample.Ended);
    }
}
