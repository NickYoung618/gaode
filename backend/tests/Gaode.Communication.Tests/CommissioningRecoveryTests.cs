using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Motion;
using Gaode.Application.Ports;
using Gaode.Application.Station01;
using Gaode.Application.Recipes;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Devices.Plc;
using Gaode.Infrastructure.Diagnostics;
using Gaode.Infrastructure.Persistence;
using Gaode.Plc.Protocol;
using Gaode.Host.Composition;
using Gaode.Host.Lifecycle;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Gaode.Communication.Tests;

[Collection("CommunicationTcp")]
public sealed class CommissioningRecoveryTests
{
    private sealed class EmptyCatalog : IRecipeCatalog
    {
        public RecipeCatalogSnapshot GetSnapshot() => new(RecipeCatalogSnapshot.CurrentSchema, "OFFLINE-unused-in-recovery", []);
    }
    [Fact]
    public async Task FormalHostPersistenceRestartRestoresHeldRunThenDoesNotRestoreVerifiedCancelledRun()
    {
        using var inputs = new ControlledCommissioningTests.Inputs();
        await inputs.PrepareStore();
        await using var plc = new SiteProtocolTcpFixture { ExerciseConfirmedOperations = true, ReadyOnReset = 1 };
        foreach (var mb in new[] { 6064, 6076, 6084, 6088, 6092 }) { plc.SetWord(mb, 0); plc.SetWord(mb + 2, 0); }
        var mechanics = JsonSerializer.Deserialize<PlcMechanicalConfiguration>(File.ReadAllText(inputs.Options.PlcMechanicsPath!),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        mechanics = mechanics with { SiteOperations = new("plc-site-operations/1", "User:R4;OFFLINE-host-restart") { RestoresWorkpieceAndMechanisms = true } };
        File.WriteAllText(inputs.Options.PlcMechanicsPath!, JsonSerializer.Serialize(mechanics, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var options = inputs.Options with { PlcPort = plc.Port, PlcIoTimeoutMs = 1000 };
        ServiceProvider Provider()
        {
            var services = new ServiceCollection(); services.AddLogging(); services.AddStation01(options);
            services.AddSingleton<IRecipeCatalog>(new EmptyCatalog());
            return services.BuildServiceProvider();
        }
        using var budget = new CancellationTokenSource(20000);
        Guid oldRun;
        await using (var first = Provider())
        {
            var receipt = first.GetRequiredService<CommandRegistry>().Register("operator", "restart-old-request", "OFFLINE-start"); oldRun = receipt.RunId;
            var json = JsonSerializer.Serialize(new RunCreatedPayload(receipt.CommandId, "restart-old-request", "operator",
                "{\"purpose\":\"Commissioning\"}", "{}", "{}", "{}", "OFFLINE", "offline", "offline", "offline"), new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var saved = await first.GetRequiredService<ITraceWriter>().SubmitCritical(new(Guid.NewGuid(), oldRun, 0,
                WriteKind.RunCreated, json, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)))), budget.Token).Completion;
            Assert.Equal(CommitState.Committed, saved.State);
        }
        CommissioningResetResult result;
        await using (var second = Provider())
        {
            await second.GetRequiredService<Station01HostedService>().InitializePersistenceAsync(budget.Token);
            var coordinator = second.GetRequiredService<Station01Coordinator>();
            Assert.Equal(RunState.RecoveryRequired, coordinator.Query(oldRun)!.State);
            Assert.Equal(oldRun, second.GetRequiredService<CommandRegistry>().PhysicalOwner);
            var device = second.GetRequiredService<LatestProtocolPlcDevice>(); await device.StartAsync(budget.Token);
            result = await second.GetRequiredService<CommissioningRecoveryService>().ResetAsync("operator", budget.Token);
            Assert.True(result.RecoveryClosed); Assert.Equal(RunState.Cancelled, coordinator.Query(oldRun)!.State);
            await coordinator.StopConsumerAsync(budget.Token);
        }
        await using (var third = Provider())
        {
            await third.GetRequiredService<Station01HostedService>().InitializePersistenceAsync(budget.Token);
            var coordinator = third.GetRequiredService<Station01Coordinator>(); Assert.Equal(0, coordinator.ActiveRunCount);
            Assert.Null(third.GetRequiredService<CommandRegistry>().PhysicalOwner);
            var query = third.GetRequiredService<ITraceQuery>();
            Assert.NotNull(CommissioningRecoveryService.ReadProof(await query.GetRunAsync(oldRun, budget.Token), await query.GetWritesAsync(oldRun, budget.Token)));
            await coordinator.StopConsumerAsync(budget.Token);
        }
        Assert.Equal(1, plc.ResetEdges); Assert.Equal(0, plc.StartEdges); Assert.Equal(0, plc.MotionEdges);
        var evidence = Environment.GetEnvironmentVariable("GAODE_RECOVERY_EVIDENCE") ?? Path.GetTempPath(); Directory.CreateDirectory(evidence);
        File.Copy(Path.Combine(options.TestRoot, "station01.test.db"), Path.Combine(evidence, "formal-host-restart.db"), false);
        File.WriteAllText(Path.Combine(evidence, "formal-host-restart.json"), JsonSerializer.Serialize(new { environment = "OFFLINE_LOOPBACK",
            oldRun, result, hostInstances = 3, cancelledRunNotRehydrated = true, cameraWorkersStarted = false,
            plc.ResetEdges, plc.StartEdges, plc.MotionEdges }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private sealed class Fixture : IAsyncDisposable
    {
        internal string Root { get; } = Path.Combine(Environment.GetEnvironmentVariable("GAODE_RECOVERY_EVIDENCE") ??
            Path.GetTempPath(), "recovery-" + Guid.NewGuid().ToString("N"));
        internal SiteProtocolTcpFixture Plc { get; } = new() {
            ExerciseConfirmedOperations = true, ReadyOnReset = 0, PreserveIdleAxisFeedback = true };
        internal LatestProtocolPlcDevice Device { get; }
        internal TraceWriter Writer { get; }
        internal TraceQuery Query { get; }
        internal CommandRegistry Commands { get; } = new();
        internal Station01Coordinator Coordinator { get; } = new(16, 16, 16);
        internal ResourceLease Lease { get; } = new();
        internal CommissioningRecoveryService Recovery { get; }
        internal Guid RunId { get; private set; }
        internal bool Exited = true, Resources = true;
        private readonly DbContextOptions<Station01DbContext> options;
        private readonly IDisposable logging;
        internal Fixture(bool rejectCancel = false, bool confirmed = true, int resetBudgetMs = 2000)
        {
            Directory.CreateDirectory(Root);
            options = new DbContextOptionsBuilder<Station01DbContext>().UseSqlite($"Data Source={Path.Combine(Root, "run.db")};Pooling=False").Options;
            using (var db = new Station01DbContext(options)) db.Database.Migrate();
            Writer = new(options, TimeProvider.System, 32, (batch, _) => {
                if (rejectCancel && batch.Kind == WriteKind.Cancel)
                {
                    using var conflict = new Station01DbContext(options);
                    conflict.Runs.Where(r => r.RunId == batch.RunId).ExecuteUpdate(set => set.SetProperty(r => r.Revision, r => r.Revision + 1));
                }
                return Task.CompletedTask;
            });
            Query = new(options);
            logging = new RuntimeDiagnosticLogging(new ControlledCommissioningTests.FileLogger(Path.Combine(Root, "runtime.log")));
            Device = new(new PlcRuntimeOptions {
                Provider = "Real", Purpose = RuntimePurposes.RealDeviceCommissioning, Host = "127.0.0.1", Port = Plc.Port,
                IoTimeoutMs = 1000, HeartbeatTimeoutMs = 3000,
                Definition = ConfirmedMemoryLayout.Load().CreateDefinition(SiteProtocolAdaptationTests.Profile()),
                PositionBasis = new("OFFLINE_ONLY", "mm", RuntimePurposes.RealDeviceCommissioning, "OFFLINE:recovery-fixture"),
                SiteOperations = new("plc-site-operations/1", "User:R4;OFFLINE-fixture") { RestoresWorkpieceAndMechanisms = confirmed }
            }, .1);
            foreach (var mb in new[] { 6064, 6076, 6084, 6088, 6092 }) { Plc.SetWord(mb, 0); Plc.SetWord(mb + 2, 0); }
            Recovery = new(Device, new(Device, Device, Device, Device, Lease), Commands, Coordinator, Writer, Query,
                _ => Exited, () => Resources, resetBudgetMs, 2000);
            Coordinator.Start();
        }
        internal async Task Seed(bool rehydrated, CancellationToken ct)
        {
            var receipt = Commands.Register("operator", "failed-request", "OFFLINE original request"); RunId = receipt.RunId;
            var payload = new RunCreatedPayload(receipt.CommandId, "failed-request", "operator", "{\"purpose\":\"Commissioning\"}",
                "{}", "{}", "{}", "OFFLINE", "offline", "offline", "offline");
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            var saved = await Writer.SubmitCritical(new(Guid.NewGuid(), RunId, 0, WriteKind.RunCreated, json,
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)))), ct).Completion;
            Assert.Equal(CommitState.Committed, saved.State);
            Assert.True(Coordinator.TryRegister(new(RunId, "failed-request", "operator", RunState.RecoveryRequired, 1, 1,
                TerminalOutcome.None, false, ActionState.Unknown, CaptureState.Unknown, AlgorithmState.Error,
                SaveState.Committed, HandoffState.NotReady, null, null, null, [])));
            if (!rehydrated) { var action = Guid.NewGuid(); Assert.True(Lease.TryHold(RunId)); Assert.True(Lease.TryBeginAction(RunId, action)); Lease.MarkUnknown(action); }
            await Device.StartAsync(ct);
        }
        internal async Task CompleteReset(CancellationToken ct)
        {
            while (Plc.Byte(2009) == 0) await Task.Delay(10, ct);
            Plc.SetByte(6015, 0); await Task.Delay(300, ct); Plc.SetByte(6015, 1);
        }
        internal void Evidence(object result) => File.WriteAllText(Path.Combine(Root, "result.json"), JsonSerializer.Serialize(new {
            environment = "OFFLINE_LOOPBACK", result, Plc.ResetEdges, Plc.StartEdges, Plc.MotionEdges,
            writes = Plc.Writes.Select(w => new { w.Offset, w.Words, w.Accepted }) }, new JsonSerializerOptions { WriteIndented = true }));
        public async ValueTask DisposeAsync()
        {
            await Device.DisposeAsync(); await Plc.DisposeAsync(); await Writer.DisposeAsync();
            await Coordinator.StopConsumerAsync(CancellationToken.None); logging.Dispose();
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FullResetDurablyClosesLiveOrRehydratedOldRunAndAllowsExplicitNewRun(bool rehydrated)
    {
        using var budget = new CancellationTokenSource(15000);
        await using var f = new Fixture(); await f.Seed(rehydrated, budget.Token);
        f.Plc.ReadyOnReset = 1;
        foreach (var mb in new[] { 6040, 6042, 6044, 6046, 6048, 6060 }) f.Plc.SetWord(mb, 1);
        var result = await f.Recovery.ResetAsync("operator", budget.Token);
        Assert.Equal(0, f.Plc.ResetReadyZeroReads); Assert.True(f.Plc.ResetReadyOneReads > 0);
        Assert.Equal(0, f.Plc.Byte(2009));
        Assert.True(result.Reset && result.RecoveryClosed && result.ManualStartRequired);
        foreach (var mb in new[] { 6040, 6042, 6044, 6046, 6048, 6060 }) Assert.Equal(1, f.Plc.Word(mb));
        var stored = await f.Query.GetRunAsync(f.RunId, budget.Token); Assert.Equal(TerminalOutcome.Cancelled, stored!.Terminal);
        var proof = CommissioningRecoveryService.ReadProof(stored, await f.Query.GetWritesAsync(f.RunId, budget.Token)); Assert.NotNull(proof);
        Assert.False(f.Lease.Unknown); Assert.Null(f.Lease.Owner);
        Assert.Equal("Available", JsonSerializer.SerializeToElement(f.Commands.StartAdmission()).GetProperty("state").GetString());
        // New query instance proves SQLite readback, including after host-side state is lost.
        Assert.Empty(await f.Query.GetUnfinishedRunsAsync(budget.Token));
        await f.Recovery.ValidateRestartAsync("operator", new(f.RunId, proof!.RecoveryWriteId), budget.Token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Recovery.ValidateRestartAsync("operator", new(f.RunId, Guid.NewGuid()), budget.Token));
        var next = f.Commands.Register("operator", "explicit-new-request", "OFFLINE new request with recovery reference");
        Assert.NotEqual(f.RunId, next.RunId); Assert.Equal(1, f.Plc.ResetEdges);
        Assert.Equal(0, f.Plc.StartEdges); Assert.Equal(0, f.Plc.MotionEdges);
        Assert.Contains("ClosedAfterVerifiedReset", File.ReadAllText(Path.Combine(f.Root, "runtime.log")));
        f.Evidence(new { rehydrated, result, stored.Terminal, proof, next.RunId, automaticStart = false });
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActiveExecutionOrResourcesBlockBeforeAnyReset(bool activeExecution)
    {
        using var budget = new CancellationTokenSource(10000);
        await using var f = new Fixture(); await f.Seed(false, budget.Token);
        if (activeExecution) f.Exited = false; else f.Resources = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Recovery.ResetAsync("operator", budget.Token));
        Assert.Equal(0, f.Plc.ResetEdges); Assert.True(f.Lease.Unknown);
        Assert.Equal("Held", JsonSerializer.SerializeToElement(f.Commands.StartAdmission()).GetProperty("state").GetString());
        f.Evidence(new { activeExecution, resetDispatched = false });
    }
    [Fact]
    public async Task FailedCancellationSaveKeepsOldRunAndOwnersHeldDespitePhysicalReset()
    {
        using var budget = new CancellationTokenSource(10000);
        await using var f = new Fixture(rejectCancel: true); await f.Seed(false, budget.Token);
        var operation = f.Recovery.ResetAsync("operator", budget.Token); await f.CompleteReset(budget.Token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => operation);
        Assert.Equal(TerminalOutcome.None, (await f.Query.GetRunAsync(f.RunId, budget.Token))!.Terminal);
        Assert.True(f.Lease.Unknown); Assert.Equal(f.RunId, f.Commands.PhysicalOwner);
        Assert.Equal(0, f.Plc.StartEdges); f.Evidence(new { failedSave = true, released = false });
    }
    [Theory]
    [InlineData(6050, 2)]
    [InlineData(6052, 1)]
    [InlineData(6040, 2)]
    public async Task InvalidResetFeedbackBlocksRecoveryAndNewRun(int feedbackAddress, int feedback)
    {
        using var budget = new CancellationTokenSource(10000);
        await using var f = new Fixture(); await f.Seed(true, budget.Token); f.Plc.SetWord(feedbackAddress, (ushort)feedback);
        var operation = f.Recovery.ResetAsync("operator", budget.Token); await f.CompleteReset(budget.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        Assert.Equal(TerminalOutcome.None, (await f.Query.GetRunAsync(f.RunId, budget.Token))!.Terminal);
        Assert.Equal(f.RunId, f.Commands.PhysicalOwner); Assert.Equal(0, f.Plc.StartEdges);
        Assert.Equal(0, f.Plc.Byte(2009));
        var log = File.ReadAllText(Path.Combine(f.Root, "runtime.log"));
        Assert.Contains("AxisFeedbackValid", log); Assert.Contains("putBackFeedback", log);
        f.Evidence(new { feedbackAddress, feedback, released = false });
    }
    [Fact]
    public async Task ReadyOneWithoutThisResetOrWithoutRestorationPolicyNeverPassesRecovery()
    {
        using var budget = new CancellationTokenSource(10000);
        await using var f = new Fixture(confirmed: false); await f.Seed(true, budget.Token);
        Assert.False((await f.Device.ReadInitialStateAsync(budget.Token)).Passed);
        var operation = f.Recovery.ResetAsync("operator", budget.Token); await f.CompleteReset(budget.Token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => operation);
        Assert.Equal(f.RunId, f.Commands.PhysicalOwner); Assert.Equal(0, f.Plc.StartEdges);
        f.Evidence(new { policyMissing = true, released = false });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InterruptedResetRecordsCancellationObservationAndKeepsOldRunHeld(bool cancelRequest)
    {
        using var outer = new CancellationTokenSource(10000);
        using var request = new CancellationTokenSource();
        await using var f = new Fixture(resetBudgetMs: 1500); await f.Seed(true, outer.Token);
        var operation = f.Recovery.ResetAsync("operator", request.Token);
        while (f.Plc.Byte(2009) == 0) await Task.Delay(10, outer.Token);
        if (cancelRequest) request.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.WaitAsync(outer.Token));
        var log = File.ReadLines(Path.Combine(f.Root, "runtime.log"))
            .Single(x => x.Contains("\"step\":\"CommissioningRecovery\"") && x.Contains("\"outcome\":\"Blocked\""));
        var jsonStart = log.IndexOf("RuntimeFlow ", StringComparison.Ordinal) + "RuntimeFlow ".Length;
        var jsonEnd = log.IndexOf(" exception=", jsonStart, StringComparison.Ordinal);
        var entry = JsonSerializer.Deserialize<JsonElement>(log[jsonStart..jsonEnd]);
        var facts = entry.GetProperty("facts");
        Assert.Equal("PlcReset", facts.GetProperty("phase").GetString());
        Assert.Equal(cancelRequest, facts.GetProperty("requestCancellationObserved").GetBoolean());
        Assert.Equal(!cancelRequest, facts.GetProperty("resetBudgetExpired").GetBoolean());
        Assert.Equal(cancelRequest ? "Request" : "ResetBudget", facts.GetProperty("cancellationObservation").GetString());
        Assert.True(entry.GetProperty("elapsedMs").GetDouble() > 0);
        var writes = await f.Query.GetWritesAsync(f.RunId, outer.Token);
        Assert.Contains(writes, w => w.PayloadJson.Contains(facts.GetProperty("resetId").GetString()!));
        Assert.Null(CommissioningRecoveryService.ReadProof(await f.Query.GetRunAsync(f.RunId, outer.Token), writes));
        Assert.Equal(f.RunId, f.Commands.PhysicalOwner);
        Assert.Equal(1, f.Plc.Byte(2009)); Assert.Equal(1, f.Plc.ResetEdges);
        Assert.Equal(0, f.Plc.StartEdges); Assert.Equal(0, f.Plc.MotionEdges);
        f.Evidence(new { cancelRequest, facts, released = false });
    }
}
