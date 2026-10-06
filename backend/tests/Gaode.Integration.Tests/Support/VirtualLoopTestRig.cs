
using System.Net.Http.Json;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Application.Station01;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Persistence;
using Gaode.Application.Recipes;
using Gaode.Infrastructure.Recipes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Gaode.Integration.Tests.Support;

/// <summary>One isolated, formal Modbus Test/VirtualPlc instance and current 007 host wiring.</summary>
public sealed class VirtualLoopTestRig : IAsyncDisposable
{
    private readonly VirtualPlcFixture plc;
    private JsonRecipeCatalog? currentCatalog;
    private IReadOnlyList<string> currentSlots = ["P01"];
    public Station01HostFixture Host { get; }
    public int PlcPort => plc.Port;
    public void InjectFault(string fault) => plc.InjectFault(fault);
    public void ClearControlledPhysicalFaultsForTest() => plc.ClearControlledPhysicalFaultsForTest();
    private VirtualLoopTestRig(VirtualPlcFixture plc, Station01HostFixture host) { this.plc=plc; Host=host; }

    public static async Task<VirtualLoopTestRig> CreateAsync(
        Action<IServiceCollection>? configureServices = null, string? imageManifestPath = null,
        string? workerManifestPath = null, string? workerScriptPath = null,
        bool useCurrentRecipe = false, string? recipeCatalogPath = null,
        IReadOnlyList<string>? occupiedSlots = null)
    {
        var plc=await VirtualPlcFixture.CreateAsync(useCurrentRecipe);
        Station01HostFixture? host=null;
        try {
            JsonRecipeCatalog? catalog=null;
            if(useCurrentRecipe) {
                var fixtures=Path.Combine(Station01HostFixture.FindWorkspace(),"specs","008-recipe-driven-inspection","fixtures");
                catalog=new JsonRecipeCatalog(recipeCatalogPath??Path.Combine(fixtures,"recipes-singleface-gates.json"));
                imageManifestPath??=Path.Combine(fixtures,"media-manifest.json");
                workerManifestPath??=Path.Combine(fixtures,"worker-manifest-singleface-gates.json");
            }
            var originalConfigure=configureServices;
            if(catalog is not null) configureServices=services=> {
                services.RemoveAll<IRecipeCatalog>(); services.AddSingleton<IRecipeCatalog>(catalog); originalConfigure?.Invoke(services);
            };
            host=await Station01HostFixture.CreateAsync(configureServices,
                simulationId:"s01-sim-virtual-loop",mode:"VirtualPlcIntegration",
                publicId:"s01-public-virtual-loop",budgetId:"s01-budget-virtual-loop",plcPort:plc.Port,
                configurationFeature:"007-station01-integrated-loop",publicVersion:"1.2.0",budgetVersion:"2.0.0",simulationVersion:"2.0.0",
                use007Components:true,imageManifestPath:imageManifestPath,workerManifestPath:workerManifestPath,
                workerScriptPath:workerScriptPath);
            return new(plc,host) {currentCatalog=catalog,currentSlots=occupiedSlots??["P01"]};
        } catch {if(host is not null)await host.DisposeAsync();await plc.DisposeAsync();throw;}
    }

    public async Task<(StartPublicRequest Request, HttpResponseMessage Response, StartReceipt Receipt)> StartAsync()
    {
        if (currentCatalog is not null)
        {
            var readyUntil = DateTimeOffset.UtcNow.AddSeconds(10);
            var readiness = Host.Host.Services.GetRequiredService<StartupReadiness>();
            while (true)
            {
                var observed = readiness.Observe();
                if (observed.HasReliableObservation && observed.OperatingMode == OperatingMode.Automatic && observed.SafetyAssessment == SafetyAssessment.Clear) break;
                if (DateTimeOffset.UtcNow >= readyUntil)
                    throw new TimeoutException("Current recipe Test PLC did not become ready.");
                await Task.Delay(25);
            }
        }
        var context = StartRunContextJson.Create(occupiedSlots: ["P01"]);
        if (currentCatalog is not null)
        {
            var selected = currentCatalog.GetSnapshot().Definitions.Single();
            context = JsonSerializer.Serialize(new
            {
                schemaVersion = StartRunContext.RecipeSchemaVersion, trayId = Guid.NewGuid(),
                stationId = "10000000-0000-0000-0000-000000000001",
                lineId = "20000000-0000-0000-0000-000000000001",
                scenarioId = selected.ScenarioId, occupiedSlots = currentSlots, purpose = "Test",
                expectedRecipeRef = new { recipeId = selected.RecipeId, version = selected.Version,
                    catalogDigest = selected.CatalogDigest }
            });
        }
        var request = new StartPublicRequest("necessary-" + Guid.NewGuid().ToString("N"),
            context,
            new ConfigReference("s01-public-virtual-loop", "1.2.0"),
            new ConfigReference("s01-budget-virtual-loop", "3.0.0"),
            new ConfigReference("s01-sim-virtual-loop", "3.0.0"));
        var response = await Host.Client.PostAsJsonAsync("/api/v1/station01/runs", request);
        var receipt = (await response.Content.ReadFromJsonAsync<StartReceipt>())!;
        return (request, response, receipt);
    }

    public async Task<RunApiSnapshot> WaitAsync(Guid runId, TimeSpan timeout,
        params RunState[] expected)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        RunApiSnapshot? snapshot = null;
        do
        {
            snapshot = await Host.Client.GetFromJsonAsync<RunApiSnapshot>(
                $"/api/v1/station01/runs/{runId:D}");
            if (snapshot is not null && expected.Contains(snapshot.State)) return snapshot;
            await Task.Delay(100);
        } while (DateTimeOffset.UtcNow < deadline);
        throw new TimeoutException($"Run {runId:D} did not reach {string.Join(',', expected)}; " +
            $"last={snapshot?.State}/{snapshot?.ErrorCode}");
    }

    public async Task<string> SaveEvidenceAsync(string scenario, StartPublicRequest request,
        HttpResponseMessage response, StartReceipt receipt, object? facts = null)
    {
        var folder = currentCatalog is null
            ? Path.Combine(Station01HostFixture.FindWorkspace(), "artifacts", "station01-007",
                "necessary-failures-20260924", scenario + "-" + receipt.RunId.ToString("N"))
            : Path.Combine(Station01HostFixture.FindWorkspace(), "artifacts", "recipe-execution-008",
                Environment.GetEnvironmentVariable("GAODE_008_TEST_BATCH") ?? "closure-integrations-20260926", scenario + "-" + receipt.RunId.ToString("N"));
        if (Environment.GetEnvironmentVariable("GAODE_009_INTEGRATION_ROOT") is { Length: > 0 } controlled)
        {
            var approved = Path.GetFullPath(Gaode.Testing.ApprovedTestRoot.Resolve(Station01HostFixture.FindWorkspace()))
                .TrimEnd(Path.DirectorySeparatorChar);
            if (!Path.IsPathFullyQualified(controlled) || !Path.GetFullPath(controlled)
                .StartsWith(approved + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("ComponentEvidenceOutsideApprovedTestRoot");
            for (var parent = new DirectoryInfo(controlled); parent is not null; parent = parent.Parent)
                if (parent.Exists && (parent.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("ComponentEvidenceLinkedAncestor");
            folder = Path.Combine(controlled, scenario + "-" + receipt.RunId.ToString("N"));
        }
        if (Directory.Exists(folder)) throw new InvalidOperationException("ComponentEvidenceAlreadyExists");
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "request.json"), JsonSerializer.Serialize(request));
        await File.WriteAllTextAsync(Path.Combine(folder, "receipt.json"), JsonSerializer.Serialize(new
        { status = (int)response.StatusCode, body = await response.Content.ReadAsStringAsync() }));
        await File.WriteAllTextAsync(Path.Combine(folder, "run.json"), JsonSerializer.Serialize(
            await Host.Client.GetFromJsonAsync<RunApiSnapshot>($"/api/v1/station01/runs/{receipt.RunId:D}")));
        await File.WriteAllTextAsync(Path.Combine(folder, "api-run.json"), await Host.Client.GetStringAsync($"/api/v1/station01/runs/{receipt.RunId:D}"));
        await File.WriteAllTextAsync(Path.Combine(folder, "api-evidence.json"), await Host.Client.GetStringAsync($"/api/v1/station01/runs/{receipt.RunId:D}/evidence"));
        var options = Host.Host.Services.GetRequiredService<DbContextOptions<Station01DbContext>>();
        await using (var db = new Station01DbContext(options))
        {
            await File.WriteAllTextAsync(Path.Combine(folder, "stage-events.json"), JsonSerializer.Serialize(
                await db.StageEvents.AsNoTracking().Where(x => x.RunId == receipt.RunId).ToListAsync()));
            await File.WriteAllTextAsync(Path.Combine(folder, "writes.json"), JsonSerializer.Serialize(
                await db.Writes.AsNoTracking().Where(x => x.RunId == receipt.RunId).ToListAsync()));
        }
        await plc.SaveDiagnosticsAsync(folder);
        if (facts is not null)
            await File.WriteAllTextAsync(Path.Combine(folder, "facts.json"), JsonSerializer.Serialize(facts));
        var dbPath = Path.Combine(Host.StoreRoot, "station01.test.db");
        if (File.Exists(dbPath))
        {
            await using var source = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
            await using var target = new SqliteConnection($"Data Source={Path.Combine(folder, "station01.snapshot.db")}");
            await source.OpenAsync();
            await target.OpenAsync();
            source.BackupDatabase(target);
        }
        await File.WriteAllTextAsync(Path.Combine(folder, "origin.json"), JsonSerializer.Serialize(new
        { utc = DateTimeOffset.UtcNow, testOnly = true, plcPort = PlcPort, Host.StoreRoot,
            configuration = "007 public 1.2.0; simulation/budget 2.0.0",
            request.RequestId, receipt.RunId, source = "in-process Host fixture + formal loopback Modbus VirtualPlc" }));
        var mediaRoot = Path.Combine(Host.StoreRoot, "media-root");
        var mediaManifest = new List<object>();
        if (Directory.Exists(mediaRoot))
            foreach (var file in Directory.EnumerateFiles(mediaRoot, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(mediaRoot, file);
                var target = Path.Combine(folder, "media-readback", relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                var bytes = await File.ReadAllBytesAsync(file);
                await File.WriteAllBytesAsync(target, bytes);
                mediaManifest.Add(new { relative, byteLength = bytes.Length,
                    sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)) });
            }
        await File.WriteAllTextAsync(Path.Combine(folder, "media-readback.json"), JsonSerializer.Serialize(mediaManifest));
        return folder;
    }

    public async ValueTask DisposeAsync()
    {
        await Host.DisposeAsync();
        await plc.DisposeAsync();
    }
}
