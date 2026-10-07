using System.Collections.Concurrent;
using System.Threading.Channels;
using Gaode.Application.Station01;
using Gaode.Application.Ports;
using Gaode.Application.Workflow;
using Gaode.Infrastructure.Persistence;
using System.Text.Json;
using Gaode.Domain.Station01;
using Microsoft.AspNetCore.SignalR;

namespace Gaode.Host.Api;

public sealed class Station01NotificationService : BackgroundService
{
    private readonly Station01Coordinator _coordinator;
    private readonly IHubContext<Station01Hub> _hub;
    private readonly Channel<Guid> _updates;
    private readonly ITraceQuery _traces;
    private readonly IStageEventStore _stages;
    private readonly TraceWriter _writer;
    private readonly StageEventStore _stageWriter;
    private readonly ILogger<Station01NotificationService> _logger;
    private readonly ConcurrentDictionary<Guid, RunSnapshot> _latest = new();
    private long _rejectedSnapshots;
    public long RejectedSnapshots => Interlocked.Read(ref _rejectedSnapshots);

    public Station01NotificationService(Station01Coordinator coordinator,
        IHubContext<Station01Hub> hub, ITraceQuery traces, IStageEventStore stages,
        TraceWriter writer, StageEventStore stageWriter, ILogger<Station01NotificationService> logger, int capacity = 64)
    {
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _coordinator = coordinator;
        _hub = hub;
        _traces = traces; _stages = stages; _writer = writer; _stageWriter = stageWriter;
        _logger = logger;
        _updates = Channel.CreateBounded<Guid>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
        _coordinator.SnapshotChanged += OnSnapshotChanged;
        _writer.RunCommitted += OnCommitted;
        _stageWriter.RunCommitted += OnCommitted;
    }

    private void OnSnapshotChanged(RunSnapshot snapshot)
        => Queue(snapshot.RunId);

    private void OnCommitted(Guid runId)
    {
        if (_coordinator.Query(runId) is not null) _ = AdvanceRevisionAsync(runId);
    }

    private async Task AdvanceRevisionAsync(Guid runId)
    {
        try { await _coordinator.SetAsync(runId, s => s with { ObservedRevision = s.ObservedRevision + 1 }); }
        catch (Exception error) { _logger.LogWarning(error, "Committed run notification failed for {RunId}; commit is unchanged, query required", runId); }
    }

    private void Queue(Guid runId)
    {
        if (!_updates.Writer.TryWrite(runId)) Interlocked.Increment(ref _rejectedSnapshots);
    }

    private NotificationEnvelope Project(RunSnapshot snapshot)
    {
        _latest.TryGetValue(snapshot.RunId, out var previous);
        _latest[snapshot.RunId] = snapshot;
        var changed = new List<string> { "revision", "state" };
        if (previous is not null && previous.PersistedRevision != snapshot.PersistedRevision) changed.Add("persistedRevision");
        if (previous is not null && previous.ErrorCode != snapshot.ErrorCode) changed.Add("errorCode");
        if (previous?.StartupDiagnostic != snapshot.StartupDiagnostic) changed.Add("startupDiagnostic");
        if (previous is not null && previous.Handoff != snapshot.Handoff) changed.Add("handoff");
        if (previous?.WholeTaskState != snapshot.WholeTaskState) changed.Add("wholeTaskState");
        if (previous?.WholeTrayCompletionId != snapshot.WholeTrayCompletionId)
            changed.Add("wholeTrayCompletionId");
        if (previous?.ReadyForRemovalSourceMatrixId != snapshot.ReadyForRemovalSourceMatrixId)
            changed.Add("readyForRemovalSourceMatrixId");
        if (previous?.ManualRemovalAllowedEventId != snapshot.ManualRemovalAllowedEventId)
            changed.Add("manualRemovalAllowedEventId");
        void Changed(string field, object? before, object? after)
        { if (JsonSerializer.Serialize(before) != JsonSerializer.Serialize(after)) changed.Add(field); }
        Changed("sortingState", previous?.SortingState, snapshot.SortingState);
        Changed("recipeSelection", previous?.RecipeSelection, snapshot.RecipeSelection);
        Changed("recipeExecution", previous?.RecipeExecution, snapshot.RecipeExecution);
        Changed("stage", previous?.RecipeExecution?.Stage, snapshot.RecipeExecution?.Stage);
        Changed("executionPhase", previous?.ExecutionPhase, snapshot.ExecutionPhase);
        Changed("slotStates", previous?.SlotStates, snapshot.SlotStates);
        Changed("abnormalPhysicalSlotIndices", previous?.AbnormalPhysicalSlotIndices, snapshot.AbnormalPhysicalSlotIndices);
        Changed("observationCoverage", previous?.ObservationCoverage, snapshot.ObservationCoverage);
        var eventType = snapshot.State == RunState.Completed &&
                        snapshot.WholeTaskState == "FinalUnloadCompletion"
            ? "FinalUnloadCompleted"
            : snapshot.ManualRemovalAllowedEventId is not null &&
              previous?.ManualRemovalAllowedEventId != snapshot.ManualRemovalAllowedEventId
                ? "ManualRemovalAllowed"
                : snapshot.WholeTrayCompletionId is not null &&
                  previous?.WholeTrayCompletionId != snapshot.WholeTrayCompletionId
                    ? "WholeTrayCompleted"
                    : snapshot.Handoff is HandoffState.Ready or HandoffState.ReadyWithLimitations
                        ? "HandoffReady"
                        : snapshot.ErrorCode is not null ? "DiagnosticChanged" : "StateChanged";
        return new NotificationEnvelope(eventType, "s01/notification/2.0",
            snapshot.RunId, snapshot.ObservedRevision, snapshot.PersistedRevision,
            changed, new(snapshot.ExecutionState, snapshot.WholeTaskState, snapshot.ErrorCode), DateTimeOffset.UtcNow);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var runId in _updates.Reader.ReadAllAsync(stoppingToken))
            {
                if (_coordinator.Query(runId) is not { } snapshot) continue;
                var current = await RuntimeObservationProjection.ReadAsync(snapshot, _traces, _stages, stoppingToken);
                var envelope = Project(current);
                await _hub.Clients.All.SendAsync(envelope.EventType, envelope, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    public override void Dispose()
    {
        _coordinator.SnapshotChanged -= OnSnapshotChanged;
        _writer.RunCommitted -= OnCommitted;
        _stageWriter.RunCommitted -= OnCommitted;
        _updates.Writer.TryComplete();
        base.Dispose();
    }
}
