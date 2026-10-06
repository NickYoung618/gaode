using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gaode.Application.Station01;
using Gaode.Integration.Tests.Support;
using Gaode.Domain.Station01;
using Xunit;

namespace Gaode.Integration.Tests.Station01;

public sealed class PublicTrayFlow016Tests
{
    [Theory]
    [InlineData("mixed")]
    [InlineData("empty")]
    [InlineData("intervention")]
    [InlineData("all-abnormal")]
    public async Task RealApiWorkerTcpSqliteChainsRequireManualConfirmation(string scenario)
    {
        var repo=Station01HostFixture.FindWorkspace();
        var allowed=Path.Combine(repo,"artifacts","016-public-tray-flow");
        var root=Path.Combine(allowed,scenario+"-"+Guid.NewGuid().ToString("N"));
        var fixture=Path.Combine(repo,"specs","016-public-preparation-tray-check-unload","examples",scenario,"run.json");
        await using var driver=await RecipeExecution010RunHarness.CreateAsync(root,fixture);
        var positions=(await driver.Teaching.GetFromJsonAsync<JsonObject>("/api/v1/station01/configuration/public-positions"))!;
        // Operator cannot change shared coordinates. No backend-port replacement.
        Assert.Equal(HttpStatusCode.Forbidden,(await driver.Client.PostAsJsonAsync("/api/v1/station01/configuration/public-positions",new {
            threeD=positions["threeD"],manualLoading=positions["manualLoading"],expectedDigest=positions["digest"] })).StatusCode);
        var originalDigest=positions["digest"]!.GetValue<string>();
        positions["manualLoading"]!["x"]=180;
        using var saved=await driver.Teaching.PostAsJsonAsync("/api/v1/station01/configuration/public-positions",new {
            threeD=positions["threeD"],manualLoading=positions["manualLoading"],expectedDigest=originalDigest });
        Assert.Equal(HttpStatusCode.OK,saved.StatusCode);
        var updated=(await saved.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.NotEqual(originalDigest,updated["digest"]!.GetValue<string>());
        using var browser=scenario=="mixed" ? StartBrowser(repo,root,driver) : null;
        try {
        if(browser is not null) {
            var readyUntil=DateTimeOffset.UtcNow.AddSeconds(30);
            while(!File.Exists(Path.Combine(root,"browser-ready.json")) && !browser.HasExited && DateTimeOffset.UtcNow<readyUntil)await Task.Delay(100);
            Assert.True(File.Exists(Path.Combine(root,"browser-ready.json")),"Actual browser must be ready before the original anomaly window opens");
            updated=(await driver.Teaching.GetFromJsonAsync<JsonObject>("/api/v1/station01/configuration/public-positions"))!;
        }
        var request=new { requestId="016-"+Guid.NewGuid().ToString("N"),contextJson=JsonSerializer.Serialize(new {
            schemaVersion=StartRunContext.CurrentSchemaVersion,trayId=Guid.NewGuid(),stationId=Guid.NewGuid(),lineId=Guid.NewGuid(),
            scenarioId=driver.Fixture.GetProperty("scenarioId").GetString(),occupiedSlots=Array.Empty<string>(),purpose="Test" }),
            publicConfigRef=driver.Fixture.GetProperty("publicConfigRef"),budgetRef=driver.Fixture.GetProperty("budgetRef"),simulationRef=driver.Fixture.GetProperty("simulationRef") };
        await driver.SaveAsync("request-016.json",request);
        using var accepted=await driver.Client.PostAsJsonAsync("/api/v1/station01/runs",request);
        await File.WriteAllTextAsync(Path.Combine(root,"receipt-016.json"),await accepted.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Accepted,accepted.StatusCode);
        var receipt=(await accepted.Content.ReadFromJsonAsync<StartReceipt>())!;
        var runPath=$"/api/v1/station01/runs/{receipt.RunId:D}";
        JsonObject? run=null;var selected=false;var until=DateTimeOffset.UtcNow.AddSeconds(150);
        while(DateTimeOffset.UtcNow<until) {
            run=(await driver.Client.GetFromJsonAsync<JsonObject>(runPath))!;
            if(!selected && scenario=="intervention" && run["trayAnomalyDecision"]?["state"]?.GetValue<string>()=="Pending") {
                using var choice=await driver.Client.PostAsJsonAsync(runPath+"/tray-anomaly-decision",new {decisionId=run["trayAnomalyDecision"]!["decisionId"],choice="ManualIntervention"});
                Assert.Equal(HttpStatusCode.OK,choice.StatusCode);selected=true;
            }
            var state=((RunState)run["state"]!.GetValue<int>()).ToString();
            if(state=="AwaitingManualRemoval")break;
            if(state is "Blocked" or "RecoveryRequired" or "Cancelled" or "Restricted")break;
            await Task.Delay(100);
        }
        await driver.SaveAsync("before-confirmation.json",run);
        Assert.True(run?["state"]?.GetValue<int>()==(int)RunState.AwaitingManualRemoval,$"{scenario}: {run}");
        Assert.Equal((int)TerminalOutcome.None,run!["finalOutcome"]!.GetValue<int>());
        Assert.Contains("ConfirmManualTrayRemoval",run["allowedActions"]!.AsArray().Select(x=>x!.GetValue<string>()));
        var expectedReason=scenario=="empty"?"EmptyTray":scenario=="intervention"?"ManualIntervention":"NormalCompletion";
        Assert.Equal(expectedReason,run["trayEndReason"]!.GetValue<string>());
        Assert.False(run["inspectionCompleted"]!.GetValue<bool>());
        Assert.Equal(3,run["slotStates"]!.AsArray().Count);
        {
            var actual=await PublicPreparationTestStore.ReadTray(driver,receipt.RunId);
            var stages=actual.Stages;
            await driver.SaveAsync("stage-events-016.json",stages);
            var writes=actual.Writes;
            await driver.SaveAsync("writes-016.json",writes);
            Assert.DoesNotContain(stages,e=>e.EventType=="FinalUnloadCompleted");
            var frozen=Assert.Single(writes,x=>x.PayloadJson.Contains("FrozenPublicConfiguration"));
            var snapshot=JsonNode.Parse(frozen.PayloadJson)!;
            Assert.Equal(updated["digest"]!.GetValue<string>(),snapshot["publicDigest"]!.GetValue<string>());
            var frozenPublic=JsonNode.Parse(snapshot["publicJson"]!.GetValue<string>())!;
            Assert.Equal(180,frozenPublic["motion"]!["points"]!["unload"]!["x"]!.GetValue<double>());
            var start=Assert.Single(writes,x=>x.Kind=="StartIntent");
            var ready=Assert.Single(writes,x=>x.Kind=="ActionFact"&&x.PayloadJson.Contains("StartReadyObserved"));
            var firstMove=writes.First(x=>x.Kind=="ActionIntent");
            Assert.True(start.Revision<ready.Revision&&ready.Revision<firstMove.Revision);
            Assert.Equal("FixedXYZ3D",JsonNode.Parse(firstMove.PayloadJson)!["kind"]!.GetValue<string>());
            if(scenario is "empty" or "intervention") {
                Assert.DoesNotContain(writes,x=>x.PayloadJson.Contains("\"kind\":\"F\"")||x.PayloadJson.Contains("RecipePlanAndBindingIntent"));
                Assert.DoesNotContain(stages,x=>x.Stage is "Detection" or "Sorting" && x.EventType=="Completed");
                var whole=Assert.Single(actual.Completions);
                Assert.Null(whole.DetectionCompletedEventId);Assert.Null(whole.SortingCompletedEventId);
            } else {
                var occupied=stages.Where(e=>e.PayloadJson.Contains("SortingAssignmentOccupied")).ToArray();
                Assert.Equal(scenario=="mixed"?1:3,occupied.Length);
                Assert.All(occupied,e=>Assert.Contains("Pending",e.PayloadJson));
                Assert.Equal(scenario=="mixed"?2:0,writes.Count(x=>x.Kind=="CaptureFact" && x.PayloadJson.Contains("ConfiguredCaptureCompleted")));
                Assert.DoesNotContain(stages,x=>x.PayloadJson.Contains("\"sourceSlotId\":\"s2\"") && scenario=="mixed");
            }
        }
        using var confirmed=await driver.Client.PostAsJsonAsync(runPath+"/manual-removal-confirmations",new {
            requestId="confirm-016",expectedRevision=run["observedRevision"]!.GetValue<long>(),reason="Test: actual operator confirmation" });
        Assert.Equal(HttpStatusCode.Accepted,confirmed.StatusCode);
        var final=(await driver.Client.GetFromJsonAsync<JsonObject>(runPath))!;
        Assert.Equal((int)RunState.Completed,final["state"]!.GetValue<int>());
        await driver.SaveAsync("final-016.json",final);
        await driver.RestartFor016Async();
        var reread=(await driver.Client.GetFromJsonAsync<JsonObject>(runPath))!;
        Assert.Equal(expectedReason,reread["trayEndReason"]!.GetValue<string>());
        Assert.Equal("FinalUnloadCompletion",reread["wholeTaskState"]!.GetValue<string>());
        var restartPositions=(await driver.Teaching.GetFromJsonAsync<JsonObject>("/api/v1/station01/configuration/public-positions"))!;
        Assert.Equal(updated.ToJsonString(),restartPositions.ToJsonString());
        await driver.SaveAsync("restart-016.json",reread);
        if(browser is not null) {
            await browser.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
            Assert.Equal(0,browser.ExitCode);
            Assert.True(File.Exists(Path.Combine(root,"browser-result.json")));
        }
        } finally { if(browser is {HasExited:false}) {browser.Kill(entireProcessTree:true);await browser.WaitForExitAsync();} }
    }
    private static System.Diagnostics.Process StartBrowser(string repo,string root,RecipeExecution010RunHarness driver) {
        var connection=Path.Combine(root,"browser-connection.json");
        File.WriteAllText(connection,JsonSerializer.Serialize(new {apiBaseUrl=driver.Client.BaseAddress!.ToString().TrimEnd('/'),testToken=driver.BrowserToken}));
        var info=new System.Diagnostics.ProcessStartInfo(Environment.GetEnvironmentVariable("GAODE_011_PYTHON")!) {
            WorkingDirectory=repo,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true };
        info.ArgumentList.Add(Path.Combine(repo,"scripts","016-browser.py"));info.ArgumentList.Add(connection);
        var process=System.Diagnostics.Process.Start(info)!;
        _=Task.Run(async()=>await File.WriteAllTextAsync(Path.Combine(root,"browser.out.log"),await process.StandardOutput.ReadToEndAsync()));
        _=Task.Run(async()=>await File.WriteAllTextAsync(Path.Combine(root,"browser.err.log"),await process.StandardError.ReadToEndAsync()));
        return process;
    }
}
