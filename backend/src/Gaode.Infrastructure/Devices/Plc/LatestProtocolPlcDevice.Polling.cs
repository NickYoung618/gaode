using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Gaode.Domain.Station01;
using Gaode.Plc.Protocol;

namespace Gaode.Infrastructure.Devices.Plc;

public sealed partial class LatestProtocolPlcDevice
{
    private sealed record GroupObservation(string Group, long Version, long Epoch, SignalValues Values,
        long PlannedDue, long Enqueued, long Published)
    {
        private readonly Guid observationId = Guid.NewGuid();
        internal long Started => Values.Stamps.Values.Min(s => s.Sent);
        internal long Ended => Values.Stamps.Values.Max(s => s.Ended);
        internal long RoundStarted => Values.Stamps.Values.Where(s => s.Queued >= Enqueued).Select(s => s.Sent).DefaultIfEmpty(Started).Min();
        internal ObservationIdentity Identity => new(observationId, Epoch,
            Values.Stamps.Values.Min(s => s.StartedUtc), Values.Stamps.Values.Max(s => s.EndedUtc), DeviceReliability.Reliable);
    }
    private sealed class AcquisitionGroup(string name, SignalId[] fields)
    {
        internal readonly string Name = name;
        internal readonly SignalId[] Fields = fields;
        internal int Generation, Period;
        internal bool Enabled, Running, Demand, Fast;
        internal long Due, LastStarted, Version;
        internal GroupObservation? Latest;
        internal Task? Work;
        internal TaskCompletionSource Changed = NewSignal();
    }
    private readonly Dictionary<string, AcquisitionGroup> groups = new()
    {
        ["B"] = new("B", PreparedPlcReadPlans.Base), ["P"] = new("P", PreparedPlcReadPlans.Position),
        ["X"] = new("X", PreparedPlcReadPlans.Axes), ["F"] = new("F", [SignalId.FlipStatus]),
        ["U"] = new("U", PreparedPlcReadPlans.FlipClear), ["T"] = new("T", PreparedPlcReadPlans.Transfer),
        ["G"] = new("G", PreparedPlcReadPlans.Gripper), ["R"] = new("R", PreparedPlcReadPlans.Rotation)
    };
    private readonly Dictionary<SignalId, ushort[]> sampledWords = [];
    private readonly Dictionary<SignalId, PlcReadStamp> sampledStamps = [];
    private TaskCompletionSource samplingChanged = NewSignal();
    private ObservationIdentity? positionIdentity;
    private bool resetting;
    private bool acquisitionPaused;
    private (int, int, bool, bool, bool, bool, bool, bool)? measuredPolicy;
    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static long Milliseconds(int value) => checked((long)(value * (double)Stopwatch.Frequency / 1000d));
    private void WakeSampling()
    {
        lock (sync) { var old = samplingChanged; samplingChanged = NewSignal(); old.TrySetResult(); }
    }
    private bool ActiveSampling => pending is not null || auxiliary || resetting || unknown || stopRequested ||
        activeInspection is not null || inspectionTarget is not null || awaitingPutBack is not null ||
        observation.MotionStatus != "Idle";
    private void SetFeedback(string name, bool enabled, bool fast = false)
    {
        lock (sync)
        {
            var group = groups[name];
            if (group.Enabled != enabled || group.Fast != fast)
            {
                var continuing = group.Enabled && enabled;
                group.Generation++; group.Enabled = enabled; group.Fast = fast;
                group.Period = fast ? PlcAcquisitionPolicy.FirstStateMs : PlcAcquisitionPolicy.FeedbackMs;
                group.Due = continuing && group.LastStarted != 0 && !fast
                    ? group.LastStarted + Milliseconds(group.Period) : Stopwatch.GetTimestamp();
                if (!enabled) { group.Latest = null; group.Changed.TrySetResult(); group.Changed = NewSignal(); }
                if (name is "X" or "G") { groups["B"].Generation++; groups["B"].Due = Stopwatch.GetTimestamp(); }
                if (name == "T") { groups["P"].Generation++; groups["P"].Due = groups["P"].LastStarted + Milliseconds(groups["P"].Period); }
            }
        }
        WakeSampling();
    }
    private void RefreshPeriods()
    {
        var now = Stopwatch.GetTimestamp();
        var basePeriod = ActiveSampling ? PlcAcquisitionPolicy.ActiveBaseMs : PlcAcquisitionPolicy.IdleBaseMs;
        var positionPeriod = unknown || resetting || auxiliary || pending?.Move is not null || observation.MotionStatus != "Idle" || groups["X"].Enabled ||
            groups["F"].Enabled || groups["U"].Enabled ? PlcAcquisitionPolicy.MovingPositionMs : PlcAcquisitionPolicy.StaticPositionMs;
        foreach (var (name, period) in new[] { ("B", basePeriod), ("P", positionPeriod) })
        {
            var group = groups[name];
            if (group.Period != period)
            {
                if (group.Period == 0 || period < group.Period) group.Due = now;
                else group.Due = group.LastStarted == 0 ? now : group.LastStarted + Milliseconds(period);
                group.Period = period;
            }
            group.Enabled = !acquisitionPaused && (name == "B" || !groups["T"].Enabled);
        }
        var policy = (basePeriod, positionPeriod, groups["X"].Enabled, groups["F"].Enabled,
            groups["F"].Fast, groups["U"].Enabled, groups["U"].Fast, groups["T"].Enabled);
        if (policy != measuredPolicy)
        {
            measuredPolicy = policy;
            PlcCommunicationMeasurement.Policy(now, basePeriod, positionPeriod, groups["X"].Enabled,
                groups["F"].Enabled, groups["F"].Fast, groups["U"].Enabled, groups["U"].Fast, groups["T"].Enabled);
        }
    }
    private async Task PumpAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                Task wake; long next;
                lock (sync)
                {
                    RefreshPeriods();
                    var now = Stopwatch.GetTimestamp();
                    foreach (var group in groups.Values.Where(g => (g.Enabled || g.Demand) && !g.Running && g.Due <= now))
                    {
                        group.Running = true;
                        group.Work = ReadRoundAsync(group, group.Generation, epoch, group.Due, group.Demand, now, ct);
                    }
                    next = groups.Values.Where(g => (g.Enabled || g.Demand) && !g.Running).Select(g => g.Due).DefaultIfEmpty(now + Milliseconds(1000)).Min();
                    wake = samplingChanged.Task;
                }
                if (stopRequested)
                {
                    var prior = PlcCommunicationMeasurement.Source.Value;
                    stopRequested = false; // One request, never an automatic resend after reconciliation is required.
                    try { PlcCommunicationMeasurement.Source.Value = "Stop"; await signals.WriteBitAsync(SignalId.SoftStopCmd, true, ct); }
                    catch(Exception error) when(error is IOException or InvalidOperationException or OperationCanceledException)
                    {
                        logger.LogError(error,"PLC stop dispatch failed; physical stop remains unconfirmed, epoch={Epoch}",epoch);
                        Gaode.Diagnostics.RuntimeDiagnostics.Record("StopDispatch","Failed",diagnosticEnvelope?.RunId,
                            new {epoch,physicalStopConfirmed=false,reason=error.Message},warning:true);
                        LatchFailure("StopDispatchFailed;PhysicalStopUnconfirmed","stop",error);
                    }
                    finally { PlcCommunicationMeasurement.Source.Value = prior; }
                    LatchFailure("StopRequested;PhysicalStopUnconfirmed");
                }
                using var timer = CancellationTokenSource.CreateLinkedTokenSource(ct);
                await Task.WhenAny(wake, Task.Delay(TimeSpan.FromSeconds(Math.Max(0, next - Stopwatch.GetTimestamp()) / (double)Stopwatch.Frequency), timer.Token));
                await timer.CancelAsync();
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally
        {
            Task[] work; lock (sync) work = groups.Values.Select(g => g.Work ?? Task.CompletedTask).ToArray();
            await Task.WhenAll(work);
        }
    }
    private async Task ReadRoundAsync(AcquisitionGroup group, int generation, long sampledEpoch, long due, bool demand, long awakened, CancellationToken ct)
    {
        var measuredGroup = group.Fast && group.Name != "X" ? group.Name + ":first" : group.Name;
        var source = demand ? "D:" + group.Name : measuredGroup;
        await Task.Yield();
        var queued = Stopwatch.GetTimestamp();
        GroupObservation? observed = null;
        var previousSource = PlcCommunicationMeasurement.Source.Value;
        var previousEligibility = PlcScheduledTransport.Eligibility.Value;
        try
        {
            bool Current() { lock (sync) return !acquisitionPaused && generation == group.Generation && sampledEpoch == epoch && (group.Enabled || group.Demand); }
            PlcScheduledTransport.Eligibility.Value = Current;
            PlcCommunicationMeasurement.Source.Value = source;
            SignalId[] fields;
            lock(sync)
            {
                // The next-loop validity watch exists only after an actual selection.
                // Before that, startup/ordinary preparation retain the original B fields.
                // G owns this value while selecting; no duplicate sample producer.
                var omitGripper=groups["G"].Enabled||selectedGripper is null||selectedGripperEpoch!=sampledEpoch;
                fields=group.Name=="B"
                    ? !demand&&groups["X"].Enabled
                        ? omitGripper ? PreparedPlcReadPlans.BaseWithoutAxesOrGripper : PreparedPlcReadPlans.BaseWithoutAxes
                        : omitGripper ? PreparedPlcReadPlans.BaseWithoutGripper : PreparedPlcReadPlans.Base
                    : group.Fields;
            }
            var values = await signals.ReadAsync(fields, ct);
            lock (sync)
            {
                if (!Current()) return; // Completed old exchange remains raw evidence, never a new observation.
                if (group.Name is "X" or "B") activeAxisMoving?.Observe(values, sampledEpoch);
                ObserveAxisClosures(values, sampledEpoch);
                foreach (var pair in values.Words) sampledWords[pair.Key] = pair.Value;
                foreach (var pair in values.Stamps) sampledStamps[pair.Key] = pair.Value;
                if (group.Name == "B" && !fields.Contains(SignalId.XPosConfirmed))
                {
                    if (groups["X"].Latest is not { } x || x.Epoch != epoch) return;
                    values = Merge(values, x.Values);
                }
                // Same source maintains selection validity for the next loop; no mid-loop handshake or action gate.
                if(values.Words.ContainsKey(SignalId.GrabActiveId)&&selectedGripper is { } selected &&
                    values.Stamps[SignalId.GrabActiveId].Sent>=selectedGripperConfirmedTick &&
                    values.Word(SignalId.GrabActiveId)!=selected)
                    selectedGripper=null;
                observed = new(group.Name, ++group.Version, sampledEpoch, values, due, queued, Stopwatch.GetTimestamp());
                group.Latest = observed; group.LastStarted = observed.RoundStarted; group.Demand = false;
                if (group.Name is "P" or "T")
                {
                    var positions = SelectValues(values, PreparedPlcReadPlans.Position);
                    var p = new GroupObservation("P", ++groups["P"].Version, epoch, positions, due, queued, observed.Published);
                    groups["P"].Latest = p; groups["P"].LastStarted = p.Started; positionIdentity = p.Identity;
                    if (group.Name == "T") SignalGroup(groups["P"]);
                }
                PublishSample();
                SignalGroup(group);
                if (groups["B"].Latest is not null && positionIdentity is not null) connected.TrySetResult();
                // Timestamp the completed projection/notification, not its start.
                observed = observed with { Published = Stopwatch.GetTimestamp() };
                group.Latest = observed;
                if (group.Name is "P" or "T" && groups["P"].Latest is { } position)
                    groups["P"].Latest = position with { Published = observed.Published };
            }
            PlcCommunicationMeasurement.Count("Rounds:" + group.Name);
            PlcCommunicationMeasurement.Round(measuredGroup,
                due, queued, observed.Values.Stamps.Values.Distinct().ToArray(), observed.Published,
                awakened, generation, sampledEpoch, source);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested || generation != group.Generation || sampledEpoch != epoch) { }
        catch (Exception error)
        {
            var first = error is PlcConnectionUnusableException refusal ? refusal.FirstCause : error;
            LatchFailure(first.Message, "business-poll", first); connected.TrySetException(error);
            lock (sync) { group.Changed.TrySetException(error); group.Changed = NewSignal(); }
        }
        finally
        {
            PlcCommunicationMeasurement.Source.Value = previousSource; PlcScheduledTransport.Eligibility.Value = previousEligibility;
            lock (sync)
            {
                group.Running = false;
                if (generation == group.Generation)
                {
                    var start = observed?.RoundStarted ?? Stopwatch.GetTimestamp();
                    var period = Milliseconds(Math.Max(1, group.Period)); var now = Stopwatch.GetTimestamp();
                    var multiples = Math.Max(1, (now - start) / period + 1);
                    group.Due = start + multiples * period;
                    if (multiples > 1) PlcCommunicationMeasurement.Count("Missed:" + group.Name, multiples - 1);
                }
            }
            WakeSampling();
        }
    }
    private static void SignalGroup(AcquisitionGroup group)
    { var old = group.Changed; group.Changed = NewSignal(); old.TrySetResult(); }
    private SignalValues Merge(SignalValues left, SignalValues right)
    {
        var words = left.Words.ToDictionary(p => p.Key, p => p.Value);
        foreach (var pair in right.Words) words[pair.Key] = pair.Value;
        var result = new SignalValues(words, definitionAdmission.Definition.ByteOrder);
        foreach (var pair in left.Stamps.Concat(right.Stamps)) result.Stamps[pair.Key] = pair.Value;
        return result;
    }
    private SignalValues SelectValues(SignalValues value, IEnumerable<SignalId> fields)
    {
        var result = new SignalValues(fields.ToDictionary(id => id, id => value.Words[id]), definitionAdmission.Definition.ByteOrder);
        foreach (var id in fields) result.Stamps[id] = value.Stamps[id];
        return result;
    }
    private void PublishSample()
    {
        if (groups["B"].Latest is not { } basic || basic.Epoch != epoch) return;
        if (PreparedPlcReadPlans.Position.Any(id => !sampledWords.ContainsKey(id))) return;
        var combined = new SignalValues(sampledWords, definitionAdmission.Definition.ByteOrder);
        var identity = basic.Identity;
        if (!unknown)
            observation = Sample(combined, epoch, identity.SampleStartedUtc) with
            { ObservedUtc = identity.SampleEndedUtc, ObservationId = identity.ObservationId, PositionIdentity = positionIdentity };
        if (!observation.SafetyClear && (pcReady || pending is not null)) LatchFailure("SafetyInterlockLost");
    }
    private async Task<GroupObservation> WaitGroupAsync(string name, long after, bool demand, CancellationToken token)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            Task changed;
            lock (sync)
            {
                var group = groups[name == "P" && groups["T"].Enabled ? "T" : name];
                if (group.Latest is { } value && value.Epoch == epoch && value.Started >= after)
                    return value;
                changed = group.Changed.Task;
                if (demand && !group.Demand)
                { group.Demand = true; if (!group.Running) group.Due = Stopwatch.GetTimestamp(); WakeSampling(); }
            }
            await changed.WaitAsync(token);
        }
    }
    private async Task RefreshPositionAsync(CancellationToken token)
    {
        _ = await WaitGroupAsync("P", Stopwatch.GetTimestamp(), true, token);
        token.ThrowIfCancellationRequested();
    }
    private async Task RefreshBaseAsync(CancellationToken token)
    {
        _ = await WaitGroupAsync("B", Stopwatch.GetTimestamp(), true, token);
        token.ThrowIfCancellationRequested();
    }
    private async Task EnsureAdmissionObservationsAsync(bool positionRequired, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        bool refreshBase, refreshPosition;
        lock (sync)
        {
            definitionAdmission.RequireAdmitted();
            if (unknown || stopRequested) throw new InvalidOperationException("PlcUnavailableOrActionInFlight");
            var state = Observe();
            refreshBase = state.Reliability == DeviceReliability.Stale;
            refreshPosition = positionRequired && state.Position?.Identity.Reliability == DeviceReliability.Stale;
        }
        // Both dependencies share this eligibility instant; no serial restart of the deadline.
        var checks = new List<Task>(2);
        if (refreshBase) checks.Add(RefreshBaseAsync(token));
        if (refreshPosition) checks.Add(RefreshPositionAsync(token));
        await Task.WhenAll(checks);
        token.ThrowIfCancellationRequested();
    }
}
