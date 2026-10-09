using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Gaode.Application.Ports;
using Gaode.Infrastructure.Devices.Cameras;

namespace Gaode.Infrastructure.Media;

// Pixel/point encoding belongs to the media adapter. No model or motion knowledge.
internal static class AlgorithmMediaEncoder
{
    public static (byte[] Bytes, string Format, long? Points) Encode(byte[] raw, string format, CaptureFrameMetadata metadata)
    {
        CameraFrameValidation.Validate(raw, format, metadata, metadata.Role, metadata.Serial);
        if (format == "GalaxyRaw")
        {
            var pixelFormat = metadata.PixelFormat.ToUpperInvariant().Replace("GX_PIXEL_FORMAT_", "").Replace("_", "");
            if (pixelFormat != "MONO8")
                throw new InvalidDataException("AlgorithmPngPixelFormatNotConfirmed");
            return (Png(raw, metadata.Width, metadata.Height), "png", null);
        }
        if (format != "CameraProFrameZipV1") throw new InvalidDataException("AlgorithmInputRawFormatNotConfirmed");
        using var stream = new MemoryStream(raw, false);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
        var xyz = zip.GetEntry("points.xyz.f32") ?? throw new InvalidDataException("AlgorithmPlyXyzMissing");
        var count = checked((long)metadata.Width * metadata.Height);
        if (xyz.Length != checked(count * 12)) throw new InvalidDataException("AlgorithmPlyPointCountMismatch");
        using var output = new MemoryStream();
        output.Write(Encoding.ASCII.GetBytes($"ply\nformat binary_little_endian 1.0\ncomment raw XYZ preserved; units and coordinates from capture metadata\nelement vertex {count}\nproperty float x\nproperty float y\nproperty float z\nend_header\n"));
        using var points = xyz.Open(); points.CopyTo(output);
        return (output.ToArray(), "ply", count);
    }

    private static byte[] Png(byte[] pixels, int width, int height)
    {
        if (pixels.LongLength != checked((long)width * height)) throw new InvalidDataException("AlgorithmPngDimensionsMismatch");
        using var output = new MemoryStream();
        output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = 8; // grayscale Mono8, original rows and dimensions
        Chunk(output, "IHDR", header);
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, true))
            for (var row = 0; row < height; row++) { zlib.WriteByte(0); zlib.Write(pixels, checked(row * width), width); }
        Chunk(output, "IDAT", compressed.ToArray());
        Chunk(output, "IEND", []);
        return output.ToArray();
    }
    private static void Chunk(Stream output, string name, byte[] data)
    {
        Span<byte> integer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(integer, data.Length); output.Write(integer);
        var type = Encoding.ASCII.GetBytes(name); output.Write(type); output.Write(data);
        var crc = uint.MaxValue;
        foreach (var value in type.Concat(data))
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0xedb88320u);
        }
        BinaryPrimitives.WriteUInt32BigEndian(integer, ~crc); output.Write(integer);
    }
}
