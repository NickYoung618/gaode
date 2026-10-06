using System.Text.Json;
using Gaode.Application.Recipes;
using Gaode.Domain.Configuration;
using Gaode.Application.Ports;
using Gaode.Application.Station01;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Gaode.Contracts.Tests.Support;

// Actual SQLite fixture wiring. Only the business query contract leaves this helper.
internal sealed class HandoffFixture : IAsyncDisposable
{
    private readonly SqliteConnection connection;
    public Guid RunId { get; }
    public Guid TrayId { get; }
    public PublicPreparationHandoffV2 Handoff { get; }
    public IStageHandoffQuery Query { get; }
    public SemanticHandoffInputs Inputs { get; private set; } = null!;
    public PublicConfiguration Motion { get; private set; } = null!;

    private HandoffFixture(SqliteConnection connection, Guid runId, Guid trayId,
        PublicPreparationHandoffV2 handoff, IStageHandoffQuery query)
    {
        this.connection = connection;
        RunId = runId;
        TrayId = trayId;
        Handoff = handoff;
        Query = query;
    }

    public static async Task<HandoffFixture> CreateAsync(bool includeWrite = true, string? invalidFSource = null,
        string? missingMedia = null)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<Station01DbContext>().UseSqlite(connection).Options;
        await using var db = new Station01DbContext(options);
        await db.Database.MigrateAsync();
        var runId = Guid.NewGuid();
        var trayId = Guid.NewGuid();
        var handoffId = Guid.NewGuid();
        var writeId = Guid.NewGuid();
        var plan = Gaode.Contracts.Tests.Recipes.Recipe011Data.Plan(trayId, 1, 1, 3);
        var revision = RecipePlanRevision.Compute(plan);
        var heightCall = Guid.NewGuid();
        var identity = new WorkflowIdentity(runId, trayId, Guid.NewGuid().ToString("D"),
            Guid.NewGuid().ToString("D"), "request-1", "scenario-A", ["P01", "P03"],
            DateTimeOffset.UtcNow, "operator-1", RunPurpose.Test,
            "public-1", "budget-1", "simulation-1");
        var handoff = new PublicPreparationHandoffV2(PublicPreparationHandoffV2.CurrentSchemaVersion,
            handoffId, identity, "points-1", "capabilities-1", ["media://3d/1"],
            ["media://f/1"], [$"algorithm-call://{heightCall:D}"], "F-001",
            "plan://1", revision, "recipe-binding://unit-1", ComponentEvidenceSource.Simulated,
            "Derived", ["evidence://handoff/1"], writeId, 1, DateTimeOffset.UtcNow,
            "");
        handoff = handoff with
        {
            PayloadDigest = PublicPreparationHandoffV2.ComputePayloadDigest(handoff)
        };
        var inputs = new SemanticHandoffInputs(handoff, plan, 11);
        handoff = inputs.Handoff;
        if (missingMedia is not null)
            inputs.Writes.RemoveAll(w => w.Kind == WriteKind.Media &&
                (JsonSerializer.Deserialize<MediaRef>(w.PayloadJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!
                    .CaptureId == inputs.Observation.CaptureId) == (missingMedia == "3D"));
        var fWrite = inputs.Writes.Single(w => w.Kind == WriteKind.AlgorithmFact && JsonSerializer.Deserialize<AlgorithmFactPayload>(w.PayloadJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.RawResultJson == "[]");
        if (invalidFSource == "missing-or-uncommitted") inputs.Writes.Remove(fWrite);
        if (invalidFSource is "wrongCall" or "unknownOrigin")
        {
            var fact = JsonSerializer.Deserialize<AlgorithmFactPayload>(fWrite.PayloadJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            fact = invalidFSource == "wrongCall" ? fact with { CallId = Guid.NewGuid() } : fact with { Origin = ComponentExecutionOrigin.Unknown };
            var payload = JsonSerializer.Serialize(fact, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            inputs.Writes[inputs.Writes.IndexOf(fWrite)] = fWrite with { PayloadJson = payload,
                PayloadDigest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload))) };
        }
        foreach (var write in inputs.Writes)
            db.Writes.Add(new WriteEntity { WriteId = write.WriteId, RunId = runId, Revision = write.Revision,
                Kind = write.Kind.ToString(), PayloadJson = write.PayloadJson, PayloadDigest = write.PayloadDigest, CommittedUtc = DateTimeOffset.UtcNow });
        var json = JsonSerializer.Serialize(handoff, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        db.Runs.Add(new RunEntity { RunId = runId, RequestId = "request-1", SubjectId = "operator-1", CreatedUtc = DateTimeOffset.UtcNow });
        if (includeWrite)
            db.Writes.Add(new WriteEntity { WriteId = writeId, RunId = runId, Revision = handoff.CommittedRevision,
                Kind = "Complete", PayloadJson = "{}", PayloadDigest = handoff.PayloadDigest,
                CommittedUtc = DateTimeOffset.UtcNow });
        if (includeWrite)
            db.PublicPreparationHandoffsV2.Add(new PublicPreparationHandoffV2Entity
            {
                HandoffId = handoffId, RunId = runId, TrayId = trayId, WriteId = writeId,
                Revision = handoff.CommittedRevision, PayloadJson = json, PayloadDigest = handoff.PayloadDigest,
                PersistedUtc = handoff.PersistedAt
            });
        await db.SaveChangesAsync();
        return new HandoffFixture(connection, runId, trayId, handoff,
            new TraceQuery(options, TimeProvider.System, 5000)) {
            Inputs = inputs,
            Motion = Gaode.Contracts.Tests.Recipes.Recipe011Data.Motion()
        };
    }

    public async ValueTask DisposeAsync() => await connection.DisposeAsync();
}
