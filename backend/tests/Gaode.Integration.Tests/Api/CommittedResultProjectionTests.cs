using System.Text.Json;
using Gaode.Application.Workflow;
using Gaode.Application.Ports;
using Gaode.Domain.Station01;
using Gaode.Host.Api;
using Xunit;

namespace Gaode.Integration.Tests.Api;

public sealed class CommittedResultProjectionTests
{
    [Fact]
    public void EvidenceProjectionPreservesCommittedActualReadbackInsteadOfCopyingTarget()
    {
        var project = typeof(QueryEndpoints).GetMethod("MotionFacts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var payload = JsonSerializer.Serialize(new { kind = "DetectionMoveConfirmed", operationId = Guid.NewGuid(),
            role = "Detection", target = new { X = 10, Y = 20, Z = 30 }, X = 10.1, Y = 20.2, Z = 30.3, epoch = 2,
            localPath = "must-not-be-published" });
        var facts = JsonSerializer.SerializeToElement(project.Invoke(null, [payload]), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(20.2, facts.GetProperty("actual").GetProperty("y").GetDouble());
        Assert.Equal(20, facts.GetProperty("target").GetProperty("point").GetProperty("y").GetDouble());
        Assert.Equal(2, facts.GetProperty("connectionEpoch").GetInt32());
        Assert.False(facts.TryGetProperty("localPath", out _));
        var intent = JsonSerializer.Serialize(new { role = "Detection", target = new { X = 10, Y = 20, Z = 30 } });
        var intentFacts = JsonSerializer.SerializeToElement(project.Invoke(null, [intent]), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(JsonValueKind.Null, intentFacts.GetProperty("actual").ValueKind);
    }

    [Fact]
    public void MotionTargetHasFiniteBusinessFieldsAndReadsCurrentSemanticPosition()
    {
        var project = typeof(QueryEndpoints).GetMethod("MotionFacts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var observation = Guid.NewGuid(); var operation = Guid.NewGuid(); var action = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new { schemaVersion = "device-semantics/1", kind = "DetectionMoveConfirmed",
            operationId = operation, actionId = action, epoch = 3,
            target = new { StepSequence = 4, ObjectId = "part-1", SlotId = "slot-2", PhysicalSlotIndex = 2,
                LocalFace = 1, HeightRound = 1, Camera = "A", PointRef = "capture", Source = "FrozenPointConfig", ZBasis = "MeasuredHeight",
                Point = new { Id = "capture", Version = "v1", Unit = "mm", Frame = "machine", X = 10, Y = 20, Z = 30 },
                IsValid = true, UnregisteredField = "not-public" },
            positionEvidence = new { correlation = new { operationId = operation, actionId = action, connectionEpoch = 3 },
                positions = new[] { new { actual = new { actualX = 10.1, actualY = 20.2, actualZ = 30.3,
                    identity = new { observationId = observation, connectionEpoch = 3, sampleEndedUtc = "2026-10-02T00:00:00Z" } },
                    matched = true, tolerance = 0.5 } } } });
        var facts = JsonSerializer.SerializeToElement(project.Invoke(null, [payload]), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var target = facts.GetProperty("target");
        Assert.Equal(2, target.GetProperty("physicalSlotIndex").GetInt32());
        Assert.Equal("FrozenPointConfig", target.GetProperty("coordinateSource").GetString());
        Assert.Equal("mm", target.GetProperty("point").GetProperty("unit").GetString());
        Assert.False(target.TryGetProperty("isValid", out _)); Assert.False(target.TryGetProperty("unregisteredField", out _));
        Assert.Equal(30.3, facts.GetProperty("actual").GetProperty("z").GetDouble());
        Assert.Equal(observation, facts.GetProperty("observationId").GetGuid());
        Assert.Equal("Captured", facts.GetProperty("recordNature").GetString());
    }

    [Theory]
    [InlineData("OK")]
    [InlineData("NG")]
    [InlineData("Pending")]
    public void CommittedImageIsVisibleBeforeObjectAggregateWithoutInventingObjectQuality(string quality)
    {
        var call = Guid.NewGuid(); var capture = Guid.NewGuid(); var media = Guid.NewGuid();
        var image = Fact(2, new { kind = "DetectionImageCommitted", objectId = "part-1", unitId = "unit-1",
            unitKind = "independentPart", localFace = 1, stepSequence = 3, camera = "A",
            callId = call, captureId = capture, detectionDisposition = quality,
            algorithmProfile = "actual-capability", technicalState = "Success",
            MediaId = media, requestedCaptureSettings = new { exposureUs = 1200 } });
        var result = CommittedResultProjection.Build([], [image]);
        var part = Assert.Single(result.Results);
        Assert.Null(part.Disposition);
        Assert.Equal("NotProduced", part.Availability);
        Assert.Equal("Unknown", part.Completeness);
        Assert.Equal("part-1", result.Context!.Id);
        var inspection = Assert.Single(part.Inspections);
        Assert.Equal(quality, inspection.Disposition);
        Assert.Equal(call, inspection.CallId);
        Assert.Equal(media, Assert.Single(inspection.MediaIds));
        Assert.Equal("RequestedCapture", Assert.Single(inspection.Parameters).Kind);
        Assert.Null(inspection.Confidence);
        Assert.Null(inspection.Defects);
        Assert.Equal("NotProvided", inspection.DetailAvailability.Defects);
    }

    [Fact]
    public void QualityAndRequiredTargetsRemainDistinctFromFlowFinal()
    {
        var requirements = Fact(1, new { kind = "DetectionRequirements", targets = new[] {
            new { objectId = "part-1", stepSequence = 3 }, new { objectId = "part-1", stepSequence = 5 } } });
        var image = Fact(2, new { kind = "DetectionImageCommitted", objectId = "part-1",
            unitKind = "independentPart", localFace = 1, stepSequence = 3, camera = "A",
            detectionDisposition = "NG" });
        var aggregate = new RunResultProjection("Single", "part-1", "NG", "actual-result", "plan",
            Guid.NewGuid(), "Simulated", "Derived");
        var result = CommittedResultProjection.Build([aggregate], [requirements, image]);
        var part = Assert.Single(result.Results);
        Assert.Equal("NG", part.Disposition);
        Assert.Equal("Derived", part.Quality);
        Assert.Equal("Incomplete", part.Completeness);
        Assert.Equal(2, part.RequiredTargetRefs!.Count);
        Assert.Single(part.CompletedTargetRefs!);
    }

    [Fact]
    public void LegacyUnassociatedPayloadDoesNotBecomeCurrentObject()
    {
        var result = CommittedResultProjection.Build([], [Fact(1, new { callId = Guid.NewGuid(), DetectionDisposition = "OK" })]);
        Assert.Empty(result.Results);
        Assert.Null(result.Context);
    }

    [Fact]
    public void ActualCaptureParametersRemainDistinctFromRequestedValues()
    {
        var image = Fact(2, new { kind = "DetectionImageCommitted", objectId = "part-1",
            unitKind = "independentPart", localFace = 1, stepSequence = 3, camera = "A",
            detectionDisposition = "OK", requestedCaptureSettings = new { exposureUs = 1200 },
            captureFact = new { actualSettings = new { exposureUs = 1100 }, applicationState = "Applied" } });
        var result = CommittedResultProjection.Build([], [image]);
        var parameters = Assert.Single(Assert.Single(result.Results).Inspections).Parameters;
        Assert.Equal(1200, JsonSerializer.SerializeToElement(Assert.Single(parameters, x => x.Kind == "RequestedCapture").Value).GetInt32());
        Assert.Equal(1100, JsonSerializer.SerializeToElement(Assert.Single(parameters, x => x.Kind == "ActualCapture").Value).GetInt32());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("stage:1")]
    public void HierarchyFacesAndFusionShareStageIdentityWithoutDuplicatingHistory(string? stageId)
    {
        var hierarchy = Fact(1, new { kind = "DetectionUnitDecision", UnitKind = "looseGroup", UnitId = "group",
            disposition = "OK", parts = new[] { new { objectId = "member", disposition = "OK",
                faces = new[] { new { LocalFace = 1, StageId = stageId, Disposition = "OK", Evidence = "actual-fusion" } } } } });
        var reader = typeof(QueryEndpoints).GetMethod("ReadHierarchy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var aggregates = Assert.IsAssignableFrom<IReadOnlyList<RunResultProjection>>(reader.Invoke(null, [hierarchy]));
        var fusion = Fact(2, new { kind = "FaceFusionCommitted", objectId = "member", unitId = "group", unitKind = "looseGroup",
            localFace = 1, stageId, disposition = "OK", stepSequence = 1 });
        var face = Assert.Single(CommittedResultProjection.Build(aggregates, [hierarchy, fusion]).Results, r => r.Kind == "Face");
        Assert.Equal(stageId, face.StageId);
        Assert.Equal("member", face.ParentId);
        Assert.Equal($"stage-event://{fusion.EventId:D}", face.ResultReference);
    }

    private static StageEvent Fact(long seq, object payload) => new(Guid.NewGuid(), Guid.NewGuid(),
        Guid.NewGuid(), "S01", "L01", WholeTrayWorkflowStage.Detection, Guid.NewGuid(), 1, 1,
        StageEventType.Executing, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
        ResultSource.Simulated, ResultQuality.Derived, null, "digest", JsonSerializer.Serialize(payload),
        Guid.NewGuid().ToString(), seq, DateTimeOffset.UtcNow.AddDays(1), "plan");
}
