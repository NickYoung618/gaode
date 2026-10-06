using System.Collections.Immutable;
using Gaode.Plc.Protocol;

namespace Gaode.Infrastructure.Devices.Plc;

internal sealed record PlcDefinitionTestInput(ProtocolDefinition Definition,
    ImmutableArray<SignalReadGroup>? PreparedPlan = null);

/// <summary>One device's immutable definition admission, shared by both connections.</summary>
internal sealed class PlcDefinitionAdmission(ProtocolDefinition definition,
    ImmutableArray<SignalReadGroup>? testPlan = null)
{
    private readonly object gate = new();
    private ProtocolDefinitionException? rejection;
    private bool admitted;
    internal PreparedPlcReadPlans Plans { get; private set; } = null!;
    public ProtocolDefinition Definition { get; } = definition;
    internal SemaphoreSlim ByteWriteGate { get; } = new(1);
    public bool IsAdmitted { get { lock (gate) return admitted; } }
    public void Prepare()
    {
        lock (gate)
        {
            if (admitted) return;
            if (rejection is not null) throw rejection;
            var violations = Definition.Validate().ToList();
            if (violations.Count == 0)
            {
                var plan = testPlan ?? Definition.ReadPlan(Definition.Fields.Select(p => p.Id));
                violations.AddRange(Definition.ValidatePlan(plan));
                if (!plan.SelectMany(g => g.Fields).Order().SequenceEqual(Definition.Fields.Select(p => p.Id).Order()))
                    violations.Add(new("AccessLengthInvalid", "Prepared read plan must cover each required field exactly once."));
            }
            if (violations.Count != 0) throw rejection = new(violations);
            Plans = new(Definition);
            admitted = true;
        }
    }
    public void RequireAdmitted()
    {
        lock (gate)
        {
            if (rejection is not null) throw rejection;
            if (!admitted) throw new InvalidOperationException("ProtocolDefinitionNotPrepared");
        }
    }
}

public sealed class SignalValues
{
    private readonly IReadOnlyDictionary<SignalId, ushort[]> words;
    private readonly Float32ByteOrder order;
    internal IReadOnlyDictionary<SignalId, ushort[]> Words => words;
    internal Dictionary<SignalId, PlcReadStamp> Stamps { get; } = [];
    internal SignalValues(IReadOnlyDictionary<SignalId, ushort[]> words, Float32ByteOrder order)
    { this.words = words; this.order = order; }
    public ushort Word(SignalId id) => words[id].Length == 1 ? words[id][0] : throw new InvalidOperationException("SignalIsNotScalar");
    public bool Bit(SignalId id) => Word(id) switch { 0 => false, 1 => true, _ => throw new InvalidDataException("InvalidBooleanFeedback") };
    public float Float(SignalId id) => words[id].Length == 2 ? Float32Codec.Decode(words[id][0], words[id][1], order) : throw new InvalidOperationException("SignalIsNotFloat32");
}

internal sealed class PlcSignalAccessor(IPlcTransport transport, PlcDefinitionAdmission admission)
{
    public async Task<SignalValues> ReadAsync(IEnumerable<SignalId> ids, CancellationToken cancellationToken)
    {
        admission.RequireAdmitted();
        return await ReadAsync(admission.Plans.Get(ids), cancellationToken);
    }
    internal async Task<SignalValues> ReadAsync(PreparedPlcReadPlan plan, CancellationToken cancellationToken)
    {
        admission.RequireAdmitted();
        admission.Plans.RequireOwned(plan);
        var result = new Dictionary<SignalId, ushort[]>();
        var stamps = new Dictionary<SignalId, PlcReadStamp>();
        foreach (var group in plan.Blocks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var clock = new PlcExchangeClock { Queued = System.Diagnostics.Stopwatch.GetTimestamp() };
            var previous = PlcExchangeClock.Current.Value;
            PlcExchangeClock.Current.Value = clock;
            ushort[] values;
            try
            {
            values = group.Area == PlcArea.Coil
                ? (await transport.ReadCoilsAsync(checked((ushort)group.Offset), checked((ushort)group.Count), cancellationToken)).Select(b => b ? (ushort)1 : (ushort)0).ToArray()
                : await transport.ReadRegistersAsync(checked((ushort)group.Offset), checked((ushort)group.Count), cancellationToken);
            }
            finally { PlcExchangeClock.Current.Value = previous; }
            if (values.Length != group.Count) throw new InvalidDataException("IncompleteSignalGroup");
            PlcCommunicationMeasurement.Count("DecodedBytes", values.Length * 2);
            foreach (var id in group.Fields)
            {
                var field = admission.Definition[id];
                var fieldWords = values.AsSpan(field.DocumentNumber - 1 - group.Offset, field.RegisterCount).ToArray();
                if (field.ValueType == PlcValueType.BoolByte)
                    fieldWords[0] = BoolByteCodec.Decode(fieldWords[0], field.ByteOffset, admission.Definition.ByteOrderForBools);
                result.Add(id, fieldWords);
                stamps.Add(id, new(clock.Queued, clock.Sent, clock.Ended, clock.StartedUtc, clock.EndedUtc));
            }
        }
        var observed = new SignalValues(result, admission.Definition.ByteOrder);
        foreach (var pair in stamps) observed.Stamps.Add(pair.Key, pair.Value);
        return observed;
    }
    public async Task<ushort> ReadWordAsync(SignalId id, CancellationToken ct) => (await ReadAsync([id], ct)).Word(id);
    public async Task<bool> ReadBitAsync(SignalId id, CancellationToken ct) => (await ReadAsync([id], ct)).Bit(id);
    public Task WriteBitAsync(SignalId id, bool value, CancellationToken ct) => WriteAsync(id, [value ? (ushort)1 : (ushort)0], ct);
    public Task WriteWordAsync(SignalId id, ushort value, CancellationToken ct) => WriteAsync(id, [value], ct);
    public Task WriteWordsAsync(SignalId id, ushort[] values, CancellationToken ct) => WriteAsync(id, values, ct);
    public Task WriteFloatAsync(SignalId id, float value, CancellationToken ct) => WriteAsync(id, Float32Codec.Encode(value, admission.Definition.ByteOrder), ct);
    private async Task WriteAsync(SignalId id, ushort[] values, CancellationToken ct)
    {
        admission.RequireAdmitted();
        ct.ThrowIfCancellationRequested();
        var field = admission.Definition[id];
        if (field.Writer != PlcWriter.Pc || field.Direction != PlcDirection.PcToPlc)
            throw new InvalidOperationException("WriteResponsibilityConflict:" + id);
        if (values.Length != field.RegisterCount || values.Length is < 1 or > 123)
            throw new InvalidOperationException("TypeWidthMismatch:" + id);
        if ((field.ValueType is PlcValueType.Bool or PlcValueType.BoolWord or PlcValueType.BoolByte) && values[0] > 1) throw new InvalidOperationException("InvalidBooleanWrite:" + id);
        var offset = checked((ushort)PlcAddressMap.ToPduOffset(field.DocumentNumber));
        if (field.ValueType == PlcValueType.BoolByte)
        {
            // Both business and heartbeat accessors share this admission. Keep the
            // read/merge/write atomic within this device, including across connections.
            await admission.ByteWriteGate.WaitAsync(ct);
            try
            {
                var before = await transport.ReadRegistersAsync(offset, 1, ct);
                if (before.Length != 1) throw new InvalidDataException("IncompleteSignalGroup");
                var merged = BoolByteCodec.Merge(before[0], values[0], field.ByteOffset, admission.Definition.ByteOrderForBools);
                await transport.WriteRegisterAsync(offset, merged, ct);
            }
            finally { admission.ByteWriteGate.Release(); }
            return;
        }
        if (field.Area == PlcArea.Coil) await transport.WriteCoilAsync(offset, values[0] == 1, ct);
        else if (values.Length == 1) await transport.WriteRegisterAsync(offset, values[0], ct);
        else await transport.WriteRegistersAsync(offset, values, ct);
    }
}
