using Gaode.Application.Ports;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Xunit;

namespace Gaode.Contracts.Tests.Ports;

// Raw stage assertions migrated individually to Communication.Tests (migration T04-T11).
// The business contract requires correlated meaning, reliable position and durable references.
public sealed class PlcStageActionPortContractTests
{
    [Theory]
    [InlineData(PlcWorkflowStage.Sorting, DeviceCompletionMeaning.MaterialTransferred)]
    [InlineData(PlcWorkflowStage.UnloadPreparation, DeviceCompletionMeaning.UnloadPrepared)]
    [InlineData(PlcWorkflowStage.UnlockObservation, DeviceCompletionMeaning.Unlocked)]
    public void CompletionRequiresCurrentIdentityMeaningAndDurableEvidence(PlcWorkflowStage stage, DeviceCompletionMeaning meaning)
    {
        var result = Example(stage, meaning);
        Assert.True(result.IsCompleted);
        Assert.False((result with { ActionId = Guid.NewGuid() }).IsCompleted);
        Assert.False((result with { ConnectionEpoch = result.ConnectionEpoch + 1 }).IsCompleted);
        Assert.False((result with { Evidence = result.Evidence! with { Meaning = DeviceCompletionMeaning.RequestSubmitted } }).IsCompleted);
        Assert.False((result with { Evidence = result.Evidence! with { DiagnosticEvidenceReferences = [] } }).IsCompleted);
        Assert.False((result with { Evidence = result.Evidence! with { Observations = [] } }).IsCompleted);
        Assert.False((result with { Kind = StageActionKind.UnknownHeld, HoldsDevice = true }).IsCompleted);
    }
    [Fact]
    public void WrongPositionAndStaleObservationCannotCompleteTransfer()
    {
        var result = Example(PlcWorkflowStage.Sorting, DeviceCompletionMeaning.MaterialTransferred);
        var evidence = result.Evidence!;
        var position = evidence.Positions[0];
        Assert.False((result with { Evidence = evidence with { Positions = [position with
            { Actual = position.Actual with { ActualX = position.Target.X + 5 } }, evidence.Positions[1]] } }).IsCompleted);
        Assert.False((result with { Evidence = evidence with { Observations = [evidence.Observations[0] with
            { Reliability = DeviceReliability.Stale }] } }).IsCompleted);
        Assert.False((result with { Evidence = evidence with { Correlation = evidence.Correlation with { ActionId = Guid.NewGuid() } } }).IsCompleted);
    }
    [Theory]
    [InlineData(PlcWorkflowStage.Sorting, DeviceCompletionMeaning.MaterialTransferred)]
    [InlineData(PlcWorkflowStage.UnloadPreparation, DeviceCompletionMeaning.UnloadPrepared)]
    public void MissingOrDifferentTargetEvidenceCannotComplete(PlcWorkflowStage stage, DeviceCompletionMeaning meaning)
    {
        var result = Example(stage, meaning);
        var evidence = result.Evidence!;
        Assert.False((result with { Evidence = evidence with { Positions = [] } }).IsCompleted);
        var other = evidence.Positions[0] with { Target = evidence.Positions[0].Target with { Id = "other-target" } };
        Assert.False((result with { Evidence = evidence with { Positions = [other] } }).IsCompleted);
        if (stage == PlcWorkflowStage.Sorting)
            Assert.False((result with { Evidence = evidence with { Positions = [evidence.Positions[1]] } }).IsCompleted);
    }
    [Fact]
    public void UnavailableOriginCannotServeAsObservedCompletion()
    {
        var result = Example(PlcWorkflowStage.UnloadPreparation, DeviceCompletionMeaning.UnloadPrepared);
        Assert.False((result with { Evidence = result.Evidence! with
            { ExecutionOrigin = new(DeviceProvider.Unavailable, null, EvidenceQuality.Unknown) } }).IsCompleted);
    }
    private static PlcStageActionResult Example(PlcWorkflowStage stage, DeviceCompletionMeaning meaning)
    {
        var now = DateTimeOffset.UtcNow;
        var c = new ActionCorrelation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(),
            7, "unit-snapshot", "unit-plan", Guid.NewGuid(), "part-1", PhysicalSlotIndex: 7);
        var source = new FixedPoint("source", "1", 10, 20, "mm", "unit-frame", 30);
        var target = source with { Id = "target", X = 40 };
        var observation = new ObservationIdentity(Guid.NewGuid(), c.ConnectionEpoch, now, now, DeviceReliability.Reliable);
        var origin = new ExecutionOrigin(DeviceProvider.Simulated, "SemanticUnitFixture/1", EvidenceQuality.Derived);
        PositionReachedEvidence Position(FixedPoint p) => new(c, p, new(p.X, p.Y, p.Z,
            "unit-axes", p.Frame, "mm", observation, origin), 0.01);
        var request = new PlcStageActionRequest(c, Guid.NewGuid(), Guid.NewGuid(), stage, "unit-assignment",
            new(1, 100, "unit-clock", now, now.AddSeconds(1)), "unit-once", Guid.NewGuid(), 7,
            target, 0.01, "Test", source, target, "unit-reservation");
        var evidence = new DeviceActionEvidence(c, meaning, [observation], stage switch
        { PlcWorkflowStage.Sorting => [Position(source), Position(target)], PlcWorkflowStage.UnloadPreparation => [Position(target)], _ => [] },
            null, origin, [new(Guid.NewGuid(), Guid.NewGuid())]);
        return new(request, StageActionKind.Completed, c.ActionId, c.ConnectionEpoch, null, false, false, now, origin, evidence);
    }
}
