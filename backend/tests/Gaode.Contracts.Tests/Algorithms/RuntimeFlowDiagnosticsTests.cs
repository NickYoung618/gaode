using System.Collections.Concurrent;
using System.Text.Json;
using Gaode.Application.Acquisition;
using Gaode.Application.Ports;
using Gaode.Application.Station01;
using Gaode.Application.Timing;
using Gaode.Contracts.Tests.Support;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Diagnostics;
using Gaode.Infrastructure.Persistence;
using Gaode.Infrastructure.Algorithms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Gaode.Contracts.Tests.Algorithms;

public sealed partial class AlgorithmRuntimeTests
{
    [Fact]
    public async Task RuntimeLogKeepsBoundedWorkerStderrWithoutDiscardingOrLeakingToken()
    {
        var log = new RuntimeLogCapture();
        using var sink = new RuntimeDiagnosticLogging(log);
        // Only the stderr reader is exercised: no child process/device is launched.
        await using var worker = new WorkerProcessSupervisor(Environment.ProcessPath!, Path.GetTempPath());
        var lines = "token=do-not-persist\nTraceback: injected worker failure\n" +
            string.Join('\n', Enumerable.Range(1, 50).Select(i => "worker-line-" + i));
        using var reader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(lines)));
        var method = typeof(WorkerProcessSupervisor).GetMethod("DrainStderrAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        await ((Task)method.Invoke(worker, [reader])!).WaitAsync(TimeSpan.FromSeconds(5));
        var saved = log.SaveEvidence("worker-stderr", worker.SessionId);
        Assert.DoesNotContain("do-not-persist", saved);
        Assert.Contains("Traceback: injected worker failure", saved);
        Assert.Contains("worker-line-50", saved);
        Assert.Contains("omittedLines", saved);
        Assert.Contains("REDACTED", saved);
    }

    [Fact]
    public async Task RuntimeLogRetainsOriginalEnqueueExceptionAndWriteIdentity()
    {
        var log = new RuntimeLogCapture();
        using var sink = new RuntimeDiagnosticLogging(log);
        var harness = Create(new ThrowingWriter());
        var error = await Assert.ThrowsAsync<SaveGateException>(() => harness.Run.SaveAsync(
            WriteKind.Audit, new { harmless = true }));
        Assert.Equal("SaveEnqueueFailed", error.Code);
        var saved = log.SaveEvidence("enqueue-failure", harness.Run.RunId);
        Assert.Contains("InjectedQueueFailure", saved);
        Assert.Contains("InjectedInnerCause", saved);
        Assert.Contains(error.WriteId.ToString(), saved);
        Assert.Contains(harness.Run.RequestId, saved);
        Assert.Contains("EnqueueFailed", saved);
        Assert.Equal(1, harness.Run.PersistedRevision);
    }

    [Fact]
    public async Task RuntimeLogDistinguishesAlgorithmDeadlineFromDispatchAndRetainsLease()
    {
        var log = new RuntimeLogCapture();
        using var sink = new RuntimeDiagnosticLogging(log);
        var port = new HangingDispatch(true);
        var harness = Create(new ImmediateWriter(CommitState.Committed), port);
        var task = harness.Runtime.InvokeAsync(harness.Run, AlgorithmRole.TrayPose,
            harness.Media.CaptureId, [harness.Media], "whole/1", CancellationToken.None);
        await port.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        harness.Clock.Advance(TimeSpan.FromMilliseconds(harness.Config.Budget.BusinessMs.TrayPoseAlgorithm!.Value));
        var result = await task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(AlgorithmState.TimedOut, result.State);
        Assert.Equal(1, harness.Store.LeaseCount);
        var saved = log.SaveEvidence("algorithm-deadline", harness.Run.RunId);
        Assert.Contains("DeadlineExceeded", saved);
        Assert.Contains(result.CallId.ToString(), saved);
        Assert.Contains(result.OperationId.ToString(), saved);
        Assert.Contains("DueTick", saved);
        Assert.Contains("Warning", saved);
        port.ReturnLate();
        port.ReleaseInputs();
    }

    [Fact]
    public async Task RuntimeLogShowsCaptureFailureWithoutLoggingMismatchedCallbacks()
    {
        var log = new RuntimeLogCapture();
        using var sink = new RuntimeDiagnosticLogging(log);
        var harness = Create(new ImmediateWriter(CommitState.Committed));
        var capture = new AcquisitionCoordinator(new FailedCapture(), harness.Store,
            new OperationIngress(new DeadlineScheduler(harness.Clock, "capture-diagnostics")),
            new TraceQuery(new DbContextOptionsBuilder<Station01DbContext>().UseSqlite("Data Source=:memory:").Options));
        await Assert.ThrowsAsync<IOException>(() => capture.CaptureAsync(harness.Run,
            CaptureRole.ThreeD, harness.Config.Public.Motion.Points.ThreeD, null, null,
            "test-camera", "test-light", 1024, CancellationToken.None));
        var saved = log.SaveEvidence("capture-failure", harness.Run.RunId);
        Assert.Contains("InjectedCaptureFailure", saved);
        // CameraAcquisitionService filters unrelated callbacks before notifying the coordinator.
        Assert.DoesNotContain("IgnoredMismatch", saved);
        var feedback = log.Documents(harness.Run.RunId).Where(x => x.GetProperty("step").GetString() == "CaptureFeedback");
        Assert.Equal("Failed", Assert.Single(feedback).GetProperty("facts").GetProperty("kind").GetString());
        Assert.DoesNotContain(log.Documents(harness.Run.RunId), x => x.GetProperty("step").GetString() == "MediaFileSave");
    }

    [Fact]
    public async Task RuntimeLogPreservesSqliteFailureEvenWhenWriterReturnsFailedReceipt()
    {
        var log = new RuntimeLogCapture();
        using var sink = new RuntimeDiagnosticLogging(log);
        // Isolated empty in-memory SQLite database: no schema, no shared file/device.
        var options = new DbContextOptionsBuilder<Station01DbContext>()
            .UseSqlite("Data Source=:memory:").Options;
        await using var writer = new TraceWriter(options, TimeProvider.System, 2);
        var runId = Guid.NewGuid();
        var batch = new WriteBatch(Guid.NewGuid(), runId, 0, WriteKind.Audit, "{}", "diagnostic-test");
        var receipt = await writer.SubmitCritical(batch).Completion.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(CommitState.Failed, receipt.State);
        var saved = log.SaveEvidence("sqlite-failure", runId);
        Assert.Contains("SqliteException", saved);
        Assert.Contains("no such table", saved);
        Assert.Contains(batch.WriteId.ToString(), saved);
        Assert.Contains("DatabaseCommit", saved);
    }

    private sealed class ThrowingWriter : ITraceWriter
    {
        public QueuedWrite SubmitCritical(WriteBatch batch, CancellationToken cancellationToken = default,
            Gaode.Domain.Station01.ActionWindow? window = null) =>
            throw new IOException("InjectedQueueFailure", new InvalidOperationException("InjectedInnerCause"));
        public Task<CommitReceipt?> ReconcileAsync(Guid writeId, CancellationToken cancellationToken) =>
            Task.FromResult<CommitReceipt?>(null);
    }

    private sealed class FailedCapture : ICapturePort
    {
        public long ConnectionEpoch => 5;
        public int TriggerCount(CaptureRole role) => 1;
        public ValueTask RequestCaptureAsync(CaptureRequest request, Action<CaptureEvent> onEvent,
            CancellationToken cancellationToken)
        {
            for (var i = 0; i < 40; i++) onEvent(new(request, CaptureEventKind.Ended, 4));
            onEvent(new(request, CaptureEventKind.Failed, 5, ErrorCode: "InjectedCaptureFailure"));
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RuntimeLogCapture : ILogger
    {
        private readonly ConcurrentQueue<(string Message, Exception? Exception)> entries = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => entries.Enqueue((formatter(state, exception), exception));
        internal JsonElement[] Documents(Guid runId) => entries
            .Where(x => x.Message.StartsWith("RuntimeFlow ", StringComparison.Ordinal))
            .Select(x => JsonSerializer.Deserialize<JsonElement>(x.Message["RuntimeFlow ".Length..]))
            .Where(x => x.GetProperty("runId").GetString() == runId.ToString()).ToArray();
        internal string SaveEvidence(string name, Guid runId)
        {
            var folder = Path.Combine(TestConfiguration.Workspace(), "artifacts", "station01-007",
                "runtime-log-20260924", "diagnostic-samples");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, name + "-" + runId.ToString("N") + ".log");
            File.WriteAllLines(path, entries.Where(x => x.Message.Contains(runId.ToString(), StringComparison.Ordinal))
                .Select(x => x.Message + Environment.NewLine + x.Exception));
            // Assertions use only the persisted file, not a live API.
            return File.ReadAllText(path);
        }
    }
}
