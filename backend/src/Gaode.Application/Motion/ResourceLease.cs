namespace Gaode.Application.Motion;

public sealed class ResourceLease
{
    private readonly object _gate = new();
    private Guid? _owner;
    private Guid? _currentAction;
    private bool _unknown;
    public Guid? Owner { get { lock (_gate) return _owner; } }
    public bool Unknown { get { lock (_gate) return _unknown; } }
    public Guid? CurrentAction { get { lock (_gate) return _currentAction; } }

    public bool TryHold(Guid runId)
    {
        lock (_gate)
        {
            if (_unknown || _owner is not null && _owner != runId) return false;
            _owner = runId;
            return true;
        }
    }

    public bool IsOwner(Guid runId)
    {
        lock (_gate) return _owner == runId;
    }
    public bool TryBeginAction(Guid runId, Guid actionId)
    {
        lock (_gate)
        {
            if (_owner != runId || _unknown || _currentAction is not null) return false;
            _currentAction = actionId;
            return true;
        }
    }
    public void Complete(Guid actionId)
    {
        lock (_gate)
        {
            if (_currentAction == actionId) _currentAction = null;
        }
    }
    public void MarkUnknown(Guid actionId)
    {
        lock (_gate)
        {
            if (_currentAction == actionId) _unknown = true;
        }
    }
    public bool PreserveForShutdown()
    {
        lock (_gate)
        {
            if (_currentAction is null) return false;
            _unknown = true;
            return true;
        }
    }
    public void ReconcileVerifiedReset(Guid runId, Guid failedActionId)
    {
        lock (_gate)
        {
            if (_owner != runId || (failedActionId == Guid.Empty ? _currentAction is not null || _unknown : _currentAction != failedActionId || !_unknown))
                throw new InvalidOperationException("RecoveryFailedActionMismatch");
            _currentAction = null;
            _unknown = false;
        }
    }
    public void ReleaseOnlyAfterVerifiedPhysicalClear(Guid runId)
    {
        lock (_gate)
        {
            if (_owner == runId && _currentAction is null && !_unknown) _owner = null;
            else throw new InvalidOperationException("占用状态未核清，不能释放");
        }
    }
}
