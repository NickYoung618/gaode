using System.Net;
using System.Net.Http.Json;
using Gaode.Application.Station01;
using Gaode.Domain.Station01;
using Gaode.Integration.Tests.Support;
using Xunit;

namespace Gaode.Integration.Tests.Api;

public sealed class PublicContractSmokeTests
{
    [Fact]
    public async Task PublicConfigValidationIsReadOnlyAndUsesProcessEngineerPolicy()
    {
        await using var fixture = await Station01HostFixture.CreateAsync();
        using var client = fixture.ClientForRole("ProcessEngineer");
        var response = await client.PostAsJsonAsync("/api/v1/station01/public-config/validate",
            new
            {
                publicConfigRef = new { id = "s01-public-dev", version = "1.0.0" },
                budgetRef = new { id = "s01-budget-dev", version = "3.0.0" },
                simulationRef = new { id = "s01-sim-normal", version = "3.0.0" }
            });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("blockingControlErrors", body, StringComparison.Ordinal);
        Assert.Equal(0, fixture.Plc.StartCommands);
    }

    [Fact]
    public async Task PauseAndRecoveryCheckRequireExpectedRevisionAndReturnStructuredReceipts()
    {
        await using var fixture = await Station01HostFixture.CreateAsync();
        var request = new StartPublicRequest(Guid.NewGuid().ToString("N"), StartRunContextJson.Create(),
            new("s01-public-dev", "1.0.0"), new("s01-budget-dev", "3.0.0"),
            new("s01-sim-normal", "3.0.0"));
        var start = await fixture.Client.PostAsJsonAsync("/api/v1/station01/runs", request);
        var receipt = (await start.Content.ReadFromJsonAsync<StartReceipt>())!;
        RunApiSnapshot? snapshot = null;
        var until = DateTimeOffset.UtcNow.AddSeconds(8);
        while (DateTimeOffset.UtcNow < until)
        {
            snapshot = await fixture.Client.GetFromJsonAsync<RunApiSnapshot>(receipt.StatusUrl);
            if (snapshot?.State == RunState.WaitingClamp) break;
            await Task.Delay(20);
        }
        Assert.NotNull(snapshot);
        Assert.Contains(RunState.WaitingClamp.ToString(), snapshot!.Events);
        HttpResponseMessage? pause = null;
        for (var attempt = 1; attempt <= 4; attempt++)
        {
            snapshot = await fixture.Client.GetFromJsonAsync<RunApiSnapshot>(receipt.StatusUrl);
            pause = await fixture.Client.PostAsJsonAsync($"/api/v1/station01/runs/{receipt.RunId}/pause",
                new { requestId = Guid.NewGuid().ToString("N"),
                    expectedRevision = snapshot!.ObservedRevision, reason = "测试暂停" });
            if (pause.StatusCode == HttpStatusCode.Accepted) break;
            Assert.Equal(HttpStatusCode.Conflict, pause.StatusCode);
            pause.Dispose();
            pause = null;
            await Task.Delay(20);
        }
        Assert.NotNull(pause);
        Assert.Equal(HttpStatusCode.Accepted, pause!.StatusCode);
        var paused = await fixture.Client.GetFromJsonAsync<RunApiSnapshot>(receipt.StatusUrl);
        var pauseDeadline = DateTimeOffset.UtcNow.AddSeconds(12);
        while (DateTimeOffset.UtcNow < pauseDeadline && paused?.State != RunState.Paused)
        {
            await Task.Delay(25);
            paused = await fixture.Client.GetFromJsonAsync<RunApiSnapshot>(receipt.StatusUrl);
        }
        Assert.Equal(RunState.Paused, paused?.State);

        using var equipment = fixture.ClientForRole("EquipmentEngineer");
        var check = await equipment.PostAsJsonAsync($"/api/v1/station01/runs/{receipt.RunId}/recovery-checks",
            new { requestId = Guid.NewGuid().ToString("N"), expectedRevision = paused!.ObservedRevision,
                sameTray = true, loadingUnchanged = true, snapshotStillApplicable = true,
                evidenceRefs = Array.Empty<string>(), reason = "测试核对" });
        Assert.Equal(HttpStatusCode.Accepted, check.StatusCode);
        Assert.Contains("Accepted", await check.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }
}
