using System.Text.Json;
using Gaode.Domain.Station01;
using Microsoft.EntityFrameworkCore;

namespace Gaode.Infrastructure.Persistence;

// Finite current/old persisted device facts. Only closed semantic projections leave
// this reader; raw traffic and old protocol values never enter business interfaces.
public static partial class DeviceEvidenceHistoryReader
{
    public static async Task<RunDiagnosticReferences> ReadReferencesAsync(Station01DbContext db, Guid runId, CancellationToken token)
    {
        var manifest = await db.Manifests.AsNoTracking().SingleAsync(token);
        var options = new DbContextOptionsBuilder<Station01DbContext>().UseSqlite(db.Database.GetConnectionString()!).Options;
        var reader = new CommunicationEvidenceReader(options, manifest.StoreId);
        var ids = await db.PlcCommunicationEvidence.AsNoTracking().Where(e => e.RunId == runId && e.StoreId == manifest.StoreId)
            .OrderBy(e => e.EvidenceId).Select(e => e.EvidenceId).ToArrayAsync(token);
        var references = new List<DiagnosticEvidenceReference>();
        var rawAvailable = false;
        foreach (var id in ids)
        {
            var actual = await reader.ReadAsync(id, token) ?? throw new InvalidDataException("CommittedEvidenceDisappeared");
            references.Add(new(manifest.StoreId, id));
            rawAvailable |= actual.RawAvailability == "CapturedRaw";
        }
        return new(rawAvailable ? "CapturedRaw" : "RawUnavailable", references);
    }

    public static async Task<StageDeviceHistory> ReadStageAsync(Station01DbContext db, StageEventEntity row,
        IReadOnlyList<DiagnosticEvidenceReference> confirmed, CancellationToken token)
    {
        using var document = JsonDocument.Parse(row.PayloadJson);
        var root = document.RootElement;
        var schema = Text(root, "schemaVersion");
        if (schema is null)
        {
            var old = LegacyDeviceHistoryReader.ReadPositions(row.PayloadJson);
            return new(null, old.RecordNature, old.RawAvailability, old.Positions, []);
        }
        if (schema is not ("device-semantics/1" or "tray-end/2" or "stage-action/1" or "sorting-evidence/1"))
            return new(null, "Unavailable", "RawUnavailable", null, []);
        // These two versioned records summarize the actual component matrix. They
        // are neither device observations nor a source for invented raw evidence.
        if (schema is "device-semantics/1" or "tray-end/2" && row.Source == "HostDerived" &&
            row.EventType is "WholeTrayCompleted" or "FinalUnloadCompleted")
            return new(null, "Derived", "RawUnavailable", null, []);
        var evidence = Value(root, "evidence") ?? Value(root, "positionEvidence");
        var correlation = Value(root, "correlation") ?? Value(evidence, "correlation");
        var actionId = Id(correlation, "actionId");
        if (actionId is null || Id(correlation, "runId") != row.RunId || Id(correlation, "operationId") != row.OperationId ||
            Long(correlation, "connectionEpoch") != row.ConnectionEpoch)
            return new(null, "Unavailable", "RawUnavailable", null, []);
        var candidates = new List<DiagnosticEvidenceReference>();
        if (Value(evidence, "diagnosticEvidenceReferences") is { ValueKind: JsonValueKind.Array } array)
            foreach (var candidate in array.EnumerateArray())
                if (Id(candidate, "storeId") is { } store && Id(candidate, "evidenceId") is { } id &&
                    confirmed.Contains(new(store, id))) candidates.Add(new(store, id));
        var matching = await db.PlcCommunicationEvidence.AsNoTracking().Where(e => e.RunId == row.RunId &&
            e.OperationId == row.OperationId && e.ActionId == actionId && e.ConnectionEpoch == row.ConnectionEpoch)
            .Select(e => new { e.StoreId, e.EvidenceId, e.ObservationId, e.RawPayloadJson }).ToArrayAsync(token);
        var valid = candidates.Where(c => matching.Any(m => m.StoreId == c.StoreId && m.EvidenceId == c.EvidenceId)).ToArray();
        // Availability follows the already verified captured record, not the semantic
        // payload's claimed provider. Simulation may have a durable reference but no wire.
        var capturedRaw = matching.Where(m => valid.Any(r => r.StoreId == m.StoreId && r.EvidenceId == m.EvidenceId))
            .Any(m => JsonSerializer.Deserialize<CommunicationEvidenceBatch>(m.RawPayloadJson,
                new JsonSerializerOptions(JsonSerializerDefaults.Web)) is { Origin.Provider: not DeviceProvider.Simulated } batch &&
                batch.Exchanges.Count + batch.HttpExchanges.Count > 0);
        var positions = new List<SemanticPositionEvidence>();
        var positionsJson = Value(evidence, "positions");
        var savedPositions = positionsJson is { ValueKind: JsonValueKind.Array } rows ? rows.EnumerateArray().ToArray() :
            Value(evidence, "sourcePositionReached") is { ValueKind: JsonValueKind.Object } source ? [source] : Array.Empty<JsonElement>();
        foreach (var p in savedPositions)
        {
            var pc = Value(p, "correlation");
            if (Id(pc, "actionId") != actionId || Id(pc, "runId") != row.RunId || Id(pc, "operationId") != row.OperationId ||
                Long(pc, "connectionEpoch") != row.ConnectionEpoch) continue;
            var actual = Value(p, "actual"); var identity = Value(actual, "identity");
            var observationId = Id(identity, "observationId");
            if (Long(identity, "connectionEpoch") != row.ConnectionEpoch || observationId is null) continue;
            var target = Value(p, "target");
            var x = Number(actual, "actualX"); var y = Number(actual, "actualY"); var z = Number(actual, "actualZ");
            var reference = valid.FirstOrDefault(r => matching.Any(m => m.EvidenceId == r.EvidenceId && m.ObservationId == observationId));
            positions.Add(new("PositionReached", Text(actual, "axisPurpose") ?? "Unconfirmed",
                target is null ? null : new(Number(target, "x"), Number(target, "y"), Number(target, "z"), Text(target, "id"), Text(target, "version")),
                x is null && y is null && z is null ? null : new(x, y, z), Number(p, "tolerance"), Boolean(p, "matched"),
                Timestamp(identity, "sampleEndedUtc"), observationId, reference, "Captured"));
        }
        return new(actionId, evidence is { ValueKind: JsonValueKind.Object } ? "Captured" : "Derived",
            capturedRaw ? "CapturedRaw" : "RawUnavailable",
            savedPositions.Length == 0 ? null : positions, valid);
    }
    private static JsonElement? Value(JsonElement? node, string name) => node is { ValueKind: JsonValueKind.Object } obj ?
        obj.EnumerateObject().Where(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)).Select(p => (JsonElement?)p.Value).FirstOrDefault() : null;
    private static string? Text(JsonElement? node, string name) => Value(node, name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;
    private static Guid? Id(JsonElement? node, string name) => Value(node, name) is { ValueKind: JsonValueKind.String } value && value.TryGetGuid(out var id) ? id : null;
    private static long? Long(JsonElement? node, string name) => Value(node, name) is { ValueKind: JsonValueKind.Number } value && value.TryGetInt64(out var n) ? n : null;
    private static double? Number(JsonElement? node, string name) => Value(node, name) is { ValueKind: JsonValueKind.Number } value && value.TryGetDouble(out var n) && double.IsFinite(n) ? n : null;
    private static bool? Boolean(JsonElement? node, string name) => Value(node, name) is { ValueKind: JsonValueKind.True } ? true : Value(node, name) is { ValueKind: JsonValueKind.False } ? false : null;
    private static DateTimeOffset? Timestamp(JsonElement? node, string name) => Value(node, name) is { ValueKind: JsonValueKind.String } value && value.TryGetDateTimeOffset(out var time) ? time : null;
}
