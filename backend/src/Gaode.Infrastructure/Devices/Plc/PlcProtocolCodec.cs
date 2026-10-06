using System.Buffers.Binary;

namespace Gaode.Infrastructure.Devices.Plc;

/// <summary>Small, explicit codec for the configured Modbus/TCP PDU boundary.</summary>
public static class PlcProtocolCodec
{
    public static byte[] Read( byte function, ushort offset, ushort count)
    {
        if (function is not (1 or 3) || count == 0) throw new ArgumentOutOfRangeException(nameof(count));
        var pdu = new byte[5]; pdu[0] = function;
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1, 2), offset);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3, 2), count);
        return pdu;
    }

    public static byte[] WriteSingle(byte function, ushort offset, ushort value)
    {
        if (function is not (5 or 6)) throw new ArgumentOutOfRangeException(nameof(function));
        var pdu = new byte[5]; pdu[0] = function;
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1, 2), offset);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3, 2), value);
        return pdu;
    }

    public static byte[] WriteMultiple(ushort offset, ReadOnlySpan<ushort> values)
    {
        if (values.Length is < 1 or > 123) throw new ArgumentOutOfRangeException(nameof(values));
        var pdu = new byte[6 + values.Length * 2]; pdu[0] = 16;
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(1, 2), offset);
        BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(3, 2), (ushort)values.Length);
        pdu[5] = (byte)(values.Length * 2);
        for (var i = 0; i < values.Length; i++)
            BinaryPrimitives.WriteUInt16BigEndian(pdu.AsSpan(6 + i * 2, 2), values[i]);
        return pdu;
    }
}
