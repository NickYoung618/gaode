using System.IO;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Wpf;
using Gaode.Station01.Desktop;

// Owns the actual WebView2 instance; no remote debugging or product test bridge.
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 2 || args[0] != "--scenario" || !Path.IsPathFullyQualified(args[1]))
        { Console.Error.WriteLine("Use --scenario <absolute OFFLINE JSON scenario path>"); return 2; }
        using var scenario = JsonDocument.Parse(File.ReadAllText(args[1]));
        var spec = scenario.RootElement.Clone();
        if (spec.GetProperty("scope").GetString() != "OFFLINE" ||
            !Path.IsPathFullyQualified(spec.GetProperty("outputPath").GetString()!)) return 2;
        var output = spec.GetProperty("outputPath").GetString()!;
        var api = new Uri(Environment.GetEnvironmentVariable("GAODE_API_BASE_URL") ?? "https://localhost:5001");
        if (!api.IsLoopback) { Console.Error.WriteLine("Probe accepts loopback fixture only"); return 2; }
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var browser = new WebView2();
        var window = new Window { Content = browser, Width = 1280, Height = 900, ShowInTaskbar = false };
        var exit = 1;
        window.Loaded += async (_, _) =>
        {
            var observations = new List<object>();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(spec.GetProperty("timeoutSeconds").GetInt32()));
            HostRuntime? runtime = null;
            try
            {
                runtime = new HostRuntime();
                await runtime.InitializeAsync(browser, deadline.Token);
                foreach (var step in spec.GetProperty("steps").EnumerateArray())
                {
                    // Scripts are explicitly supplied offline DOM actions/assertions, never credentials.
                    var expression = step.GetProperty("script").GetString()!;
                    string result;
                    if (step.TryGetProperty("waitUntil", out var wait) && wait.GetBoolean())
                    {
                        do
                        {
                            deadline.Token.ThrowIfCancellationRequested();
                            result = await browser.ExecuteScriptAsync(expression);
                            if (result == "true") break;
                            await Task.Delay(100, deadline.Token);
                        } while (true);
                    }
                    else result = await browser.ExecuteScriptAsync(expression);
                    observations.Add(new { id = step.GetProperty("id").GetString(), result });
                    if (step.TryGetProperty("expectedJson", out var expected) && result != expected.GetString())
                        throw new InvalidOperationException("DOM assertion failed: " + step.GetProperty("id").GetString());
                }
                Write(output, new { scope = "OFFLINE:actual-HostRuntime-WebView2-DOM", state = "Passed", mode = runtime.Configuration.Mode, devToolsEnabled = browser.CoreWebView2.Settings.AreDevToolsEnabled, observations });
                exit = 0;
            }
            catch (Exception error)
            {
                // No raw exception/message or environment dump can reveal the selected credential.
                Write(output, new { scope = "OFFLINE:actual-HostRuntime-WebView2-DOM", state = spec.TryGetProperty("expectedInitializationError", out var expectedError) && expectedError.GetString() == error.GetType().Name ? "PassedExpectedRejection" : "FailedOrBlocked", errorType = error.GetType().Name, observations });
            }
            finally { runtime?.Dispose(); window.Close(); app.Shutdown(); }
        };
        window.Show(); app.Run(); return exit;
    }
    private static void Write(string path, object value)
    { Directory.CreateDirectory(Path.GetDirectoryName(path)!); var json = JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true });
      foreach (var key in new[] { "GAODE_OFFLINE_OPERATOR_CREDENTIAL", "GAODE_OFFLINE_ENGINEER_CREDENTIAL", "GAODE_TEST_OPERATOR_TOKEN" }) {
        var secret = Environment.GetEnvironmentVariable(key);
        if (!string.IsNullOrEmpty(secret)) json = json.Replace(secret, "[REDACTED]", StringComparison.Ordinal).Replace(JsonSerializer.Serialize(secret)[1..^1], "[REDACTED]", StringComparison.Ordinal);
      }
      File.WriteAllText(path, json); }
}
