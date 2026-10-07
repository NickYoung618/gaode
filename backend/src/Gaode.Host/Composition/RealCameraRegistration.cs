using Gaode.Application.Acquisition;
using Gaode.Application.Ports;
using Gaode.Host.Api;
using Gaode.Infrastructure.Devices.Cameras;
using Gaode.Infrastructure.Media;
using Gaode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Gaode.Host.Composition;

public static class RealCameraRegistration
{
    public static RealCameraOptions ReadOptions(IConfiguration config)
    {
        var s = config.GetSection("Gaode:Cameras");
        string Required(string name) => s[name] ?? throw new InvalidOperationException("Gaode:Cameras:" + name + "Missing");
        return new(Required("SitePath"), Required("WorkerPath"), Required("GalaxySdkPath"),
            Required("CameraProSdkPath"), Required("StateRoot"), s.GetValue("StartupTimeoutMs", 30000),
            s.GetValue("CaptureTimeoutMs", 30000), s.GetValue("ShutdownTimeoutMs", 15000));
    }
    public static IServiceCollection AddRealCameras(this IServiceCollection services, RealCameraOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<PersistentCameraGateway>();
        services.AddSingleton<ICameraSdkGateway>(sp => sp.GetRequiredService<PersistentCameraGateway>());
        services.AddSingleton<ICapturePort, CameraCaptureAdapter>();
        services.AddSingleton<CameraAcquisitionService>();
        services.AddHostedService<RealCameraHostedService>();
        return services;
    }

    public static async Task RunCaptureOnlyAsync(WebApplicationBuilder builder)
    {
        if (builder.Configuration["Gaode:Mode"] != "Production") throw new InvalidOperationException("CaptureOnlyRequiresProductionMode");
        var root = builder.Configuration["Gaode:CameraStoreRoot"] ?? throw new InvalidOperationException("CameraStoreRootMissing");
        if (!Path.IsPathFullyQualified(root)) throw new InvalidOperationException("CameraStoreRootMustBeAbsolute");
        var options = ReadOptions(builder.Configuration);
        builder.Services.AddSingleton(new CameraStoreOwnership(root));
        builder.Services.AddSingleton(CameraCaptureJournal.Options(root));
        builder.Services.AddSingleton<CameraCaptureJournal>();
        builder.Services.AddSingleton<ICameraCaptureJournal>(sp => sp.GetRequiredService<CameraCaptureJournal>());
        builder.Services.AddSingleton(new MediaCapacity(builder.Configuration.GetValue("Gaode:CameraMemoryBytes", 2L * 1024 * 1024 * 1024),
            0, builder.Configuration.GetValue("Gaode:CameraDiskBytes", 100L * 1024 * 1024 * 1024),
            builder.Configuration.GetValue("Gaode:CameraDiskBytes", 100L * 1024 * 1024 * 1024)));
        builder.Services.AddSingleton<MediaLeaseRegistry>();
        builder.Services.AddSingleton(sp => new MediaStore(Path.Combine(root, "media"),
            sp.GetRequiredService<MediaCapacity>(), sp.GetRequiredService<MediaLeaseRegistry>(), 2));
        builder.Services.AddSingleton<IMediaStore>(sp => sp.GetRequiredService<MediaStore>());
        builder.Services.AddRealCameras(options);
        builder.Services.AddStation01Api(builder.Configuration);
        var app = builder.Build();
        app.UseAuthentication(); app.UseAuthorization();
        using var diagnostics = new Gaode.Infrastructure.Diagnostics.RuntimeDiagnosticLogging(
            app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Gaode.CameraCapture"));
        await app.Services.GetRequiredService<CameraCaptureJournal>().RestoreAsync(
            app.Services.GetRequiredService<MediaStore>(), app.Lifetime.ApplicationStopping);
        app.MapRealCameraEndpoints();
        app.MapPost("/api/v1/cameras/shutdown", (HttpContext http, IHostApplicationLifetime lifetime) =>
        {
            http.Response.OnCompleted(() => { lifetime.StopApplication(); return Task.CompletedTask; });
            return Results.Accepted(value: new { state = "Stopping", parameterRestoration = "Pending" });
        }).RequireAuthorization(Station01Authorization.RecoveryCheck);
        app.Logger.LogInformation("Formal capture-only Host: PLC/algorithm/external-light not composed; production readiness not granted");
        await app.RunAsync();
    }
}

public sealed class RealCameraHostedService(PersistentCameraGateway cameras, ILogger<RealCameraHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        await cameras.StartAsync(ct);
        foreach (var status in cameras.Status) logger.LogInformation("Camera lifecycle {Role} {State} pid={Pid} session={Session} error={Error}",
            status.Role, status.State, status.ProcessId, status.SessionId, status.Error);
    }
    public async Task StopAsync(CancellationToken ct) => await cameras.DisposeAsync();
}

public sealed class CameraStoreOwnership : IDisposable
{
    private readonly FileStream _lock;
    public CameraStoreOwnership(string root) => _lock = new(Path.Combine(root, ".camera-host.lock"),
        FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    public void Dispose() => _lock.Dispose();
}
