using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Application.Workflow;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Xunit;

namespace Gaode.Contracts.Tests.Support;

// Unit-only semantic device and evidence fixture. It supplies no protocol fields
// and makes no TCP, SQLite or independent-process acceptance claim.
internal static class SemanticStageFixture
{
    public static readonly ExecutionOrigin Origin = new(DeviceProvider.Simulated, "SemanticUnitFixture/1", EvidenceQuality.Derived);
    public static PlcStageActionResult Result(PlcStageActionRequest request, StageActionKind kind, DateTimeOffset at,
        string? error = null, bool holds = false, bool retry = false)
    {
        var identity = new ObservationIdentity(Guid.NewGuid(), request.ConnectionEpoch, at, at, DeviceReliability.Reliable);
        var positions = request.Stage switch
        {
            PlcWorkflowStage.Sorting or PlcWorkflowStage.TransferToRotation => new[] { Position(request, request.SortingSource!, identity), Position(request, request.SortingTarget!, identity) },
            PlcWorkflowStage.UnloadPreparation => new[] { Position(request, request.UnloadTarget!, identity) },
            _ => Array.Empty<PositionReachedEvidence>()
        };
        var evidence = kind != StageActionKind.Completed ? null : new DeviceActionEvidence(request.Correlation,
            request.Stage switch { PlcWorkflowStage.Sorting or PlcWorkflowStage.TransferToRotation => DeviceCompletionMeaning.MaterialTransferred,
                PlcWorkflowStage.UnloadPreparation => DeviceCompletionMeaning.UnloadPrepared, _ => DeviceCompletionMeaning.Unlocked },
            [identity], positions, null, Origin, [new(Guid.NewGuid(), Guid.NewGuid())])
            { SafeReached = request.TransferPurpose is null ? null : Position(request,request.SortingTarget!,identity) };
        return new(request, kind, request.Correlation.ActionId, request.ConnectionEpoch, error, holds, retry, at, Origin, evidence);
    }
    public static async Task CommitPickAsync(SortingTargetAllocator allocator, PlcStageActionRequest request, TimeProvider clock, CancellationToken token)
    {
        var at = clock.GetUtcNow();
        var observed = new ObservationIdentity(Guid.NewGuid(), request.ConnectionEpoch, at, at, DeviceReliability.Reliable);
        var evidence = new PickCompletionEvidence(request.Correlation, request.Correlation.ObjectId!, request.PhysicalSlotIndex!.Value,
            request.ReservationReference!, request.ActionParametersDigest, Identity(request.SortingSource!), Identity(request.SortingTarget!),
            Position(request, request.SortingSource!, observed), at, observed, Origin, [new(Guid.NewGuid(), Guid.NewGuid())], request.Window);
        // The real business allocator still validates the actual reservation/action intent
        // in the owning test's explicit memory-store fixture before issuing its receipt.
        var receipt = await allocator.CommitPickAsync(evidence, token);
        Assert.True(receipt.MayAuthorizePlace(evidence, clock.GetTimestamp()), JsonSerializer.Serialize(receipt));
    }
    private static PositionReachedEvidence Position(PlcStageActionRequest request, FixedPoint point, ObservationIdentity observation) =>
        new(request.Correlation, point, new(point.X, point.Y, point.Z, "unit-axes", point.Frame, point.Unit, observation, Origin), request.PositionTolerance);
    private static FixedPointIdentity Identity(FixedPoint p) => new(p.Id, p.Version,
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(p)))));
}
