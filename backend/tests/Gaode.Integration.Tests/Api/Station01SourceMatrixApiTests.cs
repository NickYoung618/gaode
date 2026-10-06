using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Application.Station01;
using Gaode.Application.Workflow;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Host.Api;
using Gaode.Integration.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Gaode.Integration.Tests.Api;

public sealed class Station01SourceMatrixApiTests
{
    [Fact]
    [Trait("EvidenceLevel", "StorageComponent")]
    public async Task EvidenceEndpointReturnsComponentSourcesWithoutCollapsingMixedEvidenceToReal()
    {
        // Query-component fixture only: no running process or synthetic business continuation.
        // SRC-12 formal execution evidence is supplied by V05.
        await using var fixture = await Station01HostFixture.CreateAsync();
        var runId = Guid.NewGuid(); var trayId = Guid.NewGuid();
        var stationId = Guid.NewGuid(); var lineId = Guid.NewGuid(); const string plan = "query-component-plan";
        var identity = new WorkflowIdentity(runId, trayId, stationId.ToString(), lineId.ToString(),
            "query-component", "S1", ["P01"], DateTimeOffset.UtcNow, "test:Operator", RunPurpose.Test, "1", "1", "1");
        var coordinator = fixture.Host.Services.GetRequiredService<Station01Coordinator>();
        Assert.True(coordinator.TryRegister(new(runId, "query-component", "test:Operator", RunState.UnloadPreparation,
            1, 0, TerminalOutcome.None, false, ActionState.NotRequested, CaptureState.NotRequested,
            AlgorithmState.NotRequested, SaveState.NotQueued, HandoffState.NotReady, null, null, null, [],
            Identity: identity, PlanRevision: plan)));
        await using (var db = new Gaode.Infrastructure.Persistence.Station01DbContext(fixture.Host.Services.GetRequiredService<DbContextOptions<Gaode.Infrastructure.Persistence.Station01DbContext>>()))
        {
            db.Runs.Add(new Gaode.Infrastructure.Persistence.RunEntity { RunId = runId, RequestId = "query-component", State = RunState.UnloadPreparation,
                CreatedUtc = DateTimeOffset.UtcNow, SubjectId = "test:Operator" });
            await db.SaveChangesAsync();
        }
        var eventStore = fixture.Host.Services.GetRequiredService<IStageEventStore>();
        foreach (var stage in new[] { WholeTrayWorkflowStage.Detection,
                     WholeTrayWorkflowStage.Sorting, WholeTrayWorkflowStage.UnloadPreparation })
        {
            var now = DateTimeOffset.UtcNow;
            await eventStore.AppendAsync(new StageEventAppendRequest(Guid.NewGuid(), runId,
                identity.TrayId, identity.StationId, identity.LineId, stage, Guid.NewGuid(), 1, 9,
                StageEventType.Completed, now, stage == WholeTrayWorkflowStage.Detection
                    ? ResultSource.Simulated : ResultSource.Virtual, ResultQuality.Derived,
                null, "digest-" + stage, "{}", "api-complete-" + stage, plan,
                now.AddSeconds(-1), now.AddSeconds(119)));
        }
        var components = new[]
        {
            Verified(ComponentKind.Host, ComponentEvidenceSource.Test),
            Verified(ComponentKind.Plc, ComponentEvidenceSource.Virtual),
            Verified(ComponentKind.Camera, ComponentEvidenceSource.Simulated),
            Verified(ComponentKind.Light, ComponentEvidenceSource.Simulated),
            Verified(ComponentKind.Algorithm, ComponentEvidenceSource.Simulated),
            ComponentEvidence.NotYetRequired(ComponentKind.ManualActor)
        };
        var matrix = ComponentEvidenceMatrix.Create(Guid.NewGuid(), runId, identity.TrayId,
            plan, EvidenceMilestone.ReadyForUnlock, components);
        var completionStore = fixture.Host.Services.GetRequiredService<IWholeTrayCompletionStore>();
        var completion = await completionStore.CreateAsync(new(Guid.NewGuid(), runId,
            identity.TrayId, stationId, lineId, plan, matrix, DateTimeOffset.UtcNow,
            "api-whole-tray"));

        var evidenceResponse = await fixture.Client.GetAsync(
            $"/api/v1/station01/runs/{runId:D}/evidence");
        Assert.True(evidenceResponse.StatusCode == HttpStatusCode.OK,
            $"Evidence API returned {evidenceResponse.StatusCode}: " +
            await evidenceResponse.Content.ReadAsStringAsync());
        Assert.NotNull(evidenceResponse.Headers.ETag);
        var actualJson = await evidenceResponse.Content.ReadAsStringAsync();
        using var actual = JsonDocument.Parse(actualJson);
        var sourceRows = actual.RootElement.GetProperty("readyForRemovalSourceMatrix").GetProperty("components");
        Assert.Equal((int)ComponentEvidenceSource.Virtual, sourceRows.EnumerateArray().Single(x =>
            x.GetProperty("component").GetInt32() == (int)ComponentKind.Plc).GetProperty("source").GetInt32());
        var evidence = (await evidenceResponse.Content.ReadFromJsonAsync<Station01RunEvidenceApi>())!;

        Assert.Equal(completion.Reference.CompletionId, evidence.WholeTrayCompletionId);
        Assert.Equal(EvidenceScope.SoftwareLoopOnly.ToString(),
            evidence.ReadyForRemovalSourceMatrix!.Scope);
        Assert.Empty(evidence.ReadyForRemovalSourceMatrix.BlockedComponents);
        Assert.Equal(ComponentEvidenceSource.Virtual,
            evidence.ReadyForRemovalSourceMatrix.Components.Single(x =>
                x.Component == ComponentKind.Plc).Source);
        Assert.Equal(ComponentEvidenceSource.Simulated,
            evidence.ReadyForRemovalSourceMatrix.Components.Single(x =>
                x.Component == ComponentKind.Algorithm).Source);
        Assert.Null(evidence.FinalSourceMatrix);
        Assert.Equal("AwaitingFinalUnloadCompletion", evidence.FinalResult);
        Assert.Contains(evidence.Stages, x => x.EventType ==
            StageEventType.WholeTrayCompleted.ToString() && x.PlanRevision == plan);
        var aggregate = Assert.Single(evidence.Stages, x => x.EventType == StageEventType.WholeTrayCompleted.ToString());
        Assert.Equal("HostDerived", aggregate.Source);
        Assert.Equal("Derived", aggregate.RecordNature);
        Assert.Equal("Derived", aggregate.Quality);
        var evidenceRoot = Environment.GetEnvironmentVariable("GAODE_009_EVIDENCE_ROOT");
        Assert.False(string.IsNullOrWhiteSpace(evidenceRoot));
        Directory.CreateDirectory(evidenceRoot);
        await File.WriteAllTextAsync(Path.Combine(evidenceRoot, "component-source-api.json"), actualJson);
    }

    private static ComponentEvidence Verified(ComponentKind component,
        ComponentEvidenceSource source) => new(component, ComponentEvidenceState.Verified,
        source, "Derived", "api-test/1", [$"evidence://{component}"],
        DateTimeOffset.UtcNow, "sha256:" + component);
}
