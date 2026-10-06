using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gaode.Application.Recipes;
using Gaode.Application.Station01;
using Gaode.Application.Workflow;
using Gaode.Domain.Station01;
using Gaode.Host.Api;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Gaode.Integration.Tests.Support;

// FullRun driver: independent owned Host/PLC/Worker, formal HTTP operations only.
// This helper never writes workflow state, completes a device action or supplies a detection result.
public sealed class RecipeExecution010RunHarness : IAsyncDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly List<(Process Process, Task Stdout, Task Stderr)> owned = [];
    private Process? hostSupervisor;
    private string hostName = "host", workspace = "", hostDll = "";
    private IDictionary<string, string> hostEnvironment = new Dictionary<string, string>();
    public HttpClient Client { get; } = new() { Timeout = TimeSpan.FromSeconds(15) };
    public HttpClient PlcClient { get; } = new() { Timeout = TimeSpan.FromSeconds(5) };
    private HttpClient Author { get; } = new() { Timeout = TimeSpan.FromSeconds(15) };
    public string Root { get; }
    public string StoreRoot => Path.Combine(Root, "store");
    public string RecipeRoot => Path.Combine(Root, "recipes");
    public JsonElement Fixture { get; }
    public HttpClient Teaching { get; } = new() { Timeout = TimeSpan.FromSeconds(15) };
    public string BrowserToken { get; private set; } = "";
    private RecipeExecution010RunHarness(string root, JsonElement fixture) { Root = root; Fixture = fixture; }

    public static async Task<RecipeExecution010RunHarness> CreateAsync(string root, string fixturePath)
    {
        var repo = Station01HostFixture.FindWorkspace();
        var allowed = Path.GetFullPath(Path.Combine(repo, "artifacts", "011-plc-interaction-update"));
        var comparisonRoot = Environment.GetEnvironmentVariable("GAODE_013_ATTEMPT_ROOT");
        var publicTrayRoot = Environment.GetEnvironmentVariable("GAODE_016_TEST_ROOT");
        if (publicTrayRoot is not null)
        {
            allowed = Path.GetFullPath(Path.Combine(repo, "artifacts", "016-public-tray-flow"));
            if (Path.GetFullPath(publicTrayRoot) != allowed || comparisonRoot is not null ||
                Environment.GetEnvironmentVariable("GAODE_014_JOINT_ROOT") is not null)
                throw new InvalidOperationException("016RootMustMatchCurrentWorkspace");
        }
        if (Environment.GetEnvironmentVariable("GAODE_014_JOINT_ROOT") is { } jointRoot)
        {
            allowed=Path.GetFullPath(Path.Combine(repo,"artifacts","014-012-joint"));
            if(Path.GetFullPath(jointRoot)!=allowed || comparisonRoot is not null)
                throw new InvalidOperationException("014JointRootMustMatchCurrentWorkspace");
        }
        if (comparisonRoot is not null)
        {
            allowed = Path.GetFullPath(comparisonRoot);
            var declaredRoot = Environment.GetEnvironmentVariable("GAODE_013_SOURCE_ROOT");
            if (!string.Equals(Path.GetFullPath(declaredRoot ?? ""), repo, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("013SourceRootDoesNotMatchTestAssembly");
        }
        root = Path.GetFullPath(root);
        if (!root.StartsWith(allowed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || Directory.Exists(root))
            throw new InvalidOperationException("New011EvidenceRootRequired");
        var fixture = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(fixturePath));
        if (fixture.GetProperty("schemaVersion").GetString() != "station01-fixture/2.0" || fixture.GetProperty("purpose").GetString() != "Test") throw new InvalidOperationException("ControlledTestInputRequired");
        Directory.CreateDirectory(root);
        var driver = new RecipeExecution010RunHarness(root, fixture);
        try
        {
            await driver.SaveAsync("input.json", new { fixturePath, fixture, startedUtc = DateTimeOffset.UtcNow, evidenceLevel = "FullRun" });
            var prep = driver.Start("store-prep", Path.Combine(repo, "backend/tools/Gaode.StorePrep/bin/Debug/net10.0/Gaode.StorePrep.dll"),
                [allowed, driver.StoreRoot], repo, new Dictionary<string, string>());
            await prep.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60));
            if (prep.ExitCode != 0) throw new InvalidOperationException("IsolatedStorePreparationFailed");
            var preparationDll = Path.Combine(repo, "backend/tools/Gaode.StorePrep/bin/Debug/net10.0/Gaode.StorePrep.dll");
            foreach (var (name, arguments) in new[] {
                ("recipe-prepare", new[] { "--prepare-recipes", allowed, driver.RecipeRoot }),
                ("recipe-seed", new[] { "--seed-test-recipes", allowed, driver.RecipeRoot,
                    Local("recipeInputPath"), Text("recipeInputSha256") }) })
            {
                var process = driver.Start(name, preparationDll, arguments, repo, new Dictionary<string, string>());
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60));
                if (process.ExitCode != 0) throw new InvalidOperationException("RecipeInputPreparationFailed:" + name);
            }

            var port = FreePort(); var apiPort = FreePort(); var plcApiPort = FreePort();
            driver.Client.BaseAddress = new($"http://127.0.0.1:{apiPort}");
            driver.PlcClient.BaseAddress = new($"http://127.0.0.1:{plcApiPort}");
            var token = Guid.NewGuid().ToString("N");
            var authorToken = Guid.NewGuid().ToString("N");
            var teachingToken = Guid.NewGuid().ToString("N");
            driver.BrowserToken = Guid.NewGuid().ToString("N");
            driver.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            driver.Author.BaseAddress = driver.Client.BaseAddress;
            driver.Author.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authorToken);
            driver.Teaching.BaseAddress = driver.Client.BaseAddress;
            driver.Teaching.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", teachingToken);
            var configRoot = Local("configRoot");
            if (publicTrayRoot is not null) {
                var runConfigRoot = Path.Combine(root, "config"); Directory.CreateDirectory(runConfigRoot);
                foreach (var input in Directory.EnumerateFiles(configRoot, "*.json")) File.Copy(input, Path.Combine(runConfigRoot, Path.GetFileName(input)));
                configRoot = runConfigRoot;
            }
            var simulationPath = Path.Combine(Local("configRoot"), "simulation.json");
            var simulationBytes = await File.ReadAllBytesAsync(simulationPath);
            var simulation = JsonSerializer.Deserialize<JsonElement>(simulationBytes);
            var motionDurationMs = simulation.GetProperty("stages").GetProperty("xyCompletion").GetProperty("delayMs").GetInt32();
            await driver.SaveAsync("virtual-motion-input.json", new { source = simulationPath,
                sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(simulationBytes)),
                motionDurationMs, actionDurationJitterMs = 0, purpose = "Test", businessBudgetsUnchanged = true });
            var env = new Dictionary<string, string> {
                ["ASPNETCORE_ENVIRONMENT"] = "VirtualPlc", ["Gaode__Tokens__Operator"] = token,
                ["Gaode__Tokens__ProcessEngineer"] = authorToken,
                ["Gaode__Tokens__EquipmentEngineer"] = teachingToken,
                ["Gaode__Tokens__SystemAdministrator"] = driver.BrowserToken,
                ["Gaode__Mode"] = "VirtualPlcIntegration", ["Gaode__TestRoot"] = driver.StoreRoot,
                ["Gaode__AllowedTestRoot"] = allowed, ["Gaode__ConfigRoot"] = configRoot,
                ["Gaode__SchemaRoot"] = Path.Combine(repo, "specs/001-station01-public-preparation/contracts"),
                ["Gaode__PlcPort"] = port.ToString(), ["Modbus__Port"] = port.ToString(),
                ["Gaode__PlcIoTimeoutMs"] = "1000", ["Simulation__HeartbeatTimeoutMs"] = "3000",
                ["Simulation__MotionDurationMs"] = motionDurationMs.ToString(),
                ["Simulation__ActionDurationJitterMs"] = "0",
                ["Dashboard__OpenBrowserOnStart"] = "false",
                ["Gaode__ImageManifestPath"] = Local("imageManifestPath"),
                ["Gaode__WorkerExecutablePath"] = Environment.GetEnvironmentVariable("GAODE_011_PYTHON")
                    ?? throw new InvalidOperationException("GAODE_011_PYTHONRequired"),
                ["Gaode__WorkerScriptPath"] = Path.GetFullPath(Path.Combine(repo, Text("workerScriptPath"))),
                ["Gaode__WorkerManifestPath"] = Local("workerManifestPath"),
                ["Gaode__PlcMechanicsPath"] = Local("plcMechanicsPath"),
                ["RecipeStore__DatabasePath"] = Path.Combine(driver.RecipeRoot, "recipes.db"),
                ["RecipeStore__ReadWriteTimeoutMs"] = "10000", ["RecipeStore__DbLockTimeoutSeconds"] = "5",
                ["Simulation__PutBackDurationMs"] = fixture.GetProperty("simulation").GetProperty("putBackDurationMs").GetInt32().ToString(),
                ["Simulation__FlipPutBackSafeZ"] = fixture.GetProperty("simulation").GetProperty("flipPutBackSafeZ").GetInt32().ToString()
            };
            foreach (var (key, prefix) in new[] { ("publicConfigRef", "Public"), ("budgetRef", "Budget"), ("simulationRef", "Simulation") })
            {
                env[$"Gaode__{prefix}Id"] = fixture.GetProperty(key).GetProperty("id").GetString()!;
                env[$"Gaode__{prefix}Version"] = fixture.GetProperty(key).GetProperty("version").GetString()!;
            }
            if (comparisonRoot is not null) env["GAODE_013_MEASUREMENT_ROOT"] = Path.Combine(root, "measurement");
            await driver.SaveAsync("resolved-roots.json", new { sourceRoot = repo,
                testAssembly = typeof(RecipeExecution010RunHarness).Assembly.Location,
                schemaRoot = env["Gaode__SchemaRoot"], configRoot = env["Gaode__ConfigRoot"], preparationDll,
                hostDll = Path.Combine(repo, "backend/src/Gaode.Host/bin/Debug/net10.0/Gaode.Host.dll"),
                plcDll = Path.Combine(repo, "VirtualPlc/bin/Debug/net10.0/VirtualPlc.dll"),
                worker = env["Gaode__WorkerScriptPath"], fixturePath });
            var plc = driver.Start("plc", Path.Combine(repo, "VirtualPlc/bin/Debug/net10.0/VirtualPlc.dll"),
                ["--urls", driver.PlcClient.BaseAddress.ToString()], Path.Combine(repo, "VirtualPlc"), env);
            await driver.WaitReadyAsync(plc, driver.PlcClient, "/health", r => r.GetProperty("status").GetString() == "ok");
            driver.workspace = repo;
            driver.hostDll = Path.Combine(repo, "backend/src/Gaode.Host/bin/Debug/net10.0/Gaode.Host.dll");
            driver.hostEnvironment = env;
            var host = driver.StartHost("host");
            await driver.WaitReadyAsync(host, driver.Client, "/api/v1/station01/status", r =>
                r.GetProperty("plc") is { ValueKind: JsonValueKind.Object } plcStatus &&
                plcStatus.GetProperty("connection").GetString() == "Connected" && ComponentsReady(r));
            var actualHost = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(Path.Combine(root, "host-process.json")));
            await driver.SaveAsync("processes.json", new { hostPid = actualHost.GetProperty("hostPid").GetInt32(),
                hostSupervisorPid = host.Id, plcPid = plc.Id, workerOwner = "Host/WorkerProcessSupervisor",
                hostStart = actualHost.GetProperty("startedUtc"), plcStart = plc.StartTime.ToUniversalTime(),
                apiPort, plcApiPort, port, hidden = true });
            return driver;
            string Text(string name) => fixture.GetProperty(name).GetString()!;
            string Local(string name)
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(fixturePath))!;
                var path = Path.GetFullPath(Path.Combine(directory, Text(name)));
                if (!path.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("JointInputOutsideDeclaredDirectory:" + name);
                return path;
            }
        }
        catch (Exception creation)
        {
            await driver.SaveAsync("creation-failure.json", new { type = creation.GetType().Name, creation.Message, creation.StackTrace,
                stage = "Preparation", finalAccepted = false });
            try { await driver.DisposeAsync(); }
            catch (Exception cleanup)
            {
                await driver.SaveAsync("creation-cleanup-failure.json", new { type = cleanup.GetType().Name, cleanup.Message,
                    cleanup.StackTrace, primaryFailure = "creation-failure.json", normalShutdownConfirmed = false });
            }
            throw; // Preserve the original creation failure, never classify cleanup as the first cause.
        }
    }

    public async Task<Guid> RunToFinalAsync(string? pageEvidenceRoot = null)
    {
        var comparison = Environment.GetEnvironmentVariable("GAODE_013_ATTEMPT_ROOT") is not null;
        var pageRoot = comparison ? await StartBackendObserverAsync() : await WaitForPageObserverAsync(pageEvidenceRoot);
        if (comparison)
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            var idleStart = Stopwatch.GetTimestamp();
            var idleStartUtc = DateTimeOffset.UtcNow;
            await Task.Delay(TimeSpan.FromSeconds(60));
            await SaveAsync("idle-window.json", new { startTick = idleStart,
                endTick = idleStart + 60 * Stopwatch.Frequency, frequency = Stopwatch.Frequency,
                startUtc = idleStartUtc, endUtc = idleStartUtc.AddSeconds(60), warmupSeconds = 5 });
        }
        var catalog = await Author.GetFromJsonAsync<JsonElement>("/api/v1/recipes/catalog");
        var selected = Assert.Single(catalog.GetProperty("items").EnumerateArray(), item =>
            item.GetProperty("fCode").GetString() == Fixture.GetProperty("fCode").GetString());
        var original = await ReadRecipeAsync(selected.GetProperty("recipeId").GetString()!, "recipe-original");
        var beforeBind = await SaveAndReadRecipeAsync(original.Definition, original.Tag,
            Fixture.GetProperty("saveIsolation").GetProperty("beforeBindQualityProfile").GetString()!, "recipe-before-bind");
        var context = new { schemaVersion = "station01-start-run-context/2.0", trayId = Guid.NewGuid(),
            stationId = Guid.NewGuid(), lineId = Guid.NewGuid(), scenarioId = Fixture.GetProperty("scenarioId"),
            occupiedSlots = Fixture.GetProperty("occupiedSlots"), purpose = "Test",
            expectedRecipeRef = new { recipeId = original.Definition.RecipeId, version = original.Definition.Version,
                catalogDigest = selected.GetProperty("catalogDigest").GetString() } };
        var request = new { requestId = "011-" + Guid.NewGuid().ToString("N"), contextJson = JsonSerializer.Serialize(context, Json),
            publicConfigRef = Fixture.GetProperty("publicConfigRef"), budgetRef = Fixture.GetProperty("budgetRef"),
            simulationRef = Fixture.GetProperty("simulationRef") };
        await SaveAsync("request.json", request);
        var activityStart = Stopwatch.GetTimestamp();
        var activityStartUtc = DateTimeOffset.UtcNow;
        using var accepted = await Client.PostAsJsonAsync("/api/v1/station01/runs", request);
        await File.WriteAllTextAsync(Path.Combine(Root, "receipt.json"), await accepted.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        var receipt = (await accepted.Content.ReadFromJsonAsync<StartReceipt>())!;
        var frozen = await WaitForFrozenAsync(receipt.RunId);
        Assert.Equal(beforeBind.Definition.RecipeId, frozen.Plan.RecipeId);
        Assert.Equal(beforeBind.Definition.Version, frozen.Plan.RecipeVersion);
        Assert.Equal(beforeBind.Definition.DefinitionDigest, frozen.Plan.DefinitionDigest);
        Assert.Equal(beforeBind.Definition.QualityProfile, frozen.Plan.QualityProfile);
        var beforeUpdate = (await Client.GetFromJsonAsync<RunApiSnapshot>($"/api/v1/station01/runs/{receipt.RunId:D}"))!;
        Assert.NotEqual(RunState.Completed, beforeUpdate.State);
        var afterBind = await SaveAndReadRecipeAsync(beforeBind.Definition, beforeBind.Tag,
            Fixture.GetProperty("saveIsolation").GetProperty("afterBindQualityProfile").GetString()!, "recipe-after-bind");
        Assert.Equal(JsonSerializer.Serialize(frozen, Json), JsonSerializer.Serialize(await WaitForFrozenAsync(receipt.RunId), Json));
        await SaveAsync("binding-isolation.json", new { receipt.RunId, beforeUpdate.ObservedRevision,
            original = original.Definition, savedBeforeF = beforeBind.Definition, frozen,
            savedAfterBinding = afterBind.Definition, evidenceLevel = "ActualHttpAndCommittedSqlite" });
        var awaiting = await WaitForAsync(receipt.RunId, RunState.AwaitingManualRemoval, TimeSpan.FromMinutes(5));
        Assert.Equal(frozen.PlanRevision, awaiting.PlanRevision);
        var ready = await EvidenceAsync(receipt.RunId, "evidence-ready.json");
        Assert.Null(ready.FinalSourceMatrix);
        Assert.NotNull(ready.WholeTrayCompletionId);
        Assert.Equal("SoftwareLoopOnly", ready.ReadyForRemovalSourceMatrix!.Scope);
        Assert.Empty(ready.ReadyForRemovalSourceMatrix.BlockedComponents);
        Assert.Equal(ComponentEvidenceSource.Virtual, Assert.Single(ready.ReadyForRemovalSourceMatrix.Components,
            c => c.Component == ComponentKind.Plc).Source);
        Assert.Equal(ComponentEvidenceSource.Test, Assert.Single(ready.ReadyForRemovalSourceMatrix.Components,
            c => c.Component == ComponentKind.Algorithm).Source);
        var aggregate = Assert.Single(ready.Stages, e => e.EventType == "WholeTrayCompleted");
        Assert.Equal(awaiting.PlanRevision, aggregate.PlanRevision);
        Assert.Equal("HostDerived", aggregate.Source); Assert.Equal("Derived", aggregate.RecordNature);
        Assert.Equal("Derived", aggregate.Quality);
        Assert.DoesNotContain(ready.Stages, e => e.EventType == "FinalUnloadCompleted");
        Assert.Equal("AwaitingFinalUnloadCompletion", ready.FinalResult);
        var confirmation = new ManualTrayRemovalApiRequest("011-confirm-" + receipt.RunId.ToString("N"),
            awaiting.ObservedRevision, "Controlled Test client confirms virtual tray removal");
        using var confirmed = await Client.PostAsJsonAsync($"/api/v1/station01/runs/{receipt.RunId:D}/manual-removal-confirmations", confirmation);
        Assert.Equal(HttpStatusCode.Accepted, confirmed.StatusCode);
        await File.WriteAllTextAsync(Path.Combine(Root, "confirmation.json"), await confirmed.Content.ReadAsStringAsync());
        var final = await WaitForAsync(receipt.RunId, RunState.Completed, TimeSpan.FromSeconds(15));
        Assert.Equal(TerminalOutcome.Completed, final.FinalOutcome);
        if (comparison) await SaveAsync("activity-window.json", new { startTick = activityStart,
            endTick = Stopwatch.GetTimestamp(), frequency = Stopwatch.Frequency, startUtc = activityStartUtc,
            endUtc = DateTimeOffset.UtcNow, receipt.RunId, boundary = "HTTP submission to observed committed Final; reconcile actual acceptance/Final timestamps from persisted facts" });
        using var replay = await Client.PostAsJsonAsync($"/api/v1/station01/runs/{receipt.RunId:D}/manual-removal-confirmations", confirmation);
        Assert.Equal(HttpStatusCode.Accepted, replay.StatusCode);
        var replayJson = await replay.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(replayJson.GetProperty("replay").GetBoolean());
        await SaveAsync("confirmation-replay.json", replayJson);
        var evidence = await EvidenceAsync(receipt.RunId, "evidence-final.json");
        Assert.Equal("FinalUnloadCompletion", evidence.FinalResult);
        Assert.Equal("SoftwareLoopOnly", evidence.FinalSourceMatrix!.Scope);
        Assert.Empty(evidence.FinalSourceMatrix.BlockedComponents);
        Assert.Equal(6, evidence.FinalSourceMatrix.Components.Count);
        var finalEvent = Assert.Single(evidence.Stages, e => e.EventType == "FinalUnloadCompleted");
        Assert.Equal("HostDerived", finalEvent.Source); Assert.Equal("Derived", finalEvent.Quality);
        var manualEvent = Assert.Single(evidence.Stages, e => e.EventType == "ManualTrayRemovalConfirmed");
        Assert.Equal("Test", manualEvent.Source); Assert.Equal("Derived", manualEvent.Quality);
        Assert.Equal(ComponentEvidenceSource.Test, Assert.Single(evidence.FinalSourceMatrix.Components,
            c => c.Component == ComponentKind.ManualActor).Source);
        Assert.Equal(JsonSerializer.Serialize(frozen, Json), JsonSerializer.Serialize(await WaitForFrozenAsync(receipt.RunId), Json));
        var latest = await ReadRecipeAsync(afterBind.Definition.RecipeId, "recipe-after-final");
        Assert.Equal(RecipeDefinitionSerialization.Serialize(afterBind.Definition), RecipeDefinitionSerialization.Serialize(latest.Definition));
        await SaveAsync("plc-changes.json", await PlcClient.GetFromJsonAsync<JsonElement>("/api/simulator/changes?after=0"));
        if (comparison) await File.WriteAllTextAsync(Path.Combine(pageRoot, "stop"), receipt.RunId.ToString("D"));
        else await CollectPageReferenceAsync(pageRoot, receipt.RunId);
        await VerifyNormalRestartAsync(receipt.RunId, frozen, afterBind.Definition);
        return receipt.RunId;
    }

    private async Task<string> StartBackendObserverAsync()
    {
        var observerRoot = Path.Combine(Root, "backend-observer");
        var pipeName = "gaode-013-observer-" + Guid.NewGuid().ToString("N");
        await using var pipe = new System.IO.Pipes.NamedPipeServerStream(pipeName,
            System.IO.Pipes.PipeDirection.Out, 1, System.IO.Pipes.PipeTransmissionMode.Byte,
            System.IO.Pipes.PipeOptions.Asynchronous | System.IO.Pipes.PipeOptions.CurrentUserOnly);
        var process = Start("backend-observer", Path.Combine(workspace, "scripts/013-api-observer.py"),
            [Client.BaseAddress!.ToString(), pipeName, observerRoot], workspace, new Dictionary<string, string>(),
            Environment.GetEnvironmentVariable("GAODE_011_PYTHON")!);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await pipe.WaitForConnectionAsync(timeout.Token);
        await using (var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true })
            await writer.WriteLineAsync(Author.DefaultRequestHeaders.Authorization!.Parameter.AsMemory(), timeout.Token);
        var readyPath = Path.Combine(observerRoot, "backend-observer-ready.json");
        while (!File.Exists(readyPath))
        {
            if (process.HasExited) throw new InvalidOperationException("013BackendObserverExited");
            await Task.Delay(100, timeout.Token);
        }
        var ready = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(readyPath, timeout.Token));
        Assert.Equal("ActualBackendApiObserved", ready.GetProperty("state").GetString());
        await SaveAsync("backend-observer-reference.json", new { readyPath, ready, evidenceLevel = "BackendLoadOnly" });
        return observerRoot;
    }

    private async Task<string> WaitForPageObserverAsync(string? owned016PageRoot = null)
    {
        var pageRoot = Path.GetFullPath(owned016PageRoot ?? Environment.GetEnvironmentVariable("GAODE_011_PAGE_EVIDENCE_ROOT")
            ?? throw new InvalidOperationException("012PageEvidenceRootRequired"));
        if (owned016PageRoot is not null && Environment.GetEnvironmentVariable("GAODE_016_TEST_ROOT") != Path.Combine(workspace, "artifacts", "016-public-tray-flow"))
            throw new InvalidOperationException("016OwnedPageRootRequired");
        var allowed = owned016PageRoot is not null ? Path.Combine(workspace, "artifacts", "016-public-tray-flow", "pages") : Environment.GetEnvironmentVariable("GAODE_014_JOINT_ROOT") is not null
            ? Path.GetFullPath(Path.Combine(workspace,"artifacts","014-012-joint","pages"))
            : Path.GetFullPath("E:/dzk/gaode-012-recipe-authoring/artifacts/recipe-authoring-012/joint-pages");
        if (!pageRoot.StartsWith(allowed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || Directory.Exists(pageRoot)) throw new InvalidOperationException("New012PageEvidenceRootRequired");
        var pipeName = "gaode-011-page-" + Guid.NewGuid().ToString("N");
        await using var pipe = new System.IO.Pipes.NamedPipeServerStream(pipeName,
            System.IO.Pipes.PipeDirection.Out, 1, System.IO.Pipes.PipeTransmissionMode.Byte,
            System.IO.Pipes.PipeOptions.Asynchronous | System.IO.Pipes.PipeOptions.CurrentUserOnly);
        await SaveAsync("page-connection.json", new { apiBaseUrl = Client.BaseAddress!.ToString().TrimEnd('/'),
            tokenPipeName = pipeName, evidenceRoot = pageRoot, observationTimeoutMs = 360000,
            driverRoot = Root, fixture = Fixture, state = "HostReadyAwaiting012ObserverBeforeRun" });
        using var preparation = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        await pipe.WaitForConnectionAsync(preparation.Token);
        await using (var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true })
            await writer.WriteLineAsync(Author.DefaultRequestHeaders.Authorization!.Parameter.AsMemory(), preparation.Token);
        var readyPath = Path.Combine(pageRoot, "ready.json");
        while (!File.Exists(readyPath)) await Task.Delay(100, preparation.Token);
        var ready = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(readyPath, preparation.Token));
        Assert.Equal("ObserverReadyBeforeRun", ready.GetProperty("state").GetString());
        Assert.Equal(Client.BaseAddress.ToString().TrimEnd('/'), ready.GetProperty("apiBaseUrl").GetString()!.TrimEnd('/'));
        await SaveAsync("page-ready-reference.json", new { path = readyPath, ready,
            evidenceLevel = "Actual012ObserverReadyOnly", observedUtc = DateTimeOffset.UtcNow });
        return pageRoot;
    }

    private async Task CollectPageReferenceAsync(string pageRoot, Guid runId)
    {
        var path = Path.Combine(pageRoot, "page-evidence.json");
        var until = DateTimeOffset.UtcNow.AddSeconds(45);
        while (!File.Exists(path) && DateTimeOffset.UtcNow < until) await Task.Delay(100);
        JsonElement? page = File.Exists(path)
            ? JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(path)) : null;
        var matched = page is { } actual && actual.GetProperty("runId").GetString() == runId.ToString("D")
            && actual.GetProperty("finalObserved").GetBoolean()
            && actual.GetProperty("status").GetString() == "CapturedPendingJointReconciliation";
        await SaveAsync("page-completion-reference.json", new { path, runId, matched,
            status = matched ? "CapturedPendingJointReconciliation" : "MissingOrIncomplete012PageEvidence", page });
        // This records the completion limitation after Final, never grants business progress.
    }

    private Process StartHost(string name)
    {
        hostName = name;
        hostSupervisor = Start(name, Path.Combine(workspace, "scripts/011-owned-host.py"),
            [Root, name, hostDll, Client.BaseAddress!.ToString()], workspace, hostEnvironment,
            Environment.GetEnvironmentVariable("GAODE_011_PYTHON") ?? throw new InvalidOperationException("GAODE_011_PYTHONRequired"), true);
        return hostSupervisor;
    }

    private async Task StopHostNormallyAsync()
    {
        var process = hostSupervisor ?? throw new InvalidOperationException("OwnedHostMissing");
        Assert.False(process.HasExited, "Owned Host supervisor exited before shutdown request");
        await process.StandardInput.WriteLineAsync("stop");
        await process.StandardInput.FlushAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45));
        var item = Assert.Single(owned, item => ReferenceEquals(item.Process, process));
        await Task.WhenAll(item.Stdout, item.Stderr);
        Assert.Equal(0, process.ExitCode);
        var stopped = JsonSerializer.Deserialize<JsonElement>(await File.ReadAllTextAsync(Path.Combine(Root, hostName + "-process.json")));
        Assert.True(stopped.GetProperty("normalShutdown").GetBoolean());
        var log = await File.ReadAllTextAsync(Path.Combine(Root, hostName + ".out.log"));
        Assert.Contains("Station01 Host关闭", log, StringComparison.Ordinal);
        foreach (var fragment in new[] { "Flows=True", "Runs=True", "Consumer=True", "Writes=True", "Resources=True", "AlgorithmsRemaining=0" })
            Assert.Contains(fragment, log, StringComparison.OrdinalIgnoreCase);
    }

    private async Task VerifyNormalRestartAsync(Guid runId, FrozenExecutionInputs frozen, RecipeDefinition saved)
    {
        await StopHostNormallyAsync();
        var host = StartHost("host-reread");
        await WaitReadyAsync(host, Client, "/api/v1/station01/status", ComponentsReady);
        var read = await ReadRecipeAsync(saved.RecipeId, "recipe-normal-restart");
        Assert.Equal(RecipeDefinitionSerialization.Serialize(saved), RecipeDefinitionSerialization.Serialize(read.Definition));
        var persisted = (await Client.GetFromJsonAsync<RunApiSnapshot>($"/api/v1/station01/runs/{runId:D}"))!;
        Assert.Equal(RunState.Completed, persisted.State);
        Assert.Equal(TerminalOutcome.Completed, persisted.FinalOutcome);
        Assert.Equal(frozen.PlanRevision, persisted.PlanRevision);
        Assert.Equal("Bound", persisted.RecipeState);
        Assert.Equal("FinalUnloadCompletion", persisted.WholeTaskState);
        Assert.Equal("Completed", persisted.SortingState);
        Assert.NotNull(persisted.RecipeExecution);
        Assert.Equal(frozen.Plan.RecipeVersion, persisted.RecipeExecution.Version);
        Assert.Equal(frozen.Plan.DefinitionDigest, persisted.RecipeExecution.DefinitionDigest);
        Assert.Equal(HandoffState.Ready, persisted.Handoff);
        Assert.Empty(persisted.AllowedActions);
        Assert.False(persisted.AutomaticContinuationAllowed);
        Assert.Equal(JsonSerializer.Serialize(frozen, Json), JsonSerializer.Serialize(await WaitForFrozenAsync(runId), Json));
        await SaveAsync("normal-restart-reread.json", new { runId, saved.RecipeId, saved.Version, saved.DefinitionDigest,
            frozen.PlanRevision, state = persisted.State.ToString(), outcome = persisted.FinalOutcome.ToString(),
            source = "Actual owned Host normal shutdown/restart; same separate SQLite stores; no new run command",
            shutdown = "host-process.json", shutdownLog = "host.out.log", startup = "host-reread-process.json" });
    }

    private async Task<(RecipeDefinition Definition, EntityTagHeaderValue Tag)> ReadRecipeAsync(string id, string evidence)
    {
        using var response = await Author.GetAsync("/api/v1/recipes/" + Uri.EscapeDataString(id));
        var body = await response.Content.ReadAsStringAsync();
        await SaveAsync(evidence + ".json", new { status = (int)response.StatusCode, etag = response.Headers.ETag?.ToString(), body });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Headers.ETag);
        var document = JsonSerializer.Deserialize<JsonElement>(body);
        return (RecipeDefinitionSerialization.Deserialize(document.GetProperty("definition").GetRawText()), response.Headers.ETag);
    }

    private async Task<(RecipeDefinition Definition, EntityTagHeaderValue Tag)> SaveAndReadRecipeAsync(
        RecipeDefinition previous, EntityTagHeaderValue tag, string qualityProfile, string evidence)
    {
        Assert.NotEqual(previous.QualityProfile, qualityProfile);
        var edited = previous with { QualityProfile = qualityProfile };
        if(evidence=="recipe-after-bind"&&Fixture.GetProperty("saveIsolation").TryGetProperty("afterBindEdit",out var edit))
        {
            var captureRef=previous.ExecutionPositions["s1"].PhysicalEntity.Coordinates.First().CaptureProfile!;
            var profiles=previous.CaptureProfiles.ToDictionary(p=>p.Key,p=>p.Value,StringComparer.Ordinal);
            profiles[captureRef]=profiles[captureRef] with {Settings=profiles[captureRef].Settings with {ExposureUs=edit.GetProperty("exposureUs").GetInt32()}};
            edited=edited with {CaptureProfiles=profiles,SortingGripperId=edit.GetProperty("sortingGripperId").GetInt32(),
                RotationLoadingGripperId=edit.GetProperty("rotationLoadingGripperId").GetInt32()};
        }
        var body = new JsonObject { ["requestId"] = "011-" + Guid.NewGuid().ToString("N"),
            ["definition"] = JsonNode.Parse(RecipeDefinitionSerialization.Serialize(edited)) };
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/recipes/" + Uri.EscapeDataString(previous.RecipeId))
            { Content = JsonContent.Create(body) };
        request.Headers.IfMatch.Add(tag);
        using var response = await Author.SendAsync(request);
        var actual = await response.Content.ReadAsStringAsync();
        await SaveAsync(evidence + "-save.json", new { request = body, expectedTag = tag.ToString(),
            status = (int)response.StatusCode, etag = response.Headers.ETag?.ToString(), body = actual });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = RecipeDefinitionSerialization.Deserialize(JsonSerializer.Deserialize<JsonElement>(actual).GetProperty("definition").GetRawText());
        Assert.Equal(previous.RecipeId, saved.RecipeId);
        Assert.NotEqual(previous.Version, saved.Version);
        Assert.NotEqual(previous.DefinitionDigest, saved.DefinitionDigest);
        Assert.Equal(qualityProfile, saved.QualityProfile);
        var read = await ReadRecipeAsync(saved.RecipeId, evidence + "-read");
        Assert.Equal(response.Headers.ETag, read.Tag);
        Assert.Equal(RecipeDefinitionSerialization.Serialize(saved), RecipeDefinitionSerialization.Serialize(read.Definition));
        Assert.Equal(RecipeDefinitionSerialization.Serialize(edited with { Version = saved.Version,
            DefinitionDigest = saved.DefinitionDigest, CatalogDigest = saved.CatalogDigest }), RecipeDefinitionSerialization.Serialize(saved));
        return read;
    }

    private async Task<FrozenExecutionInputs> WaitForFrozenAsync(Guid runId)
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = Path.Combine(StoreRoot, "station01.test.db"),
            Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly }.ToString();
        await using var db = new Gaode.Infrastructure.Persistence.Station01DbContext(
            new DbContextOptionsBuilder<Gaode.Infrastructure.Persistence.Station01DbContext>().UseSqlite(connection).Options);
        var end = DateTimeOffset.UtcNow.AddMinutes(2);
        while (DateTimeOffset.UtcNow < end)
        {
            var handoff = await db.PublicPreparationHandoffsV2.AsNoTracking().SingleOrDefaultAsync(h => h.RunId == runId);
            if (handoff is not null)
            {
                var writes = await db.Writes.AsNoTracking().Where(w => w.RunId == runId).OrderBy(w => w.Revision).ToArrayAsync();
                var intent = Assert.Single(writes, w => HasKind(w.PayloadJson, "RecipePlanAndBindingIntent"));
                var bound = Assert.Single(writes, w => HasKind(w.PayloadJson, "RecipePlanBound"));
                var committed = JsonSerializer.Deserialize<PublicPreparationHandoffV2>(handoff.PayloadJson, Json)!;
                Assert.True(committed.IsComplete);
                Assert.Equal(handoff.Revision, committed.CommittedRevision);
                Assert.Equal(handoff.PayloadDigest, committed.PayloadDigest);
                Assert.True(intent.Revision < bound.Revision && bound.Revision <= handoff.Revision);
                var frozen = JsonSerializer.Deserialize<JsonElement>(intent.PayloadJson).GetProperty("frozenExecutionInputs")
                    .Deserialize<FrozenExecutionInputs>(Json)!;
                Assert.True(frozen.IsValid); Assert.Equal(runId, frozen.RunId);
                Assert.Equal(committed.PlanRevision, frozen.PlanRevision);
                return frozen;
            }
            var run = (await Client.GetFromJsonAsync<RunApiSnapshot>($"/api/v1/station01/runs/{runId:D}"))!;
            if (run.State is RunState.Blocked or RunState.RecoveryRequired)
                throw new InvalidOperationException($"Binding blocked: {run.State}: {run.ErrorCode}");
            await Task.Delay(250);
        }
        throw new TimeoutException("Current committed binding/handoff was not produced");
        static bool HasKind(string json, string kind)
        {
            var value = JsonSerializer.Deserialize<JsonElement>(json);
            return value.ValueKind == JsonValueKind.Object && value.TryGetProperty("kind", out var property) && property.GetString() == kind;
        }
    }

    public Task SaveAsync(string name, object? value) => File.WriteAllTextAsync(Path.Combine(Root, name), JsonSerializer.Serialize(value, Json));
    public async Task RestartFor016Async()
    {
        if (Environment.GetEnvironmentVariable("GAODE_016_TEST_ROOT") is null) throw new InvalidOperationException("016RootRequired");
        await StopHostNormallyAsync();
        await WaitReadyAsync(StartHost("host-reread"), Client, "/api/v1/station01/status", ComponentsReady);
    }
    private async Task<Station01RunEvidenceApi> EvidenceAsync(Guid id, string name)
    {
        using var response = await Client.GetAsync($"/api/v1/station01/runs/{id:D}/evidence");
        response.EnsureSuccessStatusCode();
        Assert.NotNull(response.Headers.ETag);
        var value = (await response.Content.ReadFromJsonAsync<Station01RunEvidenceApi>())!;
        await SaveAsync(name, value); return value;
    }
    private async Task<RunApiSnapshot> WaitForAsync(Guid id, RunState expected, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            var run = (await Client.GetFromJsonAsync<RunApiSnapshot>($"/api/v1/station01/runs/{id:D}"))!;
            await SaveAsync("run-latest.json", run);
            if (run.State == expected) return run;
            if (run.State is RunState.Blocked or RunState.RecoveryRequired) throw new InvalidOperationException($"{run.State}: {run.ErrorCode}");
            await Task.Delay(500);
        }
        throw new TimeoutException($"Run {id} did not reach {expected}");
    }
    private static bool ComponentsReady(JsonElement response) =>
        response.GetProperty("algorithm") is { ValueKind: JsonValueKind.Object } algorithm &&
        algorithm.GetProperty("state").GetString() == "Ready" &&
        response.GetProperty("camera") is { ValueKind: JsonValueKind.Object } camera &&
        camera.GetProperty("state").GetString() == "Ready";

    private async Task WaitReadyAsync(Process process, HttpClient client, string path, Func<JsonElement, bool> ready)
    {
        var end = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < end)
        {
            if (process.HasExited) throw new InvalidOperationException($"Owned process {process.Id} exited ({process.ExitCode})");
            try { if (ready(await client.GetFromJsonAsync<JsonElement>(path))) return; }
            catch (HttpRequestException) { }
            await Task.Delay(250);
        }
        throw new TimeoutException("Owned process readiness: " + path);
    }
    private Process Start(string name, string dll, string[] args, string cwd, IDictionary<string, string> environment,
        string executable = "dotnet", bool redirectInput = false)
    {
        var info = new ProcessStartInfo(executable) { WorkingDirectory = cwd, UseShellExecute = false,
            CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true,
            RedirectStandardError = true, RedirectStandardInput = redirectInput };
        info.ArgumentList.Add(dll); foreach (var arg in args) info.ArgumentList.Add(arg);
        foreach (var key in info.Environment.Keys.Where(k => k.StartsWith("Gaode__", StringComparison.OrdinalIgnoreCase) ||
            k.StartsWith("Recipes__", StringComparison.OrdinalIgnoreCase) || k.StartsWith("RecipeStore__", StringComparison.OrdinalIgnoreCase)).ToArray()) info.Environment.Remove(key);
        foreach (var pair in environment) info.Environment[pair.Key] = pair.Value;
        var process = Process.Start(info) ?? throw new InvalidOperationException("Owned process failed to start");
        owned.Add((process, Drain(process.StandardOutput, name + ".out.log"), Drain(process.StandardError, name + ".err.log")));
        return process;
    }
    private async Task Drain(StreamReader reader, string name)
    {
        await using var writer = File.CreateText(Path.Combine(Root, name));
        while (await reader.ReadLineAsync() is { } line)
        {
            await writer.WriteLineAsync(line);
            // Avoid a filesystem flush for every console line. Keep the first
            // explicit communication failure immediately visible; disposal
            // flushes every remaining raw line, including exception details.
            if (line.Contains("PLC failure latched at", StringComparison.Ordinal) ||
                line.Contains("PLC exchange failed at", StringComparison.Ordinal))
                await writer.FlushAsync();
        }
    }
    private static int FreePort() { var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop(); return port; }
    public async ValueTask DisposeAsync()
    {
        Exception? shutdownFailure = null;
        if (hostSupervisor is { HasExited: false })
        {
            try { await StopHostNormallyAsync(); }
            catch (Exception error) { shutdownFailure = error; }
        }
        foreach (var item in owned.AsEnumerable().Reverse())
        {
            if (!item.Process.HasExited) item.Process.Kill(entireProcessTree: true);
            await item.Process.WaitForExitAsync(); await Task.WhenAll(item.Stdout, item.Stderr); item.Process.Dispose();
        }
        owned.Clear(); Client.Dispose(); PlcClient.Dispose(); Author.Dispose(); Teaching.Dispose();
        if (shutdownFailure is not null) throw new InvalidOperationException("OwnedHostShutdownFailed", shutdownFailure);
    }
}
