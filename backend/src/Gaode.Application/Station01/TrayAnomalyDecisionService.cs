using System.Collections.Concurrent;
using Gaode.Domain.Station01;
using Gaode.Diagnostics;

namespace Gaode.Application.Station01;

public sealed class TrayAnomalyDecisionService(Station01Coordinator coordinator, TimeProvider clock)
{
    public const int DecisionWindowMs = 10_000;
    private readonly ConcurrentDictionary<(Guid Run, Guid Observation), Pending> decisions = new();

    public async Task<TrayAnomalyDecisionProjection> WaitAsync(TrayObservation observation, ControlLatch control,
        Func<TrayAnomalyDecisionProjection, CancellationToken, Task<string>> persist, CancellationToken token)
    {
        if (!observation.HasCompleteCoverage) throw new InvalidDataException("TrayObservationCoverageIncomplete");
        var items = observation.Slots.Where(s => s.Presence == TrayPresence.Present && s.Pose == TrayPose.Abnormal)
            .Select(s => new TrayAnomalyItem(s.PhysicalSlotIndex, s.CellId!, s.Region!, s.Row!.Value,
                s.Column!.Value, "3D姿态异常", s.Reason ?? "3D姿态异常")).ToArray();
        if (items.Length == 0) throw new InvalidOperationException("TrayAnomalyDecisionNotRequired");
        var now = clock.GetUtcNow();
        var pending = new Pending(new(Guid.NewGuid(), observation.RunId, observation.ObservationId,
            observation.CheckRound, now, now.AddMilliseconds(DecisionWindowMs), items, "Pending"), control);
        if (!decisions.TryAdd((observation.RunId, observation.ObservationId), pending))
        {
            var existing = await decisions[(observation.RunId, observation.ObservationId)].Committed.Task.WaitAsync(token);
            if (existing.State == "Closed" || control.AdmissionClosed) throw new InvalidOperationException("TrayAnomalyDecisionControlClosed");
            return existing;
        }
        void Closed() => pending.Selected.TrySetResult(("Closed", "Control", null));
        control.AdmissionClosedChanged += Closed;
        try
        {
            token.ThrowIfCancellationRequested();
            if (control.AdmissionClosed) Closed();
            var openedRef = await persist(pending.Projection, token);
            pending.Projection = pending.Projection with { EvidenceReference = openedRef };
            await Publish(pending.Projection);
            RuntimeDiagnostics.Record("TrayAnomaly", "AwaitingDecision", observation.RunId,
                new { pending.Projection.DecisionId, observation.ObservationId, observation.CheckRound,
                    pending.Projection.DeadlineUtc, items });
            var remaining = pending.Projection.DeadlineUtc - clock.GetUtcNow();
            using var wait = CancellationTokenSource.CreateLinkedTokenSource(token);
            var timeout = Task.Delay(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero, clock, wait.Token);
            if (await Task.WhenAny(pending.Selected.Task, timeout) == timeout)
                pending.Selected.TrySetResult(control.AdmissionClosed ? ("Closed", "Control", null) : ("Continue", "Timeout", null));
            var choice = await pending.Selected.Task.WaitAsync(token);
            wait.Cancel();
            if (control.AdmissionClosed) choice = ("Closed", "Control", null);
            var final = pending.Projection with { State = choice.Item1 == "Closed" ? "Closed" : "Decided",
                Choice = choice.Item1, ChoiceSource = choice.Item2, OperatorId = choice.Item3 };
            var committed = await persist(final, token);
            final = final with { EvidenceReference = committed };
            pending.Projection = final;
            await Publish(final);
            RuntimeDiagnostics.Record("TrayAnomaly", "DecisionCommitted", observation.RunId, final,
                warning: final.State == "Closed");
            pending.Committed.TrySetResult(final);
            if (final.State == "Closed" || control.AdmissionClosed)
                throw new InvalidOperationException("TrayAnomalyDecisionControlClosed");
            return final;
        }
        catch (Exception error)
        {
            pending.Committed.TrySetException(error);
            RuntimeDiagnostics.Record("TrayAnomaly", "DecisionFailed", observation.RunId,
                new { pending.Projection.DecisionId, pending.Projection.ObservationId }, error);
            throw;
        }
        finally { control.AdmissionClosedChanged -= Closed; }
    }

    public async Task<TrayAnomalyDecisionProjection> ChooseAsync(Guid runId, Guid decisionId,
        string choice, string operatorId, CancellationToken token)
    {
        if (choice is not ("Continue" or "ManualIntervention")) throw new ArgumentException("TrayAnomalyChoiceInvalid");
        var pending = decisions.Values.SingleOrDefault(p => p.Projection.RunId == runId && p.Projection.DecisionId == decisionId)
            ?? throw new InvalidOperationException("TrayAnomalyDecisionNotFound");
        if (pending.Control.AdmissionClosed) pending.Selected.TrySetResult(("Closed", "Control", null));
        else if (clock.GetUtcNow() >= pending.Projection.DeadlineUtc) pending.Selected.TrySetResult(("Continue", "Timeout", null));
        else pending.Selected.TrySetResult((choice, "Operator", operatorId));
        var result = await pending.Committed.Task.WaitAsync(token);
        if (pending.Control.AdmissionClosed || result.State != "Decided" || result.Choice != choice)
            throw new InvalidOperationException("TrayAnomalyDecisionAlreadyResolved");
        return result;
    }

    private Task<RunSnapshot> Publish(TrayAnomalyDecisionProjection decision) => coordinator.SetAsync(decision.RunId,
        s => s with { TrayAnomalyDecision = decision, ObservedRevision = s.ObservedRevision + 1 }, control: true);

    private sealed class Pending(TrayAnomalyDecisionProjection projection, ControlLatch control)
    {
        public TrayAnomalyDecisionProjection Projection = projection;
        public ControlLatch Control { get; } = control;
        public TaskCompletionSource<(string, string, string?)> Selected { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<TrayAnomalyDecisionProjection> Committed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
