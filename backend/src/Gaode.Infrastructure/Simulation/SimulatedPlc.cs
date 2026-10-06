using Gaode.Application.Ports;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Application.Timing;
using Gaode.Infrastructure.Devices.Plc;
using System.Text.Json;

namespace Gaode.Infrastructure.Simulation;

public sealed class SimulatedPlc : IPlcStatePort, IPlcActionPort, IMotionPort,
    IAcquisitionCyclePort, IPlcResetPort, IDisposable
{
    private readonly SimulationProfile _profile;
    private readonly SimulationEventScheduler _scheduler;
    private readonly SimulatedDeviceState _state;
    private readonly object _gate = new();
    private PortEnvelope? _startEnvelope;
    private int _startCommands, _moveCommands, _stopCommands;
    private readonly ITimer _heartbeat;
    private bool _faultLatched, _pauseHeartbeat;
    private DeviceActionEvidence? _inspectionTarget;
    private AcquisitionSession? _activeInspection;
    private readonly CommunicationEvidenceRecorder? recorder;
    private readonly List<AcquisitionState> _acquisitionTransitions = [];
    public int ZoneConfigurations { get; private set; }

    public SimulatedPlc(SimulationProfile profile, SimulationEventScheduler scheduler,
        int heartbeatFlipMs = 500, CommunicationEvidenceRecorder? evidenceRecorder = null)
    {
        if (heartbeatFlipMs <= 0) throw new ArgumentOutOfRangeException(nameof(heartbeatFlipMs));
        recorder = evidenceRecorder;
        _profile = profile;
        _scheduler = scheduler;
        _state = new(profile.DeviceInitial, scheduler.Clock);
        _state.PulseHeartbeat();
        _heartbeat = scheduler.Clock.CreateTimer(_ => { if (!_pauseHeartbeat) _state.PulseHeartbeat(); }, null,
            TimeSpan.FromMilliseconds(heartbeatFlipMs), TimeSpan.FromMilliseconds(heartbeatFlipMs));
    }

    public int StartCommands => Volatile.Read(ref _startCommands);
    public int MoveCommands => Volatile.Read(ref _moveCommands);
    public int StopCommands => Volatile.Read(ref _stopCommands);
    public long HeartbeatCount => _state.HeartbeatCount;

    public IReadOnlyList<AcquisitionState> AcquisitionTransitions { get { lock (_gate) return _acquisitionTransitions.ToArray(); } }
    public DeviceObservation Observe()
    {
        var current = _state.Observe();
        if (current.HasReliableObservation && _scheduler.Clock.GetUtcNow() - current.Identity!.SampleEndedUtc >= TimeSpan.FromSeconds(3))
            SetConnected(false);
        return _state.Observe();
    }
    public void PauseHeartbeat() => _pauseHeartbeat = true;
    public void SetConnected(bool value)
    {
        if (!value) _faultLatched = true;
        _state.SetConnected(value);
    }
    public ValueTask RequestStartAsync(PortEnvelope envelope, Guid actionId, Guid intentWriteId,
        Action<DeviceEvent> onEvent, CancellationToken cancellationToken)
    {
        if (!envelope.IsValid || intentWriteId == Guid.Empty || !Ready())
            throw new InvalidOperationException("模拟PLC启动请求无效或互锁不满足");
        lock (_gate)
        {
            if (_startEnvelope is not null) throw new InvalidOperationException("重复实体启动请求");
            _startEnvelope = envelope;
        }
        Interlocked.Increment(ref _startCommands);
        _scheduler.Respond(_profile.Stages.PlcAcceptance, () =>
        {
            if (ResponsePolicy.IsFailure(_profile.Stages.PlcAcceptance))
            {
                Emit(envelope, DeviceEventKind.Failed, actionId, onEvent,
                    ResponsePolicy.FailureCode(_profile.Stages.PlcAcceptance));
                return;
            }
            if (ResponsePolicy.IsHold(_profile.Stages.PlcAcceptance) || !Ready() ||
                _scheduler.Clock.GetTimestamp() >= envelope.DueTick) return;
            Emit(envelope, DeviceEventKind.Accepted, actionId, onEvent);
        }, cancellationToken);
        return ValueTask.CompletedTask;
    }

    public ValueTask RequestMoveAsync(MoveRequest request, Action<DeviceEvent> onEvent,
        CancellationToken cancellationToken)
    {
        if (!request.Envelope.IsValid || request.IntentWriteId == Guid.Empty || !Ready() ||
            _state.Observe().Readiness != DeviceReadiness.Ready)
            throw new InvalidOperationException("模拟PLC运动互锁不满足");
        Interlocked.Increment(ref _moveCommands);
        _scheduler.Respond(_profile.Stages.PlcAcceptance, () =>
        {
            if (ResponsePolicy.IsFailure(_profile.Stages.PlcAcceptance))
            {
                Emit(request.Envelope, DeviceEventKind.Failed, request.ActionId, onEvent,
                    ResponsePolicy.FailureCode(_profile.Stages.PlcAcceptance));
                return;
            }
            Emit(request.Envelope, DeviceEventKind.Accepted, request.ActionId, onEvent);
            _state.SetMotion(MotionAvailability.InUse);
            Emit(request.Envelope, DeviceEventKind.Executing, request.ActionId, onEvent);
            _ = _scheduler.RespondTrackedAsync(_profile.Stages.XyCompletion, async () =>
            {
                if (ResponsePolicy.IsFailure(_profile.Stages.XyCompletion))
                {
                    Emit(request.Envelope, DeviceEventKind.Failed, request.ActionId, onEvent,
                        ResponsePolicy.FailureCode(_profile.Stages.XyCompletion));
                    return;
                }
                if (ResponsePolicy.IsHold(_profile.Stages.XyCompletion)) return;
                var purpose = request.Role switch
                {
                    "3D" or "FlipPick" or "FlipPutBack" => "XY",
                    "F" or "E" => "ScanZ", "Detection" => "DetectionZ",
                    _ => throw new InvalidOperationException("MoveAxisPurposeUnknown")
                };
                _state.MoveTo(request.Target.X, request.Target.Y, request.Target.Z, purpose);
                var evidence = await EmitCompletionAsync(request.Envelope, request.ActionId,
                    DeviceCompletionMeaning.PositionReached, request.Target, onEvent, cancellationToken, purpose);
                lock (_gate) _inspectionTarget = evidence;
                _scheduler.Duplicates(_profile.Stages.XyCompletion,
                    () => onEvent(new(request.Envelope, DeviceEventKind.Completed, request.ActionId,
                        evidence.Correlation.ConnectionEpoch, Evidence: evidence, ExecutionOrigin: SimulatedDeviceState.Origin)));
            }, cancellationToken);
        }, cancellationToken);
        return ValueTask.CompletedTask;
    }

    public ValueTask RequestStopAsync(PortEnvelope envelope, Action<DeviceEvent> onEvent,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _stopCommands);
        _faultLatched = true;
        if (!_profile.Stop.Respond) return ValueTask.CompletedTask;
        _scheduler.Schedule(_profile.Stop.AcceptedDelayMs,
            () => Emit(envelope, DeviceEventKind.Accepted, Guid.Empty, onEvent), cancellationToken);
        _scheduler.Schedule(_profile.Stop.AcceptedDelayMs + _profile.Stop.CompletedDelayMs, () =>
        {
            _state.SetMotion(MotionAvailability.HeldUnknown);
            Emit(envelope, DeviceEventKind.Stopped, Guid.Empty, onEvent);
        }, cancellationToken);
        return ValueTask.CompletedTask;
    }

    public async ValueTask<AcquisitionSession> OpenCaptureWindowAsync(CaptureWindowRequest request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!Ready() || request.PositionEvidence.Correlation != request.Correlation || request.Target != request.PositionEvidence.Target ||
            !request.PositionEvidence.Matched ||
            !_schedulerClockContains(request.Window)) throw new InvalidOperationException("SimulatedCaptureAdmissionRejected");
        lock (_gate)
        {
            if (_inspectionTarget is null || !_inspectionTarget.Positions.Contains(request.PositionEvidence) || _activeInspection is not null)
                throw new InvalidOperationException("SimulatedCapturePositionNotCurrent");
        }
        var evidence = await EvidenceAsync(request.Correlation, DeviceCompletionMeaning.CaptureAllowed, request.Window,
            [request.PositionEvidence], token);
        var session = new AcquisitionSession(Guid.NewGuid(), request, AcquisitionState.CaptureAllowed, evidence);
        lock (_gate) { _inspectionTarget = null; _activeInspection = session; _acquisitionTransitions.Add(AcquisitionState.CaptureAllowed); }
        _state.SetAcquisition(AcquisitionReadiness.Unavailable);
        return session;
    }
    public Task<CaptureCycleResult> FinishCaptureWindowAsync(AcquisitionSession session, CaptureWorkCommit work,
        ActionWindow window, CancellationToken token)
    {
        if (work.RunId != session.Request.Correlation.RunId || work.OperationId != session.Request.Correlation.OperationId ||
            work.WriteIds.Count == 0 || work.WriteIds.Any(id => id == Guid.Empty) || !work.MediaReleased)
            throw new InvalidOperationException("CaptureWorkNotCommitted");
        return ReleaseAsync(session, window, null, token);
    }
    public Task<CaptureCycleResult> CloseFailedCaptureWindowAsync(AcquisitionSession session, string reason, ActionWindow window, CancellationToken token)
    {
        if (session.Request.Role != CaptureRole.ThreeD || string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("CleanupNotApplicable");
        return ReleaseAsync(session, window, reason, token);
    }
    private async Task<CaptureCycleResult> ReleaseAsync(AcquisitionSession session, ActionWindow window, string? originalFailure, CancellationToken token)
    {
        lock (_gate) if (_activeInspection != session) throw new InvalidOperationException("CaptureSessionNotCurrent");
        if (ResponsePolicy.IsFailure(_profile.Stages.XyCompletion))
        { _faultLatched = true; return new(session, AcquisitionState.HeldUnknown, null, "SimulatedReleaseFailed"); }
        var evidence = await EvidenceAsync(session.Request.Correlation, DeviceCompletionMeaning.CaptureReleased, window,
            [session.Request.PositionEvidence], token);
        lock (_gate) { _activeInspection = null; _acquisitionTransitions.Add(originalFailure is null ? AcquisitionState.Released : AcquisitionState.Blocked); }
        _state.SetAcquisition(AcquisitionReadiness.Available);
        return new(session, originalFailure is null ? AcquisitionState.Released : AcquisitionState.Blocked, evidence, originalFailure);
    }
    public Task<InitialReadinessAssessment> ReadInitialStateAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var actual = Observe();
        var ready = Ready() && actual.Clamp == ClampState.Released && _activeInspection is null && actual.Position is { IsReliable: true };
        return Task.FromResult(new InitialReadinessAssessment(ready ? InitialReadiness.Ready : InitialReadiness.Blocked,
            actual, ready ? [] : ["SimulatedInitialStateNotReady"]));
    }

    public Task ResetAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _startEnvelope = null;
            _faultLatched = false;
            _pauseHeartbeat = false;
            _inspectionTarget = null;
            _activeInspection = null;
            _state.Reset();
        }
        return Task.CompletedTask;
    }

    private bool _schedulerClockContains(ActionWindow window) => window.IsValid && window.Contains(_scheduler.Clock.GetTimestamp());
    private async Task<DeviceActionEvidence> EvidenceAsync(ActionCorrelation correlation, DeviceCompletionMeaning meaning,
        ActionWindow window, IReadOnlyList<PositionReachedEvidence> positions, CancellationToken token,
        Action<Gaode.Infrastructure.Persistence.CommunicationEvidenceReceipt>? observeCommit = null)
    {
        token.ThrowIfCancellationRequested();
        var observation = Observe();
        if (!Ready() || observation.ConnectionEpoch != correlation.ConnectionEpoch || !_schedulerClockContains(window))
            throw new InvalidOperationException("SimulationObservationNotCurrent");
        var receipt = await (recorder ?? throw new InvalidOperationException("SimulationEvidencePersistenceRequired"))
            .RecordSimulationAsync(correlation, observation.Identity!, SimulatedDeviceState.Origin, window,
                JsonSerializer.Serialize(new { kind = "ScheduledSimulationObservation", meaning, observation, positions,
                    protocolTraffic = "NotApplicable" }), token);
        if (receipt.Reference is null || receipt.ActualCommit != ActualCommitState.Committed || receipt.Validity != ReceiptValidity.ValidCurrent)
            throw new InvalidOperationException("SimulationEvidenceCommitUnconfirmed");
        observeCommit?.Invoke(receipt);
        return new(correlation, meaning, [observation.Identity!], positions, null, SimulatedDeviceState.Origin, [receipt.Reference]);
    }
    private async Task<DeviceActionEvidence> EmitCompletionAsync(PortEnvelope envelope, Guid actionId, DeviceCompletionMeaning meaning,
        FixedPoint? target, Action<DeviceEvent> callback, CancellationToken token, string? axisPurpose = null)
    {
        try
        {
            var actual = Observe();
            var c = new ActionCorrelation(envelope.RunId, envelope.OperationId, actionId, envelope.Attempt,
                envelope.SessionId, actual.ConnectionEpoch, envelope.SnapshotId);
            PositionReachedEvidence[] positions = target is null ? [] : [new(c, target,
                actual.PositionForPurpose(axisPurpose ?? throw new InvalidOperationException("MoveAxisPurposeMissing"))!, 0)];
            var evidence = await EvidenceAsync(c, meaning, ActionWindows.From(_scheduler.Clock, envelope.StartTick, envelope.DueTick, envelope.ClockId), positions, token);
            if (target is not null) lock (_gate) _inspectionTarget = evidence;
            callback(new(envelope, DeviceEventKind.Completed, actionId, actual.ConnectionEpoch, Evidence: evidence,
                ExecutionOrigin: SimulatedDeviceState.Origin));
            return evidence;
        }
        catch (Exception)
        { _faultLatched = true; Emit(envelope, DeviceEventKind.UnknownHeld, actionId, callback, "SimulationEvidenceUnconfirmed"); throw; }
    }

    private bool Ready()
    {
        var o = Observe();
        return !_faultLatched && o.HasReliableObservation && o.OperatingMode == OperatingMode.Automatic && o.SafetyAssessment == SafetyAssessment.Clear;
    }
    private void Emit(PortEnvelope envelope, DeviceEventKind kind, Guid actionId,
        Action<DeviceEvent> callback, string? error = null) =>
        callback(new(envelope, kind, actionId, _state.Observe().ConnectionEpoch, error,
            _scheduler.Clock.GetUtcNow(), ExecutionOrigin: SimulatedDeviceState.Origin));

    public void Dispose() => _heartbeat.Dispose();
}
