using System.Diagnostics;

namespace Gaode.Diagnostics;

// Internal, low-frequency workflow evidence. No device reads, timers, or business
// decisions are added here. The Host subscribes and persists via its existing logger.
internal static class RuntimeDiagnostics
{
    private static readonly DiagnosticListener Source = new("Gaode.Runtime");

    internal static void Record(string step, string outcome, Guid? runId, object facts,
        Exception? exception = null, bool warning = false, Guid? spanId = null,
        double? elapsedMs = null)
    {
        if (!Source.IsEnabled("RuntimeFlow")) return;
        try
        {
            Source.Write("RuntimeFlow", new Dictionary<string, object?>
            {
                ["schema"] = "station01-runtime/1", ["utc"] = DateTimeOffset.UtcNow,
                ["tick"] = Stopwatch.GetTimestamp(), ["tickFrequency"] = Stopwatch.Frequency,
                ["processId"] = Environment.ProcessId, ["step"] = step,
                ["category"] = step.Contains("Save", StringComparison.Ordinal) || step.StartsWith("Database") ? "Persistence" :
                    step.Contains("Capture", StringComparison.Ordinal) || step.StartsWith("Media") ? "Acquisition" :
                    step.Contains("Algorithm", StringComparison.Ordinal) || step.StartsWith("Worker") ? "Algorithm" :
                    step.Contains("Move", StringComparison.Ordinal) || step.Contains("Clamp", StringComparison.Ordinal) ||
                    step.StartsWith("Inspection") ? "DeviceInteraction" : step.StartsWith("Configuration") ? "Configuration" : "Workflow",
                ["outcome"] = outcome, ["runId"] = runId, ["spanId"] = spanId,
                ["elapsedMs"] = elapsedMs, ["facts"] = facts,
                ["level"] = exception is not null && exception is not OperationCanceledException
                    ? "Error" : warning || exception is not null ? "Warning" : "Information",
                ["exception"] = exception
            });
        }
        catch (Exception error)
        {
            // Diagnostic output must not change the control result.
            Trace.TraceError("Runtime diagnostic output failed: {0}", error);
        }
    }

    internal static async Task<T> ObserveAsync<T>(string step, Guid? runId, object facts,
        Func<Task<T>> action, Func<T, object> resultFacts, Func<T, bool>? warning = null)
    {
        var id = Guid.NewGuid();
        var started = Stopwatch.GetTimestamp();
        Record(step, "Started", runId, facts, spanId: id);
        try
        {
            var result = await action();
            // Returned is NOT proof of device/physical completion; the result facts
            // preserve the authoritative outcome, including blocked/unknown states.
            Record(step, "Returned", runId, resultFacts(result), warning: warning?.Invoke(result) == true,
                spanId: id, elapsedMs: Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return result;
        }
        catch (Exception error)
        {
            Record(step, error is OperationCanceledException ? "Cancelled" : "Threw", runId,
                new { input = facts,
                    saveCode = (error as Gaode.Application.Station01.SaveGateException)?.Code,
                    writeId = (error as Gaode.Application.Station01.SaveGateException)?.WriteId },
                error, spanId: id, elapsedMs: Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            throw;
        }
    }

    internal static async Task ObserveAsync(string step, Guid? runId, object facts, Func<Task> action) =>
        await ObserveAsync(step, runId, facts, async () => { await action(); return true; },
            _ => new { disposition = "CallReturned_NotPhysicalCompletionProof" });
}
