using Gaode.Application.Ports;

namespace Gaode.Application.Acquisition;

public sealed class CaptureEvidenceGate
{
    private readonly object _gate = new();
    private bool _ended, _taken;
    private byte[]? _buffer;
    private string? _format;
    private CorrelatedCaptureFact? _fact;

    public bool Observe(CaptureEvent e)
    {
        lock (_gate)
        {
            if (e.Fact is { } fact && !AcquisitionContract.MatchesFact(fact, e.Request, e.ConnectionEpoch)) return false;
            if (e.Kind == CaptureEventKind.Ended) _ended = true;
            if (e.Kind == CaptureEventKind.MediaTaken && e.Buffer is { Length: > 0 })
            {
                if (_taken) return false;
                _taken = true;
                _buffer = e.Buffer;
                _format = e.Format;
                _fact = e.Fact;
            }
            return _ended && _taken;
        }
    }

    public CorrelatedCaptureFact TakeFact(CaptureRequest request, long epoch)
    {
        lock (_gate)
        {
            if (!_ended || !_taken || _fact is null || !AcquisitionContract.MatchesFact(_fact, request, epoch))
                throw new InvalidOperationException("CurrentCaptureFactMissingOrMismatched");
            return _fact;
        }
    }

    public (byte[] Buffer, string Format) Take()
    {
        lock (_gate)
        {
            if (!_ended || !_taken || _buffer is null || _format is null)
                throw new InvalidOperationException("采集结束与媒体接管依据尚未齐全");
            return (_buffer, _format);
        }
    }
}
