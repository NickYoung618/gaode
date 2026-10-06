using Gaode.Plc.Protocol;
namespace VirtualPlc;

public sealed record DeviceActionAudit(long Sequence, long ActionSequence, long Generation,
    DateTimeOffset OccurredAtUtc, string Kind, ushort Command, string Phase,
    IReadOnlyList<long> WriteSequenceRefs, object? Target, string? AxisRole,
    object Actual, object Handshake);

public sealed record DeviceActionAuditBatch(long OldestSequence, long LatestSequence, bool Gap, IReadOnlyList<DeviceActionAudit> Actions);

public sealed partial class VirtualPlcEngine
{
    public DeviceActionAuditBatch GetActionAudit(long after = 0)
    {
        lock (_gate) {
            var oldest = _actionAudit.Count == 0 ? _actionEventSequence + 1 : _actionAudit.Peek().Sequence;
            return new(oldest, _actionEventSequence, after < oldest - 1, _actionAudit.Where(x => x.Sequence > after).ToArray());
        }
    }
    private IReadOnlyList<long> RecentWriteRefs(PendingAction action)
    {
        // Local write identities only; no Host identity is inferred from the device cache.
        SignalId[] fields = action.Kind switch {
            ActionKind.Sort => [SignalId.CameraTargetX, SignalId.CameraTargetY,
                SignalId.GrabTargetZ, SignalId.SortingCmd],
            ActionKind.Flip => [SignalId.ModelPayload, SignalId.FlipTargetFace, SignalId.FlipSorting],
            ActionKind.PutBack => [SignalId.FlipSorting],
            _ => [] };
        // A write audit row identifies a whole declared field, including both words of
        // Float32. Adjacent field addresses are not part of that field's identity.
        var points = fields.Select(id => _store.Definition[id].DocumentNumber).ToHashSet();
        return _store.GetWriteAudit().Where(x => x.Accepted && x.Area == PlcArea.HoldingRegister && points.Contains(x.DocumentNumber))
            .GroupBy(x => x.DocumentNumber).Select(x => x.Last().Sequence).Order().ToArray();
    }
    private void RecordDeviceAction(PendingAction action, string phase)
    {
        float Actual(SignalId id) {
            var field = _store.Definition[id];
            var words = _store.ReadHoldingRegisters(PlcAddressMap.ToPduOffset(field.DocumentNumber), field.RegisterCount);
            return Float32Codec.Decode(words[0], words[1], _store.ByteOrder);
        }
        object? target = action.Kind == ActionKind.Sort
            ? new { x = action.X, y = action.Y, z = action.Z } : null;
        string? axis = action.Kind == ActionKind.Sort ? "GrabZ" : null;
        var item = new DeviceActionAudit(++_actionEventSequence, action.ActionSequence, resetGeneration,
            DateTimeOffset.UtcNow, action.Kind.ToString(), action.Command, phase, action.WriteSequenceRefs,
            target, axis, new { x = Actual(SignalId.MachineCurrentPosX),
                y = Actual(SignalId.MachineCurrentPosY), z = Actual(action.Kind is ActionKind.Sort or ActionKind.PutBack
                    ? SignalId.FlipGrapCurrentPosZ : SignalId.MachineCurrentPosZ) },
            new { x = _store.ReadHoldingRegisterByDocumentNumber(_store.Definition[SignalId.XPosConfirmed].DocumentNumber),
                y = _store.ReadHoldingRegisterByDocumentNumber(_store.Definition[SignalId.YPosConfirmed].DocumentNumber),
                sort = _store.ReadHoldingRegisterByDocumentNumber(_store.Definition[SignalId.SortingExecStatus].DocumentNumber),
                flip = _store.ReadHoldingRegisterByDocumentNumber(_store.Definition[SignalId.FlipStatus].DocumentNumber),
                putBack = _store.ReadHoldingRegisterByDocumentNumber(_store.Definition[SignalId.FlipUnloadStatus].DocumentNumber) });
        _actionAudit.Enqueue(item);
        if (_actionAudit.Count > 4096) _actionAudit.Dequeue();
    }
}
