using Gaode.Application.Ports;
using Gaode.Application.Workflow;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Persistence;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Gaode.Integration.Tests.Storage;

public sealed class ComponentSourceMatrixStoreTests : IAsyncLifetime
{
    private SqliteConnection connection = null!;
    private DbContextOptions<Station01DbContext> options = null!;
    private readonly Guid runId = Guid.NewGuid();
    private readonly Guid trayId = Guid.NewGuid();
    private readonly Guid stationId = Guid.NewGuid();
    private readonly Guid lineId = Guid.NewGuid();
    private const string Plan = "plan-source-matrix-1";

    public async Task InitializeAsync()
    {
        connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        options = new DbContextOptionsBuilder<Station01DbContext>().UseSqlite(connection).Options;
        await using (var db = new Station01DbContext(options))
        {
            await db.Database.MigrateAsync();
            db.Runs.Add(new RunEntity { RunId = runId, RequestId = "matrix-test",
                SubjectId = "test", State = RunState.UnloadPreparation,
                CreatedUtc = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        var events = new StageEventStore(options);
        foreach (var stage in new[] { WholeTrayWorkflowStage.Detection,
                     WholeTrayWorkflowStage.Sorting, WholeTrayWorkflowStage.UnloadPreparation })
        {
            var at = DateTimeOffset.UtcNow;
            await events.AppendAsync(new StageEventAppendRequest(Guid.NewGuid(), runId, trayId,
                stationId.ToString(), lineId.ToString(), stage, Guid.NewGuid(), 1, 3,
                StageEventType.Completed, at, stage == WholeTrayWorkflowStage.Detection
                    ? ResultSource.Simulated : ResultSource.Virtual, ResultQuality.Derived,
                null, "digest-" + stage, "{}", "complete-" + stage, Plan,
                at.AddSeconds(-1), at.AddSeconds(119)));
        }
    }

    public async Task DisposeAsync() => await connection.DisposeAsync();

    [Fact]
    public async Task MixedSourcesRoundTripAsSoftwareLoopOnlyAndNeverCollapseToReal()
    {
        var matrix = Matrix(Verified(ComponentKind.Algorithm, ComponentEvidenceSource.Simulated));
        var store = new WholeTrayCompletionStore(options);
        var saved = await store.CreateAsync(Request(matrix));
        var loaded = await store.GetByRunAsync(runId);

        Assert.Equal(EvidenceScope.SoftwareLoopOnly, saved.SourceMatrix.Scope);
        Assert.Equal(EvidenceScope.SoftwareLoopOnly, loaded!.SourceMatrix.Scope);
        Assert.Equal(ComponentEvidenceSource.Virtual, loaded.SourceMatrix.Components
            .Single(x => x.Component == ComponentKind.Plc).Source);
        Assert.Equal(ComponentEvidenceSource.Simulated, loaded.SourceMatrix.Components
            .Single(x => x.Component == ComponentKind.Algorithm).Source);
        Assert.DoesNotContain(loaded.SourceMatrix.Components, x =>
            x.Component is ComponentKind.Plc or ComponentKind.Algorithm &&
            x.Source == ComponentEvidenceSource.Real);
        await using var db = new Station01DbContext(options);
        var aggregate = await db.StageEvents.AsNoTracking().SingleAsync(e => e.EventType == "WholeTrayCompleted");
        Assert.Equal("HostDerived", aggregate.Source);
        Assert.Equal("Derived", aggregate.Quality);
    }

    [Theory]
    [InlineData(ComponentEvidenceState.Missing)]
    [InlineData(ComponentEvidenceState.Unknown)]
    [InlineData(ComponentEvidenceState.Unverifiable)]
    public async Task MissingUnknownOrUnverifiableRequiredComponentBlocksCompletion(
        ComponentEvidenceState state)
    {
        var invalidAlgorithm = new ComponentEvidence(ComponentKind.Algorithm, state, null,
            null, null, [], null, null);
        var matrix = Matrix(invalidAlgorithm);
        var store = new WholeTrayCompletionStore(options);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.CreateAsync(Request(matrix)));

        Assert.Contains("WholeTraySourceMatrixIncomplete:Algorithm", error.Message,
            StringComparison.Ordinal);
        await using var db = new Station01DbContext(options);
        Assert.Empty(await db.WholeTrayCompletions.ToListAsync());
        Assert.Empty(await db.ComponentEvidenceMatrices.ToListAsync());
        Assert.Equal(RunState.UnloadPreparation,
            (await db.Runs.SingleAsync(x => x.RunId == runId)).State);
    }

    [Fact]
    public async Task FinalMatrixKeepsReadyEvidenceImmutableAndAddsAuthenticatedHumanActor()
    {
        var readyMatrix = Matrix(Verified(ComponentKind.Algorithm,
            ComponentEvidenceSource.Simulated));
        var store = new WholeTrayCompletionStore(options);
        var ready = await store.CreateAsync(Request(readyMatrix));
        var now = DateTimeOffset.UtcNow;
        var observed = await new StageEventStore(options).AppendAsync(new StageEventAppendRequest(
            Guid.NewGuid(), runId, trayId, stationId.ToString(), lineId.ToString(),
            WholeTrayWorkflowStage.ManualRemovalAdmission, Guid.NewGuid(), 1, 3,
            StageEventType.ManualRemovalAllowed, now, ResultSource.Virtual,
            ResultQuality.Measured, null, "unlock-observed", "{}", "matrix-unlock",
            Plan, now.AddSeconds(-1), now.AddSeconds(119)));
        var manual = new ComponentEvidence(ComponentKind.ManualActor,
            ComponentEvidenceState.Verified, ComponentEvidenceSource.AuthenticatedHuman,
            "Authenticated", "host-auth/1", ["actor://test:Operator"], now,
            "sha256:manual-actor");

        var final = await store.ConfirmManualRemovalAsync(new(Guid.NewGuid(), ready.Reference,
            observed.Event.EventId, "test:Operator", now, "托盘已移除",
            "matrix-final", manual));

        await using var db = new Station01DbContext(options);
        var matrices = await db.ComponentEvidenceMatrices.AsNoTracking().Where(x =>
            x.RunId == runId).ToListAsync();
        Assert.Equal(2, matrices.Count);
        Assert.Equal(readyMatrix.MatrixId, ready.SourceMatrix.MatrixId);
        Assert.Equal(readyMatrix.MatrixId, (await store.GetByRunAsync(runId))!.SourceMatrix.MatrixId);
        var finalEntity = Assert.Single(matrices, x =>
            x.MatrixId == final.FinalSourceMatrixId);
        Assert.Equal(EvidenceMilestone.FinalUnloadCompletion.ToString(), finalEntity.Milestone);
        Assert.Equal(EvidenceScope.SoftwareLoopOnly.ToString(), finalEntity.Scope);
        var finalComponents = JsonSerializer.Deserialize<ComponentEvidence[]>(
            finalEntity.ComponentsJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(ComponentEvidenceSource.AuthenticatedHuman,
            finalComponents.Single(x => x.Component == ComponentKind.ManualActor).Source);
        var finalEvents = await db.StageEvents.AsNoTracking().Where(x => x.RunId == runId &&
            (x.EventType == StageEventType.ManualTrayRemovalConfirmed.ToString() ||
             x.EventType == StageEventType.FinalUnloadCompleted.ToString())).ToListAsync();
        Assert.Equal(2, finalEvents.Count);
        var humanFact = Assert.Single(finalEvents, e => e.EventType == "ManualTrayRemovalConfirmed");
        Assert.Equal("Real", humanFact.Source); Assert.Equal("Measured", humanFact.Quality);
        var aggregate = Assert.Single(finalEvents, e => e.EventType == "FinalUnloadCompleted");
        Assert.Equal("HostDerived", aggregate.Source); Assert.Equal("Derived", aggregate.Quality);
    }

    [Fact]
    public async Task TestManualActorRetainsActualOriginWhileFinalIsHostDerived()
    {
        var store = new WholeTrayCompletionStore(options);
        var ready = await store.CreateAsync(Request(Matrix(Verified(ComponentKind.Algorithm,
            ComponentEvidenceSource.Simulated))));
        var now = DateTimeOffset.UtcNow;
        var observed = await new StageEventStore(options).AppendAsync(new StageEventAppendRequest(
            Guid.NewGuid(), runId, trayId, stationId.ToString(), lineId.ToString(),
            WholeTrayWorkflowStage.ManualRemovalAdmission, Guid.NewGuid(), 1, 3,
            StageEventType.ManualRemovalAllowed, now, ResultSource.Virtual,
            ResultQuality.Measured, null, "unlock-observed", "{}", "test-unlock",
            Plan, now.AddSeconds(-1), now.AddSeconds(119)));
        var actor = new ComponentEvidence(ComponentKind.ManualActor,
            ComponentEvidenceState.Verified, ComponentEvidenceSource.Test,
            "Derived", "test-client/1", ["test://manual-confirmation"], now,
            "sha256:test-manual-actor");

        var final = await store.ConfirmManualRemovalAsync(new(Guid.NewGuid(), ready.Reference,
            observed.Event.EventId, "test:Operator", now, "自动模拟取盘",
            "test-final", actor));

        await using var db = new Station01DbContext(options);
        var finalEvents = await db.StageEvents.AsNoTracking().Where(x => x.RunId == runId &&
            (x.EventType == StageEventType.ManualTrayRemovalConfirmed.ToString() ||
             x.EventType == StageEventType.FinalUnloadCompleted.ToString())).ToListAsync();
        Assert.Equal(2, finalEvents.Count);
        var actorFact = Assert.Single(finalEvents, e => e.EventType == "ManualTrayRemovalConfirmed");
        Assert.Equal("Test", actorFact.Source); Assert.Equal("Derived", actorFact.Quality);
        var aggregate = Assert.Single(finalEvents, e => e.EventType == "FinalUnloadCompleted");
        Assert.Equal("HostDerived", aggregate.Source); Assert.Equal("Derived", aggregate.Quality);
        var matrix = await db.ComponentEvidenceMatrices.AsNoTracking().SingleAsync(x =>
            x.MatrixId == final.FinalSourceMatrixId);
        Assert.Equal(EvidenceScope.SoftwareLoopOnly.ToString(), matrix.Scope);
        var components = JsonSerializer.Deserialize<ComponentEvidence[]>(matrix.ComponentsJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(ComponentEvidenceSource.Test, components.Single(x =>
            x.Component == ComponentKind.ManualActor).Source);
    }

    private ComponentEvidenceMatrix Matrix(ComponentEvidence algorithm) =>
        ComponentEvidenceMatrix.Create(Guid.NewGuid(), runId, trayId, Plan,
            EvidenceMilestone.ReadyForRemoval,
            [
                Verified(ComponentKind.Host, ComponentEvidenceSource.Test),
                Verified(ComponentKind.Plc, ComponentEvidenceSource.Virtual),
                Verified(ComponentKind.Camera, ComponentEvidenceSource.Simulated),
                Verified(ComponentKind.Light, ComponentEvidenceSource.Simulated),
                algorithm,
                ComponentEvidence.NotYetRequired(ComponentKind.ManualActor)
            ]);

    private WholeTrayCompletionCreateRequest Request(ComponentEvidenceMatrix matrix) =>
        new(Guid.NewGuid(), runId, trayId, stationId, lineId, Plan, matrix,
            DateTimeOffset.UtcNow, "matrix-create-" + Guid.NewGuid().ToString("N"));

    private static ComponentEvidence Verified(ComponentKind component,
        ComponentEvidenceSource source) => new(component, ComponentEvidenceState.Verified,
        source, "Derived", "component/1", [$"evidence://{component}"],
        DateTimeOffset.UtcNow, "sha256:" + component);
}
