using Gaode.Infrastructure.Simulation;
using Gaode.Infrastructure.Recipes;
using Gaode.Integration.Tests.Storage;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json.Nodes;

namespace Gaode.Integration.Tests.Support;

public sealed class Station01HostFixture : IAsyncDisposable
{
    // The host reads its test configuration from process-wide environment
    // variables during WebApplicationFactory construction. Keep one fixture's
    // environment and store isolated until that fixture is disposed.
    private static readonly SemaphoreSlim EnvironmentGate = new(1, 1);
    public string ApprovedRoot { get; }
    public string StoreRoot { get; }
    public string RecipeStoreRoot { get; }
    public WebApplicationFactory<Program> Host { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    // Counts actual persisted software binding facts, not device commands or valid authorization receipts.
    public int CommittedRecipeBindings
    {
        get
        {
            using var db = new Gaode.Infrastructure.Persistence.Station01DbContext(
                Host.Services.GetRequiredService<Microsoft.EntityFrameworkCore.DbContextOptions<Gaode.Infrastructure.Persistence.Station01DbContext>>());
            return db.Writes.Where(w => w.Kind == "ActionFact").AsEnumerable().Count(w =>
            {
                using var json = System.Text.Json.JsonDocument.Parse(w.PayloadJson);
                return json.RootElement.TryGetProperty("kind", out var kind) && kind.GetString() == "RecipePlanBound";
            });
        }
    }
    private WebApplicationFactory<Program>? _baseHost;
    private const string Token = "station01-explicit-local-test-token";
    private static readonly IReadOnlyDictionary<string, string> Tokens = new Dictionary<string, string>
    {
        ["Operator"] = Token,
        ["EquipmentEngineer"] = "station01-equipment-engineer-token",
        ["ProcessEngineer"] = "station01-process-engineer-token",
        ["SystemAdministrator"] = "station01-system-administrator-token"
    };
    private readonly Dictionary<string, string?> _oldEnvironment = [];
    private bool _environmentGateHeld;

    private Station01HostFixture(string approvedRoot, string storeRoot, string recipeStoreRoot)
    { ApprovedRoot = approvedRoot; StoreRoot = storeRoot; RecipeStoreRoot = recipeStoreRoot; }

    public static async Task<Station01HostFixture> CreateAsync(
        Action<IServiceCollection>? configureServices = null,
        string simulationId = "s01-sim-normal",
        Func<string, Task>? afterStorePrepared = null,
        string mode = "FullSimulation",
        string publicId = "s01-public-dev",
        string budgetId = "s01-budget-dev",
        int plcPort = 1502,
        string? testAllowedOrigin = null,
        string configurationFeature = "001-station01-public-preparation",
        string publicVersion = "1.0.0", string budgetVersion = "3.0.0",
        string simulationVersion = "3.0.0", bool use007Components = false,
        string? imageManifestPath = null, string? workerManifestPath = null,
        string? workerScriptPath = null,
        string? recipeFixtureCode = null)
    {
        await EnvironmentGate.WaitAsync();
        var gateTransferred = false;
        Station01HostFixture? fixture = null;
        try
        {
            var workspace = FindWorkspace();
            var approved = Gaode.Testing.ApprovedTestRoot.Resolve(workspace);
            var store = Path.Combine(approved, "m1-" + Guid.NewGuid().ToString("N"));
            await StorePreparation.PrepareEmptyTestStoreAsync(approved, store);
            // Separate, real schema preparation. Host keeps the same SQLite instance
            // behind both save and directory ports; no in-memory replacement.
            var recipeStore = Path.Combine(approved, "recipes-" + Guid.NewGuid().ToString("N"));
            RecipeStoreSchema.Prepare(approved, recipeStore);
            if (afterStorePrepared is not null) await afterStorePrepared(store);
            fixture = new Station01HostFixture(approved, store, recipeStore);
            var feature = Path.Combine(workspace, "specs", configurationFeature);
            var configurationRoot = Path.Combine(feature, "examples");
            if (recipeFixtureCode is not null)
            {
                if (mode != "FullSimulation") throw new InvalidOperationException("RecipeFixtureRequiresFullSimulation");
                // The real loader freezes this exact copied input; do not replace just the
                // algorithm while claiming that the original configuration was executed.
                var copyRoot = Path.Combine(store, "test-configuration");
                Directory.CreateDirectory(copyRoot);
                var matched = 0;
                foreach (var source in Directory.EnumerateFiles(configurationRoot, "*.json"))
                {
                    var content = await File.ReadAllTextAsync(source);
                    var json = JsonNode.Parse(content)!;
                    if (json["id"]?.GetValue<string>() == simulationId)
                    {
                        json["fixtures"]!["rawCodes"] = new JsonArray(recipeFixtureCode);
                        content = json.ToJsonString(); matched++;
                    }
                    await File.WriteAllTextAsync(Path.Combine(copyRoot, Path.GetFileName(source)), content);
                }
                if (matched != 1) throw new InvalidOperationException("SingleSimulationFixtureRequired");
                configurationRoot = copyRoot;
            }
            var settings = new Dictionary<string, string?>
            {
                ["Gaode__Mode"] = mode,
                ["Gaode__TestRoot"] = store,
                ["Gaode__AllowedTestRoot"] = approved,
                ["RecipeStore__DatabasePath"] = Path.Combine(recipeStore, "recipes.db"),
                ["RecipeStore__ReadWriteTimeoutMs"] = "10000",
                ["RecipeStore__DbLockTimeoutSeconds"] = "1",
                ["Gaode__ConfigRoot"] = configurationRoot,
                ["Gaode__SchemaRoot"] = Path.Combine(workspace, "specs", "001-station01-public-preparation", "contracts"),
                ["Gaode__PublicId"] = publicId,
                ["Gaode__PublicVersion"] = publicVersion,
                ["Gaode__BudgetId"] = budgetId,
                ["Gaode__BudgetVersion"] = budgetVersion,
                ["Gaode__SimulationId"] = simulationId,
                ["Gaode__SimulationVersion"] = simulationVersion,
                ["Gaode__Tokens__Operator"] = Token,
                ["Gaode__PlcHost"] = "127.0.0.1",
                ["Gaode__PlcPort"] = plcPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Gaode__PlcUnitId"] = "1",
                ["Gaode__PlcProvider"] = "Virtual",
                ["Gaode__PlcIoTimeoutMs"] = "1000"
            };
            // Never inherit another local session's worker/image settings into a test host.
            settings["Gaode__ImageManifestPath"] = null;
            settings["Gaode__WorkerExecutablePath"] = null;
            settings["Gaode__WorkerScriptPath"] = null;
            settings["Gaode__WorkerManifestPath"] = null;
            if (use007Components)
            {
                var python = FindPythonExecutable();
                settings["Gaode__ImageManifestPath"] = imageManifestPath ??
                    Path.Combine(feature, "examples", "images.json");
                settings["Gaode__WorkerExecutablePath"] = Path.GetFullPath(python);
                settings["Gaode__WorkerScriptPath"] = workerScriptPath ??
                    Path.Combine(workspace, "scripts", "virtual-station01-algorithm.py");
                settings["Gaode__WorkerManifestPath"] = workerManifestPath ??
                    Path.Combine(feature, "examples", "virtual-algorithm.json");
            }
            foreach (var (role, token) in Tokens) settings["Gaode__Tokens__" + role] = token;
            if (testAllowedOrigin is not null)
                settings["Gaode__TestAllowedOrigin"] = testAllowedOrigin;
            foreach (var (key, value) in settings)
            {
                fixture._oldEnvironment[key] = Environment.GetEnvironmentVariable(key);
                Environment.SetEnvironmentVariable(key, value);
            }
            fixture._baseHost = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseContentRoot(Path.Combine(workspace, "backend", "src", "Gaode.Host"));
                    builder.ConfigureLogging(logging => logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning));
                });
            fixture.Host = configureServices is null
                ? fixture._baseHost
                : fixture._baseHost.WithWebHostBuilder(builder => builder.ConfigureServices(configureServices));
            fixture.Client = fixture.Host.CreateClient();
            fixture.Client.DefaultRequestHeaders.Authorization = new("Bearer", Token);
            fixture._environmentGateHeld = true;
            gateTransferred = true;
            return fixture;
        }
        catch
        {
            if (fixture is not null)
            {
                fixture.Client?.Dispose();
                fixture.Host?.Dispose();
                if (fixture._baseHost is not null && !ReferenceEquals(fixture._baseHost, fixture.Host))
                    fixture._baseHost.Dispose();
                foreach (var (key, value) in fixture._oldEnvironment)
                    Environment.SetEnvironmentVariable(key, value);
            }
            if (!gateTransferred) EnvironmentGate.Release();
            throw;
        }
    }

    public SimulatedPlc Plc => Host.Services.GetRequiredService<SimulatedPlc>();
    public HttpClient ClientForRole(string role)
    {
        var client = new HttpClient(Host.Server.CreateHandler()) { BaseAddress = Client.BaseAddress };
        client.DefaultRequestHeaders.Authorization = new("Bearer", Tokens[role]);
        return client;
    }
    public static string FindWorkspace()
    {
        var path = new DirectoryInfo(AppContext.BaseDirectory);
        while (path is not null)
        {
            if (File.Exists(Path.Combine(path.FullName, "global.json")) &&
                Directory.Exists(Path.Combine(path.FullName, "specs"))) return path.FullName;
            path = path.Parent;
        }
        throw new InvalidOperationException("找不到工作区");
    }

    public static string FindPythonExecutable()
    {
        var python = Environment.GetEnvironmentVariable("GAODE_TEST_PYTHON");
        if (string.IsNullOrWhiteSpace(python))
        {
            var name = OperatingSystem.IsWindows() ? "python.exe" : "python";
            python = (Environment.GetEnvironmentVariable("PATH") ?? "")
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Select(dir => Path.Combine(dir, name)).FirstOrDefault(File.Exists);
        }
        if (python is null || !File.Exists(python))
            throw new InvalidOperationException("007集成测试需要绝对路径的Python worker可执行文件");
        return Path.GetFullPath(python);
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            Client.Dispose();
            Host.Dispose();
            if (_baseHost is not null && !ReferenceEquals(_baseHost, Host)) _baseHost.Dispose();
            foreach (var (key, value) in _oldEnvironment) Environment.SetEnvironmentVariable(key, value);
        }
        finally
        {
            if (_environmentGateHeld)
            {
                _environmentGateHeld = false;
                EnvironmentGate.Release();
            }
        }
        return ValueTask.CompletedTask;
    }
}
