using Gaode.Plc.Protocol;
using Gaode.Application.Ports;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Microsoft.Extensions.Logging;

namespace Gaode.Infrastructure.Devices.Plc;

// The formally assembled device owns the connection, admission, raw evidence and motion lease.
// This adapter has no independent transport or alternative producer of completion facts.
public sealed partial class LatestProtocolStageActionAdapter(
    LatestProtocolPlcDevice device, BusinessDurations budget, IPickCommitPort pickCommit) : IPlcStageActionPort
{
    private PlcSignalAccessor Signals => device.StageSignals;
    private static ushort Code(SignalId signal, string name) => SignalCodes.Value(signal, name);

    public async ValueTask<PlcStageActionResult> ExecuteAsync(PlcStageActionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.IsValid || !DeviceStageContract.IsKnownStage(request.Stage))
            throw new ArgumentException("InvalidStageActionRequest", nameof(request));
        var began = false;
        var dispatched = false;
        try
        {
            await device.BeginStageActionAsync(request, request.Correlation.ActionId, cancellationToken);
            began = true;
            device.BeginStageEvidence(request.Correlation);
            using var deadline = PhaseCancellation(request, int.MaxValue);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
            var ct = linked.Token;
            var failure = await PreflightAsync(request, ct);
            if (failure is not null) return Result(request, StageActionKind.Failed, failure);
            device.StageLogger.LogInformation("Device stage dispatch: runId={RunId}, operationId={OperationId}, actionId={ActionId}, stage={Stage}, epoch={Epoch}",
                request.RunId, request.OperationId, request.Correlation.ActionId, request.Stage, request.ConnectionEpoch);
            switch (request.Stage)
            {
                case PlcWorkflowStage.TransferToRotation:
                case PlcWorkflowStage.Sorting:
                    dispatched = true;
                    if (request.RequestedGripperId is int grip) await device.SelectStageGripperAsync(request, grip, ct);
                    return await SortingAsync(request, ct);
                case PlcWorkflowStage.Rotate:
                    dispatched = true;
                    return await RotateAsync(request, ct);
                case PlcWorkflowStage.UnloadPreparation:
                    dispatched = true;
                    return await UnloadAsync(request, ct);
                default: throw new InvalidOperationException("UnknownStage");
            }
        }
        catch (Exception error)
        {
            device.StageLogger.LogWarning(error, "Device stage failed: runId={RunId}, operationId={OperationId}, actionId={ActionId}, stage={Stage}, dispatched={Dispatched}, noAutomaticReplay=true",
                request.RunId, request.OperationId, request.Correlation.ActionId, request.Stage, dispatched);
            // Exception details remain communication diagnostics, not business decision fields.
            return dispatched ? Unknown(request, "DeviceActionOutcomeUnconfirmed") :
                Result(request, StageActionKind.Failed, "DeviceActionNotDispatched");
        }
        finally { if (began) device.EndStageAction(); }
    }

    private async Task<string?> PreflightAsync(PlcStageActionRequest request, CancellationToken ct)
    {
        int[] waits = [1000, 2000, 4000];
        for (var attempt = 0; ; attempt++)
        {
            EnsureCurrent(request, ct);
            try
            {
                if (request.Stage == PlcWorkflowStage.UnloadPreparation)
                {
                    if (request.UnloadTarget is not { } target || !ValidPosition(target) ||
                        string.IsNullOrWhiteSpace(request.TargetPurpose)) return "UnloadTargetMissingOrInvalid";
                }
                else if (request.Stage is PlcWorkflowStage.Sorting or PlcWorkflowStage.TransferToRotation)
                {
                    if (!ValidPosition(request.SortingSource!) || !ValidPosition(request.SortingTarget!))
                        return "SortingTargetInvalid";
                    _ = device.SortingSafetyTarget(request.SortingSource!, request.TargetPurpose!);
                    _ = device.SortingSafetyTarget(request.SortingTarget!, request.TargetPurpose!);
                    var status = await Signals.ReadWordAsync(SignalId.SortingExecStatus, ct);
                    if (status != Code(SignalId.SortingExecStatus, "Idle") || await Signals.ReadWordAsync(SignalId.SortingCmd, ct) != 0)
                        return "TransferNotReady";
                }
                if (device.StageOrigin.Provider == DeviceProvider.Real && request.TargetPurpose != device.StageConfigurationPurpose)
                    return "TargetPurposeNotDeviceConfiguration";
                EnsureCurrent(request, ct);
                return null;
            }
            catch (Exception error) when (attempt < 3 && error is IOException or TimeoutException)
            {
                if (DateTimeOffset.UtcNow.AddMilliseconds(waits[attempt]) >= request.DeadlineUtc) throw;
                await Task.Delay(waits[attempt], ct);
            }
        }
    }

    private static bool ValidPosition(FixedPoint target) => !string.IsNullOrWhiteSpace(target.Id) &&
        !string.IsNullOrWhiteSpace(target.Version) && double.IsFinite(target.X) && double.IsFinite(target.Y) && double.IsFinite(target.Z);
    private async Task<PositionReachedEvidence> MoveAxesAsync(PlcStageActionRequest request, FixedPoint target, bool grabOnly, CancellationToken ct)
    {
        EnsureCurrent(request, ct);
        using var phase = PhaseCancellation(request, budget.XyCompletion);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, phase.Token);
        return await device.MoveStageAxesAsync(request, target, grabOnly, linked.Token);
    }
    private async Task WriteAsync(PlcStageActionRequest request, SignalId signal, ushort value, CancellationToken ct)
    {
        EnsureCurrent(request, ct);
        using var dispatch = device.ActionDispatchEligibility(request.Window, request.ConnectionEpoch, ct);
        await Signals.WriteWordAsync(signal, value, ct);
        EnsureCurrent(request, ct);
    }
    private void EnsureCurrent(PlcStageActionRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = device.Observe();
        if (!state.HasReliableObservation || state.OperatingMode != OperatingMode.Automatic ||
            state.SafetyAssessment != SafetyAssessment.Clear || state.ConnectionEpoch != request.ConnectionEpoch)
            throw new IOException("StageCurrentObservationLost");
        if (!request.Window.Contains(System.Diagnostics.Stopwatch.GetTimestamp())) throw new TimeoutException("StageWindowClosed");
    }
    private static CancellationTokenSource PhaseCancellation(PlcStageActionRequest request, int phaseMs)
    {
        var remaining = TimeSpan.FromSeconds((request.Window.DueTick - System.Diagnostics.Stopwatch.GetTimestamp()) /
            (double)System.Diagnostics.Stopwatch.Frequency);
        if (remaining <= TimeSpan.Zero) throw new TimeoutException("StageWindowClosed");
        return new(TimeSpan.FromMilliseconds(Math.Min(phaseMs, remaining.TotalMilliseconds)));
    }
    private async Task<PlcStageActionResult> CompleteAsync(PlcStageActionRequest request, DeviceCompletionMeaning meaning,
        IReadOnlyList<PositionReachedEvidence> positions, ObservationIdentity observation, CancellationToken ct, PositionReachedEvidence? safe = null,
        AngleReachedEvidence? angle = null)
    {
        using var save = PhaseCancellation(request, budget.CriticalSave);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, save.Token);
        var evidence = await device.SaveStageEvidenceAsync(request, meaning, positions, observation, linked.Token, safe, angle);
        EnsureCurrent(request, ct);
        return new(request, StageActionKind.Completed, request.Correlation.ActionId, request.ConnectionEpoch,
            null, false, false, observation.SampleEndedUtc, device.StageOrigin, evidence);
    }
    private PlcStageActionResult Result(PlcStageActionRequest request, StageActionKind kind, string reason) =>
        new(request, kind, request.Correlation.ActionId, request.ConnectionEpoch, reason,
            kind == StageActionKind.UnknownHeld, false, DateTimeOffset.UtcNow, device.StageOrigin, null);
    private async Task<PlcStageActionResult> RotateAsync(PlcStageActionRequest request, CancellationToken token)
    {
        using var phase = PhaseCancellation(request, budget.XyCompletion);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, phase.Token);
        var reached = await device.RotateStageAsync(request, linked.Token);
        return await CompleteAsync(request, DeviceCompletionMeaning.PositionReached, [], reached.Observation,
            token, angle: reached.Angle);
    }
    private PlcStageActionResult Unknown(PlcStageActionRequest request, string reason)
    {
        device.HoldUnknownStageAction(request.ConnectionEpoch, reason);
        return Result(request, StageActionKind.UnknownHeld, reason);
    }
}
