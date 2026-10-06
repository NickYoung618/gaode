using System.Net;
using System.Net.Http.Json;
using Gaode.Application.Ports;
using Gaode.Application.Workflow;
using Gaode.Domain.Station01;
using Gaode.Host.Api;
using Gaode.Infrastructure.Persistence;
using Gaode.Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Gaode.Integration.Tests.Api;

public sealed class VirtualManualCompletionGateTests
{
    [Fact]
    public async Task Current007ConfirmationRequiresAuthorizationUnlockRevisionAndCommittedFinal()
    {
        var injectedCommits = 0;
        await using var rig = await VirtualLoopTestRig.CreateAsync(services =>
        {
            services.RemoveAll<IWholeTrayCompletionStore>();
            services.AddSingleton<IWholeTrayCompletionStore>(sp => new WholeTrayCompletionStore(
                sp.GetRequiredService<DbContextOptions<Station01DbContext>>(), TimeProvider.System,
                (phase, _) =>
                {
                    if (phase == "ManualAndFinal" && Interlocked.Increment(ref injectedCommits) == 1)
                        return Task.FromException(new DbUpdateException("InjectedManualFinalCommitFailure"));
                    return Task.CompletedTask;
                }));
        }, useCurrentRecipe: true);
        var (request, response, receipt) = await rig.StartAsync();
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var uri = $"/api/v1/station01/runs/{receipt.RunId:D}/manual-removal-confirmations";
        var before = (await rig.Host.Client.GetFromJsonAsync<RunApiSnapshot>(
            $"/api/v1/station01/runs/{receipt.RunId:D}"))!;
        var early = await rig.Host.Client.PostAsJsonAsync(uri,
            new ManualTrayRemovalApiRequest("early", before.ObservedRevision, "Test early confirmation"));

        var awaiting = await rig.WaitAsync(receipt.RunId, TimeSpan.FromSeconds(125),
            RunState.AwaitingManualRemoval);
        var valid = new ManualTrayRemovalApiRequest("t023-confirm-" + receipt.RunId.ToString("N"),
            awaiting.ObservedRevision, "Controlled Test client, no human tray removal");
        using var anonymous = new HttpClient(rig.Host.Host.Server.CreateHandler())
        { BaseAddress = rig.Host.Client.BaseAddress };
        var noToken = await anonymous.PostAsJsonAsync(uri, valid);
        using var readOnly = rig.Host.ClientForRole("ProcessEngineer");
        var forbidden = await readOnly.PostAsJsonAsync(uri, valid);
        var wrongRevision = await rig.Host.Client.PostAsJsonAsync(uri,
            valid with { ExpectedRevision = valid.ExpectedRevision + 1 });
        var failedCommit = await rig.Host.Client.PostAsJsonAsync(uri, valid);
        var afterFailedCommit = await rig.Host.Client.GetFromJsonAsync<Station01RunEvidenceApi>(
            $"/api/v1/station01/runs/{receipt.RunId:D}/evidence");
        Assert.NotNull(afterFailedCommit);
        Assert.Null(afterFailedCommit.FinalSourceMatrix);
        Assert.Equal("AwaitingFinalUnloadCompletion", afterFailedCommit.FinalResult);
        var committed = await rig.Host.Client.PostAsJsonAsync(uri, valid);
        var completed = await rig.WaitAsync(receipt.RunId, TimeSpan.FromSeconds(10), RunState.Completed);
        var replay = await rig.Host.Client.PostAsJsonAsync(uri, valid);
        var evidence = (await rig.Host.Client.GetFromJsonAsync<Station01RunEvidenceApi>(
            $"/api/v1/station01/runs/{receipt.RunId:D}/evidence"))!;
        var options = rig.Host.Host.Services.GetRequiredService<DbContextOptions<Station01DbContext>>();
        await using var db = new Station01DbContext(options);
        var events = await db.StageEvents.AsNoTracking().Where(x => x.RunId == receipt.RunId).ToListAsync();
        await rig.SaveEvidenceAsync("manual-confirm-gates", request, response, receipt,
            new { early = await ApiFact(early), noToken = await ApiFact(noToken),
                forbidden = await ApiFact(forbidden), wrongRevision = await ApiFact(wrongRevision),
                failedCommit = await ApiFact(failedCommit), committed = await ApiFact(committed),
                replay = await ApiFact(replay), injectedCommits, completed.State,
                evidence.FinalResult, manualActor = evidence.FinalSourceMatrix?.Components.Single(x =>
                    x.Component == ComponentKind.ManualActor).Source.ToString(),
                disposition = "OnlyCommittedTestConfirmationIsFinal" });

        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, noToken.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, wrongRevision.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failedCommit.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, committed.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, replay.StatusCode);
        Assert.Contains("\"replay\":true", await replay.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal("FinalUnloadCompletion", evidence.FinalResult);
        Assert.Equal(ComponentEvidenceSource.Test, evidence.FinalSourceMatrix!.Components.Single(x =>
            x.Component == ComponentKind.ManualActor).Source);
        Assert.Single(events, x => x.EventType == StageEventType.ManualTrayRemovalConfirmed.ToString());
        Assert.Single(events, x => x.EventType == StageEventType.FinalUnloadCompleted.ToString());
        Assert.Equal(2, await db.ComponentEvidenceMatrices.AsNoTracking().CountAsync(x =>
            x.RunId == receipt.RunId));
    }

    private static async Task<object> ApiFact(HttpResponseMessage response) => new
    { status = (int)response.StatusCode, body = await response.Content.ReadAsStringAsync() };
}
