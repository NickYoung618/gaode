using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Diagnostics;
using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Application.Station01;
using Gaode.Application.Workflow;
using Gaode.Domain.Station01;
using Gaode.Host.Api;
using Gaode.Host.Composition;
using Gaode.Integration.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.Extensions.Logging;
using Gaode.Domain.Configuration;
using Xunit;

namespace Gaode.Integration.Tests.Station01;

// Real Host, configuration loader, entry points and SQLite. FullSimulation is
// explicit; BA02/03/05 retain their separate real TCP/independent-process duties.
public sealed class RecipeApplicationDeadlineTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // Entry lifecycle component: actual start/F/binding/storage, declared downstream
    // capability and a stopping service substitute. This is UpperIsolation, never FullRun.
    [Theory]
    [InlineData("context/2.0")]
    [InlineData("context/1.0")]
    [InlineData("independent/no-downstream")]
    [Trait("EvidenceLevel", "UpperIsolation")]
    public async Task CommonInputsKeepOriginalDeadlineOriginForAllThreeEntries(string entry)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var reached = new TaskCompletionSource<Gaode.Application.Workflow.RecipeExecutionDeadlines?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var dispatched = new TaskCompletionSource<DetectionRequest>(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<Guid, Gaode.Application.Workflow.RecipeExecutionDeadlines?, CancellationToken, Task> hold = async (_, deadlines, ct) =>
        { reached.TrySetResult(deadlines); using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token); await release.Task.WaitAsync(linked.Token); };
        await using var fixture = await Station01HostFixture.CreateAsync(services => {
            services.RemoveAll<IRecipeCatalog>();
            services.AddSingleton<IRecipeCatalog>(RecipeBindingTestSupport.Catalog(Path.Combine(Station01HostFixture.FindWorkspace(),
                "specs/008-recipe-driven-inspection/fixtures/recipes.json")));
            services.RemoveAll<Gaode.Application.Capabilities.CapabilityRegistry>();
            services.AddSingleton(sp => {
                var registry = CapabilityRegistration.RegisterStation01(sp.GetRequiredService<IAlgorithmPort>(), "Test");
                registry.RegisterAlgorithm(AlgorithmPurpose.SingleDetection, "declared-single", "1", "image-quality/1", 1,
                    "UpperIsolation", "declared/1", "Test", "unit-only");
                registry.RegisterAlgorithm(AlgorithmPurpose.FaceFusion, "declared-fusion", "1", "face-quality/1", 2,
                    "UpperIsolation", "declared/1", "Test", "unit-only");
                return registry;
            });
            services.RemoveAll<IDetectionPort>(); services.AddSingleton<IDetectionPort>(new StopAfterRequest(dispatched));
            services.RemoveAll<StartPublicPreparation>();
            services.AddSingleton(sp => {
                var options = sp.GetRequiredService<Station01RuntimeOptions>();
                return ActivatorUtilities.CreateInstance<StartPublicPreparation>(sp, hold,
                    sp.GetRequiredService<Gaode.Application.Timing.DeadlineScheduler>().ClockId,
                    Guid.NewGuid(), options.SimulationReference,
                    sp.GetRequiredService<IPublicConfiguration>().LoadBudget(options.BudgetReference).Value);
            });
        }, recipeFixtureCode: "TEST-TRAY-0101");
        var selected = Assert.Single(fixture.Host.Services.GetRequiredService<IRecipeCatalog>().GetSnapshot().Definitions);
        var context = JsonNode.Parse(StartRunContextJson.Create(occupiedSlots: ["P01"]))!;
        if (entry == "context/2.0")
        {
            context["schemaVersion"] = StartRunContext.RecipeSchemaVersion;
            context["expectedRecipeRef"] = JsonSerializer.SerializeToNode(new ExpectedRecipeRef(selected.RecipeId,
                selected.Version, selected.CatalogDigest), Json);
        }
        var request = new StartPublicRequest("010-entry-" + Guid.NewGuid().ToString("N"), context.ToJsonString(),
            new("s01-public-dev", "1.0.0"), new("s01-budget-dev", "3.0.0"), new("s01-sim-normal", "3.0.0"));
        using var response = await fixture.Client.PostAsJsonAsync("/api/v1/station01/runs", request, timeout.Token);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var accepted = (await response.Content.ReadFromJsonAsync<StartReceipt>(timeout.Token))!;
        var route = await reached.Task.WaitAsync(timeout.Token);
        var query = fixture.Host.Services.GetRequiredService<ITraceQuery>();
        var writes = await query.GetWritesAsync(accepted.RunId, timeout.Token);
        var intent = Assert.Single(writes, w => Kind(w.PayloadJson) == "RecipePlanAndBindingIntent");
        var receiptJson = JsonDocument.Parse(Assert.Single(writes, w => Kind(w.PayloadJson) == "RecipeApplicationReceiptObserved").PayloadJson);
        var binding = receiptJson.RootElement.GetProperty("receipt").Deserialize<RecipeBindingReceipt>(Json)!;
        Assert.True(binding.WasCompletedInWindow);
        var committedHandoff = await fixture.Host.Services.GetRequiredService<IStageHandoffQuery>().GetCommittedV2Async(accepted.RunId, timeout.Token);
        Assert.NotNull(committedHandoff);
        if (entry == "context/2.0")
        {
            Assert.NotNull(route);
            Assert.True(Assert.Single(writes, w => Kind(w.PayloadJson) == "RecipeExecutionDeadlinesFrozen").Revision < intent.Revision);
            Assert.True(binding.Window.DeadlineUtc <= route.DetectionDeadlineUtc);
        }
        else
        {
            Assert.Null(route); Assert.DoesNotContain(writes, w => Kind(w.PayloadJson) == "RecipeExecutionDeadlinesFrozen");
        }
        if (entry == "independent/no-downstream")
        {
            var before = (await query.GetRunAsync(accepted.RunId, timeout.Token))!.Revision;
            using var independent = await fixture.Client.PostAsJsonAsync("/api/v1/recipes/bind",
                new RecipePlanRequest(accepted.RunId, "S1", ["P01"]), timeout.Token);
            Assert.True(independent.StatusCode == HttpStatusCode.OK, await independent.Content.ReadAsStringAsync(timeout.Token));
            using var body = JsonDocument.Parse(await independent.Content.ReadAsStringAsync(timeout.Token));
            Assert.False(body.RootElement.GetProperty("productContinuationAuthorized").GetBoolean());
            var window = body.RootElement.GetProperty("bindingResult").GetProperty("window");
            Assert.Equal(10000, window.GetProperty("deadlineAtUtc").GetDateTimeOffset().Subtract(window.GetProperty("startedAtUtc").GetDateTimeOffset()).TotalMilliseconds);
            Assert.Equal(before, (await query.GetRunAsync(accepted.RunId, timeout.Token))!.Revision);
            Assert.Equal(committedHandoff.Handoff.PayloadDigest, (await fixture.Host.Services.GetRequiredService<IStageHandoffQuery>().GetCommittedV2Async(accepted.RunId, timeout.Token))!.Handoff.PayloadDigest);
            Assert.False(dispatched.Task.IsCompleted);
            var events = await fixture.Host.Services.GetRequiredService<IStageEventStore>().ReadAsync(accepted.RunId,
                committedHandoff.Handoff.Identity.TrayId, WholeTrayWorkflowStage.Detection, timeout.Token);
            Assert.DoesNotContain(events, e => e.StageDeadlineAtUtc.HasValue);
            // The API receipt grants binding only. End this controlled entry component;
            // do not treat an independent binding as permission to resume the product chain.
            fixture.Host.Services.GetRequiredService<Station01Coordinator>().SignalCancel(accepted.RunId);
            release.TrySetResult();
            return;
        }
        release.TrySetResult();
        var detection = await dispatched.Task.WaitAsync(timeout.Token);
        Assert.NotNull(detection.Inputs); Assert.True(detection.Inputs.IsValid);
        Assert.NotEmpty(detection.Targets!); Assert.NotEmpty(detection.ExpectedObjects!);
        if (route is not null)
        {
            Assert.Equal(route.StartedUtc, detection.StageStartedAtUtc);
            Assert.Equal(route.DetectionDeadlineUtc, detection.DeadlineUtc);
        }
        else
        {
            Assert.True(detection.StageStartedAtUtc >= binding.RequiredCommits.Max(c => c.CommittedUtc));
            Assert.Equal(Gaode.Application.Workflow.StageRetryPolicy.StageDuration,
                detection.DeadlineUtc - detection.StageStartedAtUtc!.Value);
        }
    }

    private sealed class StopAfterRequest(TaskCompletionSource<DetectionRequest> observed) : IDetectionPort
    {
        public ValueTask<DetectionPortResult> ExecuteAsync(DetectionRequest request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); observed.TrySetResult(request);
            return ValueTask.FromResult(new DetectionPortResult(request, DetectionResultKind.Failed, null, [],
                ResultSource.Fallback, ResultQuality.Unknown, "UpperIsolationStoppedAtDetectionEntry", DateTimeOffset.UtcNow));
        }
    }

    internal sealed class BindingLog(string path) : ILogger
    {
        private readonly object gate = new();
        public string Path { get; } = path;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            if (!message.StartsWith("RuntimeFlow ", StringComparison.Ordinal)) return;
            var json = message["RuntimeFlow ".Length..];
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.GetProperty("step").GetString() is not ("RecipeApplication" or "DetectionHandoff")) return;
            lock (gate) File.AppendAllText(Path, json + Environment.NewLine);
        }
    }

    [Theory]
    [InlineData("BA07-config/missing", "SchemaInvalid")]
    [InlineData("BA07-config/null", "SchemaInvalid")]
    [InlineData("BA07-config/zero", "SchemaInvalid")]
    [InlineData("BA07-config/negative", "SchemaInvalid")]
    [InlineData("BA07-config/fraction", "SchemaInvalid")]
    [InlineData("BA07-config/nonfinite-json", "Json")]
    [InlineData("BA07-config/overflow", "SchemaInvalid")]
    [InlineData("BA07-config/missing-version", "ConfigurationNotFound")]
    [InlineData("BA07-config/version-conflict", "ConfigurationVersionConflict")]
    [InlineData("BA07-config/purpose", "SchemaInvalid")]
    [InlineData("BA07-config/source", "RecipeApplicationBudgetInvalid")]
    [InlineData("BA07-config/freeze", "FrozenBudgetIdentityInvalid")]
    [InlineData("BA07-config/production-unapproved", "Production")]
    public async Task InvalidLoadedBudgetRejectsContinuousEntryBeforeAnyDeviceAction(string caseId, string reason)
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        var token = watchdog.Token;
        await using var fixture = await Station01HostFixture.CreateAsync(services => {
            if (caseId != "BA07-config/freeze") return;
            services.RemoveAll<IPublicConfiguration>();
            services.AddSingleton<IPublicConfiguration>(sp => {
                var options = sp.GetRequiredService<Station01RuntimeOptions>();
                return new InvalidFreezeInput(RecipeBindingTestSupport.Configuration(options.ConfigRoot, options.SchemaRoot));
            });
        }, recipeFixtureCode: "RC:R-S1-A-CAP:0.4.0-review");
        var configRoot = fixture.Host.Services.GetRequiredService<Station01RuntimeOptions>().ConfigRoot;
        Assert.StartsWith(fixture.StoreRoot, configRoot, StringComparison.OrdinalIgnoreCase);
        var budgetPath = Directory.EnumerateFiles(configRoot, "*.json").Single(path => {
            var node = JsonNode.Parse(File.ReadAllText(path))!;
            return node["id"]?.GetValue<string>() == "s01-budget-dev";
        });
        var budget = JsonNode.Parse(await File.ReadAllTextAsync(budgetPath, token))!;
        var scenario = caseId.Split('/')[1];
        var durations = budget["businessMs"]!.AsObject();
        switch (scenario)
        {
            case "missing": durations.Remove("recipeApplication"); break;
            case "null": durations["recipeApplication"] = null; break;
            case "zero": durations["recipeApplication"] = 0; break;
            case "negative": durations["recipeApplication"] = -1; break;
            case "fraction": durations["recipeApplication"] = 1.5; break;
            case "overflow": durations["recipeApplication"] = 2147483648L; break;
            case "missing-version": budget.AsObject().Remove("version"); break;
            case "purpose": budget["purpose"] = "Unapproved"; break;
            case "source": budget["source"] = " "; break;
            case "production-unapproved": budget["purpose"] = "Production"; break;
        }
        if (scenario == "production-unapproved")
        {
            // Give both business inputs their actual Production purpose so the
            // new unapproved budget gate, not an unrelated purpose mismatch, rejects.
            var publicPath = Directory.EnumerateFiles(configRoot, "*.json").Single(path =>
                JsonNode.Parse(File.ReadAllText(path))!["id"]?.GetValue<string>() == "s01-public-dev");
            var document = JsonNode.Parse(await File.ReadAllTextAsync(publicPath, token))!;
            document["purpose"] = "Production";
            await File.WriteAllTextAsync(publicPath, document.ToJsonString(), token);
        }
        var json = budget.ToJsonString();
        if (scenario == "nonfinite-json") json = json.Replace("\"recipeApplication\":10000", "\"recipeApplication\":NaN", StringComparison.Ordinal);
        await File.WriteAllTextAsync(budgetPath, json, token);
        if (scenario == "version-conflict")
        {
            budget["source"] = "different-content-same-version";
            await File.WriteAllTextAsync(Path.Combine(configRoot, "conflicting-test-budget.json"), budget.ToJsonString(), token);
        }
        var request = new StartPublicRequest(Guid.NewGuid().ToString("N"),
            StartRunContextJson.Create(occupiedSlots: ["P01"], purpose: scenario == "production-unapproved" ? "Production" : "Test"),
            new("s01-public-dev", "1.0.0"), new("s01-budget-dev", "3.0.0"), new("s01-sim-normal", "3.0.0"));
        var response = await fixture.Client.PostAsJsonAsync("/api/v1/station01/runs", request, token);
        Assert.True(response.StatusCode == HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync(token));
        var start = (await response.Content.ReadFromJsonAsync<StartReceipt>(token))!;
        RunApiSnapshot snapshot;
        do
        {
            snapshot = (await fixture.Client.GetFromJsonAsync<RunApiSnapshot>(start.StatusUrl, token))!;
            // The committed state can be observed before the coordinator publishes
            // its diagnostic revision. Require both within this same watchdog.
            if (snapshot.State is (RunState.Blocked or RunState.ConfigurationBlocked) &&
                !string.IsNullOrWhiteSpace(snapshot.ErrorCode)) break;
            await Task.Delay(20, token);
        } while (true);
        Assert.Contains(reason, snapshot.ErrorCode ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, fixture.Plc.StartCommands);
        Assert.Equal(0, fixture.Plc.MoveCommands);
        Assert.Equal(0, fixture.CommittedRecipeBindings);
        var query = RecipeBindingTestSupport.Query(fixture);
        var writes = (await query.GetWritesAsync(start.RunId, token)).ToArray();
        Assert.DoesNotContain(writes, w => Kind(w.PayloadJson) is "RecipePlanAndBindingIntent" or "RecipePlanBound");
        Assert.Empty(await RecipeBindingTestSupport.HandoffPayloads(fixture, start.RunId, token));
        var evidenceRoot = Environment.GetEnvironmentVariable("GAODE_009_EVIDENCE_ROOT") ?? throw new InvalidOperationException("009EvidenceRootRequired");
        Directory.CreateDirectory(evidenceRoot);
        await File.WriteAllTextAsync(Path.Combine(evidenceRoot, caseId.Replace('/', '-') + "-" + start.RunId.ToString("N") + ".json"),
            JsonSerializer.Serialize(new { caseId, scope = "ActualContinuousHostEntryFullSimulationAndSqlite", fixture.StoreRoot,
                inputPath = budgetPath, snapshot, writes, fixture.Plc.StartCommands, fixture.Plc.MoveCommands, fixture.CommittedRecipeBindings,
                independentApiAndTcpNotClaimed = true }, Json), token);
    }

    [Theory]
    [InlineData("BA01-source/strict")]
    [InlineData("BA01-source/legacy")]
    [InlineData("BA01-source/api")]
    [InlineData("BA06-downstream/strict-before-next")]
    [InlineData("BA06-downstream/api-existing")]
    [InlineData("BA06-downstream/api-none")]
    [InlineData("BA06-downstream/legacy")]
    [InlineData("BA07-config/valid-test")]
    public async Task FrozenBudgetIsUsedByEachActualEntry(string caseId)
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(50));
        var token = watchdog.Token;
        var evidenceRoot = Environment.GetEnvironmentVariable("GAODE_009_EVIDENCE_ROOT")
            ?? throw new InvalidOperationException("009EvidenceRootRequired");
        Directory.CreateDirectory(evidenceRoot);
        var log = new BindingLog(Path.Combine(evidenceRoot, caseId.Replace('/', '-') + "-" + Guid.NewGuid().ToString("N") + ".jsonl"));
        using var subscription = RecipeBindingTestSupport.Subscribe(log);
        var strict = caseId is "BA01-source/strict" or "BA06-downstream/strict-before-next" or "BA06-downstream/api-existing";
        var code = strict ? "TEST-TRAY-0101" : "RC:R-S1-A-CAP:0.4.0-review";
        await using var fixture = await Station01HostFixture.CreateAsync(services => {
            if (!strict) return;
            services.RemoveAll<IRecipeCatalog>();
            services.AddSingleton<IRecipeCatalog>(RecipeBindingTestSupport.Catalog(Path.Combine(Station01HostFixture.FindWorkspace(),
                "specs/008-recipe-driven-inspection/fixtures/recipes.json")));
        }, recipeFixtureCode: code);
        var catalog = fixture.Host.Services.GetRequiredService<IRecipeCatalog>();
        var recipe = RecipeBindingTestSupport.Match(catalog, code, "S1");
        var context = JsonNode.Parse(StartRunContextJson.Create(occupiedSlots: ["P01"]))!;
        DateTimeOffset? originalDetectionDeadline = null;
        if (strict)
        {
            context["schemaVersion"] = StartRunContext.RecipeSchemaVersion;
            context["expectedRecipeRef"] = JsonSerializer.SerializeToNode(new ExpectedRecipeRef(
                recipe.RecipeId, recipe.Version, recipe.CatalogDigest), Json);
        }
        var request = new StartPublicRequest(Guid.NewGuid().ToString("N"), context.ToJsonString(),
            new("s01-public-dev", "1.0.0"), new("s01-budget-dev", "3.0.0"), new("s01-sim-normal", "3.0.0"));
        var response = await fixture.Client.PostAsJsonAsync("/api/v1/station01/runs", request, token);
        Assert.True(response.StatusCode == HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync(token));
        var start = (await response.Content.ReadFromJsonAsync<StartReceipt>(token))!;
        RunApiSnapshot snapshot;
        do
        {
            snapshot = (await fixture.Client.GetFromJsonAsync<RunApiSnapshot>(start.StatusUrl, token))!;
            Assert.True(snapshot.State is not (RunState.Blocked or RunState.ConfigurationBlocked or RunState.RecoveryRequired),
                JsonSerializer.Serialize(new { snapshot.State, snapshot.ErrorCode, snapshot.Events }, Json));
            if (snapshot.Events.Contains("PublicHandoffV2Committed")) break;
            await Task.Delay(20, token);
        } while (true);
        Assert.Equal("Bound", snapshot.RecipeState);
        var query = RecipeBindingTestSupport.Query(fixture);
        var writes = (await query.GetWritesAsync(start.RunId, token)).ToArray();
        var approvalFact = Assert.Single(writes, w => Kind(w.PayloadJson) == "RecipeApplicationReceiptObserved");
        using var approvalJson = JsonDocument.Parse(approvalFact.PayloadJson);
        var receipt = approvalJson.RootElement.GetProperty("receipt").Deserialize<RecipeBindingReceipt>(Json)!;
        var frozenFact = Assert.Single(writes, w => Kind(w.PayloadJson) == "FrozenPublicConfiguration");
        using var frozen = JsonDocument.Parse(frozenFact.PayloadJson);
        var source = receipt.BudgetSource;
        Assert.Equal(10000, source.BudgetMs);
        Assert.Equal("2.0.0", source.Version);
        Assert.Equal("Test", source.Purpose);
        Assert.Equal(frozen.RootElement.GetProperty("budgetDigest").GetString(), source.Digest);
        Assert.Equal(frozen.RootElement.GetProperty("snapshotId").GetString(), source.SnapshotId);
        Assert.True(receipt.WasCompletedInWindow);
        var committedBound = Assert.Single(writes, w => Kind(w.PayloadJson) == "RecipePlanBound");
        using var boundJson = JsonDocument.Parse(committedBound.PayloadJson);
        Assert.Equal(boundJson.RootElement.GetProperty("window").Deserialize<ActionWindow>(Json), receipt.Window);
        var intent = Assert.Single(writes, w => Kind(w.PayloadJson) == "RecipePlanAndBindingIntent");
        Assert.Equal(intent.WriteId, receipt.IntentCommit!.WriteId);
        Assert.All(receipt.RequiredCommits, commit => Assert.True(commit.PersistedRevision > intent.Revision));
        var originalHandoff = Assert.Single(await RecipeBindingTestSupport.HandoffPayloads(fixture, start.RunId, token));
        var deadlineFact = writes.SingleOrDefault(w => Kind(w.PayloadJson) == "RecipeExecutionDeadlinesFrozen");
        if (strict)
        {
            Assert.NotNull(deadlineFact);
            Assert.True(deadlineFact.Revision < intent.Revision);
            using var deadlines = JsonDocument.Parse(deadlineFact.PayloadJson);
            var downstream = deadlines.RootElement.GetProperty("routeDeadlines").GetProperty("detectionDeadlineUtc").GetDateTimeOffset();
            originalDetectionDeadline = downstream;
            Assert.True(receipt.Window.DeadlineUtc <= downstream);
        }
        else Assert.Null(deadlineFact);

        JsonElement? handoffTiming = null;
        if (caseId == "BA06-downstream/legacy")
        {
            handoffTiming = Assert.Single(File.ReadAllLines(log.Path).Select(x => JsonDocument.Parse(x).RootElement.Clone()),
                e => e.GetProperty("runId").GetGuid() == start.RunId && e.GetProperty("step").GetString() == "DetectionHandoff");
            var timing = handoffTiming.Value.GetProperty("facts");
            Assert.False(timing.GetProperty("strict").GetBoolean());
            var beginning = timing.GetProperty("stageStartedAtUtc").GetDateTimeOffset();
            var due = timing.GetProperty("stageDeadlineAtUtc").GetDateTimeOffset();
            Assert.True(beginning >= receipt.RequiredCommits.Max(c => c.CommittedUtc!.Value));
            Assert.Equal(Gaode.Application.Workflow.StageRetryPolicy.StageDuration, due - beginning);
            Assert.Equal(receipt.BindingId, timing.GetProperty("bindingId").GetString());
            Assert.Equal(receipt.Window, timing.GetProperty("bindingWindow").Deserialize<ActionWindow>(Json));
        }

        // Publish a different latest version only in this controlled fixture copy.
        // The active run and the independent API must continue to read its committed
        // complete frozen snapshot, not this latest file.
        var configRoot = fixture.Host.Services.GetRequiredService<Station01RuntimeOptions>().ConfigRoot;
        Assert.StartsWith(fixture.StoreRoot, configRoot, StringComparison.OrdinalIgnoreCase);
        var latest = JsonNode.Parse(frozen.RootElement.GetProperty("budgetJson").GetString()!)!;
        latest["version"] = "2.0.1";
        latest["businessMs"]!["recipeApplication"] = 20000;
        await File.WriteAllTextAsync(Path.Combine(configRoot, "budget-new-version-test-only.json"), latest.ToJsonString(), token);
        var loaded = fixture.Host.Services.GetRequiredService<IPublicConfiguration>().LoadBudget(new(source.ConfigurationId, "2.0.1"));
        Assert.Equal(20000, loaded.Value.BusinessMs.RecipeApplication);
        Assert.Equal(10000, receipt.BudgetSource.BudgetMs);

        if (caseId == "BA06-downstream/strict-before-next")
        {
            Assert.NotNull(originalDetectionDeadline);
            var elapsedClock = new FakeTimeProvider(originalDetectionDeadline.Value);
            var consumer = new PublicPreparationHandoffV2Consumer(
                fixture.Host.Services.GetRequiredService<IStageHandoffQuery>(), fixture.Host.Services.GetRequiredService<ITraceQuery>(), elapsedClock);
            var error = await Assert.ThrowsAsync<TimeoutException>(() => consumer.CreateDetectionRequestAsync(
                start.RunId, receipt.Correlation.TrayId!.Value, receipt.Correlation.PlanRevision!, Guid.NewGuid(),
                receipt.Correlation.ConnectionEpoch, originalDetectionDeadline.Value, "expired-original-deadline",
                cancellationToken: token, recipeApplicationReceipt: receipt));
            Assert.Equal("ExistingDetectionDeadlineExpired", error.Message);
            Assert.True(receipt.WasCompletedInWindow); // timely binding does not refresh the next stage
            Assert.Equal(1, fixture.CommittedRecipeBindings);
        }
        if (caseId is "BA01-source/api" or "BA06-downstream/api-existing" or "BA06-downstream/api-none")
        {
            var beforeRevision = (await query.GetRunAsync(start.RunId, token))!.Revision;
            var beforeBindings = fixture.CommittedRecipeBindings;
            var bound = await fixture.Client.PostAsJsonAsync("/api/v1/recipes/bind",
                new RecipePlanRequest(start.RunId, "S1", ["P01"]), token);
            Assert.True(bound.StatusCode == HttpStatusCode.OK, await bound.Content.ReadAsStringAsync(token));
            using var body = JsonDocument.Parse(await bound.Content.ReadAsStringAsync(token));
            Assert.Equal("device-semantics/1", body.RootElement.GetProperty("deviceSchemaVersion").GetString());
            Assert.False(body.RootElement.TryGetProperty("recipeApplication", out _));
            var bindingResult = body.RootElement.GetProperty("bindingResult");
            Assert.Equal("Completed", bindingResult.GetProperty("outcome").GetString());
            Assert.Equal(source.ConfigurationId, bindingResult.GetProperty("budgetReference").GetProperty("id").GetString());
            Assert.Equal(source.Digest, bindingResult.GetProperty("budgetReference").GetProperty("digest").GetString());
            Assert.Equal(10000, bindingResult.GetProperty("budgetReference").GetProperty("budgetMs").GetInt32());
            var apiBindingId = bindingResult.GetProperty("bindingId").GetString();
            var committedAudits = (await RecipeBindingTestSupport.StagePayloads(fixture, start.RunId, token))
                .Select(e => JsonDocument.Parse(e).RootElement.Clone())
                .Where(e => e.TryGetProperty("kind", out var k) && k.GetString() == "RecipeApplicationReceiptObserved")
                .Select(e => e.GetProperty("receipt").Deserialize<RecipeBindingReceipt>(Json)!)
                .Where(r => r.BindingId == apiBindingId).ToArray();
            var apiReceipt = Assert.Single(committedAudits);
            Assert.Equal(source, apiReceipt.BudgetSource);
            var publicWindow = bindingResult.GetProperty("window");
            Assert.Equal(apiReceipt.Window.StartTick.ToString(System.Globalization.CultureInfo.InvariantCulture), publicWindow.GetProperty("startTick").GetString());
            Assert.Equal(apiReceipt.Window.DueTick.ToString(System.Globalization.CultureInfo.InvariantCulture), publicWindow.GetProperty("effectiveDueTick").GetString());
            Assert.Equal(apiReceipt.ReceivedTick.ToString(System.Globalization.CultureInfo.InvariantCulture), bindingResult.GetProperty("hostValidatedTick").GetString());
            Assert.Equal(new[] { "BindingIntent", "RecipePlanBound" }, bindingResult.GetProperty("requiredCommits").EnumerateArray()
                .Where(e => e.GetProperty("kind").GetString() != "RequiredCommunicationEvidence")
                .Select(e => e.GetProperty("kind").GetString()).ToArray());
            Assert.True(apiReceipt.WasCompletedInWindow);
            if (originalDetectionDeadline is { } originalDue)
                Assert.True(apiReceipt.Window.DeadlineUtc <= originalDue);
            else Assert.Equal(10000, (apiReceipt.Window.DeadlineUtc - apiReceipt.Window.StartedUtc).TotalMilliseconds);
            Assert.False(body.RootElement.GetProperty("productContinuationAuthorized").GetBoolean());
            Assert.NotEqual(receipt.BindingId, apiReceipt.BindingId);
            Assert.Equal(beforeBindings + 1, fixture.CommittedRecipeBindings);
            Assert.Single(apiReceipt.RequiredCommits);
            Assert.Equal(beforeRevision, (await query.GetRunAsync(start.RunId, token))!.Revision);
            Assert.Equal(originalHandoff, Assert.Single(await RecipeBindingTestSupport.HandoffPayloads(fixture, start.RunId, token)));
            Assert.Single(await RecipeBindingTestSupport.HandoffPayloads(fixture, start.RunId, token));
            receipt = apiReceipt;
        }
        using var historyResponse = JsonDocument.Parse(await fixture.Client.GetStringAsync($"/api/v1/station01/runs/{start.RunId:D}/evidence", token));
        var historyBinding = Assert.Single(historyResponse.RootElement.GetProperty("motionEvidence").EnumerateArray()
            .Select(e => e.GetProperty("facts")),
            e => e.TryGetProperty("recipeApplication", out var r) && r.ValueKind == JsonValueKind.Object && r.GetProperty("bindingId").GetString() == receipt.BindingId);
        var historicalApplication = historyBinding.GetProperty("recipeApplication");
        Assert.Equal("Completed", historicalApplication.GetProperty("outcome").GetString());
        Assert.Equal(source.Digest, historicalApplication.GetProperty("budgetReference").GetProperty("digest").GetString());
        Assert.Equal(receipt.Registration!.StartTick, historicalApplication.GetProperty("window").GetProperty("startTick").GetString());
        Assert.Equal(receipt.ReceivedTick.ToString(System.Globalization.CultureInfo.InvariantCulture), historicalApplication.GetProperty("hostValidatedTick").GetString());
        Assert.NotEmpty(historicalApplication.GetProperty("requiredCommits").EnumerateArray());
        Assert.NotEmpty(historicalApplication.GetProperty("diagnosticEvidenceReferences").EnumerateArray());
        var actualBound = Assert.Single(receipt.RequiredCommits, c => c.SavePurpose == "RecipePlanBound");
        var boundCommit = Assert.Single(historicalApplication.GetProperty("requiredCommits").EnumerateArray(),
            c => c.GetProperty("writeId").GetGuid() == actualBound.WriteId);
        Assert.Equal("ValidCurrent", boundCommit.GetProperty("receiptValidity").GetString());
        Assert.Equal(actualBound.ReceivedTick!.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            boundCommit.GetProperty("hostReceivedTick").GetString());
        if (caseId is "BA01-source/strict" or "BA06-downstream/strict-before-next" or "BA06-downstream/api-existing")
        {
            Assert.Contains(historicalApplication.GetProperty("window").GetProperty("applicableDeadlineReferences").EnumerateArray(),
                d => d.GetProperty("stage").GetString() == "Detection" && d.GetProperty("deadlineAtUtc").GetDateTimeOffset() == originalDetectionDeadline);
        }
        else if (caseId is "BA01-source/legacy" or "BA06-downstream/legacy" or "BA06-downstream/api-none")
            Assert.Empty(historicalApplication.GetProperty("window").GetProperty("applicableDeadlineReferences").EnumerateArray());
        await File.WriteAllTextAsync(Path.Combine(evidenceRoot, caseId.Replace('/', '-') + "-" + start.RunId.ToString("N") + ".json"),
            JsonSerializer.Serialize(new { caseId, scope = "HostFullSimulationAndRealSqliteComponent", fixture.StoreRoot,
                start, receipt, intent, originalHandoff, deadlineFact, frozenFact, source, latestVersion = "2.0.1",
                latestValue = loaded.Value.BusinessMs.RecipeApplication, handoffTiming, log.Path,
                tcpOrIndependentProcessAcceptance = false }, Json), token);
    }

    private static string? Kind(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("kind", out var kind)
            ? kind.GetString() : null;
    }

    [Theory]
    [InlineData("BA07-api-config/missing")]
    [InlineData("BA07-api-config/null")]
    [InlineData("BA07-api-config/zero")]
    [InlineData("BA07-api-config/negative")]
    [InlineData("BA07-api-config/fraction")]
    [InlineData("BA07-api-config/overflow")]
    [InlineData("BA07-api-config/missing-version")]
    [InlineData("BA07-api-config/version-conflict")]
    [InlineData("BA07-api-config/purpose")]
    [InlineData("BA07-api-config/source")]
    [InlineData("BA07-api-config/freeze")]
    [InlineData("BA07-api-config/production-unapproved")]
    [InlineData("BA07-api-config/nonfinite-json")]
    public async Task IndependentApiRejectsUnusableFrozenBudgetBeforeNewDeviceOrBindingWrites(string caseId)
    {
        var fault = caseId.Split('/')[1];
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        var token = watchdog.Token;
        await using var fixture = await Station01HostFixture.CreateAsync(recipeFixtureCode: "RC:R-S1-A-CAP:0.4.0-review");
        var request = new StartPublicRequest(Guid.NewGuid().ToString("N"), StartRunContextJson.Create(occupiedSlots: ["P01"]),
            new("s01-public-dev", "1.0.0"), new("s01-budget-dev", "3.0.0"), new("s01-sim-normal", "3.0.0"));
        var started = await fixture.Client.PostAsJsonAsync("/api/v1/station01/runs", request, token);
        Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);
        var start = (await started.Content.ReadFromJsonAsync<StartReceipt>(token))!;
        RunApiSnapshot snapshot;
        do
        {
            snapshot = (await fixture.Client.GetFromJsonAsync<RunApiSnapshot>(start.StatusUrl, token))!;
            Assert.True(snapshot.State != RunState.Blocked, JsonSerializer.Serialize(new { snapshot.State, snapshot.ErrorCode, snapshot.Events }, Json));
            if (snapshot.Events.Contains("PublicHandoffV2Committed")) break;
            await Task.Delay(20, token);
        } while (true);
        var query = RecipeBindingTestSupport.Query(fixture);
        var writes = (await query.GetWritesAsync(start.RunId, token)).ToArray();
        var frozen = Assert.Single(writes, w => Kind(w.PayloadJson) == "FrozenPublicConfiguration");
        var original = frozen.PayloadJson;
        var originalDigest = frozen.PayloadDigest;
        var document = JsonNode.Parse(original)!;
        var budget = JsonNode.Parse(document["budgetJson"]!.GetValue<string>())!;
        if (fault == "missing") budget["businessMs"]!.AsObject().Remove("recipeApplication");
        if (fault == "null") budget["businessMs"]!["recipeApplication"] = null;
        if (fault == "zero") budget["businessMs"]!["recipeApplication"] = 0;
        if (fault == "negative") budget["businessMs"]!["recipeApplication"] = -1;
        if (fault == "fraction") budget["businessMs"]!["recipeApplication"] = 1.5;
        if (fault == "overflow") budget["businessMs"]!["recipeApplication"] = 2147483648L;
        if (fault == "missing-version") budget.AsObject().Remove("version");
        if (fault == "version-conflict") budget["version"] = "2.0.1";
        if (fault == "purpose") budget["purpose"] = "Unapproved";
        if (fault == "source") budget["source"] = " ";
        if (fault == "production-unapproved")
        {
            budget["purpose"] = "Production";
            var pub = JsonNode.Parse(document["publicJson"]!.GetValue<string>())!;
            pub["purpose"] = "Production";
            document["publicJson"] = pub.ToJsonString();
            document["publicDigest"] = Digest(pub.ToJsonString());
        }
        var budgetJson = budget.ToJsonString();
        if (fault == "nonfinite-json") budgetJson = budgetJson.Replace("\"recipeApplication\":10000", "\"recipeApplication\":NaN", StringComparison.Ordinal);
        document["budgetJson"] = budgetJson;
        document["budgetDigest"] = Digest(budgetJson);
        if (fault == "freeze") document.AsObject().Remove("snapshotId");
        // Explicit corruption/historical-input fixture in this isolated Test database.
        // Keep the original bytes and restore them; no production/history rewrite.
        var injected = document.ToJsonString();
        await RecipeBindingTestSupport.ReplaceControlledFrozenInput(fixture, frozen.WriteId, injected, Digest(injected), token);
        var beforeBindings = fixture.CommittedRecipeBindings;
        var beforeMoves = fixture.Plc.MoveCommands;
        var beforeEvents = (await RecipeBindingTestSupport.StagePayloads(fixture, start.RunId, token)).Length;
        try
        {
            var response = await fixture.Client.PostAsJsonAsync("/api/v1/recipes/bind", new RecipePlanRequest(start.RunId, "S1", ["P01"]), token);
            var responseJson = await response.Content.ReadAsStringAsync(token);
            Assert.True(response.StatusCode == HttpStatusCode.Conflict, responseJson);
            Assert.Contains("RecipeBindingRejected", responseJson, StringComparison.Ordinal);
            Assert.Equal(beforeBindings, fixture.CommittedRecipeBindings);
            Assert.Equal(beforeMoves, fixture.Plc.MoveCommands);
            Assert.Equal(beforeEvents, (await RecipeBindingTestSupport.StagePayloads(fixture, start.RunId, token)).Length);
            Assert.Equal(writes.Length, (await query.GetWritesAsync(start.RunId, token)).Count);
            var root = Environment.GetEnvironmentVariable("GAODE_009_EVIDENCE_ROOT") ?? throw new InvalidOperationException("009EvidenceRootRequired");
            Directory.CreateDirectory(root);
            await File.WriteAllTextAsync(Path.Combine(root, "BA07-api-config-" + fault + "-" + start.RunId.ToString("N") + ".json"),
                JsonSerializer.Serialize(new { caseId, fault, fixture.StoreRoot, start.RunId, frozen.WriteId, originalDigest,
                    originalPayload = original, injectedPayload = injected, response.StatusCode, responseJson,
                    beforeBindings, afterBindings = fixture.CommittedRecipeBindings, beforeMoves, afterMoves = fixture.Plc.MoveCommands,
                    newBindingStageEvents = 0, scope = "ActualApiAndSqliteControlledInputFixture;SimulatedDevice" }, Json), token);
        }
        finally
        {
            await RecipeBindingTestSupport.ReplaceControlledFrozenInput(fixture, frozen.WriteId,
                original, originalDigest, CancellationToken.None);
        }
    }

    private static string Digest(string value) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));

    // Fault only the producer's frozen identity; use the real loader's schema,
    // bytes and values and the real Host freeze/admission path, never fake success.
    private sealed class InvalidFreezeInput(IPublicConfiguration inner) : IPublicConfiguration
    {
        public LoadedConfiguration<PublicConfiguration> SavePublicPositions(ConfigReference reference,
            FixedPoint threeD, FixedPoint manualLoading, string expectedDigest) =>
            inner.SavePublicPositions(reference, threeD, manualLoading, expectedDigest);
        public LoadedConfiguration<PublicConfiguration> LoadPublic(ConfigReference reference) => inner.LoadPublic(reference);
        public LoadedConfiguration<SimulationProfile> LoadSimulation(ConfigReference reference) => inner.LoadSimulation(reference);
        public LoadedConfiguration<BusinessBudget> LoadBudget(ConfigReference reference) =>
            inner.LoadBudget(reference) with { Digest = "" };
    }
}
