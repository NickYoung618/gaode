using Gaode.Application.Ports;
using Gaode.Application.Workflow;
using Xunit;

namespace Gaode.Contracts.Tests.Recipes;

public sealed class FaceResultAggregatorTests
{
    [Fact]
    public void RepeatedAbGroupsNeverFuseAcrossStageIdentity()
    {
        var aggregator = new FaceResultAggregator();
        var first = new FaceResultKey("same-object",1,1){StageId="stage:1"};
        var second = first with {StageId="stage:2"};
        Assert.Null(aggregator.Add(first,Image("A")));
        Assert.Null(aggregator.Add(second,Image("B")));
        Assert.Equal(2,aggregator.IncompleteKeys.Count);
        Assert.Equal("stage:1",aggregator.Add(first,Image("B"))!.Key.StageId);
        Assert.Equal("stage:2",aggregator.Add(second,Image("A"))!.Key.StageId);
        Assert.Empty(aggregator.IncompleteKeys);
    }
    [Fact]
    public void AbAndCdPairOnlyWithinSameObjectFaceAndHeightRound()
    {
        var aggregator = new FaceResultAggregator();
        var first = new FaceResultKey("object-1", 1, 1);
        var nextRound = new FaceResultKey("object-1", 1, 2);
        Assert.Null(aggregator.Add(first, Image("A")));
        Assert.Null(aggregator.Add(nextRound, Image("B")));
        Assert.Equal(2, aggregator.IncompleteKeys.Count);
        var pair = aggregator.Add(first, Image("B"));
        Assert.NotNull(pair);
        Assert.Equal("A", pair.First.Camera);
        Assert.Equal("B", pair.Second.Camera);
        Assert.Single(aggregator.IncompleteKeys);
        var cd = new FaceResultKey("object-2", 1, 1);
        Assert.Null(aggregator.Add(cd, Image("C")));
        Assert.Equal("D", aggregator.Add(cd, Image("D"))!.Second.Camera);
        Assert.Throws<InvalidDataException>(() => aggregator.Add(first, Image("A")));
    }

    private static FaceImageResult Image(string camera) => new(camera,
        new MediaRef(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Detection",
            "media/test.png", 32, "png", "Test/FixedImage", "plan", "point",
            "FileCompleted"), "OK", "worker://test");
}
