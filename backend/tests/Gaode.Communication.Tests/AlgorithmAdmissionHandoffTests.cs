using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gaode.Application.Algorithms;
using Gaode.Application.Configuration;
using Gaode.Application.Ports;
using Gaode.Application.Station01;
using Gaode.Application.Workflow;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Host.Composition;
using Gaode.Host.Lifecycle;
using Gaode.Infrastructure.Diagnostics;
using Gaode.Infrastructure.Media;
using Gaode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Gaode.Communication.Tests;

// One controlled handoff, both orderings and both Runtime entry paths. No hardware services start.
[Collection("CommunicationTcp")]
public sealed class AlgorithmAdmissionHandoffTests
{
    [Theory]
    [InlineData(AlgorithmRole.TrayPose, false)]
    [InlineData(AlgorithmRole.FDecode, false)]
    [InlineData(AlgorithmRole.Detection, false)]
    [InlineData(AlgorithmRole.TrayPose, true)]
    [InlineData(AlgorithmRole.FDecode, true)]
    [InlineData(AlgorithmRole.Detection, true)]
    public async Task ActualHostCloseAndAlgorithmEntryHaveOneOrder(AlgorithmRole role, bool permitFirst)
    {
        HostWorkerCapacity.Ensure();
        using var inputs = new ControlledCommissioningTests.Inputs();
        await inputs.PrepareStore("Test");
        var simulation = ReviewBusinessData.Read<SimulationProfile>("simulation.normal.json");
        var configRoot = inputs.Options.ConfigRoot;
        var publicJson = JsonNode.Parse(File.ReadAllText(Path.Combine(configRoot, "public.json")))!;
        publicJson["purpose"] = "Test";
        foreach (var binding in publicJson["bindings"]!.AsArray()) binding!["provider"] = "Simulated";
        File.WriteAllText(Path.Combine(configRoot, "public.json"), publicJson.ToJsonString());
        var budgetJson = JsonNode.Parse(File.ReadAllText(Path.Combine(configRoot, "budget.json")))!;
        budgetJson["purpose"] = "Test"; budgetJson.AsObject().Remove("recipeExecution");
        File.WriteAllText(Path.Combine(configRoot, "budget.json"), budgetJson.ToJsonString());
        File.WriteAllText(Path.Combine(configRoot, "simulation.json"), JsonSerializer.Serialize(simulation, Json));
        var options = inputs.Options with { Mode = "FullSimulation", PlcProvider = "Virtual", Cameras = null,
            CommissioningPath = null, CommissioningSha256 = null, PlcMechanicsPath = null, PlcFieldProfilePath = null,
            SimulationReference = new(simulation.Id, simulation.Version) };
        var port = new HeldInputPort();
        var services = new ServiceCollection(); services.AddLogging(); services.AddStation01(options);
        services.AddSingleton<IAlgorithmPort>(port);
        services.AddSingleton<Gaode.Application.Recipes.IRecipeCatalog>(new EmptyCatalog());
        services.Configure<HostOptions>(value => value.ShutdownTimeout = TimeSpan.FromSeconds(5));
        await using var provider = services.BuildServiceProvider();
        var host = provider.GetRequiredService<Station01HostedService>();
        await host.InitializePersistenceAsync(default);
        var databaseOptions = provider.GetRequiredService<DbContextOptions<Station01DbContext>>();
        var runId = Guid.NewGuid(); var trayId = Guid.NewGuid();
        var context = JsonSerializer.Serialize(new { schemaVersion = StartRunContext.CurrentSchemaVersion, trayId,
            stationId = Guid.NewGuid().ToString(), lineId = Guid.NewGuid().ToString(),
            scenarioId = inputs.Config.ExpectedRecipe.ScenarioId, occupiedSlots = new[] { "s1" }, purpose = "Test" }, Json);
        await using (var db = new Station01DbContext(databaseOptions))
        {
            db.Runs.Add(new() { RunId = runId, RequestId = runId.ToString(), SubjectId = "Test",
                ContextJson = context, Revision = 1, CreatedUtc = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        var media = provider.GetRequiredService<MediaStore>();
        var captureId = Guid.NewGuid(); MediaRef image;
        using (media.ReserveCapture(captureId, "Detection", 4))
            image = (await media.SaveAsync(runId, captureId, "Detection", "1", "1", [1, 2, 3, 4], "bin", "Test", default)) with { Purpose = "Test" };
        var imageJson = JsonSerializer.Serialize(image, Json);
        var saved = await provider.GetRequiredService<TraceWriter>().SubmitCritical(new(Guid.NewGuid(), runId, 1,
            WriteKind.Media, imageJson, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(imageJson))))).Completion;
        Assert.Equal(CommitState.Committed, saved.State);
        await media.MarkCommittedAsync(image, default);
        var config = new FrozenConfiguration(inputs.Public with { Purpose = "Test" },
            ReviewBusinessData.Budget() with { Purpose = "Test", BusinessMs = ReviewBusinessData.Budget().BusinessMs with {
                TrayPoseAlgorithm = 20000, FDecode = 20000, WorkerReleaseGrace = 20000 } },
            simulation, "{}", "{}", "{}", "Test", "Test", "Test", new Dictionary<string, string>(), "Test-T045");
        var run = new RunExecution(runId, Guid.NewGuid(), runId.ToString(), "Test", context, config,
            provider.GetRequiredService<TraceWriter>(), TimeProvider.System, Guid.NewGuid(), "Test-T045");
        run.AdoptInitialRevision(saved.CommittedRevision!.Value);
        var runtime = provider.GetRequiredService<AlgorithmRuntime>();
        var supervisor = provider.GetRequiredService<AlgorithmResourceSupervisor>();
        using var cancel = new CancellationTokenSource();
        using var barrier = new HandoffLogger(runId, permitFirst ? "PermissionGranted" : "EligibilityObserved");
        using var diagnostics = new RuntimeDiagnosticLogging(barrier);
        Task<AlgorithmOutcome?> Invoke() => CallAsync();
        async Task<AlgorithmOutcome?> CallAsync()
        {
            if (role != AlgorithmRole.Detection)
                return await runtime.InvokeAsync(run, role, captureId, [image], "Test-T045", cancel.Token);
            var request = RealAlgorithmManagedCallTests.Request(runId, [image], role, "Test-T045", "1", false);
            request = request with { Envelope = request.Envelope with { DueTick = request.Envelope.StartTick + 30 * TimeProvider.System.TimestampFrequency } };
            await using var call = await runtime.DispatchSynchronousAsync(request, trayId, 20000, 20000, 3000, _ => { }, cancel.Token);
            await call.Result;
            return null;
        }
        var invocation = Invoke();
        try
        {
            var ready = await Task.WhenAny(barrier.Reached.Task, invocation).WaitAsync(TimeSpan.FromSeconds(10));
            if (ready == invocation) await invocation; // surface an invalid fixture before waiting for its boundary
            Assert.Same(barrier.Reached.Task, ready);
            await barrier.Reached.Task;
            // Completes while handoff is paused: diagnostics/adapter/save are not inside the admission lock.
            await Task.Run(host.NotifyStopping).WaitAsync(TimeSpan.FromSeconds(3));
            await supervisor.FlushAsync(default);
            var original = Assert.Single(await new StageEventStore(databaseOptions).GetUnreclaimedResourcesAsync(0, 128, default));
            Assert.NotNull(original.ReleaseStartUtc); Assert.Equal("HostClosing", original.ReleaseTrigger);
            Assert.False(original.Reclaimed); Assert.Equal(0, port.Calls);
            if (permitFirst)
                Assert.Equal("Entered", Assert.Single(runtime.ExecutionEvidence).Dispatch);
            barrier.Resume.Set();
            if (permitFirst)
            {
                await port.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(1, port.Calls);
                cancel.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invocation);
                var held = Assert.Single(await new StageEventStore(databaseOptions).GetUnreclaimedResourcesAsync(0, 128, default));
                Assert.NotEqual("NotDispatched", held.Dispatch);
                Assert.False(held.InputsReleased); Assert.False(held.ExecutionEnded); Assert.False(held.Reclaimed);
                Assert.Equal(1, runtime.ActiveExecutions); Assert.Equal(1, media.ActiveLeases);
                SameDeadline(original, held);
                port.Exit.TrySetResult();
            }
            else
            {
                await Task.WhenAny(invocation, port.Entered.Task).WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(0, port.Calls);
                var error = await Record.ExceptionAsync(async () => await invocation);
                if (role == AlgorithmRole.Detection) Assert.IsType<AlgorithmNotDispatchedException>(error);
                else { Assert.Null(error); Assert.Equal("NotDispatched", (await invocation)!.DispatchEvidence); }
            }
            using var drain = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await runtime.WaitForIdleAsync(drain.Token);
            var rows = await new StageEventStore(databaseOptions).ReadAsync(runId, trayId, WholeTrayWorkflowStage.Detection, drain.Token);
            var final = rows.Where(value => value.EventType == StageEventType.AlgorithmLifecycleRecorded)
                .Select(value => JsonSerializer.Deserialize<AlgorithmResourceState>(value.PayloadJson, Json)!).Last();
            Assert.True(final.Reclaimed); SameDeadline(original, final);
            if (!permitFirst) Assert.Equal("NotDispatched", final.Dispatch);
            Assert.Equal(0, media.ActiveLeases);
            await host.StopAsync(drain.Token);
            Assert.True(host.LastShutdown!.ResourcesDrained);
            Assert.False(barrier.TimedOut);
            Assert.Contains(barrier.Events, value => value.GetProperty("step").GetString() == "AlgorithmAdmission" &&
                value.GetProperty("outcome").GetString() == "Closed" && value.GetProperty("facts").GetProperty("CallId").GetGuid() == final.CallId);
            Assert.Contains(barrier.Events, value => value.GetProperty("step").GetString() == "AlgorithmAdmission" &&
                value.GetProperty("outcome").GetString() == (permitFirst ? "PermissionGranted" : "Rejected") &&
                value.GetProperty("facts").GetProperty("CallId").GetGuid() == final.CallId);
            var evidence = Environment.GetEnvironmentVariable("GAODE_COMMISSIONING_EVIDENCE_ROOT");
            if (evidence is not null)
            {
                Directory.CreateDirectory(evidence);
                File.WriteAllText(Path.Combine(evidence, $"T045-{role}-{permitFirst}.json"), JsonSerializer.Serialize(new {
                    scope = "Test:actual-Host-close-Runtime-SQLite;no-hardware", role, permitFirst, port.Calls,
                    original, final, host.LastShutdown, diagnostics = barrier.Events.ToArray() }, Json));
            }
        }
        finally
        {
            barrier.Resume.Set(); cancel.Cancel(); port.Exit.TrySetResult();
            await Record.ExceptionAsync(async () => await invocation.WaitAsync(TimeSpan.FromSeconds(5)));
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await runtime.WaitForIdleAsync(cleanup.Token);
            await host.StopAsync(cleanup.Token);
        }
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private static void SameDeadline(AlgorithmResourceState expected, AlgorithmResourceState actual)
    {
        Assert.Equal(expected.ReleaseStartUtc, actual.ReleaseStartUtc); Assert.Equal(expected.ReleaseDueUtc, actual.ReleaseDueUtc);
        Assert.Equal(expected.ReleaseStartTick, actual.ReleaseStartTick); Assert.Equal(expected.ReleaseDueTick, actual.ReleaseDueTick);
    }
    private sealed class HeldInputPort : IAlgorithmPort
    {
        private int calls;
        public int Calls => Volatile.Read(ref calls);
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Exit { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ComponentExecutionOrigin Origin => new(ComponentEvidenceSource.Test, "Test:T045-held-input", "1");
        public int CallCount(AlgorithmRole role) => Calls;
        public ValueTask<AlgorithmDispatch> RequestAsync(AlgorithmRequest request, Action<AlgorithmEvent> events, CancellationToken token)
        {
            Assert.Equal("Test", request.Envelope.Purpose);
            Interlocked.Increment(ref calls); Entered.TrySetResult();
            events(new(request, AlgorithmEventKind.Accepted, WorkerSessionId: Guid.NewGuid()));
            return ValueTask.FromResult(new AlgorithmDispatch(Exit.Task));
        }
    }
    private sealed class EmptyCatalog : Gaode.Application.Recipes.IRecipeCatalog
    { public Gaode.Application.Recipes.RecipeCatalogSnapshot GetSnapshot() => new(Gaode.Application.Recipes.RecipeCatalogSnapshot.CurrentSchema, "Test:T045-no-replay", []); }
    private sealed class HandoffLogger(Guid runId, string pauseOutcome) : ILogger, IDisposable
    {
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Resume { get; } = new();
        public ConcurrentQueue<JsonElement> Events { get; } = new();
        public bool TimedOut;
        private int paused;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> format)
        {
            var text = format(state, error); if (!text.StartsWith("RuntimeFlow ", StringComparison.Ordinal)) return;
            using var document = JsonDocument.Parse(text[12..]);
            var value = document.RootElement;
            if (!value.TryGetProperty("runId", out var run) || run.ValueKind != JsonValueKind.String || run.GetGuid() != runId) return;
            Events.Enqueue(value.Clone());
            if (value.GetProperty("step").GetString() != "AlgorithmAdmission" || value.GetProperty("outcome").GetString() != pauseOutcome ||
                Interlocked.Exchange(ref paused, 1) != 0) return;
            Reached.TrySetResult(); TimedOut = !Resume.Wait(TimeSpan.FromSeconds(15));
        }
        public void Dispose() => Resume.Dispose();
    }
}
