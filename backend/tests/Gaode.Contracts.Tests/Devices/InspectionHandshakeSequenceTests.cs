using Gaode.Application.Ports;
using Gaode.Contracts.Tests.Support;
using Gaode.Domain.Station01;
using Xunit;

namespace Gaode.Contracts.Tests.Devices;

// Business session/identity obligations; numeric handshakes are in Communication.Tests.
public sealed class InspectionHandshakeSequenceTests
{
    [Fact]
    public async Task SameOperationEpochAndThreeCoordinatesGateCaptureWindow()
    {
        await using var h = Station01StepHarness.Create();
        await h.CompleteStartAsync();
        var request = await Move(h, CaptureRole.ThreeD);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await h.Plc.OpenCaptureWindowAsync(
            request with { Correlation = request.Correlation with { OperationId = Guid.NewGuid() } }, default));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await h.Plc.OpenCaptureWindowAsync(
            request with { Correlation = request.Correlation with { ConnectionEpoch = request.Correlation.ConnectionEpoch + 1 } }, default));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await h.Plc.OpenCaptureWindowAsync(
            request with { Target = request.Target with { Z = request.Target.Z + 0.5 } }, default));
        var session = await h.DriveAsync(h.Plc.OpenCaptureWindowAsync(request, default).AsTask());
        Assert.Equal(AcquisitionState.CaptureAllowed, session.State);
        var released = await Release(h, session);
        Assert.Equal(AcquisitionState.Released, released.State);
        Assert.Equal(new[] { AcquisitionState.CaptureAllowed, AcquisitionState.Released }, h.Plc.AcquisitionTransitions);
        Assert.Equal(AcquisitionReadiness.Available, h.Plc.Observe().AcquisitionReadiness);
    }
    [Fact]
    public async Task NewMoveCreatesANewCorrelationAndRejectsPreviousSession()
    {
        await using var h = Station01StepHarness.Create();
        await h.CompleteStartAsync();
        var first = await Move(h, CaptureRole.ThreeD);
        var session = await h.DriveAsync(h.Plc.OpenCaptureWindowAsync(first, default).AsTask());
        Assert.Equal(AcquisitionState.Released, (await Release(h, session)).State);
        var next = await Move(h, CaptureRole.F);
        Assert.NotEqual(first.Correlation.OperationId, next.Correlation.OperationId);
        Assert.NotEqual(first.Correlation.ActionId, next.Correlation.ActionId);
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await h.Plc.OpenCaptureWindowAsync(first, default));
        var nextSession = await h.DriveAsync(h.Plc.OpenCaptureWindowAsync(next, default).AsTask());
        Assert.Equal(AcquisitionState.CaptureAllowed, nextSession.State);
        Assert.Equal(next, nextSession.Request);
        Assert.NotEqual(session.SessionId, nextSession.SessionId);
    }
    private static async Task<CaptureCycleResult> Release(Station01StepHarness h, AcquisitionSession session)
    {
        using var saved = await Gaode.Communication.Tests.CaptureWorkFixture.CreateAsync(session);
        return await h.DriveAsync(h.Plc.FinishCaptureWindowAsync(session, saved.Work, session.Request.Window, default));
    }
    private static async Task<CaptureWindowRequest> Move(Station01StepHarness h, CaptureRole role)
    {
        var tick = h.Clock.GetTimestamp();
        var envelope = new PortEnvelope(h.Run.RunId, Guid.NewGuid(), 1, h.Run.SessionId, h.Config.SnapshotId,
            h.Config.Public.Version, "Test", tick, tick + h.Clock.TimestampFrequency * 5, h.Run.ClockId);
        var move = new MoveRequest(envelope, Guid.NewGuid(), new(role.ToString(), "1", 100, 101, "mm", "SIM", role == CaptureRole.F ? 175 : 150),
            Guid.NewGuid(), "sim-plc", role == CaptureRole.F ? "F" : "3D");
        var completed = new TaskCompletionSource<DeviceEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        await h.Plc.RequestMoveAsync(move, e => { if (e.Kind is DeviceEventKind.Completed or DeviceEventKind.UnknownHeld) completed.TrySetResult(e); }, default);
        var result = await h.DriveAsync(completed.Task);
        Assert.Equal(DeviceEventKind.Completed, result.Kind);
        var position = Assert.Single(result.Evidence!.Positions);
        return new(result.Evidence.Correlation, role, move.Target, position, new(envelope.StartTick, envelope.DueTick,
            envelope.ClockId, h.Clock.GetUtcNow(), h.Clock.GetUtcNow().AddSeconds(5)));
    }
}
