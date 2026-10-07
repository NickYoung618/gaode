using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using Gaode.Diagnostics;

namespace Gaode.Infrastructure.Algorithms;

/// <summary>
/// Owns one long-lived worker process. A worker fault is latched; callers must create a
/// new session after reconciliation and no automatic replay is attempted.
/// </summary>
public sealed class WorkerProcessSupervisor : IWorkerProcess
{
    private readonly string executable;
    private readonly string arguments;
    private readonly string workingDirectory;
    private readonly Process process;
    private readonly SemaphoreSlim writeGate = new(1, 1);
    private readonly SemaphoreSlim stopGate = new(1, 1);
    private readonly TaskCompletionSource exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private StreamReader? stdout;
    private StreamWriter? stdin;
    private int started, disposed, processDisposed;
    private readonly Queue<string> recentStderr = new();
    private long stderrLines;
    private Task stderrDrain = Task.CompletedTask;
    public WorkerImplementation? Implementation { get; }
    public Guid SessionId { get; } = Guid.NewGuid();
    public bool HasExited => Volatile.Read(ref processDisposed) != 0 || process.HasExited;
    public Task ExitTask => exited.Task;
    public string DigestInput(string relativeKey)
    {
        var path = Path.GetFullPath(Path.Combine(workingDirectory, relativeKey));
        if (!path.StartsWith(workingDirectory + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Worker input outside controlled media root");
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    public WorkerProcessSupervisor(string executable, string workingDirectory, string arguments = "", WorkerImplementation? implementation = null)
    {
        if (!Path.IsPathFullyQualified(executable) || !File.Exists(executable))
            throw new ArgumentException("Worker可执行文件必须是已存在的绝对路径", nameof(executable));
        if (!Path.IsPathFullyQualified(workingDirectory) || !Directory.Exists(workingDirectory))
            throw new ArgumentException("Worker工作目录必须是已存在的绝对路径", nameof(workingDirectory));
        Implementation = implementation;
        this.executable = executable;
        this.arguments = arguments;
        this.workingDirectory = workingDirectory;
        process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            },
            EnableRaisingEvents = true
        };
        process.Exited += (_, _) =>
        {
            exited.TrySetResult();
            RuntimeDiagnostics.Record("WorkerProcess", "Exited", null,
                new { workerSessionId = SessionId, processId = process.Id, process.ExitCode,
                    stopRequested = Volatile.Read(ref disposed) != 0 });
        };
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.Exchange(ref started, 1) != 0) throw new InvalidOperationException("Worker已启动");
        Implementation?.VerifyFiles();
        if (!process.Start()) throw new InvalidOperationException("Worker启动失败");
        stdin = process.StandardInput;
        stdout = process.StandardOutput;
        stderrDrain = DrainStderrAsync(process.StandardError);
        RuntimeDiagnostics.Record("WorkerProcess", "Started", null,
            new { workerSessionId = SessionId, processId = process.Id,
                executableName = Path.GetFileName(executable), readyTimeoutMs = 5000 });
        try
        {
            await SendAsync(new WorkerMessage("Hello", WorkerProtocolCodec.ContractVersion,
                SessionId, Guid.NewGuid(), 1), cancellationToken);
            using var readyDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            readyDeadline.CancelAfter(TimeSpan.FromSeconds(5));
            var line = await ReadLineAsync(readyDeadline.Token)
                ?? throw new IOException("WorkerExitedBeforeReady");
            var ready = WorkerProtocolCodec.Decode(Encoding.UTF8.GetBytes(line));
            if (ready.Type != "Ready" || ready.WorkerSessionId != SessionId)
                throw new InvalidDataException("WorkerReadyHandshakeMismatch");
            Implementation?.VerifyReady(ready.Reason, process.Id);
            RuntimeDiagnostics.Record("WorkerProcess", "Ready", null,
                new { workerSessionId = SessionId, processId = process.Id, implementation = Implementation });
        }
        catch (Exception error)
        {
            RecordFailure(null, null, "ReadyHandshakeFailed", error);
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }
    }

    public async Task SendAsync(WorkerMessage message, CancellationToken cancellationToken = default)
    {
        var writer = stdin ?? throw new InvalidOperationException("Worker尚未启动");
        var line = WorkerProtocolCodec.Encode(message);
        await writeGate.WaitAsync(cancellationToken);
        try {
            if (Volatile.Read(ref processDisposed) != 0) throw new ObjectDisposedException(nameof(WorkerProcessSupervisor));
            await writer.WriteAsync(line.AsMemory(), cancellationToken); await writer.FlushAsync(cancellationToken);
        }
        finally { writeGate.Release(); }
    }

    public async Task<string?> ReadLineAsync(CancellationToken cancellationToken = default)
    {
        var reader = stdout ?? throw new InvalidOperationException("Worker尚未启动");
        var line = await reader.ReadLineAsync(cancellationToken);
        if (line is null) { exited.TrySetResult(); return null; }
        if (Encoding.UTF8.GetByteCount(line) > WorkerProtocolCodec.MaxLineBytes)
            throw new InvalidDataException("Worker输出超过64KiB限制");
        return line;
    }

    public async Task RequestStopAsync(CancellationToken cancellationToken = default)
    {
        await stopGate.WaitAsync(cancellationToken);
        try { await StopCoreAsync(cancellationToken); }
        finally { stopGate.Release(); }
    }

    private async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref processDisposed) != 0) return;
        if (!HasExited && stdin is not null)
        {
            try
            {
                await SendAsync(new WorkerMessage("Shutdown", WorkerProtocolCodec.ContractVersion,
                    SessionId, Guid.NewGuid(), 1), cancellationToken);
                await ExitTask.WaitAsync(TimeSpan.FromSeconds(1), cancellationToken);
            }
            catch (TimeoutException) { /* bounded termination below */ }
            catch (IOException) { /* already closed; bounded termination below */ }
        }
        if (!HasExited) process.Kill(entireProcessTree: true);
    }

    internal void RecordFailure(Guid? runId, Guid? callId, string phase, Exception error)
    {
        string[] tail;
        long count;
        lock (recentStderr) { tail = recentStderr.ToArray(); count = stderrLines; }
        RuntimeDiagnostics.Record("WorkerFailure", phase, runId,
            new { workerSessionId = SessionId, callId, stderrLines = count, stderrTail = tail,
                omittedLines = Math.Max(0, count - tail.Length),
                disposition = "NoAutomaticReplay_ResourceReleaseNeedsEvidence" }, error);
    }

    private async Task DrainStderrAsync(StreamReader reader)
    {
        try
        {
            while (await reader.ReadLineAsync() is { } line)
            {
                // Never emit unbounded output or credentials echoed by a worker.
                var safe = Regex.Replace(line, @"(?i)(bearer\s+|(?:token|password|secret)\s*[:=]\s*)[^\s,;]+", "$1[REDACTED]");
                safe = safe.Length > 2048 ? safe[..2048] + "[truncated]" : safe;
                lock (recentStderr)
                {
                    stderrLines++;
                    if (recentStderr.Count == 32) recentStderr.Dequeue();
                    recentStderr.Enqueue(safe);
                    if (stderrLines <= 8)
                        RuntimeDiagnostics.Record("WorkerStderr", "Observed", null,
                            new { workerSessionId = SessionId, lineNumber = stderrLines, line = safe,
                                disposition = "DiagnosticOnly_NotBusinessFailure", immediateLineLimit = 8 }, warning: true);
                }
            }
            lock (recentStderr)
            {
                if (stderrLines > 0)
                    RuntimeDiagnostics.Record("WorkerStderr", "StreamEnded", null,
                        new { workerSessionId = SessionId, stderrLines, stderrTail = recentStderr.ToArray(),
                            omittedLines = Math.Max(0, stderrLines - recentStderr.Count) }, warning: true);
            }
        }
        catch (Exception error)
        {
            RuntimeDiagnostics.Record("WorkerStderr", "ReadFailed", null,
                new { workerSessionId = SessionId }, error);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        try { await RequestStopAsync(); } catch { }
        // Bounded drain only on orderly disposal; not in the control/heartbeat path.
        try { await stderrDrain.WaitAsync(TimeSpan.FromSeconds(1)); }
        catch (TimeoutException) { }
        await stopGate.WaitAsync();
        try
        {
            await writeGate.WaitAsync();
            try { Volatile.Write(ref processDisposed, 1); process.Dispose(); }
            finally { writeGate.Release(); }
        }
        finally { stopGate.Release(); }
        // These gates have no allocated WaitHandle. Let GC reclaim them after pending
        // callers leave; disposing a gate before their finally/Release caused the observed failure.
    }
}
