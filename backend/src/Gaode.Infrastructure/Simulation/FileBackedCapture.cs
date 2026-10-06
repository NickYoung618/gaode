using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Diagnostics;

namespace Gaode.Infrastructure.Simulation;

/// <summary>Test camera: each formal request reads a frozen image and consumes real wall time.</summary>
public sealed class FileBackedCapture : ICapturePort
{
    private readonly IReadOnlyDictionary<(CaptureRole Role, string Camera), (string Path, string Digest)> images;
    private readonly TimeSpan delay;
    private readonly ConcurrentDictionary<CaptureRole, int> counts = new();
    private int activeExecutions;
    public int ActiveExecutions => Volatile.Read(ref activeExecutions);
    public long ConnectionEpoch => 1;
    public string MediaSource => "Test/FixedImage";
    public Gaode.Domain.Station01.ComponentExecutionOrigin CameraOrigin => new(
        Gaode.Domain.Station01.ComponentEvidenceSource.Test, "FileBackedCapture/1", "RecordedFile");
    public Gaode.Domain.Station01.ComponentExecutionOrigin LightOrigin => new(
        Gaode.Domain.Station01.ComponentEvidenceSource.Test, "FileBackedCapture/LightSettings/1", "ConfiguredOnly");
    public int TriggerCount(CaptureRole role) => counts.GetValueOrDefault(role);

    public FileBackedCapture(string manifestPath)
    {
        var root = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var manifest = document.RootElement;
        if (manifest.GetProperty("purpose").GetString() != "Test")
            throw new InvalidDataException("固定图片清单必须标记Test来源");
        var ms = manifest.GetProperty("captureDelayMs").GetInt32();
        if (ms < 3000 || ms > 5000) throw new InvalidDataException("采集延迟必须为3–5秒");
        delay = TimeSpan.FromMilliseconds(ms);
        images = manifest.GetProperty("images").EnumerateArray().ToDictionary(
            item => (Enum.Parse<CaptureRole>(item.GetProperty("role").GetString()!, false),
                item.TryGetProperty("camera", out var camera) ? camera.GetString() ?? "" : ""),
            item => (Path.GetFullPath(Path.Combine(root, item.GetProperty("relativePath").GetString()!)),
                item.GetProperty("sha256").GetString()!));
        if (!images.ContainsKey((CaptureRole.ThreeD, "")) ||
            !images.ContainsKey((CaptureRole.F, "")))
            throw new InvalidDataException("固定图片清单缺少公共采集角色");
    }

    public ValueTask RequestCaptureAsync(CaptureRequest request, Action<CaptureEvent> onEvent,
        CancellationToken cancellationToken)
    {
        if (!request.Envelope.IsValid || request.CaptureId == Guid.Empty ||
            request.IntentWriteId == Guid.Empty || request.MaxBytes <= 0)
            throw new ArgumentException("采集请求无效", nameof(request));
        if (request.Role == CaptureRole.Detection && request.DetectionSettings is { } settings &&
            (settings.ExposureUs <= 0 || settings.RoiPixels.Length != 4 ||
             settings.LightChannel != request.LightBindingId))
            throw new InvalidDataException("DetectionCaptureSettingsInvalid");
        if (!images.TryGetValue((request.Role, request.CameraBindingId), out var image) &&
            !images.TryGetValue((request.Role, ""), out image))
            throw new InvalidDataException("固定图片清单缺少请求角色");
        counts.AddOrUpdate(request.Role, 1, (_, count) => count + 1);
        var epoch = ConnectionEpoch;
        onEvent(new(request, CaptureEventKind.Accepted, epoch));
        onEvent(new(request, CaptureEventKind.Capturing, epoch));
        Interlocked.Increment(ref activeExecutions);
        _ = CaptureAsync();
        return ValueTask.CompletedTask;

        async Task CaptureAsync()
        {
            try
            {
                var timer = Stopwatch.StartNew();
                await Task.Delay(delay, cancellationToken);
                var bytes = await File.ReadAllBytesAsync(image.Path, cancellationToken);
                if (bytes.LongLength > request.MaxBytes)
                    throw new InvalidDataException("MediaOverLimit");
                var actual = Convert.ToHexString(SHA256.HashData(bytes));
                if (!actual.Equals(image.Digest, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("ImageDigestMismatch");
                if (timer.Elapsed < delay) await Task.Delay(delay - timer.Elapsed, cancellationToken);
                onEvent(new(request, CaptureEventKind.Ended, epoch));
                onEvent(new(request, CaptureEventKind.MediaTaken, epoch, bytes, "png")
                {
                    Fact = new(request.Envelope.RunId, request.CaptureId, request.Envelope.OperationId,
                        epoch, AcquisitionContract.RequestedSettingsDigest(request), MediaSource,
                        CameraOrigin, LightOrigin, CaptureApplicationState.ConfiguredOnly, null,
                        true, ["sha256:" + actual, "file:" + image.Path])
                });
            }
            catch (Exception error) when (error is IOException or InvalidDataException or OperationCanceledException)
            {
                RuntimeDiagnostics.Record("CameraFileCapture", "Failed", request.Envelope.RunId,
                    new { request.Envelope.OperationId, request.CaptureId, role = request.Role.ToString(),
                        epoch, image.Path, expectedDigest = image.Digest, request.MaxBytes,
                        disposition = "FailedCallback_NoMediaCompletion" }, error);
                onEvent(new(request, CaptureEventKind.Failed, epoch,
                    ErrorCode: error is OperationCanceledException ? "CaptureCancelled" : error.Message));
            }
            finally { Interlocked.Decrement(ref activeExecutions); }
        }
    }
}
