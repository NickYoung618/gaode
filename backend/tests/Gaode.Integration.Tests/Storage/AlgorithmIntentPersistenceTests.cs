using System.Text.Json;
using Gaode.Application.Algorithms;
using Gaode.Application.Configuration;
using Gaode.Application.Ports;
using Gaode.Application.Station01;
using Gaode.Application.Timing;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Configuration;
using Gaode.Infrastructure.Persistence;
using Gaode.Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Gaode.Integration.Tests.Storage;

public sealed class AlgorithmIntentPersistenceTests
{
    [Fact]
    public async Task RealSqliteCommitUnknownNeverDispatchesAndLateCommitKeepsOriginalWriteIdentity()
    {
        var harness = await CreateAsync(holdIntent: true);
        await using var writer = harness.Writer;
        var invocation = harness.Runtime.InvokeAsync(harness.Run, AlgorithmRole.TrayPose,
            harness.Media.CaptureId, [harness.Media], "whole/1", CancellationToken.None);
        using var wait = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var batch = await harness.Gate.WaitUntilArrivedAsync(WriteKind.AlgorithmIntent, wait.Token);

        harness.Clock.Advance(TimeSpan.FromMilliseconds(harness.Config.Budget.BusinessMs.CriticalSave));
        await DrainAsync();
        var error = await Assert.ThrowsAsync<SaveGateException>(() => invocation);
        Assert.Equal("CommitUnknown", error.Code);
        Assert.Equal(batch.WriteId, error.WriteId);
        Assert.Equal(0, harness.Algorithm.Calls);

        harness.Gate.Release(WriteKind.AlgorithmIntent);
        var reconciled = await WaitForReceiptAsync(writer, error.WriteId);
        Assert.Equal(CommitState.Committed, reconciled.State);
        Assert.Equal(batch.WriteId, reconciled.WriteId);
        Assert.Equal(0, harness.Algorithm.Calls);
    }

    [Fact]
    public async Task IntentCommitAfterOriginalAlgorithmDeadlineCannotResetBudgetOrDispatch()
    {
        var harness = await CreateAsync(holdIntent: true, algorithmEndsBeforeSave: true);
        await using var writer = harness.Writer;
        var invocation = harness.Runtime.InvokeAsync(harness.Run, AlgorithmRole.TrayPose,
            harness.Media.CaptureId, [harness.Media], "whole/1", CancellationToken.None);
        using var wait = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await harness.Gate.WaitUntilArrivedAsync(WriteKind.AlgorithmIntent, wait.Token);
        var originalBudget = harness.Config.Budget.BusinessMs.TrayPoseAlgorithm!.Value;

        harness.Clock.Advance(TimeSpan.FromMilliseconds(originalBudget));
        await DrainAsync();
        harness.Gate.Release(WriteKind.AlgorithmIntent);
        var outcome = await invocation;

        Assert.Equal(AlgorithmState.TimedOut, outcome.State);
        Assert.Equal("PreDispatchTimeout", outcome.Decision);
        Assert.Equal(originalBudget,
            harness.Clock.GetElapsedTime(outcome.StartTick, outcome.DueTick).TotalMilliseconds);
        Assert.Equal(0, harness.Algorithm.Calls);
        await using var db = new Station01DbContext(harness.Options);
        var row = await db.AlgorithmCalls.AsNoTracking().SingleAsync(x => x.CallId == outcome.CallId);
        Assert.Equal(outcome.IntentWriteId, row.IntentWriteId);
        Assert.Equal(outcome.StartTick, row.StartTick);
        Assert.Equal(outcome.DueTick, row.DueTick);
    }

    [Fact]
    public async Task RealSqliteRevisionConditionRejectionNeverDispatches()
    {
        var harness = await CreateAsync(holdIntent: false, runRevision: 2);
        await using var writer = harness.Writer;
        // Durable run is revision 1 while the in-memory caller claims revision 2.
        var invocation = harness.Runtime.InvokeAsync(harness.Run, AlgorithmRole.TrayPose,
            harness.Media.CaptureId, [harness.Media], "whole/1", CancellationToken.None);
        var error = await Assert.ThrowsAsync<SaveGateException>(() => invocation);
        Assert.Equal(CommitState.ConditionRejected.ToString(), error.Code);
        Assert.Equal(0, harness.Algorithm.Calls);
        Assert.Null(await writer.ReconcileAsync(error.WriteId, CancellationToken.None));
    }

    private static async Task<Harness> CreateAsync(bool holdIntent, long runRevision = 1, bool algorithmEndsBeforeSave = false)
    {
        var workspace = Station01HostFixture.FindWorkspace();
        var approved = Gaode.Testing.ApprovedTestRoot.Resolve(workspace);
        Directory.CreateDirectory(approved);
        var root = await StorePreparation.PrepareEmptyTestStoreAsync(approved,
            Path.Combine(approved, "algorithm-intent-" + Guid.NewGuid().ToString("N")));
        var options = new DbContextOptionsBuilder<Station01DbContext>()
            .UseSqlite(StoreCompatibilityProbe.ReadWriteConnectionString(root)).Options;
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.Zero));
        var gate = new CommitInterleavingGate();
        if (holdIntent) gate.Hold(WriteKind.AlgorithmIntent);
        var writer = new TraceWriter(options, clock, 8, gate.BeforeCommitAsync);
        var runId = Guid.NewGuid();
        var created = new RunCreatedPayload(Guid.NewGuid(), "algorithm-intent", "test:Operator",
            "{}", "{}", "{}", "{}", "snapshot", "public", "budget", "simulation");
        var initial = new WriteBatch(Guid.NewGuid(), runId, 0, WriteKind.RunCreated,
            JsonSerializer.Serialize(created, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            "algorithm-intent-run-created");
        Assert.Equal(CommitState.Committed, (await writer.SubmitCritical(initial).Completion).State);

        var feature = Path.Combine(workspace, "specs", "001-station01-public-preparation");
        var loader = new ConfigurationLoader(Path.Combine(workspace, "specs/011-plc-interaction-update/examples/joint/config"),
            Path.Combine(feature, "contracts"));
        var loadedBudget = loader.LoadBudget(new("s01-budget-011-joint", "2"));
        if (algorithmEndsBeforeSave)
        {
            var value = loadedBudget.Value with { Source = "Component:algorithm-before-save",
                BusinessMs = loadedBudget.Value.BusinessMs with { TrayPoseAlgorithm = loadedBudget.Value.BusinessMs.CriticalSave / 2 } };
            var json = JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            loadedBudget = new(value, json, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(json))), "component:algorithm-before-save");
        }
        var config = ConfigurationFreezer.Freeze(
            loader.LoadPublic(new("s01-public-011-joint", "1")),
            loadedBudget,
            loader.LoadSimulation(new("s01-sim-011-joint", "2")),
            new Dictionary<string, string>
            { ["tray.observation"] = "1.0", ["code.raw-candidates"] = "1.0" });
        var mediaStore = new ReadyMedia(runId);
        var algorithm = new CountingAlgorithm();
        var runtime = new AlgorithmRuntime(algorithm, mediaStore,
            new OperationIngress(new DeadlineScheduler(clock, "sqlite-algorithm-gate")),
            new AlgorithmLeaseSupervisor(mediaStore), 1, 1);
        var run = new RunExecution(runId, Guid.NewGuid(), "algorithm-intent", "test:Operator",
            "{}", config, writer, clock, Guid.NewGuid(), "sqlite-algorithm-gate");
        run.AdoptInitialRevision(runRevision);
        return new(writer, gate, options, clock, config, run, runtime, algorithm, mediaStore.Reference);
    }

    private static async Task<CommitReceipt> WaitForReceiptAsync(TraceWriter writer, Guid writeId)
    {
        var until = DateTimeOffset.UtcNow.AddSeconds(5);
        while (DateTimeOffset.UtcNow < until)
        {
            if (await writer.ReconcileAsync(writeId, CancellationToken.None) is { } receipt)
                return receipt;
            await Task.Delay(10);
        }
        throw new TimeoutException("迟到提交未能在证据窗口内核对");
    }

    private static async Task DrainAsync()
    {
        for (var i = 0; i < 12; i++) await Task.Yield();
    }

    private sealed record Harness(TraceWriter Writer, CommitInterleavingGate Gate,
        DbContextOptions<Station01DbContext> Options, FakeTimeProvider Clock,
        FrozenConfiguration Config, RunExecution Run, AlgorithmRuntime Runtime,
        CountingAlgorithm Algorithm, MediaRef Media);

    private sealed class ReadyMedia : IMediaStore
    {
        public ReadyMedia(Guid runId) => Reference = new(Guid.NewGuid(), runId, Guid.NewGuid(),
            "PointCloud", "media/sqlite.bin", 1, "bin", "Test", "whole/1", "point/1", "FileCompleted");
        public MediaRef Reference { get; }
        public bool IsReady(Guid mediaId) => mediaId == Reference.MediaId;
        public IDisposable Lease(Guid mediaId, string consumer) => new EmptyLease();
        public IDisposable ReserveCapture(Guid captureId, string role, long maxBytes) => new EmptyLease();
        public ValueTask<MediaRef> SaveAsync(Guid runId, Guid captureId, string role,
            string pointVersion, string scopeVersion, byte[] buffer, string format,
            string source, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<Stream> OpenReadAsync(Guid mediaId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        private sealed class EmptyLease : IDisposable { public void Dispose() { } }
    }

    private sealed class CountingAlgorithm : IAlgorithmPort
    {
        private int calls;
        public int Calls => Volatile.Read(ref calls);
        public int CallCount(AlgorithmRole role) => Calls;
        public ValueTask<AlgorithmDispatch> RequestAsync(AlgorithmRequest request,
            Action<AlgorithmEvent> onEvent, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            return ValueTask.FromResult(new AlgorithmDispatch(Task.CompletedTask));
        }
    }
}
