using System.Text.Json;

namespace Gaode.Infrastructure.Persistence;

// Finite read-only projection of pre-009 persisted evidence. Never registered as an action port.
// Current semantic payloads must not use this reader, and historical raw values are never reconstructed.
public static class LegacyDispositionHistory
{
    public static string? SortingState(string historicalJson, bool hasCommittedInTransit)
    {
        using var doc = JsonDocument.Parse(historicalJson);
        var r = doc.RootElement;
        JsonElement? Field(JsonElement node, string name) => node.EnumerateObject()
            .Where(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)).Select(p => (JsonElement?)p.Value).FirstOrDefault();
        if (Field(r, "schemaVersion") is not null) return null;
        var kind = Field(r, "kind")?.GetString();
        var status = Field(r, "protocolStatus")?.GetInt32();
        var positions = Field(r, "positionEvidence");
        if (positions is not { ValueKind: JsonValueKind.Array }) return null;
        bool Has(string phase, int value) => positions.Value.EnumerateArray().Any(e =>
            Field(e, "phase")?.GetString() == phase && Field(e, "status")?.GetInt32() == value);
        if (kind == "SortingAssignmentInTransit" && status == 2 && Has("PickCompleted", 2)) return "InTransit";
        if (kind == "SortingAssignmentOccupied" && hasCommittedInTransit && status == 3 &&
            Field(r, "sortingAckCleared") is { ValueKind: JsonValueKind.True } &&
            Field(r, "targetOccupied") is { ValueKind: JsonValueKind.True } &&
            Has("PlaceCompleted", 3) && Has("SortingAckCleared", 0)) return "Completed";
        return null;
    }
}
