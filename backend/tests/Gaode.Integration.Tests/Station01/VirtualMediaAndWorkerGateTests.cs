using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Infrastructure.Algorithms;
using Gaode.Infrastructure.Simulation;
using Xunit;

namespace Gaode.Integration.Tests.Station01;

public sealed class VirtualMediaAndWorkerGateTests
{
    [Fact]
    public async Task MissingFixedImageFailsAfterFormalCaptureWithoutMedia()
    {
        var manifestPath = Path.GetTempFileName();
        try
        {
            var manifest = new
            {
                purpose = "Test",
                captureDelayMs = 3000,
                images = new[]
                {
                    new { role = "ThreeD", relativePath = "missing-three-d.png", sha256 = new string('0', 64) },
                    new { role = "F", relativePath = "missing-f.png", sha256 = new string('0', 64) }
                }
            };
            await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest));
            var camera = new FileBackedCapture(manifestPath);
            var envelope = new PortEnvelope(Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(),
                "snapshot", "1.0", "Test", 1, 2, "wall");
            var request = new CaptureRequest(envelope, Guid.NewGuid(), CaptureRole.ThreeD,
                "point", "1.0", null, null, "camera", "light", Guid.NewGuid(), 1024);
            var seen = new List<CaptureEventKind>();
            var terminal = new TaskCompletionSource<CaptureEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
            await camera.RequestCaptureAsync(request, value =>
            {
                lock (seen) seen.Add(value.Kind);
                if (value.Kind is CaptureEventKind.Failed or CaptureEventKind.MediaTaken)
                    terminal.TrySetResult(value);
            }, CancellationToken.None);
            var result = await terminal.Task.WaitAsync(TimeSpan.FromSeconds(6));
            Assert.Equal(CaptureEventKind.Failed, result.Kind);
            Assert.DoesNotContain(CaptureEventKind.MediaTaken, seen);
            Assert.Equal(1, camera.TriggerCount(CaptureRole.ThreeD));
        }
        finally { File.Delete(manifestPath); }
    }

    [Fact]
    public async Task MissingWorkerDoesNotFallBackToInProcessSuccess()
    {
        var runId = Guid.NewGuid();
        var captureId = Guid.NewGuid();
        var envelope = new PortEnvelope(runId, Guid.NewGuid(), 1, Guid.NewGuid(),
            "snapshot", "1.0", "Test", 1, 2, "wall");
        var media = new MediaRef(Guid.NewGuid(), runId, captureId, "Image",
            "media/run/image.png", 12, "png", "Test/FixedImage", "scope", "point", "FileCompleted");
        var request = new AlgorithmRequest(envelope, Guid.NewGuid(), captureId,
            AlgorithmRole.FDecode, [media], "params", "decode.test", "1.0",
            Guid.NewGuid(), "CapturedInput");
        var adapter = new PythonWorkerAdapter();
        await Assert.ThrowsAsync<AlgorithmNotDispatchedException>(async () =>
            await adapter.RequestAsync(request, _ => { }, CancellationToken.None));
        Assert.Equal(0, adapter.CallCount(AlgorithmRole.FDecode));
    }
}
