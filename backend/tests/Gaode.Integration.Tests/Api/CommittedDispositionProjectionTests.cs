using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Application.Workflow;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Host.Api;
using Xunit;

namespace Gaode.Integration.Tests.Api;

public sealed class CommittedDispositionProjectionTests
{
    private readonly Guid run = Guid.NewGuid(), tray = Guid.NewGuid(), operation = Guid.NewGuid();
    private static readonly DateTimeOffset Time = DateTimeOffset.Parse("2026-09-27T00:00:00Z");
    private SortingAssignment Assignment => new(operation, "member", "P03", 3,
        new FixedPoint("P03", "v1", 10, 20, "mm", "frame", 30),
        new FixedPoint("P15", "v1", 40, 50, "mm", "frame", 60), "Pending", "result", "reservation");
    private static RunResultProjection Result(string kind, string id, string quality = "Pending") =>
        new(kind, id, quality, "result", "plan", Guid.NewGuid(), "Simulated", "Derived");
    private StageEvent Fact(int sequence, object payload, StageEventType type = StageEventType.Executing,
        WholeTrayWorkflowStage stage = WholeTrayWorkflowStage.Sorting) => new(Guid.NewGuid(), run, tray,
        "S01", "L01", stage, operation, 1, 1, type, Time.AddSeconds(sequence), Time.AddSeconds(sequence),
        ResultSource.Simulated, ResultQuality.Derived, null, "digest", JsonSerializer.Serialize(payload),
        Guid.NewGuid().ToString(), sequence, Time.AddDays(1), "plan");
    private StageEvent Reserve => Fact(1, new { kind = "SortingAssignmentsReserved", assignments = new[] { Assignment } },
        StageEventType.IntentRecorded);
    // Explicit semantic component evidence. Durable reference identity validation
    // belongs to the real store/API tests; these values make no TCP/DB claim.
    private readonly Guid action = Guid.NewGuid(), session = Guid.NewGuid();
    private readonly DiagnosticEvidenceReference reference = new(Guid.NewGuid(), Guid.NewGuid());
    private ActionCorrelation Correlation => new(run, operation, action, 1, session, 1, "snapshot", "plan", tray,
        ObjectId: "member", PhysicalSlotIndex: 3);
    private ObservationIdentity Observation => new(Guid.NewGuid(), 1, Time, Time, DeviceReliability.Reliable);
    private static ExecutionOrigin Origin => new(DeviceProvider.Simulated, "semantic-fixture/1", EvidenceQuality.Derived);
    private PositionReachedEvidence At(FixedPoint target) => new(Correlation, target,
        new(target.X, target.Y, target.Z, "TestAxes", target.Frame, target.Unit, Observation, Origin), 0.1);
    private PickCompletionEvidence PickEvidence => new(Correlation, "member", 3, Assignment.ReservationReference,
        SortingTargetAllocator.AssignmentDigest(Assignment), new("P03", "v1", "fixture-source-identity"),
        new("P15", "v1", "fixture-target-identity"), At(Assignment.SourcePoint), Time, Observation,
        Origin, [reference], new(1, 10000, "fixture", Time, Time.AddSeconds(1)));
    private DeviceActionEvidence TransferEvidence(bool confirmed = true) => new(Correlation,
        confirmed ? DeviceCompletionMeaning.MaterialTransferred : DeviceCompletionMeaning.PositionReached,
        [Observation], [At(Assignment.SourcePoint), At(Assignment.TargetPoint)], null, Origin, [reference]);
    private StageEvent Pick => Fact(2, new { schemaVersion = "sorting-evidence/1", kind = "SortingAssignmentInTransit",
        assignment = Assignment, evidence = PickEvidence });
    private StageEvent Place(bool confirmed = true) => Fact(3, new { schemaVersion = "sorting-evidence/1", kind = "SortingAssignmentOccupied",
        assignment = Assignment, targetOccupied = true, evidence = TransferEvidence(confirmed) }, StageEventType.Completed);
    private (IReadOnlyList<RunResultProjection> Results, IReadOnlyList<RunMovementProjection> Movements) Project(
        params StageEvent[] facts) => CommittedResultProjection.ApplyDisposition(run, tray, "plan", [Result("Member", "member")], facts);

    [Fact]
    public void CompleteTransferEvidenceIsRequiredAndPhysicalSlotDoesNotUseEventSequence()
    {
        Assert.Equal("Reserved", Assert.Single(Project(Reserve).Results).DispositionState);
        Assert.Equal("InTransit", Assert.Single(Project(Reserve, Pick).Results).DispositionState);
        Assert.Equal("InTransit", Assert.Single(Project(Reserve, Pick, Place(false)).Results).DispositionState);
        Assert.Equal("Reserved", Assert.Single(Project(Reserve, Place()).Results).DispositionState);
        var place = Place();
        var projection = Project(Reserve, Pick, place);
        Assert.Equal("Pending", Assert.Single(projection.Results).Disposition);
        Assert.Equal("Completed", Assert.Single(projection.Results).DispositionState);
        var movement = Assert.Single(projection.Movements);
        Assert.Equal(3, movement.PhysicalSlotIndex);
        Assert.Equal("P03", movement.SourcePointRef);
        Assert.Equal("P15", movement.TargetPointRef);
        Assert.Equal(place.EventId, movement.CommittedEventId);
    }

    [Fact]
    public void WrongRunTrayPlanEpochOrAssignmentCannotCompleteAnotherEntity()
    {
        foreach (var wrong in new[] { Place() with { RunId = Guid.NewGuid() }, Place() with { TrayId = Guid.NewGuid() },
            Place() with { PlanRevision = "other" }, Place() with { ConnectionEpoch = 2 },
            Fact(3, new { kind = "SortingAssignmentOccupied", assignment = Assignment with { ObjectId = "other" },
                schemaVersion = "sorting-evidence/1", targetOccupied = true, evidence = TransferEvidence() }, StageEventType.Completed) })
            Assert.Equal("InTransit", Assert.Single(Project(Reserve, Pick, wrong).Results).DispositionState);
    }

    [Fact]
    public void UnknownHeldIsVisibleAndDoesNotBecomeCompletedFromAStaleReceipt()
    {
        var unknown = Fact(3, new { reason = "Disconnected" }, StageEventType.UnknownHeld);
        Assert.Equal("UnknownHeld", Assert.Single(Project(Reserve, Pick, unknown, Place() with {
            PersistedAt = Time.AddSeconds(4) }).Results).DispositionState);
    }

    [Fact]
    public void UnknownHeldCannotBeReopenedByLatePickThenPlaceClaims()
    {
        var unknown = Fact(3, new { reason = "EvidenceCommitUnknown" }, StageEventType.UnknownHeld);
        var projection = Project(Reserve, unknown, Pick with { PersistedAt = Time.AddSeconds(4) },
            Place() with { PersistedAt = Time.AddSeconds(5) });
        Assert.Equal("UnknownHeld", Assert.Single(projection.Results).DispositionState);
    }

    [Theory]
    [InlineData("DISPOSITION/current-pick-wrong-target")]
    [InlineData("DISPOSITION/current-place-no-position")]
    public void CurrentEvidenceMustProveTheReservedSourceAndDestination(string caseId)
    {
        var pick = caseId.EndsWith("wrong-target", StringComparison.Ordinal)
            ? Fact(2, new { schemaVersion = "sorting-evidence/1", kind = "SortingAssignmentInTransit", assignment = Assignment,
                evidence = PickEvidence with { SourcePositionReached = At(Assignment.TargetPoint) } }) : Pick;
        var place = caseId.EndsWith("no-position", StringComparison.Ordinal)
            ? Fact(3, new { schemaVersion = "sorting-evidence/1", kind = "SortingAssignmentOccupied", assignment = Assignment,
                targetOccupied = true, evidence = TransferEvidence() with { Positions = [] } }, StageEventType.Completed) : Place();
        var projection = Project(Reserve, pick, place);
        Assert.NotEqual("Completed", Assert.Single(projection.Results).DispositionState);
    }

    [Fact]
    public void ExplicitNoMoveAppliesToPhysicalEntitiesWithoutInventingMovementsOrPartDisposition()
    {
        var facts = new[] { Fact(1, new { kind = "NoAdditionalSortingRequired", ordinaryOk = new[] { "assembly", "part" } },
            StageEventType.Completed) };
        var projection = CommittedResultProjection.ApplyDisposition(run, tray, "plan",
            [Result("Assembly", "assembly", "OK"), Result("Part", "part", "OK"), Result("Group", "group", "OK")], facts);
        Assert.Equal("NoMoveRequired", projection.Results[0].DispositionState);
        Assert.Null(projection.Results[1].DispositionState);
        Assert.Null(projection.Results[2].DispositionState);
        Assert.Empty(projection.Movements);
        Assert.Null(Assert.Single(Project().Results).DispositionState);
    }

    [Fact]
    public void MixedTrayRetainsExplicitOrdinaryOkAndMovesOnlyTheProblemMember()
    {
        var reserve = Fact(1, new { kind = "SortingAssignmentsReserved", assignments = new[] { Assignment },
            ordinaryOk = new[] { "normal" } }, StageEventType.IntentRecorded);
        var projection = CommittedResultProjection.ApplyDisposition(run, tray, "plan",
            [Result("Member", "member"), Result("Member", "normal", "OK"), Result("Face", "member:face1")],
            [reserve, Pick, Place()]);
        Assert.Equal("Completed", projection.Results[0].DispositionState);
        Assert.Equal("NoMoveRequired", projection.Results[1].DispositionState);
        Assert.Null(projection.Results[2].DispositionState);
        Assert.Equal("member", Assert.Single(projection.Movements).EntityId);
    }

    [Theory]
    [InlineData(null, "Completed", true, false)]
    [InlineData("member", "Completed", false, false)]
    [InlineData(null, "UnknownHeld", false, false)]
    [InlineData(null, "Completed", false, true)]
    public void SpecialExitRequiresReleasedOccupancyActualReferencesAndSameEntity(
        string? occupied, string status, bool legacy, bool completed)
    {
        var request = new AuxiliaryHandlingRequest(Correlation, AuxiliaryHandlingKind.Exit, 5, "Pending",
            "Test/provenance-not-a-physical-point", Assignment.TargetPoint, "Test", Guid.NewGuid(),
            new(1, 10000, "fixture", Time, Time.AddSeconds(1)));
        var exit = new AuxiliaryHandlingResult(request, TransferEvidence(status == "Completed"), occupied, "Pending");
        object payload = legacy ? new { schemaVersion = "device-semantics/1", kind = "SpecialExitCompleted", objectId = "member", result = exit } :
            new { schemaVersion = "device-semantics/1", kind = "SpecialExitCompleted", objectId = "member", result = exit, sourceSlotId = "P03", targetPointRef = "P15" };
        var projection = Project(Fact(1, payload, stage: WholeTrayWorkflowStage.Detection));
        Assert.Equal(completed ? "Completed" : null, Assert.Single(projection.Results).DispositionState);
        if (completed) Assert.Equal("P03", Assert.Single(projection.Movements).SourcePointRef);
        else Assert.Empty(projection.Movements);
    }
}
