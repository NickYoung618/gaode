using Gaode.Application.Algorithms;
using Gaode.Application.Configuration;
using Gaode.Application.Ports;
using Gaode.Application.Station01;
using Gaode.Application.Timing;
using Gaode.Contracts.Tests.Support;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Gaode.Contracts.Tests.Algorithms;

public sealed partial class AlgorithmRuntimeTests
{
    [Fact]
    public async Task ControlSignalDoesNotWaitForHangingDispatchOrPretendWorkerExited()
    {
        var port = new HangingDispatch(true);
        var harness = Create(new ImmediateWriter(CommitState.Committed), port);
        using var stop = new CancellationTokenSource();
        var invocation = harness.Runtime.InvokeAsync(harness.Run, AlgorithmRole.TrayPose,
            harness.Media.CaptureId, [harness.Media], "whole/1", stop.Token);
        await port.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invocation.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, harness.Store.LeaseCount);
        Assert.Equal(1, port.CallCount(AlgorithmRole.TrayPose));
        port.ReturnLate();
        Assert.Equal(1, harness.Store.LeaseCount);
        port.ReleaseInputs();
        Assert.Equal(0, harness.Store.LeaseCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HangingDispatchTimesOutWithoutReleasingInputAndDecodeHasIndependentSlot(bool accepted)
    {
        var port = new HangingDispatch(accepted);
        var harness = Create(new ImmediateWriter(CommitState.Committed), port);
        var invocation = harness.Runtime.InvokeAsync(harness.Run, AlgorithmRole.TrayPose,
            harness.Media.CaptureId, [harness.Media], "whole/1", CancellationToken.None);
        await port.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var budget = harness.Config.Budget.BusinessMs.TrayPoseAlgorithm!.Value;
        harness.Clock.Advance(TimeSpan.FromMilliseconds(budget - 1));
        Assert.False(invocation.IsCompleted);
        harness.Clock.Advance(TimeSpan.FromMilliseconds(1));
        var result = await invocation.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(AlgorithmState.TimedOut, result.State);
        Assert.Equal("Unknown", result.DispatchEvidence);
        Assert.Equal(budget, harness.Clock.GetElapsedTime(result.StartTick, result.DueTick).TotalMilliseconds);
        Assert.Equal(1, harness.Store.LeaseCount);
        Assert.Equal(accepted, result.AcceptedTick is not null);
        var decode = await harness.Runtime.InvokeAsync(harness.Run, AlgorithmRole.FDecode,
            harness.Media.CaptureId, [harness.Media], "whole/1", CancellationToken.None)
            .WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(AlgorithmState.Success, decode.State);
        Assert.Equal(1, port.CallCount(AlgorithmRole.TrayPose));
        Assert.Equal(1, port.CallCount(AlgorithmRole.FDecode));
        Assert.Equal(1, harness.Store.LeaseCount);
        port.ReturnLate();
        Assert.Equal(1, harness.Store.LeaseCount);
        port.ReleaseInputs();
        Assert.Equal(0, harness.Store.LeaseCount);
        Assert.Equal(AlgorithmState.TimedOut, result.State);
    }

    [Theory]
    [InlineData(-1, AlgorithmState.Success)]
    [InlineData(0, AlgorithmState.TimedOut)]
    [InlineData(1, AlgorithmState.TimedOut)]
    public async Task DispatchAndResultUseOriginalDeadlineBoundary(int offset, AlgorithmState expected)
    {
        var port = new HangingDispatch(true);
        var harness = Create(new ImmediateWriter(CommitState.Committed), port);
        var invocation = harness.Runtime.InvokeAsync(harness.Run, AlgorithmRole.TrayPose,
            harness.Media.CaptureId, [harness.Media], "whole/1", CancellationToken.None);
        await port.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        harness.Clock.Advance(TimeSpan.FromMilliseconds(harness.Config.Budget.BusinessMs.TrayPoseAlgorithm!.Value + offset));
        port.ReturnLate();
        port.Result();
        var result = await invocation.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(expected, result.State);
        Assert.Equal(1, port.CallCount(AlgorithmRole.TrayPose));
        port.ReleaseInputs();
        Assert.Equal(0, harness.Store.LeaseCount);
    }

    private sealed class HangingDispatch(bool accepted) : IAlgorithmPort
    {
        private readonly TaskCompletionSource<AlgorithmDispatch> pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private AlgorithmRequest? request;
        private Action<AlgorithmEvent>? callback;
        private int height, decode;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int CallCount(AlgorithmRole role) => role == AlgorithmRole.TrayPose ? height : decode;
        public ValueTask<AlgorithmDispatch> RequestAsync(AlgorithmRequest value, Action<AlgorithmEvent> onEvent, CancellationToken cancellationToken)
        {
            if (value.Role == AlgorithmRole.FDecode)
            {
                decode++;
                onEvent(new(value, AlgorithmEventKind.Result, RawCodes: ["TEST-TRAY-0001"]));
                onEvent(new(value, AlgorithmEventKind.InputReleased));
                return ValueTask.FromResult(new AlgorithmDispatch(Task.CompletedTask));
            }
            height++;
            request = value; callback = onEvent;
            if (accepted) onEvent(new(value, AlgorithmEventKind.Accepted));
            Started.TrySetResult();
            return new(pending.Task); // Intentionally ignores cancellation, including late-return case.
        }
        public void ReturnLate() => pending.TrySetResult(new(exited.Task));
        public void Result() => callback!(new(request!, AlgorithmEventKind.Result));
        public void ReleaseInputs() { callback!(new(request!, AlgorithmEventKind.InputReleased)); exited.TrySetResult(); }
    }

    [Theory]
    [InlineData(CommitState.Failed)]
    [InlineData(CommitState.ConditionRejected)]
    public async Task IntentFailureOrConditionRejectionNeverDispatches(CommitState state)
    {
        var harness = Create(new ImmediateWriter(state));
        await Assert.ThrowsAsync<SaveGateException>(() => harness.Runtime.InvokeAsync(
            harness.Run, AlgorithmRole.TrayPose, harness.Media.CaptureId, [harness.Media], "whole/1",
            CancellationToken.None));
        Assert.Equal(0, harness.Algorithm.CallCount(AlgorithmRole.TrayPose));
    }

    [Fact]
    public async Task CommitUnknownUsesRealSaveDeadlineAndNeverDispatches()
    {
        var writer = new PendingWriter();
        var harness = Create(writer);
        var invoke = harness.Runtime.InvokeAsync(harness.Run, AlgorithmRole.TrayPose,
            harness.Media.CaptureId, [harness.Media], "whole/1", CancellationToken.None);
        await Task.Yield();
        harness.Clock.Advance(TimeSpan.FromMilliseconds(harness.Config.Budget.BusinessMs.CriticalSave));
        await DrainAsync();
        var error = await Assert.ThrowsAsync<SaveGateException>(() => invoke);
        Assert.Equal("CommitUnknown", error.Code);
        Assert.Equal(0, harness.Algorithm.CallCount(AlgorithmRole.TrayPose));
    }

    [Fact]
    public async Task SaveCompletingAfterOriginalAlgorithmDeadlineDoesNotResetBudgetOrDispatch()
    {
        var writer = new PendingWriter();
        var harness = Create(writer, algorithmEndsBeforeSave: true);
        var invoke = harness.Runtime.InvokeAsync(harness.Run, AlgorithmRole.TrayPose,
            harness.Media.CaptureId, [harness.Media], "whole/1", CancellationToken.None);
        await Task.Yield();
        var algorithmBudget = harness.Config.Budget.BusinessMs.TrayPoseAlgorithm!.Value;
        harness.Clock.Advance(TimeSpan.FromMilliseconds(algorithmBudget));
        await DrainAsync();
        writer.Complete(CommitState.Committed, 2);
        var outcome = await invoke;
        Assert.Equal(AlgorithmState.TimedOut, outcome.State);
        Assert.Equal("PreDispatchTimeout", outcome.Decision);
        Assert.Equal(0, harness.Algorithm.CallCount(AlgorithmRole.TrayPose));
        Assert.Equal(algorithmBudget,
            harness.Clock.GetElapsedTime(outcome.StartTick, outcome.DueTick).TotalMilliseconds);
    }

    [Fact]
    public async Task CommittedIntentDoesNotMeanWorkerAccepted()
    {
        var algorithm = new NoResponseAlgorithm();
        var harness = Create(new ImmediateWriter(CommitState.Committed), algorithm);
        var invoke = harness.Runtime.InvokeAsync(harness.Run, AlgorithmRole.TrayPose,
            harness.Media.CaptureId, [harness.Media], "whole/1", CancellationToken.None);
        await WaitUntilAsync(() => harness.Runtime.ExecutionEvidence.Any(x => x.DispatchReturned));
        harness.Clock.Advance(TimeSpan.FromMilliseconds(
            harness.Config.Budget.BusinessMs.TrayPoseAlgorithm!.Value));
        await DrainAsync();
        var outcome = await invoke;
        Assert.Equal(AlgorithmState.TimedOut, outcome.State);
        Assert.Null(outcome.AcceptedTick);
        Assert.Equal("Requested", outcome.DispatchEvidence);
        Assert.Equal(1, algorithm.CallCount(AlgorithmRole.TrayPose));
    }

    [Fact]
    public async Task ControlCancellationEndsBusinessWaitButDoesNotClaimWorkerExit()
    {
        var algorithm = new HoldingAlgorithm();
        var harness = Create(new ImmediateWriter(CommitState.Committed), algorithm);
        using var cancellation = new CancellationTokenSource();
        var invocation = harness.Runtime.InvokeAsync(harness.Run, AlgorithmRole.TrayPose,
            harness.Media.CaptureId, [harness.Media], "whole/1", cancellation.Token);
        await algorithm.Accepted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invocation);
        Assert.False(algorithm.Exited.Task.IsCompleted);
        Assert.Equal(1, algorithm.CallCount(AlgorithmRole.TrayPose));
        algorithm.Release();
    }

    private static Harness Create(ITraceWriter writer, IAlgorithmPort? algorithm = null, bool algorithmEndsBeforeSave = false)
    {
        var workspace = TestConfiguration.Workspace();
        var loader = new Gaode.Infrastructure.Configuration.ConfigurationLoader(
            Path.Combine(workspace, "specs/011-plc-interaction-update/examples/joint/config"),
            Path.Combine(workspace, "specs/001-station01-public-preparation/contracts"));
        var p = loader.LoadPublic(new("s01-public-011-joint", "1"));
        var b = loader.LoadBudget(new("s01-budget-011-joint", "2"));
        if (algorithmEndsBeforeSave)
        {
            // This component explicitly selects the algorithm-before-save ordering;
            // joint-run and production budgets are unchanged.
            var value = b.Value with { Source = "Component:algorithm-before-save",
                BusinessMs = b.Value.BusinessMs with { TrayPoseAlgorithm = b.Value.BusinessMs.CriticalSave / 2 } };
            var json = System.Text.Json.JsonSerializer.Serialize(value, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
            b = new(value, json, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(json))), "component:algorithm-before-save");
        }
        var s = loader.LoadSimulation(new("s01-sim-011-joint", "2"));
        var frozen = ConfigurationFreezer.Freeze(p, b, s, new Dictionary<string, string>
        {
            ["tray.observation"] = "1.0", ["code.raw-candidates"] = "1.0"
        });
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.Zero))
        { AutoAdvanceAmount = TimeSpan.Zero };
        var ingress = new OperationIngress(new DeadlineScheduler(clock, "algorithm-contract"));
        var media = new ReadyMedia();
        var port = algorithm ?? new NoResponseAlgorithm();
        var runtime = new AlgorithmRuntime(port, media, ingress,
            new AlgorithmLeaseSupervisor(media), 1, 1);
        var run = new RunExecution(Guid.NewGuid(), Guid.NewGuid(), "request", "subject", "{}",
            frozen, writer, clock, Guid.NewGuid(), "algorithm-contract");
        run.AdoptInitialRevision(1);
        var reference = media.Add(run.RunId);
        return new(runtime, run, reference, port, clock, frozen, media);
    }

    private static async Task DrainAsync()
    {
        for (var i = 0; i < 12; i++) await Task.Yield();
    }

    private sealed record Harness(AlgorithmRuntime Runtime, RunExecution Run, MediaRef Media,
        IAlgorithmPort Algorithm, FakeTimeProvider Clock, FrozenConfiguration Config, ReadyMedia Store);

    private sealed class ImmediateWriter(CommitState state) : ITraceWriter
    {
        public QueuedWrite SubmitCritical(WriteBatch batch, CancellationToken cancellationToken = default,
            Gaode.Domain.Station01.ActionWindow? window = null)
        {
            var receipt = new CommitReceipt(batch.WriteId, batch.RunId, state,
                state == CommitState.Committed ? batch.ExpectedRevision + 1 : null,
                TerminalOutcome.None, state == CommitState.Committed ? null : "Injected" + state);
            return new(new(batch.WriteId, batch.RunId, CommitState.Queued, null,
                TerminalOutcome.None, null), Task.FromResult(receipt));
        }
        public Task<CommitReceipt?> ReconcileAsync(Guid writeId, CancellationToken cancellationToken) =>
            Task.FromResult<CommitReceipt?>(null);
    }

    private sealed class PendingWriter : ITraceWriter
    {
        private readonly TaskCompletionSource<CommitReceipt> completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private WriteBatch? batch;
        public QueuedWrite SubmitCritical(WriteBatch value, CancellationToken cancellationToken = default,
            Gaode.Domain.Station01.ActionWindow? window = null)
        {
            batch = value;
            return new(new(value.WriteId, value.RunId, CommitState.Queued, null,
                TerminalOutcome.None, null), completion.Task);
        }
        public void Complete(CommitState state, long? revision) => completion.TrySetResult(new(
            batch!.WriteId, batch.RunId, state, revision, TerminalOutcome.None, null));
        public Task<CommitReceipt?> ReconcileAsync(Guid writeId, CancellationToken cancellationToken) =>
            Task.FromResult<CommitReceipt?>(null);
    }

    private sealed class ReadyMedia : IMediaStore
    {
        public int LeaseCount;
        private readonly HashSet<Guid> ready = [];
        public MediaRef Add(Guid runId)
        {
            var value = new MediaRef(Guid.NewGuid(), runId, Guid.NewGuid(), "PointCloud", "memory",
                1, "bin", "Test", "whole/1", "point/1", "FileCompleted");
            ready.Add(value.MediaId);
            return value;
        }
        public bool IsReady(Guid mediaId) => ready.Contains(mediaId);
        public IDisposable Lease(Guid mediaId, string consumer)
        {
            Interlocked.Increment(ref LeaseCount);
            return new CountedLease(this);
        }
        private sealed class CountedLease(ReadyMedia owner) : IDisposable
        {
            private int disposed;
            public void Dispose() { if (Interlocked.Exchange(ref disposed, 1) == 0) Interlocked.Decrement(ref owner.LeaseCount); }
        }
        public IDisposable ReserveCapture(Guid captureId, string role, long maxBytes) => new EmptyLease();
        public ValueTask<MediaRef> SaveAsync(Guid runId, Guid captureId, string role,
            string pointVersion, string scopeVersion, byte[] buffer, string format,
            string source, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask<Stream> OpenReadAsync(Guid mediaId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        private sealed class EmptyLease : IDisposable { public void Dispose() { } }
    }

    private sealed class NoResponseAlgorithm : IAlgorithmPort
    {
        private int calls;
        public int CallCount(AlgorithmRole role) => Volatile.Read(ref calls);
        public ValueTask<AlgorithmDispatch> RequestAsync(AlgorithmRequest request,
            Action<AlgorithmEvent> onEvent, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            return ValueTask.FromResult(new AlgorithmDispatch(Task.CompletedTask));
        }
    }

    private sealed class HoldingAlgorithm : IAlgorithmPort
    {
        private int calls;
        public TaskCompletionSource Accepted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Exited { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int CallCount(AlgorithmRole role) => Volatile.Read(ref calls);
        public ValueTask<AlgorithmDispatch> RequestAsync(AlgorithmRequest request,
            Action<AlgorithmEvent> onEvent, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            onEvent(new(request, AlgorithmEventKind.Accepted, WorkerSessionId: Guid.NewGuid()));
            Accepted.TrySetResult();
            return ValueTask.FromResult(new AlgorithmDispatch(Exited.Task));
        }
        public void Release() => Exited.TrySetResult();
    }
}
