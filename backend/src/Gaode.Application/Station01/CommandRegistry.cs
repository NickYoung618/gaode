using System.Security.Cryptography;
using System.Text;

namespace Gaode.Application.Station01;

public sealed record StartReceipt(Guid CommandId, Guid RunId, string ReceiptDurability,
    string StatusUrl, bool Accepted);

public sealed class CommandRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<(string Subject, string RequestId), (string Digest, StartReceipt Receipt)> _requests = [];
    private readonly Dictionary<Guid, StartReceipt> _commands = [];
    private Guid? _physicalOwner;
    private Guid? _faultRestartOwner;
    private Guid? _completedRun;
    private bool _maintenance;
    public Guid? PhysicalOwner { get { lock (_gate) return _physicalOwner; } }
    public Guid? BeginMaintenance()
    {
        lock (_gate)
        {
            if (_maintenance || _faultRestartOwner is not null) throw new InvalidOperationException("RecoveryMaintenanceBusy");
            _maintenance = true; return _physicalOwner;
        }
    }
    public void EndMaintenance() { lock (_gate) _maintenance = false; }
    public void ReleaseAfterCommissioningRecovery(Guid runId)
    {
        lock (_gate)
        {
            if (!_maintenance || _physicalOwner != runId) throw new InvalidOperationException("RecoveryOwnerChanged");
            _physicalOwner = null;
        }
    }
    public StartReceipt? FindStart(string subject, string requestId)
    {
        lock (_gate) return _requests.TryGetValue((subject, requestId), out var value) ? value.Receipt : null;
    }
    // Only called by the completion store after committed same-run proof has been checked.
    public void ReleaseAfterCommittedFinal(Guid runId)
    {
        lock (_gate)
        {
            if (_physicalOwner != runId) return;
            _physicalOwner = null; _completedRun = runId;
        }
    }
    public object StartAdmission()
    {
        lock (_gate) return new { schemaVersion = "station01-start-admission/1",
            state = !_maintenance && _physicalOwner is null && _faultRestartOwner is null ? "Available" : "Held",
            ownerRunId = _physicalOwner, completedRunId = _completedRun,
            reasonCodes = _maintenance ? new[] { "RecoveryInProgress" } : _faultRestartOwner is not null ? new[] { "FaultRequiresNewRun" } :
                _physicalOwner is not null ? new[] { "PhysicalRunHeld" } : Array.Empty<string>() };
    }

    public StartReceipt? Replay(string subject, string requestId, string canonicalRequest)
    {
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)));
        lock (_gate)
        {
            if (!_requests.TryGetValue((subject, requestId), out var old)) return null;
            if (old.Digest != digest) throw new InvalidOperationException("RequestConflict");
            return old.Receipt;
        }
    }
    public void Restore(string subject, string requestId, string digest, Guid commandId, Guid runId)
    {
        lock (_gate)
        {
            var receipt = new StartReceipt(commandId, runId, "Committed", $"/api/v1/station01/runs/{runId:D}", true);
            _requests.TryAdd((subject, requestId), (digest, receipt));
            _commands.TryAdd(commandId, receipt);
        }
    }

    public StartReceipt Register(string subject, string requestId, string canonicalRequest, Guid? faultRunId = null)
    {
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(requestId))
            throw new ArgumentException("请求主体和requestId必需");
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)));
        lock (_gate)
        {
            var key = (subject, requestId);
            if (_requests.TryGetValue(key, out var old))
            {
                if (old.Digest != digest) throw new InvalidOperationException("RequestConflict");
                return old.Receipt;
            }
            if (_faultRestartOwner is not null && _faultRestartOwner != faultRunId)
                throw new InvalidOperationException("FaultRequiresNewRun");
            if (_maintenance) throw new InvalidOperationException("RecoveryInProgress");
            if (_physicalOwner is not null) throw new InvalidOperationException("PhysicalRunHeld");
            var receipt = new StartReceipt(Guid.NewGuid(), Guid.NewGuid(), "Pending",
                "/api/v1/station01/runs/" + Guid.Empty, true);
            receipt = receipt with { StatusUrl = "/api/v1/station01/runs/" + receipt.RunId };
            _requests[key] = (digest, receipt);
            _commands[receipt.CommandId] = receipt;
            _physicalOwner = receipt.RunId;
            return receipt;
        }
    }

    public void UpdateDurability(Guid commandId, string durability)
    {
        lock (_gate)
        {
            if (!_commands.TryGetValue(commandId, out var current)) return;
            var next = current with { ReceiptDurability = durability };
            _commands[commandId] = next;
            foreach (var key in _requests.Keys.ToArray())
                if (_requests[key].Receipt.CommandId == commandId)
                    _requests[key] = (_requests[key].Digest, next);
        }
    }
    public StartReceipt? Get(Guid commandId)
    {
        lock (_gate) return _commands.GetValueOrDefault(commandId);
    }

    public void ReleaseForFaultRestart(Guid runId)
    {
        lock (_gate)
        {
            if (_physicalOwner != runId) throw new InvalidOperationException("FaultPhysicalOwnerMismatch");
            _physicalOwner = null;
            _faultRestartOwner = runId;
        }
    }

    public void HoldRecoveredRun(Guid runId)
    {
        lock (_gate) _physicalOwner ??= runId;
    }
}
