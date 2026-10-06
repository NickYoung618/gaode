using System.Diagnostics;
using Gaode.Application.Ports;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Plc.Protocol;

namespace Gaode.Infrastructure.Devices.Plc;

public sealed partial class LatestProtocolPlcDevice
{
    private CommunicationEvidenceRecorder? evidenceRecorder;
    private readonly Dictionary<Guid, (long Business, long Heartbeat)> evidenceStarts = [];
    private readonly Dictionary<Guid, DeviceActionEvidence> reachedPositions = [];
    private readonly Dictionary<Guid, AcquisitionSession> captures = [];

    private FaceObservation? lastFace;

    private DiagnosticEvidenceReference? lastReference;
    private Guid? referencedObservation;
    public DeviceObservation Observe() { lock (sync) return Interpret(ReadProtocolSample()); }
    internal void UseEvidenceRecorder(CommunicationEvidenceRecorder recorder)
    {
        lock (sync)
        {
            if (pump is not null) throw new InvalidOperationException("EvidenceRecorderMustBeInstalledBeforeStart");
            evidenceRecorder = recorder;
        }
    }
    private ExecutionOrigin Origin => new(options.Provider == "Real" ? DeviceProvider.Real : DeviceProvider.Virtual,
        $"{options.Provider}:{PlcAddressMap.Contract}", options.Provider == "Real" ? EvidenceQuality.Measured : EvidenceQuality.Derived);
    // Called under sync with the epoch captured before the first read of this sample.
    private ProtocolSample Sample(SignalValues values, long sampledEpoch, DateTimeOffset started)
    {
        var alarm = values.Word(SignalId.AlarmBits);
        var severity = values.Word(SignalId.AlarmSeverity);
        var safe = values.Bit(SignalId.PlcModeAuto) && !values.Bit(SignalId.PlcSystemFault) &&
            !values.Bit(SignalId.ManualZoneOccupied) && alarm == 0 && (severity == 0 || severity == SignalCodes.Value(SignalId.AlarmSeverity, "Warning"));
        return new(true, values.Bit(SignalId.PlcModeAuto), safe,
            values.Float(SignalId.MachineCurrentPosX), values.Float(SignalId.MachineCurrentPosY), sampledEpoch,
            unknown ? "Unknown" : AxisMotion(values),
            DateTimeOffset.UtcNow, values.Float(SignalId.MachineCurrentPosZ), options.Provider,
            alarm, severity, values.Bit(SignalId.PlcSystemFault), values.Bit(SignalId.PlcReadyState),
            unknown ? observation.DiagnosticCode : null, unknown ? observation.FailureOrigin : null,
            values.Bit(SignalId.ManualZoneOccupied))
        { ObservationId = Guid.NewGuid(), SampleStartedUtc = started, SampleConnectionEpoch = sampledEpoch,
            ScanZ = values.Float(SignalId.ScanCurrentPosZ), GrabZ = values.Float(SignalId.FlipGrapCurrentPosZ),
            PositionIdentity = new(Guid.NewGuid(), sampledEpoch, started, DateTimeOffset.UtcNow, DeviceReliability.Reliable) };
    }
    private static string AxisMotion(SignalValues values)
    {
        var axes = new[] { (SignalId.XMoveStart, SignalId.XPosConfirmed),
            (SignalId.YMoveStart, SignalId.YPosConfirmed), (SignalId.ZCameraMoveStart, SignalId.ZCameraPosConfirmed),
            (SignalId.ZScanMoveStart, SignalId.ZScanPosConfirmed), (SignalId.ZGrabMoveStart, SignalId.ZGrapPosConfirmed) };
        if (axes.Any(a => values.Word(a.Item2) != SignalCodes.Value(a.Item2, "Moving") &&
            values.Word(a.Item2) != SignalCodes.Value(a.Item2, "Arrived"))) return "Unknown";
        return axes.Any(a => values.Bit(a.Item1) && values.Word(a.Item2) == SignalCodes.Value(a.Item2, "Moving"))
            ? "Moving" : "Idle";
    }
    private DeviceObservation Interpret(ProtocolSample sample)
    {
        var stale = sample.FailureOrigin == "state-expiry";
        var reliability = stale ? DeviceReliability.Stale : sample.Connected && sample.ObservationId != Guid.Empty ?
            DeviceReliability.Reliable : DeviceReliability.Unavailable;
        var reliable = reliability == DeviceReliability.Reliable;
        var identity = new ObservationIdentity(sample.ObservationId, sample.SampleConnectionEpoch,
            sample.SampleStartedUtc, sample.ObservedUtc, reliability);
        var alarms = new List<AlarmAssessment>();
        ushort knownMask = 0;
        foreach (var alarm in definitionAdmission.Definition.AlarmBits)
        {
            knownMask |= checked((ushort)(1 << alarm.Bit));
            if ((sample.AlarmBits & (1 << alarm.Bit)) == 0) continue;
            alarms.Add(new(alarm.Name, alarm.Severity switch
            { var v when v == SignalCodes.Value(SignalId.AlarmSeverity, "Warning") => AlarmLevel.Warning,
                var v when v == SignalCodes.Value(SignalId.AlarmSeverity, "Fault") => AlarmLevel.Fault,
                var v when v == SignalCodes.Value(SignalId.AlarmSeverity, "Severe") => AlarmLevel.Critical, _ => AlarmLevel.Unknown }, reliability));
        }
        if ((sample.AlarmBits & ~knownMask) != 0) alarms.Add(new("UnrecognizedAlarm", AlarmLevel.Unknown, reliability));
        var reasons = new List<string>();
        if (sample.DiagnosticCode is not null) reasons.Add(sample.DiagnosticCode);
        var safety = !reliable ? SafetyAssessment.Unconfirmed : sample.SafetyClear ? SafetyAssessment.Clear : SafetyAssessment.ExplicitUnsafe;
        var observedPosition = sample.PositionIdentity ?? new(Guid.Empty, sample.SampleConnectionEpoch,
            sample.SampleStartedUtc, sample.ObservedUtc, DeviceReliability.Unavailable);
        var positionReliability = observedPosition.ConnectionEpoch != sample.ConnectionEpoch || unknown
            ? DeviceReliability.Unavailable : DateTimeOffset.UtcNow - observedPosition.SampleStartedUtc >
                TimeSpan.FromMilliseconds(Math.Max(500, options.IoTimeoutMs * 5)) ? DeviceReliability.Stale : observedPosition.Reliability;
        var position = new PositionObservation(sample.X, sample.Y, sample.Z,
            options.Provider == "Virtual" ? "TestMachineAxes" : null,
            options.Provider == "Virtual" ? "SIM_MACHINE" : null,
            options.Provider == "Virtual" ? "Test/InjectedXYZ:mm" : null, observedPosition with { Reliability = positionReliability }, Origin);
        var clamp = ClampState.Unconfirmed; // New protocol has no clamp observation.
        var unavailable = unknown || !reliable || sample.MotionStatus == "Unknown";
        var motion = unavailable ? MotionAvailability.HeldUnknown :
            pending is not null || auxiliary || sample.MotionStatus == "Moving" ? MotionAvailability.InUse : MotionAvailability.Available;
        var acquisition = !reliable ? AcquisitionReadiness.Unconfirmed :
            sample.PlcReady && sample.SafetyClear && !unknown ? AcquisitionReadiness.Available : AcquisitionReadiness.Unavailable;
        return new(reliability, sample.Connected ? DeviceConnection.Connected : DeviceConnection.Disconnected,
            sample.ConnectionEpoch, !reliable ? OperatingMode.Unconfirmed : sample.Automatic ? OperatingMode.Automatic : OperatingMode.NonAutomatic,
            !reliable ? DeviceReadiness.Unconfirmed : sample.PlcReady ? DeviceReadiness.Ready : DeviceReadiness.NotReady,
            safety, clamp, motion, acquisition,
            !reliable ? ManualAreaState.Unconfirmed : sample.ManualZoneOccupied ? ManualAreaState.Occupied : ManualAreaState.Clear,
            ManualHandlingState.Unconfirmed,
            position, lastFace, alarms, reasons, Origin, identity,
            referencedObservation == sample.ObservationId ? lastReference : null)
            { AxisPositions = new(sample.X, sample.Y, sample.Z, sample.ScanZ, sample.GrabZ) };
    }
    private static IReadOnlyList<string> InitialBlockedReasons(IReadOnlyDictionary<string, bool> checks)
    {
        var result = new List<string>();
        if (!checks["Connected"]) result.Add("DeviceObservationUnavailable");
        if (!checks["Automatic"] || !checks["Ready"]) result.Add("DeviceNotReady");
        if (!checks["SafetyClear"] || !checks["AlarmsCleared"]) result.Add("DeviceSafetyNotClear");
        if (!checks["NoManualOccupancy"]) result.Add("DeviceOccupied");
        if (!checks["FinitePosition"]) result.Add("PositionUnconfirmed");
        if (!checks["RecoveryProtocolConfigured"]) result.Add("RecoveryProtocolNotConfigured");
        if (checks.Where(p => p.Key is not ("Connected" or "Automatic" or "Ready" or "SafetyClear" or "AlarmsCleared" or "NoManualOccupancy" or "FinitePosition" or "RecoveryProtocolConfigured")).Any(p => !p.Value))
            result.Add("DeviceWorkNotReleased");
        return result;
    }
    private static ActionCorrelation Correlation(Pending action, long epoch) => new(action.Envelope.RunId,
        action.Envelope.OperationId, action.Id, action.Envelope.Attempt, action.Envelope.SessionId, epoch, action.Envelope.SnapshotId);
    private static ActionWindow Window(PortEnvelope envelope)
    {
        var now = DateTimeOffset.UtcNow; var tick = Stopwatch.GetTimestamp();
        return new(envelope.StartTick, envelope.DueTick, envelope.ClockId,
            now.AddSeconds((envelope.StartTick - tick) / (double)Stopwatch.Frequency),
            now.AddSeconds((envelope.DueTick - tick) / (double)Stopwatch.Frequency));
    }
    private void BeginEvidence(ActionCorrelation correlation)
    {
        lock (sync)
        {
            evidenceStarts.TryAdd(correlation.ActionId, (wire.ExchangeSequence, heartbeat.ExchangeSequence));
            evidenceCorrelation = correlation;
        }
    }
    private async Task<DeviceActionEvidence> CompleteEvidenceAsync(ActionCorrelation correlation,
        DeviceCompletionMeaning meaning, ActionWindow window, CancellationToken token,
        IReadOnlyList<PositionReachedEvidence>? positions = null, FaceObservation? face = null,
        string? bindingId = null, ObservationIdentity? sampledObservation = null,
        (long Business, long Heartbeat)? segmentStart = null, DeviceActionEvidence? precedingEvidence = null,
        Action<Gaode.Infrastructure.Persistence.CommunicationEvidenceReceipt>? observeCommit = null)
    {
        token.ThrowIfCancellationRequested();
        if (!window.Contains(Stopwatch.GetTimestamp()) || correlation.ConnectionEpoch != epoch)
            throw new InvalidOperationException("ActionEvidenceWindowClosed");
        var actual = Observe();
        if (!actual.HasReliableObservation || actual.Identity is null)
            throw new InvalidOperationException("ActionObservationUnavailable");
        var identity = sampledObservation ?? positions?.LastOrDefault()?.Actual.Identity ?? actual.Identity;
        if (!identity.IsValid || identity.ConnectionEpoch != correlation.ConnectionEpoch ||
            identity.Reliability != DeviceReliability.Reliable)
            throw new InvalidOperationException("ActionObservationIdentityInvalid");
        (long Business, long Heartbeat) start;
        lock (sync) if (!evidenceStarts.TryGetValue(correlation.ActionId, out start))
            throw new InvalidOperationException("ActionEvidenceCaptureMissing");
        TransitionEvidenceSegment? flipSegment;
        lock (sync) flipEvidenceSegments.TryGetValue(correlation.ActionId, out flipSegment);
        if (flipSegment is not null) start = (flipSegment.Business, flipSegment.Heartbeat);
        if (segmentStart is not null)
        {
            var capture = precedingEvidence?.Meaning == DeviceCompletionMeaning.CaptureAllowed &&
                meaning == DeviceCompletionMeaning.CaptureReleased;
            var transfer = precedingEvidence?.Meaning == DeviceCompletionMeaning.MaterialPicked &&
                meaning == DeviceCompletionMeaning.MaterialTransferred;
            if (precedingEvidence is not { IsCorrelated: true } || precedingEvidence.Correlation != correlation ||
                (!capture && !transfer))
                throw new InvalidOperationException("PrecedingActionEvidenceInvalid");
            start = segmentStart.Value;
        }
        var business = wire.EvidenceSince(start.Business);
        var pulse = heartbeat.EvidenceSince(start.Heartbeat);
        var receipt = await (evidenceRecorder ?? throw new InvalidOperationException("CommunicationEvidenceStoreUnavailable"))
            .RecordAsync(correlation, identity, Origin, window,
                business.Exchanges.Concat(pulse.Exchanges).OrderBy(e => e.StartedUtc).ToArray(), business.Gap || pulse.Gap,
                options.Float32ByteOrder, meaning.ToString(), token, bindingId, null,
                flipSegment?.References ?? precedingEvidence?.DiagnosticEvidenceReferences,
                captureScope: flipSegment is not null ? "FlipContinuationAfterCommittedSegment" :
                    precedingEvidence?.Meaning == DeviceCompletionMeaning.MaterialPicked ? "PlaceContinuationAfterCommittedPick" : null);
        if (receipt.Reference is null || receipt.Validity != ReceiptValidity.ValidCurrent ||
            receipt.ActualCommit != ActualCommitState.Committed || token.IsCancellationRequested ||
            !window.Contains(Stopwatch.GetTimestamp()) || epoch != correlation.ConnectionEpoch)
            throw new CommunicationEvidenceUnavailableException(receipt);
        lock (sync) { lastReference = receipt.Reference; referencedObservation = identity.ObservationId; }
        observeCommit?.Invoke(receipt);
        return new(correlation, meaning, [identity], positions ?? [], face, Origin,
            (flipSegment?.References ?? precedingEvidence?.DiagnosticEvidenceReferences ?? []).Append(receipt.Reference).Distinct().ToArray());
    }
    private async Task EmitCompletionAsync(Pending action, CancellationToken token)
    {
        var correlation = Correlation(action, epoch);
        var actual = Observe();
        var target = action.Move?.Target;
        PositionReachedEvidence[] positions = target is null ? Array.Empty<PositionReachedEvidence>() :
            [new PositionReachedEvidence(correlation, target, actual.PositionForPurpose(AxisPurpose(action.Move!.Role)) ?? throw new IOException("PositionUnconfirmed"), PositionTolerance)];
        if (positions.Any(p => !p.Matched)) throw new IOException("ActualPositionDoesNotMatch");
        var evidence = await CompleteEvidenceAsync(correlation, target is null ? DeviceCompletionMeaning.RequestSubmitted :
            DeviceCompletionMeaning.PositionReached, Window(action.Envelope), token, positions);
        lock (sync)
        {
            if (pending != action || unknown || action.Cancellation.IsCancellationRequested ||
                correlation.ConnectionEpoch != epoch || !Window(action.Envelope).Contains(Stopwatch.GetTimestamp()))
                throw new InvalidOperationException("MoveCompletionNoLongerCurrent");
            reachedPositions[action.Id] = evidence;
            // Publish completion only after releasing this action's ownership. A consumer may
            // immediately begin the capture whose real position evidence was just committed.
            pending = null;
        }
        action.Callback(new(action.Envelope, DeviceEventKind.Completed, action.Id, epoch, DeviceObservedUtc: actual.Identity!.SampleEndedUtc,
            Evidence: evidence, ExecutionOrigin: Origin));
    }
}

internal sealed class CommunicationEvidenceUnavailableException(
    Gaode.Infrastructure.Persistence.CommunicationEvidenceReceipt receipt) : IOException(receipt.FailureReason ?? "EvidenceReceiptInvalid")
{
    internal Gaode.Infrastructure.Persistence.CommunicationEvidenceReceipt Receipt { get; } = receipt;
}
