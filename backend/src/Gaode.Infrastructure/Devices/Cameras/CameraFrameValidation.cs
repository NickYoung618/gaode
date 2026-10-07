using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Gaode.Application.Ports;

namespace Gaode.Infrastructure.Devices.Cameras;

/// <summary>Validate the defined raw formats before a worker frame becomes application evidence.</summary>
public static class CameraFrameValidation
{
    public static void Validate(byte[] bytes, string format, CaptureFrameMetadata m, string role, string serial)
    {
        if (m.Role != role || m.Serial != serial || m.Width <= 0 || m.Height <= 0 || m.PayloadBytes != bytes.LongLength)
            throw new InvalidDataException("CameraFrameIdentityOrDimensionsInvalid");
        long Param(string name) => long.Parse(m.ActualParameters[name], CultureInfo.InvariantCulture);
        if (role != "3D")
        {
            if (format != "GalaxyRaw" || Param("Width") != m.Width || Param("Height") != m.Height ||
                Param("PayloadSize") != bytes.LongLength || m.Payloads.Count != 1)
                throw new InvalidDataException("GalaxyFrameLayoutInvalid");
            var pixel = NormalizePixel(m.PixelFormat);
            if (pixel != NormalizePixel(m.ActualParameters["PixelFormat"])) throw new InvalidDataException("GalaxyPixelFormatMismatch");
            var elementBytes = pixel switch { "MONO8" => 1, "MONO10" or "MONO12" or "MONO16" => 2,
                "RGB8" or "BGR8" or "RGB8PACKED" or "BGR8PACKED" => 3, _ => 0 };
            // Packed/high-bit-depth formats retain the SDK payload size; never apply Mono8's formula to them.
            if (elementBytes != 0 && bytes.LongLength != checked((long)m.Width * m.Height * elementBytes))
                throw new InvalidDataException("GalaxyPixelDimensionsMismatch");
            ValidatePayload(m.Payloads[0], "frame.raw", bytes.LongLength, 1, bytes.LongLength,
                Convert.ToHexString(SHA256.HashData(bytes)));
            return;
        }
        if (format != "CameraProFrameZipV1") throw new InvalidDataException("CameraProFormatInvalid");
        var width = Param("irWidth"); var height = Param("irHeight"); var pixelBytes = Param("pixelBytes");
        if (width != m.Width || height != m.Height || pixelBytes is not (1 or 2) ||
            m.PixelFormat != $"CameraPro XYZ-f32 Depth-f32 IR-u{pixelBytes * 8}" || m.ActualParameters["byteOrder"] != "little-endian")
            throw new InvalidDataException("CameraProDimensionsInvalid");
        var pixels = checked(width * height);
        var depth = Param("depthType") switch { 1 => checked(Param("textureWidth") * Param("textureHeight")),
            2 => pixels, _ => throw new InvalidDataException("CameraProDepthTypeInvalid") };
        var groups = Param("reconstructionType") switch { 0 => 2, 2 => 1, _ => throw new InvalidDataException("CameraProReconstructionTypeInvalid") };
        if (depth <= 0 || Param("irImagesPerCamera") != 2 || Param("irCameraGroups") != groups || Param("irImageBytes") != pixels * pixelBytes)
            throw new InvalidDataException("CameraProIrLayoutInvalid");
        var expected = new[] { (Name: "points.xyz.f32", Elements: checked(pixels * 3), Size: 4),
            (Name: "depth.f32", Elements: depth, Size: 4),
            (Name: "ir.bytes", Elements: checked(pixels * 2 * groups), Size: checked((int)pixelBytes)) };
        using var input = new MemoryStream(bytes, false);
        using var zip = new ZipArchive(input, ZipArchiveMode.Read);
        if (zip.Entries.Count != 4 || m.Payloads.Count != 3 || zip.Entries.Select(e => e.FullName).Distinct().Count() != 4)
            throw new InvalidDataException("CameraProRequiredChannelsInvalid");
        foreach (var e in expected)
        {
            var entry = zip.GetEntry(e.Name) ?? throw new InvalidDataException("CameraProRequiredChannelMissing:" + e.Name);
            using var stream = entry.Open();
            var p = m.Payloads.SingleOrDefault(p => p.Name == e.Name) ?? throw new InvalidDataException("CameraProChannelMetadataMissing:" + e.Name);
            ValidatePayload(p, e.Name, e.Elements, e.Size, entry.Length, Convert.ToHexString(SHA256.HashData(stream)));
        }
        var manifestEntry = zip.GetEntry("metadata.json") ?? throw new InvalidDataException("CameraProManifestMissing");
        if (manifestEntry.Length > CameraWorkerProtocol.MaxHeaderBytes) throw new InvalidDataException("CameraProManifestTooLarge");
        using var manifestStream = manifestEntry.Open();
        using var document = JsonDocument.Parse(manifestStream);
        var manifest = document.RootElement;
        if (manifest.GetProperty("role").GetString() != role || manifest.GetProperty("serial").GetString() != serial ||
            manifest.GetProperty("workerSessionId").GetGuid() != m.WorkerSessionId || manifest.GetProperty("frameIndex").GetUInt64() != m.FrameId ||
            manifest.GetProperty("triggerSequence").GetInt64() != m.TriggerSequence || manifest.GetProperty("frameTimestamp").GetDouble() != m.DeviceTimestamp ||
            manifest.GetProperty("rawPayloadBytes").GetInt64() != expected.Sum(e => checked(e.Elements * e.Size)))
            throw new InvalidDataException("CameraProManifestFrameMismatch");
        var innerPayloads = manifest.GetProperty("payloads").Deserialize<FramePayload[]>(CameraWorkerProtocol.Json);
        if (innerPayloads is null || !innerPayloads.SequenceEqual(m.Payloads)) throw new InvalidDataException("CameraProManifestPayloadMismatch");
        var innerParameters = manifest.GetProperty("actualParameters").Deserialize<Dictionary<string, string>>(CameraWorkerProtocol.Json);
        if (innerParameters is null || m.ActualParameters.Any(p => innerParameters.GetValueOrDefault(p.Key) != p.Value))
            throw new InvalidDataException("CameraProManifestParametersMismatch");
    }
    private static string NormalizePixel(string pixel) => pixel.ToUpperInvariant().Replace("GX_PIXEL_FORMAT_", "").Replace("_", "");
    private static void ValidatePayload(FramePayload p, string name, long elements, int size, long bytes, string hash)
    {
        if (p.Name != name || p.ElementCount != elements || p.ElementBytes != size || p.ByteLength != bytes ||
            checked(elements * size) != bytes || !string.Equals(p.Sha256, hash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("CameraPayloadStructureOrDigestInvalid:" + name);
    }
}
