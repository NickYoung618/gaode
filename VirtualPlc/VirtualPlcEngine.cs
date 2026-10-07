using Gaode.Plc.Protocol;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using Gaode.Diagnostics;

namespace VirtualPlc;

public sealed partial class VirtualPlcEngine : BackgroundService
{
    private enum ActionKind { Flip, PutBack, Sort }
    private sealed record PendingAction(ActionKind Kind, long DueAt, ushort Command,
        float X = 0, float Y = 0, float Z = 0, long ActionSequence = 0)
    { public IReadOnlyList<long> WriteSequenceRefs { get; init; } = []; }

    private readonly object _gate = new();
    private readonly PlcDataStore _store;
    private readonly SimulationOptions _options;
    private readonly ILogger<VirtualPlcEngine> _logger;
    private readonly Random _random;
    private readonly HashSet<SimulationFault> _faults = [];
    private PendingAction? _active;
    private PendingAction? _lastFailed;
    private long _nextHeartbeat;
    private long _deviceActionSequence, _actionEventSequence;
    private readonly Queue<DeviceActionAudit> _actionAudit = new();
    private long _lastEcho;
    private DateTimeOffset _lastEchoUtc;
    private bool _communicationTimedOut;
    private bool _softStopLatched;
    private long resetGeneration;
    private bool _pcReadySeen;
    private ushort _latchedAlarms;
    private ushort lastSortingCommand;
    private bool _sortPicked;
    private long _nextActionAllowedAt;
    private readonly HeartbeatDiagnosticWindow _diagnostics;
    private long _lastScanTick;

    public VirtualPlcEngine(PlcDataStore store, IOptions<SimulationOptions> options,
        ILogger<VirtualPlcEngine> logger)
    {
        _store = store;
        _options = options.Value;
        _logger = logger;
        _diagnostics = store.HeartbeatDiagnostics(logger);
        _random = new Random(_options.RandomSeed);
        _store.PcValueWritten += OnPcValueWritten;
    }

    public SimulatorSnapshot GetSnapshot()
    {
        lock (_gate) return _store.CreateSnapshot(_communicationTimedOut,
            _active?.Kind.ToString(), _faults);
    }





    public (bool Accepted, string Message) InjectFault(SimulationFault fault)
    {
        lock (_gate)
        {
            if (fault is SimulationFault.AxisResponseDelayed or SimulationFault.AxisWriteResponseLost)
                return _store.FeedbackFaults.Arm(fault);
            _faults.Add(fault);
            if (fault == SimulationFault.EmergencyAlarm) _latchedAlarms |= AlarmMask("EmergencyStop");
            if (fault == SimulationFault.ManualZoneOccupied) _latchedAlarms |= AlarmMask("ManualFlipOccupied");
            if (fault is SimulationFault.EmergencyAlarm or SimulationFault.ManualZoneOccupied)
                FailActiveAction();
            UpdateSafety();
            _logger.LogWarning("Simulation fault injected: {Fault}", fault);
            return (true, $"Fault {fault} injected.");
        }
    }

    public void ResetSimulation()
    {
        lock (_gate)
        {
            _faults.Clear();
                _communicationTimedOut = false;
            _softStopLatched = false;
            _pcReadySeen = false;
            _latchedAlarms = 0;
            _active = null;
            _lastFailed = null;
            _nextActionAllowedAt = 0;
            _store.ResetPcWritableValues();
            ResetAxes();
            lastFlipCommand = 0;
            _store.SetHoldingRegisterFromPlc(_store.Definition[SignalId.FlipUnloadStatus].DocumentNumber, 0);
            foreach (var signal in new[] { SignalId.FlipStatus, SignalId.SortingExecStatus })
                _store.SetHoldingRegisterFromPlc(_store.Definition[signal].DocumentNumber, SignalCodes.Value(signal, "Idle"));
            foreach (var point in new[] { PlcAddressMap.HoldingRegisters.MachineCurrentPosX,
                         PlcAddressMap.HoldingRegisters.MachineCurrentPosY,
                         PlcAddressMap.HoldingRegisters.MachineCurrentPosZ,
                         PlcAddressMap.HoldingRegisters.TeachPosX,
                         PlcAddressMap.HoldingRegisters.TeachPosY,
                         PlcAddressMap.HoldingRegisters.TeachPosZ })
                _store.SetFloatFromPlc(point, 0);
            lastSortingCommand = 0;
            _sortPicked = false;
            _lastEcho = Environment.TickCount64;
            _lastEchoUtc = DateTimeOffset.UtcNow;
            _nextHeartbeat = _lastEcho + _options.HeartbeatPeriodMs;
            _store.SetCoilFromPlc(PlcAddressMap.Coils.PlcModeAuto, true);
            UpdateSafety();
        }
    }

    public FlowSimulationDecision ResolveFlowDecision(FlowFailureCategory category, bool stepSucceeded)
    {
        if (stepSucceeded)
            return new(true, false, null, false, "Step succeeded.", DateTimeOffset.UtcNow);
        if (category == FlowFailureCategory.Mes)
            return new(true, true, null, true,
                "MES offline: host must cache and retransmit.", DateTimeOffset.UtcNow);
        return new(false, true, null, false,
            "Failed step requires host reconciliation before continuing.", DateTimeOffset.UtcNow);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ResetSimulation();
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(Math.Max(1, _options.ScanPeriodMs)));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken)) Tick();
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await _diagnostics.FlushAsync();
    }

    private void Tick()
    {
        var scanRequestedTick = Stopwatch.GetTimestamp();
        lock (_gate)
        {
            var scanTick = Stopwatch.GetTimestamp();
            var gateWaitMs = Stopwatch.GetElapsedTime(scanRequestedTick, scanTick).TotalMilliseconds;
            var scanGapMs = _lastScanTick == 0 ? 0 : Stopwatch.GetElapsedTime(_lastScanTick, scanTick).TotalMilliseconds;
            _lastScanTick = scanTick;
            if (scanGapMs >= 250 || gateWaitMs >= 250)
            {
                _diagnostics.Record("scan-delay", new { scanRequestedTick, scanTick, gateWaitMs, scanGapMs });
                _diagnostics.Capture("ScanDelay");
            }
            var now = Environment.TickCount64;
            if (now >= _nextHeartbeat)
            {
                if (!_faults.Contains(SimulationFault.PauseHeartbeat))
                    _store.SetCoilFromPlc(PlcAddressMap.Coils.PlcHeartbeatReq,
                        !_store.ReadCoilByDocumentNumber(PlcAddressMap.Coils.PlcHeartbeatReq));
                _nextHeartbeat = now + _options.HeartbeatPeriodMs;
                _diagnostics.Record("heartbeat-tick", new { scanTick, scanGapMs, gateWaitMs,
                    bit = _store.ReadCoilByDocumentNumber(PlcAddressMap.Coils.PlcHeartbeatReq),
                    paused = _faults.Contains(SimulationFault.PauseHeartbeat), _options.HeartbeatPeriodMs });
            }
            var pcReady = _store.ReadCoilByDocumentNumber(PlcAddressMap.Coils.PcSystemReady);
            if (pcReady && !_pcReadySeen) { _lastEcho = now; _lastEchoUtc = DateTimeOffset.UtcNow; }
            _pcReadySeen = pcReady;
            if (pcReady && now - _lastEcho > _options.HeartbeatTimeoutMs)
            {
                if (!_communicationTimedOut)
                {
                    _diagnostics.Record("echo-timeout", new { lastValidEchoUtc = _lastEchoUtc,
                        ageMs = now - _lastEcho, thresholdMs = _options.HeartbeatTimeoutMs, scanTick, scanGapMs,
                        requestBit = _store.ReadCoilByDocumentNumber(PlcAddressMap.Coils.PlcHeartbeatReq),
                        responseBit = _store.ReadCoilByDocumentNumber(PlcAddressMap.Coils.PcHeartbeatResp) });
                    _diagnostics.Capture("PlcEchoTimeout", true);
                }
                if (!_communicationTimedOut)
                    _logger.LogWarning("VirtualPlc heartbeat echo timeout: atUtc={AtUtc:o}, lastValidEchoUtc={LastValidEchoUtc:o}, ageMs={AgeMs}, thresholdMs={ThresholdMs}, requestBit={RequestBit}, responseBit={ResponseBit}; communication alarm is latched",
                        DateTimeOffset.UtcNow, _lastEchoUtc, now - _lastEcho, _options.HeartbeatTimeoutMs,
                        _store.ReadCoilByDocumentNumber(PlcAddressMap.Coils.PlcHeartbeatReq),
                        _store.ReadCoilByDocumentNumber(PlcAddressMap.Coils.PcHeartbeatResp));
                _communicationTimedOut = true;
                _softStopLatched = true;
                _latchedAlarms |= AlarmMask("Communication");
                FailActiveAction();
            }
            CompleteAction(now);
            ProcessAxes(now);
            ProcessFlip(now);
            ProcessSort(now);
            UpdateSafety();
        }
    }

    private void CompleteAction(long now)
    {
        if (_active is null || now < _active.DueAt) return;
        var action = _active;
        _active = null;
        switch (action.Kind)
        {
            case ActionKind.Flip:
            case ActionKind.PutBack:
                var feedback = action.Kind == ActionKind.Flip ? SignalId.FlipStatus : SignalId.FlipUnloadStatus;
                if (Consume(SimulationFault.FlipFailure))
                {
                    _lastFailed = action;
                    _store.SetHoldingRegisterFromPlc(_store.Definition[feedback].DocumentNumber, 3);
                }
                else
                {
                    if (action.Kind == ActionKind.PutBack)
                        _store.SetFloatFromPlc(_store.Definition[SignalId.FlipGrapCurrentPosZ].DocumentNumber, action.Z);
                    _store.SetHoldingRegisterFromPlc(_store.Definition[feedback].DocumentNumber, 2);
                }
                break;
            case ActionKind.Sort:
                if (Consume(SimulationFault.FullPallet) || Consume(SimulationFault.SortingFailure))
                {
                    _lastFailed = action;
                    _store.SetHoldingRegisterFromPlc(PlcAddressMap.HoldingRegisters.SortingExecStatus, 3);
                }
                else
                {
                    // The Host has already positioned the independent axes. The
                    // grip action changes holding state, never target coordinates.
                    _sortPicked = action.Command == 1;
                    _store.SetHoldingRegisterFromPlc(PlcAddressMap.HoldingRegisters.SortingExecStatus,
                        action.Command == 1 ? (ushort)1 : (ushort)2);
                }
                break;
        }
        RecordDeviceAction(action, _lastFailed == action || _latchedAlarms != 0 ? "failed" : "completed");
    }

    private ushort lastFlipCommand;
    private void ProcessFlip(long now)
    {
        var command = _store.ReadHoldingRegisterByDocumentNumber(_store.Definition[SignalId.FlipSorting].DocumentNumber);
        if (command == 0)
        {
            if (_active?.Kind is ActionKind.Flip or ActionKind.PutBack) return;
            lastFlipCommand = 0;
            _store.SetHoldingRegisterFromPlc(_store.Definition[SignalId.FlipStatus].DocumentNumber, 0);
            _store.SetHoldingRegisterFromPlc(_store.Definition[SignalId.FlipUnloadStatus].DocumentNumber, 0);
            return;
        }
        if (command == lastFlipCommand || _active is not null || movingAxes.Count != 0) return;
        if (command == 1)
        {
            if (lastFlipCommand != 0 || !_store.GetWriteAudit().Any(w => w.Accepted && w.Area == PlcArea.HoldingRegister &&
                    w.DocumentNumber == _store.Definition[SignalId.ModelPayload].DocumentNumber) ||
                !_store.GetWriteAudit().Any(w => w.Accepted && w.Area == PlcArea.HoldingRegister &&
                    w.DocumentNumber == _store.Definition[SignalId.FlipTargetFace].DocumentNumber)) return;
            if (!Start(new(ActionKind.Flip, DueAt(now, _options.FlipDurationMs), command))) return;
            lastFlipCommand = command;
            _store.SetHoldingRegisterFromPlc(_store.Definition[SignalId.FlipStatus].DocumentNumber, 1);
            if (_faults.Contains(SimulationFault.FlipFeedbackHold)) _active = _active! with { DueAt = long.MaxValue };
        }
        else if (command == 2)
        {
            if (lastFlipCommand != 1 || _store.ReadHoldingRegisterByDocumentNumber(_store.Definition[SignalId.FlipStatus].DocumentNumber) != 2 ||
                _options.PutBackDurationMs is null or <= 0 || _options.FlipPutBackSafeZ is not { } safeZ || !float.IsFinite(safeZ)) return;
            if (!Start(new(ActionKind.PutBack, DueAt(now, _options.PutBackDurationMs.Value), command, Z: safeZ))) return;
            lastFlipCommand = command;
            _store.SetHoldingRegisterFromPlc(_store.Definition[SignalId.FlipUnloadStatus].DocumentNumber, 1);
        }
    }

    private void ProcessSort(long now)
    {
        var command = _store.ReadHoldingRegisterByDocumentNumber(PlcAddressMap.HoldingRegisters.SortingCmd);
        if (command == lastSortingCommand) return;
        if (command == 0) { if (_active?.Kind != ActionKind.Sort) lastSortingCommand = 0; return; }
        if (!CanStartNow(now)) return;
        lastSortingCommand = command;
        if (command is not (1 or 2) || command == 1 && _sortPicked || command == 2 && !_sortPicked ||
            movingAxes.Count != 0 || Axes.Any(a => _store.ReadCoilByDocumentNumber(_store.Definition[a.Start].DocumentNumber)))
        { Fail(ActionKind.Sort); return; }
        foreach (var axis in Axes.Where(a => a.Name is "X" or "Y" or "GrabZ"))
            if (!_store.WasFloatWritten(_store.Definition[axis.Target].DocumentNumber) ||
                _store.ReadHoldingRegisterByDocumentNumber(_store.Definition[axis.Confirmed].DocumentNumber) != 1 ||
                Math.Abs(_store.ReadActualFloat(_store.Definition[axis.Actual].DocumentNumber) -
                    _store.ReadFloatByDocumentNumber(_store.Definition[axis.Target].DocumentNumber)) > 0.001)
            { Fail(ActionKind.Sort); return; }
        if (Consume(SimulationFault.SortingPositionMismatch))
            _store.SetFloatFromPlc(PlcAddressMap.HoldingRegisters.MachineCurrentPosX,
                _store.ReadActualFloat(PlcAddressMap.HoldingRegisters.MachineCurrentPosX) + 10);
        Start(new(ActionKind.Sort, DueAt(now, _options.SortingDurationMs), command,
            _store.ReadActualFloat(PlcAddressMap.HoldingRegisters.MachineCurrentPosX),
            _store.ReadActualFloat(PlcAddressMap.HoldingRegisters.MachineCurrentPosY),
            _store.ReadActualFloat(_store.Definition[SignalId.FlipGrapCurrentPosZ].DocumentNumber)));
    }

    private bool Start(PendingAction action)
    {
        if (_active is not null || movingAxes.Count != 0 || !CanExecute())
        {
            Fail(action.Kind, action.Command);
            RecordDeviceAction(action with { ActionSequence = ++_deviceActionSequence, WriteSequenceRefs = RecentWriteRefs(action) }, "rejected");
            return false;
        }
        action = action with { ActionSequence = ++_deviceActionSequence, WriteSequenceRefs = RecentWriteRefs(action) };
        _active = action;
        RecordDeviceAction(action, "accepted");
        _lastFailed = null;
        return true;
    }

    private bool CanStartNow(long now) => now >= _nextActionAllowedAt;

    private void BeginInterActionGap(long now)
    {
        _nextActionAllowedAt = Math.Max(_nextActionAllowedAt,
            now + Math.Max(0, _options.InterActionGapMs));
    }

    private long DueAt(long now, int baseDurationMs)
    {
        var duration = Math.Max(1, baseDurationMs);
        var jitter = Math.Max(0, _options.ActionDurationJitterMs);
        if (jitter == 0) return now + duration;
        var minimum = Math.Max(1, duration - jitter);
        var maximum = Math.Max(minimum, duration + jitter);
        return now + _random.Next(minimum, maximum + 1);
    }



    private bool CanExecute() =>
        _store.ReadCoilByDocumentNumber(PlcAddressMap.Coils.PlcReadyState) &&
        !_store.ReadCoilByDocumentNumber(PlcAddressMap.Coils.TeachModeCmd) &&
        !_store.ReadCoilByDocumentNumber(PlcAddressMap.Coils.ManualZoneOccupied);

    private void OnPcValueWritten(PcWriteEvent write)
    {
        var callbackStartedTick = Stopwatch.GetTimestamp();
        lock (_gate)
        {
            var callbackGateAcquiredTick = Stopwatch.GetTimestamp();
            if (write.Area == PlcArea.Coil)
            {
                if (write.DocumentNumber == PlcAddressMap.Coils.PcHeartbeatResp)
                {
                    var requestBit = _store.ReadCoilByDocumentNumber(PlcAddressMap.Coils.PlcHeartbeatReq);
                    var valid = (write.Value != 0) == requestBit;
                    var priorEchoUtc = _lastEchoUtc;
                    var echoGapMs = Environment.TickCount64 - _lastEcho;
                    if (valid) { _lastEcho = Environment.TickCount64; _lastEchoUtc = DateTimeOffset.UtcNow; }
                    _diagnostics.Record("echo-observed", new { write.TransactionId, requestBit,
                        responseBit = write.Value != 0, valid, priorEchoUtc, echoGapMs,
                        callbackStartedTick, callbackGateAcquiredTick,
                        callbackGateWaitMs = Stopwatch.GetElapsedTime(callbackStartedTick, callbackGateAcquiredTick).TotalMilliseconds,
                        callbackProcessingMs = Stopwatch.GetElapsedTime(callbackGateAcquiredTick).TotalMilliseconds });
                    if (!valid || echoGapMs >= 1500) _diagnostics.Capture(valid ? "LateValidEcho" : "EchoBitMismatch");
                }
                if (write.DocumentNumber == PlcAddressMap.Coils.SoftStopCmd && write.Value != 0)
                {
                    _softStopLatched = true;
                    FailActiveAction();
                }
                if (write.DocumentNumber == PlcAddressMap.Coils.SystemResetCmd && write.Value != 0)
                    ResetFromPc();
                if (write.DocumentNumber == PlcAddressMap.Coils.TeachModeCmd && write.Value != 0)
                    FailActiveAction();
            }
            else if (write.DocumentNumber == PlcAddressMap.HoldingRegisters.TeachPosSelect &&
                     _store.ReadCoilByDocumentNumber(PlcAddressMap.Coils.TeachModeCmd))
            {
                foreach (var pair in new[] {
                    (PlcAddressMap.HoldingRegisters.MachineCurrentPosX, PlcAddressMap.HoldingRegisters.TeachPosX),
                    (PlcAddressMap.HoldingRegisters.MachineCurrentPosY, PlcAddressMap.HoldingRegisters.TeachPosY),
                    (PlcAddressMap.HoldingRegisters.MachineCurrentPosZ, PlcAddressMap.HoldingRegisters.TeachPosZ) })
                {
                    var raw = _store.ReadHoldingRegisters(PlcAddressMap.ToPduOffset(pair.Item1), 2);
                    _store.SetFloatFromPlc(pair.Item2, Float32Codec.Decode(raw[0], raw[1], _store.ByteOrder));
                }
            }
            if (write.Area == PlcArea.HoldingRegister && write.DocumentNumber == _store.Definition[SignalId.GrabId].DocumentNumber)
            {
                var valid = write.Value is 1 or 2 && _active is null && movingAxes.Count == 0 && !_softStopLatched;
                _store.SetHoldingRegisterFromPlc(_store.Definition[SignalId.GrabActiveId].DocumentNumber,
                    valid ? write.Value : (ushort)0);
                _logger.LogInformation("Gripper selection feedback: requested={Requested}, valid={Valid}, generation={Generation}",
                    write.Value, valid, resetGeneration);
            }
            UpdateSafety();
        }
    }

    private void ResetFromPc()
    {
        resetGeneration++;
        if (_active is { } cancelled) RecordDeviceAction(cancelled, "resetCancelled");
        _active = null;
        _lastFailed = null;
        _communicationTimedOut = false;
        _softStopLatched = false;
        _latchedAlarms = 0;
        _lastEcho = Environment.TickCount64;
        _lastEchoUtc = DateTimeOffset.UtcNow;
        // System reset clears every PC-owned command and target.  The host must
        // explicitly re-assert PC_System_Ready and obtain fresh ready feedback.
        _store.ResetPcWritableValues();
        _store.InvalidateFloatTargets();
        _store.SetHoldingRegisterFromPlc(PlcAddressMap.HoldingRegisters.FlipStatus, SignalCodes.Value(SignalId.FlipStatus, "Idle"));
        _store.SetHoldingRegisterFromPlc(PlcAddressMap.HoldingRegisters.SortingExecStatus, SignalCodes.Value(SignalId.SortingExecStatus, "Idle"));
        _store.SetFloatFromPlc(PlcAddressMap.HoldingRegisters.MachineCurrentPosX, 0);
        _store.SetFloatFromPlc(PlcAddressMap.HoldingRegisters.MachineCurrentPosY, 0);
        _store.SetFloatFromPlc(PlcAddressMap.HoldingRegisters.MachineCurrentPosZ, 0);
        lastSortingCommand = 0;
        _sortPicked = false;
    }

    private void FailActiveAction()
    {
        if (_active is null) return;
        _lastFailed = _active;
        Fail(_active.Kind, _active.Command);
        RecordDeviceAction(_active, "failed");
        _active = null;
    }

    private void Fail(ActionKind kind, ushort command = 0)
    {
        switch (kind)
        {
            case ActionKind.Flip:
                _store.SetHoldingRegisterFromPlc(PlcAddressMap.HoldingRegisters.FlipStatus, SignalCodes.Value(SignalId.FlipStatus, "Failed"));
                break;
            case ActionKind.PutBack:
                _store.SetHoldingRegisterFromPlc(_store.Definition[SignalId.FlipUnloadStatus].DocumentNumber, 3);
                break;
            case ActionKind.Sort:
                _store.SetHoldingRegisterFromPlc(PlcAddressMap.HoldingRegisters.SortingExecStatus, SignalCodes.Value(SignalId.SortingExecStatus, "GrabFailed"));
                break;
        }
    }

    private void UpdateSafety()
    {
        var manual = _faults.Contains(SimulationFault.ManualZoneOccupied);
        var emergency = _faults.Contains(SimulationFault.EmergencyAlarm);
        if (manual) _latchedAlarms |= AlarmMask("ManualFlipOccupied");
        if (emergency) _latchedAlarms |= AlarmMask("EmergencyStop");
        var stopped = _store.ReadCoilByDocumentNumber(PlcAddressMap.Coils.SoftStopCmd);
        var ready = _store.ReadCoilByDocumentNumber(PlcAddressMap.Coils.PcSystemReady) &&
            _store.ReadCoilByDocumentNumber(PlcAddressMap.Coils.PlcModeAuto) &&
            !manual && !emergency && !_communicationTimedOut && !stopped && !_softStopLatched &&
            _latchedAlarms == 0 && !_store.ReadCoilByDocumentNumber(PlcAddressMap.Coils.TeachModeCmd);
        _store.SetCoilFromPlc(PlcAddressMap.Coils.ManualZoneOccupied, manual);
        _store.SetCoilFromPlc(PlcAddressMap.Coils.PlcSystemFault, emergency ||
            _communicationTimedOut || (_latchedAlarms & ~(AlarmMask("ZoneFull"))) != 0);
        _store.SetCoilFromPlc(PlcAddressMap.Coils.PlcReadyState, ready);
        _store.SetHoldingRegisterFromPlc(PlcAddressMap.HoldingRegisters.AlarmBits, _latchedAlarms);
        _store.SetHoldingRegisterFromPlc(PlcAddressMap.HoldingRegisters.AlarmSeverity,
            (_latchedAlarms & ((AlarmMask("EmergencyStop")) | (AlarmMask("Communication")) | (AlarmMask("PlcInternal")))) != 0 ? SignalCodes.Value(SignalId.AlarmSeverity, "Severe") :
            (_latchedAlarms & ~(AlarmMask("ZoneFull"))) != 0 ? SignalCodes.Value(SignalId.AlarmSeverity, "Fault") :
            _latchedAlarms != 0 ? SignalCodes.Value(SignalId.AlarmSeverity, "Warning") : (ushort)0);
    }

    private ushort AlarmMask(string name) => checked((ushort)(1 << _store.Definition.AlarmBits.Single(a => a.Name == name).Bit));

    private bool Consume(SimulationFault fault) => _faults.Remove(fault);

}
