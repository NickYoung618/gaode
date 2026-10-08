using System.Collections.Frozen;

namespace Gaode.Plc.Protocol;

// Wire labels and values from the partitioned protocol. Only communication code consumes these.
public static class SignalCodes
{
    private static FrozenDictionary<string, ushort> Codes(params (string Name, ushort Value)[] values) =>
        values.ToFrozenDictionary(v => v.Name, v => v.Value, StringComparer.Ordinal);
    private static readonly FrozenDictionary<SignalId, FrozenDictionary<string, ushort>> Tables =
        new Dictionary<SignalId, FrozenDictionary<string, ushort>>
        {
            [SignalId.GrabId] = Codes(("Invalid",0),("Gripper1",1),("Gripper2",2)),
            [SignalId.GrabActiveId] = Codes(("Invalid",0),("Gripper1",1),("Gripper2",2)),
            [SignalId.RPosConfirmed] = Codes(("Moving",0),("Arrived",1),("Timeout",2)),
            [SignalId.FlipSorting] = Codes(("Idle",0),("Flip",1),("PutBack",2)),
            [SignalId.FlipUnloadStatus] = Codes(("Idle",0),("Executing",1),("Completed",2),("Failed",3)),
            [SignalId.XPosConfirmed] = Codes(("Moving",0),("Arrived",1),("Timeout",2)),
            [SignalId.YPosConfirmed] = Codes(("Moving",0),("Arrived",1),("Timeout",2)),
            [SignalId.ZCameraPosConfirmed] = Codes(("Moving",0),("Arrived",1),("Timeout",2)),
            [SignalId.ZScanPosConfirmed] = Codes(("Moving",0),("Arrived",1),("Timeout",2)),
            [SignalId.ZGrapPosConfirmed] = Codes(("Moving",0),("Arrived",1),("Timeout",2)),
            [SignalId.FlipStatus] = Codes(("Idle",0),("Executing",1),("Completed",2),("Failed",3)),
            [SignalId.SortingCmd] = Codes(("Idle",0),("Pick",1),("Place",2)),
            [SignalId.SortingExecStatus] = Codes(("Idle",0),("Picked",1),("Placed",2),("GrabFailed",3)),
            [SignalId.AlarmSeverity] = Codes(("Normal",0),("Warning",1),("Fault",2),("Severe",3)),
        }.ToFrozenDictionary();
    private static readonly FrozenDictionary<string, ushort> Boolean = Codes(("False",0),("True",1));
    public static IReadOnlyDictionary<string, ushort>? For(SignalId id) =>
        ConfirmedProtocol.RequiredFields.TryGetValue(id, out var field) && field.ValueType is PlcValueType.Bool or PlcValueType.BoolWord
            ? Boolean : Tables.GetValueOrDefault(id);
    public static ushort Value(SignalId id, string label) => For(id)?[label] ?? throw new ArgumentException("Signal has no enumerated code table.", nameof(id));
    // Confirmed clear/unset representations, not a business state-machine abstraction.
    public static ushort ResetWord(SignalId id) => id switch
    {
        SignalId.SortingCmd => Value(id, "Idle"),
        _ => 0 // numeric targets, identity selectors and existing retry clear
    };
}
