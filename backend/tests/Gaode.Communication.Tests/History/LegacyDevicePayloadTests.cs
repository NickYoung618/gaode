using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Infrastructure.Persistence;
using Xunit;

namespace Gaode.Communication.Tests.History;

public sealed class LegacyDevicePayloadTests
{
    private const string Legacy = """
      {"kind":"SortingAssignmentInTransit","protocolStatus":2,"source":"Real",
       "PositionEvidence":[
        {"Phase":"PickCompleted","AxisRole":"GrabZ","TargetX":10,"TargetY":20,"TargetZ":30,
         "ActualX":10,"ActualY":20,"ActualZ":35,"Status":2,"Tolerance":0.1,"ObservedAtUtc":"2026-09-27T00:00:00Z"},
        {"Phase":"SortingAckCleared","Status":0,"TargetX":10,"TargetY":20,"TargetZ":30}]}
      """;

    [Fact]
    public void OldRecordedClaimKeepsActualMeasurementAndCannotBecomeCapturedRaw()
    {
        var before = SHA256.HashData(Encoding.UTF8.GetBytes(Legacy));
        var result = LegacyDeviceHistoryReader.ReadPositions(Legacy);
        Assert.Equal("LegacyRecordedClaim", result.RecordNature);
        Assert.Equal("RawUnavailable", result.RawAvailability);
        Assert.Equal("Real", result.RecordedSource);
        var position = Assert.Single(result.Positions!);
        Assert.Equal("PickObserved", position.Kind);
        Assert.Equal(30, position.Target!.Z);
        Assert.Equal(35, position.Actual!.Z);
        Assert.Null(position.Matched);
        Assert.Null(position.ObservationId);
        Assert.Empty(result.DiagnosticEvidenceReferences);
        var api = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("protocolStatus", api);
        Assert.DoesNotContain("protocolVersion", api);
        Assert.DoesNotContain("sortingAckCleared", api, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"status\"", api);
        Assert.Equal(before, SHA256.HashData(Encoding.UTF8.GetBytes(Legacy)));
    }

    [Theory]
    [InlineData("HISTORY-POS/PickTargetObserved", "SourcePositionReached")]
    [InlineData("HISTORY-POS/PlaceTargetObserved", "TargetPositionReached")]
    [InlineData("HISTORY-POS/PlaceCompleted", "PlaceObserved")]
    [InlineData("HISTORY-POS/UnloadPositionValidated", "PositionReached")]
    public void KnownOldShapesDoNotFillMissingActualCoordinates(string caseId, string meaning)
    {
        var phase = caseId.Split('/')[1];
        var payload = JsonSerializer.Serialize(new { positionEvidence = new[] {
            new { phase, targetX = 10, targetY = 20, targetZ = 30, observedAtUtc = "2026-09-27T00:00:00Z" } } });
        var position = Assert.Single(LegacyDeviceHistoryReader.ReadPositions(payload).Positions!);
        Assert.Equal(meaning, position.Kind);
        Assert.Null(position.Actual);
        Assert.Null(position.Matched);
        Assert.Null(position.Target!.Version);
        Assert.Equal("Unconfirmed", position.AxisRole);
    }

    [Theory]
    [InlineData("HISTORY-POS/missing", "{}")]
    [InlineData("HISTORY-POS/unknown-schema", "{\"schemaVersion\":\"future-unknown/9\",\"PositionEvidence\":[]}")]
    public void UnknownOrMissingHistoryCannotInventCompleteObjects(string caseId, string payload)
    {
        var projection = LegacyDeviceHistoryReader.ReadPositions(payload);
        Assert.True(projection.RecordNature == "Unavailable", caseId);
        Assert.Null(projection.Positions);
        Assert.Null(projection.RecordedSource);
        Assert.Empty(projection.DiagnosticEvidenceReferences);
    }

    [Theory]
    [InlineData("HISTORY-SORT/pick", true, "InTransit")]
    [InlineData("HISTORY-SORT/place", true, "Completed")]
    [InlineData("HISTORY-SORT/no-pick", false, null)]
    [InlineData("HISTORY-SORT/no-clear-claim", true, null)]
    [InlineData("HISTORY-SORT/no-clear-record", true, null)]
    [InlineData("HISTORY-SORT/wrong-completion", true, null)]
    [InlineData("HISTORY-SORT/current-schema", true, null)]
    public void OriginalDispositionClaimsRequireTheirOriginalRecordedFacts(string caseId, bool priorPick, string? state)
    {
        // Pre-009 literals are independent of production protocol definitions. This
        // is historical recorded-claim reading, never current physical authorization.
        var pick = caseId.EndsWith("/pick", StringComparison.Ordinal);
        var payload = new Dictionary<string, object?> {
            ["kind"] = pick ? "SortingAssignmentInTransit" : "SortingAssignmentOccupied",
            ["protocolStatus"] = pick ? 2 : caseId.EndsWith("wrong-completion", StringComparison.Ordinal) ? 99 : 3,
            ["sortingAckCleared"] = !caseId.EndsWith("no-clear-claim", StringComparison.Ordinal),
            ["targetOccupied"] = true,
            ["positionEvidence"] = pick ? new[] { new { Phase = "PickCompleted", Status = 2 } } :
                caseId.EndsWith("no-clear-record", StringComparison.Ordinal) ? new[] { new { Phase = "PlaceCompleted", Status = 3 } } :
                new[] { new { Phase = "PlaceCompleted", Status = 3 }, new { Phase = "SortingAckCleared", Status = 0 } }
        };
        if (caseId.EndsWith("current-schema", StringComparison.Ordinal)) payload["schemaVersion"] = "sorting-evidence/1";
        var original = JsonSerializer.Serialize(payload);
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(original));
        Assert.Equal(state, LegacyDispositionHistory.SortingState(original, priorPick));
        Assert.Equal(digest, SHA256.HashData(Encoding.UTF8.GetBytes(original)));
    }

}
