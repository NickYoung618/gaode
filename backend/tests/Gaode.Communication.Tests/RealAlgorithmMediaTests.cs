using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Media;
using Gaode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Gaode.Communication.Tests;

public sealed class RealAlgorithmMediaTests
{
    [Theory]
    [InlineData("a", "raw", "GalaxyRaw")]
    [InlineData("3d", "zip", "CameraProFrameZipV1")]
    public async Task ReadOnlyRealSampleConvertsAndPersistsProvenanceWithoutChangingPixelsOrXyz(string sample, string extension, string format)
    {
        // Explicit offline sample source; no camera/PLC or algorithm provider is constructed.
        var source = Path.Combine("D:\\gaode\\artifacts\\camera-runtime", sample + "-download-1." + extension);
        var sourceHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(source)));
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine("D:\\gaode\\artifacts\\camera-runtime", sample + "-capture-1.json")));
        var metadata = document.RootElement.GetProperty("metadata").Deserialize<CaptureFrameMetadata>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var bytes = await File.ReadAllBytesAsync(source);
        var root = Path.Combine(Path.GetTempPath(), "gaode-022-media-" + Guid.NewGuid().ToString("N"));
        try
        {
            var options = CameraCaptureJournal.Prepare(root);
            var capacity = new MediaCapacity(256 * 1024 * 1024, 0, 256 * 1024 * 1024, 256 * 1024 * 1024);
            var store = new MediaStore(root, capacity, new(), 1);
            var run = Guid.NewGuid(); var capture = Guid.NewGuid();
            await using (var db = new Station01DbContext(options))
            {
                db.Runs.Add(new() { RunId = run, RequestId = run.ToString(), SubjectId = "Test", ContextJson = "{\"purpose\":\"Test\",\"offlineOriginal\":true}",
                    State = RunState.Created, Revision = 1, CreatedUtc = DateTimeOffset.UtcNow }); await db.SaveChangesAsync();
            }
            var fact = new CorrelatedCaptureFact(run, capture, Guid.NewGuid(), 0, "offline-copy", "ReadOnlyRealSample",
                ComponentExecutionOrigin.Unknown, ComponentExecutionOrigin.Unknown, CaptureApplicationState.NotApplied, null, false, [source])
                { FrameMetadata = metadata };
            MediaRef raw;
            using (store.ReserveCapture(capture, metadata.Role, bytes.LongLength))
                raw = (await store.SaveCaptureAsync(run, capture, metadata.Role, "offline", "offline", bytes, format, fact.MediaSource, fact, default)) with { Purpose = "Test" };
            await using var writer = new TraceWriter(options, TimeProvider.System, 8);
            var revision = 1L;
            async Task Save(WriteKind kind, object value)
            {
                var json = JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                var receipt = await writer.SubmitCritical(new(Guid.NewGuid(), run, revision, kind, json,
                    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)))), default).Completion;
                Assert.Equal(CommitState.Committed, receipt.State); revision = receipt.CommittedRevision!.Value;
            }
            await Save(WriteKind.Media, raw);
            await Save(WriteKind.CaptureFact, new { mediaId = raw.MediaId, captureId = capture, captureFact = fact });
            await store.MarkCommittedAsync(raw, default);
            var converted = await store.PrepareAlgorithmInputAsync(raw, default);
            Assert.False(store.IsReady(converted.MediaId));
            await Save(WriteKind.Media, converted);
            await store.MarkCommittedAsync(converted, default);
            Assert.Equal(raw.MediaId, converted.AlgorithmInput!.RawMediaId);
            Assert.Equal(sourceHash, converted.AlgorithmInput.RawSha256);
            Assert.Equal(run, converted.RunId); Assert.Equal(capture, converted.CaptureId);
            var output = await File.ReadAllBytesAsync(Path.Combine(root, converted.RelativeKey));
            if (format == "GalaxyRaw") Assert.Equal(bytes, DecodeMono8Png(output, metadata.Width, metadata.Height));
            else
            {
                var headerEnd = Find(output, Encoding.ASCII.GetBytes("end_header\n")) + "end_header\n".Length;
                Assert.Contains("format binary_little_endian 1.0", Encoding.ASCII.GetString(output, 0, headerEnd));
                using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
                using var xyz = zip.GetEntry("points.xyz.f32")!.Open(); using var copy = new MemoryStream(); xyz.CopyTo(copy);
                Assert.Equal(copy.ToArray(), output[headerEnd..]);
                Assert.Null(converted.AlgorithmInput.PointUnitSource); Assert.Null(converted.AlgorithmInput.CoordinateSource);
            }
            var persistent = capacity.FilesUsed;
            using (store.Lease(converted.MediaId, "Test:algorithm-consumer"))
            { Assert.Equal(converted.ByteLength, capacity.WorkingUsed); Assert.Equal(0, capacity.MemoryUsed); }
            Assert.Equal(0, capacity.WorkingUsed); Assert.Equal(persistent, capacity.FilesUsed);
            Assert.Equal(Directory.GetFiles(Path.Combine(root,"media"),"*",SearchOption.AllDirectories).Sum(p => new FileInfo(p).Length), persistent);
            var restoredCapacity = new MediaCapacity(256 * 1024 * 1024, 0, 256 * 1024 * 1024, 256 * 1024 * 1024);
            var restored = new MediaStore(root, restoredCapacity, new(), 1);
            await new CameraCaptureJournal(options).RestoreAsync(restored, default);
            Assert.True(restored.IsReady(raw.MediaId)); Assert.True(restored.IsReady(converted.MediaId));
            Assert.Equal(persistent, restoredCapacity.FilesUsed);
            Assert.Equal(sourceHash, Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(source))));
            if(Environment.GetEnvironmentVariable("GAODE_022_EVIDENCE_ROOT") is {Length:>0} evidenceRoot)
            {
                var destination=Path.Combine(evidenceRoot,"media-"+sample+"-"+run.ToString("N"));
                Directory.CreateDirectory(destination);
                foreach(var file in Directory.GetFiles(root,"*",SearchOption.AllDirectories))
                {var copy=Path.Combine(destination,Path.GetRelativePath(root,file));Directory.CreateDirectory(Path.GetDirectoryName(copy)!);File.Copy(file,copy);}
                await File.WriteAllTextAsync(Path.Combine(destination,"verification.json"),JsonSerializer.Serialize(new {
                    evidence="Test:offline-read-only-real-media;not-real-algorithm-or-new-capture",source,sourceHash,raw,converted,
                    pixelOrXyzExact=true,sqliteRestoreReady=true,persistentDisk=capacity.FilesUsed,working=capacity.WorkingUsed,memory=capacity.MemoryUsed},
                    new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true}));
            }
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); Directory.Delete(root, true); }
    }
    private static byte[] DecodeMono8Png(byte[] png, int width, int height)
    {
        Assert.Equal(width, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16,4)));
        Assert.Equal(height, BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20,4)));
        Assert.Equal(8, png[24]); Assert.Equal(0, png[25]);
        using var compressed = new MemoryStream();
        for (var position = 8; position < png.Length;)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(position,4));
            if (Encoding.ASCII.GetString(png,position+4,4) == "IDAT") compressed.Write(png,position+8,length);
            position += length + 12;
        }
        compressed.Position = 0; using var zlib = new ZLibStream(compressed,CompressionMode.Decompress);
        var pixels = new byte[checked(width*height)];
        for(var row=0;row<height;row++) { Assert.Equal(0,zlib.ReadByte()); zlib.ReadExactly(pixels.AsSpan(row*width,width)); }
        Assert.Equal(-1,zlib.ReadByte()); return pixels;
    }
    private static int Find(byte[] data, byte[] marker)
    { for(var i=0;i<=data.Length-marker.Length;i++) if(data.AsSpan(i,marker.Length).SequenceEqual(marker)) return i; throw new InvalidDataException("PLY header missing"); }
}
