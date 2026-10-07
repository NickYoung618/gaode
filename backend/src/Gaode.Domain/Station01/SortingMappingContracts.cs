using Gaode.Domain.Configuration;

namespace Gaode.Domain.Station01;

public sealed record SortingActionPlan(
    Guid RunId,
    Guid TrayId,
    string RecipePlanVersion,
    int Sequence,
    string ObjectId,
    FixedPoint Position,
    string Classification,
    Guid OperationId,
    long ConnectionEpoch,
    string SlotId,
    string? MemberId,
    string Disposition = "NG")
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool ReturnsToOrigin { get; init; }
    public int? PhysicalSlotIndex { get; init; }
    public bool IsPosePending { get; init; }
    public string? ObservationReference { get; init; }
    public string? DetectionState { get; init; }
    public bool IsValid => RunId != Guid.Empty && TrayId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(RecipePlanVersion) && Sequence >= 1 &&
        !string.IsNullOrWhiteSpace(ObjectId) && Position is not null &&
        !string.IsNullOrWhiteSpace(Position.Id) && !string.IsNullOrWhiteSpace(Classification) &&
        OperationId != Guid.Empty && ConnectionEpoch > 0 && !string.IsNullOrWhiteSpace(SlotId) &&
        (Disposition is "NG" or "Pending" || Disposition == "OK" && ReturnsToOrigin);
}

public sealed record SortingMappingFailure(
    Guid RunId,
    Guid TrayId,
    string RecipePlanVersion,
    IReadOnlyList<string> MissingObjectIds,
    IReadOnlyList<string> DuplicateObjectIds,
    IReadOnlyList<string> AmbiguousObjectIds,
    string Reason)
{
    public bool IsValid => RunId != Guid.Empty && TrayId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(RecipePlanVersion) &&
        (MissingObjectIds.Count > 0 || DuplicateObjectIds.Count > 0 ||
         AmbiguousObjectIds.Count > 0) && !string.IsNullOrWhiteSpace(Reason);

    public bool AllowsPartialDispatch => false;
}

public sealed record SortingMappingResult(
    bool IsComplete,
    IReadOnlyList<SortingActionPlan> Actions,
    SortingMappingFailure? Failure,
    Guid MappingOperationId,
    long ConnectionEpoch)
{
    public bool IsValid => MappingOperationId != Guid.Empty && ConnectionEpoch > 0 &&
        ((IsComplete && Failure is null && Actions.All(x => x.IsValid)) ||
         (!IsComplete && Actions.Count == 0 && Failure is { IsValid: true }));
}
