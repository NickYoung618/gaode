using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Gaode.Station01.Desktop;

// Desktop diagnostic files only; never device control or business database access.
internal static class DesktopRuntimeLog
{
    private static readonly object Gate = new();
    private static string? commissioningLogRoot, selectedCredential;
    internal static void ConfigureCommissioning(string logRoot, string credential)
    { commissioningLogRoot = logRoot; selectedCredential = credential; }
    private static string Redact(string text)
    {
        foreach (var secret in new[] { selectedCredential, Environment.GetEnvironmentVariable("GAODE_TEST_OPERATOR_TOKEN") })
        {
            if (string.IsNullOrEmpty(secret)) continue;
            text = text.Replace(secret, "[REDACTED]", StringComparison.Ordinal)
                .Replace(JsonSerializer.Serialize(secret)[1..^1], "[REDACTED]", StringComparison.Ordinal);
        }
        return text;
    }
    internal static void Write(string kind, object facts)
    {
        try
        {
            var prepared = Environment.GetEnvironmentVariable("GAODE_TEST_PREPARED_LOAD_PATH");
            var folder = commissioningLogRoot ?? (Environment.GetEnvironmentVariable("GAODE_MODE") == "Test" &&
                !string.IsNullOrWhiteSpace(prepared) && Path.IsPathFullyQualified(prepared)
                ? Path.Combine(Path.GetDirectoryName(prepared)!, "logs")
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Gaode", "Logs"));
            var path = Path.Combine(folder, "desktop-runtime.jsonl");
            var line = JsonSerializer.Serialize(new { schema = "station01-desktop/1",
                utc = DateTimeOffset.UtcNow, processId = Environment.ProcessId, kind, facts });
            line = Redact(line);
            lock (Gate)
            {
                Directory.CreateDirectory(folder);
                if (File.Exists(path) && new FileInfo(path).Length >= 2 * 1024 * 1024)
                    File.Move(path, path + ".previous", overwrite: true);
                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch (Exception error) { System.Diagnostics.Trace.TraceError("Desktop diagnostic write failed: {0}", error); }
    }

    internal static string SafeError(string text)
    {
        var safe = Regex.Replace(Redact(text), @"(?i)(bearer\s+|(?:token|password|secret)\s*[:=]\s*)[^\s,;]+", "$1[REDACTED]");
        return safe.Length > 4096 ? safe[..4096] + "[truncated]" : safe;
    }
}
