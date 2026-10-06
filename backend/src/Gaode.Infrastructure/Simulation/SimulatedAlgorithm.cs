using Gaode.Application.Ports;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;

namespace Gaode.Infrastructure.Simulation;

public sealed class SimulatedAlgorithm(SimulationProfile profile, SimulationEventScheduler scheduler) : IAlgorithmPort
{
    private int _decode;
    private readonly Guid _session = Guid.NewGuid();
    public ComponentExecutionOrigin Origin => new(ComponentEvidenceSource.Simulated,
        $"{profile.Id}/{profile.Version}", "Simulated");
    public int CallCount(AlgorithmRole role) => role == AlgorithmRole.FDecode ? Volatile.Read(ref _decode) : 0;

    public ValueTask<AlgorithmDispatch> RequestAsync(AlgorithmRequest request, Action<AlgorithmEvent> onEvent,
        CancellationToken cancellationToken)
    {
        if (!request.Envelope.IsValid || request.IntentWriteId == Guid.Empty || request.Inputs.Count == 0 ||
            request.Inputs.Any(x => x.RunId != request.Envelope.RunId || x.CaptureId != request.CaptureId ||
                x.StorageState != "FileCompleted"))
            throw new AlgorithmNotDispatchedException(request.CallId, "InvalidInputBeforeAcquisition");
        if (request.Role != AlgorithmRole.FDecode)
            throw new AlgorithmNotDispatchedException(request.CallId, "SimulatedAlgorithmCapabilityNotIntegrated");
        Interlocked.Increment(ref _decode);
        var stage = profile.Stages.FDecode;
        onEvent(new(request, AlgorithmEventKind.Accepted, WorkerSessionId: _session));
        onEvent(new(request, AlgorithmEventKind.Running, WorkerSessionId: _session));
        var exited = scheduler.RespondTracked(stage, () =>
        {
            if (ResponsePolicy.IsFailure(stage))
            {
                onEvent(new(request, AlgorithmEventKind.Failed, ErrorCode: ResponsePolicy.FailureCode(stage),
                    WorkerSessionId: _session));
                onEvent(new(request, AlgorithmEventKind.InputReleased, WorkerSessionId: _session));
                return;
            }
            if (ResponsePolicy.IsHold(stage)) return;
            var result = new AlgorithmEvent(request, AlgorithmEventKind.Result, RawCodes: profile.Fixtures.RawCodes,
                WorkerSessionId: _session);
            onEvent(result);
            onEvent(new(request, AlgorithmEventKind.InputReleased, WorkerSessionId: _session));
            scheduler.Duplicates(stage, () => onEvent(result));
        }, cancellationToken);
        return ValueTask.FromResult(new AlgorithmDispatch(exited));
    }
}
