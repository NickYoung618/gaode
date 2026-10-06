using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Algorithms;
using Gaode.Integration.Tests.Support;
using Xunit;

namespace Gaode.Integration.Tests.Station01;

public sealed class WorkerTwoInputTests
{
    [Fact]
    public async Task MissingMediaBytesCannotDispatchIndependentWorker()
    {
        var workspace = Station01HostFixture.FindWorkspace();
        var root = Path.Combine(workspace, "artifacts", "recipe-execution-008",
            "worker-contract", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "media"));
        var script = Path.Combine(workspace, "scripts", "virtual-station01-algorithm.py");
        var config = Path.Combine(workspace, "specs", "007-station01-integrated-loop",
            "examples", "virtual-algorithm.json");
        await using var worker = new WorkerProcessSupervisor(
            Station01HostFixture.FindPythonExecutable(), root, $"\"{script}\" \"{config}\"");
        await worker.StartAsync();
        var runId = Guid.NewGuid();
        var captureId = Guid.NewGuid();
        var envelope = new PortEnvelope(runId, Guid.NewGuid(), 1, Guid.NewGuid(),
            "snapshot", "1.2.0", "Test", 1, 30, "wall");
        var media = new MediaRef(Guid.NewGuid(), runId, captureId, "Image",
            "media/missing.bin", 12, "bin", "Test/FixedImage", "scope", "point", "FileCompleted");
        var request = new AlgorithmRequest(envelope, Guid.NewGuid(), captureId,
            AlgorithmRole.Detection, [media], "params", "detection.test", "1.0",
            Guid.NewGuid(), "CapturedInput");
        var adapter = new PythonWorkerAdapter(worker);
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await adapter.RequestAsync(request, _ => Assert.Fail("No worker result allowed"),
                CancellationToken.None));
        Assert.Equal(0, adapter.CallCount(AlgorithmRole.Detection));
        await worker.RequestStopAsync();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task IndependentWorkerReadsEveryInputAndReleasesEveryLease(int count)
    {
        var workspace = Station01HostFixture.FindWorkspace();
        var root = Path.Combine(workspace, "artifacts", "recipe-execution-008",
            "worker-contract", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "media"));
        var runId = Guid.NewGuid();
        var captureIds = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray();
        var media = new List<MediaRef>();
        for (var i = 0; i < count; i++)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes($"Test capture {i}: {runId}");
            var key = $"media/input-{i}.bin";
            await File.WriteAllBytesAsync(Path.Combine(root, key), bytes);
            media.Add(new MediaRef(Guid.NewGuid(), runId, captureIds[i], "Image",
                key, bytes.Length, "bin", "Test/FixedImage", "scope", "point", "FileCompleted"));
        }
        var script = Path.Combine(workspace, "scripts", "virtual-station01-algorithm.py");
        var config = Path.Combine(workspace, "specs", "007-station01-integrated-loop",
            "examples", "virtual-algorithm.json");
        await using var worker = new WorkerProcessSupervisor(
            Station01HostFixture.FindPythonExecutable(), root, $"\"{script}\" \"{config}\"");
        await worker.StartAsync();
        var envelope = new PortEnvelope(runId, Guid.NewGuid(), 1, Guid.NewGuid(),
            "snapshot", "1.2.0", "Test", 1, 30, "wall");
        var request = new AlgorithmRequest(envelope, Guid.NewGuid(), captureIds[^1],
            AlgorithmRole.Detection, media, "params", "detection.test", "1.0",
            Guid.NewGuid(), "CapturedInput", new WorkerTargetIdentity("object-1", 1, 1, "AB"))
        {
            InputIdentities = count == 2 ?
                [new WorkerTargetIdentity("object-1", 1, 1, "A"),
                    new WorkerTargetIdentity("object-1", 1, 1, "B")] :
                [new WorkerTargetIdentity("object-1", 1, 1, "A")]
        };
        var events = new List<AlgorithmEvent>();
        var adapter = new PythonWorkerAdapter(worker);
        var dispatch = await adapter.RequestAsync(request, events.Add, CancellationToken.None);
        await dispatch.Exited.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Contains(events, value => value.Kind == AlgorithmEventKind.Accepted);
        Assert.Contains(events, value => value.Kind == AlgorithmEventKind.Result);
        Assert.Single(events, value => value.Kind == AlgorithmEventKind.InputReleased);
        Assert.Equal(1, adapter.CallCount(AlgorithmRole.Detection));
        await worker.RequestStopAsync();
        await worker.ExitTask.WaitAsync(TimeSpan.FromSeconds(5));
        var audit = (await File.ReadAllLinesAsync(Path.Combine(root, "worker-protocol.jsonl")))
            .Select(line => JsonDocument.Parse(line)).ToArray();
        try
        {
            Assert.Equal(count, audit.Count(entry =>
                entry.RootElement.GetProperty("event").GetString() == "InputReleased"));
            var result = audit.Single(entry =>
                entry.RootElement.GetProperty("event").GetString() == "Result").RootElement;
            using var payload = JsonDocument.Parse(result.GetProperty("resultJson").GetString()!);
            Assert.Equal(count == 2 ? "Fused" : "Detected",
                payload.RootElement.GetProperty("classification").GetString());
            Assert.InRange(payload.RootElement.GetProperty("elapsedMs").GetInt32(), 9900, 30000);
            Assert.Equal(media.Select(item => item.MediaId.ToString()),
                payload.RootElement.GetProperty("inputMediaIds").EnumerateArray()
                    .Select(item => item.GetString()));
            await File.WriteAllTextAsync(Path.Combine(root, "facts.json"),
                JsonSerializer.Serialize(new { runId, request.CallId, count,
                    mediaIds = media.Select(item => item.MediaId),
                    events = events.Select(item => item.Kind.ToString()),
                    workerSessionId = worker.SessionId,
                    source = "Test/Simulated independent Python worker", result = payload.RootElement.Clone() }));
        }
        finally { foreach (var item in audit) item.Dispose(); }
    }
}
