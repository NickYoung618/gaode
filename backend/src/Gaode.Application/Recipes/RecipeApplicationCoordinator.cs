using Gaode.Application.Ports;
using Gaode.Application.Station01;
using Gaode.Application.Timing;
using Gaode.Domain.Station01;
using Gaode.Diagnostics;

namespace Gaode.Application.Recipes;

public sealed record RecipeApplicationOutcome(RecipeBindingReceipt Receipt, PublicPreparationHandoffV2? Handoff);

// All three entry paths supply their actual frozen run, input and existing deadlines.
// Only current committed software facts can authorize Bound and handoff consumption.
public sealed class RecipeApplicationCoordinator
{
    public static RecipeApplicationBudgetSource RequireBudget(string id, string version, string schema, string purpose,
        string source, string digest, string snapshot, int? milliseconds)
    {
        if (schema != "2.0" || milliseconds is null or <= 0 ||
            new[] { id, version, source, digest, snapshot }.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("RecipeApplicationBudgetInvalid");
        if (purpose == "Production") throw new InvalidOperationException("RecipeApplicationProductionBudgetUnapproved");
        if (purpose != "Test") throw new InvalidOperationException("RecipeApplicationPurposeInvalid");
        return new(id, version, schema, purpose, source, digest, snapshot, milliseconds.Value);
    }
    public static ActionWindow RegisterWindow(TimeProvider clock, string clockId, int milliseconds,
        IReadOnlyList<DateTimeOffset> existingDeadlines)
    {
        if (milliseconds <= 0) throw new InvalidOperationException("RecipeApplicationBudgetInvalid");
        var start = clock.GetTimestamp(); var utc = clock.GetUtcNow();
        var ownDue = checked(start + (long)(milliseconds * (double)clock.TimestampFrequency / 1000d));
        var effective = existingDeadlines.Aggregate(ownDue, (due, prior) => Math.Min(due,
            checked(start + (long)((prior - utc).TotalSeconds * clock.TimestampFrequency))));
        if (effective <= start) throw new TimeoutException("ExistingStageDeadlineExpired");
        return new(start, effective, clockId, utc, utc.AddSeconds((effective - start) / (double)clock.TimestampFrequency));
    }
    public async Task<RecipeApplicationOutcome> ExecuteAsync(RunExecution run, RecipeRunPlan plan, string revision,
        Guid bindingId, long epoch, int? ngCapacity, int? pendingCapacity, IReadOnlyList<DateTimeOffset> existingDeadlines,
        Func<CancellationToken, Task<PublicPreparationHandoffV2?>> saveHandoff, CancellationToken token,
        IReadOnlyList<RecipeDeadlineReference>? deadlineReferences = null)
    {
        if (run.RecipeApplicationCorrelation is not null || run.RecipeApplicationReceipt is not null || bindingId == Guid.Empty)
            throw new InvalidOperationException("RecipeApplicationAlreadyRegistered");
        var budget = run.Config.Budget;
        var source = RequireBudget(budget.Id, budget.Version, budget.SchemaVersion, budget.Purpose, budget.Source,
            run.Config.BudgetDigest, run.Config.SnapshotId, budget.BusinessMs.RecipeApplication);
        if (budget.Purpose != run.Config.Public.Purpose) throw new InvalidOperationException("RecipeApplicationPurposeMismatch");
        var operation = Guid.NewGuid();
        // Legacy continuous runs and the v1 independent entry retain their actual
        // tray in the validated start context even without a v2 frozen identity.
        var trayId = run.Identity?.TrayId ?? StartRunContextParser.Parse(run.ContextJson).TrayId;
        var correlation = new ActionCorrelation(run.RunId, operation, bindingId, 1, run.SessionId, epoch,
            run.Config.SnapshotId, revision, trayId);
        // Only the pre-existing applicable deadline constrains the intent; t0 does not yet exist.
        if (existingDeadlines.Count > 0)
            run.RequiredSaveWindow = ActionWindows.FromUtc(run.Clock, run.Clock.GetUtcNow(), existingDeadlines.Min(), run.ClockId);
        CommitReceipt intent;
        try
        {
            intent = await run.SaveAsync(WriteKind.ActionIntent, new
            { kind = "RecipePlanAndBindingIntent", operationId = operation, actionId = bindingId, attempt = 1,
                snapshotId = run.Config.SnapshotId, targetOrScope = plan.RecipeId, bindingId, planRevision = revision,
                plan.RecipeId, plan.RecipeVersion, plan.CatalogDigest, displayRecipeId = plan.PlcRecipeId,
                budgetSource = source, ngCapacity, pendingCapacity, frozenExecutionInputs = run.ExecutionInputs }, cancellationToken: token);
        }
        finally { run.RequiredSaveWindow = null; }
        token.ThrowIfCancellationRequested();
        var intentCommit = new RequiredCommitEvidence(intent.WriteId, correlation, ActualCommitState.Committed,
            ReceiptValidity.ValidCurrent, intent.CommittedRevision, intent.CommittedUtc, run.LastCommitObservedTick, intent.ErrorCode)
            { RecordKind = intent.RecordKind, SavePurpose = "BindingIntent" };
        var window = RegisterWindow(run.Clock, run.ClockId, source.BudgetMs, existingDeadlines);
        static string Tick(long value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var registration = new RecipeApplicationRegistration(window.ClockId, Tick(run.Clock.TimestampFrequency), Tick(window.StartTick),
            Tick(checked(window.StartTick + (long)(source.BudgetMs * (double)run.Clock.TimestampFrequency / 1000d))), Tick(window.DueTick),
            window.StartedUtc, window.DeadlineUtc, deadlineReferences);
        RuntimeDiagnostics.Record("RecipeApplication", "WindowRegistered", run.RunId,
            new { bindingId, intent.WriteId, source, window, existingDeadlines });
        using var deadline = new RecipeDeadlineCancellation(run.Clock, window.DueTick);
        using var pending = CancellationTokenSource.CreateLinkedTokenSource(token, deadline.Token);
        run.RecipeApplicationCorrelation = correlation;
        run.RequiredSaveWindow = window;
        run.RequiredSaveCancellation = pending.Token;
        RecipeBindingReceipt receipt;
        PublicPreparationHandoffV2? handoff;
        try
        {
            RequireCurrent(run, window, pending.Token);
            await run.SaveAsync(WriteKind.ActionFact, new
            { schemaVersion = "recipe-binding/1", kind = "RecipePlanBound", bindingId, planRevision = revision,
                plan.RecipeId, plan.RecipeVersion, plan.DefinitionDigest, plan.CatalogDigest, source, window,
                registration, intentCommit, correlation,
                businessAuthorization = "AwaitingRequiredCommits" }, cancellationToken: pending.Token);
            if (run.RecipePlan is null) run.BindRecipePlan(plan, revision, bindingId);
            handoff = await saveHandoff(pending.Token);
            // Each save has already validated reception against the original window. A later
            // continuation must not turn that timely validated receipt into a timeout.
            var required = run.RecipeApplicationCommits.ToArray();
            if (required.Length == 0 || required.Any(c => c.Correlation != correlation || c.ActualCommit != ActualCommitState.Committed ||
                c.Validity != ReceiptValidity.ValidCurrent || c.CommittedUtc is null || c.ReceivedTick is not { } at || !window.Contains(at)) ||
                handoff is not null && !required.Any(c => c.WriteId == handoff.WriteId))
                throw new InvalidOperationException("RecipeRequiredCommitReceiptInvalid");
            var received = required.Max(c => c.ReceivedTick!.Value);
            receipt = new(correlation, bindingId.ToString("D"), plan.RecipeId, plan.RecipeVersion, plan.DefinitionDigest, source, window, Array.AsReadOnly(required), received,
                ReceiptValidity.ValidCurrent, RecipeApplicationState.Completed)
                { Registration = registration, IntentCommit = intentCommit };
            run.RecipeApplicationReceipt = receipt;
            run.RecipeApplicationCancellation = token;
            deadline.Dispose();
        }
        catch (Exception error)
        {
            pending.Cancel();
            RuntimeDiagnostics.Record("RecipeApplication", "ClosedWithoutAuthorization", run.RunId,
                new { bindingId, window, reason = error.GetType().Name, actualCommits = run.RecipeApplicationCommits,
                    newDispatchAllowed = false, oldActionRevivalAllowed = false }, error);
            throw;
        }
        finally { run.RequiredSaveWindow = null; run.RequiredSaveCancellation = default; }
        await run.ReportAsync(save: SaveState.Committed, persistedRevision: run.PersistedRevision);
        // This observation is not another recursively required success receipt.
        try
        {
            run.RecipeApplicationCorrelation = null;
            await run.SaveAsync(WriteKind.Audit, new { kind = "RecipeApplicationReceiptObserved", receipt,
                handoffId = handoff?.HandoffId, authorizesRepeatedProductActions = false }, cancellationToken: token);
        }
        catch (Exception error)
        { RuntimeDiagnostics.Record("RecipeApplication", "ReceiptObservationNotRecorded", run.RunId,
            new { bindingId, receipt.State, historicalReceiptMustRemainNotRecorded = true }, error); }
        return new(receipt, handoff);
    }
    private static void RequireCurrent(RunExecution run, ActionWindow window, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!window.Contains(run.Clock.GetTimestamp())) throw new TimeoutException("RecipeApplicationDeadlineExceeded");
    }

    // The timer may wake early on the platform clock. Only the original
    // monotonic cutoff can expire the business window; never register a new one.
    private sealed class RecipeDeadlineCancellation : CancellationTokenSource
    {
        private readonly TimeProvider clock;
        private readonly long due;
        private readonly ITimer timer;
        private int closed;
        public RecipeDeadlineCancellation(TimeProvider clock, long due)
        {
            this.clock = clock;
            this.due = due;
            timer = clock.CreateTimer(_ => Wake(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            Wake();
        }
        private void Wake()
        {
            if (Volatile.Read(ref closed) != 0 || IsCancellationRequested) return;
            var remaining = due - clock.GetTimestamp();
            try
            {
                if (remaining <= 0) Cancel();
                else timer.Change(TimeSpan.FromMilliseconds(Math.Max(1,
                    Math.Ceiling(remaining * 1000d / clock.TimestampFrequency))), Timeout.InfiniteTimeSpan);
            }
            catch (ObjectDisposedException) { }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Exchange(ref closed, 1) == 0) timer.Dispose();
            base.Dispose(disposing);
        }
    }
}
