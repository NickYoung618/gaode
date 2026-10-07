using Gaode.Host.Api;
using Gaode.Host.Composition;
using Gaode.Domain.Configuration;
using Gaode.Application.Recipes;
using Gaode.Infrastructure.Recipes;

if (args.Length == 2 && args[0] == "--prepare-camera-store")
{
    Gaode.Infrastructure.Persistence.CameraCaptureJournal.Prepare(args[1]);
    return;
}

// The PLC heartbeat uses asynchronous socket completions on the process worker pool.
// On small-CPU hosts, blocking startup/SQLite work can otherwise leave completed
// loopback reads queued behind the pool's slow starvation recovery for >3 seconds.
var workerCapacity = Gaode.Infrastructure.Diagnostics.HostWorkerCapacity.Ensure();
var previousWorkerMinimum = workerCapacity.PreviousMinimum;
var workerMinimum = workerCapacity.EffectiveMinimum;
var ioMinimum = workerCapacity.IoMinimum;

var builder = WebApplication.CreateBuilder(args);
// The default Windows EventLog provider throws when the isolated Test account
// cannot open the .NET Runtime source. A logger exception must not terminate
// the PLC heartbeat worker; the redirected console log is retained per run.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
// Page polling is frequent; Gaode command/stage/device diagnostics retain their own categories.
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
var section = builder.Configuration.GetSection("Gaode");
if (section.GetValue("CaptureOnly", false))
{
    await RealCameraRegistration.RunCaptureOnlyAsync(builder);
    return;
}
var options = new Station01RuntimeOptions(
    section["Mode"] ?? throw new InvalidOperationException("Gaode:Mode缺失"),
    section["TestRoot"] ?? throw new InvalidOperationException("Gaode:TestRoot缺失"),
    section["AllowedTestRoot"] ?? throw new InvalidOperationException("Gaode:AllowedTestRoot缺失"),
    section["ConfigRoot"] ?? throw new InvalidOperationException("Gaode:ConfigRoot缺失"),
    section["SchemaRoot"] ?? throw new InvalidOperationException("Gaode:SchemaRoot缺失"),
    new ConfigReference(section["PublicId"] ?? "s01-public-dev", section["PublicVersion"] ?? "1.0.0"),
    new ConfigReference(section["BudgetId"] ?? "s01-budget-dev", section["BudgetVersion"] ?? "3.0.0"),
    new ConfigReference(section["SimulationId"] ?? "s01-sim-normal", section["SimulationVersion"] ?? "3.0.0"),
    section["PlcHost"] ?? "127.0.0.1", section.GetValue("PlcPort", 1502),
    section.GetValue("PlcUnitId", (byte)1), section.GetValue("PositionTolerance", 0.001),
    section["PlcProvider"] ?? "Virtual",
    section.GetValue("PlcIoTimeoutMs", 200),
    section["ImageManifestPath"], section["WorkerExecutablePath"],
    section["WorkerScriptPath"], section["WorkerManifestPath"],
    section.GetValue<int?>("TestRecoveryWaitMs") ?? 120000, section["TestPersistenceFaultCase"],
    PlcMechanicsPath: section["PlcMechanicsPath"],
    PlcFieldProfilePath: section["PlcFieldProfilePath"],
    Cameras: section.GetValue("Cameras:Enabled", false) ? RealCameraRegistration.ReadOptions(builder.Configuration) : null);
builder.Services.AddStation01(options);
var recipeStoreOptions = new RecipeStoreOptions
{
    DatabasePath = builder.Configuration["RecipeStore:DatabasePath"] ?? "",
    ReadWriteTimeoutMs = builder.Configuration.GetValue<int>("RecipeStore:ReadWriteTimeoutMs"),
    DbLockTimeoutSeconds = builder.Configuration.GetValue<int>("RecipeStore:DbLockTimeoutSeconds")
};
recipeStoreOptions.Validate(Path.Combine(options.TestRoot, "station01.test.db"));
builder.Services.AddSingleton(recipeStoreOptions);
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(services => new SqliteRecipeStore(recipeStoreOptions, options.AllowedTestRoot,
    services.GetRequiredService<ILogger<SqliteRecipeStore>>(), () =>
        services.GetRequiredService<IHttpContextAccessor>().HttpContext?.User
            .FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "unattributed"));
builder.Services.AddSingleton<IRecipeCatalog>(services => services.GetRequiredService<SqliteRecipeStore>());
builder.Services.AddSingleton<IRecipeStore>(services => services.GetRequiredService<SqliteRecipeStore>());
builder.Services.AddStation01Api(builder.Configuration);
var testOrigin = section["TestAllowedOrigin"];
if (options.Mode == "VirtualPlcIntegration" && !string.IsNullOrWhiteSpace(testOrigin))
{
    if (!Uri.TryCreate(testOrigin, UriKind.Absolute, out var originUri) ||
        originUri.Scheme is not ("https" or "http") || originUri.AbsolutePath != "/")
        throw new InvalidOperationException("Gaode:TestAllowedOrigin必须是受控HTTP(S)页面来源");
    builder.Services.AddCors(cors => cors.AddPolicy("Station01TestPage", policy => policy
        .WithOrigins(testOrigin).WithMethods("GET", "POST", "PUT", "DELETE")
        .WithHeaders("Authorization", "Content-Type", "If-None-Match", "If-Match",
            "X-Requested-With", "X-SignalR-User-Agent").WithExposedHeaders("ETag", "Location")));
}
var app = builder.Build();
using var runtimeDiagnostics = new Gaode.Infrastructure.Diagnostics.RuntimeDiagnosticLogging(
    app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Gaode.Runtime"));
app.Logger.LogInformation("Host worker capacity: previousMin={PreviousMin}, effectiveMin={EffectiveMin}, processorCount={ProcessorCount}, ioMin={IoMin}; PLC heartbeat timeout unchanged",
    previousWorkerMinimum, workerMinimum, Environment.ProcessorCount, ioMinimum);
app.Logger.LogInformation("Host runtime conditions: windowsNativeThreadPool={WindowsNativeThreadPool}, serverGc={ServerGc}, socketInlineCompletionsEnvironment={SocketInlineCompletionsEnvironment}; protective deadlines unchanged",
    workerCapacity.WindowsNative, System.Runtime.GCSettings.IsServerGC,
    Environment.GetEnvironmentVariable("DOTNET_SYSTEM_NET_SOCKETS_INLINE_COMPLETIONS") ?? "unset");
if (options.Mode == "VirtualPlcIntegration" && !string.IsNullOrWhiteSpace(testOrigin))
{
    app.UseCors("Station01TestPage");
    var sockets = new WebSocketOptions();
    sockets.AllowedOrigins.Add(testOrigin);
    app.UseWebSockets(sockets);
}
app.Use(async (context, next) =>
{
    // Queries/media polling are intentionally excluded from normal request logs.
    if (context.Request.Method is not ("POST" or "PUT" or "DELETE")) { await next(context); return; }
    var started = System.Diagnostics.Stopwatch.GetTimestamp();
    app.Logger.LogInformation("RuntimeHttp started traceId={TraceId} method={Method} path={Path}",
        context.TraceIdentifier, context.Request.Method, context.Request.Path);
    try { await next(context); }
    catch (Exception error)
    {
        app.Logger.LogError(error, "RuntimeHttp failed traceId={TraceId} method={Method} path={Path}",
            context.TraceIdentifier, context.Request.Method, context.Request.Path);
        throw;
    }
    finally
    {
        app.Logger.Log(context.Response.StatusCode >= 400 ? LogLevel.Warning : LogLevel.Information,
            "RuntimeHttp ended traceId={TraceId} method={Method} path={Path} status={Status} responseStarted={ResponseStarted} elapsedMs={ElapsedMs}; response is not physical completion",
            context.TraceIdentifier, context.Request.Method, context.Request.Path,
            context.Response.StatusCode, context.Response.HasStarted,
            System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }
});
app.UseAuthentication();
app.UseAuthorization();
app.MapStation01Api();
app.MapRecipeEndpoints();
if (options.Cameras is not null) app.MapRealCameraEndpoints();
// Cold EF model/query and recovered-run initialization must finish before PLC
// polling starts its unchanged communication and heartbeat deadlines.
await app.Services.GetRequiredService<Gaode.Host.Lifecycle.Station01HostedService>()
    .InitializePersistenceAsync(app.Lifetime.ApplicationStopping);
app.Run();

public partial class Program;
