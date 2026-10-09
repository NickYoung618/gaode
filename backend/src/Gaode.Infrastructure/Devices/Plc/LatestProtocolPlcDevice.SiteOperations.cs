using System.Diagnostics;
using Gaode.Diagnostics;
using Gaode.Domain.Station01;
using Gaode.Plc.Protocol;

namespace Gaode.Infrastructure.Devices.Plc;

public sealed partial class LatestProtocolPlcDevice
{
    private long? startupRequestEpoch;
    private long? verifiedSystemResetEpoch;

    private void RequireSiteOperations()
    {
        if (options.SiteOperations is not { IsValid: true })
            throw new InvalidOperationException("SiteOperationsUnconfirmed:PLC-Q4");
    }

    private double SafeZeroTolerance => options.SiteOperations?.SafeZeroToleranceMm ?? PositionTolerance;

    private void RequireSafeZero(SignalValues values)
    {
        foreach (var id in new[] { SignalId.MachineCurrentPosX, SignalId.MachineCurrentPosY, SignalId.MachineCurrentPosZ })
            if (!float.IsFinite(values.Float(id)) || Math.Abs(values.Float(id)) > (float)SafeZeroTolerance)
                throw new IOException("StartupSafeZeroUnconfirmed:" + id);
    }

    private void SiteOperationLog(string state, object detail, bool warning = false)
    {
        handshakeDiagnostic = new { phase = state, epoch, detail };
        RuntimeDiagnostics.Record("PlcSiteHandshake", state, diagnosticEnvelope?.RunId,
            new { epoch, source = options.SiteOperations?.SourceReference, detail }, warning: warning);
    }

    private async Task ClearResetActionRequestsAsync(long resetEpoch, CancellationToken token)
    {
        var began = Stopwatch.GetTimestamp();
        try
        {
            var before = await signals.ReadAsync(PreparedPlcReadPlans.ResetActionRequests, token);
            SiteOperationLog("ResetPcRequestsObserved", new { resetEpoch,
                fields = PreparedPlcReadPlans.ResetActionRequests.Select(id => new { signal = id.ToString(), raw = before.Word(id) }).ToArray() });
            foreach (var id in PreparedPlcReadPlans.ResetActionBits)
            {
                await signals.WriteBitAsync(id, false, token);
                SiteOperationLog("ResetPcRequestClearWriteResponded", new { resetEpoch, signal = id.ToString(), value = 0, readbackConfirmed = false });
            }
            foreach (var id in PreparedPlcReadPlans.ResetActionWords)
            {
                await signals.WriteWordAsync(id, 0, token);
                SiteOperationLog("ResetPcRequestClearWriteResponded", new { resetEpoch, signal = id.ToString(), value = 0, readbackConfirmed = false });
            }
            // Direct wire read after all writes: neither write acknowledgements nor cached samples authorize reset.
            var values = await signals.ReadAsync(PreparedPlcReadPlans.ResetActionRequests, token);
            lock (sync)
                if (epoch != resetEpoch || unknown) throw new IOException("ResetObservationEpochLost");
            var fields = PreparedPlcReadPlans.ResetActionRequests.Select(id => new {
                signal = id.ToString(), raw = values.Word(id), read = values.Stamps[id] }).ToArray();
            var cleared = fields.All(f => f.raw == 0);
            SiteOperationLog("ResetPcRequestsReadback", new { resetEpoch, cleared, fields,
                elapsedMs = Stopwatch.GetElapsedTime(began).TotalMilliseconds }, !cleared);
            if (!cleared) throw new IOException("ResetPcRequestsNotCleared:" +
                string.Join(",", fields.Where(f => f.raw != 0).Select(f => f.signal)));
            SiteOperationLog("ResetPcRequestsConfirmed", new { resetEpoch, elapsedMs = Stopwatch.GetElapsedTime(began).TotalMilliseconds });
        }
        catch (Exception error)
        {
            SiteOperationLog("ResetPcRequestsFailed", new { resetEpoch, phase = "ClearPcActionRequests",
                elapsedMs = Stopwatch.GetElapsedTime(began).TotalMilliseconds, errorType = error.GetType().Name,
                error = error.Message, resetDispatched = false }, true);
            throw;
        }
    }

    private async Task ResetSiteHandshakeAsync(CancellationToken token)
    {
        RequireSiteOperations();
        var resetEpoch = epoch;
        SiteOperationLog("ResetRequestReadStarted", new { resetEpoch });
        var previousRequest = await signals.ReadBitAsync(SignalId.SystemResetCmd, token);
        SiteOperationLog("ResetRequestObserved", new { resetEpoch, request = previousRequest }, previousRequest);
        if (previousRequest)
            throw new IOException("PreviousResetRequestNotReleased");
        await ClearResetActionRequestsAsync(resetEpoch, token);
        // PLC cannot reset while PC soft-stop is asserted. Wire readiness here
        // permits reset only; internal pcReady remains false until verification.
        await signals.WriteBitAsync(SignalId.PcSystemReady, true, token);
        await signals.WriteBitAsync(SignalId.SoftStopCmd, false, token);
        var preconditions = await signals.ReadAsync(PreparedPlcReadPlans.ResetPreconditions, token);
        lock (sync)
            if (epoch != resetEpoch || unknown) throw new IOException("ResetObservationEpochLost");
        SiteOperationLog("ResetPreconditionsRead", new { resetEpoch,
            pcReady = preconditions.Bit(SignalId.PcSystemReady),
            softStop = preconditions.Bit(SignalId.SoftStopCmd), request = preconditions.Bit(SignalId.SystemResetCmd) });
        if (!preconditions.Bit(SignalId.PcSystemReady) || preconditions.Bit(SignalId.SoftStopCmd))
            throw new IOException("ResetPreconditionsNotConfirmed:MB2006=1,MB2008=0");
        if (preconditions.Bit(SignalId.SystemResetCmd))
            throw new IOException("PreviousResetRequestNotReleased");
        SiteOperationLog("ResetPreconditionsConfirmed", new { resetEpoch, pcReady = true, softStop = false });
        SiteOperationLog("ResetRequestWriteStarted", new { resetEpoch, request = "MB2009", value = true });
        await signals.WriteBitAsync(SignalId.SystemResetCmd, true, token);
        SiteOperationLog("ResetRequested", new { request = "MB2009", feedback = "MB6015", resetEpoch });
        var after = Stopwatch.GetTimestamp();
        lock (sync) acquisitionPaused = false;
        WakeSampling(); EnsureLoops();
        var notReadyObserved = false;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var sample = await WaitGroupAsync("B", after, true, token);
            lock (sync)
                if (epoch != resetEpoch || unknown) throw new IOException("ResetObservationEpochLost");
            if (!sample.Values.Bit(SignalId.PlcReadyState))
            {
                if (!notReadyObserved) SiteOperationLog("ResetNotReadyObserved", new { resetEpoch });
                notReadyObserved = true;
            }
            else
            {
                // SC-021-PLC-R5: a fresh Ready=1 after the request confirms PLC
                // completion; observing a preceding zero is no longer required.
                SiteOperationLog("ResetReadyObserved", new { resetEpoch, readyReadAfterRequest = true, notReadyObserved });
                await signals.WriteBitAsync(SignalId.SystemResetCmd, false, token);
                SiteOperationLog("ResetRequestClearWriteResponded", new { resetEpoch, readbackConfirmed = false });
                // Acknowledge completion before the separate checks for allowing
                // another run. Failed checks still leave the run blocked.
                var started = DateTimeOffset.UtcNow;
                var values = await signals.ReadAsync([.. PreparedPlcReadPlans.Base, .. PreparedPlcReadPlans.Position], token);
                lock (sync)
                    if (epoch != resetEpoch || unknown) throw new IOException("ResetObservationEpochLost");
                var safetyClear = Sample(values, resetEpoch, started).SafetyClear;
                SiteOperationLog("ResetVerificationObserved", new { resetEpoch, safetyClear,
                    ready = values.Bit(SignalId.PlcReadyState), positionTolerance = PositionTolerance, safeZeroToleranceMm = SafeZeroTolerance,
                    x = values.Float(SignalId.MachineCurrentPosX), y = values.Float(SignalId.MachineCurrentPosY),
                    z = values.Float(SignalId.MachineCurrentPosZ) });
                if (!safetyClear || !values.Bit(SignalId.PlcReadyState))
                    throw new IOException("ResetCompletionSafetyUnconfirmed");
                RequireSafeZero(values);
                // An explicit completed system reset releases the prior start/stop commands.
                await signals.WriteBitAsync(SignalId.PcStartCmd, false, token);
                await signals.WriteBitAsync(SignalId.SoftStopCmd, false, token);
                await signals.WriteBitAsync(SignalId.PcSystemReady, true, token);
                if (options.SiteOperations?.RestoresWorkpieceAndMechanisms == true)
                {
                    foreach (var request in PreparedPlcReadPlans.Starts)
                        await signals.WriteBitAsync(request, false, token);
                    await signals.WriteBitAsync(SignalId.RotateStart, false, token);
                    await signals.WriteWordAsync(SignalId.FlipSorting, 0, token);
                    await signals.WriteWordAsync(SignalId.SortingCmd, 0, token);
                }
                // A system reset authorizes recovery only, never same-coordinate reuse.
                lock (sync)
                {
                    if (epoch != resetEpoch || unknown) throw new IOException("ResetObservationEpochLost");
                    pcReady = false; startupRequestEpoch = null; verifiedSystemResetEpoch = resetEpoch;
                    axisClosures.Clear();
                }
                SiteOperationLog("ResetCompleted", new { resetEpoch, readyReadAfterRequest = true, notReadyObserved });
                return;
            }
            after = sample.Ended + 1;
        }
    }

    private async Task StartSiteHandshakeAsync(long expectedEpoch, CancellationToken token)
    {
        RequireSiteOperations();
        var started = DateTimeOffset.UtcNow;
        var values = await signals.ReadAsync([.. PreparedPlcReadPlans.Base, .. PreparedPlcReadPlans.Position], token);
        lock (sync)
            if (epoch != expectedEpoch || unknown || !values.Bit(SignalId.PlcModeAuto) || !Sample(values, expectedEpoch, started).SafetyClear)
                throw new IOException("StartupConnectionOrSafetyLost");
        if (!values.Bit(SignalId.PlcReadyState)) throw new IOException("StartupPlcNotReady");
        RequireSafeZero(values);
        if (await signals.ReadBitAsync(SignalId.PcStartCmd, token))
            throw new IOException("PreviousStartRequestNotReleased");
        token.ThrowIfCancellationRequested();
        await signals.WriteBitAsync(SignalId.PcStartCmd, true, token);
        lock (sync) startupRequestEpoch = expectedEpoch;
        SiteOperationLog("StartRequested", new { request = "MB2007", clearAfter = "FirstPhysicalActionClosed" });
    }

    private async Task ClearStartupAfterPhysicalActionAsync(ActionWindow window, long expectedEpoch, CancellationToken token)
    {
        lock (sync) if (startupRequestEpoch != expectedEpoch) return;
        CheckAxisWindow(window, expectedEpoch, token);
        await signals.WriteBitAsync(SignalId.PcStartCmd, false, token);
        CheckAxisWindow(window, expectedEpoch, token);
        lock (sync) startupRequestEpoch = null;
        SiteOperationLog("StartClearedAfterPhysicalAction", new { request = "MB2007", expectedEpoch });
    }
}
