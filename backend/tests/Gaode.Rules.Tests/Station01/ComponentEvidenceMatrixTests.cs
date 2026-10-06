using Gaode.Domain.Station01;
using Xunit;

namespace Gaode.Rules.Tests.Station01;

public sealed class ComponentEvidenceMatrixTests
{
    [Theory]
    [InlineData(ComponentEvidenceSource.Virtual)]
    [InlineData(ComponentEvidenceSource.Simulated)]
    [InlineData(ComponentEvidenceSource.Test)]
    public void AnyNonProductionComponentKeepsSoftwareLoopOnly(ComponentEvidenceSource source)
    {
        var evidence = ReadyForUnlockEvidence();
        evidence[ComponentKind.Plc] = Verified(ComponentKind.Plc, source);

        var result = ComponentEvidenceMatrix.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "plan-1",
            EvidenceMilestone.ReadyForUnlock, evidence.Values);

        Assert.True(result.IsComplete);
        Assert.Equal(EvidenceScope.SoftwareLoopOnly, result.Scope);
        Assert.True(result.ContainsNonProductionEvidence);
        Assert.DoesNotContain("Real", result.Scope.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ReadyForUnlockKeepsManualActorAsNotYetRequired()
    {
        var matrix = ComponentEvidenceMatrix.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "plan-1",
            EvidenceMilestone.ReadyForUnlock, ReadyForUnlockEvidence().Values);

        var manual = matrix.Components.Single(x => x.Component == ComponentKind.ManualActor);
        Assert.Equal(ComponentEvidenceState.NotYetRequired, manual.State);
        Assert.Null(manual.Source);
        Assert.True(matrix.IsComplete);
        Assert.Equal(EvidenceScope.ProductionCandidateNotAccepted, matrix.Scope);
    }

    [Theory]
    [InlineData(ComponentEvidenceState.Missing)]
    [InlineData(ComponentEvidenceState.Unknown)]
    [InlineData(ComponentEvidenceState.Unverifiable)]
    public void RequiredEvidenceStateBlocksCompletion(ComponentEvidenceState state)
    {
        var evidence = ReadyForUnlockEvidence();
        evidence[ComponentKind.Algorithm] = evidence[ComponentKind.Algorithm] with { State = state };

        var matrix = ComponentEvidenceMatrix.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "plan-1",
            EvidenceMilestone.ReadyForUnlock, evidence.Values);

        Assert.False(matrix.IsComplete);
        Assert.Contains(ComponentKind.Algorithm, matrix.BlockedComponents);
    }

    [Fact]
    public void FinalMilestoneRequiresAuthenticatedManualActorEvidence()
    {
        var evidence = ReadyForUnlockEvidence();
        var withoutActor = ComponentEvidenceMatrix.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "plan-1",
            EvidenceMilestone.FinalUnloadCompletion, evidence.Values);

        evidence[ComponentKind.ManualActor] = Verified(
            ComponentKind.ManualActor, ComponentEvidenceSource.AuthenticatedHuman);
        var withActor = ComponentEvidenceMatrix.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "plan-1",
            EvidenceMilestone.FinalUnloadCompletion, evidence.Values);

        Assert.False(withoutActor.IsComplete);
        Assert.Contains(ComponentKind.ManualActor, withoutActor.BlockedComponents);
        Assert.True(withActor.IsComplete);
    }

    private static Dictionary<ComponentKind, ComponentEvidence> ReadyForUnlockEvidence() => new()
    {
        [ComponentKind.Host] = Verified(ComponentKind.Host, ComponentEvidenceSource.Real),
        [ComponentKind.Plc] = Verified(ComponentKind.Plc, ComponentEvidenceSource.Real),
        [ComponentKind.Camera] = Verified(ComponentKind.Camera, ComponentEvidenceSource.Real),
        [ComponentKind.Light] = Verified(ComponentKind.Light, ComponentEvidenceSource.Real),
        [ComponentKind.Algorithm] = Verified(ComponentKind.Algorithm, ComponentEvidenceSource.Real),
        [ComponentKind.ManualActor] = ComponentEvidence.NotYetRequired(ComponentKind.ManualActor)
    };

    private static ComponentEvidence Verified(ComponentKind component, ComponentEvidenceSource source) =>
        new(component, ComponentEvidenceState.Verified, source, "Verified", "v1",
            ["evidence://component/1"], DateTimeOffset.UtcNow, "digest-1");
}
