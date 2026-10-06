using System.Text.Json;

namespace Gaode.Infrastructure.Persistence;

public static partial class DeviceEvidenceHistoryReader
{
    // Explicit old/current persisted motion shapes only. Never decode a protocol value.
    public static SemanticMotionFact ReadMotion(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var schema = Text(root, "schemaVersion");
        var current = schema == "device-semantics/1";
        var known = current || schema is null;
        var kind = Text(root, "kind");
        if (kind is not (null or "DetectionMoveConfirmed" or "FlipPositionConfirmed" or "ECodeMoveConfirmed" or "ThreeDRescanMoveConfirmed" or "FixedMoveCompleted")) known = false;
        var operation = Id(root, "operationId"); var action = Id(root, "actionId");
        var epoch = Long(root, "connectionEpoch") ?? Long(root, "deviceEpoch") ?? Long(root, "epoch");
        var target = known ? Value(root, "target") : null;
        var point = Value(target, "point") ?? target;
        var projectedTarget = target is null ? null : new BusinessMotionTarget(Integer(target, "stepSequence"), Text(target, "objectId"),
            Text(target, "slotId"), Integer(target, "physicalSlotIndex") ?? (current ? null : Integer(target, "protocolSlotIndex")),
            Integer(target, "localFace"), Integer(target, "heightRound"), Text(target, "camera"), Text(target, "pointRef"),
            new(Text(point, "id"), Text(point, "version"), Text(point, "unit"), Text(point, "frame"), Number(point, "x"), Number(point, "y"), Number(point, "z")),
            Text(target, "source"), Text(target, "zBasis"));
        var evidence = Value(root, "positionEvidence") ?? Value(root, "evidence");
        var correlation = Value(evidence, "correlation");
        var correlated = operation is not null && action is not null && epoch is not null &&
            Id(correlation, "operationId") == operation && Id(correlation, "actionId") == action && Long(correlation, "connectionEpoch") == epoch;
        var positions = Value(evidence, "positions");
        var position = correlated && positions is { ValueKind: JsonValueKind.Array } items && items.GetArrayLength() == 1 ? (JsonElement?)items[0] : null;
        var actual = known ? Value(root, "actual") ?? Value(position, "actual") : null;
        var identity = Value(actual, "identity");
        var x = Number(actual, "actualX") ?? Number(actual, "x");
        var y = Number(actual, "actualY") ?? Number(actual, "y");
        var z = Number(actual, "actualZ") ?? Number(actual, "z");
        if (!current && known && actual is null && kind is "DetectionMoveConfirmed" or "FlipPositionConfirmed" or "ECodeMoveConfirmed" or "ThreeDRescanMoveConfirmed")
        { x = Number(root, "x"); y = Number(root, "y"); z = Number(root, "z"); }
        return new(known ? kind : null, operation, action, Integer(root, "attempt"), Text(root, "pointId"), Text(root, "pointVersion"),
            Text(root, "role"), projectedTarget, x is null && y is null && z is null ? null : new(x, y, z), Text(root, "zAxis"),
            Number(root, "tolerance") ?? Number(position, "tolerance"), Boolean(root, "matched") ?? Boolean(position, "matched"),
            Boolean(root, "accepted"), Boolean(root, "completed"), Timestamp(root, "observedAtUtc") ?? Timestamp(identity, "sampleEndedUtc"),
            epoch, current && Long(identity, "connectionEpoch") == epoch ? Id(identity, "observationId") : null,
            !known ? "Unavailable" : current ? "Captured" : "LegacyRecordedClaim");
    }
    private static int? Integer(JsonElement? node, string name) => Long(node, name) is { } value && value is >= int.MinValue and <= int.MaxValue ? (int)value : null;
}
