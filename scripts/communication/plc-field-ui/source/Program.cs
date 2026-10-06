using FieldUi;
using System.Diagnostics;

var rootArgument = Array.IndexOf(args, "--root");
var root = rootArgument >= 0 ? Path.GetFullPath(args[rootArgument + 1]) : AppContext.BaseDirectory;
var portArgument = Array.IndexOf(args, "--port");
var port = portArgument >= 0 ? int.Parse(args[portArgument + 1]) : 18765;
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = root, Args = [] });
builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
builder.Logging.ClearProviders(); builder.Logging.AddSimpleConsole();
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
var app = builder.Build();
var engine = new Engine(root);
app.Use(async (context, next) =>
{
    try
    {
        if (context.Request.Host.Host != "127.0.0.1") { context.Response.StatusCode = 403; return; }
        if (context.Request.Method == "POST")
        {
            var origin = context.Request.Headers.Origin.ToString();
            if ((origin.Length > 0 && origin != $"http://127.0.0.1:{port}") || context.Request.ContentType?.StartsWith("application/json") != true)
            { context.Response.StatusCode = 403; return; }
        }
        await next(context);
    }
    catch (Exception e)
    {
        if (!context.Response.HasStarted) { context.Response.StatusCode = 400; await context.Response.WriteAsJsonAsync(new { error = e.Message }); }
    }
});
app.MapGet("/", () => Results.File(Path.Combine(root, "wwwroot", "index.html"), "text/html; charset=utf-8"));
app.MapGet("/app.js", () => Results.File(Path.Combine(root, "wwwroot", "app.js"), "text/javascript; charset=utf-8"));
app.MapGet("/style.css", () => Results.File(Path.Combine(root, "wwwroot", "style.css"), "text/css; charset=utf-8"));
app.MapGet("/api/config", engine.Config);
app.MapPost("/api/config", async (Configuration config) => { await engine.SaveConfig(config); return Results.Ok(new { saved = true }); });
app.MapGet("/api/state", engine.State);
app.MapPost("/api/connect", async () => { await engine.Connect(); return Results.Ok(new { connected = true }); });
app.MapPost("/api/disconnect", async () => { await engine.Disconnect(); return Results.Ok(new { stopped = true }); });
app.MapPost("/api/writes", async (Toggle request) => { await engine.EnableWrites(request.Enabled); return Results.Ok(new { request.Enabled }); });
app.MapPost("/api/heartbeat", async (Toggle request) => { await engine.EnableHeartbeat(request.Enabled); return Results.Ok(new { request.Enabled }); });
app.MapPost("/api/write", (WriteRequest r) => engine.ManualWrite(r.Signal, r.Value));
app.MapPost("/api/move", (MoveRequest r) => engine.StartMove(r.Axis, r.Target));
app.MapGet("/api/notes", engine.Notes);
app.MapPost("/api/notes", (System.Text.Json.JsonElement value) => { engine.SaveNotes(value); return Results.Ok(new { saved = true }); });
app.MapPost("/api/export", engine.Export);
app.MapGet("/api/download/{filename}", (string filename) =>
{
    if (Path.GetFileName(filename) != filename || !filename.StartsWith("PLC-return-") || !filename.EndsWith(".zip")) return Results.BadRequest();
    var path = Path.Combine(root, "exports", filename);
    return File.Exists(path) ? Results.File(path, "application/zip", filename) : Results.NotFound();
});
using var stop = new CancellationTokenSource();
var poll = engine.Poll(stop.Token);
app.Lifetime.ApplicationStarted.Register(() =>
{
    Console.WriteLine($"\n高德 PLC 联调台 {Engine.Version}：http://127.0.0.1:{port}\n日志与回传保存在：{root}\n关闭浏览器不会停止后台；请先点断开。\n");
    if (!args.Contains("--no-browser"))
        try { Process.Start(new ProcessStartInfo($"http://127.0.0.1:{port}") { UseShellExecute = true }); } catch (Exception e) { Console.WriteLine("请手动打开上述网址：" + e.Message); }
});
try { await app.RunAsync(); }
finally { stop.Cancel(); try { await poll; } catch (OperationCanceledException) { } await engine.Disconnect(); }
record Toggle(bool Enabled);
record WriteRequest(string Signal, double Value);
record MoveRequest(string Axis, double Target);
