using System.Text.Json;
using Gaode.Application.Configuration;
using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Application.Station01;
using Gaode.Contracts.Tests.Support;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Gaode.Contracts.Tests.Recipes;

// Software binding component with real SQLite. Prepared handoff references are
// explicit inputs; this does not claim execution of the public 3D/F producer.
public sealed class RecipeBindingSaveProtectionTests
{
    [Theory]
    [InlineData("Timely")]
    [InlineData("BoundRollback")]
    [InlineData("BoundLate")]
    [InlineData("HandoffLate")]
    [InlineData("CancelAtBound")]
    public async Task OnlyCurrentActualRequiredSavesAuthorizeBinding(string condition)
    {
        var root = Path.Combine(Gaode.Testing.ApprovedTestRoot.Resolve(TestConfiguration.Workspace()),
            "011-binding-components", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var loader = TestConfiguration.Loader();
        var config = ConfigurationFreezer.Freeze(loader.LoadPublic(new("s01-public-dev", "1.0.0")),
            loader.LoadBudget(new("s01-budget-dev", "3.0.0")), loader.LoadSimulation(new("s01-sim-normal", "3.0.0")),
            new Dictionary<string, string>());
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using var cancellation = new CancellationTokenSource();
        var runId = Guid.NewGuid(); var tray = Guid.NewGuid(); var binding = Guid.NewGuid();
        var plan = Recipe011Data.Plan(tray);
        var revision = RecipePlanRevision.Compute(plan);
        var options = new DbContextOptionsBuilder<Station01DbContext>().UseSqlite($"Data Source={Path.Combine(root, "run.db")}")
            .AddInterceptors(new BoundFailure(condition == "BoundRollback")).Options;
        await using (var setup = new Station01DbContext(options))
        {
            await setup.Database.MigrateAsync();
            setup.Runs.Add(new RunEntity { RunId = runId, RequestId = "component", SubjectId = "Test", CreatedUtc = clock.GetUtcNow() });
            await setup.SaveChangesAsync();
        }
        bool Bound(WriteBatch batch) => batch.Kind == WriteKind.ActionFact && batch.PayloadJson.Contains("RecipePlanBound", StringComparison.Ordinal);
        Guid? controlledWrite = null;
        await using var writer = new TraceWriter(options, clock, 32,
            beforeCommit: (batch, _) =>
            {
                if (condition == "BoundRollback" && Bound(batch))
                { controlledWrite = batch.WriteId; }
                return Task.CompletedTask;
            },
            afterCommitBeforeReceipt: (batch, receipt, _) =>
            {
                if (condition == "BoundLate" && Bound(batch) || condition == "HandoffLate" && batch.Kind == WriteKind.HandoffV2)
                {
                    Assert.Equal(CommitState.Committed, receipt.State);
                    controlledWrite = batch.WriteId;
                    clock.Advance(TimeSpan.FromMilliseconds(config.Budget.BusinessMs.RecipeApplication!.Value + 1));
                }
                if (condition == "CancelAtBound" && Bound(batch))
                { controlledWrite = batch.WriteId; cancellation.Cancel(); }
                return Task.CompletedTask;
            });
        var context = JsonSerializer.Serialize(new { schemaVersion = StartRunContext.CurrentSchemaVersion, trayId = tray,
            stationId = Guid.NewGuid(), lineId = Guid.NewGuid(), scenarioId = plan.ScenarioId, occupiedSlots = new[] { "s1" }, purpose = "Test" });
        var run = new RunExecution(runId, Guid.NewGuid(), "component", "Test", context, config, writer, clock, Guid.NewGuid(), "declared-clock");
        var identity = new WorkflowIdentity(runId, tray, Guid.NewGuid().ToString(), Guid.NewGuid().ToString(),
            "component", plan.ScenarioId, ["s1"], clock.GetUtcNow(), "Test", RunPurpose.Test,
            config.Public.Version, config.Budget.Version, config.Simulation.Version);
        var handoffCalls = 0;
        var originalDeadline = clock.GetUtcNow().AddMilliseconds(config.Budget.BusinessMs.RecipeApplication!.Value);
        async Task<PublicPreparationHandoffV2?> SaveHandoff(CancellationToken token)
        {
            handoffCalls++;
            return await run.SaveHandoffV2Async(new(PublicPreparationHandoffV2.CurrentSchemaVersion, Guid.NewGuid(), identity,
                "declared-points", "declared-capabilities", ["declared-3d-media"], ["declared-f-media"], ["declared-call"],
                plan.FCode, "declared-plan", revision, "recipe-binding://" + binding, ComponentEvidenceSource.Test,
                "DeclaredComponent", ["declared-source"], Guid.Empty, 0, clock.GetUtcNow(), ""), token);
        }
        var coordinator = new RecipeApplicationCoordinator();
        RecipeApplicationOutcome? outcome = null;
        var failure = await Record.ExceptionAsync(async () => outcome = await coordinator.ExecuteAsync(run, plan, revision,
            binding, 1, plan.NgCapacity, plan.PendingCapacity, [originalDeadline], SaveHandoff, cancellation.Token).WaitAsync(TimeSpan.FromSeconds(15)));
        using var drainLimit = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (writer.PendingCount > 0) await Task.Delay(10, drainLimit.Token);
        await using var db = new Station01DbContext(options);
        var rows = await db.Writes.Where(w => w.RunId == runId).OrderBy(w => w.Revision).ToArrayAsync();
        Assert.Contains(rows, w => w.Kind == "ActionIntent" && w.PayloadJson.Contains("RecipePlanAndBindingIntent", StringComparison.Ordinal));
        Assert.DoesNotContain(rows, w => w.PayloadJson.Contains("DeviceRecipeApplied", StringComparison.Ordinal));
        if (condition == "Timely")
        {
            Assert.Null(failure); Assert.NotNull(outcome); Assert.True(outcome.Receipt.WasCompletedInWindow);
            Assert.Equal(originalDeadline, outcome.Receipt.Window.DeadlineUtc);
            Assert.Equal(2, outcome.Receipt.RequiredCommits.Count);
            Assert.NotNull(outcome.Handoff);
            Assert.Contains(rows, w => w.WriteId == outcome.Handoff.WriteId);
            Assert.Equal(1, handoffCalls);
        }
        else
        {
            Assert.NotNull(failure); Assert.Null(outcome); Assert.Null(run.RecipeApplicationReceipt);
            Assert.NotNull(controlledWrite);
            Assert.Equal(condition == "HandoffLate" ? 1 : 0, handoffCalls);
            if (condition == "BoundRollback") Assert.DoesNotContain(rows, w => w.WriteId == controlledWrite);
            else Assert.Contains(rows, w => w.WriteId == controlledWrite); // actual commits are never erased by invalid reception
            Assert.DoesNotContain(rows, w => w.PayloadJson.Contains("RecipeApplicationReceiptObserved", StringComparison.Ordinal));
        }
        await File.WriteAllTextAsync(Path.Combine(root, "binding-result.json"), JsonSerializer.Serialize(new
            { condition, originalDeadline, outcome, error = failure?.Message, controlledWrite, handoffCalls, rows, scope = "SoftwareBindingActualSQLite" }));
    }
    private sealed class BoundFailure(bool enabled) : SaveChangesInterceptor
    {
        public override InterceptionResult<int> SavingChanges(DbContextEventData data, InterceptionResult<int> result)
        {
            if (enabled && data.Context!.ChangeTracker.Entries<WriteEntity>().Any(e =>
                e.Entity.Kind == "ActionFact" && e.Entity.PayloadJson.Contains("RecipePlanBound", StringComparison.Ordinal)))
                throw new IOException("DeclaredBoundTransactionFailure");
            return result;
        }
    }

}
