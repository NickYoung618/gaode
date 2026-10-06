using System.Diagnostics;
using Gaode.Application.Ports;
using Gaode.Domain.Station01;
using Xunit;

namespace Gaode.Contracts.Tests.Algorithms;

public sealed partial class AlgorithmRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SynchronousDispatchHangKeepsBoundedCapacityAndFIndependentUntilReliableEnd(bool lateThrow)
    {
        using var port = new BoundaryPort(lateThrow ? "SyncThrow" : "SyncHang");
        var h = Create(new ImmediateWriter(CommitState.Committed), port);
        Task<Gaode.Application.Algorithms.AlgorithmOutcome>? queued = null;
        var first = Invoke(h);
        try
        {
            await port.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            h.Clock.Advance(TimeSpan.FromMilliseconds(h.Config.Budget.BusinessMs.TrayPoseAlgorithm!.Value - 1));
            Assert.False(first.IsCompleted);
            h.Clock.Advance(TimeSpan.FromMilliseconds(1));
            var terminal = await first.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(AlgorithmState.TimedOut, terminal.State);
            Assert.Equal("Unknown", terminal.DispatchEvidence);
            Assert.Equal(1, h.Runtime.ActiveExecutions);
            Assert.Equal(1, h.Store.LeaseCount);
            queued = Invoke(h);
            var overCapacity = await Invoke(h).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("ExecutionQueueFull", overCapacity.Decision);
            Assert.Equal(1, port.CallCount(AlgorithmRole.TrayPose));
            var f = await Invoke(h, AlgorithmRole.FDecode).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(AlgorithmState.Success, f.State);
            h.Clock.Advance(TimeSpan.FromMilliseconds(h.Config.Budget.BusinessMs.TrayPoseAlgorithm.Value));
            Assert.Equal("QueueTimeout", (await queued.WaitAsync(TimeSpan.FromSeconds(5))).Decision);

            port.ReleaseDispatch.Set();
            await WaitUntilAsync(() => h.Runtime.ExecutionEvidence.Any(x => x.CallId == terminal.CallId && x.DispatchReturned));
            port.Publish(AlgorithmEventKind.Accepted);
            port.Publish(AlgorithmEventKind.Result);
            Assert.Equal(AlgorithmState.TimedOut, terminal.State);
            Assert.Equal(1, h.Store.LeaseCount);
            port.Publish(AlgorithmEventKind.InputReleased);
            port.Publish(AlgorithmEventKind.InputReleased);
            Assert.Equal(0, h.Store.LeaseCount);
            Assert.Equal(1, h.Runtime.ActiveExecutions); // InputReleased != execution ended.
            port.EndExecution();
            await Idle(h);
            Assert.Equal(1, port.CallCount(AlgorithmRole.TrayPose));
            Assert.Equal(1, port.CallCount(AlgorithmRole.FDecode));
        }
        finally
        {
            port.ReleaseAll();
            h.Clock.Advance(TimeSpan.FromMilliseconds(h.Config.Budget.BusinessMs.TrayPoseAlgorithm!.Value * 3));
            await first.WaitAsync(TimeSpan.FromSeconds(5));
            if (queued is not null) await queued.WaitAsync(TimeSpan.FromSeconds(5));
            await Idle(h);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationCallbacksCannotExtendBusinessDeadlineOrReleaseSlotPrematurely(bool throws)
    {
        using var port = new BoundaryPort(throws ? "CancelThrow" : "CancelHang");
        var h = Create(new ImmediateWriter(CommitState.Committed), port);
        var invoke = Invoke(h);
        try
        {
            await port.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await WaitUntilAsync(() => h.Runtime.ExecutionEvidence.Any(x => x.DispatchReturned));
            h.Clock.Advance(TimeSpan.FromMilliseconds(h.Config.Budget.BusinessMs.TrayPoseAlgorithm!.Value));
            var outcome = await invoke.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(AlgorithmState.TimedOut, outcome.State);
            Assert.Equal(h.Config.Budget.BusinessMs.TrayPoseAlgorithm.Value,
                h.Clock.GetElapsedTime(outcome.StartTick, outcome.DueTick).TotalMilliseconds);
            await port.CancelEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, h.Store.LeaseCount);
            Assert.Equal(AlgorithmState.Success, (await Invoke(h, AlgorithmRole.FDecode).WaitAsync(TimeSpan.FromSeconds(5))).State);
            port.EndExecution();
            if (!throws)
            {
                Assert.Equal(1, h.Runtime.ActiveExecutions); // Cancellation callback is still using its slot.
                Assert.Equal(0, h.Store.LeaseCount);
            }
            port.ReleaseCancel.Set();
            await Idle(h);
            var proof = Assert.Single(h.Runtime.ExecutionEvidence, x => x.CallId == outcome.CallId);
            Assert.Equal(throws ? "CallbacksFailed" : "CallbacksCompleted", proof.Cancellation);
            Assert.True(proof.Reclaimed);
            if (throws) Assert.Contains("Cancellation:", proof.Error);
        }
        finally
        {
            port.ReleaseAll();
            h.Clock.Advance(TimeSpan.FromMilliseconds(h.Config.Budget.BusinessMs.TrayPoseAlgorithm!.Value * 3));
            await invoke.WaitAsync(TimeSpan.FromSeconds(5));
            await Idle(h);
        }
    }

    [Fact]
    public async Task CallerCancellationDoesNotInvokeAdapterCallbacksOnCallerThread()
    {
        using var port = new BoundaryPort("CancelHang");
        var h = Create(new ImmediateWriter(CommitState.Committed), port);
        using var stop = new CancellationTokenSource();
        var invoke = h.Runtime.InvokeAsync(h.Run, AlgorithmRole.TrayPose, h.Media.CaptureId, [h.Media], "whole/1", stop.Token);
        try
        {
            await port.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await stop.CancelAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invoke.WaitAsync(TimeSpan.FromSeconds(5)));
            await port.CancelEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(1, h.Runtime.ActiveExecutions);
            Assert.Equal(1, h.Store.LeaseCount);
        }
        finally
        {
            await stop.CancelAsync();
            port.ReleaseAll();
            try { await invoke.WaitAsync(TimeSpan.FromSeconds(5)); } catch (OperationCanceledException) { }
            await Idle(h);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnlyExplicitMatchingRejectionCanProveNoInputAcquired(bool explicitRejection)
    {
        using var port = new BoundaryPort(explicitRejection ? "Reject" : "ThrowWithoutAccepted");
        var h = Create(new ImmediateWriter(CommitState.Committed), port);
        try
        {
            var outcome = await Invoke(h).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(AlgorithmState.Error, outcome.State);
            Assert.Null(outcome.AcceptedTick);
            Assert.Equal(explicitRejection ? "NotDispatched" : "Unknown", outcome.DispatchEvidence);
            Assert.Equal(!explicitRejection, port.InputUsed);
            Assert.Equal(explicitRejection ? 0 : 1, h.Store.LeaseCount);
            if (!explicitRejection)
            {
                Assert.Equal(1, h.Runtime.ActiveExecutions);
                port.Publish(AlgorithmEventKind.InputReleased);
                port.Publish(AlgorithmEventKind.InputReleased);
                Assert.Equal(0, h.Store.LeaseCount);
                Assert.Equal(1, h.Runtime.ActiveExecutions);
            }
        }
        finally { port.ReleaseAll(); await Idle(h); }
    }

    private static Task<Gaode.Application.Algorithms.AlgorithmOutcome> Invoke(Harness h, AlgorithmRole role = AlgorithmRole.TrayPose) =>
        h.Runtime.InvokeAsync(h.Run, role, h.Media.CaptureId, [h.Media], "whole/1", CancellationToken.None);

    [Theory]
    [InlineData("WrongCallReject")]
    [InlineData("AcceptedReject")]
    public async Task ContradictoryOrUncorrelatedRefusalCannotReleaseInputs(string mode)
    {
        using var port = new BoundaryPort(mode);
        var h = Create(new ImmediateWriter(CommitState.Committed), port);
        try
        {
            var outcome = await Invoke(h).WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal("Unknown", outcome.DispatchEvidence);
            Assert.Equal(1, h.Store.LeaseCount);
            Assert.Equal(1, h.Runtime.ActiveExecutions);
        }
        finally { port.ReleaseAll(); await Idle(h); }
    }

    [Fact]
    public async Task FaultedExitIsObservedButDoesNotProveWorkerEnded()
    {
        using var port = new BoundaryPort("ExitFault");
        var h = Create(new ImmediateWriter(CommitState.Committed), port);
        var invoke = Invoke(h);
        try
        {
            await port.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await WaitUntilAsync(() => h.Runtime.ExecutionEvidence.Any(x => x.DispatchReturned));
            port.FaultExit();
            await WaitUntilAsync(() => h.Runtime.ExecutionEvidence.Any(x => x.Error?.StartsWith("ExitUnconfirmed:", StringComparison.Ordinal) == true));
            h.Clock.Advance(TimeSpan.FromMilliseconds(h.Config.Budget.BusinessMs.TrayPoseAlgorithm!.Value));
            Assert.Equal(AlgorithmState.TimedOut, (await invoke.WaitAsync(TimeSpan.FromSeconds(5))).State);
            Assert.Equal(1, h.Store.LeaseCount);
            Assert.False(Assert.Single(h.Runtime.ExecutionEvidence).ExecutionEnded);
        }
        finally
        {
            port.ReleaseAll();
            h.Clock.Advance(TimeSpan.FromMilliseconds(h.Config.Budget.BusinessMs.TrayPoseAlgorithm!.Value * 3));
            await invoke.WaitAsync(TimeSpan.FromSeconds(5));
            await Idle(h);
        }
    }

    private static async Task Idle(Harness h)
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await h.Runtime.WaitForIdleAsync(watchdog.Token);
        Assert.Equal(0, h.Runtime.ActiveExecutions);
        Assert.Equal(0, h.Store.LeaseCount);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(5)) throw new TimeoutException("测试等待明确状态超时");
            await Task.Yield();
        }
    }

    private sealed class BoundaryPort(string mode) : IAlgorithmPort, IDisposable
    {
        public ManualResetEventSlim ReleaseDispatch { get; } = new(false);
        public ManualResetEventSlim ReleaseCancel { get; } = new(false);
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CancelEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private AlgorithmRequest? request;
        private Action<AlgorithmEvent>? publish;
        private int pose, decode;
        public bool InputUsed { get; private set; }
        public int CallCount(AlgorithmRole role) => role == AlgorithmRole.TrayPose ? Volatile.Read(ref pose) : Volatile.Read(ref decode);
        public ValueTask<AlgorithmDispatch> RequestAsync(AlgorithmRequest value, Action<AlgorithmEvent> onEvent, CancellationToken token)
        {
            if (value.Role == AlgorithmRole.FDecode)
            {
                Interlocked.Increment(ref decode);
                onEvent(new(value, AlgorithmEventKind.Result, RawCodes: ["TEST-TRAY-0001"]));
                onEvent(new(value, AlgorithmEventKind.InputReleased));
                return ValueTask.FromResult(new AlgorithmDispatch(Task.CompletedTask));
            }
            Interlocked.Increment(ref pose);
            request = value; publish = onEvent;
            if (mode.StartsWith("Cancel", StringComparison.Ordinal)) token.Register(() =>
            {
                CancelEntered.TrySetResult();
                if (mode == "CancelThrow") throw new InvalidOperationException("Controlled cancellation callback failure");
                if (!ReleaseCancel.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Cancellation test watchdog");
            });
            InputUsed = mode != "Reject";
            Entered.TrySetResult();
            if (mode == "Reject") throw new AlgorithmNotDispatchedException(value.CallId, "TestRefusalBeforeInputAcquisition");
            if (mode == "WrongCallReject") throw new AlgorithmNotDispatchedException(Guid.NewGuid(), "WrongCallEvidence");
            if (mode == "AcceptedReject")
            {
                onEvent(new(value, AlgorithmEventKind.Accepted));
                throw new AlgorithmNotDispatchedException(value.CallId, "ContradictoryEvidence");
            }
            if (mode == "ThrowWithoutAccepted") throw new InvalidOperationException("Already using input without Accepted");
            if (mode.StartsWith("Sync", StringComparison.Ordinal) && !ReleaseDispatch.Wait(TimeSpan.FromSeconds(15)))
                throw new TimeoutException("Dispatch test watchdog");
            if (mode == "SyncThrow") throw new InvalidOperationException("Late dispatch exception after timeout");
            return ValueTask.FromResult(new AlgorithmDispatch(exited.Task));
        }
        public void Publish(AlgorithmEventKind kind) => publish?.Invoke(new(request!, kind, RawCodes: ["LATE-CODE"]));
        public void EndExecution() { Publish(AlgorithmEventKind.WorkerExited); exited.TrySetResult(); }
        public void FaultExit() => exited.TrySetException(new InvalidOperationException("Exit observation failed"));
        public void ReleaseAll() { ReleaseDispatch.Set(); ReleaseCancel.Set(); EndExecution(); }
        public void Dispose() { ReleaseDispatch.Dispose(); ReleaseCancel.Dispose(); }
    }
}
