using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gaode.Application.Station01;
using Gaode.Domain.Station01;
using Gaode.Integration.Tests.Support;
using Xunit;

namespace Gaode.Integration.Tests.Station01;

public sealed partial class NormalPauseIntegrationTests
{
    internal static async Task<string> RunPauseAsync()
    {
        var inputs = Path.Combine(Station01HostFixture.FindWorkspace(), "specs", "008-recipe-driven-inspection", "fixtures");
        await using var rig = await VirtualLoopTestRig.CreateAsync(useCurrentRecipe: true, recipeCatalogPath: Path.Combine(inputs, "recipes.json"),
            workerManifestPath: Path.Combine(inputs, "worker-manifest.json"));
        var (request, response, receipt) = await rig.StartAsync();
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var path = $"/api/v1/station01/runs/{receipt.RunId:D}";
        using var equipment = rig.Host.ClientForRole("EquipmentEngineer");
        var end = DateTimeOffset.UtcNow.AddSeconds(60);
        RunApiSnapshot? current = null;
        while (DateTimeOffset.UtcNow < end)
        {
            current = await rig.Host.Client.GetFromJsonAsync<RunApiSnapshot>(path);
            if (current?.State is RunState.Blocked or RunState.RecoveryRequired) break;
            using var mediaResponse = await rig.Host.Client.GetAsync(path + "/media");
            if (mediaResponse.StatusCode == HttpStatusCode.NotFound) { await Task.Delay(100); continue; }
            mediaResponse.EnsureSuccessStatusCode();
            var media = await mediaResponse.Content.ReadFromJsonAsync<JsonElement>();
            if (current?.State == RunState.Running3D && media.GetProperty("items").EnumerateArray().Any(x =>
                x.GetProperty("role").GetString() == "ThreeD" && x.GetProperty("readiness").GetString() == "Ready")) break;
            await Task.Delay(100);
        }
        Assert.Equal(RunState.Running3D, current?.State);
        HttpResponseMessage? pausedResponse = null;
        for (var attempt = 0; attempt < 4; attempt++)
        {
            current = await rig.Host.Client.GetFromJsonAsync<RunApiSnapshot>(path);
            pausedResponse = await equipment.PostAsJsonAsync(path + "/pause", new
            { requestId = Guid.NewGuid().ToString("N"), expectedRevision = current!.ObservedRevision,
                reason = "Actual Test 3D capture saved; pause at completed acquisition boundary" });
            if (pausedResponse.StatusCode == HttpStatusCode.Accepted) break;
            Assert.Equal(HttpStatusCode.Conflict, pausedResponse.StatusCode);
            await Task.Delay(50);
        }
        Assert.Equal(HttpStatusCode.Accepted, pausedResponse?.StatusCode);
        var paused = await rig.WaitAsync(receipt.RunId, TimeSpan.FromSeconds(25), RunState.Paused, RunState.Blocked);
        Assert.Equal(RunState.Paused, paused.State);
        var checkResponse = await equipment.PostAsJsonAsync(path + "/recovery-checks", new
        { requestId = Guid.NewGuid().ToString("N"), expectedRevision = paused.ObservedRevision,
            sameTray = true, loadingUnchanged = true, snapshotStillApplicable = true });
        Assert.Equal(HttpStatusCode.Accepted, checkResponse.StatusCode);
        var check = (await checkResponse.Content.ReadFromJsonAsync<RecoveryCheckReceipt>())!;
        var continued = await equipment.PostAsJsonAsync(path + "/continue", new
        { requestId = Guid.NewGuid().ToString("N"), expectedRevision = paused.ObservedRevision, checkId = check.CheckId });
        Assert.Equal(HttpStatusCode.Accepted, continued.StatusCode);
        var ready = await rig.WaitAsync(receipt.RunId, TimeSpan.FromSeconds(180),
            RunState.AwaitingManualRemoval, RunState.Blocked, RunState.RecoveryRequired);
        Assert.Equal(RunState.AwaitingManualRemoval, ready.State);
        Assert.Equal(receipt.RunId, ready.RunId);
        var removal = await rig.Host.Client.PostAsJsonAsync(path + "/manual-removal-confirmations", new
        { requestId = Guid.NewGuid().ToString("N"), expectedRevision = ready.ObservedRevision,
            reason = "Backend API Test confirmation; this is not formal WPF evidence" });
        Assert.Equal(HttpStatusCode.Accepted, removal.StatusCode);
        var final = await rig.WaitAsync(receipt.RunId, TimeSpan.FromSeconds(15), RunState.Completed);
        Assert.Equal(TerminalOutcome.Completed, final.FinalOutcome);
        Assert.Single(final.Events, x => x.StartsWith("NormalPaused:"));
        Assert.Single(final.Events, x => x.StartsWith("NormalContinued:"));
        var finalMedia = await rig.Host.Client.GetFromJsonAsync<JsonElement>(path + "/media");
        Assert.Single(finalMedia.GetProperty("items").EnumerateArray(), x => x.GetProperty("role").GetString() == "ThreeD");
        Assert.Single(finalMedia.GetProperty("items").EnumerateArray(), x => x.GetProperty("role").GetString() == "F");
        return await rig.SaveEvidenceAsync("normal-pause-final",request,response,receipt,
            new {final.State,final.FinalOutcome});
    }
}
