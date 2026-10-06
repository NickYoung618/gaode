using System.Text.Json;
using Gaode.Application.Workflow;
using Gaode.Domain.Station01;
using Xunit;

namespace Gaode.Contracts.Tests.Workflow;

public sealed class ManualRemovalHistoryTests
{
    [Fact]
    public void OldFinalRetainsOriginalUnlockReferenceWithoutInventingCurrentAllowance()
    {
        var whole = new WholeTrayCompletionReference(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), "historical-plan", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var oldUnlock = Guid.NewGuid();
        var id = Guid.NewGuid();
        var json = JsonSerializer.Serialize(new { finalCompletionId = id, wholeTray = whole,
            unlockObservedEventId = oldUnlock, operatorId = "historical-operator", confirmedAtUtc = DateTimeOffset.UtcNow,
            confirmationStage = "ManualTrayRemovalConfirmation", confirmed = true, reason = "historical record",
            finalSourceMatrixId = Guid.NewGuid() });
        var value = JsonSerializer.Deserialize<FinalUnloadCompletion>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal(id, value.FinalCompletionId);
        Assert.Equal(oldUnlock, value.UnlockObservedEventId);
        Assert.True(value.Confirmed); // Preserve the original historical fact.
        Assert.Equal(Guid.Empty, value.ManualRemovalAllowedEventId);
        Assert.False(value.IsValid); // The old fact is not a new current-execution allowance.
        Assert.False(new ManualTrayRemovalConfirmationRequest(Guid.NewGuid(), whole,
            value.ManualRemovalAllowedEventId, "operator", DateTimeOffset.UtcNow, "request", "key",
            ComponentEvidence.NotYetRequired(ComponentKind.ManualActor)).IsValid);
    }
}
