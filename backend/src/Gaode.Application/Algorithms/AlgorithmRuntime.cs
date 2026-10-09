using System.Collections.Concurrent;
using Gaode.Application.Ports;
using Gaode.Application.Station01;
using Gaode.Application.Timing;
using Gaode.Domain.Station01;
using Gaode.Diagnostics;

namespace Gaode.Application.Algorithms;

public sealed record AlgorithmOutcome(Guid CallId, Guid OperationId, Guid CaptureId,
    AlgorithmState State, AlgorithmEvent? Event, Guid IntentWriteId,
    long StartTick, long DueTick, string Decision,
    string DispatchEvidence = "NotDispatched", long DispatchTick = 0,
    long? AcceptedTick = null, Guid? WorkerSessionId = null)
{
    public ComponentExecutionOrigin Origin { get; init; } = ComponentExecutionOrigin.Unknown;
}

public sealed partial class AlgorithmRuntime
{
    private readonly ConcurrentDictionary<Guid, IsolatedAlgorithmCall> active = new();
    private readonly ConcurrentQueue<AlgorithmExecutionEvidence> completed = new();
    public IReadOnlyList<AlgorithmExecutionEvidence> ExecutionEvidence =>
        active.Values.Select(x => x.Snapshot()).Concat(completed.ToArray()).ToArray();
    public int ActiveExecutions => active.Count;
    public async Task WaitForIdleAsync(CancellationToken token)
    {
        await Task.WhenAll(active.Values.Select(x => x.Reclaimed)).WaitAsync(token);
        if (resources is not null) await resources.FlushAsync(token);
    }

    // Host waits each original resource window independently; it does not abandon ownership.
    public async Task<bool> WaitForShutdownAsync(CancellationToken token)
    {
        async Task WaitCallAsync(Guid id, IsolatedAlgorithmCall call)
        {
            var remaining = resources?.RemainingReleaseWait(id) ?? TimeSpan.Zero;
            try { await call.Reclaimed.WaitAsync(remaining, token); }
            catch (TimeoutException) { }
        }
        await Task.WhenAll(active.ToArray().Select(x => WaitCallAsync(x.Key, x.Value)));
        if (resources is not null) await resources.FlushAsync(token);
        return active.IsEmpty && resources?.HasUnreclaimedResources != true;
    }

    private readonly IAlgorithmPort algorithm;
    private readonly IMediaStore media;
    private readonly OperationIngress ingress;
    private readonly AlgorithmLeaseSupervisor leases;
    private readonly AlgorithmResourceSupervisor? resources;
    private readonly SemaphoreSlim _pose, _decode, _poseAdmission, _decodeAdmission;
    private readonly SemaphoreSlim synchronousDetection = new(1, 1);

    public AlgorithmRuntime(IAlgorithmPort algorithm, IMediaStore media,
        OperationIngress ingress, AlgorithmLeaseSupervisor leases,
        int queuePerRole = 1, int workerPerRole = 1, AlgorithmResourceSupervisor? resources = null)
    {
        if (queuePerRole < 0 || workerPerRole <= 0) throw new ArgumentOutOfRangeException(nameof(queuePerRole));
        this.algorithm = algorithm;
        this.media = media;
        this.ingress = ingress;
        this.leases = leases;
        this.resources = resources;
        _pose = new(workerPerRole, workerPerRole);
        _decode = new(workerPerRole, workerPerRole);
        _poseAdmission = new(queuePerRole + workerPerRole, queuePerRole + workerPerRole);
        _decodeAdmission = new(queuePerRole + workerPerRole, queuePerRole + workerPerRole);
    }

    public Task<AlgorithmOutcome> InvokeAsync(RunExecution run, AlgorithmRole role,
        Guid captureId, IReadOnlyList<MediaRef> inputs, string scopeVersion,
        CancellationToken cancellationToken, TrayObservationContext? observationContext = null) => RuntimeDiagnostics.ObserveAsync(
            "Algorithm", run.RunId, new { run.RequestId, role = role.ToString(), captureId,
                scopeVersion, mediaIds = inputs.Select(x => x.MediaId).ToArray() },
            async () =>
            {
                var origin = algorithm.Origin;
                var outcome = await InvokeCoreAsync(run, role, captureId, inputs, scopeVersion, cancellationToken, observationContext);
                return outcome with { Origin = outcome.DispatchEvidence == "NotDispatched"
                    ? ComponentExecutionOrigin.Unknown : origin };
            },
            r => new { r.CallId, r.OperationId, r.CaptureId, state = r.State.ToString(), r.Decision,
                r.DispatchEvidence, r.DispatchTick, r.AcceptedTick, r.WorkerSessionId,
                r.StartTick, r.DueTick }, r => r.State != AlgorithmState.Success);

    private async Task<AlgorithmOutcome> InvokeCoreAsync(RunExecution run, AlgorithmRole role,
        Guid captureId, IReadOnlyList<MediaRef> inputs, string scopeVersion,
        CancellationToken cancellationToken, TrayObservationContext? observationContext)
    {
        if (inputs.Count == 0 || inputs.Any(x => x.CaptureId != captureId || x.RunId != run.RunId ||
            !media.IsReady(x.MediaId))) throw new InvalidOperationException("算法输入未完成必要媒体保存");
        var callId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var config = role switch
        {
            AlgorithmRole.TrayPose => run.Config.Public.Algorithms.TrayPose ?? new(null, null, null),
            AlgorithmRole.FDecode => run.Config.Public.Algorithms.FDecode,
            _ => throw new InvalidOperationException("PublicAlgorithmRoleUnsupported")
        };
        Gaode.Application.Configuration.AlgorithmModuleReference? module = null;
        if(run.Config.RealAlgorithm is { } descriptor)
        {
            if(algorithm.Origin.Source != ComponentEvidenceSource.Real) throw new InvalidOperationException("RealAlgorithmNotIntegratedOrNotReady");
            module = AlgorithmInputPolicy.RequireModule(descriptor,role,inputs.Count,config.ParametersVersion!,config.Capability!.Id,config.Capability.ContractVersion);
        }
        if(run.Config.RealAlgorithm is not null || algorithm.InputRepresentation(role) != AlgorithmInputRepresentation.NativeMedia)
        {
            var prepared = new List<MediaRef>();
            foreach(var original in inputs)
            {
                var converted = await media.PrepareAlgorithmInputAsync(original,cancellationToken);
                var expected = role == AlgorithmRole.TrayPose ? "ply" : "png";
                if(converted.Format!=expected) throw new InvalidDataException("AlgorithmInputRepresentationMismatch");
                await run.SaveAsync(WriteKind.Media,converted,cancellationToken:cancellationToken);
                await media.MarkCommittedAsync(converted,cancellationToken);
                prepared.Add(converted);
            }
            inputs = prepared;
        }
        var budget = role == AlgorithmRole.TrayPose ? run.Config.Budget.BusinessMs.TrayPoseAlgorithm : run.Config.Budget.BusinessMs.FDecode;
        var key = new OperationKey(run.RunId, operationId, 1, OperationPhase.Result);
        RuntimeDiagnostics.Record("Algorithm", "Admission", run.RunId,
            new { callId, operationId, captureId, role = role.ToString(), budgetMs = budget,
                capability = config.Capability?.Id, config.ParametersVersion });
        DeadlineWindow? window = budget is > 0 ? ingress.Register(key, budget.Value) : null;
        await run.ReportAsync(algorithm: AlgorithmState.Queued);
        var payload = new AlgorithmIntentPayload(callId, operationId, 1, run.RunId, captureId,
            inputs.Select(x => x.MediaId).ToArray(), run.Config.Public.Version, scopeVersion,
            config.ParametersVersion ?? "NotConfigured", config.Capability?.Id ?? "NotConfigured",
            config.Capability?.ContractVersion ?? "NotConfigured",
            run.Config.CapabilityVersions.GetValueOrDefault(config.Capability?.Id ?? "", "NotConfigured"),
            run.SessionId, run.ClockId, window?.StartTick ?? 0, window?.DueTick ?? 0,
            budget ?? 0, "CaptureEnded+MediaFileCompleted+MediaMetadataCommitted+RegisteredCapability");
        var intentReceipt = await run.SaveAsync(WriteKind.AlgorithmIntent, payload,
            cancellationToken: cancellationToken);
        if (window is null || config.Capability is null)
        {
            await run.ReportAsync(algorithm: AlgorithmState.NotConfigured);
            return new(callId, operationId, captureId, AlgorithmState.NotConfigured,
                null, intentReceipt.WriteId, 0, 0, "NotConfigured");
        }
        if (window.Completion.IsCompleted || ingress.ReceiveIfExpired(window))
        {
            await run.ReportAsync(algorithm: AlgorithmState.TimedOut);
            return new(callId, operationId, captureId, AlgorithmState.TimedOut,
                null, intentReceipt.WriteId, window.StartTick, window.DueTick, "PreDispatchTimeout");
        }
        var slot = role == AlgorithmRole.TrayPose ? _pose : _decode;
        var admission = role == AlgorithmRole.TrayPose ? _poseAdmission : _decodeAdmission;
        if (!await admission.WaitAsync(0, cancellationToken))
        {
            await run.ReportAsync(algorithm: AlgorithmState.NotReady);
            return new(callId, operationId, captureId, AlgorithmState.NotReady,
                null, intentReceipt.WriteId, window.StartTick, window.DueTick, "ExecutionQueueFull");
        }
        using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var slotWait = slot.WaitAsync(waitCancellation.Token);
        if (await Task.WhenAny(slotWait, window.Completion) != slotWait)
        {
            await waitCancellation.CancelAsync();
            try { await slotWait; slot.Release(); } catch (OperationCanceledException) { }
            admission.Release();
            await run.ReportAsync(algorithm: AlgorithmState.TimedOut);
            return new(callId, operationId, captureId, AlgorithmState.TimedOut,
                null, intentReceipt.WriteId, window.StartTick, window.DueTick, "QueueTimeout");
        }
        try { await slotWait; }
        catch { admission.Release(); throw; }
        if (window.Completion.IsCompleted || ingress.ReceiveIfExpired(window))
        {
            slot.Release(); admission.Release();
            await run.ReportAsync(algorithm: AlgorithmState.TimedOut);
            return new(callId, operationId, captureId, AlgorithmState.TimedOut,
                null, intentReceipt.WriteId, window.StartTick, window.DueTick, "QueueTimeout");
        }
        IDisposable inputLease;
        try { inputLease = leases.HoldInputs(inputs, "algorithm:" + callId); }
        catch { slot.Release(); admission.Release(); throw; }
        var returned = new TaskCompletionSource<AlgorithmEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        var envelope = new PortEnvelope(run.RunId, operationId, 1, run.SessionId,
            run.Config.SnapshotId, run.Config.Public.Version, run.Config.Public.Purpose,
            window.StartTick, window.DueTick, run.ClockId);
        var request = new AlgorithmRequest(envelope, callId, captureId, role, inputs,
            config.ParametersVersion ?? "NotConfigured", config.Capability.Id,
            config.Capability.ContractVersion, intentReceipt.WriteId, payload.InvocationBasis)
            { ObservationContext = observationContext, FrozenModule = module, AlgorithmConfigurationDigest = run.Config.RealAlgorithmDigest };
        if (resources is not null)
        {
            try
            {
                await resources.RegisterAsync(new(run.RunId, StartRunContextParser.Parse(run.ContextJson).TrayId,
                    callId, operationId, run.ClockId, run.Config.SnapshotId, inputs,
                    run.Config.Budget.BusinessMs.WorkerReleaseGrace) { Request = request }, cancellationToken);
            }
            catch { inputLease.Dispose(); slot.Release(); admission.Release(); throw; }
        }
        var execution = new IsolatedAlgorithmCall(request, inputLease, () => run.Timestamp, finished =>
        {
            completed.Enqueue(finished.Snapshot());
            while (completed.Count > 64) completed.TryDequeue(out _);
            active.TryRemove(callId, out _);
            slot.Release(); admission.Release();
        }, resources is null ? null : resources.Observe);
        active[callId] = execution;
        var feedbackLogs = 0;
        void OnEvent(AlgorithmEvent e)
        {
            var matched = AcquisitionContract.Matches(e, request) &&
                e.Request.Envelope.Attempt == request.Envelope.Attempt;
            if (Interlocked.Increment(ref feedbackLogs) <= 16)
                RuntimeDiagnostics.Record("AlgorithmFeedback", matched ? "Received" : "IgnoredMismatch", run.RunId,
                    new { callId, operationId, kind = e.Kind.ToString(), e.ErrorCode,
                        actualCallId = e.Request.CallId, actualRunId = e.Request.Envelope.RunId },
                    warning: !matched || e.Kind == AlgorithmEventKind.Failed);
            if (!matched) return;
            if (!execution.Observe(e)) return;
            if (e.Kind is AlgorithmEventKind.Result or AlgorithmEventKind.Failed)
            {
                // Only the winning original ingress decision supplies business data or its terminal projection.
                var decision = ingress.Receive(key, e.Kind == AlgorithmEventKind.Failed ? e.ErrorCode : "AlgorithmResult");
                if (decision.Outcome == IngressOutcome.Accepted)
                { resources?.Terminal(callId, e.Kind.ToString()); returned.TrySetResult(e); }
                else if (decision.Outcome == IngressOutcome.Late) resources?.Terminal(callId, "TimedOut");
            }
        }
        var cancelOnEnd = true;
        try
        {
            execution.Start(algorithm, OnEvent, () => !cancellationToken.IsCancellationRequested &&
                !window.Completion.IsCompleted && !ingress.ReceiveIfExpired(window) && resources?.AdmissionClosed != true);
            var controlSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = cancellationToken.Register(() => controlSignal.TrySetResult());
            var first = await Task.WhenAny(execution.Dispatched, window.Completion, controlSignal.Task);
            cancellationToken.ThrowIfCancellationRequested();
            if (first == execution.Dispatched)
            {
                var dispatch = await execution.Dispatched;
                if (dispatch.Error is { } failure)
                {
                    resources?.Terminal(callId, "DispatchError");
                    RuntimeDiagnostics.Record("AlgorithmDispatch", "Failed", run.RunId,
                        new { callId, operationId, dispatch.NotDispatched,
                            disposition = "PreserveIngressDecisionAndResourceOwnership" }, failure);
                    var decision = ingress.Receive(key, "DispatchException:" + failure.GetType().Name);
                    // A result already won ingress arbitration: a later dispatch exception
                    // cannot overwrite it. Resource ownership remains independently unknown.
                    if (decision.Outcome != IngressOutcome.Duplicate)
                    {
                        var state = decision.Outcome == IngressOutcome.Late ? AlgorithmState.TimedOut : AlgorithmState.Error;
                        await run.ReportAsync(algorithm: state);
                        var proof = execution.Snapshot();
                        return new(callId, operationId, captureId, state, null, intentReceipt.WriteId,
                            window.StartTick, window.DueTick, "DispatchException:" + failure.GetType().Name,
                            dispatch.NotDispatched ? "NotDispatched" : "Unknown", proof.DispatchTick,
                            proof.AcceptedTick, proof.WorkerSessionId);
                    }
                }
            }
            if (!window.Completion.IsCompleted) await run.ReportAsync(algorithm: AlgorithmState.Running);
            await Task.WhenAny(window.Completion, controlSignal.Task);
            cancellationToken.ThrowIfCancellationRequested();
            var terminal = await window.Completion;
            var evidence = execution.Snapshot();
            if (terminal.Outcome != IngressOutcome.Accepted)
            {
                resources?.Terminal(callId, "TimedOut");
                await run.ReportAsync(algorithm: AlgorithmState.TimedOut);
                return new(callId, operationId, captureId, AlgorithmState.TimedOut,
                    null, intentReceipt.WriteId, window.StartTick, window.DueTick, "DeadlineExceeded",
                    evidence.Dispatch == "NotDispatched" ? "NotDispatched" :
                        evidence.Dispatch == "Returned" ? "Requested" : "Unknown",
                    evidence.DispatchTick, evidence.AcceptedTick, evidence.WorkerSessionId);
            }
            var result = await returned.Task;
            resources?.StartObservation(callId, "Result");
            var releaseWait = resources?.RemainingReleaseWait(callId) ??
                TimeSpan.FromMilliseconds(run.Config.Budget.BusinessMs.WorkerReleaseGrace);
            try { await execution.InputAndExecutionEnded.WaitAsync(releaseWait, cancellationToken); }
            catch (TimeoutException)
            {
                RuntimeDiagnostics.Record("AlgorithmRelease", "Unknown", run.RunId,
                    new { callId, operationId, disposition = "BlockDependentSteps;PreserveOwnership" }, warning: true);
                throw new InvalidOperationException("AlgorithmResourcesUnconfirmed");
            }
            await run.ReportAsync(algorithm: result.Kind == AlgorithmEventKind.Failed
                ? AlgorithmState.Error : AlgorithmState.Success);
            cancelOnEnd = false;
            return new(callId, operationId, captureId,
                result.Kind == AlgorithmEventKind.Failed ? AlgorithmState.Error : AlgorithmState.Success,
                result, intentReceipt.WriteId, window.StartTick, window.DueTick,
                terminal.Reason ?? "AlgorithmResult", evidence.AcceptedTick is null ? "Requested" : "Accepted",
                evidence.DispatchTick, evidence.AcceptedTick, evidence.WorkerSessionId);
        }
        catch (OperationCanceledException)
        {
            resources?.Terminal(callId, "Cancelled");
            throw;
        }
        finally
        {
            // Business completion is not resource completion. The isolated execution owns
            // admission, its role slot, cancellation source and inputs until reliable release.
            execution.EndBusiness(cancelOnEnd);
            resources?.StartObservation(callId, cancellationToken.IsCancellationRequested ? "Cancelled" : "BusinessEnded");
            if (resources is not null)
            {
                using var saveLimit = new CancellationTokenSource(TimeSpan.FromMilliseconds(run.Config.Budget.BusinessMs.CriticalSave), run.Clock);
                await resources.FlushAsync(saveLimit.Token);
            }
        }
    }
}
