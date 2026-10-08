using Gaode.Application.Ports;
using Gaode.Domain.Configuration;

namespace Gaode.Infrastructure.Simulation;

public sealed class SimulatedCapture(SimulationProfile profile, SimulationEventScheduler scheduler) : ICapturePort
{
    private int _threeD, _f;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, byte> _fTriggeredRuns = new();
    private long _epoch = 1;
    public long ConnectionEpoch => Volatile.Read(ref _epoch);
    public string MediaSource => profile.Fixtures.MediaSource;
    public Gaode.Domain.Station01.ComponentExecutionOrigin CameraOrigin => new(
        Gaode.Domain.Station01.ComponentEvidenceSource.Simulated, $"{profile.Id}/{profile.Version}", "Simulated");
    public Gaode.Domain.Station01.ComponentExecutionOrigin LightOrigin => new(
        Gaode.Domain.Station01.ComponentEvidenceSource.Simulated, $"{profile.Id}/{profile.Version}", "Simulated");
    public int TriggerCount(CaptureRole role) => role == CaptureRole.ThreeD ? Volatile.Read(ref _threeD) : Volatile.Read(ref _f);

    public ValueTask RequestCaptureAsync(CaptureRequest request, Action<CaptureEvent> onEvent,
        CancellationToken cancellationToken)
    {
        if (!request.Envelope.IsValid || request.IntentWriteId == Guid.Empty ||
            request.CaptureId == Guid.Empty || request.MaxBytes <= 0)
            throw new ArgumentException("采集请求无效");
        if (request.Role == CaptureRole.F)
        {
            if (!_fTriggeredRuns.TryAdd(request.Envelope.RunId, 0))
                throw new InvalidOperationException("F已触发，不允许重拍");
            Interlocked.Increment(ref _f);
        }
        else Interlocked.Increment(ref _threeD);
        var stage = request.Role == CaptureRole.ThreeD ? profile.Stages.Capture3d : profile.Stages.CaptureF;
        var epoch = Volatile.Read(ref _epoch);
        onEvent(new(request, CaptureEventKind.Accepted, epoch));
        onEvent(new(request, CaptureEventKind.Capturing, epoch));
        scheduler.Respond(stage, () =>
        {
            if (ResponsePolicy.IsFailure(stage))
            {
                onEvent(new(request, CaptureEventKind.Failed, epoch, ErrorCode: ResponsePolicy.FailureCode(stage)));
                return;
            }
            if (ResponsePolicy.IsHold(stage)) return;
            var role = request.Role == CaptureRole.ThreeD ? "3D" : "F";
            var bytes = SyntheticMediaFixture.Create(role, request.Envelope.RunId, request.CaptureId,
                request.ScopeVersion ?? "NotApplicable");
            if (bytes.Length > request.MaxBytes)
            {
                onEvent(new(request, CaptureEventKind.Failed, epoch, ErrorCode: "MediaOverLimit"));
                return;
            }
            onEvent(new(request, CaptureEventKind.Ended, epoch));
            var captured = new CaptureEvent(request, CaptureEventKind.MediaTaken, epoch, bytes,
                request.Role == CaptureRole.ThreeD ? "bin" : "img")
            {
                Fact = new(request.Envelope.RunId, request.CaptureId, request.Envelope.OperationId,
                    epoch, AcquisitionContract.RequestedSettingsDigest(request), MediaSource,
                    CameraOrigin, LightOrigin, CaptureApplicationState.ConfiguredOnly, null,
                    false, [$"simulation:{profile.Id}/{profile.Version}"])
                    { ActualPublicSettings = request.PublicSettings,
                        CameraApplicationState = CaptureApplicationState.ConfiguredOnly,
                        LightApplicationState = CaptureApplicationState.ConfiguredOnly }
            };
            onEvent(captured);
            scheduler.Duplicates(stage, () => onEvent(captured));
        }, cancellationToken);
        return ValueTask.CompletedTask;
    }
}
