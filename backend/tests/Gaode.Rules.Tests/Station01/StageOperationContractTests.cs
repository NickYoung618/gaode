using Gaode.Domain.Station01;
using Xunit;

namespace Gaode.Rules.Tests.Station01;

public sealed class StageOperationContractTests
{
    [Fact]
    public void IdempotencyReturnsReplayForSamePayloadAndConflictForChangedPayload()
    {
        var registry = new StageIdempotencyRegistry();

        Assert.Equal(IdempotencyDecision.New, registry.Register("run-1:sort:op-1", "digest-a"));
        Assert.Equal(IdempotencyDecision.Replay, registry.Register("run-1:sort:op-1", "digest-a"));
        Assert.Equal(IdempotencyDecision.Conflict, registry.Register("run-1:sort:op-1", "digest-b"));
    }

    [Fact]
    public void WholeTrayReferenceAndFinalUnloadCompletionRequirePersistedIdentity()
    {
        var reference = new WholeTrayCompletionReference(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), "plan-1", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var completion = new FinalUnloadCompletion(Guid.NewGuid(), reference, Guid.NewGuid(),
            "operator-1", DateTimeOffset.UtcNow, "ManualTrayRemovalConfirmation", true,
            "Tray removed and confirmed by operator", Guid.NewGuid());

        Assert.True(reference.IsValid);
        Assert.True(completion.IsValid);
        Assert.False((completion with { Confirmed = false }).IsValid);
    }
}
