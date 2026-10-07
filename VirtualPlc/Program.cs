using Gaode.Plc.Protocol;
using VirtualPlc;

ThreadPool.GetMinThreads(out var previousWorkerMinimum, out var ioMinimum);
ThreadPool.GetMaxThreads(out var workerMaximum, out _);
var workerMinimum = Gaode.Diagnostics.ThreadPoolRuntimePolicy.WindowsNative ? previousWorkerMinimum : Math.Min(workerMaximum,
    Math.Max(previousWorkerMinimum, Math.Max(8, Environment.ProcessorCount + 4)));
if (workerMinimum > previousWorkerMinimum && !ThreadPool.SetMinThreads(workerMinimum, ioMinimum))
    throw new InvalidOperationException("VirtualPlcHeartbeatThreadPoolCapacityUnavailable");

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

builder.Services.Configure<ModbusOptions>(builder.Configuration.GetSection("Modbus"));
builder.Services.Configure<SimulationOptions>(builder.Configuration.GetSection("Simulation"));
builder.Services.Configure<DashboardOptions>(builder.Configuration.GetSection("Dashboard"));
builder.Services.AddSingleton<PlcDataStore>();
builder.Services.AddSingleton<VirtualPlcEngine>();
builder.Services.AddHostedService(serviceProvider =>
    serviceProvider.GetRequiredService<VirtualPlcEngine>());
builder.Services.AddHostedService<ModbusTcpServer>();

var app = builder.Build();
app.Logger.LogInformation(
    "VirtualPlc heartbeat worker minimum: previous={Previous}, effective={Effective}, processors={Processors}; heartbeat timeout unchanged",
    previousWorkerMinimum, workerMinimum, Environment.ProcessorCount);

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context => context.Context.Response.Headers.CacheControl = "no-store"
});

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "VirtualPlc",
    timestamp = DateTimeOffset.UtcNow
}));

app.MapGet("/api/simulator/state", (VirtualPlcEngine engine) =>
    Results.Ok(engine.GetSnapshot()));

app.MapGet("/api/simulator/changes", (long? after, PlcDataStore store) =>
    Results.Ok(store.GetChanges(Math.Max(0, after ?? 0))));

app.MapGet("/api/simulator/audit", (long? after, PlcDataStore store, VirtualPlcEngine engine) =>
{
    var writes = store.GetWriteAudit();
    var cursor = Math.Max(0, after ?? 0);
    var oldest = writes.Count == 0 ? 1 : writes[0].Sequence;
    var actions = engine.GetActionAudit();
    return Results.Ok(new { schemaVersion = "virtual-plc-write-audit/2.0", source = "Virtual",
        oldestSequence = oldest, latestSequence = writes.LastOrDefault()?.Sequence ?? 0,
        gap = cursor < oldest - 1, writes = writes.Where(x => x.Sequence > cursor),
        feedbackFaults = store.GetFeedbackFaults(),
        actions = actions.Actions, actionOldestSequence = actions.OldestSequence,
        actionLatestSequence = actions.LatestSequence, actionGap = actions.Gap });
});

app.MapGet("/api/simulator/address-map", (PlcDataStore store) => Results.Ok(new
{
    contract = PlcAddressMap.Contract,
    addressConvention = "Hexadecimal one-based document points; zero-based Modbus PDU offsets.",
    float32ByteOrder = store.ByteOrder.ToString(),
    coils = PlcAddressMap.CoilPoints.Values.OrderBy(x => x.DocumentNumber),
    holdingRegisters = PlcAddressMap.HoldingRegisterPoints.Values.OrderBy(x => x.DocumentNumber)
}));

app.MapPost("/api/simulator/reset", (VirtualPlcEngine engine) =>
{
    engine.ResetSimulation();
    return Results.Ok(new
    {
        success = true,
        message = "Virtual PLC reset completed. PC_System_Ready must be set again."
    });
});

app.MapPost("/api/simulator/faults/{fault}",
    (string fault, VirtualPlcEngine engine) =>
    {
        if (!Enum.TryParse<SimulationFault>(fault, true, out var parsed))
        {
            return Results.BadRequest(new
            {
                success = false,
                message = $"Unknown fault: {fault}",
                allowed = Enum.GetNames<SimulationFault>()
            });
        }

        var result = engine.InjectFault(parsed);
        return Results.Ok(new { success = result.Accepted, message = result.Message });
    });

app.MapGet("/api/simulator/flow-decision",
    (string category, bool stepSucceeded, VirtualPlcEngine engine) =>
    {
        if (!Enum.TryParse<FlowFailureCategory>(category, true, out var parsed))
        {
            return Results.BadRequest(new
            {
                success = false,
                message = $"Unknown category: {category}",
                allowed = Enum.GetNames<FlowFailureCategory>()
            });
        }

        return Results.Ok(engine.ResolveFlowDecision(parsed, stepSucceeded));
    });

app.Lifetime.ApplicationStarted.Register(() =>
{
    var options = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<DashboardOptions>>().Value;
    DashboardLauncher.TryOpen(options, app.Logger);
});

app.Run();
