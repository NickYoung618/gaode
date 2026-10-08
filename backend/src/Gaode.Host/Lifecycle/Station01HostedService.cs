using Gaode.Application.Ports;
using Gaode.Application.Station01;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Persistence;
using Gaode.Infrastructure.Media;
using Gaode.Application.Motion;
using Gaode.Application.Algorithms;
using Gaode.Application.Workflow;
using Microsoft.EntityFrameworkCore;

namespace Gaode.Host.Lifecycle;

public sealed record Station01ShutdownSnapshot(bool AdmissionClosed, bool FlowsStopped,
    bool WritesDrained, bool ResourcesDrained, bool PhysicalStopConfirmed,
    bool PhysicalOccupancyReleased, bool MotionUnknown, int RemainingWrites,
    int RemainingMediaLeases, string Disposition,
    bool ConsumerStopped = false, IReadOnlyDictionary<string, string>? Steps = null,
    bool RunsStopped = false, bool ConsumerStopRequested = false,
    int RemainingAlgorithmExecutions = 0,
    IReadOnlyList<AlgorithmExecutionEvidence>? AlgorithmExecutions = null);

public sealed class Station01HostedService(StoreAccessGuard guard, TraceWriter writer,
    ITraceQuery query, Station01Coordinator coordinator, CommandRegistry commands,
    StartPublicPreparation starts, MediaStore media, ResourceLease motionLease,
    ILogger<Station01HostedService> logger, AlgorithmRuntime algorithms,
    DbContextOptions<Station01DbContext> databaseOptions,
    ThreeStageRecoveryService threeStageRecovery) : IHostedService
{
    private readonly SemaphoreSlim _stopGate = new(1, 1);
    private Task? persistenceInitialization;
    public Station01ShutdownSnapshot? LastShutdown { get; private set; }
    public Task StartAsync(CancellationToken cancellationToken) => InitializePersistenceAsync(cancellationToken);

    // Program completes these real reads before the device hosted services start.
    // The Host's later StartAsync reuses the same initialization, preserving stop order.
    public Task InitializePersistenceAsync(CancellationToken cancellationToken) =>
        persistenceInitialization ??= RestorePersistenceAsync(cancellationToken);

    private async Task RestorePersistenceAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("StartupPersistence category=Lifecycle phase=Started deviceServicesStarted=false");
        _ = guard.Root;
        _ = writer;
        await new CameraCaptureJournal(databaseOptions).RestoreAsync(media, cancellationToken);
        coordinator.Start();
        await using (var startupDb = new Station01DbContext(databaseOptions))
        {
            foreach (var command in await startupDb.Commands.AsNoTracking().Where(x => x.Kind == "Start" && x.ReceiptState == "Committed").ToListAsync(cancellationToken))
                commands.Restore(command.SubjectId, command.RequestId, command.PayloadDigest, command.CommandId, command.RunId);
        }
        var unfinished = await query.GetUnfinishedRunsAsync(cancellationToken);
        foreach (var run in unfinished)
        {
            var retainedWrites = await query.GetWritesAsync(run.RunId, cancellationToken);
            var linked = retainedWrites.Where(x => x.State == CommitState.Committed).Any(x =>
            {
                using var doc = System.Text.Json.JsonDocument.Parse(x.PayloadJson);
                return doc.RootElement.TryGetProperty("kind", out var kind) && kind.GetString() == "RecoveryNewRunLinked";
            });
            // This old fault is historical; the linked new run is independently reconciled below.
            // No replay, new physical action or deletion is performed during startup.
            if (linked) continue;
            await using var db = new Station01DbContext(databaseOptions);
            var trays = await db.StageEvents.AsNoTracking().Where(x => x.RunId == run.RunId)
                .Select(x => x.TrayId).Distinct().ToListAsync(cancellationToken);
            foreach (var trayId in trays)
                await threeStageRecovery.RecoverCommittedAsync(run.RunId, trayId,
                    cancellationToken);
            commands.HoldRecoveredRun(run.RunId);
            coordinator.TryRegister(new RunSnapshot(run.RunId, run.RequestId, run.SubjectId,
                RunState.RecoveryRequired, run.Revision, run.Revision, TerminalOutcome.None,
                run.CancelRequested, ActionState.Unknown, CaptureState.Unknown,
                AlgorithmState.Error, SaveState.Committed, HandoffState.NotReady,
                null, null, null, ["RecoveryRequired:NoAutomaticReplay"]));
        }
        logger.LogInformation("Station01 Host就绪，未完成运行数={Count}，恢复状态不自动动作", unfinished.Count);
        logger.LogInformation("StartupPersistence category=Lifecycle phase=Completed unfinishedRuns={Count}; device startup may now proceed", unfinished.Count);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // Closing admission is unconditional, including an already exhausted host budget.
        var unfinished = coordinator.BeginShutdown();
        var motionUnknown = motionLease.PreserveForShutdown();
        if (!_stopGate.Wait(0)) await _stopGate.WaitAsync(cancellationToken);
        try
        {
            if (LastShutdown is not null) return;
            var flowsStopped = false;
            var writesDrained = false;
            var resourcesDrained = false;
            var steps = new Dictionary<string, string>();
            async Task<bool> Attempt(string name, Func<Task> operation)
            {
                try
                {
                    // Invoke even when the shared budget is exhausted: closing admission
                    // and signalling consumer cancellation must still happen.
                    await operation().WaitAsync(cancellationToken);
                    steps[name] = "Completed";
                    return true;
                }
                catch (OperationCanceledException) { steps[name] = "BudgetExpired;Unknown"; }
                catch (Exception error) { steps[name] = "Failed:" + error.GetType().Name; }
                return false;
            }
            try
            {
                flowsStopped = await Attempt("Runs", () => starts.StopAsync(cancellationToken));
                writesDrained = await Attempt("Writes", () => writer.WaitForIdleAsync(cancellationToken));
                await Attempt("Resources", async () =>
                {
                    var mediaDrain = media.WaitForIdleAsync(cancellationToken);
                    await Task.WhenAll(mediaDrain, algorithms.WaitForIdleAsync(cancellationToken));
                    resourcesDrained = await mediaDrain;
                    if (!resourcesDrained) throw new OperationCanceledException(cancellationToken);
                });
            }
            finally { await Attempt("Consumer", () => coordinator.StopConsumerAsync(cancellationToken)); }
            LastShutdown = new(true, flowsStopped && coordinator.ConsumerStopped, writesDrained, resourcesDrained,
                false, false, motionUnknown, writer.PendingCount, media.ActiveLeases,
                unfinished.Count == 0 ? "StoppedWithoutActiveRun;PhysicalOccupancyUnchanged"
                    : "RecoveryRequired;NoAutomaticReplay;PhysicalStopUnconfirmed",
                coordinator.ConsumerStopped, steps, flowsStopped, coordinator.ConsumerStopRequested,
                algorithms.ActiveExecutions, algorithms.ExecutionEvidence);
            logger.LogInformation("Station01 Host关闭：Flows={Flows} Runs={Runs} Consumer={Consumer} ConsumerStopRequested={ConsumerStopRequested} Writes={Writes} Resources={Resources} AlgorithmsRemaining={AlgorithmsRemaining}，物理停止={PhysicalStop}，占用释放={Released}",
                LastShutdown.FlowsStopped, LastShutdown.RunsStopped, LastShutdown.ConsumerStopped,
                LastShutdown.ConsumerStopRequested, LastShutdown.WritesDrained, LastShutdown.ResourcesDrained,
                LastShutdown.RemainingAlgorithmExecutions, false, false);
        }
        finally { _stopGate.Release(); }
    }
}
