using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Domain.Station01;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Gaode.Infrastructure.Persistence;

// Diagnostic-only response. Never a business port or a source of action authorization.
public sealed record CommunicationEvidenceDocument(string SchemaVersion, Guid EvidenceId,
    Guid StoreId, Guid ObservationId, Guid? RunId, Guid? OperationId, Guid? ActionId,
    long ConnectionEpoch, DateTimeOffset SampleStartedUtc, DateTimeOffset SampleEndedUtc,
    DateTimeOffset PersistedAtUtc, string PayloadDigest, ExecutionOrigin ExecutionOrigin,
    string RawAvailability, bool Gap, string RawPayloadJson);

public sealed class CommunicationEvidenceReader
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly DbContextOptions<Station01DbContext> readOptions;
    private readonly Guid storeId;

    // Only formal composition supplies the admitted store. HTTP accepts evidenceId only.
    public CommunicationEvidenceReader(DbContextOptions<Station01DbContext> options, Guid storeId)
    {
        if (storeId == Guid.Empty) throw new ArgumentException("StoreIdentityRequired", nameof(storeId));
        using var context = new Station01DbContext(options);
        var connection = new SqliteConnectionStringBuilder(context.Database.GetConnectionString())
        { Mode = SqliteOpenMode.ReadOnly, Cache = SqliteCacheMode.Private, Pooling = false };
        readOptions = new DbContextOptionsBuilder<Station01DbContext>().UseSqlite(connection.ToString()).Options;
        this.storeId = storeId;
    }

    public async Task<CommunicationEvidenceDocument?> ReadAsync(Guid evidenceId, CancellationToken token)
    {
        await using var db = new Station01DbContext(readOptions);
        var manifest = await db.Manifests.AsNoTracking().SingleAsync(token);
        if (manifest.StoreId != storeId || manifest.SchemaVersion is not ("s01-store/2" or "s01-store/3"))
            throw new InvalidDataException("CommunicationEvidenceStoreIdentityMismatch");
        var row = await db.PlcCommunicationEvidence.AsNoTracking()
            .SingleOrDefaultAsync(x => x.EvidenceId == evidenceId && x.StoreId == storeId, token);
        if (row is null) return null;
        var digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(row.RawPayloadJson)));
        if (!string.Equals(digest, row.PayloadDigest, StringComparison.Ordinal))
            throw new InvalidDataException("CommunicationEvidenceDigestMismatch");
        CommunicationEvidenceBatch batch;
        try { batch = JsonSerializer.Deserialize<CommunicationEvidenceBatch>(row.RawPayloadJson, Json)
            ?? throw new JsonException("Missing batch"); }
        catch (JsonException error) { throw new InvalidDataException("CommunicationEvidencePayloadInvalid", error); }
        // Compare stored identities, never decode against today's protocol definitions.
        if (!batch.IsValid || row.PayloadSchema != batch.SchemaVersion || row.EvidenceId != batch.EvidenceId ||
            row.StoreId != batch.StoreId || row.ObservationId != batch.ObservationId || row.RunId != batch.RunId ||
            row.OperationId != batch.OperationId || row.ActionId != batch.ActionId || row.ConnectionEpoch != batch.ConnectionEpoch ||
            row.ObservedStartUtc != batch.ObservedStartUtc || row.ObservedEndUtc != batch.ObservedEndUtc)
            throw new InvalidDataException("CommunicationEvidenceIdentityMismatch");
        return new(row.PayloadSchema, row.EvidenceId, row.StoreId, row.ObservationId, row.RunId,
            row.OperationId, row.ActionId, row.ConnectionEpoch, row.ObservedStartUtc, row.ObservedEndUtc,
            row.PersistedUtc, row.PayloadDigest, batch.Origin,
            batch.Origin.Provider == DeviceProvider.Simulated ? "RawUnavailable" : "CapturedRaw", batch.Gap, row.RawPayloadJson);
    }
}
