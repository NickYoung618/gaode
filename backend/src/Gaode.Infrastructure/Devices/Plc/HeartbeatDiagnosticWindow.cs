using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Gaode.Diagnostics;

// Linked into VirtualPlc as an internal diagnostic helper, not a device/API contract.
// No file or console I/O is performed by Record/Capture on the communication path.
internal sealed class HeartbeatDiagnosticWindow(string source, ILogger logger)
{
    private const int Capacity = 256;
    private readonly object sync = new();
    private readonly Queue<Entry> entries = new();
    private readonly List<Window> queued = new();
    private readonly Guid recorderId = Guid.NewGuid();
    private Task drain = Task.CompletedTask;
    private bool draining;
    private long sequence, lastCaptureTick, followUpTick;
    private string? followUpReason;
    private int suppressed, dropped;
    private sealed record Entry(long Sequence, DateTimeOffset Utc, long Tick, string Kind, object Facts);
    private sealed record Window(string Reason, bool Critical, object Payload);

    internal void Record(string kind, object facts)
    {
        if (!logger.IsEnabled(LogLevel.Warning)) return;
        string? followUp = null;
        lock (sync)
        {
            var tick = Stopwatch.GetTimestamp();
            entries.Enqueue(new(++sequence, DateTimeOffset.UtcNow, tick, kind, facts));
            if (entries.Count > Capacity) entries.Dequeue();
            if (followUpTick != 0 && tick >= followUpTick)
            {
                followUp = followUpReason;
                followUpTick = 0;
                followUpReason = null;
            }
        }
        if (followUp is not null) Capture(followUp + ":follow-up", followUp: true);
    }

    internal void Capture(string reason, bool critical = false, object? context = null, bool followUp = false)
    {
        if (!logger.IsEnabled(LogLevel.Warning)) return;
        lock (sync)
        {
            var tick = Stopwatch.GetTimestamp();
            if (!critical && !followUp && lastCaptureTick != 0 &&
                Stopwatch.GetElapsedTime(lastCaptureTick, tick) < TimeSpan.FromSeconds(30))
            { suppressed++; return; }
            // Preserve the first queued critical window. Never grow the logging backlog.
            if (queued.Count >= 2)
            {
                var replace = critical ? queued.FindIndex(window => !window.Critical) : -1;
                dropped++;
                if (replace < 0) return;
                queued.RemoveAt(replace);
            }
            ThreadPool.GetAvailableThreads(out var workers, out var io);
            ThreadPool.GetMinThreads(out var minWorkers, out _);
            using var process = Process.GetCurrentProcess();
            var processStartedUtc = process.StartTime.ToUniversalTime();
            var payload = new
            {
                schema = "plc-heartbeat-window/1", source, recorderId, processId = Environment.ProcessId,
                capturedUtc = DateTimeOffset.UtcNow, capturedTick = tick, tickFrequency = Stopwatch.Frequency,
                reason, critical, context, capacity = Capacity, totalRecords = sequence,
                overwrittenRecords = Math.Max(0, sequence - entries.Count),
                suppressedWindows = suppressed, droppedWindows = dropped,
                runtime = new { poolThreads = ThreadPool.ThreadCount, pendingWork = ThreadPool.PendingWorkItemCount,
                    availableWorkers = workers, availableIo = io, minWorkers, processors = Environment.ProcessorCount,
                    processStartedUtc, processCpuMs = process.TotalProcessorTime.TotalMilliseconds,
                    processUptimeMs = (DateTime.UtcNow - processStartedUtc).TotalMilliseconds,
                    processPriority = process.PriorityClass.ToString(),
                    gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2),
                    totalGcPauseMs = GC.GetTotalPauseDuration().TotalMilliseconds },
                records = entries.ToArray()
            };
            queued.Add(new(reason, critical, payload));
            suppressed = 0;
            if (!followUp && critical)
            {
                lastCaptureTick = tick;
                followUpTick = tick + 2 * Stopwatch.Frequency;
                followUpReason = reason;
            }
            else if (!followUp)
            {
                lastCaptureTick = tick;
            }
            if (!draining)
            {
                draining = true;
                drain = Task.Run(Drain);
            }
        }
    }

    private void Drain()
    {
        while (true)
        {
            Window window;
            lock (sync)
            {
                if (queued.Count == 0) { draining = false; return; }
                window = queued[0];
                queued.RemoveAt(0);
            }
            try { logger.LogWarning("PLC diagnostic window: {WindowJson}", JsonSerializer.Serialize(window.Payload)); }
            catch (Exception error)
            {
                // Diagnostic failure must never change PLC control or manufacture success.
                lock (sync) dropped++;
                System.Diagnostics.Trace.TraceError("PLC diagnostic window output failed: {0}", error);
            }
        }
    }

    internal Task FlushAsync() { PlcTimingTrace.Flush(); lock (sync) return drain; }
}

// Optional finite diagnostic adjunct to the unchanged neutral transaction counter.
// Records only actual application observation points, never kernel receipt claims.
internal static class PlcTimingTrace
{
    private static readonly string component = typeof(PlcTimingTrace).Assembly.GetName().Name!;
    private static readonly string? directory = Environment.GetEnvironmentVariable("GAODE_013_MEASUREMENT_ROOT");
    internal static bool Enabled => directory is not null;
    private const int Capacity = 32768;
    private static readonly object sync = new();
    private static readonly object output = new();
    private static readonly List<object> entries = [];
    private static long dropped, recordingTicks, maxRecordingTicks;
    internal static void Record(string kind, object facts)
    {
        if (!Enabled) return;
        var start = Stopwatch.GetTimestamp();
        lock (sync)
        {
            if (entries.Count == Capacity) dropped++;
            else entries.Add(new { kind, facts });
            var cost = Stopwatch.GetTimestamp() - start;
            recordingTicks += cost; maxRecordingTicks = Math.Max(maxRecordingTicks, cost);
        }
    }
    internal static void Flush()
    {
        if (directory is null) return;
        object payload;
        lock (sync) payload = new { schema = "013-stage-timing/1", pid = Environment.ProcessId, component,
            frequency = Stopwatch.Frequency, capacity = Capacity, dropped, recordingTicks, maxRecordingTicks,
            capturedUtc = DateTimeOffset.UtcNow, entries = entries.ToArray() };
        lock (output)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, $"stages-{Environment.ProcessId}-{component}.json"), JsonSerializer.Serialize(payload));
        }
    }
}
