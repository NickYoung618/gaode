using System.Net;
using System.Net.Http.Json;
using Gaode.Application.Station01;
using Gaode.Domain.Station01;
using Gaode.Host.Api;
using Gaode.Infrastructure.Persistence;
using Gaode.Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Gaode.Integration.Tests.Station01;

public sealed class FinalUnloadCompletionIntegrationTests
{
    // SRC-13a admission component. Successful confirmation/replay and final references
    // are exercised once by the formal 010 FullRun harness (SRC-13a/b), not recreated here.
    [Fact]
    [Trait("EvidenceLevel", "Component")]
    public async Task AuthenticatedManualConfirmationIsTheOnlyStepThatCreatesFinalUnloadCompletion()
    {
        await using var fixture = await Station01HostFixture.CreateAsync();
        var runId = Guid.NewGuid();
        var coordinator = fixture.Host.Services.GetRequiredService<Station01Coordinator>();
        Assert.True(coordinator.TryRegister(new(runId, "admission-only", "test:Operator", RunState.RunningF,
            1, 0, TerminalOutcome.None, false, ActionState.NotRequested, CaptureState.NotRequested,
            AlgorithmState.NotRequested, SaveState.NotQueued, HandoffState.NotReady, null, null, null, [])));
        // This registered component state has no executing start task: the early request is
        // deterministic, not a race against a rapidly completing process.
        var uri = $"/api/v1/station01/runs/{runId:D}/manual-removal-confirmations";
        var early = await fixture.Client.PostAsJsonAsync(uri,
            new ManualTrayRemovalApiRequest("too-early", 1, "No unlock has been observed"));
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        Assert.Contains("ObservedUnlockedRequired", await early.Content.ReadAsStringAsync());
        Assert.Equal(RunState.RunningF, coordinator.Query(runId)!.State);
        await using var db = new Station01DbContext(fixture.Host.Services.GetRequiredService<DbContextOptions<Station01DbContext>>());
        Assert.Empty(await db.WholeTrayCompletions.Where(x => x.RunId == runId).ToListAsync());
        Assert.Empty(await db.ComponentEvidenceMatrices.Where(x => x.RunId == runId).ToListAsync());
        Assert.Empty(await db.StageEvents.Where(x => x.RunId == runId).ToListAsync());
    }
}
