using Gaode.Application.Ports;
using Gaode.Application.Workflow;
using Gaode.Application.Recipes;
using Gaode.Infrastructure.Persistence;
using Gaode.Integration.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Gaode.Integration.Tests.CommunicationFixtures;

public sealed class FormalHostCompositionTests
{
    [Theory]
    [InlineData("media-manifest.json")]
    [InlineData("media-manifest-q02.json")]
    public async Task FormalHostCompositionUsesOneDeviceAndRealStageAndEvidenceProducers(string imageManifest)
    {
        await using var rig = await VirtualLoopTestRig.CreateAsync(useCurrentRecipe: true, imageManifestPath: Path.Combine(Station01HostFixture.FindWorkspace(),
            "specs/008-recipe-driven-inspection/fixtures", imageManifest));
        var services = rig.Host.Host.Services;
        var device = services.GetRequiredService<Gaode.Infrastructure.Devices.Plc.LatestProtocolPlcDevice>();
        Assert.Same(device, services.GetRequiredService<IPlcStatePort>());
        Assert.Same(device, services.GetRequiredService<IPlcActionPort>());
        Assert.Same(device, services.GetRequiredService<IMotionPort>());
        Assert.Same(device, services.GetRequiredService<IAcquisitionCyclePort>());
        Assert.Same(device, services.GetRequiredService<IPhysicalHandlingPort>());
        Assert.Same(device, services.GetRequiredService<IPlcResetPort>());
        Assert.IsType<Gaode.Infrastructure.Devices.Plc.LatestProtocolStageActionAdapter>(
            services.GetRequiredService<IPlcStageActionPort>());
        Assert.IsType<Gaode.Application.Workflow.RecipeDetectionExecutor>(services.GetRequiredService<IDetectionPort>());
        Assert.IsType<StageEventStore>(services.GetRequiredService<IStageEventStore>());
        Assert.NotNull(services.GetRequiredService<SortingTargetAllocator>());
        Assert.NotNull(services.GetRequiredService<Gaode.Infrastructure.Devices.Plc.CommunicationEvidenceRecorder>());
        var port = services.GetRequiredService<IDetectionPort>();
        var request = new DetectionRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            WholeTrayWorkflowStage.Detection, Guid.NewGuid(), "missing-plan", 1, DateTimeOffset.UtcNow.AddSeconds(30),
            ["declared-input"], "Test", "missing-plan");
        var refused = await port.ExecuteAsync(request, default);
        Assert.Equal(DetectionResultKind.Failed, refused.Kind);
        Assert.Equal("FrozenDetectionPlanMissing", refused.ErrorCode);
        Assert.False(refused.IsRealAcceptance);
        // Production admission is separate from malformed input: change only purpose.
        var catalog = services.GetRequiredService<IRecipeCatalog>();
        var definition = Assert.IsType<RecipeDefinition>(RecipeBindingTestSupport.Match(catalog, "TEST-TRAY-0999", "S1"));
        Assert.True(RecipeAdmission.Evaluate(definition, ["P01"], "Test").Eligible);
        var production = RecipeAdmission.Evaluate(definition, ["P01"], "Production");
        Assert.False(production.Eligible);
        Assert.Equal("RecipeApprovalMissing", production.Reason);
        Assert.Equal(0, services.GetRequiredService<ICapturePort>().TriggerCount(CaptureRole.Detection));
        Assert.Equal(0, services.GetRequiredService<IAlgorithmPort>().CallCount(AlgorithmRole.Detection));
        // Missing objects still reject through the common executor, after the old
        // simulated service is deleted; no fabricated object or capture is allowed.
        var plan = RecipeRunPlanner.BuildExecutable(definition, request.TrayId.ToString(), ["P01"]);
        var noObjects = request with { Plan = plan, PlanRevision = RecipePlanRevision.Compute(plan),
            ExpectedObjects = [] };
        Assert.True((noObjects with { ExpectedObjects = [new("declared-object",
            new("declared-position", "1", 10, 20, "mm", "machine", 30), "BASE")] }).IsValid);
        Assert.False(noObjects.IsValid);
        var noResult = await port.ExecuteAsync(noObjects, default);
        Assert.Equal(DetectionResultKind.Failed, noResult.Kind);
        Assert.Empty(noResult.Objects);
        Assert.Equal(0, services.GetRequiredService<ICapturePort>().TriggerCount(CaptureRole.Detection));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (!device.Observe().HasReliableObservation || device.HeartbeatEdges == 0)
            await Task.Delay(20, deadline.Token);
        Assert.True(device.HeartbeatEdges > 0);
        Assert.NotEmpty(device.BusinessExchanges);
        Assert.NotEmpty(device.HeartbeatExchanges);
        // Actual composition/transport component evidence; the simulator is in this
        // test process. Independent-process verification remains separately required.
    }

}
