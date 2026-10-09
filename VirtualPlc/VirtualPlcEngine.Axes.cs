using Gaode.Plc.Protocol;

namespace VirtualPlc;

public sealed partial class VirtualPlcEngine
{
    private sealed record AxisDefinition(string Name, SignalId Target, SignalId Start, SignalId Actual, SignalId Confirmed);
    private sealed record AxisExecution(AxisDefinition Axis, float From, float Target, long Started, long Due,
        long Sequence, IReadOnlyList<long> Writes, bool Timeout);
    private static readonly AxisDefinition[] Axes = [
        new("X", SignalId.CameraTargetX, SignalId.XMoveStart, SignalId.MachineCurrentPosX, SignalId.XPosConfirmed),
        new("Y", SignalId.CameraTargetY, SignalId.YMoveStart, SignalId.MachineCurrentPosY, SignalId.YPosConfirmed),
        new("DetectionZ", SignalId.CameraTargetZ, SignalId.ZCameraMoveStart, SignalId.MachineCurrentPosZ, SignalId.ZCameraPosConfirmed),
        new("ScanZ", SignalId.ScanTargetZ, SignalId.ZScanMoveStart, SignalId.ScanCurrentPosZ, SignalId.ZScanPosConfirmed),
        new("GrabZ", SignalId.GrabTargetZ, SignalId.ZGrabMoveStart, SignalId.FlipGrapCurrentPosZ, SignalId.ZGrapPosConfirmed),
        new("R", SignalId.RotateTargetR, SignalId.RotateStart, SignalId.MachineCurrentPosR, SignalId.RPosConfirmed)
    ];
    private readonly Dictionary<SignalId, AxisExecution> movingAxes = [];
    private readonly HashSet<SignalId> assertedAxes = [];
    private readonly HashSet<SignalId> completedAxes = [];
    private readonly Dictionary<string, long> clearDue = [];
    // Offline fault injection only; not PLC protocol fields.
    public int FeedbackClearDelayMs { get; set; }
    public bool HoldFeedbackClear { get; set; }
    private bool ClearDue(string resource, long now)
    {
        if (!clearDue.TryGetValue(resource, out var due)) clearDue[resource] = due = now + FeedbackClearDelayMs;
        return !HoldFeedbackClear && now >= due;
    }

    private void ResetAxes()
    {
        movingAxes.Clear(); assertedAxes.Clear(); completedAxes.Clear(); clearDue.Clear();
        _store.SetHoldingRegisterFromPlc(_store.Definition[SignalId.GrabActiveId].DocumentNumber, 0);
        foreach (var axis in Axes)
        {
            _store.SetFloatFromPlc(_store.Definition[axis.Actual].DocumentNumber, 0);
            _store.SetHoldingRegisterFromPlc(_store.Definition[axis.Confirmed].DocumentNumber,
                SignalCodes.Value(axis.Confirmed, "Arrived"));
        }
    }

    private void ProcessAxes(long now)
    {
        var safe = _store.ReadCoilByDocumentNumber(PlcAddressMap.Coils.PlcReadyState) &&
            _store.ReadCoilByDocumentNumber(PlcAddressMap.Coils.PlcModeAuto) &&
            !_store.ReadCoilByDocumentNumber(PlcAddressMap.Coils.PlcSystemFault) && !_softStopLatched;
        foreach (var axis in Axes)
        {
            if (movingAxes.TryGetValue(axis.Start, out var moving))
            {
                if (!safe)
                {
                    RecordAxis(moving, "heldUnsafe"); movingAxes.Remove(axis.Start);
                    // A stopped/incomplete action never receives an arrival value.
                    continue;
                }
                var progress = Math.Clamp((now - moving.Started) / (double)(moving.Due - moving.Started), 0, moving.Timeout ? 0.5 : 1);
                _store.SetFloatFromPlc(_store.Definition[axis.Actual].DocumentNumber,
                    (float)(moving.From + (moving.Target - moving.From) * progress));
                if (now >= moving.Due)
                {
                    var failed = moving.Timeout;
                    _store.SetHoldingRegisterFromPlc(_store.Definition[axis.Confirmed].DocumentNumber,
                        SignalCodes.Value(axis.Confirmed, failed ? "Timeout" : "Arrived"));
                    if (!failed) completedAxes.Add(axis.Start);
                    RecordAxis(moving, failed ? "timeout" : "completed"); movingAxes.Remove(axis.Start);
                }
            }
            var asserted = _store.ReadCoilByDocumentNumber(_store.Definition[axis.Start].DocumentNumber);
            if (!asserted)
            {
                assertedAxes.Remove(axis.Start);
                continue;
            }
            if (!assertedAxes.Add(axis.Start)) continue;
            completedAxes.Remove(axis.Start); clearDue.Remove(axis.Name);
            var number = _store.Definition[axis.Target].DocumentNumber;
            if (!safe || _active is not null || movingAxes.ContainsKey(axis.Start) || !_store.WasFloatWritten(number))
            {
                _logger.LogWarning("Axis request rejected: {Axis}, safe={Safe}, targetWritten={TargetWritten}",
                    axis.Name, safe, _store.WasFloatWritten(number));
                continue;
            }
            var target = _store.ReadFloatByDocumentNumber(number);
            if (!float.IsFinite(target)) { _logger.LogWarning("Axis target invalid: {Axis}", axis.Name); continue; }
            var references = _store.GetWriteAudit().Where(w => w.Accepted &&
                (w.Area == PlcArea.Coil && w.DocumentNumber == _store.Definition[axis.Start].DocumentNumber ||
                 w.Area == PlcArea.HoldingRegister && w.DocumentNumber == number))
                .GroupBy(w => (w.Area, w.DocumentNumber)).Select(g => g.Last().Sequence).Order().ToArray();
            var execution = new AxisExecution(axis, _store.ReadActualFloat(_store.Definition[axis.Actual].DocumentNumber),
                target, now, DueAt(now, _options.MotionDurationMs), ++_deviceActionSequence, references, Consume(SimulationFault.MoveTimeout));
            if (execution.From == execution.Target && !execution.Timeout)
            {
                RecordAxis(execution, "sameTargetNoMotion");
                continue;
            }
            completedAxes.Remove(axis.Start); clearDue.Remove(axis.Name);
            movingAxes.Add(axis.Start, execution);
            _store.SetHoldingRegisterFromPlc(_store.Definition[axis.Confirmed].DocumentNumber,
                SignalCodes.Value(axis.Confirmed, "Moving"));
            RecordAxis(execution, "accepted");
        }
    }

    private void RecordAxis(AxisExecution action, string phase)
    {
        _actionAudit.Enqueue(new(++_actionEventSequence, action.Sequence, resetGeneration,
            DateTimeOffset.UtcNow, "AxisMove", 1, phase, action.Writes, new { value = action.Target }, action.Axis.Name,
            new { value = _store.ReadActualFloat(_store.Definition[action.Axis.Actual].DocumentNumber) },
            new { confirmed = _store.ReadHoldingRegisterByDocumentNumber(_store.Definition[action.Axis.Confirmed].DocumentNumber) }));
        if (_actionAudit.Count > 4096) _actionAudit.Dequeue();
        _logger.LogInformation("Virtual axis {Axis} {Phase}: action={Action}, target={Target}",
            action.Axis.Name, phase, action.Sequence, action.Target);
    }
}
