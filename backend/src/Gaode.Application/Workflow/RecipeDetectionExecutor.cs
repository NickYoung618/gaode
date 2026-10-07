using Gaode.Application.Timing;
using Gaode.Application.Acquisition;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Application.Workflow;
using Gaode.Application.Motion;
using Gaode.Application.Station01;
using Gaode.Domain.Station01;
using Gaode.Diagnostics;


namespace Gaode.Application.Workflow;

/// <summary>Executes the frozen business plan through its bound device ports and required saves.</summary>
public sealed partial class RecipeDetectionExecutor(ICapturePort camera, IAlgorithmPort algorithm,
    IMediaStore media, IStageEventStore events, ITraceWriter writer,
    ITraceQuery traces, MotionCoordinator motion, IPhysicalHandlingPort? handling = null,
    NormalPauseBoundary? pause = null, IPlcStageActionPort? stageActions = null,
    RotationExecutionConfiguration? rotationConfiguration = null,
    TrayAnomalyDecisionService? anomalyDecisions = null, Station01Coordinator? coordinator = null) : IDetectionPort
{
    private static ResultSource SourceOf(ComponentExecutionOrigin origin) => origin.Source switch {
        ComponentEvidenceSource.Real => ResultSource.Real, ComponentEvidenceSource.Virtual => ResultSource.Virtual,
        ComponentEvidenceSource.Simulated => ResultSource.Simulated, ComponentEvidenceSource.Test => ResultSource.Test,
        _ => ResultSource.Fallback };

    public ValueTask<DetectionPortResult> ExecuteAsync(DetectionRequest request,
        CancellationToken cancellationToken) => new(RuntimeDiagnostics.ObserveAsync(
            "DetectionPort", request.RunId, new { request.OperationId, request.Attempt,
                request.ConnectionEpoch, request.PlanRevision, request.DeadlineUtc },
            () => ExecuteCoreAsync(request, cancellationToken).AsTask(),
            r => new { request.OperationId, kind = r.Kind.ToString(), r.ErrorCode,
                r.ResultReference }, r => r.Kind != DetectionResultKind.Completed));

    private async ValueTask<DetectionPortResult> ExecuteCoreAsync(DetectionRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!request.IsValid || request.Plan is null ||
            RecipePlanRevision.Compute(request.Plan) != request.PlanRevision)
            return NotStarted(request, "FrozenDetectionPlanMissing");
        if (request.Plan.InspectionKind == RecipeInspectionKind.SpecialRotation && request.Plan.Steps.Any(s =>
                (request.Scope is null || request.Scope.Includes(s)) && request.InitialObservation?.Slots.Any(o =>
                    o.PhysicalSlotIndex == s.PhysicalSlotIndex && o.Presence == TrayPresence.Present && o.Pose == TrayPose.Normal) == true) &&
            (request.Scope is null || stageActions is null || rotationConfiguration?.Basis is not { } rotationBasis ||
                rotationBasis.Purpose != request.Purpose || string.IsNullOrWhiteSpace(rotationBasis.SourceReference)))
            return NotStarted(request, "RotationMechanicalBasisMissing");
        var scopedSteps = request.Plan.Steps.Where(s => request.Scope is null || request.Scope.Includes(s)).ToArray();
        if (request.FrozenBusinessDurations is not { PlcAcceptance: > 0, XyCompletion: > 0 })
            return NotStarted(request, "FrozenActionBudgetsMissing");
        if (request.CriticalSaveBudgetMs <= 0) return NotStarted(request, "FrozenDetectionSaveBudgetMissing");
        if (request.Plan.Steps.Any(s => s.Kind == RecipeStepKind.FlipMember) &&
            (request.FrozenBusinessDurations.FlipCompletion is null or <= 0 || request.FrozenBusinessDurations.PutBackCompletion is null or <= 0))
            return NotStarted(request, "FlipOrPutBackBudgetMissing");
        if (request.Plan.Steps.Any(s => s.Kind == RecipeStepKind.RescanWholeTray) &&
            request.FrozenBusinessDurations.TrayPoseAlgorithm is null or <= 0)
            return NotStarted(request, "TrayPoseBudgetMissing");
        if (request.InitialObservation is not { HasCompleteCoverage: true, Purpose: TrayObservationPurpose.InitialPreparation } initial ||
            initial.RunId != request.RunId || initial.TrayId != request.TrayId ||
            request.InitialObservationWriteId is not { } observationWriteId || observationWriteId == Guid.Empty)
            return NotStarted(request, "CommittedInitialObservationMissing");
        var lastObservation = initial;
        var poseBasis = initial.Slots.Where(s => s.Presence == TrayPresence.Present && s.Pose == TrayPose.Abnormal)
            .ToDictionary(s => s.PhysicalSlotIndex, s => (Id: initial.ObservationId, Reference: $"write://{observationWriteId:D}"));
        var participation = SlotParticipation.Apply([], initial.Slots);
        bool Participates(int? physicalSlot) => physicalSlot is { } index && participation.TryGetValue(index, out var slot) &&
            slot.State == SlotParticipationState.Participating;
        if (request.Plan.Steps.Where(s => s.Kind == RecipeStepKind.Capture).Any(s =>
            s.PhysicalSlotIndex is not { } index || !participation.TryGetValue(index, out var slot) || slot.State == SlotParticipationState.Unknown))
            return NotStarted(request, "InitialObservationCoverageUnknown");
        if (request.Inputs is not { IsValid: true } frozen || frozen.RunId != request.RunId ||
            frozen.TrayId != request.TrayId || frozen.PlanRevision != request.PlanRevision)
            return NotStarted(request, "FrozenExecutionInputsMissing");
        var planProblem = RecipeExecutionCoordinator.ValidateDetectionPlan(request.Plan);
        if (planProblem is not null) return NotStarted(request, planProblem);
        var targetProblem = RecipeExecutionCoordinator.ValidateTargets(request);
        if (targetProblem is not null) return NotStarted(request, targetProblem);
        var steps = scopedSteps.Where(x => x.Kind == RecipeStepKind.Capture)
            .OrderBy(x => x.Sequence).ToArray();
        if (steps.Length == 0 || request.ExpectedObjects is not { Count: > 0 })
            return NotStarted(request, "DetectionStepsMissing");
        var results = new Dictionary<string, List<(int LocalFace, string? StageId, string Disposition, string Evidence)>>();
        var faces = new FaceResultAggregator();
        var references = new List<string>();
        var captures = new List<CorrelatedCaptureFact>();
        var dispatchedOrigin = ComponentExecutionOrigin.Unknown;
        var evidenceBasis = DetectionEvidenceBasis.ProductInspection;
        DetectionPortResult Failure(string error, DetectionResultKind kind = DetectionResultKind.Failed) =>
            new(request, motion.IsHeld(request.RunId) && motion.Unknown ? DetectionResultKind.UnknownHeld : kind,
                null, [], dispatchedOrigin.Source switch {
                ComponentEvidenceSource.Real => ResultSource.Real, ComponentEvidenceSource.Virtual => ResultSource.Virtual,
                ComponentEvidenceSource.Simulated => ResultSource.Simulated, ComponentEvidenceSource.Test => ResultSource.Test,
                _ => ResultSource.Fallback }, ResultQuality.Unknown, error, DateTimeOffset.UtcNow,
                [$"detection-error://{error}"]) { AlgorithmOrigin = dispatchedOrigin, CaptureFacts = captures.ToArray(),
                    LastObservation = lastObservation, SlotParticipation = participation };
        var validECodes = new HashSet<string>(StringComparer.Ordinal);
        Guid? transitionRound = null;
        var phase = "PlanValidated";
        Guid? currentCapture = null, currentCall = null;
        Guid? activeMotionAction = null;
        try
        {
            if (!steps.Any(s => Participates(s.PhysicalSlotIndex)))
            {
                if (steps.Any(s => participation[s.PhysicalSlotIndex!.Value].State != SlotParticipationState.PoseExcluded))
                    return Failure("NoParticipatingConfiguredSlots");
                var savedCapture = await ReadInitialExclusionCaptureAsync(request, initial, cancellationToken);
                if (savedCapture is null) return Failure("InitialPoseExclusionEvidenceMissing");
                captures.Add(savedCapture);
                references.Add($"write://{observationWriteId:D}");
                dispatchedOrigin = initial.Source;
                evidenceBasis = DetectionEvidenceBasis.InitialPoseExclusion;
            }
            await AppendAsync(request, Guid.NewGuid(), steps[0], StageEventType.Executing,
                new { kind = "DetectionRequirements", request.Plan.UnitKind,
                    targets = steps.Where(s => s.Camera != "E").Select(s => new {
                        objectId = s.MemberId ?? s.UnitId, s.UnitId, s.LocalFace,
                        stepSequence = s.Sequence, camera = s.Camera }) }, cancellationToken);
            foreach (var step in scopedSteps.OrderBy(x => x.Sequence))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (step.Kind != RecipeStepKind.RescanWholeTray && !Participates(step.PhysicalSlotIndex)) continue;
                if (pause is not null)
                    await pause.WaitAsync(request.RunId, RunState.Detection,
                        "DetectionStep:" + step.Sequence, request.ConnectionEpoch, request.DeadlineUtc,
                        (kind, _, ct) => AppendAsync(request, Guid.NewGuid(), step, StageEventType.Executing,
                            new { kind, step.Sequence, step.Kind, sameRun = true }, ct), cancellationToken);
                if (DateTimeOffset.UtcNow >= request.DeadlineUtc)
                    return Failure("DetectionDeadlineExceeded", DetectionResultKind.TimedOut);
                if (step.Kind == RecipeStepKind.Rotate)
                {
                    var basis = rotationConfiguration!.Basis!;
                    var correlation = new ActionCorrelation(request.RunId, Guid.NewGuid(), Guid.NewGuid(), request.Attempt,
                        request.SessionId, request.ConnectionEpoch, request.SnapshotId, request.PlanRevision,
                        request.TrayId, step.MemberId ?? step.UnitId, PhysicalSlotIndex: step.PhysicalSlotIndex);
                    var action = new PlcStageActionRequest(correlation, request.StationId, request.LineId,
                        PlcWorkflowStage.Rotate, Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { step.AngleDeg, step.StageId, basis }))),
                        ActionWindows.FromUtc(TimeProvider.System, request.StageStartedAtUtc ?? DateTimeOffset.UtcNow, request.DeadlineUtc, request.ClockId), request.IdempotencyKey + ":rotate:" + step.Sequence,
                        TargetPurpose: request.Purpose)
                    { RotationTarget = new(step.AngleDeg!.Value, step.StageId!, basis.AngleToleranceDeg, basis.SourceReference) };
                    await AppendAsync(request, action.OperationId, step, StageEventType.IntentRecorded,
                        new { kind = "RotationIntent", step.Sequence,step.StageId,request.Scope,action.Correlation, action.RotationTarget }, cancellationToken);
                    var rotated = await stageActions!.ExecuteAsync(action, cancellationToken);
                    if (!rotated.IsCompleted || rotated.Request != action)
                        return Failure(rotated.ErrorCode ?? "RotationCompletionUnconfirmed",
                            rotated.HoldsDevice ? DetectionResultKind.UnknownHeld : DetectionResultKind.Failed);
                    await SaveTraceAsync(request, WriteKind.ActionFact,
                        new { kind = "RotationReached", step.Sequence, step.StageId, rotated.Evidence }, cancellationToken);
                    await AppendAsync(request, action.OperationId, step, StageEventType.Executing,
                        new { kind = "RotationReached", step.Sequence, step.StageId, rotated.Evidence }, cancellationToken);
                    continue;
                }
                if (step.Kind == RecipeStepKind.FlipMember)
                {
                    phase = "FlipAndPutBack";
                    transitionRound ??= Guid.NewGuid();
                    await ExecuteTransitionAsync(request, step, transitionRound.Value, cancellationToken);
                    continue;
                }
                if (step.Kind == RecipeStepKind.RescanWholeTray)
                {
                    if (transitionRound is null) continue; // No participating entity was moved in this round.
                    phase = "PostPlacementObservation";
                    var checkedTray = await ObserveAfterPlacementAsync(request, step, transitionRound.Value, cancellationToken);
                    lastObservation = checkedTray.Observation;
                    captures.Add(checkedTray.Capture);
                    references.Add(checkedTray.Reference);
                    foreach(var abnormal in lastObservation.Slots.Where(s => s.Presence == TrayPresence.Present && s.Pose == TrayPose.Abnormal))
                        poseBasis.TryAdd(abnormal.PhysicalSlotIndex,(lastObservation.ObservationId,checkedTray.Reference));
                    participation = SlotParticipation.Apply(participation.Values, lastObservation.Slots);
                    transitionRound = null;
                    if (participation.Values.Any(s => s.State == SlotParticipationState.Unknown))
                        return Failure("PostPlacementObservationCoverageUnknown");
                    if (lastObservation.Slots.Any(s => s.Presence == TrayPresence.Present && s.Pose == TrayPose.Abnormal))
                    {
                        var control = coordinator?.Control(request.RunId) ?? throw new InvalidOperationException("TrayAnomalyControlUnavailable");
                        var decision = await (anomalyDecisions ?? throw new InvalidOperationException("TrayAnomalyDecisionUnavailable"))
                            .WaitAsync(lastObservation, control, async (choice, ct) => {
                                var id = Guid.NewGuid();
                                var payload = JsonSerializer.Serialize(new { kind = "TrayAnomalyDecision", decision = choice },
                                    new JsonSerializerOptions(JsonSerializerDefaults.Web));
                                using var saveDeadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
                                saveDeadline.CancelAfter(Remaining(request.DeadlineUtc - DateTimeOffset.UtcNow,
                                    TimeSpan.FromMilliseconds(request.CriticalSaveBudgetMs)));
                                var saved = await events.AppendAsync(new(id, request.RunId, request.TrayId,
                                    request.StationId.ToString(), request.LineId.ToString(), WholeTrayWorkflowStage.Detection,
                                    choice.DecisionId, 1, request.ConnectionEpoch, StageEventType.Executing, DateTimeOffset.UtcNow,
                                    ResultSource.HostDerived, ResultQuality.Derived, null,
                                    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))), payload,
                                    $"tray-decision:{choice.DecisionId:N}:{choice.State}", request.PlanRevision), saveDeadline.Token);
                                if (!saved.IsCommitted) throw new InvalidOperationException("TrayDecisionNotCommitted");
                                return $"stage-event://{saved.Event.EventId:D}";
                            }, cancellationToken);
                        if (decision.Choice == "ManualIntervention")
                            return new(request, DetectionResultKind.Executing, null, [], SourceOf(lastObservation.Source),
                                ResultQuality.Derived, null, DateTimeOffset.UtcNow) { EndBasisReference = decision.EvidenceReference,
                                    LastObservation = lastObservation, SlotParticipation = participation, CaptureFacts = captures,
                                    AlgorithmOrigin = lastObservation.Source };
                        await AppendAsync(request, Guid.NewGuid(), step, StageEventType.Executing,
                            new { kind = "PoseInspectionTerminated", lastObservation.ObservationId,
                                excluded = participation.Values.Where(s => s.State == SlotParticipationState.PoseExcluded).ToArray(),
                                previousInspectionFactsPreserved = true }, cancellationToken, "PoseInspectionTerminated");
                    }
                    continue;
                }
                var isECode = step.Kind == RecipeStepKind.ReadECode;
                if (step.Kind != RecipeStepKind.Capture && !isECode) continue;
                AcquisitionSession? inspection = null;
                {
                    var position = request.Plan.Steps.Single(x => x.Sequence == step.Sequence - 1);
                    var target = request.Targets!.Single(x => x.StepSequence == position.Sequence);
                    phase = "ProductMoveAndBeginInspection";
                    var moved = await MoveAndBeginAsync(request, position, target.Point, target.Source, target.ZBasis, cancellationToken,
                        isECode ? "E" : "Detection");
                    inspection = moved.Session;
                    activeMotionAction = moved.ActionId;
                }
                var captureId = Guid.NewGuid();
                currentCapture = captureId;
                currentCall = null;
                phase = "CaptureIntentSave";
                var captureOperation = Guid.NewGuid();
                await AppendAsync(request, captureOperation, step, StageEventType.IntentRecorded,
                    new { captureId, step.Sequence, step.MemberId, step.CaptureProfile }, cancellationToken);
                var captureIntent = await SaveTraceAsync(request, WriteKind.CaptureIntent,
                    new OperationIntentPayload(captureOperation, isECode ? "CaptureE" : "DetectionCapture", request.Attempt,
                        null, captureId, $"step:{step.Sequence}:{step.MemberId}",
                        request.PlanRevision, Guid.Empty), cancellationToken);
                var mediaKind = isECode ? "E" : "Detection";
                var maxCaptureBytes = camera.GetMaxCaptureBytes(step.Camera ?? throw new InvalidDataException("CaptureCameraMissing"), 4 * 1024 * 1024);
                using var reservation = media.ReserveCapture(captureId, mediaKind, maxCaptureBytes);
                var envelope = Envelope(request, captureOperation);
                var settings = ReadCaptureSettings(request.Plan, step);
                var capture = new CaptureRequest(envelope, captureId, isECode ? CaptureRole.E : CaptureRole.Detection,
                    step.MemberId ?? step.UnitId, request.PlanRevision, step.SlotId,
                    request.PlanRevision, step.Camera ?? throw new InvalidDataException("CaptureCameraMissing"), settings.LightChannel,
                    captureIntent.WriteId,
                    maxCaptureBytes) { DetectionSettings = settings };
                var finished = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                var captureGate = new CaptureEvidenceGate();
                var captureEpoch = camera.GetConnectionEpoch(capture.CameraBindingId);
                void OnCapture(CaptureEvent value)
                {
                    if (!AcquisitionContract.Matches(value, capture, captureEpoch) || value.Request.Envelope.Attempt != capture.Envelope.Attempt)
                        return;
                    if (value.Kind is CaptureEventKind.Failed or CaptureEventKind.Unknown)
                        finished.TrySetException(new IOException(value.ErrorCode ?? "CaptureFailed"));
                    if (captureGate.Observe(value)) finished.TrySetResult(true);
                }
                phase = "CaptureRequestAndWait";
                RuntimeDiagnostics.Record("DetectionCapture", "Requesting", request.RunId,
                    new { request.OperationId, captureOperation, captureId, step.Sequence, step.Camera,
                        settings, request.DeadlineUtc, maximumWaitMs = request.Inputs!.CostProfile.CaptureWaitMs });
                await Task.Delay(TimeSpan.FromMilliseconds(settings.SettleMs), cancellationToken);
                using var captureDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                captureDeadline.CancelAfter(request.Inputs!.CostProfile.CaptureWaitMs);
                var received = await new CameraAcquisitionService(camera, media).ReceiveAsync(capture, captureDeadline.Token, OnCapture);
                var wait = request.DeadlineUtc - DateTimeOffset.UtcNow;
                await finished.Task.WaitAsync(
                    Remaining(wait, TimeSpan.FromMilliseconds(request.Inputs!.CostProfile.CaptureWaitMs)), cancellationToken);
                var (buffer, format) = (received.Bytes, received.Format);
                var captureFact = received.Fact;
                captures.Add(captureFact);
                phase = "MediaSave";
                var saved = await media.SaveCaptureAsync(request.RunId, captureId, mediaKind,
                    request.PlanRevision, request.PlanRevision, buffer, format,
                    captureFact.MediaSource, captureFact, cancellationToken);
                saved = saved with { Purpose = request.Purpose };
                await SaveTraceAsync(request, WriteKind.Media, saved, cancellationToken);
                await SaveTraceAsync(request, WriteKind.CaptureFact,
                    new { kind = "ConfiguredCaptureCompleted", captureId, saved.MediaId,
                        step.Sequence, objectId = step.MemberId ?? step.UnitId, step.LocalFace,
                        step.Camera, step.CaptureProfile, requestedCaptureSettings = settings,
                        captureEnded = true, captureFact, requestedCapture = capture }, cancellationToken);
                await media.MarkCommittedAsync(saved, cancellationToken);
                var digest = Convert.ToHexString(SHA256.HashData(buffer));
                references.Add($"media://{saved.MediaId:D}");
                await AppendAsync(request, captureOperation, step, StageEventType.Executing,
                    new { captureId, saved.MediaId, saved.RelativeKey, saved.ByteLength,
                        sha256 = digest, step.Sequence, step.MemberId,
                        stepSequence = step.Sequence, camera = step.Camera,
                        objectId = step.MemberId ?? step.UnitId,
                        localFace = step.LocalFace, heightRound = step.CoordinateEpoch,
                        requestedCaptureSettings = settings }, cancellationToken);

                var callId = Guid.NewGuid();
                currentCall = callId;
                phase = "AlgorithmIntentSave";
                var algorithmOperation = Guid.NewGuid();
                await AppendAsync(request, algorithmOperation, step, StageEventType.IntentRecorded,
                    new { callId, captureId, saved.MediaId, step.Sequence,
                        step.AlgorithmProfile }, cancellationToken);
                var algorithmEnvelope = Envelope(request, algorithmOperation);
                var bound = Bound(request, step, fusion: false);
                var producer = algorithm.Origin;
                var algorithmIntent = await SaveTraceAsync(request,
                    WriteKind.AlgorithmIntent, new AlgorithmIntentPayload(callId,
                        algorithmOperation, request.Attempt, request.RunId, captureId,
                        [saved.MediaId], algorithmEnvelope.ConfigVersion, request.PlanRevision,
                        bound.Requirement.ParametersVersion, bound.CapabilityId,
                        bound.CapabilityVersion, bound.ProviderVersion, algorithmEnvelope.SessionId,
                        algorithmEnvelope.ClockId, algorithmEnvelope.StartTick, algorithmEnvelope.DueTick, request.Inputs!.CostProfile.AlgorithmWaitMs,
                        "FrozenRecipeCaptureStep+MediaFileCompleted+MediaMetadataCommitted"),
                    cancellationToken);
                var response = new TaskCompletionSource<AlgorithmEvent>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                var algorithmRequest = new AlgorithmRequest(algorithmEnvelope,
                    callId, captureId, isECode ? AlgorithmRole.EDecode : AlgorithmRole.Detection, [saved],
                    bound.Requirement.ParametersVersion, bound.CapabilityId,
                    bound.CapabilityVersion, algorithmIntent.WriteId, "FrozenRecipeCaptureStep",
                    new WorkerTargetIdentity(step.MemberId ?? step.UnitId,
                        step.LocalFace ?? 0, step.CoordinateEpoch, step.Camera ?? "") { StageId = step.StageId });
                void OnAlgorithm(AlgorithmEvent value)
                {
                    if (!AcquisitionContract.Matches(value, algorithmRequest) || value.Request.Envelope.Attempt != request.Attempt) return;
                    if (value.Kind is AlgorithmEventKind.Result or AlgorithmEventKind.Failed)
                        response.TrySetResult(value);
                }
                phase = "AlgorithmRequestAndWait";
                RuntimeDiagnostics.Record("DetectionAlgorithm", "Requesting", request.RunId,
                    new { request.OperationId, algorithmOperation, callId, captureId, step.Sequence,
                        request.DeadlineUtc, maximumWaitMs = request.Inputs!.CostProfile.AlgorithmWaitMs });
                var dispatch = await algorithm.RequestAsync(algorithmRequest, OnAlgorithm,
                    cancellationToken);
                dispatchedOrigin = producer;
                wait = request.DeadlineUtc - DateTimeOffset.UtcNow;
                AlgorithmEvent result;
                bool timedOut;
                if (isECode)
                {
                    timedOut = false;
                    try { result = await response.Task.WaitAsync(Remaining(wait, TimeSpan.FromMilliseconds(request.Inputs!.CostProfile.AlgorithmWaitMs)), cancellationToken); }
                    catch (TimeoutException) when (DateTimeOffset.UtcNow < request.DeadlineUtc)
                    {
                        await dispatch.Exited.WaitAsync(Remaining(request.DeadlineUtc - DateTimeOffset.UtcNow,
                            TimeSpan.FromMilliseconds(request.Inputs!.CostProfile.InputReleaseWaitMs)), cancellationToken);
                        result = new AlgorithmEvent(algorithmRequest, AlgorithmEventKind.Failed, ErrorCode: "EDecodeTimedOut");
                        timedOut = true;
                    }
                }
                else (result, timedOut) = await AwaitDetectionResultAsync(request, step,
                    algorithmOperation, algorithmRequest, response.Task, dispatch.Exited, cancellationToken);
                if (!isECode && (result.Kind != AlgorithmEventKind.Result ||
                    result.DetectionDisposition is not ("OK" or "NG" or "Pending"))
                    )
                    return Failure(result.ErrorCode ?? "DetectionAlgorithmInvalid");
                phase = "AlgorithmInputReleaseWait";
                await dispatch.Exited.WaitAsync(TimeSpan.FromMilliseconds(request.Inputs!.CostProfile.InputReleaseWaitMs), cancellationToken);
                phase = "AlgorithmFactSave";
                var imageCommit = await SaveTraceAsync(request, WriteKind.AlgorithmFact,
                    new AlgorithmFactPayload(callId, timedOut ? AlgorithmState.TimedOut :
                        result.Kind == AlgorithmEventKind.Failed ? AlgorithmState.Error : AlgorithmState.Success,
                        true, JsonSerializer.Serialize(new { result.DetectionDisposition, result.RawCodes, result.ErrorCode }),
                        timedOut ? "DetectionTimedOut" : "WorkerResult", timedOut ? "FinitePending" : "Accepted", WorkerSessionId: result.WorkerSessionId) { Origin = producer, RunId = request.RunId, CaptureId = result.Request.CaptureId },
                    cancellationToken);
                var evidence = $"worker://{result.WorkerSessionId:D}/{callId:D}";
                references.Add(evidence);
                await AppendAsync(request, algorithmOperation, step, StageEventType.Executing,
                    new { kind = "DetectionImageCommitted", callId, captureId, saved.MediaId, step.Sequence,
                        stepSequence = step.Sequence, objectId = step.MemberId ?? step.UnitId,
                        step.UnitId, request.Plan.UnitKind, step.LocalFace, step.StageId, camera = step.Camera,
                        step.AlgorithmProfile, requestedCaptureSettings = settings, captureFact,
                        technicalState = timedOut ? "TimedOut" : result.Kind == AlgorithmEventKind.Failed ? "Error" : "Success",
                        result.DetectionDisposition, result.WorkerSessionId, result.ErrorCode,
                        detailAvailability = new { parameters = "Provided", defects = "NotProvided", confidence = "NotProvided", measurement = "NotProvided" } }, cancellationToken);
                if (inspection is not null)
                {
                    phase = "AcquisitionRelease";
                    var remaining = Remaining(request.DeadlineUtc - DateTimeOffset.UtcNow,
                        TimeSpan.FromMilliseconds(request.Inputs!.CostProfile.AcquisitionReleaseMs));
                    await motion.FinishCaptureWindowAsync(inspection, new(request.RunId, inspection.Request.Correlation.OperationId,
                        [imageCommit.WriteId], true), false, ActionWindows.FromUtc(TimeProvider.System, DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow + remaining, request.ClockId), cancellationToken);
                    await SaveTraceAsync(request, WriteKind.ActionFact,
                        new { schemaVersion = "device-semantics/1", kind = "AcquisitionReleased", inspection.Request.Correlation.OperationId,
                            inspection.Request.Correlation.ConnectionEpoch, step.Sequence, step.MemberId,
                            step.SlotId, step.Camera, step.CoordinateEpoch,
                            observed = motion.Observe() }, cancellationToken);
                    await AppendAsync(request, inspection.Request.Correlation.OperationId, step, StageEventType.Executing,
                        new { schemaVersion = "device-semantics/1", kind = "AcquisitionReleased", step.Sequence,
                            inspection.Request.Correlation.ConnectionEpoch }, cancellationToken);
                    motion.ConfirmCompleted(activeMotionAction!.Value);
                    activeMotionAction = null;
                }
                if (isECode)
                {
                    var codes = result.RawCodes?.Where(code => !string.IsNullOrWhiteSpace(code)).ToArray() ?? [];
                    var code = result.Kind == AlgorithmEventKind.Result && codes.Length == 1 ? codes[0] : null;
                    var issue = code is null ? result.ErrorCode ?? (codes.Length > 1 ? "ECodeAmbiguous" : "ECodeNoResult") : null;
                    var boundIdentity = request.Plan.ECodeBindTo is "AssemblyId" or "GroupId" ||
                        request.Plan.ECodeBindTo == "physicalEntity" && request.Plan.UnitKind == "assembledEntity"
                        ? step.UnitId : step.MemberId ?? step.UnitId;
                    if (code is not null) validECodes.Add(boundIdentity);
                    await SaveTraceAsync(request, WriteKind.ActionFact,
                        new { kind = "ECodeBinding", callId, captureId, saved.MediaId,
                            objectId = step.MemberId ?? step.UnitId, step.UnitId, boundIdentity,
                            bindTo = request.Plan.ECodeBindTo, step.LocalFace,
                            step.Sequence, externalCode = code, issue, internalIdentityRetained = true,
                            faceSource = "ConfiguredScanCoordinate", resetConfirmed = inspection is not null }, cancellationToken);
                    await AppendAsync(request, Guid.NewGuid(), step, StageEventType.Executing,
                        new { kind = "ECodeBinding", callId, captureId, saved.MediaId,
                            objectId = step.MemberId ?? step.UnitId, step.UnitId, boundIdentity,
                            bindTo = request.Plan.ECodeBindTo, step.LocalFace, step.Sequence,
                            externalCode = code, issue, faceSource = "ConfiguredScanCoordinate" }, cancellationToken, issue);
                    continue;
                }
                var objectId = step.MemberId ?? step.UnitId;
                var pair = faces.Add(new FaceResultKey(objectId,
                    step.LocalFace ?? throw new InvalidDataException("CaptureFaceMissing"),
                    step.CoordinateEpoch) { StageId = step.StageId }, new FaceImageResult(
                        step.Camera ?? throw new InvalidDataException("CaptureCameraMissing"),
                        saved, result.DetectionDisposition!, evidence));
                if (pair is not null)
                {
                    phase = "FaceFusion";
                    var fused = await FuseAsync(request, step, pair, cancellationToken);
                    references.Add(fused.Evidence);
                    if (!results.TryGetValue(objectId, out var list)) results[objectId] = list = [];
                    list.Add((pair.Key.LocalFace, pair.Key.StageId, fused.Disposition, fused.Evidence));
                }
            }
            if (faces.IncompleteKeys.Count != 0)
                throw new InvalidDataException("FaceFusionInputMissing");
            var objects = request.ExpectedObjects.Where(x => scopedSteps.Any(s =>
                (request.Plan.UnitKind == "assembledEntity" ? s.UnitId : s.MemberId ?? s.UnitId) == x.ObjectId &&
                Participates(s.PhysicalSlotIndex))).Select(x =>
            {
                var values = request.Plan.UnitKind == "assembledEntity"
                    ? request.Plan.Steps.Where(step => step.Kind == RecipeStepKind.Capture && step.UnitId == x.ObjectId)
                        .Select(step => step.MemberId!).Distinct().SelectMany(id => results.TryGetValue(id, out var part)
                            ? part : throw new InvalidDataException("DetectionPartResultMissing")).ToList()
                    : results.TryGetValue(x.ObjectId, out var member) ? member : [];
                if (values.Count == 0)
                    throw new InvalidDataException("DetectionObjectResultMissing");
                return new DetectionObjectResult(x.ObjectId, x.Position,
                    x.ExpectedClassification,
                    values.Any(v => v.Disposition == "NG") ? "NG" :
                    values.Any(v => v.Disposition == "Pending") || request.Plan.ECodeRequiredForOk &&
                        !HasRequiredECode(request.Plan, x.ObjectId, validECodes) ? "Pending" : "OK",
                    values.Select(v => v.Evidence).ToArray());
            }).ToArray();
            {
                foreach (var decision in scopedSteps.Where(x => x.Kind == RecipeStepKind.DecideUnit))
                {
                    var unitObjects = request.Plan.Steps.Where(x =>
                            x.Kind == RecipeStepKind.PositionForCapture && x.UnitId == decision.UnitId)
                        .Where(x => Participates(x.PhysicalSlotIndex)).Select(x => x.MemberId ?? x.UnitId).Distinct(StringComparer.Ordinal).ToArray();
                    if (unitObjects.Length == 0) continue;
                    var decided = objects.Where(x => request.Plan.UnitKind == "assembledEntity"
                        ? x.ObjectId == decision.UnitId : unitObjects.Contains(x.ObjectId,
                        StringComparer.Ordinal)).ToArray();
                    if (decided.Length != (request.Plan.UnitKind == "assembledEntity" ? 1 : unitObjects.Length))
                        throw new InvalidDataException("DetectionDecisionInputMissing");
                    var operationId = Guid.NewGuid();
                    var parts = results.Where(x => unitObjects.Contains(x.Key)).Select(x => new
                    {
                        objectId = x.Key,
                        disposition = x.Value.Any(v => v.Disposition == "NG") ? "NG" :
                            x.Value.Any(v => v.Disposition == "Pending") ? "Pending" : "OK",
                        faces = x.Value.Select(v => new { v.LocalFace, v.StageId, v.Disposition, v.Evidence }).ToArray()
                    }).ToArray();
                    var disposition = decided.Any(x => x.Disposition == "NG") ? "NG" :
                        decided.Any(x => x.Disposition == "Pending") ? "Pending" : "OK";
                    await SaveTraceAsync(request, WriteKind.ActionFact,
                        new { kind = "DetectionUnitDecision", decision.Sequence,
                            decision.UnitId, request.Plan.UnitKind, objects = decided, parts,
                            disposition }, cancellationToken);
                    await AppendAsync(request, operationId, decision, StageEventType.Executing,
                        new { kind = "DetectionUnitDecision", decision.Sequence,
                            decision.UnitId, request.Plan.UnitKind, objects = decided, parts, disposition }, cancellationToken);
                }
            }
            return new DetectionPortResult(request, DetectionResultKind.Completed,
                $"detection://{request.RunId:D}/{request.OperationId:D}", objects,
                SourceOf(dispatchedOrigin), ResultQuality.Derived, null, DateTimeOffset.UtcNow,
                references) { AlgorithmOrigin = dispatchedOrigin, CaptureFacts = captures,
                    LastObservation = lastObservation, SlotParticipation = participation, EvidenceBasis = evidenceBasis,
                    PosePending = scopedSteps.Where(s => s.Kind == RecipeStepKind.SortUnit && s.PhysicalSlotIndex is { } index &&
                        participation[index].State == SlotParticipationState.PoseExcluded).Select(s => new PosePendingHandling(
                            s.MemberId ?? s.UnitId, s.PhysicalSlotIndex!.Value, poseBasis[s.PhysicalSlotIndex.Value].Id,
                            poseBasis[s.PhysicalSlotIndex.Value].Reference,
                            scopedSteps.Any(c => c.Kind == RecipeStepKind.Capture && c.PhysicalSlotIndex == s.PhysicalSlotIndex &&
                                results.ContainsKey(c.MemberId ?? c.UnitId)) ? "FurtherInspectionTerminated" : "NotInspected", "3D姿态异常")).ToArray() };
        }
        catch (OperationCanceledException error) when (!cancellationToken.IsCancellationRequested)
        {
            if (activeMotionAction is { } actionId) motion.MarkUnknown(actionId);
            RuntimeDiagnostics.Record("DetectionPort", "TimedOut", request.RunId,
                new { request.OperationId, currentCapture, currentCall, phase, request.DeadlineUtc }, error);
            // A device-owned deadline may have closed after real motion. It is
            // not an algorithm-only timeout and cannot authorize Pending sorting.
            return Failure("DetectionTimedOut:" + phase);
        }
        catch (TimeoutException error)
        {
            if (activeMotionAction is { } actionId) motion.MarkUnknown(actionId);
            RuntimeDiagnostics.Record("DetectionPort", "TimedOut", request.RunId,
                new { request.OperationId, currentCapture, currentCall, phase, request.DeadlineUtc }, error);
            // This adapter may already have moved/captured. Only the per-call
            // released-input branch above can produce finite Pending; never ask
            // the outer executor to replay the entire frozen physical sequence.
            return Failure("DetectionTimedOut:" + phase);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or
            InvalidOperationException)
        {
            if (activeMotionAction is { } actionId) motion.MarkUnknown(actionId);
            RuntimeDiagnostics.Record("DetectionPort", "Failed", request.RunId,
                new { request.OperationId, currentCapture, currentCall, phase, request.DeadlineUtc }, error);
            return Failure(error.Message);
        }
    }

    private static bool HasRequiredECode(RecipeRunPlan plan, string objectId, IReadOnlySet<string> codes)
    {
        if (plan.ECodeBindTo is "AssemblyId" or "GroupId" ||
            plan.ECodeBindTo == "physicalEntity" && plan.UnitKind == "assembledEntity")
        {
            var unit = plan.Steps.First(s => s.Kind == RecipeStepKind.Capture &&
                (plan.UnitKind == "assembledEntity" ? s.UnitId : s.MemberId ?? s.UnitId) == objectId).UnitId;
            return codes.Contains(unit);
        }
        return codes.Contains(objectId);
    }

    private async Task ExecuteTransitionAsync(DetectionRequest request, RecipeStep step, Guid transitionId, CancellationToken token)
    {
        var observed = motion.Observe();
        if (handling is null || !observed.HasReliableObservation || observed.SafetyAssessment != SafetyAssessment.Clear ||
            observed.ManualArea != ManualAreaState.Clear || observed.ConnectionEpoch != request.ConnectionEpoch ||
            step.SlotId is null || step.TargetPose is null)
            throw new InvalidOperationException("FlipSafetyOrTargetUnavailable");
        var plan = request.Plan!;
        var input = plan.ExecutionPositions[step.SlotId].ForObject(plan.UnitKind, step.Material);
        var transition = step.ScanPoseId is not null && plan.ECode.ExtraPose is { } extra && extra.PoseId == step.ScanPoseId
            ? new RecipeFlipTransition(extra.TargetPose, extra.PickPointRef!, extra.PutBackPointRef!)
            : input.Flip?.Stages.GetValueOrDefault(step.StageId ?? "") ?? throw new InvalidOperationException("FlipTransitionMissing");
        if (transition.TargetPose != step.TargetPose) throw new InvalidOperationException("FlipPoseIdentityMismatch");
        var pick = CoordinateResolver.Purpose(input, transition.PickPointRef, RecipePointPurpose.FlipPick);
        var put = CoordinateResolver.Purpose(input, transition.PutBackPointRef, RecipePointPurpose.FlipPutBack);
        async Task Execute(bool placing)
        {
            var configured = placing ? put : pick;
            var role = placing ? "FlipPutBack" : "FlipPick";
            var moved = await MoveAndBeginAsync(request, step, configured.Point, configured.CoordinateEvidenceReference,
                configured.CoordinateEvidenceReference, token, role, placing ? null : new(transitionId,
                    plan.Model, transition.TargetPose, step.PhysicalEntityId ?? throw new InvalidOperationException("FlipEntityMissing"),
                    step.PhysicalSlotIndex ?? throw new InvalidOperationException("FlipSlotMissing")));
            motion.ConfirmCompleted(moved.ActionId);
            token.ThrowIfCancellationRequested();
            var operation = Guid.NewGuid();
            var action = Guid.NewGuid();
            var correlation = new ActionCorrelation(request.RunId, operation, action, request.Attempt,
                request.SessionId, request.ConnectionEpoch, request.SnapshotId, request.PlanRevision, request.TrayId,
                step.PhysicalEntityId, LocalFace: step.LocalFace, PhysicalSlotIndex: step.PhysicalSlotIndex);
            var intent = await SaveTraceAsync(request, WriteKind.ActionIntent, new { kind = role,
                operationId = operation, actionId = action, transitionId, correlation, step.Sequence,
                step.StageId, step.ScanPoseId, step.TargetPose, plan.Model, point = configured }, token);
            var duration = placing ? request.FrozenBusinessDurations!.PutBackCompletion : request.FrozenBusinessDurations!.FlipCompletion;
            if (duration is null or <= 0) throw new InvalidOperationException("FlipOrPutBackBudgetMissing");
            var now = DateTimeOffset.UtcNow;
            var window = ActionWindows.FromUtc(TimeProvider.System, now,
                now + Remaining(request.DeadlineUtc - now, TimeSpan.FromMilliseconds(duration.Value)), request.ClockId);
            motion.BeginSpecialAction(request.RunId, action);
            DeviceActionEvidence evidence;
            try
            {
                if (placing)
                {
                    var command = new PutBackRequest(correlation, transitionId, moved.Evidence.Positions.Single(), window, intent.WriteId);
                    evidence = await handling.PutBackAsync(command, token);
                    if (!FaceEstablishment.Confirms(command, evidence)) throw new InvalidOperationException("PutBackCompletionUnconfirmed");
                }
                else
                {
                    var command = new FlipRequest(correlation, transitionId, plan.Model, transition.TargetPose,
                        moved.Evidence.Positions.Single(), window, intent.WriteId);
                    evidence = await handling.FlipAsync(command, token);
                    if (!FaceEstablishment.Confirms(command, evidence)) throw new InvalidOperationException("FlipCompletionUnconfirmed");
                }
                token.ThrowIfCancellationRequested();
                if (!window.Contains(TimeProvider.System.GetTimestamp())) throw new TimeoutException("TransitionDeadlineExpired");
                await SaveTraceAsync(request, WriteKind.ActionFact, new { kind = role + "Completed", transitionId,
                    operationId = operation, actionId = action, step.Sequence, step.StageId, step.ScanPoseId, evidence }, token);
                await AppendAsync(request, operation, step, StageEventType.Executing,
                    new { kind = role + "Completed", transitionId, step.Sequence, evidence }, token);
                motion.ConfirmCompleted(action);
            }
            catch { motion.MarkUnknown(action); throw; }
        }
        await Execute(false);
        await Execute(true);
    }
    private static DetectionCaptureSettings ReadCaptureSettings(RecipeRunPlan plan, RecipeStep step)
    {
        if (step.CaptureProfile is null || !plan.CaptureProfiles.TryGetValue(step.CaptureProfile, out var profile))
            throw new InvalidDataException("DetectionCaptureProfileMissing");
        var settings = profile.Settings;
        var roi = settings.RoiPixels;
        if (settings.ExposureUs <= 0 || !double.IsFinite(settings.Gain) || settings.Gain <= 0 ||
            roi.Length != 4 || roi.Any(x => x < 0) || settings.LightChannel.Length == 0 ||
            settings.BrightnessPercent is < 0 or > 100 || settings.SettleMs is < 0 or > 1000)
            throw new InvalidDataException("DetectionCaptureProfileInvalid");
        return settings;
    }

    private sealed record ProductMove(AcquisitionSession? Session, Guid ActionId, DeviceActionEvidence Evidence);

    private async Task<ProductMove> MoveAndBeginAsync(DetectionRequest request,
        RecipeStep step, Gaode.Domain.Configuration.FixedPoint target, string source, string basis, CancellationToken cancellationToken,
        string role = "Detection", FlipMovePreparation? flipPreparation = null)
    {
        var operationId = Guid.NewGuid();
        var actionId = Guid.NewGuid();
        await AppendAsync(request, operationId, step, StageEventType.IntentRecorded,
            new { schemaVersion = "device-semantics/1", kind = role == "Detection" ? "DetectionMove" : role is "FlipPick" or "FlipPutBack" ? role + "Move" : role == "E" ? "ECodeMove" : "ThreeDRescanMove",
                role, actionId, target, step.Sequence }, cancellationToken);
        var intent = await SaveTraceAsync(request, WriteKind.ActionIntent,
            new OperationIntentPayload(operationId,
                role == "Detection" ? "Detection" : role is "FlipPick" or "FlipPutBack" ? role : role == "E" ? "ECode" : "ThreeDRescan", request.Attempt, actionId,
                null, $"{target.Id}@{target.Version}:X={target.X};Y={target.Y};Z={target.Z};{source};{basis}",
                request.PlanRevision, Guid.Empty), cancellationToken);
        var epoch = motion.Observe().ConnectionEpoch;
        if (epoch != request.ConnectionEpoch)
            throw new IOException("DetectionConnectionEpochMismatch");
        var accepted = new TaskCompletionSource<DeviceEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource<DeviceEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        var envelope = Envelope(request, operationId);
        var binding = request.MotionConfiguration!.Bindings.Single(x => x.Role == "PLC").Id;
        void OnMove(DeviceEvent value)
        {
            if (!DeviceContract.Matches(value, envelope, epoch) || value.ActionId != actionId)
            {
                RuntimeDiagnostics.Record("DetectionMove", "IgnoredFeedback", request.RunId,
                    new { operationId, actionId, value.Kind, value.ConnectionEpoch }, warning: true);
                return;
            }
            if (value.Kind == DeviceEventKind.Accepted) accepted.TrySetResult(value);
            if (value.Kind == DeviceEventKind.Completed) completed.TrySetResult(value);
            if (value.Kind is DeviceEventKind.Failed or DeviceEventKind.UnknownHeld)
            {
                var error = new IOException(value.ErrorCode ?? "DetectionMoveFailed");
                accepted.TrySetException(error);
                completed.TrySetException(error);
            }
        }
        RuntimeDiagnostics.Record("DetectionMove", "Dispatching", request.RunId,
            new { operationId, actionId, role, target, request.DeadlineUtc });
        using var moveCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            await motion.RequestMoveAsync(request.MotionConfiguration, new MoveRequest(envelope,
                actionId, target, intent.WriteId, binding, role) { FlipPreparation = flipPreparation }, false,
                OnMove, moveCancellation.Token);
            await accepted.Task.WaitAsync(Remaining(request.DeadlineUtc - DateTimeOffset.UtcNow,
                TimeSpan.FromMilliseconds(request.FrozenBusinessDurations!.PlcAcceptance)), cancellationToken);
            var completion = await completed.Task.WaitAsync(Remaining(request.DeadlineUtc - DateTimeOffset.UtcNow,
                TimeSpan.FromMilliseconds(request.FrozenBusinessDurations!.XyCompletion)), cancellationToken);
            var actual = motion.Observe();
            var tolerance = request.MotionConfiguration.Motion.PositionTolerance;
            if (actual.ConnectionEpoch != epoch || !actual.HasReliableObservation || actual.SafetyAssessment != SafetyAssessment.Clear ||
                completion.Evidence is not { IsCorrelated: true, Meaning: DeviceCompletionMeaning.PositionReached } moveEvidence ||
                moveEvidence.Correlation.ActionId != actionId || moveEvidence.Correlation.OperationId != operationId ||
                moveEvidence.Positions.Count != 1 || moveEvidence.Positions[0].Target != target ||
                !moveEvidence.Positions[0].Matched ||
                actual.PositionForPurpose(role switch { "E" => "ScanZ", "Detection" => "DetectionZ", _ => "XY" }) is not { } position ||
                !new PositionReachedEvidence(moveEvidence.Correlation, target, position, tolerance).Matched)
                throw new IOException("DetectionPositionEvidenceMismatch");
            await SaveTraceAsync(request, WriteKind.ActionFact,
                new { schemaVersion = "device-semantics/1", kind = role == "Detection" ? "DetectionMoveConfirmed" : role is "FlipPick" or "FlipPutBack" ? role + "PositionConfirmed" : role == "E" ? "ECodeMoveConfirmed" : "ThreeDRescanMoveConfirmed",
                    operationId, actionId, role,
                    step.Sequence, target, positionEvidence = moveEvidence, observed = actual, epoch }, cancellationToken);
            await AppendAsync(request, operationId, step, StageEventType.Executing,
                new { schemaVersion = "device-semantics/1", kind = role == "Detection" ? "DetectionMoveConfirmed" : role is "FlipPick" or "FlipPutBack" ? role + "PositionConfirmed" : role == "E" ? "ECodeMoveConfirmed" : "ThreeDRescanMoveConfirmed",
                    role, actionId, step.Sequence, target, positionEvidence = moveEvidence, observed = actual, epoch }, cancellationToken);
            AcquisitionSession? session = null;
            if (role is not ("FlipPick" or "FlipPutBack"))
                session = await motion.OpenCaptureWindowAsync(new(moveEvidence.Correlation,
                    role switch { "3D" => CaptureRole.ThreeD, "E" => CaptureRole.E, _ => CaptureRole.Detection }, target,
                    moveEvidence.Positions.Single(), ActionWindows.From(TimeProvider.System, envelope.StartTick, envelope.DueTick, envelope.ClockId)),
                    false, cancellationToken);
            return new(session, actionId, moveEvidence);
        }
        catch
        {
            motion.MarkUnknown(actionId);
            await moveCancellation.CancelAsync();
            throw;
        }
    }

    private async Task<(string Disposition, string Evidence)> FuseAsync(
        DetectionRequest request, RecipeStep step, FaceFusionInputs pair,
        CancellationToken cancellationToken)
    {
        var callId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var inputs = new[] { pair.First.Media, pair.Second.Media };
        await AppendAsync(request, operationId, step, StageEventType.IntentRecorded,
            new { kind = "FaceFusion", callId, pair.Key, mediaIds = inputs.Select(x => x.MediaId) },
            cancellationToken);
        var algorithmEnvelope = Envelope(request, operationId);
        var bound = Bound(request, step, fusion: true);
        var producer = algorithm.Origin;
        var intent = await SaveTraceAsync(request, WriteKind.AlgorithmIntent,
            new AlgorithmIntentPayload(callId, operationId, request.Attempt, request.RunId,
                pair.Second.Media.CaptureId, inputs.Select(x => x.MediaId).ToArray(),
                algorithmEnvelope.ConfigVersion, request.PlanRevision, bound.Requirement.ParametersVersion,
                bound.CapabilityId, bound.CapabilityVersion, bound.ProviderVersion, algorithmEnvelope.SessionId,
                algorithmEnvelope.ClockId, algorithmEnvelope.StartTick, algorithmEnvelope.DueTick, request.Inputs!.CostProfile.AlgorithmWaitMs,
                "FrozenRecipeFacePair+BothMediaFileCompleted+BothSingleFactsCommitted"),
            cancellationToken);
        var response = new TaskCompletionSource<AlgorithmEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var identities = new[] { pair.First.Camera, pair.Second.Camera }
            .Select(camera => new WorkerTargetIdentity(pair.Key.ObjectId,
                pair.Key.LocalFace, pair.Key.HeightRound, camera) { StageId = pair.Key.StageId }).ToArray();
        var fusionRequest = new AlgorithmRequest(algorithmEnvelope, callId,
            pair.Second.Media.CaptureId, AlgorithmRole.Detection, inputs,
            bound.Requirement.ParametersVersion, bound.CapabilityId, bound.CapabilityVersion,
            intent.WriteId, "FrozenRecipeFaceFusion") { InputIdentities = identities };
        void OnResult(AlgorithmEvent value)
        {
            if (AcquisitionContract.Matches(value, fusionRequest) && value.Request.Envelope.Attempt == request.Attempt &&
                value.Kind is AlgorithmEventKind.Result or AlgorithmEventKind.Failed)
                response.TrySetResult(value);
        }
        RuntimeDiagnostics.Record("FaceFusion", "Requesting", request.RunId,
            new { request.OperationId, operationId, callId, pair.Key,
                mediaIds = inputs.Select(x => x.MediaId), request.DeadlineUtc });
        var dispatch = await algorithm.RequestAsync(fusionRequest, OnResult, cancellationToken);
        var wait = request.DeadlineUtc - DateTimeOffset.UtcNow;
        var (result, timedOut) = await AwaitDetectionResultAsync(request, step,
            operationId, fusionRequest, response.Task, dispatch.Exited, cancellationToken);
        if (result.Kind != AlgorithmEventKind.Result ||
            result.DetectionDisposition is not ("OK" or "NG" or "Pending"))
            throw new InvalidDataException(result.ErrorCode ?? "FaceFusionWorkerInvalid");
        await dispatch.Exited.WaitAsync(TimeSpan.FromMilliseconds(request.Inputs!.CostProfile.InputReleaseWaitMs), cancellationToken);
        await SaveTraceAsync(request, WriteKind.AlgorithmFact,
            new AlgorithmFactPayload(callId, timedOut ? AlgorithmState.TimedOut : AlgorithmState.Success,
                true, JsonSerializer.Serialize(new { result.DetectionDisposition,
                    pair.Key, mediaIds = inputs.Select(x => x.MediaId) }),
                timedOut ? "DetectionTimedOut" : "WorkerResult", timedOut ? "FinitePending" : "Accepted", WorkerSessionId: result.WorkerSessionId) { Origin = producer, RunId = request.RunId, CaptureId = result.Request.CaptureId },
            cancellationToken);
        var evidence = $"worker://{result.WorkerSessionId:D}/{callId:D}";
        var disposition = pair.First.Disposition == "NG" || pair.Second.Disposition == "NG" ||
            result.DetectionDisposition == "NG" ? "NG" :
            pair.First.Disposition == "Pending" || pair.Second.Disposition == "Pending" ||
            result.DetectionDisposition == "Pending" ? "Pending" : "OK";
        await AppendAsync(request, operationId, step, StageEventType.Executing,
            new { kind = "FaceFusionCommitted", callId, pair.Key, stepSequence = step.Sequence,
                objectId = pair.Key.ObjectId, localFace = pair.Key.LocalFace, pair.Key.StageId,
                step.UnitId, UnitKind = request.Plan!.UnitKind, step.AlgorithmProfile,
                inputCallIds = new[] { Guid.Parse(pair.First.WorkerEvidence.Split('/')[^1]), Guid.Parse(pair.Second.WorkerEvidence.Split('/')[^1]) },
                mediaIds = inputs.Select(x => x.MediaId), result.DetectionDisposition,
                result.WorkerSessionId, result.ErrorCode, disposition,
                technicalState = timedOut ? "TimedOut" : result.Kind == AlgorithmEventKind.Failed ? "Error" : "Success",
                inputDispositions = new[] { pair.First.Disposition, pair.Second.Disposition } }, cancellationToken);
        return (disposition, evidence);
    }

    private async Task<(AlgorithmEvent Result, bool TimedOut)> AwaitDetectionResultAsync(
        DetectionRequest request, RecipeStep step, Guid operationId, AlgorithmRequest call,
        Task<AlgorithmEvent> response, Task released, CancellationToken token)
    {
        try
        {
            return (await response.WaitAsync(Remaining(request.DeadlineUtc - DateTimeOffset.UtcNow,
                TimeSpan.FromMilliseconds(request.Inputs!.CostProfile.AlgorithmWaitMs)), token), false);
        }
        catch (TimeoutException) when (DateTimeOffset.UtcNow < request.DeadlineUtc)
        {
            // A late algorithm is not an unknown mechanical action. Require actual
            // input release before recording finite Pending and completing Z reset.
            await released.WaitAsync(Remaining(request.DeadlineUtc - DateTimeOffset.UtcNow,
                TimeSpan.FromMilliseconds(request.Inputs!.CostProfile.InputReleaseWaitMs)), token);
            if (!response.IsCompletedSuccessfully)
                throw new InvalidDataException("DetectionAlgorithmNoResultAfterInputRelease");
            var late = await response;
            if (late.Kind != AlgorithmEventKind.Result ||
                late.DetectionDisposition is not ("OK" or "NG" or "Pending"))
                throw new InvalidDataException(late.ErrorCode ?? "DetectionAlgorithmInvalid");
            await AppendAsync(request, operationId, step, StageEventType.PendingRecorded,
                new { kind = "AlgorithmDeadlineExpired", call.CallId, step.Sequence,
                    step.MemberId, step.LocalFace, step.Camera, ignoredLateDisposition = late.DetectionDisposition,
                    inputReleaseConfirmed = true }, token, "DetectionTimedOut");
            RuntimeDiagnostics.Record("DetectionAlgorithm", "FinitePending", request.RunId,
                new { call.CallId, step.Sequence, step.MemberId, step.LocalFace, step.Camera }, warning: true);
            return (late with { DetectionDisposition = "Pending" }, true);
        }
    }

    private static PortEnvelope Envelope(DetectionRequest request, Guid operationId)
    {
        var start = System.Diagnostics.Stopwatch.GetTimestamp();
        var remaining = request.DeadlineUtc - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero) throw new TimeoutException("DetectionDeadlineExceeded");
        if (request.SessionId == Guid.Empty || string.IsNullOrWhiteSpace(request.SnapshotId) || string.IsNullOrWhiteSpace(request.ClockId))
            throw new InvalidOperationException("DetectionRunClockOrSnapshotMissing");
        return new PortEnvelope(request.RunId, operationId, request.Attempt, request.SessionId,
            request.SnapshotId, request.MotionConfiguration?.Version ?? request.PlanRevision, request.Purpose, start,
            checked(start + (long)(System.Diagnostics.Stopwatch.Frequency * remaining.TotalSeconds)),
            request.ClockId);
    }

    private BoundCapability Bound(DetectionRequest request, RecipeStep step, bool fusion)
    {
        var key = step.AlgorithmProfile + (fusion ? "/fusion" : "");
        if (step.AlgorithmProfile is null || !request.Inputs!.Capabilities.TryGetValue(key, out var bound) ||
            !algorithm.Origin.IsKnown || algorithm.Origin.VersionRef != bound.ProviderVersion ||
            bound.Requirement.Purpose != (fusion ? AlgorithmPurpose.FaceFusion :
                step.Kind == RecipeStepKind.ReadECode ? AlgorithmPurpose.EntityCode : AlgorithmPurpose.SingleDetection))
            throw new InvalidOperationException("FrozenAlgorithmBindingMismatch");
        return bound;
    }

    private static TimeSpan Remaining(TimeSpan remaining, TimeSpan limit) =>
        remaining > TimeSpan.Zero ? remaining < limit ? remaining : limit :
        throw new TimeoutException("DetectionDeadlineExceeded");

    private static (ActionWindow Window, CancellationTokenSource Limit) SaveWindow(DetectionRequest request, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (request.CriticalSaveBudgetMs <= 0) throw new InvalidOperationException("FrozenDetectionSaveBudgetMissing");
        var now = DateTimeOffset.UtcNow;
        var start = System.Diagnostics.Stopwatch.GetTimestamp();
        var remaining = Remaining(request.DeadlineUtc - now, TimeSpan.FromMilliseconds(request.CriticalSaveBudgetMs));
        var window = new ActionWindow(start, checked(start + (long)(System.Diagnostics.Stopwatch.Frequency * remaining.TotalSeconds)),
            request.ClockId, now, now + remaining);
        var limit = CancellationTokenSource.CreateLinkedTokenSource(token);
        limit.CancelAfter(remaining);
        return (window, limit);
    }
    private static void RequireSaveReceipt(ActionWindow window, CancellationToken token)
    {
        if (token.IsCancellationRequested || !window.Contains(System.Diagnostics.Stopwatch.GetTimestamp()))
            throw new TimeoutException("DetectionSaveReceiptExpired");
    }
    private async Task<CommitReceipt> SaveTraceAsync(DetectionRequest request, WriteKind kind,
        object payload, CancellationToken token)
    {
        var (window, saveLimit) = SaveWindow(request, token);
        using var limit = saveLimit;
        Guid? attemptedWriteId = null;
        try
        {
            RuntimeDiagnostics.Record("DetectionSave", "ReadingRevision", request.RunId,
                new { kind, window.StartTick, window.DueTick, request.CriticalSaveBudgetMs });
            var run = await traces.GetRunAsync(request.RunId, limit.Token)
                ?? throw new InvalidOperationException("DetectionRunNotPersisted");
            RuntimeDiagnostics.Record("DetectionSave", "RevisionRead", request.RunId,new { kind,run.Revision });
            var json = JsonSerializer.Serialize(payload);
            var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
            var writeId = Guid.NewGuid();
            attemptedWriteId = writeId;
            var queued = writer.SubmitCritical(new WriteBatch(writeId, request.RunId,
                run.Revision, kind, json, digest), limit.Token, window);
            RuntimeDiagnostics.Record("DetectionSave", "Queued", request.RunId,new { writeId,kind,run.Revision });
            var receipt = await queued.Completion.WaitAsync(limit.Token);
            RuntimeDiagnostics.Record("DetectionSave", "ReceiptObserved", request.RunId,
                new {writeId,kind,receipt.State,receipt.CommittedRevision,receipt.ErrorCode});
            RuntimeDiagnostics.Record("DetectionSave", "DeadlineChecked", request.RunId,
                new { writeId, window.DueTick, nowTick = System.Diagnostics.Stopwatch.GetTimestamp(),
                    cancelled = limit.IsCancellationRequested });
            RequireSaveReceipt(window, limit.Token);
            if (receipt.State != CommitState.Committed || receipt.WriteId != writeId || receipt.RunId != request.RunId)
                throw new InvalidOperationException("DetectionTraceSaveFailed:" + receipt.State);
            return receipt;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            RuntimeDiagnostics.Record("DetectionSave", "WindowClosed", request.RunId,
                new { writeId = attemptedWriteId, window.DueTick, nowTick = System.Diagnostics.Stopwatch.GetTimestamp(),
                    cancelled = limit.IsCancellationRequested, actualCommit = "NotInferred" }, warning: true);
            throw new TimeoutException("DetectionSaveWindowClosed;ActualCommitNotInferred");
        }
    }

    private async Task AppendAsync(DetectionRequest request, Guid operationId, RecipeStep step,
        StageEventType type, object payload, CancellationToken token, string? errorCode = null,
        string? factKey = null)
    {
        var json = JsonSerializer.Serialize(payload);
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        var key = $"{request.IdempotencyKey}:step:{step.Sequence}:{operationId:N}:{type}";
        // Waiting for the operator and establishing the confirmed face are distinct
        // persisted facts of the same action. Keep its identity and replay protection.
        if (factKey is not null) key += ":" + factKey;
        var (window, saveLimit) = SaveWindow(request, token);
        using var limit = saveLimit;
        StageEventAppendResult append;
        try
        {
        append = await events.AppendAsync(new StageEventAppendRequest(Guid.NewGuid(),
            request.RunId, request.TrayId, request.StationId.ToString(), request.LineId.ToString(),
            WholeTrayWorkflowStage.Detection, operationId, request.Attempt,
            request.ConnectionEpoch, type, DateTimeOffset.UtcNow, ResultSource.HostDerived,
            ResultQuality.Derived, errorCode, digest, json, key, request.PlanRevision,
            request.StageStartedAtUtc, request.DeadlineUtc), limit.Token).WaitAsync(limit.Token);
        RequireSaveReceipt(window, limit.Token);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        { throw new TimeoutException("DetectionStageSaveWindowClosed;ActualCommitNotInferred"); }
        if (!append.IsCommitted) throw new InvalidOperationException("DetectionEvidenceSaveFailed");
    }

    private static DetectionPortResult NotStarted(DetectionRequest request, string error) =>
        new(request, DetectionResultKind.Failed, null, [], ResultSource.Fallback, ResultQuality.Unknown,
            error, DateTimeOffset.UtcNow, [$"detection-error://{error}"]);

}
