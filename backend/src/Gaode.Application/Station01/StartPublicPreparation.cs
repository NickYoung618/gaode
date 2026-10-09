using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Concurrent;
using Gaode.Application.Capabilities;
using Gaode.Application.Configuration;
using Gaode.Application.Motion;
using Gaode.Application.Ports;
using Gaode.Application.Station01.Steps;
using Gaode.Application.Recipes;
using Gaode.Application.Workflow;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Diagnostics;

namespace Gaode.Application.Station01;

public sealed record StartPublicRequest(string RequestId, string ContextJson,
    ConfigReference PublicConfigRef, ConfigReference BudgetRef,
    ConfigReference SimulationRef, FaultRestartFrom? RestartFrom = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    CommissioningRestartFrom? CommissioningRestartFrom = null);

public sealed record ThreeDAndFRecipeGateDecision(
    bool CanLoadAndBind,
    bool MustLockAndStop,
    string Code);

public static class ThreeDAndFRecipeGate
{
    public static ThreeDAndFRecipeGateDecision Evaluate(bool threeDAcquisitionCompleted,
        bool fAcquisitionCompleted, AlgorithmState fAlgorithmState, FCodeResult fCode)
    {
        if (!threeDAcquisitionCompleted)
            return new(false, true, "ThreeDAcquisitionIncomplete");
        if (!fAcquisitionCompleted)
            return new(false, true, "FAcquisitionIncomplete");
        if (!FCodePolicy.CanBindRecipe(fAlgorithmState, fCode))
            return new(false, true, "FCodeNotUniqueAndParsed");
        return new(true, false, "RecipeAdmissionAllowed");
    }

    public static T ExecuteAfterGate<T>(ThreeDAndFRecipeGateDecision decision, Func<T> operation)
    {
        if (!decision.CanLoadAndBind || decision.MustLockAndStop)
            throw new InvalidOperationException(decision.Code);
        return operation();
    }
}

public sealed class StartPublicPreparation(IPublicConfiguration configurations,
    CapabilityRegistry capabilities, PublicConfigurationValidator validator,
    ITraceWriter writer, Station01Coordinator coordinator, CommandRegistry commands,
    StartupReadiness readiness, StartPreparationStep startPreparation, ThreeDStep threeD,
    FScanStep fScan, IRecipeCatalog recipeCatalog,
    CompletePublicPreparation completion, PublicPreparationHandoffV2Consumer handoffConsumer,
    WholeTrayWorkflowOrchestrator wholeTrayWorkflow,
    TimeProvider clock, string clockId, Guid sessionId,
    ConfigReference boundSimulation, BusinessBudget bootstrapBudget, IExecutionCostProvider executionCosts, bool externalVirtualPlc = false,
    string externalPlcProvider = "Virtual", IStartupDiagnosticSink? diagnostics = null, FixedMoveRecoveryInteraction? recovery = null,
    NormalPauseBoundary? pause = null,
    Func<Guid, RecipeExecutionDeadlines?, int, CancellationToken, Task>? beforeRecipeBindingForTest = null,
    Func<Guid, RecipeExecutionDeadlines?, CancellationToken, Task>? beforeRecipeContinuationForTest = null,
    TrayAnomalyDecisionService? anomalyDecisions = null,
    LoadedConfiguration<CommissioningConfiguration>? commissioningConfiguration = null,
    ICommissioningRunInputs? commissioningInputs = null,
    LoadedConfiguration<RealAlgorithmConfiguration>? realAlgorithmConfiguration = null,
    Func<bool>? algorithmResourcesUnconfirmed = null)
{
    private readonly ConcurrentDictionary<Guid, ActiveRun> _active = new();
    private int _admissionClosed;
    public bool IsExecuting(Guid runId) => _active.ContainsKey(runId);

    public StartReceipt Start(string subject, StartPublicRequest request)
    {
        RuntimeDiagnostics.Record("StartAdmission", "Received", null,
            new { request.RequestId, request.PublicConfigRef, request.BudgetRef, request.SimulationRef });
        if (Volatile.Read(ref _admissionClosed) != 0 || coordinator.AdmissionClosed)
            throw new InvalidOperationException("HostStopping");
        if (request.SimulationRef != boundSimulation)
            throw new InvalidOperationException("本Host的模拟配置版本不可运行中切换");
        var canonical = JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (commands.Replay(subject, request.RequestId, canonical) is { } replay) return replay;
        if (algorithmResourcesUnconfirmed?.Invoke() == true) throw new InvalidOperationException("AlgorithmResourcesUnconfirmed");
        var startContext = StartRunContextParser.Parse(request.ContextJson);
        if (request.RestartFrom is { } restart) (recovery ?? throw new InvalidOperationException("FaultRestartUnavailable")).ValidateRestart(restart);
        var receipt = commands.Register(subject, request.RequestId, canonical, request.RestartFrom?.FaultRunId);
        if (coordinator.Query(receipt.RunId) is not null)
        {
            RuntimeDiagnostics.Record("StartAdmission", "Replay", receipt.RunId,
                new { request.RequestId, receipt.CommandId, disposition = "NoNewExecution" });
            return receipt;
        }
        var identity = startContext.Freeze(receipt.RunId, request.RequestId, subject,
            clock.GetUtcNow(), request.PublicConfigRef.Version, request.BudgetRef.Version,
            request.SimulationRef.Version);
        var snapshot = new RunSnapshot(receipt.RunId, request.RequestId, subject,
            RunState.Created, 0, 0, TerminalOutcome.None, false,
            ActionState.NotRequested, CaptureState.NotRequested,
            AlgorithmState.NotRequested, SaveState.NotQueued, HandoffState.NotReady,
            null, null, null, [], Identity: identity)
        {
            RecipeSelection = startContext.ExpectedRecipeRef is { } selected
                ? new RecipeSelectionProjection(selected.RecipeId, selected.Version,
                    selected.CatalogDigest, startContext.ScenarioId, startContext.Purpose.ToString())
                : null
        };
        if (!coordinator.TryRegister(snapshot))
            throw new InvalidOperationException(coordinator.AdmissionClosed ? "HostStopping" : "RunCapacityUnavailable");
        diagnostics?.Record(request.RequestId, receipt.CommandId, receipt.RunId,
            "Admission", "Accepted", "RunCreated");
        var active = new ActiveRun();
        if (!_active.TryAdd(receipt.RunId, active)) throw new InvalidOperationException("RunAlreadyActive");
        active.Task = Task.Run(async () =>
        {
            try { await ExecuteAsync(receipt, request, startContext, identity, subject, active.Cancellation.Token); }
            finally { _active.TryRemove(receipt.RunId, out _); }
        });
        return receipt;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        Interlocked.Exchange(ref _admissionClosed, 1);
        coordinator.BeginShutdown();
        var active = _active.Values.ToArray();
        foreach (var run in active) run.Cancellation.Cancel();
        var tasks = active.Select(x => x.Task).Where(x => x is not null).Cast<Task>().ToArray();
        if (tasks.Length > 0) await Task.WhenAll(tasks).WaitAsync(cancellationToken);
    }

    private async Task ExecuteAsync(StartReceipt receipt, StartPublicRequest request,
        StartRunContext startContext, WorkflowIdentity identity, string subject,
        CancellationToken cancellationToken)
    {
        long initialRevision = 0;
        RunExecution? execution = null;
        RuntimeDiagnostics.Record("RunExecution", "Started", receipt.RunId,
            new { request.RequestId, receipt.CommandId, sessionId, clockId,
                request.PublicConfigRef, request.BudgetRef, request.SimulationRef });
        try
        {
            await Stage(receipt.RunId, RunState.Preparing);
            initialRevision = await SaveInitialContextAsync(receipt, request, subject);
            commands.UpdateDurability(receipt.CommandId, "Committed");
            var publicConfig = configurations.LoadPublic(request.PublicConfigRef);
            var budget = configurations.LoadBudget(request.BudgetRef);
            var commissioning = publicConfig.Value.Purpose == RuntimePurposes.RealDeviceCommissioning;
            if (commissioning && startContext.Purpose != RunPurpose.Commissioning)
                throw new InvalidOperationException("CommissioningRunPurposeMismatch");
            var simulation = commissioning ? null : configurations.LoadSimulation(request.SimulationRef);
            var validation = validator.Validate(publicConfig.Value, budget.Value, simulation?.Value,
                fullSimulation: !externalVirtualPlc && !commissioning, externalVirtualPlc: externalVirtualPlc,
                externalPlcProvider: externalPlcProvider, realDeviceCommissioning: commissioning,
                commissioning: commissioningConfiguration?.Value, realAlgorithm: realAlgorithmConfiguration?.Value);
            var frozen = ConfigurationFreezer.Freeze(publicConfig, budget, simulation, capabilities.Versions, commissioningConfiguration, realAlgorithmConfiguration);
            RuntimeDiagnostics.Record("Configuration", validation.CanStart ? "Frozen" : "Blocked", receipt.RunId,
                new { frozen.SnapshotId, frozen.PublicDigest, frozen.BudgetDigest, frozen.SimulationDigest,
                    validation.BlockingControlErrors, validation.AlgorithmIssues,
                    purpose = frozen.Public.Purpose, source = frozen.Public.Source }, warning: !validation.CanStart);
            var run = new RunExecution(receipt.RunId, receipt.CommandId, request.RequestId,
                subject, request.ContextJson, frozen, writer, clock, sessionId, clockId);
            execution = run;
            run.FreezeIdentity(identity);
            run.AdoptInitialRevision(initialRevision);
            run.Progress = (action, capture, algorithm, save, handoff, persisted) =>
                coordinator.SetAsync(receipt.RunId, s => s with
                {
                    Action = action ?? s.Action, Capture = capture ?? s.Capture,
                    Algorithm = algorithm ?? s.Algorithm, Save = save ?? s.Save,
                    Handoff = handoff ?? s.Handoff,
                    PersistedRevision = persisted ?? s.PersistedRevision,
                    ObservedRevision = s.ObservedRevision + 1
                });
            await run.SaveAsync(WriteKind.Audit, new
            {
                kind = "FrozenPublicConfiguration", frozen.SnapshotId,
                frozen.PublicJson, frozen.BudgetJson, frozen.SimulationJson,
                frozen.PublicDigest, frozen.BudgetDigest, frozen.SimulationDigest, frozen.CommissioningJson,
                frozen.CommissioningDigest, frozen.CommissioningSourceFile,
                frozen.RealAlgorithmJson, frozen.RealAlgorithmDigest, frozen.RealAlgorithmSourceFile,
                frozen.CapabilityVersions
            });
            await coordinator.SetAsync(receipt.RunId, s => s with
            {
                PublicVersion = frozen.Public.Version, BudgetVersion = frozen.Budget.Version,
                SimulationVersion = frozen.Simulation?.Version,
                PersistedRevision = run.PersistedRevision, Save = SaveState.Committed,
                ObservedRevision = s.ObservedRevision + 1
            });
            var realAlgorithmNotReady = realAlgorithmConfiguration is not null && validation.AlgorithmIssues.Count > 0;
            if (!validation.CanStart || realAlgorithmNotReady)
            {
                await run.SaveAsync(WriteKind.Audit, new { validation.BlockingControlErrors,
                    validation.AlgorithmIssues, disposition = "ConfigurationBlocked" },
                    RunState.ConfigurationBlocked);
                await Stage(receipt.RunId, RunState.ConfigurationBlocked, realAlgorithmNotReady ? "RealAlgorithmNotReady" : "PublicConfigurationInvalid");
                diagnostics?.Record(request.RequestId, receipt.CommandId, receipt.RunId,
                    "ConfigurationValidation", "ConfigurationBlocked", "NoDeviceAction");
                return;
            }
            if (request.RestartFrom is { } restart)
            {
                await recovery!.LinkNewRunAsync(restart, receipt.RunId, receipt.CommandId, request.RequestId, run, cancellationToken);
            }
            if (commissioning && realAlgorithmConfiguration is null)
            {
                FLocation? selectedFLocation = null;
                if (startContext.ExpectedRecipeRef is { } selected)
                {
                    var catalog = recipeCatalog.GetSnapshot();
                    var saved = catalog.Definitions.SingleOrDefault(d => d.RecipeId == selected.RecipeId && d.Version == selected.Version);
                    if (saved is null || catalog.CatalogDigest != selected.CatalogDigest)
                        throw new InvalidOperationException("CommissioningSelectedRecipeVersionChanged");
                    if (saved.CommissioningFPosition is { SchemaVersion: "commissioning-f-position/1", X: { } x, Y: { } y })
                        selectedFLocation = new(x, y, frozen.Public.Motion.Unit, frozen.Public.Motion.Frame,
                            $"recipe:{saved.RecipeId}/{saved.Version}:{saved.DefinitionDigest}:commissioningFPosition");
                    else if (saved.LightExecution is not null)
                        throw new InvalidOperationException("CommissioningRecipeFPositionMissing");
                }
                (commissioningInputs ?? throw new InvalidOperationException("CommissioningAlgorithmInputsNotIntegrated"))
                    .FreezeRun(run.RunId, startContext.TrayId, startContext.ScenarioId, frozen.Public, startContext.ExpectedRecipeRef, selectedFLocation);
            }
            var control = coordinator.Control(receipt.RunId)!;
            var publicEpoch = readiness.Observe().ConnectionEpoch;
            async Task PauseAt(string boundary, RunState resume, DateTimeOffset? deadline = null)
            {
                if (pause is null) return;
                await pause.WaitAsync(run.RunId, resume, boundary, publicEpoch,
                    deadline, async (kind, state, ct) =>
                    { await run.SaveAsync(WriteKind.Audit, new { kind, boundary, sameRun = true }, state, cancellationToken: ct); },
                    cancellationToken);
            }
            await PauseAt("BeforePublicStart", RunState.Preparing);
            var ready = readiness.Check(frozen.Public, control);
            RuntimeDiagnostics.Record("StartupReadiness", ready.Ready ? "Ready" : "Blocked", receipt.RunId,
                new { ready.Reasons, ready.SafetyAssessment, ready.Observation }, warning: !ready.Ready);
            if (!ready.Ready)
            {
                var diagnostic = new StartupDiagnostic(ready.Reasons, ready.SafetyAssessment,
                    "StartupReadiness", "BlockedNoDeviceAction", ready.Observation.ConnectionEpoch,
                    ready.Observation.Identity?.SampleEndedUtc, ready.Observation.ExecutionOrigin,
                    ready.Observation, ready.Observation.DiagnosticEvidenceReference);
                await run.SaveAsync(WriteKind.Audit, new { ready.Reasons, ready.SafetyAssessment,
                    semanticObservation = ready.Observation, disposition = "BlockedNoDeviceAction",
                    noStartOrMotionRequest = true }, RunState.Blocked);
                await Stage(receipt.RunId, RunState.Blocked, "StartupNotReady");
                await coordinator.SetAsync(receipt.RunId, s => s with
                {
                    StartupDiagnostic = diagnostic, ObservedRevision = s.ObservedRevision + 1
                });
                diagnostics?.Record(request.RequestId, receipt.CommandId, receipt.RunId,
                    "StartupReadiness", ready.SafetyAssessment + ":" + string.Join(',', ready.Reasons),
                    "BlockedNoDeviceAction", ready.Observation.ConnectionEpoch,
                    ready.Observation.Identity?.SampleEndedUtc, ready.Observation.ExecutionOrigin.Provider.ToString());
                return;
            }
            var started = await startPreparation.ExecuteAsync(run, control,
                state => Stage(receipt.RunId, state), cancellationToken);
            await PauseAt("StartReadySavedBefore3D", RunState.Running3D);
            var initial3d = await threeD.ExecuteAsync(run, control, cancellationToken);
            started = run.RecoveredStart ?? started;
            TrayAnomalyDecisionProjection? initialDecision = null;
            if (initial3d.Observation.Slots.Any(s => s.Presence == TrayPresence.Present && s.Pose == TrayPose.Abnormal))
                initialDecision = await (anomalyDecisions ?? throw new InvalidOperationException("TrayAnomalyDecisionUnavailable"))
                    .WaitAsync(initial3d.Observation, control, async (decision, ct) => {
                        var saved = await run.SaveAsync(WriteKind.Audit, new { kind = "TrayAnomalyDecision", decision }, cancellationToken: ct);
                        return $"write://{saved.WriteId:D}";
                    }, cancellationToken);
            if (initial3d.Observation.IsEmptyTray || initialDecision?.Choice == "ManualIntervention")
            {
                var reason = initial3d.Observation.IsEmptyTray ? TrayEndReason.EmptyTray : TrayEndReason.ManualIntervention;
                await EndEarlyAsync(run, control, initial3d, reason,
                    initialDecision?.EvidenceReference ?? $"write://{run.InitialObservationWriteId:D}", cancellationToken);
                return;
            }
            await PauseAt("ThreeDSavedAndResetBeforeF", RunState.RunningF);
            await Stage(receipt.RunId, RunState.RunningF);
            var f = await fScan.ExecuteAsync(run, control, cancellationToken);
            await PauseAt("FSavedAndResetBeforeBinding", RunState.RunningF);
            var gate = ThreeDAndFRecipeGate.Evaluate(true, true, f.Algorithm.State, f.Code);
            if (!gate.CanLoadAndBind)
            {
                control.MarkSafetyFault();
                await run.SaveAsync(WriteKind.Audit, new
                {
                    kind = "FRecipeGateRejected", gate.Code,
                    f.Algorithm.State, f.Code.RecognitionState, f.Code.ParseState,
                    noRecipePlan = true, noRecipeBinding = true, noHandoff = true
                }, RunState.Blocked, cancellationToken: CancellationToken.None);
                throw new InvalidOperationException(gate.Code);
            }
            var recipeCode = f.Code.TrayIdentifier ?? f.Code.PrimaryCode!;
            var expectedRef = startContext.ExpectedRecipeRef;
            var selection = expectedRef is null ? null : new RecipeSelectionIntent(expectedRef.RecipeId,
                expectedRef.Version, expectedRef.CatalogDigest);
            var matched = RecipeMatcher.Match(recipeCatalog.GetSnapshot(), recipeCode, selection,
                startContext.ScenarioId, run.Config.Public.Purpose);
            if (matched.Status != RecipeMatchStatus.Matched || matched.Definition is null)
            {
                await run.SaveAsync(WriteKind.Audit, new { kind = "RecipeFMatchRejected", matched.Status,
                    matched.Reason, actualFCode = recipeCode, selection, noProductMotion = true },
                    RunState.Blocked, cancellationToken: cancellationToken);
                throw new InvalidOperationException(matched.Reason ?? "RecipeFMatchRejected");
            }
            var selectedForF = matched.Definition;
            var observedSlots = initial3d.Observation.Slots;
            var bindings = selectedForF.UnitKind == "looseGroup"
                ? selectedForF.Positions.SelectMany(p => p.Members.Select(m => (m.CellId, m.PhysicalSlotIndex))).ToArray()
                : selectedForF.Positions.Select(p => (p.CellId, p.PhysicalSlotIndex)).ToArray();
            if (bindings.Any(b => !observedSlots.Any(s => s.PhysicalSlotIndex == b.PhysicalSlotIndex && s.CellId == b.CellId && s.Region == "OK")) ||
                observedSlots.Any(s => s.Presence == TrayPresence.Present && !bindings.Any(b => b.PhysicalSlotIndex == s.PhysicalSlotIndex && b.CellId == s.CellId)))
                throw new InvalidOperationException("RecipePhysicalSlotObservationIncompleteOrUnknown");
            var occupiedSlots = selectedForF.Positions.Where(p => observedSlots.Any(s => s.Presence == TrayPresence.Present &&
                (selectedForF.UnitKind == "looseGroup" ? p.Members.Any(m => m.PhysicalSlotIndex == s.PhysicalSlotIndex) : p.PhysicalSlotIndex == s.PhysicalSlotIndex)))
                .Select(p => p.SlotId).ToArray();
            var admission = RecipeAdmission.Evaluate(selectedForF, occupiedSlots, run.Config.Public.Purpose);
            if (!admission.Eligible) throw new InvalidOperationException("RecipeRestricted:" + admission.Reason);
            RuntimeDiagnostics.Record("RecipePlan", "Loading", run.RunId,
                new { startContext.ScenarioId, occupiedCount = occupiedSlots.Length, admission,
                    initial3d.Observation.ObservationId, run.InitialObservationWriteId });
            var plan = ThreeDAndFRecipeGate.ExecuteAfterGate(gate, () =>
                RecipeRunPlanner.BuildExecutable(selectedForF, startContext.TrayId.ToString("D"), occupiedSlots, run.Config.Public.Purpose));
            run.ExecutionInputs = RecipeAdmission.Freeze(run.RunId, startContext.TrayId, plan,
                capabilities, executionCosts.Resolve(frozen), run.Config.Public.Purpose, run.Config.Public.Algorithms.TrayPose);
            if (commissioning && realAlgorithmConfiguration is null) commissioningInputs!.BindRecipe(run.RunId, run.ExecutionInputs);
            var planRevision = RecipePlanRevision.Compute(plan);
            var routeDeadlines = startContext.ExpectedRecipeRef is null ? null :
                RecipeExecutionBudget.Freeze(plan, budget.Value, clock.GetUtcNow(), run.ExecutionInputs.CostProfile);
            if (routeDeadlines is not null)
                await run.SaveAsync(WriteKind.Audit, new
                {
                    kind = "RecipeExecutionDeadlinesFrozen", planRevision,
                    plan.RecipeId, plan.RecipeVersion, plan.CatalogDigest,
                    budgetId = budget.Value.Id, budgetVersion = budget.Value.Version,
                    routeDeadlines
                }, cancellationToken: cancellationToken);
            using var bindingCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, control.Cancellation);
            if (beforeRecipeBindingForTest is not null)
                await beforeRecipeBindingForTest(run.RunId, routeDeadlines, run.Config.Budget.BusinessMs.CriticalSave, bindingCancellation.Token);
            var bindingId = Guid.NewGuid();
            RecipeApplicationOutcome binding;

            RuntimeDiagnostics.Record("RecipeBinding", "Requested", run.RunId, new { bindingId, planRevision, purpose = "FUniqueRecipeApplication" });
            try
            {
                binding = await new RecipeApplicationCoordinator().ExecuteAsync(run, plan, planRevision,
                bindingId, f.Move.DeviceEpoch, plan.NgCapacity, plan.PendingCapacity,
                routeDeadlines is null ? [] : [routeDeadlines.DetectionDeadlineUtc, routeDeadlines.UnloadDeadlineUtc, routeDeadlines.SortingDeadlineUtc],
                async ct =>
                {
                    await Stage(receipt.RunId, RunState.SavingHandoff);
                    return await completion.ExecuteAsync(run, control, started, initial3d, f, ct);
                }, bindingCancellation.Token, routeDeadlines is null ? [] : [
                    new("Detection", routeDeadlines.StartedUtc, routeDeadlines.DetectionDeadlineUtc),
                    new("Sorting", routeDeadlines.StartedUtc, routeDeadlines.SortingDeadlineUtc),
                    new("UnloadPreparation", routeDeadlines.StartedUtc, routeDeadlines.UnloadDeadlineUtc)]);

                RuntimeDiagnostics.Record("RecipeBinding", "Confirmed", run.RunId, new { bindingId, planRevision,
                    binding.Receipt, basis = "AllRequiredCurrentReceipts" });
            }
            catch { throw; }
            // A controlled Test scheduler can delay this existing boundary. It
            // cannot provide a receipt, change a deadline or authorize continuation.
            if (beforeRecipeContinuationForTest is not null)
                await beforeRecipeContinuationForTest(run.RunId, routeDeadlines, bindingCancellation.Token);
            if (control.AdmissionClosed || run.RecipeApplicationCancellation.IsCancellationRequested || !binding.Receipt.WasCompletedInWindow)
                throw new InvalidOperationException("RecipeApplicationAuthorizationClosed");
            var handoff = binding.Handoff ?? throw new InvalidOperationException("RequiredHandoffMissing");
            if (routeDeadlines is not null && clock.GetUtcNow() >= routeDeadlines.DetectionDeadlineUtc)
                throw new TimeoutException("DetectionDeadlineExpiredBeforeHandoffConsumer");
            await coordinator.SetAsync(receipt.RunId, s => s with
            {
                RecipeState = "Bound",
                PlanRevision = planRevision,
                RecipeExecution = new RecipeExecutionProjection(plan.RecipeId, plan.RecipeVersion,
                    plan.CatalogDigest, planRevision, plan.ScenarioId,
                    selectedForF.Route),
                RecipeBindingReference = $"recipe-binding://{bindingId:D}",
                ObservedRevision = s.ObservedRevision + 1
            });
            var detectionRequest = await handoffConsumer.CreateDetectionRequestAsync(
                handoff.Identity.RunId, handoff.Identity.TrayId, handoff.PlanRevision,
                Guid.NewGuid(), f.Move.DeviceEpoch,
                routeDeadlines?.DetectionDeadlineUtc ??
                    StageRetryPolicy.FreezeDeadline(clock.GetUtcNow()),
                $"handoff:{handoff.HandoffId:N}:detection", run.RecipePlan,
                cancellationToken, routeDeadlines?.StartedUtc,
                motionConfiguration: run.Config.Public,
                recipeApplicationReceipt: binding.Receipt);
            detectionRequest = detectionRequest with { CriticalSaveBudgetMs = run.Config.Budget.BusinessMs.CriticalSave,
                FrozenBusinessDurations = run.Config.Budget.BusinessMs, AlgorithmConfiguration = run.Config.RealAlgorithm,
                AlgorithmConfigurationDigest = run.Config.RealAlgorithmDigest };
            RuntimeDiagnostics.Record("DetectionHandoff", "RequestPrepared", run.RunId,
                new { operationId = detectionRequest.OperationId, planRevision,
                    stageStartedAtUtc = detectionRequest.StageStartedAtUtc,
                    stageDeadlineAtUtc = detectionRequest.DeadlineUtc,
                    bindingId = binding.Receipt.BindingId, bindingWindow = binding.Receipt.Window });
            await coordinator.SetAsync(receipt.RunId, s => s.Next(RunState.HandoffReady) with
            {
                FinalOutcome = TerminalOutcome.None,
                Handoff = HandoffState.Ready,
                Save = SaveState.Committed, PersistedRevision = run.PersistedRevision,
                Action = ActionState.Completed, Capture = CaptureState.MediaTaken,
                Algorithm = f.Algorithm.State,
                RecipeState = "Bound", PlanRevision = handoff.PlanRevision,
                RecipeBindingReference = handoff.RecipeBindingReference,
                HandoffId = handoff.HandoffId, WholeTaskState = "HandoffReady",
                Events = [..s.Events, "PublicHandoffV2Committed",
                    $"DetectionRequestPrepared:{detectionRequest.OperationId:N}"]
            });
            {
                await Stage(receipt.RunId, RunState.Detection);
                var workflow = await wholeTrayWorkflow.ExecuteThreeStagesAsync(new WholeTrayWorkflowRequest(
                    new ThreeStageExecutionRequest(detectionRequest, plan, Guid.NewGuid(), Guid.NewGuid(),
                        run.Config.Public.Motion.Points.Unload,
                        run.Config.Public.Motion.PositionTolerance,
                        run.Config.Public.Purpose, run.Config.SnapshotId, routeDeadlines),
                    Guid.NewGuid(), $"{detectionRequest.IdempotencyKey}:whole-tray"), cancellationToken);
                if (workflow.ThreeStages is { Status: ThreeStageExecutionStatus.EarlyEndRequested, EndBasisReference: { } endBasis })
                {
                    await EndEarlyAsync(run, control, initial3d, TrayEndReason.ManualIntervention, endBasis, cancellationToken);
                    return;
                }
                if (workflow.Status == WholeTrayWorkflowStatus.ReadyForRemoval && workflow.WholeTray is not null)
                {
                    await coordinator.SetAsync(receipt.RunId, s => s.Next(RunState.ReadyForRemoval) with
                    {
                        WholeTaskState = "ReadyForRemoval",
                        WholeTrayCompletionId = workflow.WholeTray.Reference.CompletionId,
                        ReadyForRemovalSourceMatrixId = workflow.WholeTray.SourceMatrix.MatrixId,
                        TrayEndReason = workflow.WholeTray.Reference.EndReason.ToString(),
                        InspectionCompleted = workflow.WholeTray.Reference.InspectionCompleted,
                        Events = [..s.Events, "DetectionCompleted", "UnloadPreparationCompleted",
                            "SortingCompleted", "WholeTrayCompleted"]
                    });
                    await PauseAt("WholeTraySavedBeforeRemovalAllowance", RunState.ReadyForRemoval);
                    if (control.CancelRequested || control.SafetyFault)
                        throw new InvalidOperationException("ManualRemovalAllowanceControlClosed");
                    var unlocked = await wholeTrayWorkflow.AllowManualRemovalAsync(new ManualRemovalAllowanceRequest(
                        workflow.WholeTray.Reference, Guid.NewGuid(), detectionRequest.ConnectionEpoch,
                        clock.GetUtcNow() + T050RetryPolicy.StageTimeout,
                        $"{detectionRequest.IdempotencyKey}:removal-allowance", run.Config.SnapshotId, run.SessionId), cancellationToken);
                    if (unlocked.Status == WholeTrayWorkflowStatus.ManualRemovalAllowed)
                    {
                        await coordinator.SetAsync(receipt.RunId, s => s.Next(RunState.AwaitingManualRemoval) with
                        {
                            WholeTaskState = "AwaitingManualTrayRemoval",
                            ManualRemovalAllowedEventId = unlocked.ManualRemovalAllowedEventId,
                            Events = [..s.Events, "ManualRemovalAllowed", "AwaitingManualTrayRemoval"]
                        });
                    }
                    else
                    {
                        if (recovery is not null && run.Config.Public.Purpose == "Test")
                            await recovery.CloseWorkflowFaultAsync(run, control, "ManualRemovalAllowance", unlocked.ErrorCode ?? "ManualRemovalAllowanceUnconfirmed", cancellationToken);
                        await Stage(receipt.RunId, RunState.RecoveryRequired, unlocked.ErrorCode ?? "ManualRemovalAllowanceUnconfirmed");
                    }
                }
                else if (workflow.Status == WholeTrayWorkflowStatus.UnknownHeld ||
                    workflow.ThreeStages?.Status == ThreeStageExecutionStatus.UnknownHeld)
                {
                    if (recovery is not null && run.Config.Public.Purpose == "Test")
                        await recovery.CloseWorkflowFaultAsync(run, control, "Workflow", workflow.ErrorCode ?? "UnknownHeld", cancellationToken);
                    await Stage(receipt.RunId, RunState.RecoveryRequired, workflow.ErrorCode ?? "UnknownHeld");
                }
                else
                {
                    await Stage(receipt.RunId, RunState.Blocked,
                        workflow.ErrorCode ?? workflow.Status.ToString());
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            diagnostics?.Record(request.RequestId, receipt.CommandId, receipt.RunId,
                "HostShutdown", "StopUnconfirmed", "RecoveryRequiredNoAutomaticRelease");
            var disposition = "HostShutdown:AdmissionClosed;PhysicalStopUnconfirmed;NoAutomaticRelease";
            if (execution is not null)
            {
                try
                {
                    await execution.SaveAsync(WriteKind.Audit, new
                    {
                        disposition = "ShutdownRecoveryRequired",
                        admissionClosed = true,
                        physicalStopConfirmed = false,
                        physicalOccupancyReleased = false
                    }, RunState.RecoveryRequired, cancellationToken: CancellationToken.None);
                }
                catch (SaveGateException saveError)
                {
                    disposition += ";Save=" + saveError.Code + ":" + saveError.WriteId;
                }
            }
            await Stage(receipt.RunId, RunState.RecoveryRequired, disposition);
        }
        catch (ConfigurationException error)
        {
            diagnostics?.Record(request.RequestId, receipt.CommandId, receipt.RunId,
                "ConfigurationLoad", error.Code, "ConfigurationBlocked", exception: error);
            if (initialRevision > 0)
            {
                try { await SaveConfigurationFailureAsync(receipt.RunId, initialRevision, error.Code); }
                catch (SaveGateException saveError)
                {
                    await Stage(receipt.RunId, RunState.Blocked,
                        error.Code + ":" + saveError.Code + ":" + saveError.WriteId);
                    return;
                }
            }
            await Stage(receipt.RunId, RunState.ConfigurationBlocked, error.Code);
        }
        catch (SaveGateException error)
        {
            diagnostics?.Record(request.RequestId, receipt.CommandId, receipt.RunId,
                "CriticalSave", error.Code, "BlockedOrCommitUnknown", exception: error);
            commands.UpdateDurability(receipt.CommandId,
                error.Code == "CommitUnknown" ? "CommitUnknown" : "Failed");
            await Stage(receipt.RunId, RunState.Blocked, error.Code + ":" + error.WriteId);
        }
        catch (FaultRunClosedException)
        {
            await Stage(receipt.RunId, RunState.RecoveryRequired,
                coordinator.Query(receipt.RunId)?.ErrorCode ?? "FaultRequiresNewRun");
        }
        catch (Exception error)
        {
            var observed = readiness.Observe();
            var before = coordinator.Query(receipt.RunId);
            var assessment = !observed.HasReliableObservation ? "Unconfirmed" :
                observed.SafetyAssessment != SafetyAssessment.Clear ? "ExplicitUnsafe" : "Other";
            var reason = !observed.HasReliableObservation ? observed.ReasonCodes.FirstOrDefault() ?? "DeviceObservationUnavailable" :
                observed.SafetyAssessment != SafetyAssessment.Clear ? "SafetyInterlockDenied" : error.GetType().Name;
            var disposition = before?.Action == ActionState.Unknown
                ? "UnknownHeldNoAutomaticRetry" : "BlockedNoAutomaticRetry";
            var stopStage = before?.State.ToString() ?? "Execution";
            diagnostics?.Record(request.RequestId, receipt.CommandId, receipt.RunId,
                stopStage, assessment + ":" + reason, disposition,
                observed.ConnectionEpoch, observed.Identity?.SampleEndedUtc, observed.ExecutionOrigin.Provider.ToString(), error);
            var publicCode = error is InvalidOperationException &&
                error.Message is "FCodeNotUniqueAndParsed" or "ExpectedRecipeFMismatch"
                ? "InvalidOperationException:" + error.Message : error.GetType().Name;
            await Stage(receipt.RunId, RunState.Blocked, publicCode);
            await coordinator.SetAsync(receipt.RunId, s => s with
            {
                StartupDiagnostic = new StartupDiagnostic([reason], assessment, stopStage,
                    disposition, observed.ConnectionEpoch, observed.Identity?.SampleEndedUtc,
                    observed.ExecutionOrigin, observed, observed.DiagnosticEvidenceReference),
                ObservedRevision = s.ObservedRevision + 1
            });
        }
        finally
        {
            commissioningInputs?.ReleaseRun(receipt.RunId);
            recovery?.ExecutionExited(receipt.RunId);
            var snapshot = coordinator.Query(receipt.RunId);
            RuntimeDiagnostics.Record("RunExecution", "Exited", receipt.RunId,
                new { request.RequestId, receipt.CommandId, state = snapshot?.State.ToString(),
                    snapshot?.ErrorCode, snapshot?.WholeTaskState,
                    disposition = "StateIsAuthoritative_NotAutomaticCompletion" },
                warning: snapshot?.ErrorCode is not null);
        }
    }

    private sealed class ActiveRun
    {
        public CancellationTokenSource Cancellation { get; } = new();
        public Task? Task { get; set; }
    }

    private async Task EndEarlyAsync(RunExecution run, ControlLatch control, ThreeDEvidence initial,
        TrayEndReason reason, string basis, CancellationToken token)
    {
        if (control.AdmissionClosed) throw new InvalidOperationException("EarlyEndControlClosed");
        var context = StartRunContextParser.Parse(run.ContextJson);
        var capture = run.InitialThreeDCapture ?? throw new InvalidOperationException("Initial3DCaptureEvidenceMissing");
        ComponentEvidence Evidence(ComponentKind kind, ComponentExecutionOrigin origin, string reference, string digest) =>
            new(kind, ComponentEvidenceState.Verified, origin.Source, origin.Quality, origin.VersionRef, [reference], clock.GetUtcNow(), digest);
        var captureReference = $"write://{run.InitialThreeDCaptureWriteId:D}";
        var observationReference = $"write://{run.InitialObservationWriteId:D}";
        var preparation = new[] {
            Evidence(ComponentKind.Camera, capture.CameraOrigin, captureReference, initial.Media.RelativeKey),
            Evidence(ComponentKind.Light, capture.LightOrigin, captureReference, initial.Media.RelativeKey),
            Evidence(ComponentKind.Algorithm, initial.Observation.Source, observationReference, initial.Observation.ObservationId.ToString("N")) };
        var now = clock.GetUtcNow();
        // Same approved move/save allowances as normal unload, without a fictitious plan.
        var cost = executionCosts.Resolve(run.Config);
        var milliseconds = checked(run.Config.Budget.BusinessMs.XyCompletion + cost.UnloadDeviceAllowanceMs +
            3L * run.Config.Budget.BusinessMs.CriticalSave);
        var revision = run.PlanRevision ?? "public-snapshot:" + run.Config.SnapshotId;
        var epoch = readiness.Observe().ConnectionEpoch;
        var request = new PublicUnloadRequest(run.RunId, context.TrayId, Guid.Parse(context.StationId),
            Guid.Parse(context.LineId), run.SessionId, epoch, run.Config.SnapshotId, revision, Guid.NewGuid(),
            run.Config.Public.Motion.Points.Unload ?? throw new InvalidOperationException("ManualLoadingPositionMissing"),
            run.Config.Public.Motion.PositionTolerance, run.Config.Public.Purpose,
            Gaode.Application.Timing.ActionWindows.FromUtc(clock, now, now.AddMilliseconds(milliseconds), run.ClockId),
            $"public-end:{run.RunId:N}");
        await Stage(run.RunId, RunState.UnloadPreparation);
        var ended = await wholeTrayWorkflow.ExecuteEarlyEndAsync(request, reason, basis, preparation, run.PlanRevision, token);
        if (ended is not { Status: WholeTrayWorkflowStatus.ReadyForRemoval, WholeTray: { } whole })
            throw new InvalidOperationException(ended.ErrorCode ?? "PublicUnloadUnconfirmed");
        if (control.AdmissionClosed) throw new InvalidOperationException("ManualRemovalAllowanceControlClosed");
        var allowance = await wholeTrayWorkflow.AllowManualRemovalAsync(new(whole.Reference, Guid.NewGuid(), epoch,
            clock.GetUtcNow().AddMilliseconds(run.Config.Budget.BusinessMs.CriticalSave),
            $"public-end:{run.RunId:N}:allow", run.Config.SnapshotId, run.SessionId), token);
        if (allowance.Status != WholeTrayWorkflowStatus.ManualRemovalAllowed)
            throw new InvalidOperationException(allowance.ErrorCode ?? "ManualRemovalAllowanceUnconfirmed");
        await coordinator.SetAsync(run.RunId, s => s.Next(RunState.AwaitingManualRemoval) with {
            WholeTaskState = "AwaitingManualTrayRemoval", WholeTrayCompletionId = whole.Reference.CompletionId,
            ReadyForRemovalSourceMatrixId = whole.SourceMatrix.MatrixId, ManualRemovalAllowedEventId = allowance.ManualRemovalAllowedEventId,
            TrayEndReason = reason.ToString(), InspectionCompleted = false,
            Events = [..s.Events, reason == TrayEndReason.EmptyTray ? "料盘为空" : "ManualIntervention",
                "PublicUnloadCompleted", "ManualRemovalAllowed"] });
    }

    private async Task<long> SaveInitialContextAsync(StartReceipt receipt, StartPublicRequest request, string subject)
    {
        var payload = new RunCreatedPayload(receipt.CommandId, request.RequestId, subject,
            request.ContextJson, "NotFrozen:" + request.PublicConfigRef.Id + "/" + request.PublicConfigRef.Version,
            "NotFrozen:" + request.BudgetRef.Id + "/" + request.BudgetRef.Version,
            "NotFrozen:" + request.SimulationRef.Id + "/" + request.SimulationRef.Version,
            "PendingFreeze", "", "", "") { CanonicalRequest = JsonSerializer.Serialize(request,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)) };
        var receiptResult = await SubmitInitialAsync(receipt.RunId, 0, WriteKind.RunCreated, payload);
        return receiptResult.CommittedRevision!.Value;
    }

    private Task SaveConfigurationFailureAsync(Guid runId, long revision, string code) =>
        SubmitInitialAsync(runId, revision, WriteKind.Audit,
            new { code, disposition = "ConfigurationBlocked", noPlcRequest = true },
            RunState.ConfigurationBlocked);

    private async Task<CommitReceipt> SubmitInitialAsync(Guid runId, long revision,
        WriteKind kind, object payload, RunState? state = null)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        var writeId = Guid.NewGuid();
        QueuedWrite queued;
        try { queued = writer.SubmitCritical(new WriteBatch(writeId, runId, revision,
            kind, json, digest, state)); }
        catch (Exception ex)
        {
            RuntimeDiagnostics.Record("InitialSave", "EnqueueFailed", runId,
                new { writeId, revision, kind = kind.ToString() }, ex);
            throw new SaveGateException("SaveEnqueueFailed", writeId, ex.Message);
        }
        RuntimeDiagnostics.Record("InitialSave", "Queued", runId,
            new { writeId, revision, kind = kind.ToString(), timeoutMs = bootstrapBudget.BusinessMs.CriticalSave });
        CommitReceipt result;
        try { result = await queued.Completion.WaitAsync(
            TimeSpan.FromMilliseconds(bootstrapBudget.BusinessMs.CriticalSave), clock); }
        catch (TimeoutException) { throw new SaveGateException("CommitUnknown", writeId, "初始上下文提交未知"); }
        if (result.State != CommitState.Committed || result.CommittedRevision is null)
            throw new SaveGateException(result.State.ToString(), writeId,
                result.ErrorCode ?? "初始上下文未提交");
        return result;
    }

    private async Task Stage(Guid runId, RunState next, string? error = null)
    {
        await coordinator.SetAsync(runId, snapshot => snapshot.Next(next) with
        {
            ErrorCode = error, Events = [..snapshot.Events, next.ToString()]
        });
    }
}
