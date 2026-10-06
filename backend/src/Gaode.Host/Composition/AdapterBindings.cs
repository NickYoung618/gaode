using Gaode.Application.Ports;
using Gaode.Infrastructure.Devices.Plc;
using Gaode.Infrastructure.Integrations;
using Gaode.Infrastructure.Simulation;
using Gaode.Infrastructure.Configuration;
using Gaode.Domain.Configuration;
using Microsoft.Extensions.Time.Testing;

namespace Gaode.Host.Composition;

public static class AdapterBindings
{
    public static IServiceCollection AddSimulationAdapters(this IServiceCollection services,
        SimulationProfile profile, BusinessBudget budget)
    {
        services.AddSingleton<TimeProvider>(_ => profile.ClockMode switch
        {
            "RealElapsed" => TimeProvider.System,
            "Controlled" => new FakeTimeProvider(profile.VirtualStartUtc),
            _ => throw new InvalidOperationException("UnsupportedSimulationClockMode")
        });
        services.AddSingleton(sp => new SimulationEventScheduler(
            sp.GetRequiredService<TimeProvider>(), budget.Limits.MaxPendingTimerEvents));
        services.AddSingleton(sp => new SimulatedPlc(profile,
            sp.GetRequiredService<SimulationEventScheduler>(), budget.BusinessMs.HeartbeatFlip, sp.GetRequiredService<CommunicationEvidenceRecorder>()));
        services.AddSingleton<IPlcStatePort>(sp => sp.GetRequiredService<SimulatedPlc>());
        services.AddSingleton<IPlcActionPort>(sp => sp.GetRequiredService<SimulatedPlc>());
        services.AddSingleton<IMotionPort>(sp => sp.GetRequiredService<SimulatedPlc>());
        services.AddSingleton<IAcquisitionCyclePort>(sp => sp.GetRequiredService<SimulatedPlc>());
        services.AddSingleton<IPlcResetPort>(sp => sp.GetRequiredService<SimulatedPlc>());
        services.AddSingleton<ICapturePort>(sp => new SimulatedCapture(profile,
            sp.GetRequiredService<SimulationEventScheduler>()));
        services.AddSingleton<IAlgorithmPort>(sp => new SimulatedAlgorithm(profile,
            sp.GetRequiredService<SimulationEventScheduler>()));
        return services;
    }

    public static IServiceCollection AddFirstStationStageAdapters(this IServiceCollection services,
        Station01RuntimeOptions options, BusinessBudget budget)
    {
        services.AddSingleton(sp => options.Mode == "FullSimulation"
            ? new RotationExecutionConfiguration(null)
            : RotationExecutionConfigurationFactory.FromDevice(sp.GetRequiredService<LatestProtocolPlcDevice>()));
        services.AddSingleton<IDetectionPort, Gaode.Application.Workflow.RecipeDetectionExecutor>();

        if (options.Mode == "FullSimulation")
            services.AddSingleton<IPlcStageActionPort, NotIntegratedPlcStageActionPort>();
        else
            services.AddSingleton<IPlcStageActionPort>(sp =>
                new LatestProtocolStageActionAdapter(sp.GetRequiredService<LatestProtocolPlcDevice>(),
                    budget.BusinessMs,
                    sp.GetRequiredService<Gaode.Application.Workflow.SortingTargetAllocator>()));
        return services;
    }
}
