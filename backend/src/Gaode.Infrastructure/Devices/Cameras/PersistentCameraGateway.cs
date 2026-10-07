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
    public long GetMaxCaptureBytes(string binding, long fallback) => Find(binding).MaxBytes is > 0 and <= CameraWorkerProtocol.MaxPayloadBytes
        ? Find(binding).MaxBytes : throw new InvalidOperationException("CameraNotReady:" + binding);
    public IReadOnlyList<CameraWorkerStatus> Status => _workers.Values.Select(x => x.Snapshot()).ToArray();
    public async Task StartAsync(CancellationToken ct)
    {
        // Vendor discovery scans all interfaces. Prepare once in order to avoid overlapping
        // discovery broadcasts; capture and explicit recovery remain independent per camera.
        foreach (var worker in _workers.Values) await worker.StartAsync(ct);
    }
    public Task RecoverAsync(string binding, CancellationToken ct) => Find(binding).RecoverAsync(ct);
    public Task OpenAsync(string cameraBindingId, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("CameraLifecycleOwnedByHost");
    public Task ConfigureAsync(string cameraBindingId, int exposureUs, double gain, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("RealCameraPreservesImagingParameters");
    public Task<CameraFrame> TriggerAsync(string binding, string pointVersion, CancellationToken ct = default) => Find(binding).CaptureAsync(ct);
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        await Task.WhenAll(_workers.Values.Select(x => x.StopAsync()));
    }

    private sealed class Worker(CameraBinding binding, RealCameraOptions options)
    {
        public CameraBinding Binding => binding;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private Process? _process;
        private NamedPipeServerStream? _pipe;
        private string _state = "Stopped";
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
        public CameraWorkerStatus Snapshot() => new(binding.Role, binding.Serial, binding.ExpectedNicMac,
            _state, _process is { HasExited: false } ? _process.Id : null, _session, Epoch, MaxBytes, _openCount, _error, _metadata);
        public async Task StartAsync(CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try { await StartCoreAsync(ct); }
            finally { _gate.Release(); }
        }
        private async Task StartCoreAsync(CancellationToken ct)
        {
            if (_state == "Ready") return;
            _state = "Starting"; _error = null; MaxBytes = 0; _lastFrame = null; _lastTrigger = 0;
            _session = Guid.NewGuid(); Epoch = DateTime.UtcNow.Ticks;
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
                _process = new Process { StartInfo = start, EnableRaisingEvents = true };
                _process.OutputDataReceived += (_, e) => { if (e.Data is not null) Log("WorkerOutput", e.Data); };
                _process.ErrorDataReceived += (_, e) => { if (e.Data is not null) Log("WorkerError", e.Data); };
                if (!_process.Start()) throw new IOException("CameraWorkerStartFailed");
                _process.BeginOutputReadLine(); _process.BeginErrorReadLine();
                Log("ProcessStarted", new { pid = _process.Id });
                await _pipe.WaitForConnectionAsync(budget.Token);
                _state = "Opening";
                var ready = await ExchangeAsync(new("init", _session, Guid.NewGuid()) { Binding = binding }, "ready", 0, budget.Token);
                if (ready.Header.Metadata is not { } m || m.Serial != binding.Serial || m.Role != binding.Role ||
                    m.WorkerSessionId != _session || NormalizeMac(m.NicMac) != NormalizeMac(binding.ExpectedNicMac) ||
                    ready.Header.MaxBytes is <= 0 or > CameraWorkerProtocol.MaxPayloadBytes)
                    throw new InvalidDataException("CameraReadyIdentityOrCapacityMismatch");
                _metadata = m; MaxBytes = ready.Header.MaxBytes; _openCount++; _state = "Ready";
                Log("Ready", new { MaxBytes, _openCount, metadata = m });
            }
            catch (Exception e)
            {
                Fault(e); await StopCoreAsync(); _state = "Faulted";
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
        public async Task<CameraFrame> CaptureAsync(CancellationToken ct)
        {
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
            budget.CancelAfter(options.CaptureTimeoutMs);
            await _gate.WaitAsync(budget.Token);
            try
            {
                if (_state != "Ready") throw new InvalidOperationException("CameraNotReady:" + binding.Role);
                _state = "Capturing";
                var request = new CameraWireMessage("capture", _session, Guid.NewGuid()) { MaxBytes = MaxBytes };
                Log("CaptureRequested", request.RequestId);
                var frame = await ExchangeAsync(request, "frame", MaxBytes, budget.Token);
                var m = frame.Header.Metadata ?? throw new InvalidDataException("CameraFrameMetadataMissing");
                if (m.WorkerSessionId != _session || m.Serial != binding.Serial || m.Role != binding.Role ||
                    NormalizeMac(m.NicMac) != NormalizeMac(binding.ExpectedNicMac) ||
                    m.TriggerSequence <= _lastTrigger || (_lastFrame is { } last && m.FrameId <= last) ||
                    m.PayloadBytes != frame.Data.LongLength || frame.Data.Length == 0 || m.ReceivedUtc < m.TriggeredUtc ||
                    frame.Header.Format is not ("GalaxyRaw" or "CameraProFrameZipV1"))
                    throw new InvalidDataException("CameraFreshFrameEvidenceInvalid");
                _metadata = m; _lastFrame = m.FrameId; _lastTrigger = m.TriggerSequence;
                _state = "Ready"; Log("FrameTaken", new { request.RequestId, m.FrameId, m.TriggerSequence, m.PayloadBytes });
                return new(frame.Data, frame.Header.Format!, frame.Header.ContentType ?? "application/octet-stream", Epoch) { Metadata = m };
            }
            catch (Exception e) { Fault(e); _pipe?.Dispose(); _pipe = null; throw; }
            finally { _gate.Release(); }
        }
        private void Fault(Exception e) { _error = e.ToString(); _state = "Faulted"; Log("Faulted_NoAutomaticReplay", _error); }
        public async Task RecoverAsync(CancellationToken ct)
        {
            await _gate.WaitAsync(ct);
            try { await StopCoreAsync(); await StartCoreAsync(ct); }
            finally { _gate.Release(); }
        }
        public async Task StopAsync()
        {
            await _gate.WaitAsync();
            try { await StopCoreAsync(); }
            finally { _gate.Release(); }
        }
        private async Task StopCoreAsync()
        {
            if (_process is null && _pipe is null) return;
            var restored = false;
            _state = "Stopping";
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
                _pipe?.Dispose(); _pipe = null; _process?.Dispose(); _process = null;
                _state = "Stopped"; Log(restored ? "Stopped_ParametersRestored" : "Stopped_ParameterRestorationUnconfirmed");
            }
        }
    }
}

