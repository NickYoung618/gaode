using System.Buffers.Binary;
namespace Gaode.Plc.Protocol;

/// <summary>Byte order of the four Float32 bytes on two Modbus registers.</summary>
public enum Float32ByteOrder { Abcd, Cdab, Badc, Dcba }

public static class Float32Codec
{
    public static ushort[] Encode(float value, Float32ByteOrder order)
    {
        if (!float.IsFinite(value)) throw new ArgumentOutOfRangeException(nameof(value));
        Span<byte> canonical = stackalloc byte[4];
        BinaryPrimitives.WriteSingleBigEndian(canonical, value);
        var wire = Reorder(canonical, order);
        return [BinaryPrimitives.ReadUInt16BigEndian(wire.AsSpan(0, 2)),
            BinaryPrimitives.ReadUInt16BigEndian(wire.AsSpan(2, 2))];
    }

    public static float Decode(ushort first, ushort second, Float32ByteOrder order)
    {
        Span<byte> wire = stackalloc byte[4];
        BinaryPrimitives.WriteUInt16BigEndian(wire[..2], first);
        BinaryPrimitives.WriteUInt16BigEndian(wire[2..], second);
        // Every supported permutation is its own inverse.
        var canonical = Reorder(wire, order);
        var value = BinaryPrimitives.ReadSingleBigEndian(canonical);
        if (!float.IsFinite(value)) throw new InvalidDataException("PLC returned a non-finite Float32 coordinate.");
        return value;
    }

    private static byte[] Reorder(ReadOnlySpan<byte> bytes, Float32ByteOrder order) => order switch
    {
        Float32ByteOrder.Abcd => [bytes[0], bytes[1], bytes[2], bytes[3]],
        Float32ByteOrder.Cdab => [bytes[2], bytes[3], bytes[0], bytes[1]],
        Float32ByteOrder.Badc => [bytes[1], bytes[0], bytes[3], bytes[2]],
        Float32ByteOrder.Dcba => [bytes[3], bytes[2], bytes[1], bytes[0]],
        _ => throw new ArgumentOutOfRangeException(nameof(order))
    };
}
