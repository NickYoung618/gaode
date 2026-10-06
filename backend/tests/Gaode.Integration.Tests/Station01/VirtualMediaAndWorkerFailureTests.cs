using System.Net;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Algorithms;
using Gaode.Infrastructure.Media;
using Gaode.Integration.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Gaode.Integration.Tests.Station01;

public sealed class VirtualMediaAndWorkerFailureTests
{
    [Fact]
    public async Task MissingFrozenImageCannotCreateMediaOrAlgorithmSuccess()
    {
        var input = Path.Combine(Station01HostFixture.FindWorkspace(), "artifacts", "station01-007",
            "necessary-failures-20260924", "missing-image-input-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(input);
        var manifest = Path.Combine(input, "images.json");
        await File.WriteAllTextAsync(manifest, JsonSerializer.Serialize(new
        {
            purpose = "Test", captureDelayMs = 3000,
            images = new[]
            {
                new { role = "ThreeD", relativePath = "absent-three-d.png", sha256 = new string('0', 64) },
                new { role = "F", relativePath = "absent-f.png", sha256 = new string('0', 64) }
            }
        }));
        await using var rig = await VirtualLoopTestRig.CreateAsync(imageManifestPath: manifest);
        var (request, response, receipt) = await rig.StartAsync();
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var final = await rig.WaitAsync(receipt.RunId, TimeSpan.FromSeconds(25),
            RunState.Blocked, RunState.RecoveryRequired);
        Assert.Equal(TerminalOutcome.None, final.FinalOutcome);
        Assert.NotNull(final.ErrorCode);
        var algorithm = rig.Host.Host.Services.GetRequiredService<IAlgorithmPort>();
        Assert.Equal(0, algorithm.CallCount(AlgorithmRole.Height));
        Assert.Equal(0, algorithm.CallCount(AlgorithmRole.FDecode));
        var folder = await rig.SaveEvidenceAsync("missing-image", request, response, receipt,
            new { manifest, final.ErrorCode, final.State, final.FinalOutcome,
                heightCalls = algorithm.CallCount(AlgorithmRole.Height),
                fCalls = algorithm.CallCount(AlgorithmRole.FDecode), disposition = "NoMediaNoAlgorithmSuccess" });
        Assert.DoesNotContain("\"Kind\":\"Media\"", await File.ReadAllTextAsync(Path.Combine(folder, "writes.json")));
    }

    [Fact]
    public async Task MediaSaveFailureCannotBecomeCompletedMediaOrDefaultWorkerOk()
    {
        var fault = new FailingMediaStore();
        await using var rig = await VirtualLoopTestRig.CreateAsync(services =>
        {
            services.RemoveAll<IMediaStore>();
            services.AddSingleton<IMediaStore>(sp =>
            {
                fault.Inner = sp.GetRequiredService<MediaStore>();
                return fault;
            });
        });
        var (request, response, receipt) = await rig.StartAsync();
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var final = await rig.WaitAsync(receipt.RunId, TimeSpan.FromSeconds(30),
            RunState.Blocked, RunState.RecoveryRequired);
        Assert.Equal(TerminalOutcome.None, final.FinalOutcome);
        Assert.Equal(1, fault.Failures);
        var algorithm = rig.Host.Host.Services.GetRequiredService<IAlgorithmPort>();
        Assert.Equal(0, algorithm.CallCount(AlgorithmRole.Height));
        var folder = await rig.SaveEvidenceAsync("media-save", request, response, receipt,
            new { injectedError = "InjectedMediaSaveFailure", fault.Failures,
                final.ErrorCode, final.State, final.FinalOutcome,
                heightCalls = algorithm.CallCount(AlgorithmRole.Height) });
        Assert.DoesNotContain("\"Kind\":\"Media\"", await File.ReadAllTextAsync(Path.Combine(folder, "writes.json")));
    }

    [Fact]
    public async Task IndependentWorkerWrongCallIdCannotBecomeAcceptedOrResult()
    {
        var root = Path.Combine(Station01HostFixture.FindWorkspace(), "artifacts", "station01-007",
            "necessary-failures-20260924", "wrong-worker-call-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var script = Path.Combine(Station01HostFixture.FindWorkspace(), "backend", "tests",
            "Gaode.Integration.Tests", "Fixtures", "wrong-call-id-worker.py");
        await using var worker = new WorkerProcessSupervisor(
            Station01HostFixture.FindPythonExecutable(), root, $"\"{script}\"");
        await worker.StartAsync();
        var runId = Guid.NewGuid();
        var captureId = Guid.NewGuid();
        var callId = Guid.NewGuid();
        var envelope = new PortEnvelope(runId, Guid.NewGuid(), 1, Guid.NewGuid(),
            "snapshot", "1.2.0", "Test", 1, 2, "wall");
        Directory.CreateDirectory(Path.Combine(root, "media", "run"));
        await File.WriteAllBytesAsync(Path.Combine(root, "media", "run", "image.png"),
            new byte[12]);
        var media = new MediaRef(Guid.NewGuid(), runId, captureId, "Image",
            "media/run/image.png", 12, "png", "Test/FixedImage", "scope", "point", "FileCompleted");
        var request = new AlgorithmRequest(envelope, callId, captureId,
            AlgorithmRole.FDecode, [media], "params", "decode.test", "1.0", Guid.NewGuid(), "CapturedInput");
        var events = new List<AlgorithmEventKind>();
        var adapter = new PythonWorkerAdapter(worker);
        var dispatch = await adapter.RequestAsync(request, e => events.Add(e.Kind), CancellationToken.None);
        var error = await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await dispatch.Exited.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal("WorkerCorrelationMismatch", error.Message);
        Assert.DoesNotContain(AlgorithmEventKind.Accepted, events);
        Assert.DoesNotContain(AlgorithmEventKind.Result, events);
        Assert.Contains(AlgorithmEventKind.WorkerExited, events);
        Assert.Equal(1, adapter.CallCount(AlgorithmRole.FDecode));
        await File.WriteAllTextAsync(Path.Combine(root, "facts.json"), JsonSerializer.Serialize(new
        { runId, captureId, callId, worker.SessionId, events, rawError = error.ToString(),
            disposition = "WorkerExited_NoResult_NoAutomaticReplay", deadlineMs = 5000 }));
        await File.WriteAllTextAsync(Path.Combine(root, "request.json"), JsonSerializer.Serialize(request));
    }

    [Fact]
    public async Task WorkerRejectsMediaFromAnotherCaptureBeforeDispatch()
    {
        var root = Path.Combine(Station01HostFixture.FindWorkspace(), "artifacts", "station01-007",
            "necessary-failures-20260924", "wrong-worker-media-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var runId = Guid.NewGuid();
        var captureId = Guid.NewGuid();
        var envelope = new PortEnvelope(runId, Guid.NewGuid(), 1, Guid.NewGuid(),
            "snapshot", "1.2.0", "Test", 1, 2, "wall");
        var staleMedia = new MediaRef(Guid.NewGuid(), runId, Guid.NewGuid(), "Image",
            "media/old/image.png", 12, "png", "Test/FixedImage", "scope", "point", "FileCompleted");
        var request = new AlgorithmRequest(envelope, Guid.NewGuid(), captureId,
            AlgorithmRole.FDecode, [staleMedia], "params", "decode.test", "1.0", Guid.NewGuid(), "CapturedInput");
        var adapter = new PythonWorkerAdapter();
        var error = await Assert.ThrowsAsync<AlgorithmNotDispatchedException>(async () =>
            await adapter.RequestAsync(request, _ => Assert.Fail("No worker callback allowed"), CancellationToken.None));
        Assert.Equal(0, adapter.CallCount(AlgorithmRole.FDecode));
        await File.WriteAllTextAsync(Path.Combine(root, "request.json"), JsonSerializer.Serialize(request));
        await File.WriteAllTextAsync(Path.Combine(root, "facts.json"), JsonSerializer.Serialize(new
        { runId, captureId, staleCaptureId = staleMedia.CaptureId, rawError = error.ToString(),
            disposition = "RejectedBeforeWorkerDispatch_NoAlgorithmFact" }));
    }

    private sealed class FailingMediaStore : IMediaStore
    {
        public IMediaStore Inner { get; set; } = null!;
        public int Failures { get; private set; }
        public IDisposable ReserveCapture(Guid captureId, string role, long maxBytes) =>
            Inner.ReserveCapture(captureId, role, maxBytes);
        public ValueTask<MediaRef> SaveAsync(Guid runId, Guid captureId, string role,
            string pointVersion, string scopeVersion, byte[] buffer, string format,
            string source, CancellationToken cancellationToken)
        {
            Failures++;
            throw new IOException("InjectedMediaSaveFailure");
        }
        public ValueTask<Stream> OpenReadAsync(Guid mediaId, CancellationToken cancellationToken) =>
            Inner.OpenReadAsync(mediaId, cancellationToken);
        public IDisposable Lease(Guid mediaId, string consumer) => Inner.Lease(mediaId, consumer);
        public bool IsReady(Guid mediaId) => Inner.IsReady(mediaId);
    }
}
