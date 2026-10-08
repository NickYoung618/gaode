using Gaode.Application.Acquisition;
using Gaode.Application.Algorithms;
using Gaode.Application.Capabilities;
using Gaode.Application.Configuration;
using Gaode.Application.Motion;
using Gaode.Application.Ports;
using Gaode.Application.Station01;
using Gaode.Application.Station01.Steps;
using Gaode.Application.Timing;
using Gaode.Domain.Configuration;
using Gaode.Infrastructure.Configuration;
using Gaode.Infrastructure.Integrations;
using Gaode.Infrastructure.Diagnostics;
using Gaode.Infrastructure.Media;
using Gaode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Gaode.Infrastructure.Devices.Plc;
using Gaode.Infrastructure.Simulation;
using Gaode.Infrastructure.Algorithms;
using Gaode.Infrastructure.Devices.Cameras;
using Gaode.Application.Workflow;

namespace Gaode.Host.Composition;

public static class Station01Registration
{
    public static IServiceCollection AddStation01(this IServiceCollection services,
        Station01RuntimeOptions options)
    {
        var commissioningMode = options.Mode == RuntimePurposes.RealDeviceCommissioning;
        if (options.Mode is not ("FullSimulation" or "VirtualPlcIntegration" or "Production" or RuntimePurposes.RealDeviceCommissioning))
            throw new InvalidOperationException("未知运行模式");
        if (options.Cameras is not null && options.Mode != "Production" && !commissioningMode)
            throw new InvalidOperationException("RealCameraWorkersRequireProductionMode");
        if (commissioningMode && (options.PlcProvider != "Real" || options.Cameras is null ||
            string.IsNullOrWhiteSpace(options.PlcHost) || options.PlcPort is < 1 or > 65535 ||
            !double.IsFinite(options.PositionTolerance) || options.PositionTolerance <= 0 ||
            options.PlcMechanicsPath is null || !Path.IsPathFullyQualified(options.PlcMechanicsPath) ||
            options.PlcFieldProfilePath is null || !Path.IsPathFullyQualified(options.PlcFieldProfilePath) ||
            options.CommissioningPath is null || options.CommissioningSha256 is null))
            throw new InvalidOperationException("CommissioningRealDevicesAndExplicitConfigurationRequired");
        if (!commissioningMode && (options.CommissioningPath is not null || options.CommissioningSha256 is not null))
            throw new InvalidOperationException("CommissioningConfigurationRequiresCommissioningMode");
        if (options.PlcProvider is not ("Virtual" or "Real"))
            throw new InvalidOperationException("Gaode:PlcProvider必须为Virtual或Real");
        if (options.TestPersistenceFaultCase is { } faultCase &&
            (options.Mode != "VirtualPlcIntegration" || options.PlcProvider != "Virtual" ||
             !ControlledTestPersistenceFault.IsKnown(faultCase)))
            throw new InvalidOperationException("PersistenceFaultRequiresControlledVirtualTestCase");
        var virtualLoopConfigured = options.ImageManifestPath is not null ||
            options.WorkerExecutablePath is not null || options.WorkerScriptPath is not null ||
            options.WorkerManifestPath is not null;
        if (virtualLoopConfigured && (options.Mode != "VirtualPlcIntegration" ||
            options.ImageManifestPath is null || options.WorkerExecutablePath is null ||
            options.WorkerScriptPath is null || options.WorkerManifestPath is null ||
            !Path.IsPathFullyQualified(options.ImageManifestPath) ||
            !Path.IsPathFullyQualified(options.WorkerExecutablePath) ||
            !Path.IsPathFullyQualified(options.WorkerScriptPath) ||
            !Path.IsPathFullyQualified(options.WorkerManifestPath) ||
            !File.Exists(options.ImageManifestPath) ||
            !File.Exists(options.WorkerScriptPath) ||
            !File.Exists(options.WorkerManifestPath)))
            throw new InvalidOperationException("007虚拟闭环配置必须在VirtualPlcIntegration中完整提供并使用现存绝对路径");
        if (!Path.IsPathFullyQualified(options.TestRoot) ||
            !Path.IsPathFullyQualified(options.ConfigRoot) ||
            !Path.IsPathFullyQualified(options.SchemaRoot))
            throw new InvalidOperationException("配置、schema和Test根必须使用绝对路径");
        var loader = new ConfigurationLoader(options.ConfigRoot, options.SchemaRoot);
        var publicConfig = loader.LoadPublic(options.PublicReference);
        var budget = loader.LoadBudget(options.BudgetReference);
        var simulation = commissioningMode ? null : loader.LoadSimulation(options.SimulationReference);
        var commissioning = commissioningMode
            ? CommissioningAlgorithmInputs.Load(options.CommissioningPath!, options.CommissioningSha256!) : null;
        if (!commissioningMode && (publicConfig.Value.Purpose != "Test" || budget.Value.Purpose != "Test" ||
            simulation!.Value.Purpose != "Test" ||
            (options.Mode == "FullSimulation" && publicConfig.Value.Bindings.Any(b => b.Provider != "Simulated")) ||
            (options.Mode == "VirtualPlcIntegration" && publicConfig.Value.Bindings.Any(b => b.Provider != (b.Role == "PLC" ? options.PlcProvider : "Simulated")))))
            throw new InvalidOperationException("Test配置不得用于真实设备绑定");
        if (commissioningMode)
        {
            if (options.SimulationReference != new ConfigReference(commissioning!.Value.Id, commissioning.Value.Version))
                throw new InvalidOperationException("CommissioningFixedReferenceMismatch");
            var validation = new PublicConfigurationValidator(Station01Policies.Create()).Validate(
                publicConfig.Value, budget.Value, null, false, false, options.PlcProvider, true, commissioning.Value);
            if (!validation.CanStart) throw new InvalidOperationException(string.Join(";", validation.BlockingControlErrors));
            services.AddSingleton(commissioning);
        }
        services.AddSingleton(options);
        services.AddSingleton<IPublicConfiguration>(loader);
        services.AddSingleton<PublicPositionTeaching>();
        services.AddSingleton(sp => CapabilityRegistration.RegisterStation01(sp.GetRequiredService<IAlgorithmPort>(), publicConfig.Value.Purpose, commissioning?.Value));
        services.AddSingleton<PublicConfigurationValidator>();
        services.AddSingleton<Gaode.Application.Recipes.IExecutionCostProvider, ApprovedExecutionCostProvider>();
        if (options.Mode == "FullSimulation")
        {
            services.AddSimulationAdapters(simulation!.Value, budget.Value);
        }
        else
        {
            // VirtualPlcIntegration may use simulated camera/algorithm ports for bounded
            // test runs. Production must not silently fall back to simulated success.
            if (options.Mode == "VirtualPlcIntegration")
            {
                services.AddSimulationAdapters(simulation!.Value, budget.Value);
                if (options.ImageManifestPath is not null)
                    services.AddSingleton<ICapturePort>(_ => new FileBackedCapture(options.ImageManifestPath));
                if (options.WorkerExecutablePath is not null && options.WorkerScriptPath is not null &&
                    options.WorkerManifestPath is not null)
                {
                    services.AddSingleton(sp =>
                    {
                        _ = sp.GetRequiredService<StoreAccessGuard>();
                        var mediaRoot = Path.Combine(options.TestRoot, "media-root");
                        Directory.CreateDirectory(mediaRoot);
                        return new WorkerProcessSupervisor(options.WorkerExecutablePath, mediaRoot,
                            $"\"{options.WorkerScriptPath}\" \"{options.WorkerManifestPath}\"",
                            WorkerImplementation.Read(options.WorkerScriptPath, options.WorkerManifestPath));
                    });
                    services.AddSingleton<IAlgorithmPort>(sp => new PythonWorkerAdapter(
                        sp.GetRequiredService<WorkerProcessSupervisor>()));
                }
            }
            else
            {
                services.AddSingleton<TimeProvider>(_ => TimeProvider.System);
                if (commissioningMode)
                {
                    services.AddSingleton<ILightGateway, SimulatedLightGateway>();
                    services.AddRealCameras(options.Cameras!, commissioning!.Value.PublicLightChannels, requireSeven: true, publicConfig.Value);
                    services.AddSingleton(sp => new CommissioningAlgorithm(commissioning, sp.GetRequiredService<MediaStore>()));
                    services.AddSingleton<IAlgorithmPort>(sp => sp.GetRequiredService<CommissioningAlgorithm>());
                    services.AddSingleton<ICommissioningRunInputs>(sp => sp.GetRequiredService<CommissioningAlgorithm>());
                }
                else
                {
                    if (options.Cameras is null) services.AddSingleton<ICapturePort, NotIntegratedCapture>();
                    else services.AddRealCameras(options.Cameras);
                    services.AddSingleton<IAlgorithmPort, NotIntegratedAlgorithm>();
                }
            }
            var plcOptions = new PlcRuntimeOptions
            {
                Provider = options.PlcProvider, Host = options.PlcHost, Port = options.PlcPort,
                Purpose = commissioningMode ? RuntimePurposes.RealDeviceCommissioning : null,
                UnitId = options.PlcUnitId,
                IoTimeoutMs = options.PlcIoTimeoutMs,
                HeartbeatTimeoutMs = budget.Value.BusinessMs.HeartbeatDisconnect,
                ActionsEnabled = true
            };
            services.AddSingleton(sp => {
                return new LatestProtocolPlcDevice(plcOptions, options.PositionTolerance,
                    sp.GetRequiredService<ILogger<LatestProtocolPlcDevice>>(), sp.GetRequiredService<CommunicationEvidenceRecorder>(),
                    options.PlcMechanicsPath, options.PlcFieldProfilePath);
            });
            services.AddSingleton<IPlcStatePort>(sp => sp.GetRequiredService<LatestProtocolPlcDevice>());
            services.AddSingleton<IPlcActionPort>(sp => sp.GetRequiredService<LatestProtocolPlcDevice>());
            services.AddSingleton<IMotionPort>(sp => sp.GetRequiredService<LatestProtocolPlcDevice>());
            services.AddSingleton<IAcquisitionCyclePort>(sp => sp.GetRequiredService<LatestProtocolPlcDevice>());
            services.AddSingleton<IPhysicalHandlingPort>(sp => sp.GetRequiredService<LatestProtocolPlcDevice>());
            services.AddSingleton<IPlcResetPort>(sp => sp.GetRequiredService<LatestProtocolPlcDevice>());
            services.AddHostedService<PlcConnectionHostedService>();
        }
        services.AddFirstStationStageAdapters(options, budget.Value);
        services.AddSingleton(sp => new DeadlineScheduler(sp.GetRequiredService<TimeProvider>(),
            "station01-" + Guid.NewGuid().ToString("N")));
        services.AddSingleton(sp => new OperationIngress(
            sp.GetRequiredService<DeadlineScheduler>(),
            budget.Value.Limits.LateEvidencePerOperation,
            budget.Value.Limits.DuplicateSummariesPerOperation));
        services.AddSingleton<ResourceLease>();
        services.AddSingleton<MotionCoordinator>();
        services.AddSingleton(sp => new Station01Coordinator(budget.Value.Limits.FlowNormal,
            budget.Value.Limits.FlowControl, budget.Value.Limits.TerminalReservations));
        services.AddSingleton<CommandRegistry>();
        services.AddSingleton<TrayAnomalyDecisionService>();
        services.AddSingleton<Station01ControlCommandService>();
        services.AddSingleton<NormalPauseBoundary>();
        if (options.Mode == "VirtualPlcIntegration" && options.PlcProvider == "Virtual")
            services.AddSingleton(sp => new FixedMoveRecoveryInteraction(sp.GetRequiredService<Station01Coordinator>(),
                sp.GetRequiredService<MotionCoordinator>(), sp.GetRequiredService<IPlcResetPort>(),
                sp.GetRequiredService<CommandRegistry>(), sp.GetRequiredService<ITraceQuery>(), options.TestRecoveryWaitMs,
                () => sp.GetRequiredService<AlgorithmRuntime>().ActiveExecutions == 0 &&
                    sp.GetRequiredService<MediaStore>().ActiveLeases == 0 &&
                    sp.GetRequiredService<MediaStore>().ActiveJobs == 0 && sp.GetRequiredService<MediaStore>().ActiveReservations == 0 &&
                    sp.GetRequiredService<ICapturePort>() is FileBackedCapture camera && camera.ActiveExecutions == 0 &&
                    sp.GetRequiredService<IAlgorithmPort>() is PythonWorkerAdapter worker && worker.InputsAndExecutionsReleased));
        services.AddSingleton<StartupReadiness>();
        services.AddSingleton(sp => StoreAccessGuard.Acquire(options.TestRoot, options.AllowedTestRoot));
        if (options.TestPersistenceFaultCase is { } selectedFault)
            services.AddSingleton(sp => {
                _ = sp.GetRequiredService<StoreAccessGuard>();
                return new ControlledTestPersistenceFault(options.TestRoot, selectedFault);
            });
        services.AddSingleton(sp =>
        {
            _ = sp.GetRequiredService<StoreAccessGuard>();
            var builder = new DbContextOptionsBuilder<Station01DbContext>();
            builder.UseSqlite(StoreCompatibilityProbe.ReadWriteConnectionString(options.TestRoot, options.StoreProfile));
            sp.GetService<ControlledTestPersistenceFault>()?.Configure(builder);
            return builder.Options;
        });
        services.AddSingleton<TraceWriter>(sp => {
            var database = sp.GetRequiredService<DbContextOptions<Station01DbContext>>();
            var clock = sp.GetRequiredService<TimeProvider>();
            return sp.GetService<ControlledTestPersistenceFault>() is { } fault
                ? fault.CreateWriter(database, clock, budget.Value.Limits.Writer)
                : new TraceWriter(database, clock, budget.Value.Limits.Writer);
        });
        services.AddSingleton<ITraceWriter>(sp => sp.GetRequiredService<TraceWriter>());
        services.AddSingleton(sp =>
        {
            _ = sp.GetRequiredService<StoreAccessGuard>();
            var store = StoreCompatibilityProbe.Inspect(options.TestRoot, options.StoreProfile);
            if (!store.Compatible || store.StoreId is not { } id) throw new InvalidOperationException(store.Code);
            return new CommunicationEvidenceRecorder(sp.GetRequiredService<TraceWriter>(), id, sp.GetRequiredService<TimeProvider>(), budget.Value.BusinessMs.CriticalSave);
        });
        services.AddSingleton<TraceQuery>(sp =>
        {
            _ = sp.GetRequiredService<StoreAccessGuard>();
            var builder = new DbContextOptionsBuilder<Station01DbContext>();
            builder.UseSqlite(StoreCompatibilityProbe.ReadOnlyConnectionString(options.TestRoot, options.StoreProfile));
            return new TraceQuery(builder.Options, sp.GetRequiredService<TimeProvider>(),
                budget.Value.BusinessMs.Query);
        });
        services.AddSingleton<ITraceQuery>(sp => sp.GetRequiredService<TraceQuery>());
        services.AddSingleton<IStageHandoffQuery>(sp => sp.GetRequiredService<TraceQuery>());
        services.AddSingleton<PublicPreparationHandoffV2Consumer>();
        services.AddSingleton<StageEventStore>(sp => new StageEventStore(
            sp.GetRequiredService<DbContextOptions<Station01DbContext>>(),
            sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IStageEventStore>(sp => sp.GetRequiredService<StageEventStore>());
        services.AddSingleton<IWholeTrayCompletionStore>(sp => new WholeTrayCompletionStore(
            sp.GetRequiredService<DbContextOptions<Station01DbContext>>(),
            sp.GetRequiredService<TimeProvider>(), commands: sp.GetRequiredService<CommandRegistry>()));
        services.AddSingleton<IControlledRecoveryDecisionStore>(sp =>
            new ControlledRecoveryDecisionStore(
                sp.GetRequiredService<DbContextOptions<Station01DbContext>>()));
        services.AddSingleton<ControlledRecoveryService>();
        services.AddSingleton<RecipeSortingMapper>();
        services.AddSingleton(sp => new SortingTargetAllocator(sp.GetRequiredService<IStageEventStore>(),
            sp.GetRequiredService<TimeProvider>(), budget.Value.BusinessMs.CriticalSave));
        services.AddSingleton<IWorkflowDelay, SystemWorkflowDelay>();
        services.AddSingleton<ThreeStageWorkflowExecutor>();
        services.AddSingleton<ThreeStageRecoveryService>();
        services.AddSingleton(sp => new WholeTrayWorkflowOrchestrator(
            sp.GetRequiredService<ThreeStageWorkflowExecutor>(), sp.GetRequiredService<IStageEventStore>(),
            sp.GetRequiredService<IWholeTrayCompletionStore>(), sp.GetRequiredService<ResourceLease>(),
            sp.GetRequiredService<TimeProvider>(), new(Gaode.Domain.Station01.ComponentEvidenceSource.Real,
                typeof(Station01Registration).Assembly.FullName!, "Derived")));
        services.AddSingleton(sp => new MediaCapacity(budget.Value.Limits.MediaMemoryBytes,
            budget.Value.Limits.FReservedMemoryBytes, budget.Value.Limits.RunMediaQuotaBytes,
            budget.Value.Limits.DataQuotaBytes));
        services.AddSingleton<MediaLeaseRegistry>();
        services.AddSingleton<MediaStore>(sp =>
        {
            _ = sp.GetRequiredService<StoreAccessGuard>();
            return new MediaStore(Path.Combine(options.TestRoot, "media-root"),
                sp.GetRequiredService<MediaCapacity>(), sp.GetRequiredService<MediaLeaseRegistry>(),
                budget.Value.Limits.MediaJobs);
        });
        services.AddSingleton<IMediaStore>(sp => sp.GetRequiredService<MediaStore>());
        services.AddSingleton<Gaode.Infrastructure.Persistence.CameraCaptureJournal>();
        services.AddSingleton<CameraAcquisitionService>();
        services.AddSingleton<AcquisitionCoordinator>();
        services.AddSingleton<AlgorithmLeaseSupervisor>();
        services.AddSingleton(sp => new AlgorithmRuntime(
            sp.GetRequiredService<IAlgorithmPort>(), sp.GetRequiredService<IMediaStore>(),
            sp.GetRequiredService<OperationIngress>(), sp.GetRequiredService<AlgorithmLeaseSupervisor>(),
            budget.Value.Limits.AlgorithmQueuePerRole, budget.Value.Limits.WorkerPerRole));
        services.AddSingleton<FixedMoveStep>();
        services.AddSingleton<StartPreparationStep>();
        services.AddSingleton<ThreeDStep>();
        services.AddSingleton<FScanStep>();
        services.AddSingleton<CompletePublicPreparation>();
        services.AddSingleton(sp => new StartPublicPreparation(
            sp.GetRequiredService<IPublicConfiguration>(),
            sp.GetRequiredService<CapabilityRegistry>(),
            sp.GetRequiredService<PublicConfigurationValidator>(),
            sp.GetRequiredService<ITraceWriter>(),
            sp.GetRequiredService<Station01Coordinator>(),
            sp.GetRequiredService<CommandRegistry>(),
            sp.GetRequiredService<StartupReadiness>(),
            sp.GetRequiredService<StartPreparationStep>(),
            sp.GetRequiredService<ThreeDStep>(),
            sp.GetRequiredService<FScanStep>(),
            sp.GetRequiredService<Gaode.Application.Recipes.IRecipeCatalog>(),
            sp.GetRequiredService<CompletePublicPreparation>(),
            sp.GetRequiredService<PublicPreparationHandoffV2Consumer>(),
            sp.GetRequiredService<WholeTrayWorkflowOrchestrator>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<DeadlineScheduler>().ClockId,
            Guid.NewGuid(), options.SimulationReference, budget.Value, sp.GetRequiredService<Gaode.Application.Recipes.IExecutionCostProvider>(),
            options.Mode == "VirtualPlcIntegration", options.PlcProvider,
            sp.GetRequiredService<Gaode.Infrastructure.Diagnostics.StructuredStageDiagnostics>(),
            sp.GetService<FixedMoveRecoveryInteraction>(), sp.GetRequiredService<NormalPauseBoundary>(),
            sp.GetService<ControlledTestPersistenceFault>() is { } bindingSchedule
                ? bindingSchedule.BeforeRecipeBindingAsync : null,
            sp.GetService<ControlledTestPersistenceFault>() is { } continuationSchedule
                ? continuationSchedule.BeforeRecipeContinuationAsync : null,
            sp.GetRequiredService<TrayAnomalyDecisionService>(), commissioning,
            sp.GetService<ICommissioningRunInputs>()));
        services.AddSingleton<IReservedIntegration>(new NotIntegratedPort("MES"));
        if (commissioningMode)
            services.AddSingleton(sp => new CommissioningRecoveryService(
                sp.GetRequiredService<IPlcResetPort>(), sp.GetRequiredService<MotionCoordinator>(),
                sp.GetRequiredService<CommandRegistry>(), sp.GetRequiredService<Station01Coordinator>(),
                sp.GetRequiredService<ITraceWriter>(), sp.GetRequiredService<ITraceQuery>(),
                id => !sp.GetRequiredService<StartPublicPreparation>().IsExecuting(id),
                () => sp.GetRequiredService<AlgorithmRuntime>().ActiveExecutions == 0 &&
                    sp.GetRequiredService<MediaStore>().ActiveJobs == 0 &&
                    sp.GetRequiredService<MediaStore>().ActiveReservations == 0 &&
                    sp.GetRequiredService<MediaStore>().ActiveLeases == 0 &&
                    sp.GetRequiredService<PersistentCameraGateway>().Status.All(s => s.State is "Ready" or "Stopped"),
                budget.Value.BusinessMs.XyCompletion, budget.Value.BusinessMs.CriticalSave));
        services.AddSingleton<IReservedIntegration>(new NotIntegratedPort("ModelManagement"));
        services.AddSingleton<IReservedIntegration>(new NotIntegratedPort("SampleManagement"));
        services.AddSingleton<StructuredStageDiagnostics>();
        if (options.Mode == "VirtualPlcIntegration" && options.WorkerExecutablePath is not null)
            services.AddHostedService<Lifecycle.VirtualWorkerHostedService>();
        services.AddSingleton<Lifecycle.Station01HostedService>();
        services.AddHostedService(sp => sp.GetRequiredService<Lifecycle.Station01HostedService>());
        services.AddSingleton(sp => new Api.Station01NotificationService(
            sp.GetRequiredService<Station01Coordinator>(),
            sp.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<Api.Station01Hub>>(),
            sp.GetRequiredService<ITraceQuery>(), sp.GetRequiredService<IStageEventStore>(),
            sp.GetRequiredService<TraceWriter>(), sp.GetRequiredService<StageEventStore>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Api.Station01NotificationService>>(),
            budget.Value.Limits.NotificationsPerClient));
        services.AddHostedService(sp => sp.GetRequiredService<Api.Station01NotificationService>());
        return services;
    }
}

