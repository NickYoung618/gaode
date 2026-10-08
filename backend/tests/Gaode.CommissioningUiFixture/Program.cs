using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Capabilities;
using Gaode.Application.Configuration;
using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Application.Station01;
using Gaode.Communication.Tests;
using Gaode.Communication.Tests.Devices;
using Gaode.Domain.Configuration;
using Gaode.Host.Api;
using Gaode.Host.Composition;
using Gaode.Infrastructure.Diagnostics;
using Gaode.Infrastructure.Persistence;
using Gaode.Infrastructure.Recipes;
using Gaode.Infrastructure.Devices.Plc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

if (args.Length < 4 || args[0] != "--mode" || args[2] != "--output" ||
    args[1] is not ("commissioning" or "legacy-test") || !Path.IsPathFullyQualified(args[3]))
{ Console.Error.WriteLine("Use --mode commissioning|legacy-test --output <absolute manifest> [--self-check]"); return 2; }
await using var fixture = new OfflineFixture(args[1], args[3]);
await fixture.StartAsync();
if (args.Contains("--self-check")) await fixture.SelfCheckAsync();
else await fixture.App.WaitForShutdownAsync();
return 0;

internal sealed partial class OfflineFixture(string mode, string output) : IAsyncDisposable
{
    internal WebApplication App { get; private set; } = null!;
    private readonly ControlledCommissioningTests.Inputs inputs = new();
    private readonly CancellationTokenSource lifetime = new(TimeSpan.FromMinutes(15));
    private SiteProtocolTcpFixture? sitePlc;
    private ProtocolTcpFixture? legacyPlc;
    private LatestProtocolPlcDevice? device;
    private Station01RuntimeOptions options = null!;
    private RecipeDefinition recipe = null!;
    private string operatorToken = "", engineerToken = "";
    private IDisposable? diagnostics;
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    internal async Task StartAsync()
    {
        HostWorkerCapacity.Ensure();
        operatorToken = Environment.GetEnvironmentVariable("GAODE_OFFLINE_OPERATOR_CREDENTIAL") ?? throw new InvalidOperationException("OfflineOperatorCredentialRequired");
        engineerToken = Environment.GetEnvironmentVariable("GAODE_OFFLINE_ENGINEER_CREDENTIAL") ?? throw new InvalidOperationException("OfflineEngineerCredentialRequired");
        await inputs.PrepareStore(mode == "legacy-test" ? "Test" : null);
        options = inputs.Options;
        recipe = Recipe011Data.ForSlots(2, 1);
        if (mode == "legacy-test") await PrepareLegacyAsync();
        else
        {
            sitePlc = new SiteProtocolTcpFixture();
            options = options with { PlcPort = sitePlc.Port, PlcIoTimeoutMs = 1000 };
        }
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.Logging.ClearProviders();
        var log = Path.Combine(inputs.Root, "host.jsonl");
        builder.Logging.AddProvider(new OfflineLogProvider(log));
        diagnostics = new RuntimeDiagnosticLogging(new ControlledCommissioningTests.FileLogger(log));
        if (mode == "commissioning")
        {
            builder.Configuration["Gaode:Mode"] = "RealDeviceCommissioning";
            SetIdentity(builder.Configuration, 0, "operator", "Operator", operatorToken);
            SetIdentity(builder.Configuration, 1, "engineer", "ProcessEngineer", engineerToken);
        }
        else
        {
            builder.Configuration["Gaode:Tokens:Operator"] = operatorToken;
            builder.Configuration["Gaode:Tokens:ProcessEngineer"] = engineerToken;
            builder.Services.AddCors(c => c.AddPolicy("OfflinePage", p => p.WithOrigins("https://appassets.local")
                .WithMethods("GET", "POST", "PUT", "DELETE").AllowAnyHeader().WithExposedHeaders("ETag", "Location")));
        }
        var services = builder.Services;
        var previous = services.Where(s => s.ServiceType == typeof(IHostedService)).ToArray();
        services.AddStation01(options);
        // Only test harness drives lifecycle: never launch the seven real SDK workers.
        foreach (var s in services.Where(s => s.ServiceType == typeof(IHostedService) && !previous.Contains(s)).ToArray()) services.Remove(s);
        services.AddStation01Api(builder.Configuration);
        var recipeRoot = Path.Combine(inputs.Root, "recipes");
        RecipeStoreSchema.Prepare(inputs.Root, recipeRoot);
        var recipeOptions = new RecipeStoreOptions { DatabasePath = Path.Combine(recipeRoot, "recipes.db"), ReadWriteTimeoutMs = 10000, DbLockTimeoutSeconds = 5 };
        services.AddHttpContextAccessor();
        services.AddSingleton(recipeOptions);
        services.AddSingleton(sp => new SqliteRecipeStore(recipeOptions, inputs.Root, sp.GetRequiredService<ILogger<SqliteRecipeStore>>(),
            () => sp.GetRequiredService<IHttpContextAccessor>().HttpContext?.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "OFFLINE-fixture-seed"));
        services.AddSingleton<IRecipeStore>(sp => sp.GetRequiredService<SqliteRecipeStore>());
        services.AddSingleton<IRecipeCatalog>(sp => sp.GetRequiredService<SqliteRecipeStore>());
        if (device is not null)
        {
            services.AddSingleton(device);
            services.AddSingleton<IAlgorithmPort>(sp => new CommissioningWorkflowTests.FileReadingTestAlgorithm(sp.GetRequiredService<Gaode.Infrastructure.Media.MediaStore>(), recipe.FCode));
        }
        App = builder.Build();
        App.UseCors(mode == "commissioning" ? "Station01CommissioningPage" : "OfflinePage");
        App.UseAuthentication(); App.UseAuthorization();
        // Explicit test-only lost response switch, once; request still goes through the formal endpoint.
        var drop = Environment.GetEnvironmentVariable("GAODE_OFFLINE_DROP_START_RESPONSE") == "1";
        App.Use(async (http, next) =>
        {
            if (drop && http.Request.Method == "POST" && http.Request.Path == "/api/v1/station01/runs")
            {
                drop = false;
                var original = http.Response.Body;
                await using var discarded = new MemoryStream(); http.Response.Body = discarded;
                try { await next(http); http.Abort(); } finally { http.Response.Body = original; }
            }
            else await next(http);
        });
        App.MapStation01Api(); App.MapRecipeEndpoints();
        App.Urls.Add("http://127.0.0.1:0");
        await App.Services.GetRequiredService<Gaode.Host.Lifecycle.Station01HostedService>().InitializePersistenceAsync(lifetime.Token);
        var saved = await App.Services.GetRequiredService<IRecipeStore>().SaveAsync(new(recipe, null, null, "OFFLINE-seed-v1"), lifetime.Token);
        if (saved.Status != RecipeSaveStatus.Saved) throw new InvalidOperationException("OfflineRecipeSeedFailed:" + saved.Reason);
        recipe = saved.Definition!;
        if (mode == "commissioning")
            await App.Services.GetRequiredService<LatestProtocolPlcDevice>().StartAsync(lifetime.Token);
        await App.StartAsync(lifetime.Token);
        var mediaRuns = mode == "commissioning" ? await CreateMediaRunsAsync() : [];
        if (Environment.GetEnvironmentVariable("GAODE_OFFLINE_VERIFY_RECIPE_EDIT_021") == "1") _ = VerifyRecipeEditAsync();
        var manifest = new { schemaVersion = "commissioning-ui-fixture/1", scope = "OFFLINE", mode,
            apiBaseUrl = App.Urls.Single(), signalRUrl = App.Urls.Single() + "/hubs/station01", root = inputs.Root,
            recipeId = recipe.RecipeId, recipeVersion = recipe.Version, mediaRuns,
            operatorProfile = WriteProfile("operator", "Operator"), engineerProfile = WriteProfile("engineer", "ProcessEngineer"),
            preparedTemplatePath = WriteTemplate(), logPath = log,
            deviceScope = "loopback PLC only; seven SDK workers not started; no field authority" };
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, JsonSerializer.Serialize(manifest, Json));
    }
    private static void SetIdentity(IConfiguration c, int index, string name, string role, string token)
    {
        var p = "Gaode:CommissioningIdentities:" + index + ":";
        c[p + "profileId"] = "OFFLINE-" + name; c[p + "subjectId"] = "offline:" + name;
        c[p + "displayName"] = "OFFLINE " + name; c[p + "role"] = role;
        c[p + "purpose"] = "RealDeviceCommissioning"; c[p + "credential"] = token;
    }
    private string WriteProfile(string name, string role)
    {
        var path = Path.Combine(inputs.Root, name + "-profile.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new { schemaVersion = "commissioning-desktop-profile/1", profileId = "OFFLINE-" + name,
            expectedSubjectId = "offline:" + name, expectedRole = role, mode = "RealDeviceCommissioning",
            credentialEnvironmentVariable = "GAODE_OFFLINE_" + name.ToUpperInvariant() + "_CREDENTIAL", preparedTemplatePath = Path.Combine(inputs.Root, "template.json"), logRoot = Path.Combine(inputs.Root, "desktop-logs") }, Json));
        return path;
    }
    private string WriteTemplate()
    {
        var path = Path.Combine(inputs.Root, "template.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new { schemaVersion = "commissioning-console-template/1", id = "OFFLINE-template", version = "1",
            sourceReference = "OFFLINE:021-isolated-fixture/1;no-field-authority", mode = "RealDeviceCommissioning",
            contextTemplate = new { schemaVersion = StartRunContext.RecipeSchemaVersion, stationId = "OFFLINE-station", lineId = "OFFLINE-line", scenarioId = recipe.ScenarioId, occupiedSlots = new[] { "s1" }, purpose = "Commissioning" },
            publicConfigRef = options.PublicReference, budgetRef = options.BudgetReference, simulationRef = options.SimulationReference }, Json));
        return path;
    }
    internal async Task SelfCheckAsync()
    {
        using var client = new HttpClient { BaseAddress = new Uri(App.Urls.Single()) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", operatorToken);
        using var identity = await client.GetAsync("/api/v1/station01/identity"); identity.EnsureSuccessStatusCode();
        using var catalog = await client.GetAsync("/api/v1/recipes/catalog"); catalog.EnsureSuccessStatusCode();
        var manifest = JsonDocument.Parse(File.ReadAllText(output));
        var mediaResults = new List<object>();
        foreach (var run in manifest.RootElement.GetProperty("mediaRuns").EnumerateArray())
        {
            var runId = run.GetGuid();
            using var response = await client.GetAsync($"/api/v1/station01/runs/{runId}/media"); response.EnsureSuccessStatusCode();
            using var media = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            foreach (var item in media.RootElement.GetProperty("items").EnumerateArray())
            {
                using var file = await client.GetAsync("/api/v1/station01/media/" + item.GetProperty("mediaId").GetString()); file.EnsureSuccessStatusCode();
                if ((await file.Content.ReadAsByteArrayAsync()).Length == 0) throw new InvalidDataException("EmptyOfflineMedia");
            }
            mediaResults.Add(media.RootElement.Clone());
        }
        File.WriteAllText(output + ".check.json", JsonSerializer.Serialize(new { scope = "OFFLINE:fixture-foundation-not-page-or-desktop-acceptance",
            identity = JsonDocument.Parse(await identity.Content.ReadAsStringAsync()).RootElement.Clone(), catalogStatus = (int)catalog.StatusCode, mediaResults }, Json));
    }
    public async ValueTask DisposeAsync()
    {
        if (App is not null)
        {
            await App.Services.GetRequiredService<StartPublicPreparation>().StopAsync(CancellationToken.None);
            await App.Services.GetRequiredService<Station01Coordinator>().StopConsumerAsync(CancellationToken.None);
            await App.StopAsync(); await App.DisposeAsync();
        }
        if (device is not null) await device.DisposeAsync();
        if (legacyPlc is not null) await legacyPlc.DisposeAsync();
        if (sitePlc is not null) await sitePlc.DisposeAsync();
        diagnostics?.Dispose(); lifetime.Dispose(); inputs.Dispose();
    }
    private sealed class OfflineLogProvider(string path) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new ControlledCommissioningTests.FileLogger(path);
        public void Dispose() { }
    }
}
