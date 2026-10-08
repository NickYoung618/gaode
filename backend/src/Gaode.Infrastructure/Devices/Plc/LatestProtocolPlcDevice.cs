using Gaode.Plc.Protocol;
using Gaode.Application.Ports;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using Gaode.Diagnostics;
using System.Net.Http.Json;

namespace Gaode.Infrastructure.Devices.Plc;

/// <summary>Single in-flight wire action. Host owns business deadlines; this pump never retries an action.</summary>
public sealed partial class LatestProtocolPlcDevice : IPlcStatePort, IPlcActionPort, IMotionPort,
    IAcquisitionCyclePort, IPhysicalHandlingPort, IPlcResetPort, IAsyncDisposable
{
    private sealed class Pending(PortEnvelope envelope, Guid id, Action<DeviceEvent> callback,
        CancellationToken cancellation, MoveRequest? move = null)
    {
        public PortEnvelope Envelope { get; } = envelope;
        public Guid Id { get; } = id;
        public Action<DeviceEvent> Callback { get; } = callback;
        public CancellationToken Cancellation { get; } = cancellation;
        public MoveRequest? Move { get; } = move;
        public int Stage;
        public DateTimeOffset ReadinessRequestedUtc;
    }
    private readonly PlcRuntimeOptions options;
    private readonly ILogger<LatestProtocolPlcDevice> logger;
    private readonly object sync = new();
    private readonly ModbusTcpClient wire, heartbeat;
    private readonly PlcScheduledTransport scheduled;
    private readonly PlcDefinitionAdmission definitionAdmission;
    private readonly PlcSignalAccessor signals, heartbeatSignals;
    internal bool DefinitionAdmitted => definitionAdmission.IsAdmitted;
    internal IReadOnlyList<ModbusWriteDispatch> AllWriteDispatches => wire.WriteDispatches.Concat(heartbeat.WriteDispatches).OrderBy(x => x.Tick).ToArray();
    internal long AllWriteDispatchCount => wire.TotalWriteDispatches + heartbeat.TotalWriteDispatches;
    internal bool HasWriteDispatchGap => wire.WriteDispatchGap || heartbeat.WriteDispatchGap;
    internal IReadOnlyList<ModbusExchange> BusinessExchanges => wire.Exchanges;
    internal ModbusJournalSnapshot BusinessEvidenceSince(long sequence) => wire.EvidenceSince(sequence);
    internal IReadOnlyList<ModbusExchange> HeartbeatExchanges => heartbeat.Exchanges;
    internal ModbusJournalSnapshot HeartbeatEvidenceSince(long sequence) => heartbeat.EvidenceSince(sequence);
    internal ModbusDispatchSnapshot BothChannelDispatchSnapshot
    {
        get
        {
            var business = wire.DispatchSnapshot;
            var pulse = heartbeat.DispatchSnapshot;
            return new(business.Total + pulse.Total, business.Gap || pulse.Gap,
                business.Writes.Concat(pulse.Writes).OrderBy(w => w.Tick).ToArray());
        }
    }
    private readonly CancellationTokenSource lifetime = new();
    private readonly TaskCompletionSource connected = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task? pump, pulse, actionAdvance;
    private Pending? pending;
    private CommunicationCaptureContext? inspectionTarget;
    private CommunicationCaptureContext? activeInspection;
    private ProtocolSample observation;
    private readonly HashSet<Guid> usedActions = [];
    private long epoch = 1;
    private bool unknown, pcReady, auxiliary;
    private volatile bool priorFlipMoveConfirmed;
    private volatile bool stopRequested;
    private int disposed;
    private long heartbeatEdges;
    private string? failure;
    private readonly HeartbeatDiagnosticWindow heartbeatTrace;
    private PortEnvelope? diagnosticEnvelope;
    public long HeartbeatEdges => Interlocked.Read(ref heartbeatEdges);
    public string? Failure { get { lock (sync) return failure; } }
    public double PositionTolerance { get; }

    public LatestProtocolPlcDevice(PlcRuntimeOptions options, double positionTolerance,
        ILogger<LatestProtocolPlcDevice>? logger = null, CommunicationEvidenceRecorder? recorder = null,
        string? mechanicalConfigurationPath = null, string? fieldProfilePath = null)
        : this(options, positionTolerance, logger, null, recorder, mechanicalConfigurationPath, fieldProfilePath) { }

    internal LatestProtocolPlcDevice(PlcRuntimeOptions options, double positionTolerance,
        ILogger<LatestProtocolPlcDevice>? logger, PlcDefinitionTestInput? testInput, CommunicationEvidenceRecorder? recorder = null,
        string? mechanicalConfigurationPath = null, string? fieldProfilePath = null)
    {
        if (mechanicalConfigurationPath is { } mechanicsPath)
            PlcMechanicalConfiguration.Apply(options, mechanicsPath);
        options.LoadFieldProfile(fieldProfilePath);
        options.Validate();
        if (testInput is not null && options.Provider != "Virtual")
            throw new InvalidOperationException("DefinitionFixtureRequiresVirtualTest");
        var definition = testInput?.Definition ?? options.Definition ?? (options.Provider == "Virtual"
            ? ConfirmedProtocol.CreateTest(options.Float32ByteOrder)
            : throw new InvalidOperationException("FormalProtocolDefinitionMissing"));
        definitionAdmission = new(definition, testInput?.PreparedPlan);
        if (!double.IsFinite(positionTolerance) || positionTolerance < 0)
            throw new ArgumentOutOfRangeException(nameof(positionTolerance));
        this.options = options;
        this.logger = logger ?? NullLogger<LatestProtocolPlcDevice>.Instance;
        if (fieldProfilePath is not null)
            this.logger.LogInformation("PLC field layout loaded: source={Source}, fields={Fields}, pendingDefinitionViolations={Violations}",
                definition.SourceReference, definition.Fields.Length,
                string.Join(";", definition.Validate().Select(v => v.Reason + ":" + v.Detail)));
        heartbeatTrace = new("Host/heartbeat-loop", this.logger);
        PositionTolerance = positionTolerance;
        wire = new(options.Host, options.Port, options.UnitId, TimeSpan.FromMilliseconds(options.IoTimeoutMs), this.logger, "business");
        heartbeat = new(options.Host, options.Port, options.UnitId, TimeSpan.FromMilliseconds(options.IoTimeoutMs), this.logger, "heartbeat");
        scheduled = new(wire, options.IoTimeoutMs);
        signals = new(scheduled, definitionAdmission);
        heartbeatSignals = new(heartbeat, definitionAdmission);
        evidenceRecorder = recorder;
        observation = new(false, false, false, 0, 0, epoch, "Disconnected",
            DateTimeOffset.UtcNow, 0, options.Provider);
    }
    private ProtocolSample ReadProtocolSample()
    {
        lock (sync)
        {
            if (disposed == 0 && !unknown && lastHeartbeatEdge != 0 &&
                Stopwatch.GetTimestamp() - lastHeartbeatEdge >= Milliseconds(options.HeartbeatTimeoutMs))
                LatchFailure("HeartbeatStoppedChanging", "heartbeat");
            // A snapshot is assembled from several Modbus requests (coils,
            // motion registers and alarms).  The stale threshold must cover
            // one complete poll round-trip on a real network, while the
            // heartbeat loop remains the authoritative liveness watchdog.
            var staleAfter = TimeSpan.FromMilliseconds(Math.Max(500, options.IoTimeoutMs * 5));
            if (DateTimeOffset.UtcNow - observation.SampleStartedUtc > staleAfter)
                return observation with { SafetyClear = false,
                    DiagnosticCode = observation.DiagnosticCode ?? "ProtocolSampleStale",
                    FailureOrigin = observation.FailureOrigin ?? "state-expiry" };
            return observation;
        }
    }
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        definitionAdmission.Prepare();
        if (pump is not null) throw new InvalidOperationException("AlreadyStarted");
        ThreadPool.GetMinThreads(out var workerMinimum, out var ioMinimum);
        ThreadPool.GetMaxThreads(out var workerMaximum, out _);
        var requiredWorkers = ThreadPoolRuntimePolicy.WindowsNative ? workerMinimum : Math.Min(workerMaximum,
            Math.Max(workerMinimum, Math.Max(8, Environment.ProcessorCount + 4)));
        if (requiredWorkers > workerMinimum &&
            !ThreadPool.SetMinThreads(requiredWorkers, ioMinimum))
            throw new InvalidOperationException("PlcHeartbeatThreadPoolCapacityUnavailable");
        logger.LogInformation("PLC threadpool provider: windowsNative={WindowsNative}; minimum API supported={MinimumSupported}",
            ThreadPoolRuntimePolicy.WindowsNative, !ThreadPoolRuntimePolicy.WindowsNative);
        logger.LogInformation("PLC heartbeat worker minimum: previous={Previous}, effective={Effective}",
            workerMinimum, requiredWorkers);
        logger.LogInformation("PLC starting: {Host}:{Port}, acquisition={Acquisition}, ioTimeoutMs={IoTimeoutMs}, heartbeatTimeoutMs={HeartbeatTimeoutMs}",
            options.Host, options.Port, PlcAcquisitionPolicy.Identity, options.IoTimeoutMs, options.HeartbeatTimeoutMs);
        pulse = HeartbeatAsync(lifetime.Token);
        pump = PumpAsync(lifetime.Token);
        await connected.Task.WaitAsync(cancellationToken);
    }

    public async Task<InitialReadinessAssessment> ReadInitialStateAsync(CancellationToken ct)
    {
        long sampledEpoch;
        lock (sync) sampledEpoch = epoch;
        var sampleStarted = DateTimeOffset.UtcNow;
        var words = await signals.ReadAsync(definitionAdmission.Definition.Fields.Select(p => p.Id), ct);
        ushort R(SignalId id) => words.Word(id);
        bool C(SignalId id) => words.Bit(id);
        ProtocolSample actual;
        lock (sync)
        {
            if (sampledEpoch != epoch) throw new IOException("InitialObservationEpochChanged");
            actual = Sample(words, sampledEpoch, sampleStarted);
        }
        var checks = new Dictionary<string, bool> {
            ["Connected"] = actual.Connected, ["Automatic"] = C(SignalId.PlcModeAuto),
            ["Ready"] = C(SignalId.PlcReadyState), ["SafetyClear"] = actual.SafetyClear && !C(SignalId.PlcSystemFault),
            ["ResetRequestCleared"] = !C(SignalId.SystemResetCmd),
            ["StopCleared"] = !C(SignalId.SoftStopCmd),
            ["NoManualOccupancy"] = words.Words.ContainsKey(SignalId.ManualZoneOccupied) && !C(SignalId.ManualZoneOccupied),
            ["AxesNotTriggered"] = new[] { SignalId.XMoveStart, SignalId.YMoveStart, SignalId.ZCameraMoveStart, SignalId.ZScanMoveStart, SignalId.ZGrabMoveStart }.All(id => !C(id)),
            ["FlipCommandCleared"] = R(SignalId.FlipSorting) == SignalCodes.Value(SignalId.FlipSorting, "Idle"),
            ["SortingCleared"] = R(SignalId.SortingCmd) == SignalCodes.Value(SignalId.SortingCmd, "Idle") && R(SignalId.SortingExecStatus) == SignalCodes.Value(SignalId.SortingExecStatus, "Idle"),
            ["AlarmsCleared"] = R(SignalId.AlarmBits) == 0,
            ["FinitePosition"] = double.IsFinite(actual.X) && double.IsFinite(actual.Y) && double.IsFinite(actual.Z)
        };
        var siteRecovery = definitionAdmission.Definition.IsSiteLayout &&
            options.SiteOperations is { IsValid: true, RestoresWorkpieceAndMechanisms: true };
        checks["RecoveryProtocolConfigured"] = siteRecovery;
        if (definitionAdmission.Definition.IsSiteLayout)
        {
            // Site safety comes from the confirmed alarm/independent interlocks,
            // not the absent legacy ManualZoneOccupied point.
            checks.Remove("NoManualOccupancy");
            checks["ThisSystemResetObserved"] = verifiedSystemResetEpoch == sampledEpoch && !unknown;
            checks["PcReadyAndStartClear"] = C(SignalId.PcSystemReady) && !C(SignalId.PcStartCmd);
            checks["AxisFeedbackValid"] = PreparedPlcReadPlans.Axes.All(id => ClosedAxisFeedbackValid(id, R(id))) &&
                !C(SignalId.RotateStart) && ClosedAxisFeedbackValid(SignalId.RPosConfirmed, R(SignalId.RPosConfirmed));
            checks["FlipFeedbackCleared"] = R(SignalId.FlipStatus) == 0 && R(SignalId.FlipUnloadStatus) == 0;
            checks["AllLinearAxesAtSafeZero"] = PreparedPlcReadPlans.Position.All(id =>
                float.IsFinite(words.Float(id)) && Math.Abs(words.Float(id)) <= PositionTolerance);
        }
        RuntimeDiagnostics.Record("RecoveryInitialObservation", checks.Values.All(x => x) ? "Passed" : "Blocked", null,
            new { actual, checks, protocol = PlcAddressMap.Contract,
                axisFeedback = PreparedPlcReadPlans.Axes.Select(id => new { signal = id.ToString(), raw = R(id) }).ToArray(),
                rotationFeedback = R(SignalId.RPosConfirmed), putBackFeedback = R(SignalId.FlipUnloadStatus) },
            warning: checks.Values.Any(x => !x));
        var interpreted = Interpret(actual);
        var blocked = InitialBlockedReasons(checks);
        return new(blocked.Count == 0 ? InitialReadiness.Ready : InitialReadiness.Blocked, interpreted, blocked,
            siteRecovery ? verifiedSystemResetEpoch : null);
    }

    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        definitionAdmission.RequireAdmitted();
        lock (sync)
        {
            if (disposed != 0) throw new ObjectDisposedException(nameof(LatestProtocolPlcDevice));
            pending = null;
            inspectionTarget = null;
            activeInspection = null;
            reachedPositions.Clear(); captures.Clear(); evidenceStarts.Clear(); flipEvidenceSegments.Clear(); pickEvidenceSegments.Clear(); evidenceCorrelation = null;
            awaitingPutBack = null; lastFace = null;
            // Reset invalidates the old manual request, not the physical occupancy.
            // Fresh PLC safety/area observations below still gate any new action.
            lastReference = null; referencedObservation = null;
            priorFlipMoveConfirmed = false;
            unknown = false;
            failure = null;
            pcReady = false;
            verifiedSystemResetEpoch = null;
            stopRequested = false;
            epoch++;
            resetting = true;
            acquisitionPaused = true;
            foreach (var group in groups.Values)
            {
                group.Generation++; group.Latest = null; group.Demand = false;
                group.Enabled = group.Name is "B" or "P"; group.Fast = false;
                group.Due = Stopwatch.GetTimestamp(); SignalGroup(group);
            }
            sampledWords.Clear(); sampledStamps.Clear(); positionIdentity = null;
            observation = observation with { Connected = false, SafetyClear = false,
                ConnectionEpoch = epoch, MotionStatus = "Resetting",
                DiagnosticCode = "PlcResetting", FailureOrigin = "reset" };
        }
        WakeSampling();
        try
        {
        await wire.ResetConnectionAsync(cancellationToken);
        await heartbeat.ResetConnectionAsync(cancellationToken);
        if (definitionAdmission.Definition.IsSiteLayout)
        {
            await ResetSiteHandshakeAsync(cancellationToken);
            return;
        }
        await signals.WriteBitAsync(SignalId.SystemResetCmd, true, cancellationToken);
        await signals.WriteBitAsync(SignalId.SystemResetCmd, false, cancellationToken);
        await signals.WriteBitAsync(SignalId.PcSystemReady, false, cancellationToken);
        await signals.WriteBitAsync(SignalId.PcSystemReady, true, cancellationToken);
        lock (sync) { pcReady = true; acquisitionPaused = false; }
        WakeSampling();
        EnsureLoops();
        var after = Stopwatch.GetTimestamp();
        var immediate = true;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sample = await WaitGroupAsync("B", after, immediate, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var observed = ReadProtocolSample();
            if (observed.Connected && observed.PlcReady && observed.SafetyClear) return;
            after = sample.Ended + 1; immediate = false;
        }
        }
        catch (Exception error) { LatchFailure(error.Message, "reset", error); throw; }
        finally { lock (sync) { resetting = false; acquisitionPaused = false; } WakeSampling(); }
    }

    private void EnsureLoops()
    {
        definitionAdmission.RequireAdmitted();
        if (pump is null || pump.IsCompleted) pump = PumpAsync(lifetime.Token);
        if (pulse is null || pulse.IsCompleted) pulse = HeartbeatAsync(lifetime.Token);
    }
    public async ValueTask RequestStartAsync(PortEnvelope envelope, Guid actionId, Guid intentWriteId,
        Action<DeviceEvent> onEvent, CancellationToken cancellationToken)
    {
        await EnsureAdmissionObservationsAsync(false, cancellationToken);
        lock (sync)
        {
            definitionAdmission.RequireAdmitted();
            RequireAvailable();
            if (!envelope.IsValid || intentWriteId == Guid.Empty || !usedActions.Add(actionId))
                throw new InvalidOperationException("StartEvidenceInvalid");
            BeginEvidence(new(envelope.RunId, envelope.OperationId, actionId, envelope.Attempt, envelope.SessionId, epoch, envelope.SnapshotId));
            pending = new(envelope, actionId, onEvent, cancellationToken);
            WakeSampling();
            actionAdvance = AdvanceAdmittedAsync(pending, actionAdvance, lifetime.Token);
            diagnosticEnvelope = envelope;
        }
    }
    public async ValueTask RequestMoveAsync(MoveRequest request, Action<DeviceEvent> onEvent, CancellationToken cancellationToken)
    {
        using var admission = LimitTo(Window(request.Envelope), cancellationToken);
        await EnsureAdmissionObservationsAsync(true, admission.Token);
        lock (sync)
        {
            RequireAvailable();
            if (!request.Envelope.IsValid) throw new InvalidOperationException("MoveEvidenceInvalid:Envelope");
            if (request.IntentWriteId == Guid.Empty) throw new InvalidOperationException("MoveEvidenceInvalid:Intent");
            if (inspectionTarget is not null || activeInspection is not null)
                throw new InvalidOperationException("MoveEvidenceInvalid:InspectionPending");
            if (!observation.PlcReady) throw new InvalidOperationException("MoveEvidenceInvalid:Readiness");
            if (!Finite(request.Target)) throw new InvalidOperationException("MoveEvidenceInvalid:Target");
            if (request.Role is not ("3D" or "F" or "E" or "Detection" or "FlipPick" or "FlipPutBack"))
                throw new InvalidOperationException("MoveEvidenceInvalid:Role");
            if (request.Role == "FlipPick") ResolveProgram(request.FlipPreparation ?? throw new InvalidOperationException("FlipPreparationMissing"));
            else if (request.FlipPreparation is not null) throw new InvalidOperationException("FlipPreparationWrongPurpose");
            if (!usedActions.Add(request.ActionId)) throw new InvalidOperationException("MoveEvidenceInvalid:Duplicate");
            BeginEvidence(new(request.Envelope.RunId, request.Envelope.OperationId, request.ActionId,
                request.Envelope.Attempt, request.Envelope.SessionId, epoch, request.Envelope.SnapshotId));
            pending = new(request.Envelope, request.ActionId, onEvent, cancellationToken, request);
            WakeSampling();
            diagnosticEnvelope = request.Envelope;
            // The observation poll remains live, but its entire read plan must not
            // consume this action's acceptance window before motion can begin.
            actionAdvance = AdvanceAdmittedAsync(pending, actionAdvance, lifetime.Token);
        }
    }
    public ValueTask RequestStopAsync(PortEnvelope envelope, Action<DeviceEvent> onEvent, CancellationToken cancellationToken)
    {
        lock(sync) { diagnosticEnvelope=envelope; stopRequested = true; }
        WakeSampling();
        // Write acceptance and physical stop are different. This protocol has no reliable stopped acknowledgement.
        return ValueTask.CompletedTask;
    }

    private async ValueTask BeginInspectionAsync(CommunicationCaptureContext context, CancellationToken cancellationToken)
    {
        await EnsureAdmissionObservationsAsync(true, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            RequireAvailable();
            if (inspectionTarget != context || context.ConnectionEpoch != epoch)
                throw new InvalidOperationException("InspectionOperationEpochMismatch");
            var position = Observe().PositionForPurpose(AxisPurpose(context.Role));
            if (position is not { IsReliable: true } ||
                Math.Abs(position.ActualX!.Value - context.Target.X) > PositionTolerance ||
                Math.Abs(position.ActualY!.Value - context.Target.Y) > PositionTolerance ||
                position.ActualZ is { } z && Math.Abs(z - context.Target.Z) > PositionTolerance)
                throw new IOException("InspectionCoordinatesMismatch");
            inspectionTarget = null; activeInspection = context;
        }
        // Capture/algorithm/input-release are Host work. The new protocol has no
        // inspection-state or reset ACK to write or fabricate.
    }

    private async Task CompleteInspectionAsync(CommunicationCaptureContext context,
        TimeSpan timeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (timeout <= TimeSpan.Zero) throw new TimeoutException("CaptureReleaseWindowClosed");
        var started = Stopwatch.GetTimestamp();
        await RefreshBaseAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (Stopwatch.GetElapsedTime(started) >= timeout) throw new TimeoutException("CaptureReleaseWindowClosed");
        lock (sync)
        {
            RequireAvailable();
            if (activeInspection != context || context.ConnectionEpoch != epoch)
                throw new InvalidOperationException("InspectionOperationEpochMismatch");
            var actual = ReadProtocolSample();
            if (!actual.Connected || !actual.SafetyClear) throw new IOException("CaptureReleaseObservationLost");
            activeInspection = null;
        }
        WakeSampling();
    }

    private void RequireAvailable()
    {
        definitionAdmission.RequireAdmitted();
        var current = ReadProtocolSample();
        if (current.SafetyUnconfirmed)
        {
            RuntimeDiagnostics.Record("PlcAdmission", "SiteSafetyAndInitialAdmissionUnconfirmed", diagnosticEnvelope?.RunId,
                new { current.ObservationId, current.ConnectionEpoch, protocol = definitionAdmission.Definition.SourceReference,
                    dependencies = "PLC-Q3/Q4", disposition = "NoMotionDispatch" }, warning: true);
            throw new InvalidOperationException("SiteSafetyAndInitialAdmissionUnconfirmed:PLC-Q3/Q4");
        }
        if (!options.ActionsEnabled || unknown || stopRequested || auxiliary || pending is not null ||
            !current.Connected || !current.Automatic || !current.SafetyClear)
        {
            RuntimeDiagnostics.Record("PlcAdmission", "Unavailable", diagnosticEnvelope?.RunId,
                new { options.ActionsEnabled, unknown, stopRequested, auxiliary,
                    pendingAction = pending?.Id,
                    failure, observation }, warning: true);
            throw new InvalidOperationException("PlcUnavailableOrActionInFlight");
        }
    }
    private static bool Finite(FixedPoint p) => double.IsFinite(p.X) && double.IsFinite(p.Y) &&
        double.IsFinite(p.Z) && float.IsFinite((float)p.X) && float.IsFinite((float)p.Y) && float.IsFinite((float)p.Z);

    private object HeartbeatContext()
    {
        lock (sync)
        {
            var inspection = activeInspection ?? inspectionTarget;
            var envelope = pending?.Envelope ?? diagnosticEnvelope;
            return new { epoch, runId = inspection?.RunId ?? envelope?.RunId,
                operationId = inspection?.OperationId ?? envelope?.OperationId, actionId = pending?.Id,
                association = pending is not null ? "ActiveAction" : inspection is not null
                    ? "InspectionHandshake" : envelope is not null ? "LastSubmittedActionNotCurrentStage" : "NoRun",
                role = pending?.Move?.Role, envelope?.ConfigVersion, envelope?.SnapshotId,
                unknown, failure, pcReady, observation.ObservedUtc, observation.AlarmBits,
                observation.MotionStatus, observation.PlcReady };
        }
    }

    private long lastHeartbeatEdge;
    private TaskCompletionSource heartbeatChanged = NewSignal();
    private async Task HeartbeatDeadlineAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            Task changed; long due;
            lock (sync) { due = lastHeartbeatEdge + Milliseconds(options.HeartbeatTimeoutMs); changed = heartbeatChanged.Task; }
            var remaining = due - Stopwatch.GetTimestamp();
            if (remaining <= 0) { LatchFailure("HeartbeatStoppedChanging", "heartbeat"); return; }
            using var timer = CancellationTokenSource.CreateLinkedTokenSource(token);
            await Task.WhenAny(changed, Task.Delay(TimeSpan.FromSeconds(remaining / (double)Stopwatch.Frequency), timer.Token));
            await timer.CancelAsync();
        }
    }
    private async Task HeartbeatAsync(CancellationToken ct)
    {
        bool? previous = null;
        lock (sync) lastHeartbeatEdge = Stopwatch.GetTimestamp();
        using var monitorStop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var monitor = HeartbeatDeadlineAsync(monitorStop.Token);
        var due = Stopwatch.GetTimestamp();
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var awakened = Stopwatch.GetTimestamp();
                PlcCommunicationMeasurement.Source.Value = "H";
                var values = await heartbeatSignals.ReadAsync([SignalId.PlcHeartbeatReq], ct);
                var bit = values.Bit(SignalId.PlcHeartbeatReq);
                var read = values.Stamps[SignalId.PlcHeartbeatReq];
                var observedAt = Stopwatch.GetTimestamp();
                // Compare with the original edge deadline BEFORE accepting a late response.
                lock (sync)
                {
                    observedAt = Stopwatch.GetTimestamp(); // Lock wait cannot renew an already elapsed edge deadline.
                    if (observedAt >= lastHeartbeatEdge + Milliseconds(options.HeartbeatTimeoutMs) ||
                        observation.FailureOrigin == "heartbeat") throw new IOException("HeartbeatStoppedChanging");
                    if (previous is not null && previous != bit)
                    {
                        lastHeartbeatEdge = observedAt; Interlocked.Increment(ref heartbeatEdges);
                        var old = heartbeatChanged; heartbeatChanged = NewSignal(); old.TrySetResult();
                    }
                }
                var publishedAt = Stopwatch.GetTimestamp();
                var echoed = previous != bit;
                long echoedAt = 0;
                if (echoed)
                {
                    PlcCommunicationMeasurement.Source.Value = previous is null ? "H:initial" : "H:echo";
                    await heartbeatSignals.WriteBitAsync(SignalId.PcHeartbeatResp, bit, ct);
                    echoedAt = Stopwatch.GetTimestamp();
                }
                previous = bit;
                if (PlcTimingTrace.Enabled) PlcTimingTrace.Record("heartbeat-cycle", new
                { due, awakened, readStarted = read.Sent, responseCollected = read.Ended, observedAt, publishedAt, echoedAt, bit, echoed, epoch });
                heartbeatTrace.Record("cycle", new { plannedDue = due, readStarted = read.Sent, readFinished = observedAt,
                    observedAt, echoedAt, bit, echoed, lastHeartbeatEdge, epoch,
                    dueLatenessMs = (read.Sent - due) * 1000d / Stopwatch.Frequency,
                    hostObservationToEchoMs = echoedAt == 0 ? (double?)null : (echoedAt - observedAt) * 1000d / Stopwatch.Frequency });
                if (read.Sent - due > Milliseconds(25)) heartbeatTrace.Capture("HeartbeatScheduleDelay", context: HeartbeatContext());
                var finished = Stopwatch.GetTimestamp();
                var period = Milliseconds(PlcAcquisitionPolicy.HeartbeatMs);
                var started = read.Sent == 0 ? observedAt : read.Sent;
                PlcCommunicationMeasurement.Round("H", due, read.Queued,
                    [read], publishedAt, awakened, epoch: epoch);
                due = started + Math.Max(1, (finished - started) / period + 1) * period;
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, due - Stopwatch.GetTimestamp()) / (double)Stopwatch.Frequency), ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception error) { LatchFailure(error.Message, "heartbeat", error); }
        finally { await monitorStop.CancelAsync(); await monitor; }
    }
    private async Task AdvanceStart(Pending p, CancellationToken ct)
    {
        using var deadline = LimitTo(Window(p.Envelope), p.Cancellation);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        var token = linked.Token;
        var admittedEpoch = epoch;
        using var dispatch = ActionDispatchEligibility(Window(p.Envelope), admittedEpoch, token);
        var starts = await signals.ReadAsync([SignalId.XMoveStart, SignalId.YMoveStart,
            SignalId.ZCameraMoveStart, SignalId.ZScanMoveStart, SignalId.ZGrabMoveStart], token);
        if (new[] { SignalId.XMoveStart, SignalId.YMoveStart, SignalId.ZCameraMoveStart,
                SignalId.ZScanMoveStart, SignalId.ZGrabMoveStart }.Any(starts.Bit))
            throw new IOException("PreviousAxisWorkNotReleased");
        await signals.WriteBitAsync(SignalId.PcSystemReady, true, token);
        pcReady = true; p.ReadinessRequestedUtc = DateTimeOffset.UtcNow; p.Stage = 1;
        var after = Stopwatch.GetTimestamp();
        var immediate = true;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var sampled = await WaitGroupAsync("B", after, immediate, token);
            token.ThrowIfCancellationRequested();
            if (!Window(p.Envelope).Contains(Stopwatch.GetTimestamp())) throw new TimeoutException("StartupWindowClosed");
            var observed = ReadProtocolSample();
            if (!observed.Connected || !observed.SafetyClear || observed.ConnectionEpoch != admittedEpoch)
                throw new IOException("StartupConnectionOrSafetyLost");
            if (observed.PlcReady && observed.SampleStartedUtc >= p.ReadinessRequestedUtc) break;
            after = sampled.Ended + 1; immediate = false;
        }
        if (definitionAdmission.Definition.IsSiteLayout)
            await StartSiteHandshakeAsync(admittedEpoch, token);
        var evidence = await CompleteEvidenceAsync(Correlation(p, epoch), DeviceCompletionMeaning.RequestSubmitted,
            Window(p.Envelope), token);
        lock (sync) pending = null;
        WakeSampling();
        logger.LogInformation("PLC startup readiness observed: runId={RunId}, operationId={OperationId}, actionId={ActionId}, epoch={Epoch}; no clamp or physical button fact inferred",
            p.Envelope.RunId, p.Envelope.OperationId, p.Id, epoch);
        p.Callback(new(p.Envelope, DeviceEventKind.Accepted, p.Id, epoch,
            Evidence: evidence, ExecutionOrigin: Origin));
    }

    internal IPlcTransport StageTransport => scheduled;

    private async Task<ObservationIdentity> ExecuteTransitionCommandAsync(ActionCorrelation correlation, ActionWindow window,
        bool puttingBack, CancellationToken token, Action admitted)
    {
        lock (sync)
        {
            RequireAvailable();
            if (epoch != correlation.ConnectionEpoch || inspectionTarget is not null || activeInspection is not null ||
                !observation.PlcReady || observation.ManualZoneOccupied || !priorFlipMoveConfirmed || !usedActions.Add(correlation.ActionId))
                throw new InvalidOperationException("TransitionPreconditionFailed");
            auxiliary = true;
        }
        WakeSampling();
        admitted();
        var group = puttingBack ? "U" : "F";
        using var dispatch = ActionDispatchEligibility(window, correlation.ConnectionEpoch, token);
        try
        {
            var feedback = puttingBack ? SignalId.FlipUnloadStatus : SignalId.FlipStatus;
            var complete = SignalCodes.Value(feedback, "Completed");
            var baseline = await signals.ReadWordAsync(feedback, token);
            CheckAxisWindow(window, correlation.ConnectionEpoch, token);
            if (baseline == complete || !puttingBack && baseline != SignalCodes.Value(feedback, "Idle"))
                throw new IOException("TransitionFeedbackNotFresh");
            lock (sync) axisClosures.Remove(SignalId.ZGrabMoveStart);
            await signals.WriteWordAsync(SignalId.FlipSorting,
                SignalCodes.Value(SignalId.FlipSorting, puttingBack ? "PutBack" : "Flip"), token);
            CheckAxisWindow(window, correlation.ConnectionEpoch, token);
            var after = Stopwatch.GetTimestamp();
            SetFeedback(group, true, true);
            var executingObserved = false;
            while (true)
            {
                CheckAxisWindow(window, correlation.ConnectionEpoch, token);
                var sample = await WaitGroupAsync(group, after, false, token);
                CheckAxisWindow(window, correlation.ConnectionEpoch, token);
                var state = sample.Values.Word(feedback);
                if (state == SignalCodes.Value(feedback, "Failed")) throw new IOException(puttingBack ? "PutBackFailedFeedback" : "FlipFailedFeedback");
                if (state == SignalCodes.Value(feedback, "Executing"))
                { executingObserved = true; SetFeedback(group, true); }
                else if (state != SignalCodes.Value(feedback, "Idle") && state != complete) throw new IOException("TransitionUnknownFeedback");
                if (state == complete)
                {
                    if (!executingObserved) throw new IOException("FlipCurrentExecutionUnconfirmed");
                    if (puttingBack)
                    {
                        CheckAxisWindow(window, correlation.ConnectionEpoch, token);
                        await ClearAndConfirmAsync("FlipPutBack", "U", SignalId.FlipSorting, false,
                            PreparedPlcReadPlans.FlipClear, window, correlation.ConnectionEpoch, token);
                    }
                    CheckAxisWindow(window, correlation.ConnectionEpoch, token);
                    logger.LogInformation("PLC transition completed: runId={RunId}, actionId={ActionId}, epoch={Epoch}, putBack={PutBack}",
                        correlation.RunId, correlation.ActionId, correlation.ConnectionEpoch, puttingBack);
                    return sample.Identity;
                }
                await SaveTransitionSegmentAsync(correlation, window, token);
                CheckAxisWindow(window, correlation.ConnectionEpoch, token);
                after = sample.Ended + 1;
            }
        }
        catch (Exception error) { LatchFailure(error.Message); stopRequested = true; WakeSampling(); throw; }
        finally { SetFeedback(group, false); WakeSampling(); }
    }
    internal Float32ByteOrder StageFloat32ByteOrder => options.Float32ByteOrder;
    internal ILogger StageLogger => logger;
    internal ResultSource StageResultSource => options.Provider == "Real"
        ? ResultSource.Real : ResultSource.Virtual;
    internal ResultQuality StageResultQuality => options.Provider == "Real"
        ? ResultQuality.Measured : ResultQuality.Derived;
    internal string StageComponentVersion => options.Provider == "Real"
        ? $"Real:{definitionAdmission.Definition.SourceReference}" : $"{options.Provider}:{PlcAddressMap.Contract}";

    internal async Task BeginStageActionAsync(PlcStageActionRequest request, Guid actionId, CancellationToken token)
    {
        using var admission = LimitTo(request.Window, token);
        await EnsureAdmissionObservationsAsync(true, admission.Token);
        lock (sync)
        {
            RequireAvailable();
            if (!request.IsValid || request.ConnectionEpoch != epoch ||
                !observation.PlcReady || inspectionTarget is not null || activeInspection is not null || !usedActions.Add(actionId))
                throw new InvalidOperationException("StageActionPreconditionFailed");
            auxiliary = true;
        }
        WakeSampling();
    }
    internal void EndStageAction() { lock (sync) auxiliary = false; WakeSampling(); }

    internal void HoldUnknownStageAction(long actionEpoch, string error)
    {
        lock (sync)
        {
            // The old result remains UnknownHeld for the business owner. Its late
            // observer must not inject a new stop into an explicit reset's new epoch.
            if (actionEpoch != epoch)
            {
                logger.LogWarning("Old stage remains unknown after epoch change: actionEpoch={ActionEpoch}, currentEpoch={Epoch}, reason={Reason}, noReplay=true",
                    actionEpoch, epoch, error);
                return;
            }
            LatchFailure(error);
            stopRequested = true;
        }
    }
    private async Task AdvanceAdmittedAsync(Pending work, Task? previous, CancellationToken token)
    {
        try
        {
            if (previous is not null) await previous;
            lock (sync) if (unknown || pending != work) return;
            work.Cancellation.ThrowIfCancellationRequested();
            if (work.Move is null) await AdvanceStart(work, token);
            else await AdvanceMove(work, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) { LatchFailure(error.Message, "action-advance", error); }
    }
    private async Task AdvanceMove(Pending p, CancellationToken ct)
    {
        if (p.Stage != 0) return;
        p.Stage = 1;
        var request = p.Move!;
        using var deadline = LimitTo(Window(request.Envelope), p.Cancellation);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        using var dispatch = ActionDispatchEligibility(Window(request.Envelope), epoch, stop.Token);
        if (request.Role == "FlipPick")
        {
            var preparation = request.FlipPreparation ?? throw new InvalidOperationException("FlipPreparationMissing");
            var program = ResolveProgram(preparation);
            if (definitionAdmission.Definition.IsSiteLayout)
                await signals.WriteFloatAsync(SignalId.ModelNumber, checked((float)program.ModelNumber!.Value), stop.Token);
            else await signals.WriteWordsAsync(SignalId.ModelPayload, program.ModelWords, stop.Token);
            await signals.WriteWordAsync(SignalId.FlipTargetFace, program.TargetFaceWord, stop.Token);
        }
        await DriveTargetAsync(request.Target, request.Role, Window(request.Envelope), epoch,
            () => { Emit(p, DeviceEventKind.Accepted); Emit(p, DeviceEventKind.Executing); }, stop.Token);
        lock (sync)
        {
            if (request.Role is not ("FlipPick" or "FlipPutBack"))
                inspectionTarget = new CommunicationCaptureContext(request.Envelope.RunId,
                    request.Envelope.OperationId, epoch, request.Target) { Role = request.Role };
            else priorFlipMoveConfirmed = true;
            if (request.Role == "FlipPutBack") putBackPositionActionId = p.Id;
            if (request.FlipPreparation is { } preparation) flipPreparations.Add(p.Id, preparation);
        }
        await EmitCompletionAsync(p, stop.Token);
    }
    private ushort[] Encode(double value) => Float32Codec.Encode((float)value, options.Float32ByteOrder);
    private void Emit(Pending p, DeviceEventKind kind, string? error = null) =>
        p.Callback(new(p.Envelope, kind, p.Id, epoch, error, DateTimeOffset.UtcNow, ExecutionOrigin: Origin));
    private void LatchFailure(string message, string origin = "action", Exception? exception = null,
        ActionCorrelation? failedCorrelation = null)
    {
        var diagnosticContext = HeartbeatContext();
        Pending? work; ProtocolSample prior; long failedEpoch;
        lock (sync)
        {
            if (unknown) return;
            prior = observation;
            failedEpoch = epoch;
            unknown = true; axisClosures.Clear(); failure = message; work = pending;
            CaptureFailureEvidence(failedCorrelation ?? (work is not null ? Correlation(work, epoch) :
                (auxiliary || activeInspection is not null ? evidenceCorrelation : null)),
                prior, origin + ":" + message);
            pending = null;
            epoch++;
            observation = observation with { Connected = false, SafetyClear = false, MotionStatus = "Unknown",
                ConnectionEpoch = epoch,
                DiagnosticCode = origin == "heartbeat" ? "PlcHeartbeatLost" :
                    message == "SafetyInterlockLost" ? "PlcSafetyInterlockLost" : "PlcCommunicationUnknown",
                FailureOrigin = origin };
        }
        logger.LogError(exception,
            "PLC failure latched at {FailedAtUtc:o}: origin={Origin}, reason={Reason}, epoch={Epoch}, lastObservationUtc={LastObservationUtc:o}, observationAgeMs={ObservationAgeMs}, priorConnected={PriorConnected}, priorSafetyClear={PriorSafetyClear}, priorAlarmBits={PriorAlarmBits}, priorAlarmSeverity={PriorAlarmSeverity}, priorPlcSystemFault={PriorPlcSystemFault}, heartbeatEdges={HeartbeatEdges}, runId={RunId}, actionId={ActionId}. New physical actions are blocked; no automatic resend.",
            DateTimeOffset.UtcNow, origin, message, failedEpoch, prior.ObservedUtc,
            (DateTimeOffset.UtcNow - prior.ObservedUtc).TotalMilliseconds,
            prior.Connected, prior.SafetyClear, prior.AlarmBits, prior.AlarmSeverity, prior.PlcSystemFault,
            HeartbeatEdges, work?.Envelope.RunId, work?.Id);
        // Also dump when the business poll sees PLC's alarm first; the heartbeat loop
        // may already have resumed and therefore never throw its own timeout.
        heartbeatTrace.Capture("FailureLatched:" + origin + ":" + message, true, diagnosticContext);
        heartbeat.CaptureDiagnosticWindow("FailureLatched:" + origin + ":" + message, true, diagnosticContext);
        // The observation is already invalidated; the terminal fact still belongs to the failed action's epoch.
        if (work is not null) work.Callback(new(work.Envelope, DeviceEventKind.Failed, work.Id,
            failedEpoch, message, DateTimeOffset.UtcNow, ExecutionOrigin: Origin));
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        lifetime.Cancel();
        await Task.WhenAll(pump ?? Task.CompletedTask, pulse ?? Task.CompletedTask);
        await (actionAdvance ?? Task.CompletedTask);
        Task[] failureSaves;
        lock (sync) failureSaves = failureEvidenceTasks.ToArray();
        await Task.WhenAll(failureSaves);
        await wire.DisposeAsync(); await heartbeat.DisposeAsync(); lifetime.Dispose();
        await heartbeatTrace.FlushAsync();
    }
}
