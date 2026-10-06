using Gaode.Application.Ports;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Xunit;

namespace Gaode.Contracts.Tests.Ports;

public sealed class StagePortContractTests
{
    [Fact]
    public void DetectionResultMustCorrelateOperationAndExposeStableObjects()
    {
        var request = DetectionRequest();
        var result = new DetectionPortResult(request, DetectionResultKind.Completed, "det-1",
            [new("part-1", new FixedPoint("p-1", "1", 1, 2, "mm", "tray", 3), "OK")],
            ResultSource.Real, ResultQuality.Measured, null, DateTimeOffset.UtcNow);

        Assert.True(request.IsValid);
        Assert.Equal(WholeTrayWorkflowStage.Detection, request.Stage);
        Assert.True(result.IsValid);
        Assert.True(result.IsRealAcceptance);
    }

    [Fact]
    public void FailureAndSimulatedResultsCannotBecomeRealCompletionEvidence()
    {
        var request = DetectionRequest();
        var failure = new DetectionPortResult(request, DetectionResultKind.Failed, null, [],
            ResultSource.Real, ResultQuality.Unknown, "AlgorithmTimeout", DateTimeOffset.UtcNow);
        var simulated = new DetectionPortResult(request, DetectionResultKind.Completed, "det-sim",
            [new("part-1", new FixedPoint("p-1", "1", 1, 2, "mm", "tray", 3), "OK")],
            ResultSource.Simulated, ResultQuality.Degraded, null, DateTimeOffset.UtcNow);

        Assert.True(failure.IsValid);
        Assert.False(failure.IsRealAcceptance);
        Assert.True(simulated.IsValid);
        Assert.False(simulated.IsRealAcceptance);
    }

    [Fact]
    public void AcceptedAndExecutingResultsDoNotRequireFailureCodes()
    {
        var request = DetectionRequest();
        Assert.True(new DetectionPortResult(request, DetectionResultKind.Accepted, null, [],
            ResultSource.Real, ResultQuality.Unknown, null, DateTimeOffset.UtcNow).IsValid);
        Assert.True(new DetectionPortResult(request, DetectionResultKind.Executing, null, [],
            ResultSource.Real, ResultQuality.Unknown, null, DateTimeOffset.UtcNow).IsValid);
    }

    [Fact]
    public void StageCompletionRequiresCurrentActionAndSemanticTransferEvidence()
    {
        // Former address/code assertions moved to independent protocol oracle and TCP handshake tests.
        var request = ActionRequest(PlcWorkflowStage.Sorting);
        var now = DateTimeOffset.UtcNow;
        var result = Gaode.Contracts.Tests.Support.SemanticStageFixture.Result(request, StageActionKind.Completed, now);
        var evidence = result.Evidence!;
        Assert.True(request.IsValid);
        Assert.True(result.IsCompleted);
        Assert.False((result with { ConnectionEpoch = request.ConnectionEpoch + 1 }).IsCorrelated);
        Assert.False((result with { ActionId = Guid.NewGuid() }).IsCompleted);
        Assert.False((result with { Evidence = null }).IsCompleted);
        Assert.False((result with { Evidence = evidence with { Meaning = DeviceCompletionMeaning.RequestSubmitted } }).IsCompleted);
    }

    [Fact]
    public void UnlockObservationRequiresWholeTrayReference()
    {
        Assert.False(ActionRequest(PlcWorkflowStage.UnlockObservation) with { WholeTrayCompletionId = null } is { IsValid: true });
        Assert.True(ActionRequest(PlcWorkflowStage.UnlockObservation) with { WholeTrayCompletionId = Guid.NewGuid() } is { IsValid: true });
    }

    [Fact]
    public void UnknownHeldIsNonRetryableAndRetainsDeviceOwnership()
    {
        var unknown = new UnknownHeld(Guid.NewGuid(), Guid.NewGuid(), PlcWorkflowStage.Sorting,
            Guid.NewGuid(), 7, "ConnectionEpochChanged", true, false);
        var result = new PlcStageActionResult(ActionRequest(PlcWorkflowStage.Sorting),
            StageActionKind.UnknownHeld, Guid.NewGuid(), 1, "ConnectionEpochChanged",
            true, false, DateTimeOffset.UtcNow, new(DeviceProvider.Simulated, "SemanticUnitFixture/1", EvidenceQuality.Unknown), null);

        Assert.True(unknown.IsValid);
        Assert.True(result.IsUnknownHeld);
    }

    [Fact]
    public void WorkflowAndPlcStageEnumsRemainSeparate()
    {
        Assert.Equal(
            [WholeTrayWorkflowStage.Detection, WholeTrayWorkflowStage.Sorting,
             WholeTrayWorkflowStage.UnloadPreparation, WholeTrayWorkflowStage.UnlockObservation,
             WholeTrayWorkflowStage.ManualTrayRemovalConfirmation, WholeTrayWorkflowStage.RecipeApplication,
             WholeTrayWorkflowStage.ManualRemovalAdmission],
            Enum.GetValues<WholeTrayWorkflowStage>());
        Assert.Equal(
            [PlcWorkflowStage.Sorting, PlcWorkflowStage.UnloadPreparation,
             PlcWorkflowStage.UnlockObservation,PlcWorkflowStage.TransferToRotation,PlcWorkflowStage.Rotate],
            Enum.GetValues<PlcWorkflowStage>());
        Assert.NotEqual(typeof(WholeTrayWorkflowStage), typeof(PlcWorkflowStage));
    }

    private static DetectionRequest DetectionRequest() => new(Guid.NewGuid(), Guid.NewGuid(),
        Guid.NewGuid(), Guid.NewGuid(), WholeTrayWorkflowStage.Detection, Guid.NewGuid(), "plan-1", 3,
        DateTimeOffset.UtcNow.AddSeconds(30), ["media-1"], "RealOrSimulated", "detect-key",
        ExpectedObjects:[new("part-1",new FixedPoint("p-1","1",1,2,"mm","tray",3),"OK")]);

    private static PlcStageActionRequest ActionRequest(PlcWorkflowStage stage)
    {
        var now = DateTimeOffset.UtcNow;
        var correlation = new ActionCorrelation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1,
            Guid.NewGuid(), 1, "snapshot/1", "plan-1", Guid.NewGuid(), "part-1", PhysicalSlotIndex: 1);
        return new(correlation, Guid.NewGuid(), Guid.NewGuid(), stage, "sha256:params",
            new(1, 100, "unit-clock", now, now.AddSeconds(30)), "action-key",
            stage == PlcWorkflowStage.UnlockObservation ? Guid.NewGuid() : null,
            stage == PlcWorkflowStage.Sorting ? 1 : null,
            SortingSource: stage == PlcWorkflowStage.Sorting ? new FixedPoint("P01", "test-v1", 1, 2, "mm", "SIM_MACHINE", 3) : null,
            SortingTarget: stage == PlcWorkflowStage.Sorting ? new FixedPoint("P14", "test-v1", 4, 5, "mm", "SIM_MACHINE", 6) : null,
            ReservationReference: stage == PlcWorkflowStage.Sorting ? "reserved/1" : null);
    }
}
