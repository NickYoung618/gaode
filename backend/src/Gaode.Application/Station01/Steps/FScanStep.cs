using System.Text.Json;
using Gaode.Application.Acquisition;
using Gaode.Application.Algorithms;
using Gaode.Application.Capabilities;
using Gaode.Application.Ports;
using Gaode.Application.Motion;
using Gaode.Domain.Station01;
using Gaode.Diagnostics;
using Gaode.Application.Timing;

namespace Gaode.Application.Station01.Steps;

public sealed record FEvidence(MoveEvidence Move, MediaRef Media,
    AlgorithmOutcome Algorithm, FCodeResult Code);

public sealed class FScanStep(FixedMoveStep move, AcquisitionCoordinator acquisition,
    AlgorithmRuntime algorithms, MotionCoordinator motion, CapabilityRegistry capabilities)
{
    public async Task<FEvidence> ExecuteAsync(RunExecution run, ControlLatch control,
        CancellationToken cancellationToken)
    {
        var config = run.Config.Public;
        var observation = run.InitialObservation;
        if (observation is not { IsValid: true, Purpose: TrayObservationPurpose.InitialPreparation, FLocation: { IsValid: true } location } ||
            run.InitialObservationWriteId is null || observation.RunId != run.RunId ||
            location.Unit != config.Motion.Unit || location.Frame != config.Motion.Frame)
            throw new InvalidOperationException("CommittedInitialFLocationRequired");
        var point = config.Motion.Points.F with { X = location.X, Y = location.Y };
        await run.SaveAsync(WriteKind.Audit, new { kind = "FLocationResolvedFromInitialObservation", observation.ObservationId,
            observation.CaptureId, observation.CallId, sourceWriteId = run.InitialObservationWriteId, location, point }, cancellationToken: cancellationToken);
        var moved = await move.ExecuteAsync(run, control, point, "F", cancellationToken);
        if (control.CancelRequested || control.SafetyFault) throw new InvalidOperationException("控制准入已关闭");
        var inspection = await motion.OpenCaptureWindowAsync(new(moved.Evidence.Correlation, CaptureRole.F,
            point, moved.Evidence.Positions.Single(), moved.Window), false, cancellationToken);
        CommitReceipt? captureCommit = null;
        PersistedCapture? captured = null;
        try
        {
            captured = await acquisition.CaptureAsync(run, CaptureRole.F, point,
                null, null, config.CaptureF.BindingId, config.CaptureF.LightBindingId,
                config.CaptureF.MaxCaptureBytes, cancellationToken, inspection);
            var media = captured.Media;
            var outcome = await algorithms.InvokeAsync(run, AlgorithmRole.FDecode,
                media.CaptureId, [media], "NotApplicable", cancellationToken);
            AlgorithmOutcomePolicy.RequireFinite(outcome.State);
            var response = outcome.State == AlgorithmState.Success && outcome.Event?.Kind == AlgorithmEventKind.Result;
            Gaode.Application.Recipes.DecodedTrayCode? decodedCode = null;
            (bool Valid, string? Value) Parse(string raw)
            {
                if (config.Parser.Capability is null) return (true, raw);
                var decoded = capabilities.Decode(config.Parser.Capability, config.Purpose, raw);
                decodedCode = decoded;
                return (decoded is not null, decoded?.ParsedCode);
            }
            var result = FCodePolicy.Evaluate(response,
                response ? outcome.Event!.RawCodes : null, config.Parser.Capability is null ? null : Parse);
            captureCommit = await run.SaveAsync(WriteKind.AlgorithmFact,
                new AlgorithmFactPayload(outcome.CallId, outcome.State, response,
                    JsonSerializer.Serialize(outcome.Event?.RawCodes), outcome.Decision,
                    outcome.DispatchEvidence, outcome.DispatchTick, outcome.AcceptedTick,
                    outcome.WorkerSessionId) { Origin = outcome.Origin, RunId = run.RunId, CaptureId = media.CaptureId, DecodedCode = decodedCode },
                cancellationToken: cancellationToken);
            run.RecordCommittedFSource(new(run.RunId, media.CaptureId, outcome.CallId, outcome.Origin,
                captureCommit.WriteId, captureCommit.CommittedRevision!.Value));
            return new(moved, media, outcome, result);
        }
        catch (Exception error)
        {
            RuntimeDiagnostics.Record("FScan", "FailedBeforeInspectionCleanup", run.RunId,
                new { moved.OperationId, moved.DeviceEpoch,
                    disposition = "PreserveOriginalFailure_AcquisitionReleaseMayAlsoFail" }, error);
            throw;
        }
        finally
        {
            if (captureCommit is { State: CommitState.Committed } && !control.CancelRequested && !control.SafetyFault && !cancellationToken.IsCancellationRequested)
                await motion.FinishCaptureWindowAsync(inspection,
                    captured!.Completion.ForWindow(inspection, StartRunContextParser.Parse(run.ContextJson).TrayId), false,
                    ActionWindows.Start(run.Clock, run.Config.Budget.BusinessMs.XyCompletion, run.ClockId), cancellationToken);
        }
    }
}
