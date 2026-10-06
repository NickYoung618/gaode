using System.Net.Http.Json;
using Gaode.Application.Ports;
using Gaode.Application.Station01;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Host.Lifecycle;
using Gaode.Integration.Tests.Support;
using Gaode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Gaode.Integration.Tests.Hosting;

public sealed class HostLifecycleTests
{
    [Fact]
    public async Task AlreadyExpiredShutdownBudgetStillClosesAdmissionAndSignalsConsumer()
    {
        await using var fixture = await Station01HostFixture.CreateAsync();
        var lifecycle = fixture.Host.Services.GetRequiredService<Station01HostedService>();
        var coordinator = fixture.Host.Services.GetRequiredService<Station01Coordinator>();
        using var expired = new CancellationTokenSource();
        expired.Cancel();
        await lifecycle.StopAsync(expired.Token).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(coordinator.AdmissionClosed);
        Assert.True(coordinator.ConsumerStopRequested);
        Assert.NotNull(lifecycle.LastShutdown);
        Assert.Equal(4, lifecycle.LastShutdown.Steps!.Count);
        Assert.False(lifecycle.LastShutdown.PhysicalStopConfirmed);
        Assert.False(lifecycle.LastShutdown.PhysicalOccupancyReleased);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ShutdownBudgetExpiryDuringCriticalWriteStillSignalsConsumerAndKeepsWriteIdentity(bool committedWithoutReceipt)
    {
        var gate = new CommitInterleavingGate();
        if (committedWithoutReceipt) gate.HoldReceipt(WriteKind.StartIntent);
        else gate.Hold(WriteKind.StartIntent);
        await using var fixture = await Station01HostFixture.CreateAsync(services =>
        {
            services.RemoveAll<TraceWriter>();
            services.AddSingleton(sp => new TraceWriter(
                sp.GetRequiredService<DbContextOptions<Station01DbContext>>(),
                sp.GetRequiredService<TimeProvider>(), 32, gate.BeforeCommitAsync,
                gate.AfterCommitBeforeReceiptAsync));
        });
        var request = new StartPublicRequest(Guid.NewGuid().ToString("N"), StartRunContextJson.Create(),
            new("s01-public-dev", "1.0.0"), new("s01-budget-dev", "3.0.0"), new("s01-sim-normal", "3.0.0"));
        var response = await fixture.Client.PostAsJsonAsync("/api/v1/station01/runs", request);
        var receipt = (await response.Content.ReadFromJsonAsync<StartReceipt>())!;
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        Guid writeId;
        if (committedWithoutReceipt)
            writeId = (await gate.WaitUntilCommittedAsync(WriteKind.StartIntent, watchdog.Token)).WriteId;
        else writeId = (await gate.WaitUntilArrivedAsync(WriteKind.StartIntent, watchdog.Token)).WriteId;
        var lifecycle = fixture.Host.Services.GetRequiredService<Station01HostedService>();
        var coordinator = fixture.Host.Services.GetRequiredService<Station01Coordinator>();
        var writer = fixture.Host.Services.GetRequiredService<TraceWriter>();
        try
        {
            using var budget = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
            await lifecycle.StopAsync(budget.Token).WaitAsync(watchdog.Token);
            var stopped = Assert.IsType<Station01ShutdownSnapshot>(lifecycle.LastShutdown);
            Assert.True(stopped.AdmissionClosed);
            Assert.False(stopped.WritesDrained);
            Assert.True(stopped.RemainingWrites > 0);
            Assert.Equal(new[] { "Consumer", "Resources", "Runs", "Writes" }, stopped.Steps!.Keys.Order().ToArray());
            Assert.True(coordinator.ConsumerStopRequested);
            try { await coordinator.ConsumerCompletion.WaitAsync(watchdog.Token); }
            catch (OperationCanceledException) when (coordinator.ConsumerStopped) { }
            Assert.True(coordinator.ConsumerStopped);
            Assert.False(stopped.PhysicalStopConfirmed);
            Assert.False(stopped.PhysicalOccupancyReleased);
            Assert.Equal(0, fixture.Plc.StartCommands);
            Assert.Equal(0, fixture.Plc.MoveCommands);
            var known = await writer.ReconcileAsync(writeId, watchdog.Token);
            Assert.Equal(committedWithoutReceipt, known?.State == CommitState.Committed);
            await File.WriteAllTextAsync(Path.Combine(fixture.StoreRoot, "shutdown-evidence.json"),
                System.Text.Json.JsonSerializer.Serialize(new { writeId, committedWithoutReceipt,
                    stopped, consumerStopped = coordinator.ConsumerStopped, fixture.Plc.StartCommands,
                    fixture.Plc.MoveCommands }), watchdog.Token);
        }
        finally
        {
            if (committedWithoutReceipt) gate.ReleaseReceipt(WriteKind.StartIntent);
            else gate.Release(WriteKind.StartIntent);
        }
        await writer.WaitForIdleAsync(watchdog.Token);
        Assert.Equal(CommitState.Committed, (await writer.ReconcileAsync(writeId, watchdog.Token))!.State);
        await using var db = new Station01DbContext(fixture.Host.Services.GetRequiredService<DbContextOptions<Station01DbContext>>());
        Assert.Empty(await db.Handoffs.Where(x => x.RunId == receipt.RunId).ToArrayAsync(watchdog.Token));
        Assert.Equal(TerminalOutcome.None, (await db.Runs.SingleAsync(x => x.RunId == receipt.RunId, watchdog.Token)).Terminal);
    }

    [Fact]
    public async Task HostUsesWorkflowTestRootAndReservedIntegrationsRemainNotIntegrated()
    {
        await using var fixture = await Station01HostFixture.CreateAsync();
        var configuredRoot = Environment.GetEnvironmentVariable("GAODE_TEST_ROOT");
        var approvedRoot = string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(Station01HostFixture.FindWorkspace(), "artifacts", "station01", "test-stores")
            : Path.GetFullPath(configuredRoot);
        Assert.Equal(Path.GetFullPath(approvedRoot), fixture.ApprovedRoot, ignoreCase: true);
        Assert.StartsWith(Path.GetFullPath(approvedRoot) + Path.DirectorySeparatorChar, fixture.StoreRoot,
            StringComparison.OrdinalIgnoreCase);
        var integrations = fixture.Host.Services.GetServices<IReservedIntegration>().ToArray();
        Assert.Equal(3, integrations.Length);
        foreach (var integration in integrations)
            Assert.Equal("NotIntegrated", await integration.StatusAsync(CancellationToken.None));
        var status = await fixture.Client.GetFromJsonAsync<Dictionary<string, object>>("/api/v1/station01/status");
        Assert.Equal("FullSimulation", status!["mode"].ToString());
        Assert.Equal("NotEvaluated", status["quality"].ToString());
    }

    [Fact]
    public async Task ExistingUnfinishedRunStartsRecoveryRequiredWithoutAutomaticDeviceReplay()
    {
        var runId = Guid.NewGuid();
        await using var fixture = await Station01HostFixture.CreateAsync(afterStorePrepared: async root =>
        {
            var options = new DbContextOptionsBuilder<Station01DbContext>()
                .UseSqlite(StoreCompatibilityProbe.ReadWriteConnectionString(root)).Options;
            await using var db = new Station01DbContext(options);
            db.Runs.Add(new RunEntity
            {
                RunId = runId, RequestId = "unfinished", SubjectId = "test:Operator",
                ContextJson = "{}", State = RunState.Running3D, Terminal = TerminalOutcome.None,
                Revision = 1, CreatedUtc = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        });
        var snapshot = fixture.Host.Services.GetRequiredService<Station01Coordinator>().Query(runId);
        Assert.Equal(RunState.RecoveryRequired, snapshot?.State);
        Assert.Contains("RecoveryRequired:NoAutomaticReplay", snapshot?.Events ?? []);
        Assert.Equal(0, fixture.Plc.StartCommands);
        Assert.Equal(0, fixture.Plc.MoveCommands);
        var request = new StartPublicRequest(Guid.NewGuid().ToString("N"), StartRunContextJson.Create(),
            new("s01-public-dev", "1.0.0"), new("s01-budget-dev", "3.0.0"),
            new("s01-sim-normal", "3.0.0"));
        Assert.Equal(System.Net.HttpStatusCode.Conflict,
            (await fixture.Client.PostAsJsonAsync("/api/v1/station01/runs", request)).StatusCode);
    }

    [Fact]
    public async Task StopClosesAdmissionPersistsRecoveryAndDoesNotClaimPhysicalStopOrRelease()
    {
        await using var fixture = await Station01HostFixture.CreateAsync();
        var request = new StartPublicRequest(Guid.NewGuid().ToString("N"),
            StartRunContextJson.Create(),
            new("s01-public-dev", "1.0.0"), new("s01-budget-dev", "3.0.0"),
            new("s01-sim-normal", "3.0.0"));
        var started = await fixture.Client.PostAsJsonAsync("/api/v1/station01/runs", request);
        var receipt = (await started.Content.ReadFromJsonAsync<StartReceipt>())!;
        var until = DateTimeOffset.UtcNow.AddSeconds(8);
        RunApiSnapshot? snapshot = null;
        while (DateTimeOffset.UtcNow < until)
        {
            snapshot = await fixture.Client.GetFromJsonAsync<RunApiSnapshot>(receipt.StatusUrl);
            if (snapshot?.State == RunState.WaitingClamp) break;
            await Task.Delay(20);
        }
        Assert.Equal(RunState.WaitingClamp, snapshot?.State);
        Assert.Equal(1, fixture.Plc.StartCommands);
        Assert.Equal(0, fixture.Plc.MoveCommands);
        var capturesBeforeStop = fixture.Host.Services.GetRequiredService<ICapturePort>();
        Assert.Equal(0, capturesBeforeStop.TriggerCount(CaptureRole.ThreeD));
        Assert.Equal(0, capturesBeforeStop.TriggerCount(CaptureRole.F));

        var lifecycle = fixture.Host.Services.GetRequiredService<Station01HostedService>();
        using var stopBudget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await lifecycle.StopAsync(stopBudget.Token);
        var stopped = Assert.IsType<Station01ShutdownSnapshot>(lifecycle.LastShutdown);
        Assert.True(stopped.AdmissionClosed);
        Assert.True(stopped.FlowsStopped);
        Assert.True(stopped.WritesDrained);
        Assert.True(stopped.ResourcesDrained);
        Assert.False(stopped.PhysicalStopConfirmed);
        Assert.False(stopped.PhysicalOccupancyReleased);
        Assert.True(stopped.MotionUnknown);
        var final = fixture.Host.Services.GetRequiredService<Station01Coordinator>().Query(receipt.RunId);
        Assert.Equal(RunState.RecoveryRequired, final?.State);
        Assert.Contains("PhysicalStopUnconfirmed", final?.ErrorCode);
        var rejected = await fixture.Client.PostAsJsonAsync("/api/v1/station01/runs",
            request with { RequestId = Guid.NewGuid().ToString("N") });
        Assert.Equal(System.Net.HttpStatusCode.ServiceUnavailable, rejected.StatusCode);

        var options = new DbContextOptionsBuilder<Station01DbContext>()
            .UseSqlite(StoreCompatibilityProbe.ReadOnlyConnectionString(fixture.StoreRoot)).Options;
        await using var db = new Station01DbContext(options);
        var persisted = await db.Runs.SingleAsync(x => x.RunId == receipt.RunId);
        Assert.Equal(RunState.RecoveryRequired, persisted.State);
        Assert.Contains(await db.Writes.Where(x => x.RunId == receipt.RunId).ToListAsync(),
            x => x.Kind == WriteKind.Audit.ToString() &&
                x.PayloadJson.Contains("ShutdownRecoveryRequired", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BoundedStopRetainsAcceptedAlgorithmMediaLeaseUntilWorkerExit()
    {
        var algorithm = new AcceptedHoldingAlgorithm();
        await using var fixture = await Station01HostFixture.CreateAsync(services =>
        {
            services.RemoveAll<IAlgorithmPort>();
            services.AddSingleton<IAlgorithmPort>(algorithm);
        });
        var request = new StartPublicRequest(Guid.NewGuid().ToString("N"),
            StartRunContextJson.Create(),
            new("s01-public-dev", "1.0.0"), new("s01-budget-dev", "3.0.0"),
            new("s01-sim-normal", "3.0.0"));
        var started = await fixture.Client.PostAsJsonAsync("/api/v1/station01/runs", request);
        var receipt = (await started.Content.ReadFromJsonAsync<StartReceipt>())!;
        var until = DateTimeOffset.UtcNow.AddSeconds(8);
        while (DateTimeOffset.UtcNow < until)
        {
            var state = await fixture.Client.GetFromJsonAsync<RunApiSnapshot>(receipt.StatusUrl);
            if (state?.State == RunState.WaitingClamp) break;
            await Task.Delay(20);
        }
        await algorithm.Accepted.Task.WaitAsync(TimeSpan.FromSeconds(8));
        var leases = fixture.Host.Services.GetRequiredService<Gaode.Infrastructure.Media.MediaLeaseRegistry>();
        Assert.True(leases.TotalCount > 0);

        var lifecycle = fixture.Host.Services.GetRequiredService<Station01HostedService>();
        using var stopBudget = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        await lifecycle.StopAsync(stopBudget.Token);
        var stopped = Assert.IsType<Station01ShutdownSnapshot>(lifecycle.LastShutdown);
        Assert.True(stopped.AdmissionClosed);
        Assert.Equal(stopped.RunsStopped && stopped.ConsumerStopped, stopped.FlowsStopped);
        Assert.False(stopped.ResourcesDrained);
        Assert.True(stopped.RunsStopped);
        var coordinator = fixture.Host.Services.GetRequiredService<Station01Coordinator>();
        Assert.True(coordinator.ConsumerStopRequested);
        try { await coordinator.ConsumerCompletion.WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (OperationCanceledException) when (coordinator.ConsumerStopped) { }
        Assert.True(coordinator.ConsumerStopped);
        Assert.True(stopped.RemainingMediaLeases > 0);
        Assert.False(stopped.PhysicalStopConfirmed);
        Assert.False(stopped.PhysicalOccupancyReleased);

        algorithm.ReleaseWorker();
        until = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < until && leases.TotalCount != 0) await Task.Delay(20);
        Assert.Equal(0, leases.TotalCount);
        while (DateTimeOffset.UtcNow < until)
        {
            var state = fixture.Host.Services.GetRequiredService<Station01Coordinator>().Query(receipt.RunId);
            if (state?.State == RunState.RecoveryRequired) break;
            await Task.Delay(20);
        }
        Assert.Equal(RunState.RecoveryRequired,
            fixture.Host.Services.GetRequiredService<Station01Coordinator>().Query(receipt.RunId)?.State);
    }

    private sealed class AcceptedHoldingAlgorithm : IAlgorithmPort
    {
        private readonly TaskCompletionSource exited =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int heightCalls;
        private AlgorithmRequest? acceptedRequest;
        private Action<AlgorithmEvent>? publish;
        private Guid workerSession;
        public TaskCompletionSource Accepted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int CallCount(AlgorithmRole role) => role == AlgorithmRole.Height
            ? Volatile.Read(ref heightCalls) : 0;
        public ValueTask<AlgorithmDispatch> RequestAsync(AlgorithmRequest request,
            Action<AlgorithmEvent> onEvent, CancellationToken cancellationToken)
        {
            if (request.Role != AlgorithmRole.Height)
                throw new InvalidOperationException("F步骤不应在关闭期间启动");
            Interlocked.Increment(ref heightCalls);
            acceptedRequest = request;
            publish = onEvent;
            workerSession = Guid.NewGuid();
            onEvent(new(request, AlgorithmEventKind.Accepted, WorkerSessionId: workerSession));
            onEvent(new(request, AlgorithmEventKind.Running, WorkerSessionId: workerSession));
            Accepted.TrySetResult();
            return ValueTask.FromResult(new AlgorithmDispatch(exited.Task));
        }
        public void ReleaseWorker()
        {
            if (acceptedRequest is { } request)
                publish!(new(request, AlgorithmEventKind.InputReleased, WorkerSessionId: workerSession));
            exited.TrySetResult();
        }
    }
}
