using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Gaode.Plc.Protocol;

public sealed record AlarmBitDefinition(string Name, SignalId Field, int Bit, ushort Severity);
public sealed record SignalReadGroup(PlcArea Area, int Offset, int Count, ImmutableArray<SignalId> Fields);
public sealed record DefinitionViolation(string Reason, string Detail);
public sealed class ProtocolDefinitionException(IReadOnlyList<DefinitionViolation> violations)
    : InvalidOperationException(string.Join("; ", violations.Select(v => v.Reason + ": " + v.Detail)))
{
    public IReadOnlyList<DefinitionViolation> Violations { get; } = violations;
}

// Fixed connection profile, frozen when an adapter is constructed. Not a runtime protocol switch.
public sealed class ProtocolDefinition
{
    public ImmutableArray<PlcPoint> Fields { get; }
    public ImmutableArray<AlarmBitDefinition> AlarmBits { get; }
    public int CoilCapacity { get; }
    public int RegisterCapacity { get; }
    public Float32ByteOrder ByteOrder { get; }
    public BoolByteOrder ByteOrderForBools { get; init; } = BoolByteOrder.EvenLow;
    public string Purpose { get; init; } = "Unspecified";
    public string SourceReference { get; init; } = "";
    public ProtocolDefinition(IEnumerable<PlcPoint> fields, IEnumerable<AlarmBitDefinition> alarmBits,
        int coilCapacity, int registerCapacity, Float32ByteOrder byteOrder)
    {
        Fields = fields.ToImmutableArray();
        AlarmBits = alarmBits.ToImmutableArray();
        CoilCapacity = coilCapacity;
        RegisterCapacity = registerCapacity;
        ByteOrder = byteOrder;
    }

    public PlcPoint this[SignalId id] => Fields.Single(p => p.Id == id);
    public int Capacity(PlcArea area) => area == PlcArea.Coil ? CoilCapacity : RegisterCapacity;

    public ImmutableArray<SignalReadGroup> ReadPlan(IEnumerable<SignalId> requested)
    {
        var groups = new List<SignalReadGroup>();
        foreach (var field in requested.Distinct().Select(id => this[id]).OrderBy(p => p.Area).ThenBy(p => p.DocumentNumber))
        {
            var offset = field.DocumentNumber - 1;
            var max = field.Area == PlcArea.Coil ? 2000 : 125;
            if (groups.LastOrDefault() is { } previous && previous.Area == field.Area &&
                offset <= previous.Offset + previous.Count &&
                Math.Max(previous.Offset + previous.Count, offset + field.RegisterCount) - previous.Offset <= max)
                groups[^1] = previous with { Count = Math.Max(previous.Offset + previous.Count, offset + field.RegisterCount) - previous.Offset, Fields = previous.Fields.Add(field.Id) };
            else groups.Add(new(field.Area, offset, field.RegisterCount, [field.Id]));
        }
        return groups.ToImmutableArray();
    }

    public IReadOnlyList<DefinitionViolation> Validate()
    {
        var errors = new List<DefinitionViolation>();
        void Fail(string reason, string detail) => errors.Add(new(reason, detail));
        if (CoilCapacity is < 1 or > 65536 || RegisterCapacity is < 1 or > 65536)
            Fail("AddressRangeInvalid", "Profile capacities must fit a Modbus address area.");
        if (!Enum.IsDefined(ByteOrder)) Fail("TypeWidthMismatch", "Unsupported Float32 byte order.");
        if (!Enum.IsDefined(ByteOrderForBools)) Fail("TypeWidthMismatch", "Unsupported BOOL byte order.");
        var requirements = ConfirmedProtocol.RequiredFields;
        foreach (var required in requirements.Values)
        {
            var entries = Fields.Where(p => p.Id == required.Id).ToArray();
            if (entries.Length != 1) { Fail("RequiredSignalMissing", $"{required.Id}: expected exactly one definition, found {entries.Length}."); continue; }
            var actual = entries[0];
            if (!actual.HostReadable) Fail("ReadDirectionConflict", $"{actual.Id}: Host read/echo inspection required by the partitioned contract.");
            if (actual.Direction != required.Direction || actual.Writer != required.Writer || actual.ClearWriter != required.ClearWriter)
                Fail("WriteResponsibilityConflict", $"{actual.Id}: direction/writer/clear owner differs from the confirmed responsibility.");
            var byteBoolean = required.ValueType == PlcValueType.Bool && actual.ValueType == PlcValueType.BoolByte && actual.Area == PlcArea.HoldingRegister;
            if (!byteBoolean && (actual.ValueType != required.ValueType || actual.Area != required.Area))
                Fail("TypeWidthMismatch", $"{actual.Id}: expected {required.Area}/{required.ValueType}.");
        }
        foreach (var field in Fields)
        {
            if (!requirements.ContainsKey(field.Id)) Fail("RequiredSignalMissing", $"Unconfirmed signal {field.Id}.");
            var width = field.ValueType == PlcValueType.Float32 ? 2 : field.ValueType == PlcValueType.Words ? field.RegisterCount : 1;
            if (!Enum.IsDefined(field.ValueType) || !Enum.IsDefined(field.Area) || field.RegisterCount != width || width is < 1 or > 123 ||
                field.Area == PlcArea.Coil && field.ValueType != PlcValueType.Bool ||
                (field.ValueType == PlcValueType.BoolByte ? field.ByteOffset is not (0 or 1) || field.Area != PlcArea.HoldingRegister : field.ByteOffset != 0))
                Fail("TypeWidthMismatch", $"{field.Id}: {field.ValueType} declared {field.RegisterCount}, expected {width}.");
            if (field.DocumentNumber < 1 || (long)field.DocumentNumber - 1 + field.RegisterCount > Capacity(field.Area))
                Fail("AddressRangeInvalid", $"{field.Id}: [{field.DocumentNumber - 1},{(long)field.DocumentNumber - 1 + field.RegisterCount}) exceeds {field.Area} capacity {Capacity(field.Area)}.");
        }
        foreach (var area in Fields.GroupBy(p => p.Area))
        {
            var sorted = area.Select(p => new { Point = p,
                Start = area.Key == PlcArea.Coil ? (long)p.DocumentNumber - 1 : ((long)p.DocumentNumber - 1) * 2 + p.ByteOffset,
                Width = area.Key == PlcArea.Coil ? p.RegisterCount : p.ValueType == PlcValueType.BoolByte ? 1 : p.RegisterCount * 2
            }).OrderBy(p => p.Start).ToArray();
            for (var i = 0; i < sorted.Length; i++)
                for (var j = i + 1; j < sorted.Length && sorted[j].Start < sorted[i].Start + sorted[i].Width; j++)
                    Fail("FieldOverlap", $"{area.Key}: {sorted[i].Point.Id} and {sorted[j].Point.Id} storage intervals intersect.");
        }
        foreach (var bit in AlarmBits)
        {
            var fields = Fields.Where(p => p.Id == bit.Field).ToArray();
            if (fields.Length != 1 || bit.Bit < 0 || bit.Bit >= fields[0].RegisterCount * (fields[0].Area == PlcArea.Coil ? 1 : fields[0].ValueType == PlcValueType.BoolByte ? 8 : 16))
                Fail("BitOutOfRange", $"{bit.Name}: bit {bit.Bit} outside {bit.Field}.");
        }
        if (AlarmBits.Select(b => b.Name).Distinct().Count() != AlarmBits.Length ||
            AlarmBits.Select(b => (b.Field, b.Bit)).Distinct().Count() != AlarmBits.Length)
            Fail("FieldOverlap", "Alarm identities or bit interpretations are duplicated.");
        return errors;
    }

    public IReadOnlyList<DefinitionViolation> ValidatePlan(IEnumerable<SignalReadGroup> plan)
    {
        var errors = new List<DefinitionViolation>();
        foreach (var group in plan)
        {
            var max = group.Area == PlcArea.Coil ? 2000 : 125;
            if (!Enum.IsDefined(group.Area) || group.Count < 1 || group.Count > max || group.Offset < 0 ||
                (long)group.Offset + group.Count > Capacity(group.Area))
            { errors.Add(new("AccessLengthInvalid", $"{group.Area}/{group.Offset}/{group.Count}: illegal request.")); continue; }
            var fields = group.Fields.Select(id => Fields.SingleOrDefault(p => p.Id == id)).ToArray();
            if (fields.Length == 0 || fields.Any(p => p is null || p.Area != group.Area || !p.HostReadable) ||
                group.Fields.Distinct().Count() != fields.Length)
            { errors.Add(new("AccessLengthInvalid", "Request must contain known readable fields in its own area.")); continue; }
            var next = group.Offset;
            foreach (var field in fields.OrderBy(p => p!.DocumentNumber))
            {
                var start = field!.DocumentNumber - 1;
                if (start < group.Offset || start > next) errors.Add(new("AccessLengthInvalid", "Request crosses undeclared storage or cuts a field."));
                next = Math.Max(next, start + field.RegisterCount);
            }
            if (next != group.Offset + group.Count) errors.Add(new("AccessLengthInvalid", "Request width differs from its complete fields."));
        }
        return errors;
    }
}

public static class ConfirmedProtocol
{
    internal static readonly FrozenDictionary<SignalId, PlcPoint> RequiredFields = PlcAddressMap.CoilPoints.Values
        .Concat(PlcAddressMap.HoldingRegisterPoints.Values).ToFrozenDictionary(p => p.Id);
    // Literal alarm meanings from the confirmed source §2.7; not host business states.
    private static readonly ImmutableArray<AlarmBitDefinition> Bits =
    [
        new("LightCurtain", SignalId.AlarmBits, 0, 3), new("EmergencyStop", SignalId.AlarmBits, 1, 3),
        new("SafetyDoor", SignalId.AlarmBits, 2, 3), new("ServoOverload", SignalId.AlarmBits, 3, 3),
        new("ManualFlipOccupied", SignalId.AlarmBits, 4, 2), new("AirPressure", SignalId.AlarmBits, 5, 2),
        new("Communication", SignalId.AlarmBits, 6, 3), new("PlcInternal", SignalId.AlarmBits, 7, 3),
        new("PalletNotLocked", SignalId.AlarmBits, 8, 2), new("FocusMotionFailure", SignalId.AlarmBits, 9, 2),
        new("GrabFailure", SignalId.AlarmBits, 10, 2), new("ZoneFull", SignalId.AlarmBits, 11, 1),
        new("MesOffline", SignalId.AlarmBits, 12, 1)
    ];
    // Existing explicit Test storage capacities. This does not approve a physical PLC profile.
    public static ProtocolDefinition CreateTest(Float32ByteOrder order) => new(RequiredFields.Values, Bits, 64, 256, order)
    { Purpose = "Test", SourceReference = $"011-explicit-test-address-map;semantics:{PlcAddressMap.SourceSha256};014-additional-semantics:{PlcAddressMap.RotationSourceSha256}" };
}
