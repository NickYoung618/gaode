using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using Gaode.Application.Ports;

namespace Gaode.Infrastructure.Devices.Cameras;

public sealed record RealCameraOptions(string SitePath, string WorkerPath, string GalaxySdkPath,
    string CameraProSdkPath, string StateRoot, int StartupTimeoutMs = 30000,
    int CaptureTimeoutMs = 30000, int ShutdownTimeoutMs = 15000);

public sealed record CameraWorkerStatus(string Role, string Serial, string ExpectedNicMac,
    string State, int? ProcessId, Guid SessionId, long Epoch, long MaxBytes,
    int OpenCount, string? Error, CaptureFrameMetadata? Metadata);

public sealed record CameraRecoveryResourceState(string Role, string State, bool OperationActive, bool ProcessRunning,
    bool PipePresent, bool CleanupConfirmed, bool Released);

public sealed class CameraRecoveryRejectedException(CameraWorkerStatus status)
    : InvalidOperationException("CameraRecoveryNotAllowed:" + status.State)
{ public CameraWorkerStatus Status { get; } = status; }
public sealed class CameraRecoveryFailedException(CameraWorkerStatus status)
    : IOException("CameraRecoveryFailed:" + status.Error)
{ public CameraWorkerStatus Status { get; } = status; }

/// <summary>One process, pipe and serialization gate per physical camera. No automatic trigger replay.</summary>
public sealed class PersistentCameraGateway : ICameraSdkGateway
{
    private readonly RealCameraOptions _options;
    private readonly Dictionary<string, Worker> _workers;
    private int _disposed;
    public PersistentCameraGateway(RealCameraOptions options)
    {
        _options = options;
        foreach (var path in new[] { options.SitePath, options.WorkerPath, options.StateRoot,
            options.GalaxySdkPath, options.CameraProSdkPath })
            if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("CameraPathsMustBeAbsolute");
        if (!File.Exists(options.WorkerPath)) throw new FileNotFoundException("CameraWorkerMissing", options.WorkerPath);
        using var doc = JsonDocument.Parse(File.ReadAllText(options.SitePath));
        var bindings = doc.RootElement.GetProperty("Cameras").Deserialize<CameraBinding[]>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        if (bindings.Length == 0 || bindings.Select(x => x.Role).Distinct().Count() != bindings.Length ||
            bindings.Select(x => x.Serial).Distinct().Count() != bindings.Length ||
            bindings.Select(x => NormalizeMac(x.ExpectedNicMac)).Distinct().Count() != bindings.Length ||
            bindings.Any(x => x.Role is not ("A" or "B" or "C" or "D" or "E" or "F" or "3D") ||
                x.Kind != (x.Role == "3D" ? "3D" : "2D") || string.IsNullOrWhiteSpace(x.Serial) ||
                NormalizeMac(x.ExpectedNicMac).Length != 12))
            throw new InvalidDataException("CameraBindingsInvalidOrAmbiguous");
        Directory.CreateDirectory(options.StateRoot);
        _workers = bindings.ToDictionary(x => x.Role, x => new Worker(x, options));
    }
    private static string NormalizeMac(string value) => value.Replace("-", "").Replace(":", "").ToUpperInvariant();
    private Worker Find(string binding) => _workers.GetValueOrDefault(binding) ??
        _workers.Values.SingleOrDefault(x => x.Binding.Serial == binding) ?? throw new InvalidOperationException("CameraBindingNotConfigured:" + binding);
    public long ConnectionEpoch => 0; // Multi-device consumers must use GetConnectionEpoch(binding).
    public long GetConnectionEpoch(string binding) => Find(binding).Epoch;
    public long GetMaxCaptureBytes(string binding, long fallback) => Find(binding).Snapshot() is { State: "Ready", MaxBytes: > 0 } status
        ? status.MaxBytes : throw new InvalidOperationException("CameraNotReady:" + binding);
    public IReadOnlyList<CameraRecoveryResourceState> RecoveryResources => _workers.Values.Select(x => x.RecoveryResource()).ToArray();
    public IReadOnlyList<CameraWorkerStatus> Status => _workers.Values.Select(x => x.Snapshot()).ToArray();
    public async Task StartAsync(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        // Vendor discovery scans all interfaces. Prepare once in order to avoid overlapping
        // discovery broadcasts; capture and explicit recovery remain independent per camera.
        foreach (var worker in _workers.Values) await worker.StartAsync(ct);
    }
    public Task RecoverAsync(string binding, CancellationToken ct)
    {
        if (Volatile.Read(ref _disposed) != 0) throw new CameraRecoveryRejectedException(Find(binding).Snapshot());
        return Find(binding).RecoverAsync(ct);
    }
    public Task OpenAsync(string cameraBindingId, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("CameraLifecycleOwnedByHost");
    public Task ConfigureAsync(string cameraBindingId, int exposureUs, double gain, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("RealCameraPreservesImagingParameters");
    public Task<CameraFrame> TriggerAsync(string binding, string pointVersion, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return Find(binding).CaptureAsync(ct);
    }
    public Task<CameraFrame> CaptureConfiguredAsync(string binding, string pointVersion,
        CameraImagingSettings settings, string settingsDigest, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (settings.ExposureUs <= 0 || settings.Gain is { } gain && (!double.IsFinite(gain) || gain < 0) ||
            string.IsNullOrWhiteSpace(settingsDigest)) throw new ArgumentException("CameraSettingsInvalid");
        return Find(binding).CaptureAsync(ct, settings, settingsDigest);
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        foreach (var worker in _workers.Values) worker.RequestStop();
        await Task.WhenAll(_workers.Values.Select(x => x.StopAsync()));
    }

    private sealed class Worker(CameraBinding binding, RealCameraOptions options)
    {
        public CameraBinding Binding => binding;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private readonly object _stateGate = new();
        private int _stopRequested;
        private Process? _normalClosing;
        private Process? _process;
        private NamedPipeServerStream? _pipe;
        private string _state = "Stopped";
        private bool _cleanupConfirmed = true;
        private string? _error;
        private Guid _session;
        public long Epoch { get; private set; }
        public long MaxBytes { get; private set; }
        private int _openCount;
        private CaptureFrameMetadata? _metadata;
        private ulong? _lastFrame;
        private long _lastTrigger;
        private readonly string _log = Path.Combine(options.StateRoot, binding.Role + ".host.jsonl");
        private readonly object _logGate = new();
        private void Log(string stage, object? detail = null)
        {
            lock (_logGate) File.AppendAllText(_log, JsonSerializer.Serialize(new { utc = DateTimeOffset.UtcNow,
                role = binding.Role, serial = binding.Serial, stage, state = _state, session = _session, epoch = Epoch, detail }) + Environment.NewLine);
        }
        public CameraWorkerStatus Snapshot()
        {
            lock (_stateGate)
            {
                ReconcileExit();
                return new(binding.Role, binding.Serial, binding.ExpectedNicMac,
                    _state, _process is { HasExited: false } ? _process.Id : null, _session, Epoch, MaxBytes, _openCount, _error, _metadata);
            }
        }
        public CameraRecoveryResourceState RecoveryResource()
        {
            var idle = _gate.Wait(0);
            try
            {
                lock (_stateGate)
                {
                    ReconcileExit();
                    var running = _process is { HasExited: false };
                    var pipe = _pipe is not null;
                    var released = idle && (_state == "Ready" && running && pipe ||
                        _state is "Faulted" or "Stopped" && !running && !pipe && _cleanupConfirmed);
                    return new(binding.Role, _state, !idle, running, pipe, _cleanupConfirmed, released);
                }
            }
            finally { if (idle) _gate.Release(); }
        }
        private void ReconcileExit()
        {
            if (_process is { HasExited: true } exited && !ReferenceEquals(exited, _normalClosing) &&
                _state is not ("Faulted" or "Stopped" or "Stopping"))
                Fault(new IOException("CameraWorkerExitedUnexpectedly: exitCode=" + exited.ExitCode));
        }
        private async Task ObserveExitAsync(Process process, Guid session)
        {
            await _gate.WaitAsync();
            try
            {
                lock (_stateGate)
                    if (ReferenceEquals(_process, process) && _session == session) ReconcileExit();
            }
            finally { _gate.Release(); }
        }
        public void RequestStop()
        {
            Interlocked.Exchange(ref _stopRequested, 1);
            lock (_stateGate) { _state = "Stopping"; MaxBytes = 0; }
        }
        public async Task StartAsync(CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try { await StartCoreAsync(ct); }
            finally { _gate.Release(); }
        }
        private async Task StartCoreAsync(CancellationToken ct)
        {
            lock (_stateGate)
            {
                if (Volatile.Read(ref _stopRequested) != 0) throw new CameraRecoveryRejectedException(Snapshot());
                if (_state == "Ready") return;
                _cleanupConfirmed = false;
                _state = "Starting"; _error = null; MaxBytes = 0; _lastFrame = null; _lastTrigger = 0;
                _session = Guid.NewGuid(); Epoch = DateTime.UtcNow.Ticks;
            }
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
            budget.CancelAfter(options.StartupTimeoutMs);
            try
            {
                var pipeName = "gaode-camera-" + _session.ToString("N");
                _pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                var start = new ProcessStartInfo(options.WorkerPath) { UseShellExecute = false,
                    CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true,
                    WorkingDirectory = Path.GetDirectoryName(options.WorkerPath)! };
                foreach (var arg in new[] { "--pipe", pipeName, "--session", _session.ToString(),
                    "--galaxy-sdk", options.GalaxySdkPath, "--camerapro-sdk", options.CameraProSdkPath,
                    "--state-root", Path.Combine(options.StateRoot, binding.Role, _session.ToString("N")) }) start.ArgumentList.Add(arg);
                lock (_stateGate) _process = new Process { StartInfo = start, EnableRaisingEvents = true };
                var currentProcess = _process;
                var currentSession = _session;
                currentProcess.Exited += (_, _) => _ = ObserveExitAsync(currentProcess, currentSession);
                _process.OutputDataReceived += (_, e) => { if (e.Data is not null) Log("WorkerOutput", e.Data); };
                _process.ErrorDataReceived += (_, e) => { if (e.Data is not null) Log("WorkerError", e.Data); };
                if (!_process.Start()) throw new IOException("CameraWorkerStartFailed");
                _process.BeginOutputReadLine(); _process.BeginErrorReadLine();
                Log("ProcessStarted", new { pid = _process.Id });
                await _pipe.WaitForConnectionAsync(budget.Token);
                lock (_stateGate) _state = "Opening";
                var ready = await ExchangeAsync(new("init", _session, Guid.NewGuid()) { Binding = binding }, "ready", 0, budget.Token);
                if (ready.Header.Metadata is not { } m || m.Serial != binding.Serial || m.Role != binding.Role ||
                    m.WorkerSessionId != _session || NormalizeMac(m.NicMac) != NormalizeMac(binding.ExpectedNicMac) ||
                    ready.Header.MaxBytes is <= 0 or > CameraWorkerProtocol.MaxPayloadBytes)
                    throw new InvalidDataException("CameraReadyIdentityOrCapacityMismatch");
                lock (_stateGate)
                {
                    if (_process.HasExited) throw new IOException("CameraWorkerExitedDuringOpen");
                    if (Volatile.Read(ref _stopRequested) != 0) throw new IOException("CameraServiceStopping");
                    _metadata = m; MaxBytes = ready.Header.MaxBytes; _openCount++; _state = "Ready";
                }
                Log("Ready", new { MaxBytes, _openCount, metadata = m });
            }
            catch (Exception e)
            {
                var neverOpened = e is IOException && e.Data["CameraDeviceOpenAttempted"] is false;
                Fault(e); await StopCoreAsync();
                lock (_stateGate) { _state = "Faulted"; _cleanupConfirmed |= neverOpened && _process is null && _pipe is null; }
                Log("RecoveryResourceObserved", new { neverOpened, _cleanupConfirmed, processReleased = _process is null, pipeReleased = _pipe is null });
            }
        }
        private async Task<(CameraWireMessage Header, byte[] Data)> ExchangeAsync(CameraWireMessage request,
            string expected, long maxBytes, CancellationToken ct)
        {
            var pipe = _pipe ?? throw new IOException("CameraPipeMissing");
            await CameraWorkerProtocol.WriteAsync(pipe, request, ReadOnlyMemory<byte>.Empty, ct);
            var response = await CameraWorkerProtocol.ReadAsync(pipe, maxBytes, ct);
            CameraWorkerProtocol.ValidateResponse(request, response.Header, expected);
            return response;
        }
        public async Task<CameraFrame> CaptureAsync(CancellationToken ct, CameraImagingSettings? settings = null, string? digest = null)
        {
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
            budget.CancelAfter(options.CaptureTimeoutMs);
            await _gate.WaitAsync(budget.Token);
            var admitted = false;
            try
            {
                lock (_stateGate)
                {
                    ReconcileExit();
                    if (Volatile.Read(ref _stopRequested) != 0 || _state != "Ready") throw new InvalidOperationException("CameraNotReady:" + binding.Role);
                    _state = "Capturing";
                    admitted = true;
                }
                var request = new CameraWireMessage(settings is null ? "capture" : "capture-configured", _session, Guid.NewGuid())
                    { MaxBytes = MaxBytes, Settings = settings, SettingsDigest = digest };
                Log("CaptureRequested", new { request.RequestId, settings, settingsDigest = digest });
                var frame = await ExchangeAsync(request, "frame", MaxBytes, budget.Token);
                if (settings is not null)
                {
                    var actual = frame.Header.ActualSettings;
                    if (frame.Header.SettingsDigest != digest || actual is null || actual.ExposureUs != settings.ExposureUs ||
                        settings.Gain is { } requestedGain && actual.Gain != requestedGain ||
                        settings.RoiPixels is { } roi && !roi.SequenceEqual(new[] { actual.OffsetX, actual.OffsetY, actual.Width, actual.Height }))
                        throw new InvalidDataException("CameraSettingsReadbackMismatch");
                }
                var m = frame.Header.Metadata ?? throw new InvalidDataException("CameraFrameMetadataMissing");
                if (m.WorkerSessionId != _session || m.Serial != binding.Serial || m.Role != binding.Role ||
                    NormalizeMac(m.NicMac) != NormalizeMac(binding.ExpectedNicMac) ||
                    m.TriggerSequence <= _lastTrigger || (_lastFrame is { } last && m.FrameId <= last) ||
                    m.PayloadBytes != frame.Data.LongLength || frame.Data.Length == 0 || m.ReceivedUtc < m.TriggeredUtc ||
                    frame.Header.Format is not ("GalaxyRaw" or "CameraProFrameZipV1"))
                    throw new InvalidDataException("CameraFreshFrameEvidenceInvalid");
                CameraFrameValidation.Validate(frame.Data, frame.Header.Format!, m, binding.Role, binding.Serial);
                lock (_stateGate)
                {
                    ReconcileExit();
                    if (_state != "Capturing" || Volatile.Read(ref _stopRequested) != 0) throw new IOException("CameraCaptureSessionNoLongerReady");
                    _metadata = m; _lastFrame = m.FrameId; _lastTrigger = m.TriggerSequence; _state = "Ready";
                }
                Log("FrameTaken", new { request.RequestId, m.FrameId, m.TriggerSequence, m.PayloadBytes });
                return new(frame.Data, frame.Header.Format!, frame.Header.ContentType ?? "application/octet-stream", Epoch)
                    { Metadata = m, ActualSettings = frame.Header.ActualSettings };
            }
            catch (Exception e) { if (admitted) { Fault(e); _pipe?.Dispose(); _pipe = null; } throw; }
            finally { _gate.Release(); }
        }
        private void Fault(Exception e)
        {
            lock (_stateGate) { _error = e.ToString(); _state = "Faulted"; MaxBytes = 0; Log("Faulted_NoAutomaticReplay", _error); }
        }
        public async Task RecoverAsync(CancellationToken ct)
        {
            if (!await _gate.WaitAsync(0, ct)) throw new CameraRecoveryRejectedException(Snapshot());
            try
            {
                lock (_stateGate)
                {
                    ReconcileExit();
                    if (Volatile.Read(ref _stopRequested) != 0 || _state != "Faulted") throw new CameraRecoveryRejectedException(Snapshot());
                }
                var previousSession = _session;
                await StopCoreAsync();
                if (Volatile.Read(ref _stopRequested) != 0) throw new CameraRecoveryRejectedException(Snapshot());
                await StartCoreAsync(ct);
                var result = Snapshot();
                if (result.State != "Ready" || result.SessionId == previousSession) throw new CameraRecoveryFailedException(result);
            }
            finally { _gate.Release(); }
        }
        public async Task StopAsync()
        {
            await _gate.WaitAsync();
            try { await StopCoreAsync(); }
            finally { lock (_stateGate) { _state = "Stopped"; MaxBytes = 0; } _gate.Release(); }
        }
        private async Task StopCoreAsync()
        {
            if (_process is null && _pipe is null) return;
            var restored = false;
            lock (_stateGate) { _normalClosing = _process; _state = "Stopping"; MaxBytes = 0; }
            using var timeout = new CancellationTokenSource(options.ShutdownTimeoutMs);
            try
            {
                if (_pipe is { IsConnected: true } && _process is { HasExited: false })
                {
                    await ExchangeAsync(new("close", _session, Guid.NewGuid()), "closed", 0, timeout.Token);
                    restored = true;
                }
                if (_process is { HasExited: false }) await _process.WaitForExitAsync(timeout.Token);
            }
            catch (Exception e) { Log("NormalShutdownNotConfirmed", e.ToString()); }
            finally
            {
                if (_process is { HasExited: false }) { _process.Kill(entireProcessTree: true); await _process.WaitForExitAsync(); }
                _pipe?.Dispose(); _pipe = null;
                lock (_stateGate)
                {
                    _process?.Dispose(); _process = null; _normalClosing = null;
                    _cleanupConfirmed |= restored;
                    _state = "Stopped"; Log(restored ? "Stopped_ParametersRestored" : "Stopped_ParameterRestorationUnconfirmed");
                }
            }
        }
    }
}
