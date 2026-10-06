using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Domain.Station01;
using Gaode.Integration.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Gaode.Integration.Tests.Station01;

public sealed partial class OrdinaryImpactRegressionTests
{
    internal static async Task<string> RunOrdinaryAsync(string slug, int calls, int flips, int sorts)
    {
        var root = Path.Combine(Station01HostFixture.FindWorkspace(), "specs", "008-recipe-driven-inspection", "fixtures");
        if (slug is "q09" or "q18") root = Path.Combine(root, "usr-e-1.0.2");
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, $"fixture-{slug}.json")));
        var f = fixture.RootElement;
        await using var rig = await VirtualLoopTestRig.CreateAsync(useCurrentRecipe: true,
            recipeCatalogPath: f.GetProperty("recipeCatalogPath").GetString(),
            occupiedSlots: f.GetProperty("occupiedSlots").EnumerateArray().Select(x => x.GetString()!).ToArray(),
            imageManifestPath: f.GetProperty("imageManifestPath").GetString(),
            workerManifestPath: f.GetProperty("workerManifestPath").GetString());
        var (request, accepted, receipt) = await rig.StartAsync();
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        var final = await rig.WaitAsync(receipt.RunId, TimeSpan.FromMinutes(6),
            RunState.AwaitingManualRemoval, RunState.Blocked, RunState.RecoveryRequired);
        var folder = await rig.SaveEvidenceAsync("impact-" + slug, request, accepted, receipt,
            new { final.State, final.ErrorCode, expectedDetectionCalls = calls, expectedFlips = flips, expectedSorts = sorts });
        Assert.Equal(RunState.AwaitingManualRemoval, final.State);
        var algorithm = rig.Host.Host.Services.GetRequiredService<IAlgorithmPort>();
        Assert.Equal(1, algorithm.CallCount(AlgorithmRole.Height));
        Assert.Equal(1, algorithm.CallCount(AlgorithmRole.FDecode));
        Assert.Equal(calls, algorithm.CallCount(AlgorithmRole.Detection));
        var events = await File.ReadAllTextAsync(Path.Combine(folder, "stage-events.json"));
        using var parsed = JsonDocument.Parse(events);
        var payloads = parsed.RootElement.EnumerateArray().Select(x => JsonDocument.Parse(x.GetProperty("PayloadJson").GetString()!)).ToArray();
        try
        {
            Assert.Equal(flips, payloads.Count(x => x.RootElement.TryGetProperty("kind", out var kind) && kind.GetString() == "FaceEstablished"));
            Assert.DoesNotContain("Rescan", events);
        }
        finally { foreach (var payload in payloads) payload.Dispose(); }
        var context = Assert.IsType<ResultContextProjection>(final.ResultContext);
        var current = Assert.Single(final.Results, x => x.Kind == context.Kind && x.Id == context.Id);
        var expectedQuality = slug == "q03-ng" ? "NG" : slug == "q03-pending" ? "Pending" : "OK";
        Assert.Equal(expectedQuality, current.Disposition);
        Assert.Equal("Complete", current.Completeness);
        Assert.NotEmpty(current.Inspections);
        Assert.All(current.Inspections.Where(x => x.BusinessCamera is "A" or "B" or "C" or "D"), item => {
            Assert.NotNull(item.CallId); Assert.NotNull(item.CaptureId); Assert.NotEmpty(item.MediaIds);
            Assert.Equal("NotProvided", item.DetailAvailability.Confidence); Assert.Null(item.Confidence);
        });
        using var op = rig.Host.ClientForRole("Operator");
        var remove = await op.PostAsJsonAsync($"/api/v1/station01/runs/{receipt.RunId:D}/manual-removal-confirmations",
            new { requestId = "backend-impact-remove-" + slug, expectedRevision = final.ObservedRevision, reason = "Backend test confirms unlocked virtual tray removal; not page evidence" });
        Assert.Equal(HttpStatusCode.Accepted, remove.StatusCode);
        var completed = await rig.WaitAsync(receipt.RunId, TimeSpan.FromSeconds(15), RunState.Completed);
        Assert.Equal(expectedQuality, Assert.Single(completed.Results, x => x.Kind == context.Kind && x.Id == context.Id).Disposition);
        Assert.Equal(TerminalOutcome.Completed, completed.FinalOutcome);
        await rig.SaveEvidenceAsync("impact-final-" + slug, request, accepted, receipt, new { completed.State, expectedQuality, scope = "BackendApiNotWpf" });
        return folder;
    }
}
