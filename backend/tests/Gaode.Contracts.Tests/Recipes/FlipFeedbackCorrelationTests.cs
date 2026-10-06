using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Application.Workflow;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Xunit;

namespace Gaode.Contracts.Tests.Recipes;

// Business correlation only; raw handshake values are tested in communication tests.
public sealed class FlipFeedbackCorrelationTests
{
    [Fact]
    public void CurrentTransitionCompletionConfirmsOnlyItsOwnAction()
    {
        var (request, result) = Example();
        Assert.True(FaceEstablishment.Confirms(request, result));
        Assert.False(FaceEstablishment.Confirms(new PutBackRequest(request.Correlation, request.TransitionId,
            request.PositionEvidence, request.Window, request.IntentWriteId), result));
    }
    [Fact]
    public void OldActionCannotSatisfyNewAction()
    {
        var (request, result) = Example();
        Assert.False(FaceEstablishment.Confirms(request with { Correlation = request.Correlation with { ActionId = Guid.NewGuid() } }, result));
        Assert.False(FaceEstablishment.Confirms(request with { TransitionId = Guid.NewGuid() }, result));
        Assert.True(FaceEstablishment.Confirms(request, result));
    }
    [Fact]
    public void WrongCompletionMeaningStaleFeedbackAndEpochDoNotComplete()
    {
        var (request, result) = Example();
        Assert.False(FaceEstablishment.Confirms(request, result with { Meaning = DeviceCompletionMeaning.PositionReached }));
        Assert.False(FaceEstablishment.Confirms(request, result with { Correlation = result.Correlation with { ConnectionEpoch = 8 } }));
        Assert.False(FaceEstablishment.Confirms(request, result with { Observations = result.Observations.Select(o => o with { Reliability = DeviceReliability.Stale }).ToArray() }));
        Assert.False(FaceEstablishment.Confirms(request, result with { TransitionId = null }));
    }
    private static (FlipRequest, DeviceActionEvidence) Example()
    {
        var now = DateTimeOffset.UtcNow;
        var c = new ActionCorrelation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(), 7,
            "component", "plan/1", Guid.NewGuid(), "part", PhysicalSlotIndex: 3);
        var observed = new ObservationIdentity(Guid.NewGuid(), 7, now, now, DeviceReliability.Reliable);
        var origin = new ExecutionOrigin(DeviceProvider.Simulated, "SemanticUnitFixture/1", EvidenceQuality.Derived);
        var point = new FixedPoint("pick", "component", 10, 20, "mm", "component", 30);
        var position = new PositionReachedEvidence(c, point, new(10, 20, 30, "axes", "component", "mm", observed, origin), 0.01);
        var request = new FlipRequest(c, Guid.NewGuid(), "model", new RecipeTargetPose("motion", "1", "access"), position,
            new(1, 100, "component", now, now.AddSeconds(1)), Guid.NewGuid());
        var evidence = new DeviceActionEvidence(c, DeviceCompletionMeaning.FlipCompleted, [observed], [], null, origin,
            [new(Guid.NewGuid(), Guid.NewGuid())]) { TransitionId = request.TransitionId };
        return (request, evidence);
    }
}
