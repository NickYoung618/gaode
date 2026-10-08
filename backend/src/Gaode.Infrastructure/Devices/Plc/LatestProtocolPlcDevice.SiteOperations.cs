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

    private void RequireSafeZero(SignalValues values)
    {
        foreach (var id in new[] { SignalId.MachineCurrentPosX, SignalId.MachineCurrentPosY, SignalId.MachineCurrentPosZ })
            if (!float.IsFinite(values.Float(id)) || Math.Abs(values.Float(id)) > PositionTolerance)
                throw new IOException("StartupSafeZeroUnconfirmed:" + id);
    }

    private void SiteOperationLog(string state, object detail, bool warning = false) =>
        RuntimeDiagnostics.Record("PlcSiteHandshake", state, diagnosticEnvelope?.RunId,
            new { epoch, source = options.SiteOperations?.SourceReference, detail }, warning: warning);

    private async Task ResetSiteHandshakeAsync(CancellationToken token)
    {
        RequireSiteOperations();
        var resetEpoch = epoch;
        if (await signals.ReadBitAsync(SignalId.SystemResetCmd, token))
            throw new IOException("PreviousResetRequestNotReleased");
        // PLC cannot reset while PC soft-stop is asserted. Wire readiness here
        // permits reset only; internal pcReady remains false until verification.
        await signals.WriteBitAsync(SignalId.PcSystemReady, true, token);
        await signals.WriteBitAsync(SignalId.SoftStopCmd, false, token);
        var preconditions = await signals.ReadAsync(PreparedPlcReadPlans.ResetPreconditions, token);
        lock (sync)
            if (epoch != resetEpoch || unknown) throw new IOException("ResetObservationEpochLost");
        if (!preconditions.Bit(SignalId.PcSystemReady) || preconditions.Bit(SignalId.SoftStopCmd))
            throw new IOException("ResetPreconditionsNotConfirmed:MB2006=1,MB2008=0");
        if (preconditions.Bit(SignalId.SystemResetCmd))
            throw new IOException("PreviousResetRequestNotReleased");
        SiteOperationLog("ResetPreconditionsConfirmed", new { resetEpoch, pcReady = true, softStop = false });
        await signals.WriteBitAsync(SignalId.SystemResetCmd, true, token);
        SiteOperationLog("ResetRequested", new { request = "MB2009", feedback = "MB6015", resetEpoch });
        lock (sync) acquisitionPaused = false;
        WakeSampling(); EnsureLoops();
        var after = Stopwatch.GetTimestamp();
        var sawNotReady = false;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            var sample = await WaitGroupAsync("B", after, true, token);
            lock (sync)
                if (epoch != resetEpoch || unknown) throw new IOException("ResetObservationEpochLost");
            if (!sample.Values.Bit(SignalId.PlcReadyState))
            {
                if (!sawNotReady) SiteOperationLog("ResetNotReadyObserved", new { resetEpoch });
                sawNotReady = true;
            }
            else if (sawNotReady)
            {
                var started = DateTimeOffset.UtcNow;
                var values = await signals.ReadAsync([.. PreparedPlcReadPlans.Base, .. PreparedPlcReadPlans.Position], token);
                lock (sync)
                    if (epoch != resetEpoch || unknown) throw new IOException("ResetObservationEpochLost");
                if (!Sample(values, resetEpoch, started).SafetyClear || !values.Bit(SignalId.PlcReadyState))
                    throw new IOException("ResetCompletionSafetyUnconfirmed");
                RequireSafeZero(values);
                await signals.WriteBitAsync(SignalId.SystemResetCmd, false, token);
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
                lock (sync)
                {
                    if (epoch != resetEpoch || unknown) throw new IOException("ResetObservationEpochLost");
                    pcReady = true; startupRequestEpoch = null; verifiedSystemResetEpoch = resetEpoch;
                }
                SiteOperationLog("ResetCompleted", new { resetEpoch, readyFallingThenRisingObserved = true });
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
            if (epoch != expectedEpoch || unknown || !Sample(values, expectedEpoch, started).SafetyClear)
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
