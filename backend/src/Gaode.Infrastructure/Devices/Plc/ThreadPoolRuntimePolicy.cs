namespace Gaode.Diagnostics;

/// <summary>Matches .NET 10's supported Windows thread-pool configuration precedence.</summary>
public static class ThreadPoolRuntimePolicy
{
    public static bool WindowsNative { get; } = UsesWindowsNative();

    private static bool UsesWindowsNative()
    {
        if (!OperatingSystem.IsWindows()) return false;
        var value = Environment.GetEnvironmentVariable("DOTNET_ThreadPool_UseWindowsThreadPool");
        if (value == "1") return true;
        if (value == "0") return false;
        if (bool.TryParse(value, out var configured)) return configured;
        return AppContext.TryGetSwitch("System.Threading.ThreadPool.UseWindowsThreadPool", out var enabled) && enabled;
    }
}
