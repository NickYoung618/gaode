namespace Gaode.Plc.Protocol;

public enum BoolByteOrder { EvenLow, EvenHigh }

public static class BoolByteCodec
{
    public static int Shift(int byteOffset, BoolByteOrder order)
    {
        if (byteOffset is not (0 or 1) || !Enum.IsDefined(order))
            throw new ArgumentOutOfRangeException(nameof(byteOffset));
        return (byteOffset == 0) == (order == BoolByteOrder.EvenLow) ? 0 : 8;
    }

    public static ushort Decode(ushort word, int byteOffset, BoolByteOrder order)
    {
        var value = (word >> Shift(byteOffset, order)) & 255;
        return value is 0 or 1 ? (ushort)value : throw new InvalidDataException("InvalidBooleanByteFeedback:" + value);
    }

    public static ushort Merge(ushort before, ushort value, int byteOffset, BoolByteOrder order)
    {
        if (value > 1) throw new ArgumentOutOfRangeException(nameof(value));
        var shift = Shift(byteOffset, order);
        return (ushort)((before & ~(255 << shift)) | (value << shift));
    }
}
