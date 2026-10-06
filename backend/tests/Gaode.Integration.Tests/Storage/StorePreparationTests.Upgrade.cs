using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Gaode.Integration.Tests.Storage;

public sealed partial class StorePreparationTests
{
    [Theory]
    [InlineData("SU01-before-commit/after-ddl", "after-ddl")]
    [InlineData("SU01-before-commit/after-history", "after-history")]
    [InlineData("SU01-before-commit/after-manifest", "after-manifest")]
    [InlineData("SU02-after-commit/before-receipt", "after-commit-before-receipt")]
    [InlineData("SU03-unresolved/hold", "after-commit-before-receipt")]
    [InlineData("SU04-inconsistent/reject-restore", "after-manifest")]
    public async Task RequiredUpgradeCase(string caseId, string interruption)
    {
        var workspace = Workspace();
        var allowed = Gaode.Testing.ApprovedTestRoot.Resolve(workspace);
        var root = Path.Combine(allowed, "009-upgrade-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var source = await CreateSourceAsync(root);
        var sourceCompatibility = StoreCompatibilityProbe.Inspect(root);
        Assert.False(sourceCompatibility.Compatible);
        Assert.Equal("SourceStoreRequiresControlledUpgrade", sourceCompatibility.Code);
        var tool = ToolPath(workspace);
        var actualProcess = await InterruptToolAsync(tool, ["--upgrade-test", allowed, root], interruption);
        Assert.False(StoreCompatibilityProbe.Inspect(root).Compatible);
        Assert.Throws<InvalidOperationException>(() => StoreAccessGuard.Acquire(root, allowed));
        var ddlBefore = Directory.GetFiles(root, "ddl-*.json", SearchOption.AllDirectories).Length;
        Assert.True(ddlBefore > 0);
        StoreMaintenanceResult result;
        object? heldObservation = null;
        if (caseId == "SU03-unresolved/hold")
        {
            using var holding = StartTool(tool, ["--reconcile-test", allowed, root, "--pause-at", "before-reconciliation"]);
            await AwaitCheckpointAsync(holding, "before-reconciliation");
            try
            {
                Assert.False(StoreCompatibilityProbe.Inspect(root).Compatible);
                Assert.Throws<InvalidOperationException>(() => StoreAccessGuard.Acquire(root, allowed));
                var reentry = await RunToolAsync(tool, ["--upgrade-test", allowed, root]);
                Assert.NotEqual(0, reentry.ExitCode);
                Assert.Equal(ddlBefore, Directory.GetFiles(root, "ddl-*.json", SearchOption.AllDirectories).Length);
                heldObservation = new { state = "U1", holdingPid = holding.Id, reentry, ddlBefore, ddlAfter = ddlBefore };
            }
            finally { holding.Kill(entireProcessTree: true); await holding.WaitForExitAsync(); }
            result = await RunMaintenanceAsync(tool, ["--reconcile-test", allowed, root]);
            Assert.Equal("U2", result.State); Assert.Equal(0, result.DdlExecuted);
        }
        else if (caseId == "SU04-inconsistent/reject-restore")
        {
            using (var connection = OpenStore(root))
            {
                using var mutate = connection.CreateCommand(); mutate.CommandText = "DROP INDEX IX_Writes_RunRevision";
                mutate.ExecuteNonQuery(); // One controlled structural inconsistency, never a forged commit result.
            }
            var rejected = await RunToolAsync(tool, ["--upgrade-test", allowed, root]);
            Assert.NotEqual(0, rejected.ExitCode);
            Assert.False(StoreCompatibilityProbe.Inspect(root).Compatible);
            Assert.Equal(ddlBefore, Directory.GetFiles(root, "ddl-*.json", SearchOption.AllDirectories).Length);
            var restored = await RunMaintenanceAsync(tool, ["--restore-source-test", allowed, root]);
            Assert.Equal("U0", restored.State);
            Assert.False(StoreCompatibilityProbe.Inspect(root).Compatible);
            Assert.True(Directory.EnumerateDirectories(Path.Combine(root, ".009-maintenance"), "preserved-failed-*", SearchOption.AllDirectories).Any());
            result = await RunMaintenanceAsync(tool, ["--upgrade-test", allowed, root]);
            Assert.Equal("U2", result.State); Assert.True(result.DdlExecuted > 0);
            heldObservation = new { rejected, restored };
        }
        else
        {
            result = await RunMaintenanceAsync(tool, ["--reconcile-test", allowed, root]);
            if (caseId.StartsWith("SU01", StringComparison.Ordinal))
            {
                Assert.Equal("U0", result.State); Assert.Equal(0, result.DdlExecuted);
                Assert.False(StoreCompatibilityProbe.Inspect(root).Compatible);
                Assert.Equal(ddlBefore, Directory.GetFiles(root, "ddl-*.json", SearchOption.AllDirectories).Length);
                result = await RunMaintenanceAsync(tool, ["--upgrade-test", allowed, root]);
                Assert.Equal("U2", result.State); Assert.True(result.DdlExecuted > 0);
            }
            else { Assert.Equal("U2", result.State); Assert.Equal(0, result.DdlExecuted); }
        }
        var ddlAfterClassification = Directory.GetFiles(root, "ddl-*.json", SearchOption.AllDirectories).Length;
        var repeated = await RunMaintenanceAsync(tool, ["--upgrade-test", allowed, root]);
        Assert.Equal("U2", repeated.State); Assert.Equal(0, repeated.DdlExecuted);
        Assert.Equal(ddlAfterClassification, Directory.GetFiles(root, "ddl-*.json", SearchOption.AllDirectories).Length);
        using var hostGuard = StoreAccessGuard.Acquire(root, allowed);
        var admission = StoreCompatibilityProbe.Inspect(root);
        Assert.True(admission.Compatible, admission.Code);
        Assert.Equal(source.StoreId, admission.StoreId);
        using var actualConnection = OpenStore(root);
        var actual = StoreSchemaInspection.Inspect(actualConnection, root, hashes: true);
        Assert.Equal("U2", actual.State);
        Assert.Equal(source.BusinessDigest, actual.BusinessDigest);
        Assert.Equal(source.MediaDigest, actual.MediaDigest);
        var evidenceRoot = Environment.GetEnvironmentVariable("GAODE_009_EVIDENCE_ROOT") ??
            Path.Combine(workspace, "artifacts", "recipe-execution-008", "009-isolation", "upgrade-component-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(evidenceRoot);
        var evidencePath = Path.Combine(evidenceRoot, caseId.Replace('/', '-') + "-" + Guid.NewGuid().ToString("N") + ".json");
        await File.WriteAllTextAsync(evidencePath, JsonSerializer.Serialize(new { caseId, root, actualProcess,
            tool, toolSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(tool))),
            sourceLinkedComponentTool = Environment.GetEnvironmentVariable("GAODE_009_STOREPREP_COMPONENT") is not null,
            source, actual, heldObservation, result, repeated, admission, maintenanceEvidence = result.EvidenceDirectory,
            scope = "Controlled SQLite maintenance only; not whole009/HostWorker acceptance" }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task<StoreInspection> CreateSourceAsync(string root)
    {
        var options = new DbContextOptionsBuilder<Station01DbContext>().UseSqlite(Connection(root)).Options;
        await using var db = new Station01DbContext(options);
        const string sourceMigration = "202609230001_Station01MainFlow";
        Assert.Contains(sourceMigration, db.GetService<IMigrationsAssembly>().Migrations.Keys);
        // Existing IDs have a 12-digit prefix. Resolve with EF's own name rule, while the
        // actual history and complete source shape below still require the exact original IDs.
        var migrationName = db.GetService<IMigrationsIdGenerator>().GetName(sourceMigration);
        await db.GetService<IMigrator>().MigrateAsync(migrationName);
        var runId = Guid.NewGuid(); var storeId = Guid.NewGuid(); var writeId = Guid.NewGuid();
        const string oldPayload = "{\"source\":\"LegacyFixture-原文\",\"protocolStatus\":2,\"quality\":\"HistoricalRecordedClaim\"}";
        db.Manifests.Add(new() { StoreId = storeId, SchemaVersion = "s01-store/1", Profile = "Test", PrepareOperationId = Guid.NewGuid(), PreparedUtc = DateTimeOffset.UtcNow });
        db.Runs.Add(new() { RunId = runId, RequestId = "upgrade-fixture", SubjectId = "component", Revision = 1, State = RunState.Created, CreatedUtc = DateTimeOffset.UtcNow });
        db.Writes.Add(new() { WriteId = writeId, RunId = runId, Revision = 1, Kind = "Audit", PayloadJson = oldPayload,
            PayloadDigest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(oldPayload))), CommittedUtc = DateTimeOffset.UtcNow });
        Directory.CreateDirectory(Path.Combine(root, "media-root"));
        await File.WriteAllBytesAsync(Path.Combine(root, "media-root", "legacy.bin"), [1, 7, 9, 0, 255]);
        db.Media.Add(new() { MediaId = Guid.NewGuid(), RunId = runId, CaptureId = Guid.NewGuid(), RelativeKey = "legacy.bin", ByteLength = 5,
            Format = "bin", Source = "LegacyFixture", State = "Ready" });
        await db.SaveChangesAsync();
        using var connection = OpenStore(root);
        var source = StoreSchemaInspection.Inspect(connection, root, hashes: true);
        Assert.Equal("U0", source.State); Assert.Equal(storeId, source.StoreId);
        return source;
    }
    private static string Connection(string root) => new SqliteConnectionStringBuilder { DataSource = Path.Combine(root, "station01.test.db"),
        ForeignKeys = true, Pooling = false, DefaultTimeout = 1 }.ToString();
    private static SqliteConnection OpenStore(string root) { var c = new SqliteConnection(Connection(root)); c.Open(); return c; }
    private static string Workspace()
    {
        for (var p = new DirectoryInfo(AppContext.BaseDirectory); p is not null; p = p.Parent)
            if (File.Exists(Path.Combine(p.FullName, "global.json"))) return p.FullName;
        throw new InvalidOperationException("WorkspaceNotFound");
    }
    private static string ToolPath(string workspace)
    {
        var component = Environment.GetEnvironmentVariable("GAODE_009_STOREPREP_COMPONENT");
        var configuration = typeof(StorePreparationTests).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyConfigurationAttribute), false)
            .Cast<System.Reflection.AssemblyConfigurationAttribute>().Single().Configuration;
        var path = component ?? Path.Combine(workspace, "backend", "tools", "Gaode.StorePrep", "bin", configuration, "net10.0", "Gaode.StorePrep.dll");
        Assert.True(File.Exists(path), "Build the actual StorePrep before the controlled upgrade suite: " + path);
        if (component is not null) Assert.StartsWith(Path.Combine(workspace, "artifacts", "recipe-execution-008", "009-isolation"), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);
        return path;
    }
    private static Process StartTool(string tool, string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(tool); foreach (var argument in arguments) start.ArgumentList.Add(argument);
        start.Environment["GAODE_009_STORE_INTERRUPTION"] = "1";
        return Process.Start(start) ?? throw new InvalidOperationException("StorePrepProcessNotStarted");
    }
    private static async Task AwaitCheckpointAsync(Process process, string phase)
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (await process.StandardOutput.ReadLineAsync(watchdog.Token) is { } line)
            if (line == "UPGRADE_CHECKPOINT:" + phase) return;
        throw new InvalidOperationException("MaintenanceCheckpointMissing:" + phase + ":" + await process.StandardError.ReadToEndAsync());
    }
    private static async Task<object> InterruptToolAsync(string tool, string[] arguments, string phase)
    {
        using var process = StartTool(tool, [..arguments, "--pause-at", phase]);
        try
        {
            await AwaitCheckpointAsync(process, phase);
            var pid = process.Id; var interruptedAt = DateTimeOffset.UtcNow;
            process.Kill(entireProcessTree: true); await process.WaitForExitAsync();
            return new { pid, phase, interruptedAt, exitCode = process.ExitCode, kind = "ActualProcessKilledAtRealTransactionBoundary" };
        }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } }
    }
    private sealed record ToolResult(int ExitCode, string Output, string Error);
    private static async Task<ToolResult> RunToolAsync(string tool, string[] arguments)
    {
        using var process = StartTool(tool, arguments);
        var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(watchdog.Token); }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } }
        return new(process.ExitCode, await output, await error);
    }
    private static async Task<StoreMaintenanceResult> RunMaintenanceAsync(string tool, string[] arguments)
    {
        var run = await RunToolAsync(tool, arguments);
        Assert.True(run.ExitCode == 0, run.Output + run.Error);
        return JsonSerializer.Deserialize<StoreMaintenanceResult>(run.Output.Trim()) ?? throw new InvalidOperationException("MaintenanceResultMissing");
    }
}
