using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Domain.Station01;
using Gaode.Application.Workflow;
using Gaode.Integration.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Gaode.Integration.Tests.Station01;

public sealed partial class RecipeMultiObjectIntegrationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task TwoGroupsKeepCompletedMembersAndRunIndependentTargetsThroughRealPorts(bool mixedResults, bool fourFaceModel)
    {
        var root = Path.Combine(Station01HostFixture.FindWorkspace(), "specs", "008-recipe-driven-inspection", "fixtures");
        if (fourFaceModel) root = Path.Combine(root, "usr-e-1.0.2");
        var catalogPath = Path.Combine(root, fourFaceModel ? "recipes-group-a-e.json" : "recipes-group-f.json");
        var catalog = RecipeBindingTestSupport.Catalog(catalogPath);
        Assert.True(Gaode.Application.Recipes.RecipeAdmission.Evaluate(Assert.Single(catalog.GetSnapshot().Definitions), ["P01"], "Test").Eligible);
        await using var rig = await VirtualLoopTestRig.CreateAsync(useCurrentRecipe: true,
            recipeCatalogPath: catalogPath, occupiedSlots: ["P01", "P03"],
            imageManifestPath: Path.Combine(root, fourFaceModel ? "media-manifest-group-a-e.json" : "media-manifest-group-f.json"),
            workerManifestPath: Path.Combine(root, fourFaceModel ? "worker-manifest-group-a-e.json" :
                mixedResults ? "worker-manifest-group-f-mixed.json" : "worker-manifest-group-f.json"));
        var (request, accepted, receipt) = await rig.StartAsync();
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        var wait = TimeSpan.FromMinutes(6);
        if (fourFaceModel)
        {
            var prepared = await rig.WaitAsync(receipt.RunId, TimeSpan.FromMinutes(2),
                RunState.Detection, RunState.Blocked, RunState.RecoveryRequired);
            if (prepared.State == RunState.Detection)
            {
                var committed = await rig.Host.Host.Services.GetRequiredService<ITraceQuery>()
                    .GetWritesAsync(receipt.RunId, CancellationToken.None);
                var frozen = Assert.Single(committed, x => x.PayloadJson.Contains("\"kind\":\"RecipeExecutionDeadlinesFrozen\""));
                using var deadline = JsonDocument.Parse(frozen.PayloadJson);
                // Observe the application's existing frozen deadline; do not change it.
                // The extra 30s lets the harness observe a terminal timeout, not dispatch work.
                wait = deadline.RootElement.GetProperty("routeDeadlines").GetProperty("sortingDeadlineUtc")
                    .GetDateTimeOffset() - DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30);
                Assert.True(wait > TimeSpan.Zero);
            }
        }
        RunApiSnapshot final;
        try
        {
            final = await rig.WaitAsync(receipt.RunId, wait,
                RunState.AwaitingManualRemoval, RunState.Blocked, RunState.RecoveryRequired);
        }
        catch (TimeoutException)
        {
            await rig.SaveEvidenceAsync("group-harness-timeout", request, accepted, receipt,
                new { observationLimit = wait, scope = "BackendApiNotWpf" });
            throw;
        }
        var algorithm = rig.Host.Host.Services.GetRequiredService<IAlgorithmPort>();
        var folder = await rig.SaveEvidenceAsync(fourFaceModel ? "group-a-e" : mixedResults ? "group-f-mixed" : "group-f", request, accepted, receipt,
            new { final.State, final.ErrorCode, heightCalls = algorithm.CallCount(AlgorithmRole.Height),
                fCalls = algorithm.CallCount(AlgorithmRole.FDecode), detectionCalls = algorithm.CallCount(AlgorithmRole.Detection) });
        Assert.Equal(RunState.AwaitingManualRemoval, final.State);
        Assert.Equal(1, algorithm.CallCount(AlgorithmRole.Height));
        Assert.Equal(1, algorithm.CallCount(AlgorithmRole.FDecode));
        Assert.Equal(fourFaceModel ? 42 : 18, algorithm.CallCount(AlgorithmRole.Detection));
        Assert.Equal(fourFaceModel ? 2 : 0, algorithm.CallCount(AlgorithmRole.EDecode));
        using var events = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(folder, "stage-events.json")));
        var payloads = events.RootElement.EnumerateArray().Select(e => e.GetProperty("PayloadJson").GetString()!)
            .Select(json => JsonDocument.Parse(json)).ToArray();
        try
        {
            var flips = payloads.Where(p => p.RootElement.TryGetProperty("kind", out var k) && k.GetString() == "FaceEstablished").ToArray();
            Assert.Equal(fourFaceModel ? 6 : 2, flips.Length);
            Assert.All(flips, p => Assert.EndsWith(":M01", p.RootElement.GetProperty("entity").GetString()));
            var captures = payloads.Where(p => p.RootElement.TryGetProperty("stepSequence", out _) && p.RootElement.TryGetProperty("RelativeKey", out _)).ToArray();
            Assert.Equal(fourFaceModel ? 30 : 12, captures.Length);
            Assert.Equal(fourFaceModel ? 8 : 4, captures.Select(p => p.RootElement.GetProperty("objectId").GetString()).Distinct().Count());
            Assert.All(captures.Where(p => p.RootElement.GetProperty("objectId").GetString()!.EndsWith(":M02")),
                p => Assert.Equal(1, p.RootElement.GetProperty("localFace").GetInt32()));
            var decisions = payloads.Where(p => p.RootElement.TryGetProperty("kind", out var k) &&
                k.GetString() == "DetectionUnitDecision").SelectMany(p =>
                p.RootElement.GetProperty("objects").EnumerateArray()).ToArray();
            Assert.Equal(fourFaceModel ? 8 : 4, decisions.Length);
            Assert.Equal(mixedResults ? 1 : 0, decisions.Count(x => x.GetProperty("Disposition").GetString() == "NG"));
            Assert.Equal(fourFaceModel ? 8 : mixedResults ? 3 : 4, decisions.Count(x => x.GetProperty("Disposition").GetString() == "OK"));
            if (mixedResults)
            {
                Assert.EndsWith(":G:P01:M01", Assert.Single(decisions, x =>
                    x.GetProperty("Disposition").GetString() == "NG").GetProperty("ObjectId").GetString());
                var facts = await File.ReadAllTextAsync(Path.Combine(folder, "writes.json"));
                Assert.Contains("Pending", facts);
            }
        }
        finally { foreach (var payload in payloads) payload.Dispose(); }
        var queried = await rig.Host.Client.GetFromJsonAsync<RunApiSnapshot>($"/api/v1/station01/runs/{receipt.RunId:D}");
        Assert.Equal(2, queried!.Results.Count(x => x.Kind == "Group"));
        Assert.Equal(fourFaceModel ? 8 : 4, queried.Results.Count(x => x.Kind == "Member"));
        Assert.Equal(fourFaceModel ? 14 : 6, queried.Results.Count(x => x.Kind == "Face"));
        if (mixedResults)
        {
            Assert.Contains(queried.Results, x => x.Kind == "Face" && x.Disposition == "Pending");
            Assert.Contains(queried.Results, x => x.Kind == "Member" && x.Disposition == "NG");
        }
        // The branch obligation ends at its committed component results. Authorized
        // final removal is covered by V05; do not add a third complete tray run.

    }

    internal static Task<string> RunAssemblyAsync(bool missingCode, bool workerError, bool ngParts) =>
        AssemblyComponentExecution.RunAsync(missingCode, workerError, ngParts);

    [Fact]
    public Task FourFaceGroupsUseSourceCompositionAndDistinctETargets() =>
        TwoGroupsKeepCompletedMembersAndRunIndependentTargetsThroughRealPorts(false, true);

    [Fact]
    public Task EWorkerFailureLeavesIssueAndContinuesWholeAssembly() =>
        RunAssemblyAsync(false, true, false);

}
