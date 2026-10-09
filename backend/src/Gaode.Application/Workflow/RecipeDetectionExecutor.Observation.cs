using System.Text.Json;
using Gaode.Application.Acquisition;
using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Application.Timing;
using Gaode.Domain.Station01;
using Gaode.Diagnostics;

namespace Gaode.Application.Workflow;

public sealed partial class RecipeDetectionExecutor
{
    private sealed record InitialCaptureRecord(Guid CaptureId, Guid OperationId, Guid MediaId,
        bool Ended, bool MediaTaken, CaptureRequest RequestedCapture, CorrelatedCaptureFact CaptureFact);

    private async Task<CorrelatedCaptureFact?> ReadInitialExclusionCaptureAsync(
        DetectionRequest request, TrayObservation initial, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        var remaining = request.DeadlineUtc - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero) return null;
        deadline.CancelAfter(remaining);
        var writes = await traces.GetWritesAsync(request.RunId, deadline.Token);
        bool Committed(PersistedWrite write) => write.RunId == request.RunId && write.State == CommitState.Committed &&
            write.PayloadDigest == Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(write.PayloadJson)));
        var observation = writes.SingleOrDefault(w => w.WriteId == request.InitialObservationWriteId &&
            w.Kind == WriteKind.AlgorithmFact && Committed(w));
        if (observation is null) return null;
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var fact = JsonSerializer.Deserialize<AlgorithmFactPayload>(observation.PayloadJson, json);
        if (fact is not { State: AlgorithmState.Success } || fact.RunId != request.RunId ||
            fact.CallId != initial.CallId || fact.CaptureId != initial.CaptureId || fact.Origin != initial.Source ||
            JsonSerializer.Serialize(JsonSerializer.Deserialize<TrayObservation>(fact.RawResultJson)) != JsonSerializer.Serialize(initial))
            return null;
        var captures = writes.Where(w => w.Kind == WriteKind.CaptureFact && w.Revision < observation.Revision && Committed(w))
            .Select(w => (Write: w, Capture: JsonSerializer.Deserialize<InitialCaptureRecord>(w.PayloadJson, json)))
            .Where(x => x.Capture?.CaptureId == initial.CaptureId).ToArray();
        if (captures.Length != 1 || captures[0].Capture is not { Ended: true, MediaTaken: true } captured ||
            captured.CaptureFact is not { } actual || captured.RequestedCapture is not { Role: CaptureRole.ThreeD } command ||
            captured.OperationId != actual.OperationId || command.CaptureId != initial.CaptureId ||
            !command.Envelope.IsValid || command.Envelope.RunId != request.RunId ||
            !AcquisitionContract.MatchesFact(actual, command, request.ConnectionEpoch) ||
            !actual.CameraOrigin.IsKnown || !actual.LightOrigin.IsKnown) return null;
        var mediaExists = writes.Where(w => w.Kind == WriteKind.Media && w.Revision < captures[0].Write.Revision && Committed(w))
            .Select(w => JsonSerializer.Deserialize<MediaRef>(w.PayloadJson, json)).Any(m => m is not null &&
                m.RunId == request.RunId && m.CaptureId == initial.CaptureId && m.MediaId == captured.MediaId &&
                m.StorageState == "FileCompleted" && request.InputMediaReferences.Contains($"media://{m.MediaId:D}", StringComparer.Ordinal));
        return mediaExists ? actual : null;
    }

    private async Task<(TrayObservation Observation, CorrelatedCaptureFact Capture, string Reference)> ObserveAfterPlacementAsync(
        DetectionRequest request, RecipeStep step, Guid transitionId, CancellationToken token)
    {
        var configuration = request.MotionConfiguration ?? throw new InvalidOperationException("ObservationConfigurationMissing");
        var inputs = request.Inputs ?? throw new InvalidOperationException("FrozenExecutionInputsMissing");
        var bound = inputs.TrayPoseCapability;
        if (bound is null || bound.Requirement.Purpose != AlgorithmPurpose.TrayPose ||
            !algorithm.Origin.IsKnown || bound.ProviderVersion != algorithm.Origin.VersionRef)
            throw new InvalidOperationException("FrozenTrayPoseBindingMissing");
        var poseBudget = request.FrozenBusinessDurations?.TrayPoseAlgorithm;
        if (poseBudget is null or <= 0) throw new InvalidOperationException("TrayPoseBudgetMissing");
        var point = configuration.Motion.Points.ThreeD;
        var moved = await MoveAndBeginAsync(request, step, point, configuration.Motion.CoordinateSource,
            configuration.Motion.CoordinateDigest, token, "3D");
        try
        {
            var captureId = Guid.NewGuid();
            var operation = Guid.NewGuid();
            var intent = await SaveTraceAsync(request, WriteKind.CaptureIntent, new OperationIntentPayload(operation,
                "PostPlacement3D", request.Attempt, null, captureId, point.Id, request.PlanRevision, Guid.Empty), token);
            var config = configuration.Capture3d;
            var maxBytes = camera.GetMaxCaptureBytes(config.BindingId, config.MaxCaptureBytes);
            using var reservation = media.ReserveCapture(captureId, "3D", maxBytes);
            var capture = new CaptureRequest(Envelope(request, operation), captureId, CaptureRole.ThreeD,
                point.Id, point.Version, config.Scope.Id, config.Scope.Version, config.BindingId,
                config.LightBindingId, intent.WriteId, maxBytes)
            {
                AcquisitionSessionId = moved.Session!.SessionId, AcquisitionOperationId = moved.Session.Request.Correlation.OperationId,
                LightExecution = configuration.LightExecution,
                PublicSettings = new(configuration.Id, configuration.Version, config.Parameters.ExposureUs, config.Parameters.LightLevel)
            };
            var gate = new CaptureEvidenceGate();
            var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var epoch = camera.GetConnectionEpoch(config.BindingId);
            void OnCapture(CaptureEvent value)
            {
                if (!AcquisitionContract.Matches(value, capture, epoch) || value.Request.Envelope.Attempt != request.Attempt) return;
                if (value.Kind is CaptureEventKind.Failed or CaptureEventKind.Unknown) ended.TrySetException(new IOException(value.ErrorCode ?? "ObservationCaptureFailed"));
                if (gate.Observe(value)) ended.TrySetResult();
            }
            RuntimeDiagnostics.Record("TrayObservation", "CaptureRequested", request.RunId,
                new { transitionId, step.Sequence, captureId, purpose = "PostPlacementCheck", checkRound = step.CoordinateEpoch });
            using var captureDeadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            captureDeadline.CancelAfter(inputs.CostProfile.CaptureWaitMs);
            var received = await new CameraAcquisitionService(camera, media).ReceiveAsync(capture, captureDeadline.Token, OnCapture);
            await ended.Task.WaitAsync(Remaining(request.DeadlineUtc - DateTimeOffset.UtcNow,
                TimeSpan.FromMilliseconds(request.FrozenBusinessDurations!.Capture3d)), token);
            var (buffer, format) = (received.Bytes, received.Format);
            var captureFact = received.Fact;
            var saved = await media.SaveCaptureAsync(request.RunId, captureId, "3D", config.Scope.Version, point.Version,
                buffer, format, captureFact.MediaSource, captureFact, token);
            saved = saved with { Purpose = request.Purpose };
            var mediaCommit = await SaveTraceAsync(request, WriteKind.Media, saved, token);
            var captureCommit = await SaveTraceAsync(request, WriteKind.CaptureFact, new { kind = "PostPlacementCaptureCompleted",
                transitionId, step.Sequence, captureId, saved.MediaId, captureFact }, token);
            await media.MarkCommittedAsync(saved, token);
            var captureCompletion = await CaptureCompletionEvidence.FromCommittedAsync(traces,media,capture,received,saved,mediaCommit,captureCommit,token);
            var frozenModule = request.AlgorithmConfiguration is null ? null : Gaode.Application.Algorithms.AlgorithmInputPolicy.RequireModule(
                request.AlgorithmConfiguration,AlgorithmRole.TrayPose,1,bound.Requirement.ParametersVersion,bound.CapabilityId,bound.CapabilityVersion);
            if(frozenModule is not null || algorithm.InputRepresentation(AlgorithmRole.TrayPose) != AlgorithmInputRepresentation.NativeMedia)
            {
                saved = await media.PrepareAlgorithmInputAsync(saved,token);
                if(saved.Format!="ply") throw new InvalidDataException("TrayPoseAlgorithmPlyRequired");
                await SaveTraceAsync(request,WriteKind.Media,saved,token);
                await media.MarkCommittedAsync(saved,token);
            }

            var call = Guid.NewGuid();
            operation = Guid.NewGuid();
            var envelope = Envelope(request, operation);
            var algorithmIntent = await SaveTraceAsync(request, WriteKind.AlgorithmIntent,
                new AlgorithmIntentPayload(call, operation, request.Attempt, request.RunId, captureId, [saved.MediaId],
                    configuration.Version, config.Scope.Version, bound.Requirement.ParametersVersion, bound.CapabilityId,
                    bound.CapabilityVersion, bound.ProviderVersion, request.SessionId, request.ClockId, envelope.StartTick,
                    envelope.DueTick, poseBudget.Value, "AllRelatedPutBackCommitted+MediaCommitted"), token);
            var context = new TrayObservationContext(request.TrayId, TrayObservationPurpose.PostPlacementCheck,
                step.CoordinateEpoch, transitionId);
            var command = new AlgorithmRequest(envelope, call, captureId, AlgorithmRole.TrayPose, [saved],
                bound.Requirement.ParametersVersion, bound.CapabilityId, bound.CapabilityVersion,
                algorithmIntent.WriteId, "AllRelatedPutBackCommitted+MediaCommitted") { ObservationContext = context,
                    FrozenModule = frozenModule, AlgorithmConfigurationDigest = request.AlgorithmConfigurationDigest };
            using var algorithmCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            var origin = algorithm.Origin;
            await using var dispatch = await synchronousAlgorithms.DispatchSynchronousAsync(command,request.TrayId,
                poseBudget.Value,inputs.CostProfile.InputReleaseWaitMs,request.CriticalSaveBudgetMs,_ => {},algorithmCancellation.Token);
            AlgorithmEvent result;
            try { result = await dispatch.Result.WaitAsync(algorithmCancellation.Token); }
            catch
            {
                await algorithmCancellation.CancelAsync();
                // Cancellation does not release inputs. The adapter still owns them until Exited.
                throw;
            }
            await dispatch.Exited.WaitAsync(Remaining(request.DeadlineUtc - DateTimeOffset.UtcNow,
                dispatch.RemainingReleaseWait), token);
            var observation = result.Observation;
            var valid = result.Kind == AlgorithmEventKind.Result && observation is { HasCompleteCoverage: true } &&
                request.InitialObservation is { } initial && observation.HasSamePhysicalMapping(initial) &&
                observation.RunId == request.RunId && observation.TrayId == request.TrayId &&
                observation.CallId == call && observation.CaptureId == captureId && observation.Source == origin &&
                observation.Purpose == context.Purpose && observation.CheckRound == context.CheckRound &&
                observation.RelatedTransitionId == transitionId;
            var fact = await SaveTraceAsync(request, WriteKind.AlgorithmFact, new AlgorithmFactPayload(call,
                valid ? AlgorithmState.Success : AlgorithmState.InvalidResult, true, JsonSerializer.Serialize(observation),
                valid ? "PostPlacementObserved" : result.ErrorCode ?? "ObservationInvalid", "Dispatched",
                WorkerSessionId: result.WorkerSessionId) { Origin = origin, RunId = request.RunId, CaptureId = captureId }, token);
            await motion.FinishCaptureWindowAsync(moved.Session!, captureCompletion.ForWindow(moved.Session!,request.TrayId), false, ActionWindows.FromUtc(TimeProvider.System, DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow + Remaining(request.DeadlineUtc - DateTimeOffset.UtcNow,
                        TimeSpan.FromMilliseconds(inputs.CostProfile.AcquisitionReleaseMs)), request.ClockId), token);
            motion.ConfirmCompleted(moved.ActionId);
            if (!valid) throw new InvalidDataException("PostPlacementObservationInvalid");
            await AppendAsync(request, operation, step, StageEventType.Executing,
                new { kind = "TrayObservationCommitted", transitionId, step.Sequence, observation, writeId = fact.WriteId }, token);
            return (observation!, captureFact, $"write://{fact.WriteId:D}");
        }
        catch (Exception error)
        {
            motion.MarkUnknown(moved.ActionId);
            RuntimeDiagnostics.Record("TrayObservation", "RecheckFailed", request.RunId,
                new { transitionId, step.Sequence, step.CoordinateEpoch, request.DeadlineUtc }, error);
            throw;
        }
    }
}
