using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.IO;
using System.Text.Json;

namespace Gaode.Station01.Desktop;

public sealed class HostRuntime : IDisposable
{
    public const string VirtualHost = "appassets.local";
    private CoreWebView2? _core;
    private WebView2? _browser;
    public HostConfiguration Configuration { get; private set; } = HostConfiguration.FromEnvironment();
    public bool IsInitialized => _core is not null;

    public async Task InitializeAsync(WebView2 browser, CancellationToken cancellationToken = default)
    {
        DesktopRuntimeLog.Write("Initializing", new { Configuration.Mode, Configuration.ResourceVersion });
        try { await InitializeCoreAsync(browser, cancellationToken); }
        catch (Exception error)
        {
            DesktopRuntimeLog.Write("InitializationFailed", new { error = DesktopRuntimeLog.SafeError(error.ToString()) });
            throw;
        }
    }

    private async Task InitializeCoreAsync(WebView2 browser, CancellationToken cancellationToken)
    {
        Configuration.Validate();
        var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
        if (string.IsNullOrWhiteSpace(version)) throw new InvalidOperationException("未检测到 WebView2 Runtime，请先安装受支持的 Microsoft Edge WebView2 Runtime。");
        _browser = browser;
        var options = new CoreWebView2EnvironmentOptions();
        if (Configuration.Mode == "Test" && int.TryParse(
                Environment.GetEnvironmentVariable("GAODE_TEST_WEBVIEW2_DEBUG_PORT"), out var debugPort) &&
            debugPort is > 0 and <= 65535)
            options.AdditionalBrowserArguments =
                $"--remote-debugging-address=127.0.0.1 --remote-debugging-port={debugPort}";
        var environment = await CoreWebView2Environment.CreateAsync(null, null, options);
        cancellationToken.ThrowIfCancellationRequested();
        await browser.EnsureCoreWebView2Async(environment);
        _core = browser.CoreWebView2;
        _core.Settings.AreDevToolsEnabled = Configuration.Mode is "Test" or "Simulation";
        _core.Settings.AreDefaultContextMenusEnabled = Configuration.Mode is "Test" or "Simulation";
        _core.NavigationStarting += OnNavigationStarting;
        _core.ProcessFailed += (_, e) =>
        {
            DesktopRuntimeLog.Write("WebViewProcessFailed", new { kind = e.ProcessFailedKind.ToString() });
            browser.Dispatcher.Invoke(() => browser.Visibility = System.Windows.Visibility.Collapsed);
        };
        _core.NavigationCompleted += (_, e) => DesktopRuntimeLog.Write("NavigationCompleted",
            new { e.IsSuccess, error = e.WebErrorStatus.ToString(), e.NavigationId });
        // Runtime events are observed locally; no debug port, JS bridge or extra API.
        try
        {
            _core.GetDevToolsProtocolEventReceiver("Runtime.consoleAPICalled").DevToolsProtocolEventReceived += (_, e) =>
            {
                try
                {
                    using var message = JsonDocument.Parse(e.ParameterObjectAsJson);
                    var values = message.RootElement.GetProperty("args");
                    if (values.GetArrayLength() < 2 || !values[0].TryGetProperty("value", out var marker) ||
                        marker.GetString() != "GaodePageDiagnostic") return;
                    var data = values[1].GetProperty("value").GetString();
                    if (data is null || data.Length > 8192) return;
                    using var details = JsonDocument.Parse(data);
                    DesktopRuntimeLog.Write("PageDiagnostic", details.RootElement.Clone());
                }
                catch (Exception error) { DesktopRuntimeLog.Write("PageDiagnosticDecodeFailed", new { errorType = error.GetType().Name }); }
            };
            _core.GetDevToolsProtocolEventReceiver("Runtime.exceptionThrown").DevToolsProtocolEventReceived += (_, e) =>
            {
                try
                {
                using var message = JsonDocument.Parse(e.ParameterObjectAsJson);
                var details = message.RootElement.GetProperty("exceptionDetails");
                var description = details.TryGetProperty("exception", out var exception) &&
                    exception.TryGetProperty("description", out var text) ? text.GetString() : details.GetProperty("text").GetString();
                DesktopRuntimeLog.Write("PageException", new { error = DesktopRuntimeLog.SafeError(description ?? "Unknown") });
                }
                catch (Exception error) { DesktopRuntimeLog.Write("PageExceptionDecodeFailed", new { errorType = error.GetType().Name }); }
            };
            await _core.CallDevToolsProtocolMethodAsync("Runtime.enable", "{}");
        }
        catch (Exception error)
        {
            DesktopRuntimeLog.Write("PageDiagnosticsUnavailable", new { error = DesktopRuntimeLog.SafeError(error.ToString()) });
        }
        var resourceRoot = ResolveResourceRoot();
        if (!Directory.Exists(resourceRoot)) throw new DirectoryNotFoundException($"前端静态资源目录不存在：{resourceRoot}");
        var runtimeFile = Path.Combine(resourceRoot, "runtime.js");
        DesktopRuntimeLog.Write("ResourcesResolved", new { webViewVersion = version, resourceRoot,
            runtimeSha256 = File.Exists(runtimeFile) ? Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(runtimeFile))) : null });
        _core.SetVirtualHostNameToFolderMapping(VirtualHost, resourceRoot, CoreWebView2HostResourceAccessKind.DenyCors);
        object? preparedStartRequest = null;
        var preparedPath = Environment.GetEnvironmentVariable("GAODE_TEST_PREPARED_LOAD_PATH");
        if (Configuration.Mode == "Test" && !string.IsNullOrWhiteSpace(preparedPath))
        {
            using var prepared = JsonDocument.Parse(File.ReadAllText(preparedPath));
            preparedStartRequest = prepared.RootElement.GetProperty("request").Clone();
        }
        // This script is injected into the controlled WebView2 process; the token is never written to a page file.
        await _core.AddScriptToExecuteOnDocumentCreatedAsync($"window.__GAODE_HOST_CONFIG__ = Object.freeze({JsonSerializer.Serialize(new { apiBaseUrl = Configuration.ApiBaseUrl, signalrUrl = Configuration.SignalRUrl, mode = Configuration.Mode, resourceVersion = Configuration.ResourceVersion, prototypeSha256 = Configuration.PrototypeSha256, testToken = Configuration.Mode == "Test" ? Environment.GetEnvironmentVariable("GAODE_TEST_OPERATOR_TOKEN") : null, preparedStartRequest })});");
        _core.Navigate($"https://{VirtualHost}/login.html");
    }

    private static string ResolveResourceRoot()
    {
        var configured = Environment.GetEnvironmentVariable("GAODE_FRONTEND_DIST");
        if (!string.IsNullOrWhiteSpace(configured)) return Path.GetFullPath(configured);
        var sibling = Path.Combine(AppContext.BaseDirectory, "frontend", "dist");
        if (Directory.Exists(sibling)) return sibling;
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "frontend", "dist"));
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!e.Uri.StartsWith($"https://{VirtualHost}/", StringComparison.OrdinalIgnoreCase)) e.Cancel = true;
    }

    public void Dispose()
    {
        DesktopRuntimeLog.Write("Disposing", new { initialized = IsInitialized });
        if (_core is not null) _core.NavigationStarting -= OnNavigationStarting;
        _browser?.Dispose(); _core = null; _browser = null;
    }
}
