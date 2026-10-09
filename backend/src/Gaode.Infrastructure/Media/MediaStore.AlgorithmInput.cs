using System.Security.Cryptography;
using System.Text.Json;
using Gaode.Application.Ports;

namespace Gaode.Infrastructure.Media;

public sealed partial class MediaStore
{
    public async ValueTask<MediaRef> PrepareAlgorithmInputAsync(MediaRef raw, CancellationToken token)
    {
        if (!_ready.TryGetValue(raw.MediaId, out var committed) || committed != raw || raw.AlgorithmInput is not null)
            throw new InvalidDataException("AlgorithmInputRequiresCommittedOriginalMedia");
        using var lease = Lease(raw.MediaId, "algorithm-input-conversion");
        var full = Resolve(raw.RelativeKey);
        await using var metadataFile = File.OpenRead(full + ".metadata.json");
        var stored = await JsonSerializer.DeserializeAsync<StoredEvidence>(metadataFile, JsonOptions, token)
            ?? throw new InvalidDataException("AlgorithmInputOriginalEvidenceMissing");
        var metadata = stored.Fact?.FrameMetadata ?? throw new InvalidDataException("AlgorithmInputFrameMetadataMissing");
        if ((stored.Reference with { Purpose=raw.Purpose }) != raw || stored.Fact!.RunId != raw.RunId || stored.Fact.CaptureId != raw.CaptureId || stored.Fact.MediaSource != raw.Source)
            throw new InvalidDataException("AlgorithmInputCaptureIdentityMismatch");
        // Bounded by an existing single-media memory budget, not a batch reservation.
        var pointsBytes = raw.Format == "CameraProFrameZipV1" ? checked((long)metadata.Width * metadata.Height * 12) : 0;
        // Includes array copies, growable stream capacity and per-row PNG filter bytes.
        // This conservative encoding bound is charged against the existing configured limit.
        using var memory = _capacity.ReserveBuffer(checked(raw.ByteLength * 8 + pointsBytes * 6 + metadata.Height * 8L + 65536));
        await _jobs.WaitAsync(token); Interlocked.Increment(ref _activeJobs);
        try
        {
            var bytes = await File.ReadAllBytesAsync(full, token);
            var originalHash = Convert.ToHexString(SHA256.HashData(bytes));
            if (bytes.LongLength != raw.ByteLength || originalHash != stored.Sha256)
                throw new InvalidDataException("AlgorithmInputOriginalDigestMismatch");
            var encoded = AlgorithmMediaEncoder.Encode(bytes, raw.Format, metadata);
            token.ThrowIfCancellationRequested();
            var hash = Convert.ToHexString(SHA256.HashData(encoded.Bytes));
            var provenance = new AlgorithmInputProvenance("algorithm-input/1", raw.MediaId, raw.RelativeKey, raw.Format,
                originalHash, hash, "station01-png-ply", "1", metadata.Width, metadata.Height, metadata.PixelFormat,
                encoded.Points, metadata.ActualParameters.GetValueOrDefault("pointUnit"), metadata.ActualParameters.GetValueOrDefault("coordinateFrame"));
            var id = Guid.NewGuid();
            var relative = Path.Combine("media", raw.RunId.ToString("N"), raw.CaptureId.ToString("N"), id.ToString("N") + "." + encoded.Format);
            var output = Resolve(relative);
            var reference = raw with { MediaId = id, RelativeKey = relative.Replace('\\', '/'), Format = encoded.Format,
                ByteLength = encoded.Bytes.LongLength, AlgorithmInput = provenance };
            using (var disk = _capacity.ReserveFile(encoded.Bytes.LongLength))
            {
                try
                {
                    await using (var file = new FileStream(output + ".partial", FileMode.CreateNew, FileAccess.Write, FileShare.None,
                        65536, FileOptions.Asynchronous | FileOptions.WriteThrough))
                    { await file.WriteAsync(encoded.Bytes, token); await file.FlushAsync(token); file.Flush(true); }
                    File.Move(output + ".partial", output);
                }
                finally
                {
                    var retained = File.Exists(output) ? new FileInfo(output).Length : File.Exists(output + ".partial") ? new FileInfo(output + ".partial").Length : 0;
                    if (retained > 0) disk.Commit(retained);
                }
            }
            await WriteEvidenceAsync(output + ".metadata.json", new(reference, stored.Fact, hash), token);
            return reference; // Caller must persist Media fact; never publish from file completion alone.
        }
        finally { Interlocked.Decrement(ref _activeJobs); _jobs.Release(); }
    }
}
