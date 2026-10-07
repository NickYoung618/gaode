using System.Diagnostics;
using Gaode.Application.Ports;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Plc.Protocol;
using Microsoft.Extensions.Logging;

namespace Gaode.Infrastructure.Devices.Plc;

public sealed partial class LatestProtocolPlcDevice
{
    private sealed record AxisMove(SignalId Target, SignalId Start, SignalId Confirmed, SignalId Actual, double Value);
    private readonly Dictionary<Guid, FlipMovePreparation> flipPreparations = [];
    private Guid? putBackPositionActionId;
    private AxisMovingWatch? activeAxisMoving;

    private static string AxisPurpose(string role) => role switch
    {
        "3D" or "FlipPick" or "FlipPutBack" or "Unload" => "XY",
        "F" or "E" => "ScanZ", "Detection" => "DetectionZ",
        "Grab" => "GrabZ", _ => throw new InvalidOperationException("MoveAxisPurposeUnknown")
    };

    private PlcPoseProgram ResolveProgram(FlipMovePreparation preparation)
    {
        if (preparation.TransitionId == Guid.Empty || string.IsNullOrWhiteSpace(preparation.PhysicalEntityId) ||
            preparation.PhysicalSlotIndex <= 0) throw new InvalidOperationException("FlipPreparationIdentityInvalid");
        var mappings = options.PosePrograms.Where(p => p.Model == preparation.Model &&
            p.ProfileId == preparation.TargetPose.ProfileId && p.ProfileVersion == preparation.TargetPose.ProfileVersion &&
            p.PoseKey == preparation.TargetPose.PoseKey).ToArray();
        if (mappings.Length != 1 || string.IsNullOrWhiteSpace(mappings[0].SourceReference) ||
            mappings[0].Purpose != (options.Provider == "Real" ? "Production" : "Test") ||
            mappings[0].ModelWords.Length != definitionAdmission.Definition[SignalId.ModelPayload].RegisterCount)
            throw new InvalidOperationException("PlcPoseProgramMappingMissing");
        return mappings[0] with { ModelWords = (ushort[])mappings[0].ModelWords.Clone() };
    }

    private async Task DriveTargetAsync(FixedPoint target, string role, ActionWindow window, long expectedEpoch,
        Action? accepted, CancellationToken token)
    {
        await DriveAxesAsync([
            new(SignalId.CameraTargetX, SignalId.XMoveStart, SignalId.XPosConfirmed, SignalId.MachineCurrentPosX, target.X),
            new(SignalId.CameraTargetY, SignalId.YMoveStart, SignalId.YPosConfirmed, SignalId.MachineCurrentPosY, target.Y)
        ], window, expectedEpoch, accepted, token);
        AxisMove? z = AxisPurpose(role) switch
        {
            "XY" => null,
            "DetectionZ" => new(SignalId.CameraTargetZ, SignalId.ZCameraMoveStart, SignalId.ZCameraPosConfirmed, SignalId.MachineCurrentPosZ, target.Z),
            "ScanZ" => new(SignalId.ScanTargetZ, SignalId.ZScanMoveStart, SignalId.ZScanPosConfirmed, SignalId.ScanCurrentPosZ, target.Z),
            "GrabZ" => new(SignalId.GrabTargetZ, SignalId.ZGrabMoveStart, SignalId.ZGrapPosConfirmed, SignalId.FlipGrapCurrentPosZ, target.Z),
            _ => throw new InvalidOperationException("MoveAxisPurposeUnknown")
        };
        if (z is not null) await DriveAxesAsync([z], window, expectedEpoch, null, token);
        CheckAxisWindow(window, expectedEpoch, token);
        var position = Observe().PositionForPurpose(AxisPurpose(role));
        if (position is null || !position.IsReliable || Math.Abs(position.ActualX!.Value - target.X) > PositionTolerance ||
            Math.Abs(position.ActualY!.Value - target.Y) > PositionTolerance ||
            position.ActualZ is { } actualZ && Math.Abs(actualZ - target.Z) > PositionTolerance)
            throw new IOException("CompletedCoordinatesMismatch");
    }

    private void CheckAxisWindow(ActionWindow window, long expectedEpoch, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!window.Contains(Stopwatch.GetTimestamp())) throw new TimeoutException("AxisWindowClosed");
        var sample = ReadProtocolSample();
        if (!sample.Connected || !sample.SafetyClear || sample.ConnectionEpoch != expectedEpoch || unknown)
            throw new IOException("AxisObservationOrSafetyLost");
    }

    private async Task DriveAxesAsync(AxisMove[] axes, ActionWindow window, long expectedEpoch,
        Action? accepted, CancellationToken token)
    {
        using var dispatch = ActionDispatchEligibility(window, expectedEpoch, token);
        CheckAxisWindow(window, expectedEpoch, token);
        var allAxes = axes;
        // Read the current wire values, never decide reuse from cached arrival flags.
        var baseline = await signals.ReadAsync([.. PreparedPlcReadPlans.Base, .. PreparedPlcReadPlans.Position], token);
        CheckAxisWindow(window, expectedEpoch, token);
        if (axes.Any(a => baseline.Bit(a.Start))) throw new IOException("StaleAxisTrigger");
        foreach (var axis in axes)
        {
            var state = baseline.Word(axis.Confirmed);
            if (state != SignalCodes.Value(axis.Confirmed, "Moving") && state != SignalCodes.Value(axis.Confirmed, "Arrived"))
                throw new IOException("AxisFeedbackUnknown:" + axis.Confirmed);
            if (!float.IsFinite(baseline.Float(axis.Actual)) || !float.IsFinite((float)axis.Value))
                throw new IOException("AxisPositionInvalid:" + axis.Actual);
        }
        var reused = axes.Where(a => baseline.Word(a.Confirmed) == SignalCodes.Value(a.Confirmed, "Arrived") &&
            Math.Abs(baseline.Float(a.Actual) - (float)a.Value) <= PositionTolerance).ToArray();
        foreach (var axis in reused)
            logger.LogInformation("AxisPositionReused: action={ActionId} epoch={Epoch} axis={Axis} target={Target} actual={Actual} tolerance={Tolerance}",
                pending?.Id, expectedEpoch, axis.Confirmed, axis.Value, baseline.Float(axis.Actual), PositionTolerance);
        axes = axes.Except(reused).ToArray();
        if (axes.Length == 0)
        {
            await VerifyAxisPositionsAsync(allAxes, reused, window, expectedEpoch, token);
            accepted?.Invoke(); // Local positioning request satisfied; no PLC motion was dispatched.
            return;
        }
        foreach (var axis in axes)
        {
            CheckAxisWindow(window, expectedEpoch, token);
            await signals.WriteFloatAsync(axis.Target, (float)axis.Value, token);
        }
        var dispatchClocks = axes.ToDictionary(a => a.Confirmed, _ => new PlcExchangeClock());
        var watch = new AxisMovingWatch(expectedEpoch, dispatchClocks);
        lock (sync) activeAxisMoving = watch;
        var notified = false;
        try
        {
            // The same pump must be observing before an async write continuation can be delayed.
            SetFeedback("X", true, true);
            foreach (var axis in axes)
            {
                CheckAxisWindow(window, expectedEpoch, token);
                var priorClock = PlcExchangeClock.Current.Value;
                try
                {
                    dispatchClocks[axis.Confirmed].Queued = Stopwatch.GetTimestamp();
                    PlcExchangeClock.Current.Value = dispatchClocks[axis.Confirmed];
                    await signals.WriteBitAsync(axis.Start, true, token);
                }
                finally { PlcExchangeClock.Current.Value = priorClock; }
            }
            CheckAxisWindow(window, expectedEpoch, token);
            var after = dispatchClocks.Values.Max(c => Volatile.Read(ref c.Ended));
            while (true)
            {
                CheckAxisWindow(window, expectedEpoch, token);
                var sample = await WaitGroupAsync(notified ? "B" : "X", after, false, token);
                CheckAxisWindow(window, expectedEpoch, token);
                var feedback = sample.Values;
                foreach (var axis in axes)
                {
                    var state = feedback.Word(axis.Confirmed);
                    if (state == SignalCodes.Value(axis.Confirmed, "Timeout")) throw new IOException("AxisCompletionTimeout:" + axis.Confirmed);
                    if (state != SignalCodes.Value(axis.Confirmed, "Moving") && state != SignalCodes.Value(axis.Confirmed, "Arrived")) throw new IOException("AxisFeedbackUnknown:" + axis.Confirmed);
                }
                bool movingObserved;
                lock (sync) movingObserved = axes.All(a => watch.SawMoving(a.Confirmed));
                if (!notified && movingObserved)
                {
                    CheckAxisWindow(window, expectedEpoch, token);
                    SetFeedback("X", false); // B resumes ownership only after every commanded axis was seen Moving.
                    accepted?.Invoke(); notified = true;
                }
                if (movingObserved && axes.All(a => feedback.Word(a.Confirmed) == SignalCodes.Value(a.Confirmed, "Arrived")))
                {
                    // Require real coordinates whose sampling begins after this arrival observation.
                    var actual = await WaitGroupAsync("P", sample.Ended + 1, true, token);
                    CheckAxisWindow(window, expectedEpoch, token);
                    if (axes.Any(a => !float.IsFinite(actual.Values.Float(a.Actual)) ||
                        Math.Abs(actual.Values.Float(a.Actual) - (float)a.Value) > PositionTolerance))
                        throw new IOException("AxisActualPositionMismatch");
                    foreach (var axis in axes)
                    {
                        CheckAxisWindow(window, expectedEpoch, token);
                        await signals.WriteBitAsync(axis.Start, false, token);
                    }
                    CheckAxisWindow(window, expectedEpoch, token);
                    await VerifyAxisPositionsAsync(allAxes, reused, window, expectedEpoch, token);
                    return;
                }
                after = sample.Ended + 1;
            }
        }
        finally
        {
            lock (sync) { if (ReferenceEquals(activeAxisMoving, watch)) activeAxisMoving = null; }
            SetFeedback("X", false);
        }
    }
    private async Task VerifyAxisPositionsAsync(AxisMove[] allAxes, AxisMove[] reused,
        ActionWindow window, long expectedEpoch, CancellationToken token)
    {
        CheckAxisWindow(window, expectedEpoch, token);
        var current = await signals.ReadAsync([.. PreparedPlcReadPlans.Base, .. PreparedPlcReadPlans.Position], token);
        CheckAxisWindow(window, expectedEpoch, token);
        foreach (var axis in allAxes)
        {
            if (current.Bit(axis.Start) || !float.IsFinite(current.Float(axis.Actual)) ||
                Math.Abs(current.Float(axis.Actual) - (float)axis.Value) > PositionTolerance ||
                reused.Contains(axis) && current.Word(axis.Confirmed) != SignalCodes.Value(axis.Confirmed, "Arrived"))
                throw new IOException("AxisFinalPositionUnconfirmed:" + axis.Confirmed);
        }
    }

    internal IDisposable ActionDispatchEligibility(ActionWindow window, long expectedEpoch, CancellationToken token)
    {
        var previous = PlcScheduledTransport.Eligibility.Value;
        PlcScheduledTransport.Eligibility.Value = () =>
        {
            lock (sync) return !token.IsCancellationRequested && window.Contains(Stopwatch.GetTimestamp()) &&
                epoch == expectedEpoch && !unknown && !stopRequested && ReadProtocolSample().SafetyClear;
        };
        return new DispatchScope(previous);
    }
    private sealed class DispatchScope(Func<bool>? previous) : IDisposable
    { public void Dispose() => PlcScheduledTransport.Eligibility.Value = previous; }
}

// One bounded action watch. Only the existing pump records actual post-dispatch samples.
internal sealed class AxisMovingWatch(long epoch, IReadOnlyDictionary<SignalId, PlcExchangeClock> dispatch)
{
    private readonly HashSet<SignalId> moving = [];
    internal bool SawMoving(SignalId axis) => moving.Contains(axis);
    internal void Observe(SignalValues values, long sampledEpoch)
    {
        if (sampledEpoch != epoch) return;
        foreach (var (axis, clock) in dispatch)
        {
            var sent = Volatile.Read(ref clock.Ended);
            if (sent > 0 && values.Stamps.TryGetValue(axis, out var sample) && sample.Sent >= sent &&
                values.Words.ContainsKey(axis) && values.Word(axis) == SignalCodes.Value(axis, "Moving"))
                moving.Add(axis);
        }
    }
}
