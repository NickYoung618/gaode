using System.Net;
using System.Text.Json.Nodes;
using Gaode.Application.Ports;
using Gaode.Application.Workflow;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Persistence;
using Gaode.Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Gaode.Integration.Tests.Station01;

public sealed class VirtualRecipeAndDetectionGateTests
{
    [Fact]
    public async Task FailedFirstThreeDMotionNeverGuessesSafeZOrStartsF()
    {
        await using var rig = await VirtualLoopTestRig.CreateAsync();
        rig.InjectFault("MoveTimeout");
        var (request, response, receipt) = await rig.StartAsync();
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var final = await rig.WaitAsync(receipt.RunId, TimeSpan.FromSeconds(35),
            RunState.Blocked, RunState.RecoveryRequired);
        Assert.Equal(TerminalOutcome.None, final.FinalOutcome);
        var algorithm = rig.Host.Host.Services.GetRequiredService<IAlgorithmPort>();
        Assert.Equal(0, algorithm.CallCount(AlgorithmRole.Height));
        Assert.Equal(0, algorithm.CallCount(AlgorithmRole.FDecode));
        await rig.SaveEvidenceAsync("three-d-motion-gate", request, response, receipt,
            new { fault = "MoveTimeout", final.State, final.ErrorCode, final.FinalOutcome,
                heightCalls = algorithm.CallCount(AlgorithmRole.Height),
                fCalls = algorithm.CallCount(AlgorithmRole.FDecode),
                disposition = "NoSafeZInference_NoFOrRecipeBinding" });
    }

    [Fact]
    public async Task IndependentWorkerUnmatchedFDoesNotReuseOldRecipe()
    {
        var manifest = await WorkerManifestAsync("unmatched-f", node =>
            node["fCode"] = "RC:NOT-APPROVED:0.0.0");
        await using var rig = await VirtualLoopTestRig.CreateAsync(workerManifestPath: manifest, useCurrentRecipe: true);
        var (request, response, receipt) = await rig.StartAsync();
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var final = await rig.WaitAsync(receipt.RunId, TimeSpan.FromSeconds(70),
            RunState.Blocked, RunState.RecoveryRequired);
        Assert.Equal(TerminalOutcome.None, final.FinalOutcome);
        Assert.Null(final.PlanRevision);
        var options = rig.Host.Host.Services.GetRequiredService<DbContextOptions<Station01DbContext>>();
        await using var db = new Station01DbContext(options);
        Assert.False(await db.StageEvents.AsNoTracking().AnyAsync(x => x.RunId == receipt.RunId &&
            x.Stage == WholeTrayWorkflowStage.Sorting.ToString()));
        await rig.SaveEvidenceAsync("unmatched-f", request, response, receipt,
            new { workerManifest = manifest, final.State, final.ErrorCode,
                final.RecipeState, final.PlanRevision, disposition = "NoStaleRecipe_NoSorting" });
    }

    [Fact]
    public async Task InvalidDetectionWorkerResultStopsWithoutDefaultOk()
    {
        var manifest = await WorkerManifestAsync("invalid-detection", node =>
            { node["detectionDisposition"] = new JsonArray("UNMAPPED"); node["fCode"] = "TEST-TRAY-0999"; });
        await using var rig = await VirtualLoopTestRig.CreateAsync(workerManifestPath: manifest, useCurrentRecipe: true);
        var (request, response, receipt) = await rig.StartAsync();
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var final = await rig.WaitAsync(receipt.RunId, TimeSpan.FromSeconds(155),
            RunState.AwaitingManualRemoval, RunState.Blocked, RunState.RecoveryRequired);
        var options = rig.Host.Host.Services.GetRequiredService<DbContextOptions<Station01DbContext>>();
        await using var db = new Station01DbContext(options);
        var stages = await db.StageEvents.AsNoTracking().Where(x => x.RunId == receipt.RunId).ToListAsync();
        await rig.SaveEvidenceAsync("detection-invalid", request, response, receipt,
            new { workerManifest = manifest, final.State, final.ErrorCode,
                terminal = stages.Where(x => x.EventType == StageEventType.Failed.ToString())
                    .Select(x => new { x.OperationId, x.Attempt, x.StageDeadlineUtc, x.ErrorCode }),
                disposition = "InvalidResultBlocked_NoHardcodedOK_NoFinalClaim" });
        Assert.Contains(stages, x => x.EventType == StageEventType.Failed.ToString() &&
            x.ErrorCode == "DetectionAlgorithmInvalid");
        Assert.DoesNotContain(stages, x => x.Stage == WholeTrayWorkflowStage.Sorting.ToString() &&
            x.EventType == StageEventType.Completed.ToString());
        Assert.Equal(RunState.Blocked, final.State);
        Assert.Equal(TerminalOutcome.None, final.FinalOutcome);
    }

    [Fact]
    public async Task LateIndependentDetectionWorkerBecomesFinitePendingAndSorts()
    {
        var script = Path.Combine(Station01HostFixture.FindWorkspace(), "backend", "tests",
            "Gaode.Integration.Tests", "Fixtures", "detection-late-worker.py");
        await using var rig = await VirtualLoopTestRig.CreateAsync(workerScriptPath: script, useCurrentRecipe: true);
        var (request, response, receipt) = await rig.StartAsync();
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var final = await rig.WaitAsync(receipt.RunId, TimeSpan.FromSeconds(155),
            RunState.AwaitingManualRemoval, RunState.Blocked, RunState.RecoveryRequired);
        var options = rig.Host.Host.Services.GetRequiredService<DbContextOptions<Station01DbContext>>();
        await using var db = new Station01DbContext(options);
        var stages = await db.StageEvents.AsNoTracking().Where(x => x.RunId == receipt.RunId).ToListAsync();
        await rig.SaveEvidenceAsync("detection-late-pending", request, response, receipt,
            new { workerScript = script, final.State, final.ErrorCode,
                pending = stages.Where(x => x.EventType == StageEventType.PendingRecorded.ToString())
                    .Select(x => new { x.OperationId, x.Attempt, x.StageDeadlineUtc, x.ErrorCode }),
                disposition = "FinitePendingThenFormalSorting_NoDefaultOK" });
        Assert.Contains(stages, x => x.EventType == StageEventType.PendingRecorded.ToString() &&
            x.ErrorCode is "DetectionTimedOut" or "StageDeadlineExceeded");
        Assert.Contains(stages, x => x.Stage == WholeTrayWorkflowStage.Sorting.ToString() &&
            x.EventType == StageEventType.Completed.ToString());
        Assert.DoesNotContain(stages, x => x.EventType == StageEventType.MappingFailed.ToString());
        Assert.Equal(RunState.AwaitingManualRemoval, final.State);
        Assert.Equal(TerminalOutcome.None, final.FinalOutcome);
    }

    private static async Task<string> WorkerManifestAsync(string scenario, Action<JsonNode> edit)
    {
        var root = Path.Combine(Gaode.Testing.ApprovedTestRoot.Resolve(Station01HostFixture.FindWorkspace()),
            "controlled-inputs", scenario + "-input-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var source = Path.Combine(Station01HostFixture.FindWorkspace(), "specs",
            "007-station01-integrated-loop", "examples", "virtual-algorithm.json");
        var node = JsonNode.Parse(await File.ReadAllTextAsync(source))!;
        edit(node);
        var target = Path.Combine(root, "worker-manifest.json");
        await File.WriteAllTextAsync(target, node.ToJsonString());
        return target;
    }
}
