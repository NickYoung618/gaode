using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Application.Station01;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Persistence;
using Gaode.Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Gaode.Integration.Tests.Station01;

public sealed class PublicPreparationHandoffV2IntegrationTests
{
    [Fact]
    public async Task DetectionCanOnlyStartFromCommittedMatchingV2Handoff()
    {
        await using var fixture = await Station01HostFixture.CreateAsync(recipeFixtureCode: "RC:R-S1-A-CAP:0.4.0-review");
        var query = fixture.Host.Services.GetRequiredService<IStageHandoffQuery>();
        var consumer = new PublicPreparationHandoffV2Consumer(query, fixture.Host.Services.GetRequiredService<ITraceQuery>());
        var runId = Guid.NewGuid();
        var trayId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        const string planRevision = "plan-revision-003-integration";

        // StorageComponent: the absence of a committed handoff and current receipt are independent gates.
        // A placeholder request is not a positive example of the shared execution contract.
        Assert.Null(await query.GetCommittedV2Async(runId, default));
        var beforeCommit = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.CreateDetectionRequestAsync(runId, trayId, planRevision, operationId, 17,
                DateTimeOffset.UtcNow.AddSeconds(120), "before-commit"));
        Assert.Equal("CommittedHandoffV2Required", beforeCommit.Message);

        var writeId = Guid.NewGuid();
        var handoffId = Guid.NewGuid();
        var persistedAt = DateTimeOffset.UtcNow;
        var identity = new WorkflowIdentity(runId, trayId,
            "10000000-0000-0000-0000-000000000001",
            "20000000-0000-0000-0000-000000000001",
            "request-v2", "S1", ["P01", "P02"], persistedAt,
            "test:Operator", RunPurpose.Test, "public-1", "budget-1", "simulation-1");
        var handoff = new PublicPreparationHandoffV2(
            PublicPreparationHandoffV2.CurrentSchemaVersion, handoffId, identity,
            "points-1", "capability-digest-1", ["media://3d/1"], ["media://f/1"],
            ["result://height/1", "result://f/1"], "TEST-TRAY-0001",
            $"recipe-plan://{runId:D}/{planRevision}", planRevision,
            "recipe-binding://binding-1", ComponentEvidenceSource.Test, "VerifiedTestEvidence",
            ["write://start", "write://3d", "write://f", "write://recipe-bind"],
            writeId, 1, persistedAt, "");
        handoff = handoff with
        {
            PayloadDigest = PublicPreparationHandoffV2.ComputePayloadDigest(handoff)
        };
        var payloadDigest = handoff.PayloadDigest;
        var payloadJson = JsonSerializer.Serialize(handoff,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var options = fixture.Host.Services.GetRequiredService<DbContextOptions<Station01DbContext>>();
        await using (var db = new Station01DbContext(options))
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            db.Runs.Add(new RunEntity
            {
                RunId = runId,
                RequestId = identity.RequestId,
                SubjectId = identity.ActorId,
                ContextJson = StartRunContextJson.Create("S1", trayId, identity.OccupiedSlots),
                State = RunState.SavingHandoff,
                Terminal = TerminalOutcome.None,
                Revision = 1,
                CreatedUtc = persistedAt
            });
            db.Writes.Add(new WriteEntity
            {
                WriteId = writeId,
                RunId = runId,
                Revision = 1,
                Kind = "HandoffV2",
                PayloadJson = payloadJson,
                PayloadDigest = payloadDigest,
                CommittedUtc = persistedAt
            });
            db.PublicPreparationHandoffsV2.Add(new PublicPreparationHandoffV2Entity
            {
                HandoffId = handoffId,
                RunId = runId,
                TrayId = trayId,
                WriteId = writeId,
                Revision = 1,
                PayloadJson = payloadJson,
                PayloadDigest = payloadDigest,
                PersistedUtc = persistedAt
            });
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        var committed = await query.GetCommittedV2Async(runId, default);
        Assert.NotNull(committed);
        Assert.True(committed.IsVerified);
        // A manually persisted or historical handoff is insufficient: the current
        // binding must first receive all actual commits within its frozen window.
        var withoutReceipt = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            consumer.CreateDetectionRequestAsync(runId, trayId, planRevision,
                operationId, 17, DateTimeOffset.UtcNow.AddSeconds(120), "committed-without-receipt"));
        Assert.Equal("CurrentRecipeApplicationReceiptRequired", withoutReceipt.Message);

        await using var verification = new Station01DbContext(options);
        Assert.Equal(TerminalOutcome.None,
            (await verification.Runs.AsNoTracking().SingleAsync(x => x.RunId == runId)).Terminal);
        Assert.Empty(await verification.StageEvents.Where(x => x.RunId == runId).ToArrayAsync());
        // Live committed success/actual F source is covered once by 010 V05 FullRun;
        // this deliberately inserted storage fixture cannot authorize execution.
    }
}
