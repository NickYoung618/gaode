using System.Diagnostics;
using Gaode.Application.Ports;
using Gaode.Domain.Station01;
using Gaode.Plc.Protocol;
using Microsoft.Extensions.Logging;

namespace Gaode.Infrastructure.Devices.Plc;

public sealed partial class LatestProtocolPlcDevice
{
    private sealed record AxisClosure(long Epoch, float Actual);
    private readonly Dictionary<SignalId, AxisClosure> axisClosures = [];
    private DeviceObservation? completedMoveObservation;
    private object? handshakeDiagnostic;
    internal sealed record ClearanceProof(Guid Cycle, long Epoch, long WriteEnded,
        IReadOnlyDictionary<SignalId, PlcReadStamp> Reads);
    internal static ClearanceProof? ConfirmClearance(Guid cycle, long expectedEpoch, long sampledEpoch,
        long writeEnded, SignalValues values, SignalId[] fields, DateTimeOffset now, int staleMs)
    {
        if (sampledEpoch != expectedEpoch || writeEnded <= 0 || fields.Any(id =>
            !values.Stamps.TryGetValue(id, out var stamp) || stamp.Sent <= writeEnded))
            throw new IOException("PlcClearObservationInvalid");
        if (fields.Any(id => (now-values.Stamps[id].StartedUtc).TotalMilliseconds > staleMs))
            throw new IOException("PlcClearObservationExpired");
        return fields.All(id=>values.Word(id)==0) ? new(cycle,expectedEpoch,writeEnded,
            fields.ToDictionary(id=>id,id=>values.Stamps[id])) : null;
    }

    private bool ClosedAxisFeedbackValid(SignalId feedback, ushort value) => value == 0 ||
        definitionAdmission.Definition.IsSiteLayout && value == SignalCodes.Value(feedback, "Arrived");

    private bool CanReuseAxis(AxisMove axis, SignalValues values, long expectedEpoch)
    {
        lock (sync)
        {
            if (!axisClosures.TryGetValue(axis.Start, out var closed)) return false;
            if (closed.Epoch != expectedEpoch || values.Bit(axis.Start) || !ClosedAxisFeedbackValid(axis.Confirmed, values.Word(axis.Confirmed)) ||
                !float.IsFinite(values.Float(axis.Actual)) ||
                Math.Abs(values.Float(axis.Actual) - closed.Actual) > PositionTolerance)
            { axisClosures.Remove(axis.Start); return false; }
            return Math.Abs(values.Float(axis.Actual) - (float)axis.Value) <= PositionTolerance;
        }
    }

    private void ObserveAxisClosures(SignalValues values, long sampledEpoch)
    {
        if (values.Words.ContainsKey(SignalId.PlcReadyState) && (!values.Bit(SignalId.PlcReadyState) ||
            !values.Bit(SignalId.PlcModeAuto) || values.Bit(SignalId.PlcSystemFault) ||
            definitionAdmission.Definition.IsSiteLayout && (options.SiteOperations is not { IsValid: true } ||
                ConfirmedMemoryLayout.IndependentSafetySignals.Values.Any(values.Bit)) ||
            (!definitionAdmission.Definition.IsSiteLayout && values.Bit(SignalId.ManualZoneOccupied)) ||
            values.Word(SignalId.AlarmBits) != 0))
        { axisClosures.Clear(); return; }
        var starts = PreparedPlcReadPlans.Starts;
        var feedbacks = PreparedPlcReadPlans.Axes;
        var positions = PreparedPlcReadPlans.Position;
        for (var i = 0; i < starts.Length; i++)
        {
            if (!axisClosures.TryGetValue(starts[i], out var closed)) continue;
            if (closed.Epoch != sampledEpoch ||
                values.Words.ContainsKey(starts[i]) && values.Word(starts[i]) != 0 ||
                values.Words.ContainsKey(feedbacks[i]) && !ClosedAxisFeedbackValid(feedbacks[i], values.Word(feedbacks[i])) ||
                values.Words.ContainsKey(positions[i]) && (!float.IsFinite(values.Float(positions[i])) ||
                    Math.Abs(values.Float(positions[i]) - closed.Actual) > PositionTolerance))
                axisClosures.Remove(starts[i]);
        }
    }

    // One pump, one original action deadline. A write ACK is only a watermark.
    private async Task ClearAndConfirmAsync(string resource, string group, SignalId request, bool bit,
        SignalId[] fields, ActionWindow window, long expectedEpoch, CancellationToken token)
    {
        var cycle = Guid.NewGuid();
        using var eligibility = ActionDispatchEligibility(window, expectedEpoch, token);
        var clock = new PlcExchangeClock();
        var previous = PlcExchangeClock.Current.Value;
        try
        {
            CheckAxisWindow(window, expectedEpoch, token);
            PlcExchangeClock.Current.Value = clock;
            if (bit) await signals.WriteBitAsync(request, false, token);
            else await signals.WriteWordAsync(request, 0, token);
        }
        catch (Exception error)
        {
            logger.LogError(error, "PcClearWriteUnknown resource={Resource} cycle={Cycle} epoch={Epoch} action={Action}",
                resource, cycle, expectedEpoch, evidenceCorrelation?.ActionId);
            throw new IOException("PcClearWriteUnknown:" + resource, error);
        }
        finally { PlcExchangeClock.Current.Value = previous; }
        logger.LogInformation("PlcClearWaiting resource={Resource} cycle={Cycle} epoch={Epoch} action={Action} writeEnded={WriteEnded} due={Due}",
            resource, cycle, expectedEpoch, evidenceCorrelation?.ActionId, clock.Ended, window.DueTick);
        handshakeDiagnostic = new { resource, cycle, expectedEpoch, action=evidenceCorrelation, writeEnded=clock.Ended,
            due=window.DueTick, phase="Waiting" };
        Gaode.Diagnostics.RuntimeDiagnostics.Record("PlcHandshake", "Waiting", evidenceCorrelation?.RunId, handshakeDiagnostic);
        var after = clock.Ended + 1;
        var demandRead = true;
        try
        {
            while (true)
            {
                CheckAxisWindow(window, expectedEpoch, token);
                var sample = await WaitGroupAsync(group, after, demandRead, token);
                // The feedback group remains enabled. After one immediate read, use its
                // scheduled fresh samples rather than flooding the evidence journal.
                demandRead = false;
                CheckAxisWindow(window, expectedEpoch, token);
                var stamps = fields.Select(id => sample.Values.Stamps[id]).ToArray();
                var proof=ConfirmClearance(cycle,expectedEpoch,sample.Epoch,clock.Ended,sample.Values,fields,
                    DateTimeOffset.UtcNow,Math.Max(500,options.IoTimeoutMs*5));
                handshakeDiagnostic = new { resource, cycle, expectedEpoch, action=evidenceCorrelation, writeEnded=clock.Ended,
                    due=window.DueTick, fields=fields.Select(id=>new { signal=id.ToString(),raw=sample.Values.Word(id),read=sample.Values.Stamps[id] }).ToArray() };
                if (proof is not null)
                {
                    logger.LogInformation("PlcClearConfirmed resource={Resource} cycle={Cycle} epoch={Epoch} action={Action} writeEnded={WriteEnded} readStarted={ReadStarted} readEnded={ReadEnded} fields={Fields}",
                        resource, cycle, expectedEpoch, evidenceCorrelation?.ActionId, clock.Ended,
                        stamps.Min(s => s.Sent), stamps.Max(s => s.Ended), string.Join(",", fields));
                    Gaode.Diagnostics.RuntimeDiagnostics.Record("PlcHandshake", "Confirmed", evidenceCorrelation?.RunId, handshakeDiagnostic);
                    return;
                }
                after = sample.Ended + 1;
            }
        }
        catch (Exception error)
        {
            logger.LogError(error, "PlcClearFailed resource={Resource} cycle={Cycle} epoch={Epoch} action={Action} due={Due} noReplay=true",
                resource, cycle, expectedEpoch, evidenceCorrelation?.ActionId, window.DueTick);
            Gaode.Diagnostics.RuntimeDiagnostics.Record("PlcHandshake", "Failed", evidenceCorrelation?.RunId, handshakeDiagnostic, error);
            throw new IOException("PlcClearUnconfirmed:" + resource + ":" + error.Message, error);
        }
    }

    internal async Task ClearSortingAsync(PlcStageActionRequest request, CancellationToken token)
    {
        SetFeedback("T", true);
        try { await ClearAndConfirmAsync("Sort", "T", SignalId.SortingCmd, false,
            [SignalId.SortingCmd, SignalId.SortingExecStatus], request.Window, request.ConnectionEpoch, token); }
        finally { SetFeedback("T", false); }
    }
}
