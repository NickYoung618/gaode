using System.Text.Json;
using Gaode.Application.Configuration;
using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Application.Station01;
using Gaode.Contracts.Tests.Recipes;
using Gaode.Contracts.Tests.Support;
using Gaode.Domain.Station01;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Gaode.Contracts.Tests.Station01;

// Controlled-clock business components; no device/TCP/SQLite acceptance claim.
public sealed class RecipeApplicationContractTests
{
    [Fact]
    public void RunCancellationClosesRegisteredBindingTokenWithoutTurningPauseIntoCancellation()
    {
        var control = new ControlLatch();
        using var binding = CancellationTokenSource.CreateLinkedTokenSource(control.Cancellation);
        control.RequestStop(); Assert.False(binding.IsCancellationRequested);
        control.Resume(); control.RequestCancel();
        Assert.True(binding.IsCancellationRequested); Assert.True(control.AdmissionClosed);
        control.Resume(); Assert.True(control.CancelRequested);
    }

    [Fact]
    public async Task BoundReceiptCannotAuthorizeBindingWithoutActualRequiredSave()
    {
        var f = new Fixture(); f.Writer.RejectKind = "RecipePlanBound";
        await Assert.ThrowsAsync<SaveGateException>(() => f.Execute());
        Assert.Equal(0, f.HandoffCalls); Assert.Null(f.Run.RecipeApplicationReceipt);
    }

    [Fact]
    public async Task EarlyPlatformTimerWakeDoesNotCloseOriginalBindingWindow()
    {
        var clock = new FakeTimeProvider(); var timerClock = new EarlyWakeClock(clock);
        var f = new Fixture(clock, timerClock);
        f.BeforeRequiredSave = () => {
            var bindingTimer = Assert.Single(timerClock.Timers, t => t.LastDue == TimeSpan.FromMilliseconds(10000));
            clock.Advance(TimeSpan.FromMilliseconds(9999)); bindingTimer.Fire();
            Assert.Equal(TimeSpan.FromMilliseconds(1), bindingTimer.LastDue);
        };
        var result = await f.Execute();
        Assert.True(result.Receipt.WasCompletedInWindow);
        Assert.Equal(10000, (result.Receipt.Window.DeadlineUtc - result.Receipt.Window.StartedUtc).TotalMilliseconds);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CapacityInputsKeepTrayIdentityAndAllNecessarySavesInOneWindow(bool capacity)
    {
        var f = new Fixture(); var outcome = await f.Execute(capacity);
        var receipt = outcome.Receipt;
        Assert.True(receipt.WasCompletedInWindow); Assert.Equal(f.TrayId, receipt.Correlation.TrayId);
        Assert.Equal(new[] { "RecipePlanAndBindingIntent", "RecipePlanBound", "RequiredComponentHandoff", "RecipeApplicationReceiptObserved" },
            f.Writer.Writes.Select(w => w.Kind));
        using var intent = JsonDocument.Parse(f.Writer.Writes[0].Batch.PayloadJson);
        Assert.Equal(capacity ? 2 : (int?)null, intent.RootElement.GetProperty("ngCapacity").Deserialize<int?>());
        Assert.Equal(capacity ? 3 : (int?)null, intent.RootElement.GetProperty("pendingCapacity").Deserialize<int?>());
        Assert.Null(f.Writer.Writes[0].Window); Assert.Null(f.Writer.Writes[3].Window);
        Assert.Equal(receipt.Window, f.Writer.Writes[1].Window); Assert.Equal(receipt.Window, f.Writer.Writes[2].Window);
        Assert.Equal(2, receipt.RequiredCommits.Count); Assert.All(receipt.RequiredCommits, c => Assert.NotNull(c.CommittedUtc));
        Assert.Equal(f.Writer.Writes[0].Batch.WriteId, receipt.IntentCommit!.WriteId);
        Assert.Equal(f.Writer.IntentCommittedTick, receipt.Window.StartTick);
        Assert.Equal(1, f.HandoffCalls);
        Assert.Equal(receipt.Window.StartTick.ToString(System.Globalization.CultureInfo.InvariantCulture), receipt.Registration!.StartTick);
        Assert.Equal(f.Clock.TimestampFrequency.ToString(System.Globalization.CultureInfo.InvariantCulture), receipt.Registration.ClockFrequency);
        var json = JsonSerializer.Serialize(receipt, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("deviceEvidence", json); Assert.DoesNotContain("DeviceApplied", json);
    }

    [Fact]
    public async Task FailedIntentNeverRegistersTotalWindowOrCreatesBound()
    {
        var f = new Fixture(); f.Writer.RejectKind = "RecipePlanAndBindingIntent";
        await Assert.ThrowsAsync<SaveGateException>(() => f.Execute());
        Assert.Single(f.Writer.Writes); Assert.Null(f.Run.RecipeApplicationReceipt); Assert.Equal(0, f.HandoffCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateOrCancelledBoundReturnDoesNotCreateHandoff(bool cancelled)
    {
        var f = new Fixture(); using var cancel = new CancellationTokenSource();
        f.Writer.BeforeReceipt = kind => { if (kind == "RecipePlanBound") {
            if (cancelled) cancel.Cancel(); else f.Clock.Advance(TimeSpan.FromMilliseconds(10000));
        } };
        await Assert.ThrowsAsync<SaveGateException>(() => f.Execute(token: cancel.Token));
        Assert.Equal(0, f.HandoffCalls); Assert.Null(f.Run.RecipeApplicationReceipt);
    }

    [Fact]
    public async Task ExpiredExistingDeadlineRejectsBeforeIntentAndDoesNotRefreshIt()
    {
        var f = new Fixture();
        await Assert.ThrowsAsync<TimeoutException>(() => f.Execute(existing: [f.Clock.GetUtcNow()]));
        Assert.Empty(f.Writer.Writes); Assert.Equal(0, f.HandoffCalls);
    }

    [Fact]
    public async Task TimelyRequiredReceiptsRemainValidAfterLaterContinuation()
    {
        var f = new Fixture(); f.AfterRequiredSave = () => f.Clock.Advance(TimeSpan.FromMilliseconds(10000));
        var outcome = await f.Execute();
        Assert.True(outcome.Receipt.WasCompletedInWindow);
        Assert.False(outcome.Receipt.Window.Contains(f.Clock.GetTimestamp()));
        Assert.Equal(2, outcome.Receipt.RequiredCommits.Count);
    }

    [Fact]
    public async Task BoundCommitAloneCannotCompleteWhenHandoffSaveFails()
    {
        var f = new Fixture(); f.Writer.RejectKind = "RequiredComponentHandoff";
        await Assert.ThrowsAsync<SaveGateException>(() => f.Execute());
        Assert.Contains(f.Writer.Writes, w => w.Kind == "RecipePlanBound"); Assert.Null(f.Run.RecipeApplicationReceipt);
    }

    [Theory]
    [InlineData(9999, true)]
    [InlineData(10000, false)]
    [InlineData(10001, false)]
    public async Task LastRequiredReceiptUsesOriginalTotalWindow(int receivedAt, bool valid)
    {
        var f = new Fixture();
        f.BeforeRequiredSave = () => f.Clock.Advance(TimeSpan.FromMilliseconds(9000));
        f.Writer.BeforeReceipt = kind => { if (kind == "RequiredComponentHandoff") f.Clock.Advance(TimeSpan.FromMilliseconds(receivedAt - 9000)); };
        if (valid) Assert.True((await f.Execute()).Receipt.WasCompletedInWindow);
        else { await Assert.ThrowsAsync<SaveGateException>(() => f.Execute()); Assert.Null(f.Run.RecipeApplicationReceipt); }
        Assert.Equal(1, f.HandoffCalls);
        Assert.Equal(10000, (f.Writer.Writes[1].Window!.DeadlineUtc - f.Writer.Writes[1].Window!.StartedUtc).TotalMilliseconds);
    }

    [Fact]
    public async Task EarlierDownstreamDeadlineClosesNecessarySaveWithoutRefreshingAnyDeadline()
    {
        var f = new Fixture(); var deadline = f.Clock.GetUtcNow().AddMilliseconds(2000);
        f.BeforeRequiredSave = () => f.Clock.Advance(TimeSpan.FromMilliseconds(1500));
        f.Writer.BeforeReceipt = kind => { if (kind == "RequiredComponentHandoff") f.Clock.Advance(TimeSpan.FromMilliseconds(500)); };
        await Assert.ThrowsAsync<SaveGateException>(() => f.Execute(existing: [deadline]));
        Assert.Equal(deadline, f.Writer.Writes[1].Window!.DeadlineUtc); Assert.Null(f.Run.RecipeApplicationReceipt);
    }

    private sealed class Fixture
    {
        public readonly FakeTimeProvider Clock; public readonly Guid TrayId = Guid.NewGuid();
        public readonly SemanticWriter Writer; public readonly RunExecution Run; public readonly RecipeRunPlan Plan;
        public int HandoffCalls; public Action? AfterRequiredSave; public Action? BeforeRequiredSave;
        public Fixture(FakeTimeProvider? clock = null, TimeProvider? runtimeClock = null)
        {
            Clock = clock ?? new(); Writer = new(Clock);
            var loader = TestConfiguration.Loader();
            var config = ConfigurationFreezer.Freeze(loader.LoadPublic(new("s01-public-dev", "1.0.0")),
                loader.LoadBudget(new("s01-budget-dev", "3.0.0")), loader.LoadSimulation(new("s01-sim-normal", "3.0.0")),
                new Dictionary<string, string>());
            var context = JsonSerializer.Serialize(new { schemaVersion = StartRunContext.CurrentSchemaVersion, trayId = TrayId,
                stationId = "S01", lineId = "Test", scenarioId = "component-scenario", occupiedSlots = new[] { "s1" }, purpose = "Test" });
            Run = new(Guid.NewGuid(), Guid.NewGuid(), "semantic-binding", "UnitTest", context, config, Writer,
                runtimeClock ?? Clock, Guid.NewGuid(), "semantic-binding-clock");
            Plan = Recipe011Data.Plan(TrayId);
        }
        public Task<RecipeApplicationOutcome> Execute(bool capacity = true, IReadOnlyList<DateTimeOffset>? existing = null,
            CancellationToken token = default) => new RecipeApplicationCoordinator().ExecuteAsync(Run, Plan,
                RecipePlanRevision.Compute(Plan), Guid.NewGuid(), 1, capacity ? 2 : null, capacity ? 3 : null,
                existing ?? [], async ct => {
                    HandoffCalls++; BeforeRequiredSave?.Invoke();
                    await Run.SaveAsync(WriteKind.Complete, new { kind = "RequiredComponentHandoff" }, cancellationToken: ct);
                    AfterRequiredSave?.Invoke(); return null;
                }, token);
    }
    private sealed class SemanticWriter(FakeTimeProvider clock) : ITraceWriter
    {
        public readonly List<(WriteBatch Batch, string? Kind, ActionWindow? Window)> Writes = [];
        public string? RejectKind; public Action<string?>? BeforeReceipt; public long IntentCommittedTick;
        public QueuedWrite SubmitCritical(WriteBatch batch, CancellationToken cancellationToken = default, ActionWindow? window = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var doc = JsonDocument.Parse(batch.PayloadJson); var kind = doc.RootElement.GetProperty("kind").GetString();
            Writes.Add((batch, kind, window)); if (kind == "RecipePlanAndBindingIntent") IntentCommittedTick = clock.GetTimestamp();
            BeforeReceipt?.Invoke(kind);
            var receipt = new CommitReceipt(batch.WriteId, batch.RunId, kind == RejectKind ? CommitState.Failed : CommitState.Committed,
                batch.ExpectedRevision + 1, TerminalOutcome.None, kind == RejectKind ? "SemanticUnitRejected" : null, clock.GetUtcNow());
            return new(new(batch.WriteId, batch.RunId, CommitState.Queued, null, TerminalOutcome.None, null), Task.FromResult(receipt));
        }
        public Task<CommitReceipt?> ReconcileAsync(Guid writeId, CancellationToken token) => Task.FromResult<CommitReceipt?>(null);
    }
    private sealed class EarlyWakeClock(FakeTimeProvider inner) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => inner.GetUtcNow();
        public override long GetTimestamp() => inner.GetTimestamp();
        public override long TimestampFrequency => inner.TimestampFrequency;
        public List<ProbeTimer> Timers { get; } = [];
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        { var timer = new ProbeTimer(callback, state, dueTime); Timers.Add(timer); return timer; }
        public sealed class ProbeTimer(TimerCallback callback, object? state, TimeSpan due) : ITimer
        {
            public TimeSpan LastDue = due; public void Fire() => callback(state);
            public bool Change(TimeSpan dueTime, TimeSpan period) { LastDue = dueTime; return true; }
            public void Dispose() { } public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
