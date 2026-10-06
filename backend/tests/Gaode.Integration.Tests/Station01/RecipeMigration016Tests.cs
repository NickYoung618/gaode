using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gaode.Application.Station01;
using Gaode.Domain.Station01;
using Gaode.Integration.Tests.Support;
using Xunit;
using Microsoft.Data.Sqlite;

namespace Gaode.Integration.Tests.Station01;

// T39/T44 actual process duties. Explicit migrated Test inputs retain composition,
// faces, individual targets, E issue and final confirmation; no component substitution.
public sealed class RecipeMigration016Tests
{
    [Fact]
    public async Task ReceivedTwoSpecialUnitsKeepOriginalSlotsAndDistinctEventsThroughActualFinalAndRestart()
    {
        var repo = Station01HostFixture.FindWorkspace();
        var identity = Guid.NewGuid().ToString("N");
        var root = Path.Combine(repo, "artifacts/016-public-tray-flow", "received-014-special-" + identity);
        var control = Path.Combine(repo, "artifacts/016-public-tray-flow", "control-received014-" + identity);
        var pageRoot = Path.Combine(repo, "artifacts/016-public-tray-flow", "pages", "received014-" + identity);
        Directory.CreateDirectory(control);
        var start = new System.Diagnostics.ProcessStartInfo("node") { WorkingDirectory = repo, UseShellExecute = false,
            CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add(Path.Combine(repo, "scripts/014-012-joint-page.mjs"));
        start.ArgumentList.Add(Path.Combine(root, "page-connection.json")); start.ArgumentList.Add(control);
        using var observer = System.Diagnostics.Process.Start(start)!;
        var stdout = observer.StandardOutput.ReadToEndAsync(); var stderr = observer.StandardError.ReadToEndAsync();
        try {
        var prewarmUntil = DateTimeOffset.UtcNow.AddSeconds(30);
        while (!File.Exists(Path.Combine(control, "browser-prewarm.json"))) {
            Assert.False(observer.HasExited, "Actual page observer failed before Host startup");
            Assert.True(DateTimeOffset.UtcNow < prewarmUntil, "Actual page prewarm incomplete"); await Task.Delay(100);
        }
        var source = Path.Combine(repo, "specs/016-public-preparation-tray-check-unload/examples/received-014-special/run-special.json");
        await using var driver = await RecipeExecution010RunHarness.CreateAsync(root, source);
        try {
            var runId = await driver.RunToFinalAsync(pageRoot);
            await observer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45));
            Assert.Equal(0, observer.ExitCode);
            using var page = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(pageRoot, "page-evidence.json")));
            Assert.True(page.RootElement.GetProperty("finalObserved").GetBoolean());
            Assert.Equal(runId.ToString("D"), page.RootElement.GetProperty("runId").GetString());
            Assert.Empty(page.RootElement.GetProperty("errors").EnumerateArray());
            await RecipeExecution010Expectations.VerifyAsync(driver, runId);
            var before = (await driver.Client.GetFromJsonAsync<RunApiSnapshot>($"/api/v1/station01/runs/{runId:D}"))!;
            var actual = await PublicPreparationTestStore.ReadTray(driver, runId);
            var cycles = actual.Stages.Where(s => s.PayloadJson.Contains("\"kind\":\"UnitCycleCompleted\"")).ToArray();
            Assert.Equal(2, cycles.Length); Assert.Equal(2, cycles.Select(s => s.EventId).Distinct().Count());
            Assert.Single(actual.Stages, s => s.EventType == "FinalUnloadCompleted");
            await driver.RestartFor016Async();
            var reread = (await driver.Client.GetFromJsonAsync<RunApiSnapshot>($"/api/v1/station01/runs/{runId:D}"))!;
            Assert.Equal("FinalUnloadCompletion", reread.WholeTaskState);
            Assert.Equal(JsonSerializer.Serialize(before.Results), JsonSerializer.Serialize(reread.Results));
            await driver.SaveAsync("special-restart-and-events.json", new { actual, before, reread });
        } catch (Exception first) {
            await driver.SaveAsync("test-first-failure.json", new { type = first.GetType().Name, first.Message, first.StackTrace, cleanupReportedSeparately = true });
            throw;
        }
        } finally {
            if (!observer.HasExited) {
                await File.WriteAllTextAsync(Path.Combine(control, "observer-abort.json"), "{\"state\":\"OwnedObserverCleanupRequested\",\"businessSuccess\":false}");
                try { await observer.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
                catch (TimeoutException) { observer.Kill(entireProcessTree: true); await observer.WaitForExitAsync(); }
            }
            await File.WriteAllTextAsync(Path.Combine(control, "observer.log"), await stdout + await stderr);
        }
    }

    [Theory]
    [InlineData("group-normal")]
    [InlineData("group-mixed")]
    [InlineData("group-four")]
    [InlineData("assembly-code")]
    [InlineData("assembly-missing")]
    [InlineData("assembly-error")]
    [InlineData("assembly-ng-pending")]
    public async Task GroupsAndAssemblyRetainIndependentResultsThroughActualFinalAndRestart(string input)
    {
        var repo = Station01HostFixture.FindWorkspace();
        var root = Path.Combine(repo, "artifacts/016-public-tray-flow", "migration-" + input + "-" + Guid.NewGuid().ToString("N"));
        var source = Path.Combine(repo, "specs/016-public-preparation-tray-check-unload/examples", "migration-" + input, "run.json");
        await using var driver = await RecipeExecution010RunHarness.CreateAsync(root, source);
        try {
        if (input is "group-normal" or "assembly-code") {
            var connection = Path.Combine(root, "authoring-browser-connection.json");
            await File.WriteAllTextAsync(connection, JsonSerializer.Serialize(new {
                apiBaseUrl = driver.Client.BaseAddress!.ToString().TrimEnd('/'), testToken = driver.BrowserToken,
                fCode = driver.Fixture.GetProperty("fCode").GetString() }));
            var start = new System.Diagnostics.ProcessStartInfo(Environment.GetEnvironmentVariable("GAODE_011_PYTHON")!) {
                WorkingDirectory = repo, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(Path.Combine(repo, "scripts/016-authoring-browser.py")); start.ArgumentList.Add(connection);
            using var browser = System.Diagnostics.Process.Start(start)!;
            var stdout = browser.StandardOutput.ReadToEndAsync(); var stderr = browser.StandardError.ReadToEndAsync();
            try { await browser.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(90)); }
            finally { if (!browser.HasExited) { browser.Kill(entireProcessTree: true); await browser.WaitForExitAsync(); }
                await File.WriteAllTextAsync(Path.Combine(root, "authoring-browser.log"), await stdout + await stderr); }
            Assert.Equal(0, browser.ExitCode);
            Assert.True(File.Exists(Path.Combine(root, "authoring-browser-result.json")));
        }
        var expected = driver.Fixture.GetProperty("expected");
        int Count(string name) => expected.GetProperty(name).GetInt32();
        var catalog = await driver.Teaching.GetFromJsonAsync<JsonElement>("/api/v1/recipes/catalog");
        var selected = Assert.Single(catalog.GetProperty("items").EnumerateArray(), x =>
            x.GetProperty("fCode").GetString() == driver.Fixture.GetProperty("fCode").GetString());
        using var accepted = await driver.Client.PostAsJsonAsync("/api/v1/station01/runs", new {
            requestId = "migration-" + Guid.NewGuid().ToString("N"),
            contextJson = JsonSerializer.Serialize(new { schemaVersion = StartRunContext.RecipeSchemaVersion,
                trayId = Guid.NewGuid(), stationId = Guid.NewGuid(), lineId = Guid.NewGuid(),
                scenarioId = driver.Fixture.GetProperty("scenarioId"), occupiedSlots = driver.Fixture.GetProperty("occupiedSlots"), purpose = "Test",
                expectedRecipeRef = new { recipeId = selected.GetProperty("recipeId"), version = selected.GetProperty("version"),
                    catalogDigest = selected.GetProperty("catalogDigest") } }),
            publicConfigRef = driver.Fixture.GetProperty("publicConfigRef"), budgetRef = driver.Fixture.GetProperty("budgetRef"),
            simulationRef = driver.Fixture.GetProperty("simulationRef") });
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        var receipt = (await accepted.Content.ReadFromJsonAsync<StartReceipt>())!;
        var path = $"/api/v1/station01/runs/{receipt.RunId:D}";
        RunApiSnapshot run;
        var until = DateTimeOffset.UtcNow.AddMinutes(12); // Observation only; no action deadline changes.
        do {
            run = (await driver.Client.GetFromJsonAsync<RunApiSnapshot>(path))!;
            if (run.State is RunState.AwaitingManualRemoval or RunState.Blocked or RunState.RecoveryRequired) break;
            await Task.Delay(100);
        } while (DateTimeOffset.UtcNow < until);
        await driver.SaveAsync("before-confirmation.json", run);
        Assert.True(run.State == RunState.AwaitingManualRemoval, JsonSerializer.Serialize(run));
        Assert.Equal(Count("groupCount"), run.Results.Count(r => r.Kind == "Group"));
        var memberKind = input.StartsWith("assembly", StringComparison.Ordinal) ? "Part" : "Member";
        Assert.Equal(Count("memberCount"), run.Results.Count(r => r.Kind == memberKind));
        Assert.Equal(Count("faceCount"), run.Results.Count(r => r.Kind == "Face"));
        Assert.Equal(Count("ng"), run.Results.Count(r => r.Kind == memberKind && r.Disposition == "NG"));
        var stored = await PublicPreparationTestStore.ReadTray(driver, receipt.RunId);
        await driver.SaveAsync("actual-sqlite.json", stored);
        Assert.Equal(Count("captureCount"), stored.Writes.Count(w => w.Kind == "CaptureFact" && w.PayloadJson.Contains("ConfiguredCaptureCompleted")));
        var flipFacts = stored.Stages.Select(s => JsonDocument.Parse(s.PayloadJson).RootElement.Clone()).ToArray();
        var picks = flipFacts.Where(s => s.TryGetProperty("kind", out var k) && k.GetString() == "FlipPickCompleted").ToArray();
        var putBacks = flipFacts.Where(s => s.TryGetProperty("kind", out var k) && k.GetString() == "FlipPutBackCompleted").ToArray();
        Assert.Equal(Count("flips"), picks.Length); Assert.Equal(picks.Length, putBacks.Length);
        Assert.Equal(picks.Select(s => s.GetProperty("transitionId").GetGuid()).Order(), putBacks.Select(s => s.GetProperty("transitionId").GetGuid()).Order());
        var decisions = stored.Stages.Where(s => s.PayloadJson.Contains("\"kind\":\"DetectionUnitDecision\""))
            .SelectMany(s => JsonDocument.Parse(s.PayloadJson).RootElement.GetProperty("objects").EnumerateArray().Select(x => x.Clone())).ToArray();
        Assert.Equal(Count("objectCount"), decisions.Length);
        Assert.Equal(decisions.Length, decisions.Select(x => x.GetProperty("ObjectId").GetString()).Distinct().Count());
        Assert.Equal(Count("ng"), decisions.Count(x => x.GetProperty("Disposition").GetString() == "NG"));
        if (input == "group-mixed") {
            Assert.EndsWith(":G:P01:M01", Assert.Single(decisions, x => x.GetProperty("Disposition").GetString() == "NG").GetProperty("ObjectId").GetString());
            Assert.Contains(run.Results, r => r.Kind == "Face" && r.Disposition == "Pending");
        }
        if (input == "assembly-missing") {
            var binding = Assert.Single(flipFacts, s => s.TryGetProperty("kind", out var k) && k.GetString() == "ECodeBinding");
            Assert.Equal("ECodeNoResult", binding.GetProperty("issue").GetString());
            Assert.Equal(JsonValueKind.Null, binding.GetProperty("externalCode").ValueKind);
            Assert.Equal("Pending", Assert.Single(decisions).GetProperty("Disposition").GetString());
            Assert.Single(stored.Stages, s => s.PayloadJson.Contains("SortingAssignmentOccupied"));
        }
        if (input == "assembly-error") {
            var binding = Assert.Single(flipFacts, s => s.TryGetProperty("kind", out var k) && k.GetString() == "ECodeBinding");
            Assert.Equal("ControlledEDecodeFailure", binding.GetProperty("issue").GetString());
            Assert.Equal("Pending", Assert.Single(decisions).GetProperty("Disposition").GetString());
            Assert.Single(stored.Stages, s => s.PayloadJson.Contains("SortingAssignmentOccupied"));
        }
        if (input == "assembly-ng-pending") {
            Assert.Contains(run.Results, r => r.Kind == "Face" && r.Disposition == "Pending");
            Assert.Equal("NG", Assert.Single(decisions).GetProperty("Disposition").GetString());
            Assert.Single(stored.Stages, s => s.PayloadJson.Contains("SortingAssignmentOccupied"));
        }
        Assert.Single(stored.Completions);
        Assert.DoesNotContain(stored.Stages, s => s.EventType == "FinalUnloadCompleted");
        using var confirmation = await driver.Client.PostAsJsonAsync(path + "/manual-removal-confirmations", new {
            requestId = "migration-confirm", expectedRevision = run.ObservedRevision, reason = "Test: explicit operator confirmation" });
        Assert.Equal(HttpStatusCode.Accepted, confirmation.StatusCode);
        await driver.RestartFor016Async();
        var reread = (await driver.Client.GetFromJsonAsync<RunApiSnapshot>(path))!;
        await driver.SaveAsync("restart-readback.json", reread);
        Assert.Equal("FinalUnloadCompletion", reread.WholeTaskState);
        Assert.Equal(JsonSerializer.Serialize(run.Results), JsonSerializer.Serialize(reread.Results));
        if (input is "group-normal" or "assembly-code") {
            using var browserProof = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "authoring-browser-result.json")));
            var recipeId = browserProof.RootElement.GetProperty("recipeId").GetString();
            var saved = browserProof.RootElement.GetProperty("saved");
            var reloaded = await driver.Teaching.GetFromJsonAsync<JsonElement>("/api/v1/recipes/" + recipeId);
            Assert.Equal(saved.GetProperty("version").GetString(), reloaded.GetProperty("definition").GetProperty("version").GetString());
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(saved.GetRawText()), JsonNode.Parse(reloaded.GetProperty("definition").GetRawText())));
            await using var sqlite = new SqliteConnection(new SqliteConnectionStringBuilder {
                DataSource = Path.Combine(driver.RecipeRoot, "recipes.db"), Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            await sqlite.OpenAsync();
            await using var command = sqlite.CreateCommand();
            command.CommandText = "SELECT c.DefinitionJson FROM RecipeHead h JOIN RecipeSavedContent c ON c.RecipeId=h.RecipeId AND c.Version=h.CurrentVersion WHERE h.RecipeId=$id";
            command.Parameters.AddWithValue("$id", recipeId);
            using var persisted = JsonDocument.Parse((string)(await command.ExecuteScalarAsync())!);
            Assert.Equal(saved.GetProperty("version").GetString(), persisted.RootElement.GetProperty("version").GetString());
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(saved.GetRawText()), JsonNode.Parse(persisted.RootElement.GetRawText())));
            await driver.SaveAsync("authoring-restart-sqlite.json", new { api = reloaded, sqlite = persisted.RootElement });
        }
        } catch (Exception first) {
            await driver.SaveAsync("test-first-failure.json", new { type = first.GetType().Name, first.Message, first.StackTrace,
                cleanupReportedSeparately = true, finalAccepted = false });
            throw;
        }
    }
}
