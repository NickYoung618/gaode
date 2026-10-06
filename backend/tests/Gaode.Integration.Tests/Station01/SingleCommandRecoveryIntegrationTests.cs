using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Application.Station01;
using Gaode.Domain.Station01;
using Gaode.Integration.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Gaode.Infrastructure.Persistence;
using Xunit;

namespace Gaode.Integration.Tests.Station01;

// USR-D replaces the old U05 same-operation attempt=2 scenario; old source/evidence retained.
public sealed partial class SingleCommandRecoveryIntegrationTests
{
    internal static async Task<(string Before,string After)> RunRestartLinkAsync(CommitState injected)
    {
        var writer = new RejectRestartLink(injected);
        await using var rig = await VirtualLoopTestRig.CreateAsync(services => {
            services.RemoveAll<ITraceWriter>();
            services.AddSingleton<ITraceWriter>(sp => { writer.Inner = sp.GetRequiredService<TraceWriter>(); return writer; });
        }, useCurrentRecipe: true);
        rig.InjectFault("MoveTimeout");
        var (request, accepted, receipt) = await rig.StartAsync();
        var interaction = rig.Host.Host.Services.GetRequiredService<FixedMoveRecoveryInteraction>();
        var end = DateTimeOffset.UtcNow.AddSeconds(35);
        while (interaction.Query(receipt.RunId) is null && DateTimeOffset.UtcNow < end) await Task.Delay(100);
        Assert.NotNull(interaction.Query(receipt.RunId));
        using var admin = rig.Host.ClientForRole("SystemAdministrator");
        async Task<RunApiSnapshot> Old() => (await admin.GetFromJsonAsync<RunApiSnapshot>($"/api/v1/station01/runs/{receipt.RunId:D}"))!;
        var reset = await admin.PostAsJsonAsync($"/api/v1/station01/runs/{receipt.RunId:D}/recovery-reset", new {
            requestId = "reset-link-gate", expectedRevision = (await Old()).ObservedRevision, reason = "Reset test fault" });
        Assert.True(reset.StatusCode == HttpStatusCode.Accepted, await reset.Content.ReadAsStringAsync());
        var check = await admin.PostAsJsonAsync($"/api/v1/station01/runs/{receipt.RunId:D}/recovery-checks", new {
            requestId = "initial-link-gate", expectedRevision = (await Old()).ObservedRevision,
            sameTray = true, loadingUnchanged = true, snapshotStillApplicable = true,
            reason = "Original Test tray verified", evidenceRefs = new[] { "test://original-tray" } });
        Assert.True(check.StatusCode == HttpStatusCode.Accepted, await check.Content.ReadAsStringAsync());
        var checkedState = interaction.QueryRestart(receipt.RunId)!;
        var before=await rig.SaveEvidenceAsync("usr-d-link-before-"+injected,request,accepted,receipt);
        var nextRequest = request with { RequestId = "explicit-link-save-gate", RestartFrom = new(receipt.RunId,
            checkedState.ResetId!.Value, checkedState.InitialCheckId!.Value, (await Old()).ObservedRevision) };
        var response = await admin.PostAsJsonAsync("/api/v1/station01/runs", nextRequest);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var next = (await response.Content.ReadFromJsonAsync<StartReceipt>())!;
        var blocked = await rig.WaitAsync(next.RunId, TimeSpan.FromSeconds(20), RunState.Blocked, RunState.RecoveryRequired);
        var after=await rig.SaveEvidenceAsync("usr-d-link-" + injected, nextRequest, response, next, new {
            injected, faultRunId = receipt.RunId, blocked.State, blocked.ErrorCode,
            rejection = writer.Rejections });
        Assert.Equal(1, writer.Rejections);
        Assert.Equal(TerminalOutcome.None, blocked.FinalOutcome);
        Assert.Null(interaction.QueryRestart(receipt.RunId)!.NewRunId);
        Assert.Null(interaction.QueryRestart(receipt.RunId)!.InitialCheckId);
        Assert.Contains("RecoveryLinkCommitUnconfirmed", interaction.QueryRestart(receipt.RunId)!.BlockedReasons);
        return (before,after);
    }

    private sealed class RejectRestartLink(CommitState injected) : ITraceWriter
    {
        public ITraceWriter Inner { get; set; } = null!;
        public int Rejections { get; private set; }
        public Task<CommitReceipt?> ReconcileAsync(Guid writeId, CancellationToken ct) => Inner.ReconcileAsync(writeId, ct);
        public QueuedWrite SubmitCritical(WriteBatch batch, CancellationToken cancellationToken = default,
            Gaode.Domain.Station01.ActionWindow? window = null)
        {
            using var doc = JsonDocument.Parse(batch.PayloadJson);
            if (batch.Kind != WriteKind.Audit || !doc.RootElement.TryGetProperty("kind", out var kind) || kind.GetString() != "RecoveryNewRunLinked")
                return Inner.SubmitCritical(batch, cancellationToken, window);
            Rejections++;
            var receipt = new CommitReceipt(batch.WriteId, batch.RunId, injected, null, TerminalOutcome.None, "ControlledRestartLink" + injected);
            return new(receipt with { State = CommitState.Queued }, Task.FromResult(receipt));
        }
    }

    [Fact]
    public async Task FaultRequiresResetVerifiedInitialStateAndExplicitCompleteNewRun()
    {
        await using var rig = await VirtualLoopTestRig.CreateAsync(useCurrentRecipe: true);
        rig.InjectFault("MoveTimeout");
        var (request, accepted, receipt) = await rig.StartAsync();
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        var interaction = rig.Host.Host.Services.GetRequiredService<FixedMoveRecoveryInteraction>();
        var end = DateTimeOffset.UtcNow.AddSeconds(35);
        while (interaction.Query(receipt.RunId) is null && DateTimeOffset.UtcNow < end) await Task.Delay(100);
        var pending = Assert.IsType<FailedMoveRecoveryPrompt>(interaction.Query(receipt.RunId));
        using var engineer = rig.Host.ClientForRole("EquipmentEngineer");
        using var admin = rig.Host.ClientForRole("SystemAdministrator");
        async Task<RunApiSnapshot> Current() => (await engineer.GetFromJsonAsync<RunApiSnapshot>($"/api/v1/station01/runs/{receipt.RunId:D}"))!;
        await rig.SaveEvidenceAsync("usr-d-before-reset", request, accepted, receipt, new { pending, noResend = true });
        async Task<HttpResponseMessage> Check(string id, bool sameTray) => await engineer.PostAsJsonAsync(
            $"/api/v1/station01/runs/{receipt.RunId:D}/recovery-checks", new {
                requestId = id, expectedRevision = (await Current()).ObservedRevision,
                sameTray, loadingUnchanged = true, snapshotStillApplicable = true,
                reason = "Observed original Test tray", evidenceRefs = new[] { "test-physical-original-task" } });
        Assert.Equal(HttpStatusCode.Conflict, (await Check("before-reset", true)).StatusCode);
        var reset = await engineer.PostAsJsonAsync($"/api/v1/station01/runs/{receipt.RunId:D}/recovery-reset",
            new { requestId = "actual-reset", expectedRevision = (await Current()).ObservedRevision,
                reason = "Reset both ends after consumed MoveTimeout" });
        Assert.True(reset.StatusCode == HttpStatusCode.Accepted, await reset.Content.ReadAsStringAsync());
        Assert.True(interaction.Query(receipt.RunId)!.ResetEpoch > pending.FailedEpoch);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync("/api/v1/station01/reset", null)).StatusCode);
        rig.InjectFault("ManualZoneOccupied");
        Assert.Equal(HttpStatusCode.Conflict, (await Check("unsafe-physical-initial", true)).StatusCode);
        rig.ClearControlledPhysicalFaultsForTest(); // Test occupant actually leaves; Host reset alone must not clear occupancy.
        var resetAgain = await engineer.PostAsJsonAsync($"/api/v1/station01/runs/{receipt.RunId:D}/recovery-reset",
            new { requestId = "reset-after-occupied-check", expectedRevision = (await Current()).ObservedRevision, reason = "Clear injected occupied Test zone" });
        Assert.Equal(HttpStatusCode.Accepted, resetAgain.StatusCode);
        var mediaStore = rig.Host.Host.Services.GetRequiredService<Gaode.Infrastructure.Media.MediaStore>();
        var capture = Guid.NewGuid();
        var input = await File.ReadAllBytesAsync(Path.Combine(Station01HostFixture.FindWorkspace(), "specs", "007-station01-integrated-loop", "fixtures", "images", "three-d.png"));
        Gaode.Application.Ports.MediaRef sample;
        using (mediaStore.ReserveCapture(capture, "3D", input.Length))
            sample = await mediaStore.SaveAsync(receipt.RunId, capture, "3D", "TestResourceGate", "TestResourceGate", input, "png", "Test/ActualLeaseInputNotDetectionResult", default);
        using (mediaStore.Lease(sample.MediaId, "ControlledOldInputConsumer")) {
            Assert.Equal(1, mediaStore.ActiveLeases);
            Assert.Equal(HttpStatusCode.Conflict, (await Check("old-resource-held", true)).StatusCode);
            Assert.Contains("SoftwareResourcesNotReleased", interaction.QueryRestart(receipt.RunId)!.BlockedReasons);
        }
        Assert.Equal(0, mediaStore.ActiveLeases);
        Assert.Equal(HttpStatusCode.Conflict, (await Check("wrong-tray", false)).StatusCode);
        Assert.Null(interaction.Query(receipt.RunId)!.CheckId);
        var checkedResponse = await Check("verified-initial", true);
        Assert.True(checkedResponse.StatusCode == HttpStatusCode.Accepted, await checkedResponse.Content.ReadAsStringAsync());
        var check = (await checkedResponse.Content.ReadFromJsonAsync<RecoveryCheckReceipt>())!;
        var continuation = await engineer.PostAsJsonAsync($"/api/v1/station01/runs/{receipt.RunId:D}/continue",
            new { requestId = "must-not-continue", expectedRevision = (await Current()).ObservedRevision, checkId = check.CheckId });
        Assert.Equal(HttpStatusCode.Conflict, continuation.StatusCode);
        Assert.Equal(0, rig.Host.Host.Services.GetRequiredService<IAlgorithmPort>().CallCount(AlgorithmRole.Height));
        var initial = interaction.QueryRestart(receipt.RunId)!;
        Assert.Equal("InitialReady", initial.Status);
        var nextRequest = request with { RequestId = "explicit-new-round",
            RestartFrom = new FaultRestartFrom(receipt.RunId, initial.ResetId!.Value,
                initial.InitialCheckId!.Value, (await Current()).ObservedRevision) };
        var nextResponse = await admin.PostAsJsonAsync("/api/v1/station01/runs", nextRequest);
        Assert.True(nextResponse.StatusCode == HttpStatusCode.Accepted, await nextResponse.Content.ReadAsStringAsync());
        var next = (await nextResponse.Content.ReadFromJsonAsync<StartReceipt>())!;
        Assert.NotEqual(receipt.RunId, next.RunId);
        Assert.NotEqual(receipt.CommandId, next.CommandId);
        var replay = await admin.PostAsJsonAsync("/api/v1/station01/runs", nextRequest);
        Assert.Equal(next.RunId, (await replay.Content.ReadFromJsonAsync<StartReceipt>())!.RunId);
        var reuse = await admin.PostAsJsonAsync("/api/v1/station01/runs", nextRequest with { RequestId = "second-consumer" });
        Assert.Equal(HttpStatusCode.Conflict, reuse.StatusCode);
        var ready = await rig.WaitAsync(next.RunId, TimeSpan.FromMinutes(4), RunState.AwaitingManualRemoval, RunState.Blocked, RunState.RecoveryRequired);
        await rig.SaveEvidenceAsync("usr-d-new-round-before-removal", nextRequest, nextResponse, next, new { ready.State, ready.ErrorCode });
        Assert.Equal(RunState.AwaitingManualRemoval, ready.State);
        var remove = await admin.PostAsJsonAsync($"/api/v1/station01/runs/{next.RunId:D}/manual-removal-confirmations",
            new { requestId = "new-round-removal", expectedRevision = ready.ObservedRevision, reason = "Observed unlocked and Test tray removed" });
        Assert.True(remove.StatusCode == HttpStatusCode.Accepted, await remove.Content.ReadAsStringAsync());
        var final = await rig.WaitAsync(next.RunId, TimeSpan.FromSeconds(15), RunState.Completed);
        var folder = await rig.SaveEvidenceAsync("usr-d-complete-new-round", nextRequest, nextResponse, next,
            new { final.State, faultRunId = receipt.RunId, newRunId = next.RunId });
        Assert.Equal(TerminalOutcome.Completed, final.FinalOutcome);
        Assert.Equal("FinalUnloadCompletion", final.WholeTaskState);
        Assert.Equal(receipt.RunId, final.FaultRestart!.FaultRunId);
        var old = await Current();
        Assert.Equal(RunState.RecoveryRequired, old.State);
        Assert.Equal(next.RunId, old.FaultRestart!.NewRunId);
        var algorithm = rig.Host.Host.Services.GetRequiredService<IAlgorithmPort>();
        Assert.Equal(1, algorithm.CallCount(AlgorithmRole.Height));
        Assert.Equal(1, algorithm.CallCount(AlgorithmRole.FDecode));
        var oldFolder = await rig.SaveEvidenceAsync("usr-d-old-evidence-retained", request, accepted, receipt, old);
        var oldWrites = await File.ReadAllTextAsync(Path.Combine(oldFolder, "writes.json"));
        Assert.Contains("RecoveryInitialCheckBlocked", oldWrites);
        Assert.Contains("RecoveryInitialCheckAccepted", oldWrites);
        Assert.Contains("RecoveryNewRunLinked", oldWrites);
        Assert.DoesNotContain("RecoverySingleResendAuthorized", oldWrites);
        var newWrites = await File.ReadAllTextAsync(Path.Combine(folder, "writes.json"));
        Assert.Contains("RecoveryFromFaultRun", newWrites);
        Assert.Contains("FixedXYZ3D", newWrites);
        Assert.DoesNotContain("\"attempt\":2", newWrites);
    }
}
