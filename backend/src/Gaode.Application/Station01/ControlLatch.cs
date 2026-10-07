namespace Gaode.Application.Station01;

public sealed class ControlLatch
{
    private int _stop;
    private int _cancel;
    private int _fault;
    private readonly CancellationTokenSource _cancellation = new();
    public CancellationToken Cancellation => _cancellation.Token;
    public event Action? AdmissionClosedChanged;
    public bool AdmissionClosed => Volatile.Read(ref _stop) != 0 || Volatile.Read(ref _cancel) != 0 || Volatile.Read(ref _fault) != 0;
    public bool CancelRequested => Volatile.Read(ref _cancel) != 0;
    public bool SafetyFault => Volatile.Read(ref _fault) != 0;
    public bool PauseRequested => Volatile.Read(ref _stop) != 0 && !CancelRequested && !SafetyFault;
    public void RequestStop() { Interlocked.Exchange(ref _stop, 1); AdmissionClosedChanged?.Invoke(); }
    public void RequestCancel()
    {
        Interlocked.Exchange(ref _cancel, 1);
        _cancellation.Cancel();
        AdmissionClosedChanged?.Invoke();
    }
    public void MarkSafetyFault() { Interlocked.Exchange(ref _fault, 1); AdmissionClosedChanged?.Invoke(); }
    public void Resume()
    {
        if (!CancelRequested && !SafetyFault) Interlocked.Exchange(ref _stop, 0);
    }
    internal void ResumeAfterVerifiedReset()
    {
        if (CancelRequested) throw new InvalidOperationException("ContinueNotAllowed");
        Interlocked.Exchange(ref _fault, 0);
        Interlocked.Exchange(ref _stop, 0);
    }
}
