using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gaode.Application.Algorithms;
using Gaode.Application.Ports;
using Gaode.Application.Station01;
using Gaode.Domain.Station01;
using Gaode.Integration.Tests.Support;
using Gaode.Infrastructure.Media;
using Gaode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Gaode.Integration.Tests.Station01;

public sealed class AlgorithmAdapterFailureTests
{
    [Theory]
    [InlineData(AlgorithmRole.Height)]
    [InlineData(AlgorithmRole.FDecode)]
    public async Task AdapterExceptionIsPersistedAndIndependentStepsReachLimitedHandoff(
        AlgorithmRole failingRole)
    {
        var algorithm = new FaultingAlgorithm(failingRole);
        await using var fixture = await Station01HostFixture.CreateAsync(services =>
        {
            services.RemoveAll<ICapturePort>();
            services.AddSingleton<ICapturePort, ImmediateCapture>();
            services.RemoveAll<IAlgorithmPort>();
            services.AddSingleton<IAlgorithmPort>(algorithm);
        }, recipeFixtureCode: "RC:R-S1-A-CAP:0.4.0-review");
        algorithm.Store = fixture.Host.Services.GetRequiredService<IMediaStore>();
        try
        {
        var request = new StartPublicRequest(Guid.NewGuid().ToString("N"),
            StartRunContextJson.Create(),
            new("s01-public-dev", "1.0.0"), new("s01-budget-dev", "3.0.0"),
            new("s01-sim-normal", "3.0.0"));
        var response = await fixture.Client.PostAsJsonAsync("/api/v1/station01/runs", request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var receipt = (await response.Content.ReadFromJsonAsync<StartReceipt>())!;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(25);
        RunApiSnapshot? snapshot = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            snapshot = await fixture.Client.GetFromJsonAsync<RunApiSnapshot>(
                "/api/v1/station01/runs/" + receipt.RunId);
            if (snapshot?.State == RunState.WaitingClamp ||
                snapshot?.State is RunState.HandoffReady or RunState.Blocked) break;
            await Task.Delay(25);
        }
        Assert.NotNull(snapshot);
        Assert.Contains(RunState.WaitingClamp.ToString(), snapshot!.Events);

        while (DateTimeOffset.UtcNow < deadline)
        {
            snapshot = await fixture.Client.GetFromJsonAsync<RunApiSnapshot>(
                "/api/v1/station01/runs/" + receipt.RunId);
            if (snapshot?.State == RunState.HandoffReady ||
                (snapshot?.State == RunState.Blocked && snapshot.ErrorCode is not null)) break;
            await Task.Delay(25);
        }

        var expectedState = failingRole == AlgorithmRole.FDecode
            ? RunState.Blocked : RunState.HandoffReady;
        Assert.True(snapshot?.State == expectedState,
            $"state={snapshot?.State}; error={snapshot?.ErrorCode}; events={string.Join(',', snapshot?.Events ?? [])}");
        Assert.Equal(1, algorithm.CallCount(AlgorithmRole.Height));
        Assert.Equal(1, algorithm.CallCount(AlgorithmRole.FDecode));
        var handoff = await fixture.Client.GetAsync(
            "/api/v1/station01/runs/" + receipt.RunId + "/handoff");
        if (failingRole == AlgorithmRole.FDecode)
        {
            Assert.Equal(HttpStatusCode.Conflict, handoff.StatusCode);
            Assert.Equal("Unmatched", snapshot!.RecipeState);
            Assert.Contains("FCodeNotUniqueAndParsed", snapshot.ErrorCode, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, handoff.StatusCode);
            var savedHandoff = await handoff.Content
                .ReadFromJsonAsync<PublicPreparationHandoffV2>();
            Assert.NotNull(savedHandoff);
            Assert.Equal("Degraded", savedHandoff.Quality);
            Assert.True(savedHandoff.IsComplete);
        }
        var options = new DbContextOptionsBuilder<Station01DbContext>()
            .UseSqlite(StoreCompatibilityProbe.ReadOnlyConnectionString(fixture.StoreRoot)).Options;
        await using var db = new Station01DbContext(options);
        var facts = await db.Writes.Where(x => x.RunId == receipt.RunId &&
            x.Kind == "AlgorithmFact").ToListAsync();
        Assert.Contains(facts, x => x.PayloadJson.Contains(
            "DispatchException:InvalidOperationException", StringComparison.Ordinal));
        var call = await db.AlgorithmCalls.SingleAsync(x => x.RunId == receipt.RunId && x.TechnicalState == "Error");
        Assert.Equal("Unknown", call.DispatchEvidence);
        Assert.True(algorithm.InputRead);
        var mediaIds = JsonSerializer.Deserialize<Guid[]>(call.InputMediaIdsJson)!;
        var leases = fixture.Host.Services.GetRequiredService<MediaLeaseRegistry>();
        Assert.All(mediaIds, id => Assert.Equal(1, leases.Count(id)));
        await File.WriteAllTextAsync(Path.Combine(fixture.StoreRoot, "unacknowledged-input-evidence.json"),
            JsonSerializer.Serialize(new { call.CallId, call.IntentWriteId, call.DispatchEvidence,
                algorithm.InputRead, mediaIds, heightCalls = algorithm.CallCount(AlgorithmRole.Height),
                decodeCalls = algorithm.CallCount(AlgorithmRole.FDecode) }));
        }
        finally
        {
            algorithm.Release();
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await fixture.Host.Services.GetRequiredService<AlgorithmRuntime>().WaitForIdleAsync(cleanup.Token);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task AcceptedThenThrowOrHangKeepsInputLeaseUntilWorkerReleasesAndPersistsUnknownDispatch(bool hang, bool synchronous)
    {
        var algorithm = new AcceptedThenFaultingAlgorithm(hang, synchronous);
        await using var fixture = await Station01HostFixture.CreateAsync(services =>
        {
            services.RemoveAll<ICapturePort>();
            services.AddSingleton<ICapturePort, ImmediateCapture>();
            services.RemoveAll<IAlgorithmPort>();
            services.AddSingleton<IAlgorithmPort>(algorithm);
        }, recipeFixtureCode: "RC:R-S1-A-CAP:0.4.0-review");
        try
        {
        var request = new StartPublicRequest(Guid.NewGuid().ToString("N"),
            StartRunContextJson.Create(),
            new("s01-public-dev", "1.0.0"), new("s01-budget-dev", "3.0.0"),
            new("s01-sim-normal", "3.0.0"));
        var response = await fixture.Client.PostAsJsonAsync("/api/v1/station01/runs", request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var receipt = (await response.Content.ReadFromJsonAsync<StartReceipt>())!;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(25);
        RunApiSnapshot? snapshot = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            snapshot = await fixture.Client.GetFromJsonAsync<RunApiSnapshot>(
                "/api/v1/station01/runs/" + receipt.RunId);
            if (snapshot?.State == RunState.WaitingClamp ||
                snapshot?.State is RunState.HandoffReady or RunState.Blocked) break;
            await Task.Delay(25);
        }
        Assert.NotNull(snapshot);
        Assert.Contains(RunState.WaitingClamp.ToString(), snapshot!.Events);
        while (DateTimeOffset.UtcNow < deadline)
        {
            snapshot = await fixture.Client.GetFromJsonAsync<RunApiSnapshot>(
                "/api/v1/station01/runs/" + receipt.RunId);
            if (snapshot?.State is RunState.HandoffReady or RunState.Blocked) break;
            await Task.Delay(25);
        }
        Assert.Equal(RunState.HandoffReady, snapshot?.State);
        Assert.Equal(TerminalOutcome.None, snapshot?.FinalOutcome);

        var options = new DbContextOptionsBuilder<Station01DbContext>()
            .UseSqlite(StoreCompatibilityProbe.ReadOnlyConnectionString(fixture.StoreRoot)).Options;
        await using var db = new Station01DbContext(options);
        var expected = hang ? AlgorithmState.TimedOut : AlgorithmState.Error;
        var failedCall = await db.AlgorithmCalls.AsNoTracking().SingleAsync(x =>
            x.RunId == receipt.RunId && x.TechnicalState == expected.ToString());
        Assert.Equal("Unknown", failedCall.DispatchEvidence);
        var mediaIds = JsonSerializer.Deserialize<Guid[]>(failedCall.InputMediaIdsJson)!;
        Assert.NotEmpty(mediaIds);
        var leases = fixture.Host.Services.GetRequiredService<MediaLeaseRegistry>();
        Assert.All(mediaIds, id => Assert.Equal(1, leases.Count(id)));

        var capture = fixture.Host.Services.GetRequiredService<ICapturePort>();
        Assert.Equal(1, algorithm.CallCount(AlgorithmRole.Height));
        Assert.Equal(1, algorithm.CallCount(AlgorithmRole.FDecode));
        Station01Evidence.AssertM1Boundary(capture.TriggerCount(CaptureRole.F),
            capture.TriggerCount(CaptureRole.ThreeD), fixture.Plc.MoveCommands,
            snapshot!.RecipeState, snapshot.QualityState,
            (await db.Writes.Where(x => x.RunId == receipt.RunId).ToArrayAsync()).Select(x => x.Kind));
        await File.WriteAllTextAsync(Path.Combine(fixture.StoreRoot, "dispatch-unknown-evidence.json"),
            JsonSerializer.Serialize(new { hang, synchronous, failedCall.CallId, failedCall.IntentWriteId,
                failedCall.DispatchEvidence, failedCall.TechnicalState, failedCall.StartTick, failedCall.DueTick,
                failedCall.BudgetMs, retainedInputMediaIds = mediaIds,
                heightCalls = algorithm.CallCount(AlgorithmRole.Height),
                decodeCalls = algorithm.CallCount(AlgorithmRole.FDecode),
                fTriggers = capture.TriggerCount(CaptureRole.F), fixture.Plc.MoveCommands }));

        algorithm.ReleaseInputs();
        Assert.All(mediaIds, id => Assert.Equal(0, leases.Count(id)));
        }
        finally
        {
            algorithm.ReleaseInputs();
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await fixture.Host.Services.GetRequiredService<AlgorithmRuntime>().WaitForIdleAsync(cleanup.Token);
        }
    }

    private sealed class FaultingAlgorithm(AlgorithmRole failingRole) : IAlgorithmPort
    {
        public IMediaStore Store { get; set; } = null!;
        public bool InputRead { get; private set; }
        private AlgorithmRequest? used;
        private Action<AlgorithmEvent>? publish;
        public void Release()
        {
            if (used is { } request) publish!(new(request, AlgorithmEventKind.WorkerExited, WorkerSessionId: _session));
        }
        private int _height;
        private int _decode;
        private readonly Guid _session = Guid.NewGuid();

        public int CallCount(AlgorithmRole role) => role == AlgorithmRole.Height
            ? Volatile.Read(ref _height) : Volatile.Read(ref _decode);

        public ValueTask<AlgorithmDispatch> RequestAsync(AlgorithmRequest request,
            Action<AlgorithmEvent> onEvent, CancellationToken cancellationToken)
        {
            if (request.Role == AlgorithmRole.Height) Interlocked.Increment(ref _height);
            else Interlocked.Increment(ref _decode);
            if (request.Role == failingRole)
            {
                used = request; publish = onEvent;
                using var stream = Store.OpenReadAsync(request.Inputs[0].MediaId, CancellationToken.None).AsTask().GetAwaiter().GetResult();
                InputRead = stream.ReadByte() >= 0;
                throw new InvalidOperationException("Injected adapter failure");
            }

            onEvent(new(request, AlgorithmEventKind.Accepted, WorkerSessionId: _session));
            onEvent(new(request, AlgorithmEventKind.Running, WorkerSessionId: _session));
            var heights = request.Role == AlgorithmRole.Height
                ? new[] { HeightSample.FromRaw("height-source", 12.5, "mm", "SIM_REFERENCE") }
                : null;
            var codes = request.Role == AlgorithmRole.FDecode ? new[] { "RC:R-S1-A-CAP:0.4.0-review" } : null;
            onEvent(new(request, AlgorithmEventKind.Result, heights, codes,
                WorkerSessionId: _session));
            return ValueTask.FromResult(new AlgorithmDispatch(Task.CompletedTask));
        }
    }

    private sealed class AcceptedThenFaultingAlgorithm(bool hang, bool synchronous) : IAlgorithmPort
    {
        private readonly ManualResetEventSlim dispatchRelease = new(false);
        private readonly TaskCompletionSource<AlgorithmDispatch> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Action<AlgorithmEvent>? release;
        private AlgorithmRequest? acceptedRequest;
        private readonly Guid _session = Guid.NewGuid();
        private int _height;
        private int _decode;

        public int CallCount(AlgorithmRole role) => role == AlgorithmRole.Height
            ? Volatile.Read(ref _height) : Volatile.Read(ref _decode);

        public ValueTask<AlgorithmDispatch> RequestAsync(AlgorithmRequest request,
            Action<AlgorithmEvent> onEvent, CancellationToken cancellationToken)
        {
            if (request.Role == AlgorithmRole.Height)
            {
                Interlocked.Increment(ref _height);
                acceptedRequest = request;
                release = onEvent;
                onEvent(new(request, AlgorithmEventKind.Accepted, WorkerSessionId: _session));
                if (synchronous && !dispatchRelease.Wait(TimeSpan.FromSeconds(35)))
                    throw new TimeoutException("Controlled dispatch watchdog");
                if (hang) return new(pending.Task);
                throw new InvalidOperationException("Injected failure after worker acceptance");
            }

            Interlocked.Increment(ref _decode);
            onEvent(new(request, AlgorithmEventKind.Accepted, WorkerSessionId: _session));
            onEvent(new(request, AlgorithmEventKind.Running, WorkerSessionId: _session));
            onEvent(new(request, AlgorithmEventKind.Result, RawCodes: ["RC:R-S1-A-CAP:0.4.0-review"],
                WorkerSessionId: _session));
            onEvent(new(request, AlgorithmEventKind.InputReleased, WorkerSessionId: _session));
            return ValueTask.FromResult(new AlgorithmDispatch(Task.CompletedTask));
        }

        public void ReleaseInputs()
        {
            dispatchRelease.Set();
            if (acceptedRequest is not { } request) return;
            release!(new(request, AlgorithmEventKind.InputReleased, WorkerSessionId: _session));
            release!(new(request, AlgorithmEventKind.WorkerExited, WorkerSessionId: _session));
            pending.TrySetResult(new(Task.CompletedTask));
        }
    }

    private sealed class ImmediateCapture : ICapturePort
    {
        private int threeD;
        private int f;

        public long ConnectionEpoch => 1;

        public int TriggerCount(CaptureRole role) => role == CaptureRole.ThreeD
            ? Volatile.Read(ref threeD) : Volatile.Read(ref f);

        public ValueTask RequestCaptureAsync(CaptureRequest request,
            Action<CaptureEvent> onEvent, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = request.Role == CaptureRole.ThreeD
                ? Interlocked.Increment(ref threeD)
                : Interlocked.Increment(ref f);
            if (request.Role == CaptureRole.F && count > 1)
                throw new InvalidOperationException("F已触发，不允许重拍");

            var epoch = ConnectionEpoch;
            onEvent(new(request, CaptureEventKind.Accepted, epoch));
            onEvent(new(request, CaptureEventKind.Capturing, epoch));
            onEvent(new(request, CaptureEventKind.Ended, epoch));
            onEvent(new(request, CaptureEventKind.MediaTaken, epoch,
                [1, 2, 3], request.Role == CaptureRole.ThreeD ? "bin" : "img"));
            return ValueTask.CompletedTask;
        }
    }
}
