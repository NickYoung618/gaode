using System.Collections.Concurrent;
using Gaode.Application.Ports;

namespace Gaode.Application.Station01;

/// <summary>Correlates device, capture and algorithm evidence without letting late facts
/// reopen a completed operation or trigger another action.</summary>
public sealed class OperationEvidenceReducer(int lateEvidenceCapacity = 64)
{
    private readonly ConcurrentDictionary<string, byte> accepted = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<string> late = new();
    public IReadOnlyList<string> LateEvidence => late.ToArray();

    public EvidenceDisposition Observe(DeviceEvent value, PortEnvelope expected, long epoch, bool closed)
    {
        var key = $"device:{value.ActionId:N}:{value.Kind}:{value.ConnectionEpoch}";
        return Record(key, DeviceContract.Matches(value, expected, epoch), value.ConnectionEpoch, epoch, closed);
    }

    public EvidenceDisposition Observe(CaptureEvent value, CaptureRequest expected, long epoch, bool closed)
    {
        var key = $"capture:{value.Request.CaptureId:N}:{value.Kind}:{value.ConnectionEpoch}";
        return Record(key, AcquisitionContract.Matches(value, expected, epoch), value.ConnectionEpoch, epoch, closed);
    }

    public EvidenceDisposition Observe(AlgorithmEvent value, AlgorithmRequest expected, bool closed)
    {
        var key = $"algorithm:{value.Request.CallId:N}:{value.Kind}:{value.WorkerSessionId?.ToString("N")}";
        var matches = AcquisitionContract.Matches(value, expected);
        if (!matches) return Record(key, false, 0, 0, closed);
        if (!accepted.TryAdd(key, 0)) return EvidenceDisposition.Duplicate;
        if (closed) return RememberLate(key);
        return EvidenceDisposition.Accepted;
    }

    private EvidenceDisposition Record(string key, bool matches, long eventEpoch, long expectedEpoch, bool closed)
    {
        var seen = accepted.ContainsKey(key);
        var disposition = LateEvidencePolicy.Classify(matches, eventEpoch, expectedEpoch, closed, seen);
        if (disposition == EvidenceDisposition.Accepted) accepted.TryAdd(key, 0);
        if (disposition == EvidenceDisposition.Late) return RememberLate(key);
        return disposition;
    }

    private EvidenceDisposition RememberLate(string key)
    {
        late.Enqueue(key);
        while (late.Count > lateEvidenceCapacity && late.TryDequeue(out _)) { }
        return EvidenceDisposition.Late;
    }
}
