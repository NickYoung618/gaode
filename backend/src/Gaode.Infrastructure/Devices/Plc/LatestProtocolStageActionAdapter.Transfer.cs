using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Plc.Protocol;
using Microsoft.Extensions.Logging;

namespace Gaode.Infrastructure.Devices.Plc;

public sealed partial class LatestProtocolStageActionAdapter
{
    private async Task<PlcStageActionResult> SortingAsync(PlcStageActionRequest request, CancellationToken ct)
    {
        await MoveAxesAsync(request, request.SortingSource!, false, ct);
        await MoveAxesAsync(request, request.SortingSource!, true, ct);
        await WriteAsync(request, SignalId.SortingCmd, Code(SignalId.SortingCmd, "Pick"), ct);
        var picked = await WaitTransferAsync(request, false, ct);
        DeviceActionEvidence raw;
        try
        {
            using var save = PhaseCancellation(request, budget.CriticalSave);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, save.Token);
            raw = await device.SaveStageEvidenceAsync(request, DeviceCompletionMeaning.MaterialPicked,
                [picked.Reached], picked.Completed, linked.Token);
        }
        catch (Exception error)
        {
            var known = (error as CommunicationEvidenceUnavailableException)?.Receipt;
            var notice = new PickEvidenceFailureNotice(request.Correlation, request.ReservationReference!,
                PhysicalPickState.Observed, picked.Reached, picked.Completed.SampleEndedUtc, picked.Completed,
                device.StageOrigin, known?.ActualCommit ?? ActualCommitState.Unknown,
                known?.Validity == ReceiptValidity.ValidCurrent ? ReceiptValidity.Invalid : known?.Validity ?? ReceiptValidity.None,
                "PickEvidencePersistenceUnconfirmed");
            device.HoldUnknownStageAction(request.ConnectionEpoch, notice.Reason);
            // This separate failure save has no success/placement permission. It remains best effort if storage is unavailable.
            try
            {
                using var failureSave = new CancellationTokenSource(TimeSpan.FromMilliseconds(budget.CriticalSave));
                await pickCommit.ReportPickEvidenceFailureAsync(notice, failureSave.Token);
            }
            catch (Exception failure)
            {
                device.StageLogger.LogError(failure, "Pick failure record unconfirmed: runId={RunId}, actionId={ActionId}, physicalPick=Observed, holdsDevice=true",
                    request.RunId, request.Correlation.ActionId);
            }
            return Result(request, StageActionKind.UnknownHeld, notice.Reason);
        }
        var evidence = new PickCompletionEvidence(request.Correlation,
            request.Correlation.ObjectId ?? throw new InvalidOperationException("SortingObjectMissing"),
            request.PhysicalSlotIndex!.Value, request.ReservationReference!, request.ActionParametersDigest,
            Identity(request.SortingSource!), Identity(request.SortingTarget!), picked.Reached,
            picked.Completed.SampleEndedUtc, picked.Completed, device.StageOrigin, raw.DiagnosticEvidenceReferences, request.Window);
        PickCommitReceipt receipt;
        using (var save = PhaseCancellation(request, budget.CriticalSave))
        using (var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, save.Token))
            receipt = await pickCommit.CommitPickAsync(evidence, linked.Token);
        Gaode.Diagnostics.RuntimeDiagnostics.Record("PickCommitReceipt", "Observed", request.RunId,
            new { request.Correlation.ActionId, request.OperationId, receipt,
                mayAuthorizePlace = receipt.MayAuthorizePlace(evidence, Stopwatch.GetTimestamp()) });
        if (receipt.Window != request.Window || !receipt.MayAuthorizePlace(evidence, Stopwatch.GetTimestamp()))
            return Unknown(request, "PickCommitReceiptNotCurrent");
        EnsureCurrent(request, ct);
        await MoveAxesAsync(request, device.SortingSafetyTarget(request.SortingSource!, request.TargetPurpose!), true, ct);
        if (!receipt.MayAuthorizePlace(evidence, Stopwatch.GetTimestamp())) return Unknown(request, "PickCommitReceiptExpired");
        await MoveAxesAsync(request, request.SortingTarget!, false, ct);
        await MoveAxesAsync(request, request.SortingTarget!, true, ct);
        if (!receipt.MayAuthorizePlace(evidence, Stopwatch.GetTimestamp())) return Unknown(request, "PickCommitReceiptExpired");
        await WriteAsync(request, SignalId.SortingCmd, Code(SignalId.SortingCmd, "Place"), ct);
        var placed = await WaitTransferAsync(request, true, ct);
        var safe = await MoveAxesAsync(request, device.SortingSafetyTarget(request.SortingTarget!, request.TargetPurpose!), true, ct);
        await device.ClearSortingAsync(request, ct);
        return await CompleteAsync(request, DeviceCompletionMeaning.MaterialTransferred,
            [picked.Reached, placed.Reached], safe.Actual.Identity, ct, safe);
    }
    private static FixedPointIdentity Identity(FixedPoint point) => new(point.Id, point.Version,
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(point)))));
    private async Task<(PositionReachedEvidence Reached, ObservationIdentity Completed)> WaitTransferAsync(
        PlcStageActionRequest request, bool placing, CancellationToken ct)
    {
        using var phase = PhaseCancellation(request, budget.XyCompletion);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, phase.Token);
        ct = linked.Token;
        var target = placing ? request.SortingTarget! : request.SortingSource!;
        var expected = Code(SignalId.SortingExecStatus, placing ? "Placed" : "Picked");
        var after = Stopwatch.GetTimestamp();
        device.SetTransferSampling(true);
        try
        {
        while (true)
        {
            EnsureCurrent(request, ct);
            var sample = await device.ReadStageSampleAsync(after, request.ConnectionEpoch, ct);
            EnsureCurrent(request, ct);
            var current = new PositionReachedEvidence(request.Correlation, target, sample.Position, request.PositionTolerance);
            if (!current.Matched) throw new IOException("TransferCurrentTargetNotObserved");
            if (sample.Status == expected) return (current, sample.Position.Identity);
            if (sample.Status == Code(SignalId.SortingExecStatus, "GrabFailed")) throw new IOException("TransferFailureFeedback");
            // Before completion the prior feedback is retained. There is no new
            // Executing code in this protocol and no ACK inferred from a write.
            if (placing ? sample.Status != Code(SignalId.SortingExecStatus, "Picked") :
                sample.Status != Code(SignalId.SortingExecStatus, "Idle"))
                throw new IOException("TransferUnrecognizedFeedback");
            after = sample.Ended + 1;
        }
        }
        finally { device.SetTransferSampling(false); }
    }
    private async Task<PlcStageActionResult> UnloadAsync(PlcStageActionRequest request, CancellationToken ct)
    {
        var reached = await MoveAxesAsync(request, request.UnloadTarget!, false, ct);
        return await CompleteAsync(request, DeviceCompletionMeaning.UnloadPrepared, [reached], reached.Actual.Identity, ct);
    }
}
