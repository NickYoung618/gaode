using Gaode.Plc.Protocol;
using Microsoft.Extensions.Options;
using System.Globalization;
using Gaode.Diagnostics;

namespace VirtualPlc;

public sealed record PcWriteEvent(PlcArea Area, int DocumentNumber, ushort Value,
    ushort? TransactionId = null);
public sealed record PcWriteAudit(DateTimeOffset OccurredAtUtc, PlcArea Area,
    int DocumentNumber, ushort Value, bool Accepted, string? RejectionReason)
{
    public string? RequestHex { get; init; }
    public string? ResponseHex { get; init; }
    public DateTimeOffset? ResponseSentAtUtc { get; init; }
    public long Sequence { get; init; }
    public Guid? ConnectionId { get; init; }
    public ushort? TransactionId { get; init; }
    public byte? Function { get; init; }
    public string? ByteOrder { get; init; }
    public string Receipt => Accepted ? "Accepted" : "Rejected";
    public int? StartAddress => PduOffset;
    public int? Count => RegisterCount;
    public DateTimeOffset? ReceivedAtUtc { get; init; }
    public int? PduOffset { get; init; }
    public IReadOnlyList<ushort>? RawWords { get; init; }
    public int? RegisterCount { get; init; }
}
public sealed record SignalChange(long Sequence, DateTimeOffset OccurredAtUtc,
    PlcArea Area, string Address, string Name, PlcDirection Direction,
    string Previous, string Current, string Writer);
public sealed record SignalChangeBatch(long OldestSequence, long LatestSequence,
    bool Gap, IReadOnlyList<SignalChange> Changes);

public sealed class PlcDataStore
{
    private readonly object _gate = new();
    private readonly bool[] _coils;
    private readonly ushort[] _registers;
    public ProtocolDefinition Definition { get; }
    internal TestFeedbackFaults FeedbackFaults { get; } = new();
    public IReadOnlyList<TestFeedbackFaultRecord> GetFeedbackFaults() => FeedbackFaults.Records();
    private readonly Float32ByteOrder _byteOrder;
    private readonly HashSet<int> _writtenFloats = [];
    private readonly List<PcWriteAudit> _writeAudit = [];
    private const int ChangeCapacity = 8192;
    private readonly Queue<SignalChange> _changes = new();
    private long _changeSequence;
    private long _writeSequence;
    private readonly AsyncLocal<(Guid Connection, ushort Transaction, byte Function)?> _transport = new();
    internal void SetTransportContext(Guid connection, ushort transaction, byte function) => _transport.Value = (connection, transaction, function);
    internal void ClearTransportContext() => _transport.Value = null;
    public event Action<PcWriteEvent>? PcValueWritten;
    private HeartbeatDiagnosticWindow? _heartbeatDiagnostics;
    internal HeartbeatDiagnosticWindow HeartbeatDiagnostics(ILogger logger)
    {
        lock (_gate) return _heartbeatDiagnostics ??= new("VirtualPlc/heartbeat", logger);
    }

    public PlcDataStore(IOptions<SimulationOptions> options)
    {
        if (!Enum.TryParse<Float32ByteOrder>(options.Value.Float32ByteOrder, true, out _byteOrder) ||
            !Enum.IsDefined(_byteOrder))
            throw new ArgumentException("Simulation:Float32ByteOrder must be ABCD, CDAB, BADC or DCBA.");
        Definition = ConfirmedProtocol.CreateTest(_byteOrder);
        var violations = Definition.Validate();
        if (violations.Count != 0) throw new ProtocolDefinitionException(violations);
        _coils = new bool[Definition.CoilCapacity];
        _registers = new ushort[Definition.RegisterCapacity];
        SetCoilFromPlc(PlcAddressMap.Coils.PlcModeAuto, true);
    }

    public Float32ByteOrder ByteOrder => _byteOrder;
    public IReadOnlyList<PcWriteAudit> GetWriteAudit()
    {
        lock (_gate) return _writeAudit.ToArray();
    }

    public SignalChangeBatch GetChanges(long after, int limit = 1024)
    {
        lock (_gate)
        {
            var oldest = _changes.Count == 0 ? _changeSequence + 1 : _changes.Peek().Sequence;
            return new(oldest, _changeSequence, after < oldest - 1,
                _changes.Where(change => change.Sequence > after)
                    .Take(Math.Clamp(limit, 1, 4096)).ToArray());
        }
    }

    private void RecordChange(PlcPoint point, PlcArea area, string previous,
        string current, string writer)
    {
        if (previous == current) return;
        _changes.Enqueue(new(++_changeSequence, DateTimeOffset.UtcNow, area,
            point.DocumentAddress, point.Name, point.Direction, previous, current, writer));
        if (_changes.Count > ChangeCapacity) _changes.Dequeue();
    }

    private static string FloatText(float value) => value.ToString("G9", CultureInfo.InvariantCulture);

    public bool[] ReadCoils(int pduOffset, int count)
    {
        ValidateRange(pduOffset, count, _coils.Length);
        lock (_gate) return _coils.AsSpan(pduOffset, count).ToArray();
    }

    public ushort[] ReadHoldingRegisters(int pduOffset, int count)
    {
        ValidateRange(pduOffset, count, _registers.Length);
        lock (_gate) return _registers.AsSpan(pduOffset, count).ToArray();
    }

    public bool ReadCoilByDocumentNumber(int number)
    {
        lock (_gate) return _coils[PlcAddressMap.ToPduOffset(number)];
    }

    public ushort ReadHoldingRegisterByDocumentNumber(int number)
    {
        lock (_gate) return _registers[PlcAddressMap.ToPduOffset(number)];
    }

    public float ReadFloatByDocumentNumber(int number)
        => ReadFloat(number, PlcDirection.PcToPlc);

    public float ReadActualFloat(int number)
        => ReadFloat(number, PlcDirection.PlcToPc);

    private float ReadFloat(int number, PlcDirection direction)
    {
        RequirePoint(number, direction, PlcValueType.Float32);
        lock (_gate)
        {
            var at = PlcAddressMap.ToPduOffset(number);
            return Float32Codec.Decode(_registers[at], _registers[at + 1], _byteOrder);
        }
    }

    public bool WasFloatWritten(int number)
    {
        lock (_gate) return _writtenFloats.Contains(number);
    }

    public bool TryWriteCoilsFromPc(int pduOffset, IReadOnlyList<bool> values,
        ushort? transactionId = null)
    {
        if (!ValidRange(pduOffset, values.Count, _coils.Length)) return false;
        for (var i = 0; i < values.Count; i++)
            if (!PlcAddressMap.CoilPoints.TryGetValue(pduOffset + i + 1, out var p) ||
                p.Direction != PlcDirection.PcToPlc) return false;
        lock (_gate)
        {
            for (var i = 0; i < values.Count; i++)
            {
                var point = PlcAddressMap.CoilPoints[pduOffset + i + 1];
                var previous = _coils[pduOffset + i] ? "1" : "0";
                _coils[pduOffset + i] = values[i];
                RecordChange(point, PlcArea.Coil, previous, values[i] ? "1" : "0", "PC");
                Record(new(DateTimeOffset.UtcNow, PlcArea.Coil, pduOffset + i + 1,
                    values[i] ? (ushort)1 : (ushort)0, true, null) { PduOffset = pduOffset, RegisterCount = values.Count, RawWords = values.Select(x => x ? (ushort)1 : (ushort)0).ToArray() });
            }
        }
        for (var i = 0; i < values.Count; i++)
            PcValueWritten?.Invoke(new(PlcArea.Coil, pduOffset + i + 1,
                values[i] ? (ushort)1 : (ushort)0, transactionId));
        return true;
    }

    public void SetCoilFromPcForSimulation(int documentNumber, bool value)
    {
        if (!CoilPointsForPc(documentNumber)) throw new InvalidOperationException("PLC does not accept this simulation input.");
        lock (_gate)
        {
            var at = PlcAddressMap.ToPduOffset(documentNumber);
            var previous = _coils[at] ? "1" : "0";
            _coils[at] = value;
            RecordChange(PlcAddressMap.CoilPoints[documentNumber], PlcArea.Coil,
                previous, value ? "1" : "0", "PC");
            Record(new(DateTimeOffset.UtcNow, PlcArea.Coil, documentNumber,
                value ? (ushort)1 : (ushort)0, true, null));
        }
        PcValueWritten?.Invoke(new(PlcArea.Coil, documentNumber, value ? (ushort)1 : (ushort)0));
    }

    private static bool CoilPointsForPc(int documentNumber) =>
        PlcAddressMap.CoilPoints.TryGetValue(documentNumber, out var point) && point.Direction == PlcDirection.PcToPlc;

    public bool TryWriteHoldingRegistersFromPc(int pduOffset, IReadOnlyList<ushort> values)
    {
        if (!ValidRange(pduOffset, values.Count, _registers.Length)) return false;
        var start = pduOffset + 1;
        var end = start + values.Count;
        var points = new List<PlcPoint>();
        for (var at = start; at < end;)
        {
            var point = PlcAddressMap.RegisterAt(at);
            if (point is null || point.DocumentNumber != at ||
                point.Direction != PlcDirection.PcToPlc ||
                at + point.RegisterCount > end) return false;
            var index = at - start;
            if ((point.ValueType is PlcValueType.Bool or PlcValueType.BoolWord) && values[index] > 1) return false;
            if (point.DocumentNumber == PlcAddressMap.HoldingRegisters.TeachPosSelect &&
                values[index] != 0 && !ValidTeachId(values[index])) return false;
            if (point.ValueType == PlcValueType.Float32)
            {
                var decoded = Float32Codec.Decode(values[index], values[index + 1], _byteOrder);
                if (!float.IsFinite(decoded)) return false;
            }
            points.Add(point);
            at += point.RegisterCount;
        }
        lock (_gate)
        {
            var previous = points.Select(point =>
            {
                var at = PlcAddressMap.ToPduOffset(point.DocumentNumber);
                return point.ValueType == PlcValueType.Float32
                    ? FloatText(Float32Codec.Decode(_registers[at], _registers[at + 1], _byteOrder))
                    : _registers[at].ToString(CultureInfo.InvariantCulture);
            }).ToArray();
            for (var i = 0; i < values.Count; i++) _registers[pduOffset + i] = values[i];
            foreach (var point in points.Where(p => p.ValueType == PlcValueType.Float32))
                _writtenFloats.Add(point.DocumentNumber);
            for (var i = 0; i < points.Count; i++)
            {
                var point = points[i];
                var at = PlcAddressMap.ToPduOffset(point.DocumentNumber);
                var current = point.ValueType == PlcValueType.Float32
                    ? FloatText(Float32Codec.Decode(_registers[at], _registers[at + 1], _byteOrder))
                    : _registers[at].ToString(CultureInfo.InvariantCulture);
                RecordChange(point, PlcArea.HoldingRegister, previous[i], current, "PC");
                Record(new(DateTimeOffset.UtcNow, PlcArea.HoldingRegister,
                    point.DocumentNumber, values[point.DocumentNumber - start], true, null)
                    { PduOffset = pduOffset, RawWords = values.ToArray(), RegisterCount = values.Count });
            }
        }
        foreach (var point in points)
            PcValueWritten?.Invoke(new(PlcArea.HoldingRegister, point.DocumentNumber,
                values[point.DocumentNumber - start]));
        return true;
    }

    public void SetCoilFromPlc(int number, bool value)
    {
        if (!PlcAddressMap.CoilPoints.TryGetValue(number, out var point) ||
            point.Direction != PlcDirection.PlcToPc) throw new InvalidOperationException("PLC does not own coil.");
        lock (_gate)
        {
            var at = PlcAddressMap.ToPduOffset(number);
            var previous = _coils[at] ? "1" : "0";
            _coils[at] = value;
            RecordChange(point, PlcArea.Coil, previous, value ? "1" : "0", "PLC");
        }
    }

    public void SetHoldingRegisterFromPlc(int number, ushort value)
    {
        var point = PlcAddressMap.HoldingRegisterPoints.GetValueOrDefault(number);
        if (point is null || point.Direction != PlcDirection.PlcToPc ||
            point.ValueType == PlcValueType.Float32 ||
            ((point.ValueType is PlcValueType.Bool or PlcValueType.BoolWord) && value > 1))
            throw new InvalidOperationException($"Wrong PLC scalar write at 4x{number:X4}.");
        lock (_gate)
        {
            var at = PlcAddressMap.ToPduOffset(number);
            var previous = _registers[at].ToString(CultureInfo.InvariantCulture);
            _registers[at] = value;
            RecordChange(point, PlcArea.HoldingRegister, previous,
                value.ToString(CultureInfo.InvariantCulture), "PLC");
        }
    }

    public void SetFloatFromPlc(int number, float value)
    {
        RequirePoint(number, PlcDirection.PlcToPc, PlcValueType.Float32);
        if (!float.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
        var words = Float32Codec.Encode(value, _byteOrder);
        lock (_gate)
        {
            var at = PlcAddressMap.ToPduOffset(number);
            var previous = FloatText(Float32Codec.Decode(_registers[at], _registers[at + 1], _byteOrder));
            _registers[at] = words[0];
            _registers[at + 1] = words[1];
            RecordChange(PlcAddressMap.HoldingRegisterPoints[number], PlcArea.HoldingRegister,
                previous, FloatText(value), "PLC");
        }
    }

    public void InvalidateFloatTargets()
    {
        lock (_gate)
        {
            _writtenFloats.Clear();
            foreach (var point in PlcAddressMap.HoldingRegisterPoints.Values.Where(
                p => p.Direction == PlcDirection.PcToPlc && p.ValueType == PlcValueType.Float32))
            {
                var at = PlcAddressMap.ToPduOffset(point.DocumentNumber);
                var previous = FloatText(Float32Codec.Decode(_registers[at], _registers[at + 1], _byteOrder));
                _registers[at] = 0;
                _registers[at + 1] = 0;
                RecordChange(point, PlcArea.HoldingRegister, previous, "0", "PLC");
            }
        }
    }

    public void ResetPcWritableValues()
    {
        lock (_gate)
        {
            _writtenFloats.Clear();
            foreach (var point in PlcAddressMap.CoilPoints.Values.Where(p => p.Direction == PlcDirection.PcToPlc))
            {
                var at = PlcAddressMap.ToPduOffset(point.DocumentNumber);
                var previous = _coils[at] ? "1" : "0";
                _coils[at] = false;
                RecordChange(point, PlcArea.Coil, previous, "0", "PLC reset");
            }
            foreach (var point in PlcAddressMap.HoldingRegisterPoints.Values.Where(p => p.Direction == PlcDirection.PcToPlc))
            {
                var at = PlcAddressMap.ToPduOffset(point.DocumentNumber);
                var previous = point.ValueType == PlcValueType.Float32
                    ? FloatText(Float32Codec.Decode(_registers[at], _registers[at + 1], _byteOrder))
                    : _registers[at].ToString(CultureInfo.InvariantCulture);
                for (var i = 0; i < point.RegisterCount; i++)
                    _registers[at + i] = 0;
                if (point.ValueType != PlcValueType.Float32) _registers[at] = SignalCodes.ResetWord(point.Id);
                RecordChange(point, PlcArea.HoldingRegister, previous,
                    _registers[at].ToString(CultureInfo.InvariantCulture), "PLC reset");
            }
        }
    }

    public SimulatorSnapshot CreateSnapshot(bool communicationTimedOut, string? activeAction,
        IEnumerable<SimulationFault> activeFaults)
    {
        lock (_gate)
        {
            var coils = PlcAddressMap.CoilPoints.Values.OrderBy(p => p.DocumentNumber)
                .Select(p => new CoilValue(p.DocumentAddress, PlcAddressMap.ToPduOffset(p.DocumentNumber),
                    p.Name, p.Direction.ToString(), _coils[PlcAddressMap.ToPduOffset(p.DocumentNumber)]))
                .ToArray();
            var registers = PlcAddressMap.HoldingRegisterPoints.Values.OrderBy(p => p.DocumentNumber)
                .Select(p =>
                {
                    var at = PlcAddressMap.ToPduOffset(p.DocumentNumber);
                    var words = _registers.AsSpan(at, p.RegisterCount).ToArray();
                    return new RegisterValue(p.DocumentAddress, at, p.Name, p.Direction.ToString(),
                        words[0], unchecked((short)words[0]), p.ValueType.ToString(), words,
                        p.ValueType == PlcValueType.Float32
                            ? Float32Codec.Decode(words[0], words[1], _byteOrder) : null);
                }).ToArray();
            return new(DateTimeOffset.UtcNow, communicationTimedOut, activeAction,
                activeFaults.Select(f => f.ToString()).OrderBy(f => f).ToArray(), coils, registers);
        }
    }

    private static bool ValidTeachId(ushort id) =>
        id is >= 1 and <= 15 or >= 101 and <= 105 or
              >= 201 and <= 205 or >= 301 and <= 315;

    private static void RequirePoint(int number, PlcDirection direction, PlcValueType type)
    {
        if (!PlcAddressMap.HoldingRegisterPoints.TryGetValue(number, out var p) ||
            p.Direction != direction || p.ValueType != type)
            throw new InvalidOperationException($"Wrong ownership or type for 4x{number:X4}.");
    }

    private static bool ValidRange(int offset, int count, int length) =>
        offset >= 0 && count > 0 && (long)offset + count <= length;
    private static void ValidateRange(int offset, int count, int length)
    {
        if (!ValidRange(offset, count, length)) throw new ArgumentOutOfRangeException(nameof(offset));
    }

    internal void RecordWriteResponse(byte[] pdu, byte[] response, Guid connection, ushort transaction,
        DateTimeOffset receivedAt, DateTimeOffset? sentAt, byte[] requestFrame, byte[]? responseFrame) {
        if (pdu.Length < 5 || pdu[0] is not (5 or 6 or 15 or 16)) return;
        var start = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(1,2));
        var count = pdu[0] is 5 or 6 ? 1 : System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(3,2));
        ushort[]? words = pdu[0] is 5 or 6 ? [System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(3,2))] :
            pdu[0] == 16 && pdu.Length >= 6 + count*2 ? Enumerable.Range(0,count).Select(i => System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(pdu.AsSpan(6+i*2,2))).ToArray() : null;
        var accepted = response.Length > 0 && response[0] == pdu[0];
        lock (_gate) {
            var found = false;
            for (var i = _writeAudit.Count - 1; i >= 0; i--) {
                var prior = _writeAudit[i];
                if (prior.ConnectionId != connection || prior.TransactionId != transaction || prior.Function != pdu[0] || prior.ResponseSentAtUtc is not null) continue;
                found = true;
                _writeAudit[i] = prior with { RequestHex = Convert.ToHexString(requestFrame), ResponseHex = responseFrame is null ? null : Convert.ToHexString(responseFrame), ReceivedAtUtc = receivedAt,
                    ResponseSentAtUtc = sentAt, PduOffset = start, RegisterCount = count, RawWords = words ?? prior.RawWords };
            }
            // Invalid ranges can reject before a logical point write. Preserve that received frame too.
            if (!found) Record(new(receivedAt, pdu[0] is 5 or 15 ? PlcArea.Coil : PlcArea.HoldingRegister,
                start+1, words?.FirstOrDefault() ?? 0, accepted, accepted ? null : "ModbusException:" + Convert.ToHexString(response)) {
                ConnectionId = connection, TransactionId = transaction, Function = pdu[0], PduOffset = start,
                RegisterCount = count, RawWords = words, RequestHex = Convert.ToHexString(requestFrame), ResponseHex = responseFrame is null ? null : Convert.ToHexString(responseFrame), ReceivedAtUtc = receivedAt, ResponseSentAtUtc = sentAt });
        }
    }

    private void Record(PcWriteAudit entry)
    {
        if (_writeAudit.Count >= 4096) _writeAudit.RemoveAt(0);
        var context = _transport.Value;
        _writeAudit.Add(entry with { Sequence = ++_writeSequence, ConnectionId = context?.Connection ?? entry.ConnectionId,
            TransactionId = context?.Transaction ?? entry.TransactionId, Function = context?.Function ?? entry.Function, ByteOrder = _byteOrder.ToString() });
    }
}
