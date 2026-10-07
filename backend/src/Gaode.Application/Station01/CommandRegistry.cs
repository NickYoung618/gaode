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
