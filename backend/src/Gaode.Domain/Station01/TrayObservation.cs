using System.Text.Json.Serialization;

namespace Gaode.Domain.Station01;

[JsonConverter(typeof(JsonStringEnumConverter<TrayPresence>))]
public enum TrayPresence { Unknown, Absent, Present }
[JsonConverter(typeof(JsonStringEnumConverter<TrayPose>))]
public enum TrayPose { Unknown, Normal, Abnormal }
[JsonConverter(typeof(JsonStringEnumConverter<TrayObservationPurpose>))]
public enum TrayObservationPurpose { InitialPreparation, PostPlacementCheck }
public sealed record TraySlotObservation(int PhysicalSlotIndex, TrayPresence Presence, TrayPose Pose, string? Reason = null)
{
    public string? CellId { get; init; }
    public string? Region { get; init; }
    public int? Row { get; init; }
    public int? Column { get; init; }
    public bool HasPositionIdentity => !string.IsNullOrWhiteSpace(CellId) && Region is "OK" or "NG" or "Pending" &&
        Row is > 0 and <= 10 && Column is > 0 and <= 10 && CellId == $"r{Row}:c{Column}";
}
public sealed record FLocation(double X, double Y, string Unit, string Frame, string SourceReference)
{
    public bool IsValid => double.IsFinite(X) && double.IsFinite(Y) && Unit == "mm" &&
        !string.IsNullOrWhiteSpace(Frame) && !string.IsNullOrWhiteSpace(SourceReference);
}
public sealed record TrayObservation(Guid ObservationId, Guid RunId, Guid TrayId, Guid CaptureId, Guid CallId,
    DateTimeOffset ObservedAtUtc, TrayObservationPurpose Purpose, int CheckRound, Guid? RelatedTransitionId,
    IReadOnlyList<TraySlotObservation> Slots, FLocation? FLocation, ComponentExecutionOrigin Source,
    IReadOnlyList<string> EvidenceReferences)
{
    public string SchemaVersion { get; init; } = "tray-observation/1";
    public string? MappingSourceReference { get; init; }
    public IReadOnlyList<int> ExpectedPhysicalSlotIndices { get; init; } = [];
    public bool HasCompleteCoverage => IsValid && SchemaVersion == "tray-observation/2" &&
        !string.IsNullOrWhiteSpace(MappingSourceReference) && ExpectedPhysicalSlotIndices.Count > 0 &&
        ExpectedPhysicalSlotIndices.All(i => i > 0) &&
        ExpectedPhysicalSlotIndices.Distinct().Count() == ExpectedPhysicalSlotIndices.Count &&
        ExpectedPhysicalSlotIndices.Order().SequenceEqual(Slots.Select(s => s.PhysicalSlotIndex).Order()) &&
        Slots.All(s => s.HasPositionIdentity && s.Presence != TrayPresence.Unknown &&
            (s.Presence == TrayPresence.Absent || s.Pose != TrayPose.Unknown)) &&
        Slots.Select(s => s.CellId).Distinct(StringComparer.Ordinal).Count() == Slots.Count;
    public bool IsEmptyTray => HasCompleteCoverage && Slots.All(s => s.Presence == TrayPresence.Absent);
    public bool HasSamePhysicalMapping(TrayObservation initial) => HasCompleteCoverage && initial.HasCompleteCoverage &&
        RunId == initial.RunId && TrayId == initial.TrayId &&
        ExpectedPhysicalSlotIndices.Order().SequenceEqual(initial.ExpectedPhysicalSlotIndices.Order()) &&
        Slots.All(s => initial.Slots.Any(i => i.PhysicalSlotIndex == s.PhysicalSlotIndex &&
            i.CellId == s.CellId && i.Region == s.Region && i.Row == s.Row && i.Column == s.Column));
    public bool IsValid => ObservationId != Guid.Empty && RunId != Guid.Empty && TrayId != Guid.Empty &&
        CaptureId != Guid.Empty && CallId != Guid.Empty && ObservedAtUtc != default && CheckRound > 0 &&
        Source.IsKnown && EvidenceReferences.Count > 0 && EvidenceReferences.All(x => !string.IsNullOrWhiteSpace(x)) &&
        Slots.Count > 0 && Slots.All(s => s.PhysicalSlotIndex > 0 && Enum.IsDefined(s.Presence) && Enum.IsDefined(s.Pose)) &&
        Slots.Select(s => s.PhysicalSlotIndex).Distinct().Count() == Slots.Count &&
        (Purpose == TrayObservationPurpose.InitialPreparation ? CheckRound == 1 && RelatedTransitionId is null && (FLocation is null || FLocation.IsValid) :
            Purpose == TrayObservationPurpose.PostPlacementCheck && CheckRound > 1 && RelatedTransitionId is { } transition && transition != Guid.Empty && FLocation is null);
}
