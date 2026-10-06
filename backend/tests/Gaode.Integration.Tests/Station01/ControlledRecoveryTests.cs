using System.Net;
using System.Net.Http.Json;
using Gaode.Application.Ports;
using Gaode.Application.Workflow;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Persistence;
using Gaode.Integration.Tests.Support;
using Gaode.Host.Api;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Gaode.Integration.Tests.Station01;

public sealed class ControlledRecoveryTests
{
    [Fact]
    public async Task OldTaskRequiresPersistedAuthenticatedDecisionWithCompleteAuditEvidence()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<Station01DbContext>()
            .UseSqlite(connection).Options;
        var runId = Guid.NewGuid();
        var trayId = Guid.NewGuid();
        await using (var db = new Station01DbContext(options))
        {
            await db.Database.MigrateAsync();
            db.Runs.Add(new RunEntity { RunId = runId, RequestId = "old-task-run",
                SubjectId = "operator", Revision = 4, State = RunState.RecoveryRequired,
                CreatedUtc = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        var store = new ControlledRecoveryDecisionStore(options);
        var service = new ControlledRecoveryService(store);

        foreach (var trigger in new[] { RecoveryAuthorizationTrigger.HostRestart,
                     RecoveryAuthorizationTrigger.PlcReconnect,
                     RecoveryAuthorizationTrigger.OrdinaryReset })
        {
            var automatic = await service.AuthorizeAsync(runId, trayId, "old-task-17", 4,
                trigger, false);
            Assert.False(automatic.IsAuthorized);
            Assert.Equal("AutomaticRecoveryDecisionForbidden", automatic.Reason);
        }
        var missing = await service.AuthorizeAsync(runId, trayId, "old-task-17", 4,
            RecoveryAuthorizationTrigger.AuthenticatedManualDecision, true);
        Assert.False(missing.IsAuthorized);
        Assert.Equal("ControlledRecoveryDecisionRequired", missing.Reason);

        var decision = await service.RecordDecisionAsync("decision-request-1", 4,
            "old-task-17", runId, trayId, Guid.NewGuid(), "Detection",
            ControlledRecoveryAction.ReDetect, "test:SystemAdministrator",
            "SystemAdministrator", "人工核对原任务和媒体后允许重新检测",
            ["stage-event://17", "media://capture/17"]);
        var authorized = await service.AuthorizeAsync(runId, trayId, "old-task-17", 4,
            RecoveryAuthorizationTrigger.AuthenticatedManualDecision, true);

        Assert.True(authorized.IsAuthorized);
        Assert.Equal(decision.DecisionId, authorized.DecisionId);
        Assert.Equal(ControlledRecoveryAction.ReDetect, authorized.AuthorizedAction);
        Assert.False(authorized.CreatesAlgorithmFact);
        Assert.False(authorized.CreatesPlcFact);
        Assert.False(authorized.CreatesCompletionFact);
        await using var verify = new Station01DbContext(options);
        var persisted = await verify.ControlledRecoveryDecisions.SingleAsync();
        Assert.Equal("test:SystemAdministrator", persisted.ActorId);
        Assert.Equal("SystemAdministrator", persisted.ActorRole);
        Assert.Equal("old-task-17", persisted.OriginalTaskId);
        Assert.Contains("stage-event://17", persisted.EvidenceReferencesJson,
            StringComparison.Ordinal);
        Assert.Empty(await verify.WholeTrayCompletions.ToListAsync());
        Assert.Empty(await verify.StageEvents.ToListAsync());
    }

    [Fact]
    public async Task RestartRebuildsCommittedFactsMarksInFlightPlcUnknownAndNeverReplaysCompletion()
    {
        var runId = Guid.NewGuid();
        var trayId = Guid.NewGuid();
        await using var fixture = await Station01HostFixture.CreateAsync(afterStorePrepared:
            async storeRoot =>
            {
                var options = new DbContextOptionsBuilder<Station01DbContext>().UseSqlite(
                    StoreCompatibilityProbe.ReadWriteConnectionString(storeRoot)).Options;
                await using (var db = new Station01DbContext(options))
                {
                    db.Runs.Add(new RunEntity { RunId = runId, RequestId = "restart-run",
                        SubjectId = "operator", Revision = 8, State = RunState.Sorting,
                        CreatedUtc = DateTimeOffset.UtcNow });
                    await db.SaveChangesAsync();
                }
                var now = DateTimeOffset.UtcNow;
                await new StageEventStore(options).AppendAsync(new StageEventAppendRequest(
                    Guid.NewGuid(), runId, trayId, Guid.NewGuid().ToString(),
                    Guid.NewGuid().ToString(), WholeTrayWorkflowStage.Sorting, Guid.NewGuid(),
                    1, 12, StageEventType.Started, now, ResultSource.Virtual,
                    ResultQuality.Derived, null, "started-digest", "{}", "restart-started",
                    "frozen-plan-8", now, now.AddSeconds(120)));
            });

        var snapshot = await fixture.Client.GetFromJsonAsync<RunApiSnapshot>(
            $"/api/v1/station01/runs/{runId:D}");
        Assert.NotNull(snapshot);
        Assert.Equal(RunState.RecoveryRequired, snapshot.State);
        Assert.Contains("RecoveryRequired:NoAutomaticReplay", snapshot.Events);
        Assert.Equal(0, fixture.Plc.StartCommands);
        Assert.Equal(0, fixture.Plc.MoveCommands);

        var dbOptions = fixture.Host.Services
            .GetRequiredService<DbContextOptions<Station01DbContext>>();
        await using (var beforeDecision = new Station01DbContext(dbOptions))
            Assert.Empty(await beforeDecision.ControlledRecoveryDecisions.Where(x =>
                x.RunId == runId).ToListAsync());

        var decisionRequest = new ControlledRecoveryDecisionRequest("restart-decision-1",
            snapshot.ObservedRevision, trayId, "old-task-restart", null, "Sorting",
            ControlledRecoveryAction.Scrap, "核对未知物理动作后决定报废旧任务",
            ["stage-event://restart-started", "operator-check://restart-1"]);
        var forbidden = await fixture.Client.PostAsJsonAsync(
            $"/api/v1/station01/runs/{runId:D}/controlled-recovery-decisions",
            decisionRequest);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var administrator = fixture.ClientForRole("SystemAdministrator");
        var accepted = await administrator.PostAsJsonAsync(
            $"/api/v1/station01/runs/{runId:D}/controlled-recovery-decisions",
            decisionRequest);
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);

        await using var verify = new Station01DbContext(dbOptions);
        var stageEvents = await verify.StageEvents.Where(x => x.RunId == runId)
            .OrderBy(x => x.Sequence).ToListAsync();
        Assert.Equal(2, stageEvents.Count);
        Assert.Equal(StageEventType.UnknownHeld.ToString(), stageEvents[1].EventType);
        Assert.Equal("RecoveryInFlight", stageEvents[1].ErrorCode);
        Assert.DoesNotContain(stageEvents, x => x.EventType ==
            StageEventType.Completed.ToString());
        var decision = await verify.ControlledRecoveryDecisions.SingleAsync(x =>
            x.RunId == runId);
        Assert.Equal("test:SystemAdministrator", decision.ActorId);
        Assert.Equal(ControlledRecoveryAction.Scrap.ToString(), decision.Decision);
        Assert.Empty(await verify.WholeTrayCompletions.Where(x =>
            x.RunId == runId).ToListAsync());
    }
}
