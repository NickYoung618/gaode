using Gaode.Domain.Station01;
using Xunit;

namespace Gaode.Rules.Tests.Station01;

public sealed class CompletionPolicyTests
{
    [Fact]
    public void EveryPhysicalAndSaveGateIsNecessary()
    {
        var valid = new CompletionEvidence(true, true, true, true, true, true,
            true, true, true, true, true, true);
        Assert.True(CompletionPolicy.CanComplete(valid));
        Assert.False(CompletionPolicy.CanComplete(valid with { DeviceReady = false }));
        Assert.False(CompletionPolicy.CanComplete(valid with { MotionKnown = false }));
        Assert.False(CompletionPolicy.CanComplete(valid with { AllNecessarySaved = false }));
        Assert.False(CompletionPolicy.CanComplete(valid with { MediaSaved = false }));
    }
}
