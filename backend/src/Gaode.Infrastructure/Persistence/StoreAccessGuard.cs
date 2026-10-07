using System.Collections.Concurrent;
using System.Text;

namespace Gaode.Infrastructure.Persistence;

public sealed class StoreAccessGuard : IDisposable
{
    private readonly FileStream _handle;
    private readonly bool _maintenance;
    private int _disposed;
    private static readonly ConcurrentDictionary<string, StoreAccessGuard> Owners = new(StringComparer.OrdinalIgnoreCase);
    public string Root { get; }

    private StoreAccessGuard(string root, FileStream handle, bool maintenance)
    { Root = root; _handle = handle; _maintenance = maintenance; }

    public static StoreAccessGuard Acquire(string root, string approvedTestRoot)
        => AcquireCore(root, approvedTestRoot, false);
    internal static StoreAccessGuard AcquireMaintenance(string root, string approvedTestRoot)
        => AcquireCore(root, approvedTestRoot, true);

    private static StoreAccessGuard AcquireCore(string root, string approvedTestRoot, bool maintenance)
    {
        if (!Path.IsPathFullyQualified(root) || !Path.IsPathFullyQualified(approvedTestRoot))
            throw new InvalidOperationException("测试库路径必须为绝对路径");
        var full = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        var allowed = Path.GetFullPath(approvedTestRoot).TrimEnd(Path.DirectorySeparatorChar);
        if (!full.StartsWith(allowed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("测试库不在项目隔离Test根内");
        if (Directory.Exists(full) && (File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("测试库根不能是链接");
        for (var parent = new DirectoryInfo(full); parent is not null && parent.FullName.Length >= allowed.Length; parent = parent.Parent)
            if (parent.Exists && (parent.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("测试库祖先不能是链接");
        Directory.CreateDirectory(full);
        var lockPath = Path.Combine(full, ".station01.store.lock");
        try
        {
            var handle = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            if (!maintenance && handle.Length != 0)
            { handle.Dispose(); throw new InvalidOperationException("MaintenanceReconciliationRequired"); }
            var guard = new StoreAccessGuard(full, handle, maintenance);
            if (!Owners.TryAdd(full, guard)) { handle.Dispose(); throw new InvalidOperationException("StoreAlreadyOwned"); }
            return guard;
        }
        catch (IOException ex) { throw new InvalidOperationException("测试库正由Host或维护夹具占用", ex); }
    }

    // The existing lock also retains an unfinished maintenance intent after process death.
    // This is a crash latch, not another store manifest: identity/schema remain in SQLite Manifests.
    internal string ReadMaintenanceIntent()
    {
        if (!_maintenance) throw new InvalidOperationException("MaintenanceLockRequired");
        _handle.Position = 0;
        using var reader = new StreamReader(_handle, Encoding.UTF8, false, 1024, leaveOpen: true);
        return reader.ReadToEnd();
    }
    internal void WriteMaintenanceIntent(string intent)
    {
        if (!_maintenance || string.IsNullOrWhiteSpace(intent)) throw new InvalidOperationException("MaintenanceIntentRequired");
        _handle.Position = 0; _handle.SetLength(0);
        _handle.Write(Encoding.UTF8.GetBytes(intent)); _handle.Flush(flushToDisk: true);
    }
    internal void CompleteVerifiedMaintenance()
    {
        if (!_maintenance) throw new InvalidOperationException("MaintenanceLockRequired");
        _handle.SetLength(0); _handle.Flush(flushToDisk: true);
    }
    internal static string? AdmissionBlock(string root)
    {
        var full = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
        if (Owners.TryGetValue(full, out var owner)) return owner._maintenance ? "MaintenanceInProgress" : null;
        var lockPath = Path.Combine(full, ".station01.store.lock");
        if (!File.Exists(lockPath)) return null;
        try
        {
            using var handle = new FileStream(lockPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return handle.Length == 0 ? null : "MaintenanceReconciliationRequired";
        }
        catch (IOException) { return "StoreInUse"; }
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        Owners.TryRemove(Root, out _);
        _handle.Dispose();
    }
}
