using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Application.Station01;
using Gaode.Domain.Station01;
using Xunit;
using Gaode.Contracts.Tests.Support;

namespace Gaode.Contracts.Tests.Station01;

public sealed class PublicPreparationHandoffV2Tests
{
    [Fact]
    public async Task OnlyPersistedMatchingV2HandoffCanConstructDetectionRequest()
    {
        await using var fixture = await HandoffFixture.CreateAsync();

        var request = await new PublicPreparationHandoffV2Consumer(fixture.Query, (ITraceQuery)fixture.Query)
            .CreateDetectionRequestAsync(fixture.RunId, fixture.TrayId, fixture.Handoff.PlanRevision, Guid.NewGuid(), 11,
                DateTimeOffset.UtcNow.AddSeconds(120), "detection-1",
                motionConfiguration: fixture.Motion,
                recipeApplicationReceipt: fixture.Inputs.Receipt);

        Assert.True(request.IsValid);
        Assert.Equal(fixture.RunId, request.RunId);
        Assert.Equal(fixture.TrayId, request.TrayId);
        Assert.Equal(fixture.Handoff.PlanRevision, request.PlanRevision);
        Assert.Equal(2, request.InputMediaReferences.Count);
    }

    [Fact]
    public async Task InMemoryNotificationCannotReplaceCommittedHandoff()
    {
        await using var fixture = await HandoffFixture.CreateAsync(includeWrite: false);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new PublicPreparationHandoffV2Consumer(fixture.Query, (ITraceQuery)fixture.Query).CreateDetectionRequestAsync(
                fixture.RunId, fixture.TrayId, fixture.Handoff.PlanRevision, Guid.NewGuid(), 1,
                DateTimeOffset.UtcNow.AddMinutes(1), "key"));

        Assert.Equal("CommittedHandoffV2Required", error.Message);
    }

    [Fact]
    public async Task EmptyReferencesOrMissingCommittedWriteCannotProduceRequest()
    {
        await using var fixture = await HandoffFixture.CreateAsync(includeWrite: false);
        Assert.Null(await fixture.Query.GetCommittedV2Async(fixture.RunId, default));

        var invalid = fixture.Handoff with { EvidenceReferences = [] };
        Assert.False(invalid.IsComplete);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new PublicPreparationHandoffV2Consumer(fixture.Query, (ITraceQuery)fixture.Query).CreateDetectionRequestAsync(
                fixture.RunId, fixture.TrayId, fixture.Handoff.PlanRevision, Guid.NewGuid(), 1,
                DateTimeOffset.UtcNow.AddMinutes(1), "key"));
    }

    [Fact]
    public async Task WrongRunTrayOrPlanIsRejected()
    {
        await using var fixture = await HandoffFixture.CreateAsync();
        var consumer = new PublicPreparationHandoffV2Consumer(fixture.Query, (ITraceQuery)fixture.Query);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Create(consumer, Guid.NewGuid(), fixture.TrayId, fixture.Handoff.PlanRevision, fixture.Handoff));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Create(consumer, fixture.RunId, Guid.NewGuid(), fixture.Handoff.PlanRevision, fixture.Handoff));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Create(consumer, fixture.RunId, fixture.TrayId, "plan-8", fixture.Handoff));
    }

    [Theory]
    [InlineData("missing-or-uncommitted")]
    [InlineData("wrongCall")]
    [InlineData("unknownOrigin")]
    public async Task FSourceFactMustBeCommittedAndMatchCurrentCallForHandoff(string reason)
    {
        await using var fixture = await HandoffFixture.CreateAsync(invalidFSource: reason);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new PublicPreparationHandoffV2Consumer(fixture.Query, (ITraceQuery)fixture.Query)
                .CreateDetectionRequestAsync(fixture.RunId, fixture.TrayId, fixture.Handoff.PlanRevision,
                    Guid.NewGuid(), 11, DateTimeOffset.UtcNow.AddMinutes(2), "source-rejection",
                    motionConfiguration: fixture.Motion,
                    recipeApplicationReceipt: fixture.Inputs.Receipt));
        Assert.Equal("CommittedFSourceMissingOrMismatched", error.Message);
    }

    [Theory]
    [InlineData("3D", "CommittedTrayObservationMediaRequired")]
    [InlineData("F", "CommittedFSourceMissingOrMismatched")]
    public async Task ObservationOrCodeFactCannotReplaceItsCommittedMedia(string role, string expected)
    {
        await using var fixture = await HandoffFixture.CreateAsync(missingMedia: role);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new PublicPreparationHandoffV2Consumer(fixture.Query, (ITraceQuery)fixture.Query)
                .CreateDetectionRequestAsync(fixture.RunId, fixture.TrayId, fixture.Handoff.PlanRevision,
                    Guid.NewGuid(), 11, DateTimeOffset.UtcNow.AddMinutes(2), "media-rejection",
                    motionConfiguration: fixture.Motion, recipeApplicationReceipt: fixture.Inputs.Receipt));
        Assert.Equal(expected, error.Message);
    }

    private static Task<DetectionRequest> Create(PublicPreparationHandoffV2Consumer consumer,
        Guid runId, Guid trayId, string planRevision, PublicPreparationHandoffV2 handoff) =>
        consumer.CreateDetectionRequestAsync(runId, trayId, planRevision,
            Guid.NewGuid(), 1, DateTimeOffset.UtcNow.AddMinutes(1), "key",
            recipeApplicationReceipt: SemanticRecipeReceiptFixture.For(handoff));

}
