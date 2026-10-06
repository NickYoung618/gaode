using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Communication.Tests.Persistence;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Persistence;
using Xunit;

namespace Gaode.Communication.Tests.History;

// Actual SQLite persistence, with explicitly authored semantic/packet fixtures.
// These verify historical identity/projection, not physical motion or TCP.
public sealed class DeviceEvidenceHistoryTests
{
    [Theory]
    [InlineData("HISTORY-CURRENT/matching")]
    [InlineData("HISTORY-CURRENT/foreign-action")]
    [InlineData("HISTORY-CURRENT/foreign-epoch")]
    [InlineData("HISTORY-CURRENT/missing-actual")]
    [InlineData("HISTORY-CURRENT/foreign-reference")]
    [InlineData("HISTORY-CURRENT/simulated-reference")]
    public async Task HistoricalReferencesRequireActualCommittedMatchingIdentities(string caseId)
    {
        var fixture = await CommunicationEvidenceStoreTests.CreateAsync();
        var correlation = new ActionCorrelation(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1,
            Guid.NewGuid(), 1, "history-fixture/1");
        var batch = fixture.Batch with { RunId = correlation.RunId, OperationId = correlation.OperationId,
            ActionId = caseId.EndsWith("foreign-reference") ? Guid.NewGuid() : correlation.ActionId };
        if (caseId.EndsWith("simulated-reference")) batch = batch with {
            Origin = new(DeviceProvider.Simulated, "FullSimulation/semantic-009", EvidenceQuality.Derived),
            InterpretationVersion = "FullSimulation/semantic-009", ByteOrder = "NotApplicable", Exchanges = [] };
        var clock = TimeProvider.System; var utc = clock.GetUtcNow(); var tick = clock.GetTimestamp();
        await using (var writer = new TraceWriter(fixture.Options, clock, 4))
        {
            var receipt = await new CommunicationEvidenceStore(writer, clock).SaveAsync(batch,
                new(tick, tick + clock.TimestampFrequency * 10, "system", utc, utc.AddSeconds(10)), CancellationToken.None);
            Assert.Equal(ReceiptValidity.ValidCurrent, receipt.Validity);
        }
        var positionCorrelation = caseId.EndsWith("foreign-action") ? correlation with { ActionId = Guid.NewGuid() } : correlation;
        var identity = new ObservationIdentity(batch.ObservationId, 1, batch.ObservedStartUtc, batch.ObservedEndUtc, DeviceReliability.Reliable);
        var payload = JsonSerializer.Serialize(new { schemaVersion = "device-semantics/1", correlation,
            evidence = new { correlation, executionOrigin = fixture.Batch.Origin,
                diagnosticEvidenceReferences = new[] { new DiagnosticEvidenceReference(fixture.StoreId, batch.EvidenceId) },
                positions = new[] { new { correlation = positionCorrelation, target = new { id = "pick", version = "1", x = 10, y = 20, z = 30 },
                    actual = new { actualX = (double?)10, actualY = (double?)20,
                        actualZ = caseId.EndsWith("missing-actual") ? (double?)null : 35,
                        axisPurpose = "Sorting", identity }, tolerance = 0.1, matched = (bool?)false } } } });
        var row = new StageEventEntity { RunId = correlation.RunId, OperationId = correlation.OperationId,
            ConnectionEpoch = caseId.EndsWith("foreign-epoch") ? 2 : 1, PayloadJson = payload };
        await using var db = new Station01DbContext(fixture.Options);
        var references = await DeviceEvidenceHistoryReader.ReadReferencesAsync(db, correlation.RunId, CancellationToken.None);
        Assert.Single(references.References);
        var actual = await DeviceEvidenceHistoryReader.ReadStageAsync(db, row, references.References, CancellationToken.None);
        if (caseId.EndsWith("foreign-epoch"))
        {
            Assert.Equal("Unavailable", actual.RecordNature); Assert.Null(actual.ActionId);
            Assert.Null(actual.Positions); Assert.Empty(actual.DiagnosticEvidenceReferences); return;
        }
        Assert.Equal(correlation.ActionId, actual.ActionId);
        if (caseId.EndsWith("foreign-action")) { Assert.Empty(actual.Positions!); return; }
        var position = Assert.Single(actual.Positions!);
        Assert.False(position.Matched); // Stored false is not recomputed from today's target/tolerance.
        Assert.Equal(30, position.Target!.Z);
        Assert.Equal(caseId.EndsWith("missing-actual") ? null : (double?)35, position.Actual!.Z);
        Assert.Equal(batch.ObservationId, position.ObservationId);
        if (caseId.EndsWith("foreign-reference"))
        {
            Assert.Null(position.DiagnosticEvidenceReference); Assert.Empty(actual.DiagnosticEvidenceReferences);
            Assert.Equal("RawUnavailable", actual.RawAvailability);
        }
        else
        {
            Assert.Equal(new(fixture.StoreId, batch.EvidenceId), position.DiagnosticEvidenceReference);
            Assert.Equal(caseId.EndsWith("simulated-reference") ? "RawUnavailable" : "CapturedRaw", actual.RawAvailability);
        }
        using var projection = JsonDocument.Parse(JsonSerializer.Serialize(position));
        Assert.False(projection.RootElement.TryGetProperty("Status", out _));
        Assert.False(projection.RootElement.TryGetProperty("IsValid", out _));
    }
}
