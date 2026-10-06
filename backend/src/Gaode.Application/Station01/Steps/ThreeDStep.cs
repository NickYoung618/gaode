using System.Text.Json;
using Gaode.Application.Acquisition;
using Gaode.Application.Algorithms;
using Gaode.Application.Ports;
using Gaode.Application.Motion;
using Gaode.Domain.Station01;
using Gaode.Diagnostics;
using Gaode.Application.Timing;

namespace Gaode.Application.Station01.Steps;

public sealed record ThreeDEvidence(MoveEvidence Move, MediaRef Media,
    AlgorithmOutcome Algorithm, TrayObservation Observation);

public sealed class ThreeDStep(FixedMoveStep move, AcquisitionCoordinator acquisition,
    AlgorithmRuntime algorithms, MotionCoordinator motion)
{
    public async Task<ThreeDEvidence> ExecuteAsync(RunExecution run, ControlLatch control,
        CancellationToken cancellationToken)
    {
        var config = run.Config.Public;
        var point = config.Motion.Points.ThreeD;
        var moved = await move.ExecuteAsync(run, control, point, "3D", cancellationToken);
        if (control.CancelRequested || control.SafetyFault) throw new InvalidOperationException("控制准入已关闭");
        var inspection = await motion.OpenCaptureWindowAsync(new(moved.Evidence.Correlation, CaptureRole.ThreeD,
            point, moved.Evidence.Positions.Single(), moved.Window), false, cancellationToken);
        CommitReceipt? work = null;
        string? failureReason = null;
        try
        {
            var media = await acquisition.CaptureAsync(run, CaptureRole.ThreeD, point,
                config.Capture3d.Scope.Id, config.Capture3d.Scope.Version,
                config.Capture3d.BindingId, config.Capture3d.LightBindingId,
                config.Capture3d.MaxCaptureBytes, cancellationToken);
            var context = new TrayObservationContext(StartRunContextParser.Parse(run.ContextJson).TrayId,
                TrayObservationPurpose.InitialPreparation, 1, null);
            var outcome = await algorithms.InvokeAsync(run, AlgorithmRole.TrayPose,
                media.CaptureId, [media], config.Capture3d.Scope.Version, cancellationToken, context);
            var observation = outcome.Event?.Observation;
            var technical = outcome.State == AlgorithmState.Success && (observation is not { HasCompleteCoverage: true } ||
                observation.RunId != run.RunId || observation.TrayId != context.TrayId || observation.CaptureId != media.CaptureId ||
                observation.CallId != outcome.CallId || observation.Purpose != context.Purpose || observation.CheckRound != 1)
                ? AlgorithmState.InvalidResult : outcome.State;
            AlgorithmOutcomePolicy.RequireFinite(technical);
            work = await run.SaveAsync(WriteKind.AlgorithmFact,
                new AlgorithmFactPayload(outcome.CallId, technical,
                    outcome.Event?.Kind == AlgorithmEventKind.Result,
                    JsonSerializer.Serialize(observation), outcome.Decision, outcome.DispatchEvidence,
                    outcome.DispatchTick, outcome.AcceptedTick, outcome.WorkerSessionId)
                    { Origin = outcome.Origin, RunId = run.RunId, CaptureId = media.CaptureId },
                cancellationToken: cancellationToken);
            if (technical != AlgorithmState.Success || observation is null)
                throw new InvalidOperationException("InitialTrayObservationUnavailable:" + technical);
            run.RecordInitialObservation(observation, work);
            return new(moved, media, outcome, observation);
        }
        catch (Exception error)
        {
            failureReason = error.Message;
            RuntimeDiagnostics.Record("ThreeD", "FailedBeforeInspectionCleanup", run.RunId,
                new { moved.OperationId, moved.DeviceEpoch,
                    disposition = "PreserveOriginalFailure_AcquisitionReleaseMayAlsoFail" }, error);
            throw;
        }
        finally
        {
            // Shutdown/cancellation is deliberately left in RecoveryRequired.  Do not
            // turn the cleanup handshake into a business failure after admission closes.
            if (!control.CancelRequested && !control.SafetyFault && !cancellationToken.IsCancellationRequested)
            {
                var release = ActionWindows.Start(run.Clock, run.Config.Budget.BusinessMs.XyCompletion, run.ClockId);
                if (work is { State: CommitState.Committed })
                    await motion.FinishCaptureWindowAsync(inspection,
                        new(run.RunId, moved.OperationId, [work.WriteId], true), false, release, cancellationToken);
                else
                {
                    try { await motion.CloseFailedCaptureWindowAsync(inspection,
                        failureReason ?? "CaptureWorkNotCommitted", release, cancellationToken); }
                    catch (Exception cleanup)
                    {
                        RuntimeDiagnostics.Record("ThreeD", "FailureCleanupUnconfirmed", run.RunId,
                            new { moved.OperationId, originalFailure = failureReason, holdsDevice = true }, cleanup);
                        if (failureReason is null) throw;
                    }
                }
            }
        }
    }
}
