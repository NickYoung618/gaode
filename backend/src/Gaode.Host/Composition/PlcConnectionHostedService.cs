using Gaode.Infrastructure.Devices.Plc;

namespace Gaode.Host.Composition;

public sealed class PlcConnectionHostedService(LatestProtocolPlcDevice device,
    IHostApplicationLifetime applicationLifetime, ILogger<PlcConnectionHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("StartupPlc category=Lifecycle phase=WaitingForHostStarted; device remains disconnected");
        var hostStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = applicationLifetime.ApplicationStarted.Register(() => hostStarted.TrySetResult());
        await hostStarted.Task.WaitAsync(stoppingToken);
        logger.LogInformation("StartupPlc category=Lifecycle phase=Connecting hostStarted=true");
        await device.StartAsync(stoppingToken);
        logger.LogInformation("StartupPlc category=Lifecycle phase=Connected; business admission still uses actual device state");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try { await base.StopAsync(cancellationToken); }
        finally { await device.DisposeAsync(); }
    }
}

