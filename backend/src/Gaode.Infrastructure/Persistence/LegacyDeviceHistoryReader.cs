using System.Text.Json;

namespace Gaode.Infrastructure.Persistence;

// Only the documented pre-009 persisted shape is recognized. No current protocol
// definitions, online compatibility, write path or control port participates here.
public static class LegacyDeviceHistoryReader
{
    public static DevicePositionHistory ReadPositions(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || Value(root, "schemaVersion") is not null ||
            Value(root, "positionEvidence") is not { ValueKind: JsonValueKind.Array } positions)
            return new("Unavailable", "RawUnavailable", null, null, []);
        var result = new List<SemanticPositionEvidence>();
        foreach (var row in positions.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object))
        {
            var kind = Text(row, "phase") switch
            {
                "PickTargetObserved" => "SourcePositionReached", "PlaceTargetObserved" => "TargetPositionReached",
                "PickCompleted" => "PickObserved", "PlaceCompleted" => "PlaceObserved",
                "UnloadPositionValidated" => "PositionReached", _ => null
            };
            // Write acknowledgements and reset/clear phases remain only in the old raw payload.
            if (kind is null) continue;
            var tx = Number(row, "targetX"); var ty = Number(row, "targetY"); var tz = Number(row, "targetZ");
            var ax = Number(row, "actualX"); var ay = Number(row, "actualY"); var az = Number(row, "actualZ");
            var matched = Value(row, "matched");
            result.Add(new(kind, Text(row, "axisRole") ?? "Unconfirmed",
                tx is null && ty is null && tz is null ? null : new(tx, ty, tz),
                ax is null && ay is null && az is null ? null : new(ax, ay, az), Number(row, "tolerance"),
                matched is { ValueKind: JsonValueKind.True } ? true : matched is { ValueKind: JsonValueKind.False } ? false : null,
                Value(row, "observedAtUtc") is { ValueKind: JsonValueKind.String } time && time.TryGetDateTimeOffset(out var at) ? at : null,
                null, null, "LegacyRecordedClaim"));
        }
        // Neither an old phase nor a synthesized status establishes a captured packet.
        return new("LegacyRecordedClaim", "RawUnavailable", Text(root, "source"), result, []);
    }

    private static JsonElement? Value(JsonElement item, string name) => item.EnumerateObject()
        .Where(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)).Select(x => (JsonElement?)x.Value).FirstOrDefault();
    private static string? Text(JsonElement item, string name) => Value(item, name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;
    private static double? Number(JsonElement item, string name) => Value(item, name) is { ValueKind: JsonValueKind.Number } value &&
        value.TryGetDouble(out var number) && double.IsFinite(number) ? number : null;
}
