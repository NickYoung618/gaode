using System.Security.Cryptography;
using System.Text;
using Gaode.Application.Ports;
using Gaode.Domain.Station01;
using Gaode.Communication.Tests.Persistence;
using Gaode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Gaode.Communication.Tests.History;

// Persistence/projection fixtures only. Formal captured traffic is verified separately.
public sealed class CommunicationEvidenceReaderTests
{
    [Theory]
    [InlineData("HISTORY-RAW/success")]
    [InlineData("HISTORY-RAW/unknown")]
    [InlineData("HISTORY-RAW/timeout")]
    public async Task ReopenedReaderPreservesStoredBytesAndMissingResponse(string caseId)
    {
        var fixture = await CommunicationEvidenceStoreTests.CreateAsync();
        var exchange = fixture.Batch.Exchanges.Single();
        if (caseId.EndsWith("unknown")) exchange = exchange with { ResponseHex = "000100000005010302FFFF" };
        if (caseId.EndsWith("timeout")) exchange = exchange with { ResponseHex = null, Error = "TimeoutException" };
        var batch = fixture.Batch with { Exchanges = [exchange] };
        await using (var writer = new TraceWriter(fixture.Options, TimeProvider.System, 4))
        {
            var receipt = await new CommunicationEvidenceStore(writer, TimeProvider.System)
                .SaveAsync(batch, Window(), CancellationToken.None);
            Assert.Equal(ReceiptValidity.ValidCurrent, receipt.Validity);
        }
        await using var db = new Station01DbContext(fixture.Options);
        var before = await db.PlcCommunicationEvidence.AsNoTracking().SingleAsync();
        var result = await new CommunicationEvidenceReader(fixture.Options, fixture.StoreId)
            .ReadAsync(batch.EvidenceId, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(before.RawPayloadJson, result.RawPayloadJson);
        Assert.Equal(before.PayloadDigest, result.PayloadDigest);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(result.RawPayloadJson))), result.PayloadDigest);
        Assert.Equal(batch.ObservationId, result.ObservationId);
        Assert.Equal(before.PersistedUtc, result.PersistedAtUtc);
        Assert.Equal("plc-evidence/1", result.SchemaVersion);
        Assert.Equal("CapturedRaw", result.RawAvailability);
        using var payload = System.Text.Json.JsonDocument.Parse(result.RawPayloadJson);
        Assert.Equal(exchange.ResponseHex, payload.RootElement.GetProperty("exchanges")[0].GetProperty("responseHex").GetString());
        Assert.Equal(exchange.Error, payload.RootElement.GetProperty("exchanges")[0].GetProperty("error").GetString());
        Assert.Equal(before.RawPayloadJson, (await db.PlcCommunicationEvidence.AsNoTracking().SingleAsync()).RawPayloadJson);
    }

    [Fact]
    public async Task MissingOrForeignStoreReferenceCannotYieldARecord()
    {
        var fixture = await CommunicationEvidenceStoreTests.CreateAsync();
        await using var writer = new TraceWriter(fixture.Options, TimeProvider.System, 4);
        await new CommunicationEvidenceStore(writer, TimeProvider.System).SaveAsync(fixture.Batch, Window(), CancellationToken.None);
        var reader = new CommunicationEvidenceReader(fixture.Options, fixture.StoreId);
        Assert.Null(await reader.ReadAsync(Guid.NewGuid(), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidDataException>(() => new CommunicationEvidenceReader(fixture.Options, Guid.NewGuid())
            .ReadAsync(fixture.Batch.EvidenceId, CancellationToken.None));
    }

    [Theory]
    [InlineData("HISTORY-RAW/digest-mismatch", true)]
    [InlineData("HISTORY-RAW/identity-mismatch", false)]
    public async Task CorruptBytesOrMismatchedIndexedIdentityAreRejected(string caseId, bool digest)
    {
        var fixture = await CommunicationEvidenceStoreTests.CreateAsync();
        await using var writer = new TraceWriter(fixture.Options, TimeProvider.System, 4);
        await new CommunicationEvidenceStore(writer, TimeProvider.System).SaveAsync(fixture.Batch, Window(), CancellationToken.None);
        await using (var db = new Station01DbContext(fixture.Options))
        {
            // Bypass the EF immutable-entity guard only to model corrupt storage in
            // this isolated copy. Production readers and writers retain that guard.
            if (digest) await db.Database.ExecuteSqlRawAsync("UPDATE PlcCommunicationEvidence SET RawPayloadJson = RawPayloadJson || ' '");
            else await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE PlcCommunicationEvidence SET ObservationId = {Guid.NewGuid()}");
        }
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => new CommunicationEvidenceReader(fixture.Options, fixture.StoreId)
            .ReadAsync(fixture.Batch.EvidenceId, CancellationToken.None));
        Assert.True(error.Message.Contains(digest ? "Digest" : "Identity", StringComparison.Ordinal), caseId + ": " + error.Message);
    }

    private static ActionWindow Window()
    {
        var now = TimeProvider.System.GetTimestamp();
        var utc = DateTimeOffset.UtcNow;
        return new(now, now + TimeProvider.System.TimestampFrequency * 10, "system", utc, utc.AddSeconds(10));
    }
}
