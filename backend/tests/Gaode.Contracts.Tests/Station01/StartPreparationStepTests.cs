using Gaode.Application.Ports;
using Gaode.Application.Station01.Steps;
using Gaode.Contracts.Tests.Support;
using Gaode.Domain.Station01;
using Xunit;

namespace Gaode.Contracts.Tests.Station01;

public sealed class StartPreparationStepTests
{
    [Fact]
    public async Task OrdinaryPauseStillObservesAndSavesTheDispatchedReadiness()
    {
        await using var h = Station01StepHarness.Create();
        var task = h.Start.ExecuteAsync(h.Run, h.Control, _ => Task.CompletedTask, default);
        Assert.Equal(1, h.Plc.StartCommands);
        h.Control.RequestStop();
        var evidence = await h.DriveAsync(task);
        Assert.True(evidence.DeviceReady);
        Assert.True(h.Control.PauseRequested);
        Assert.Null(h.Motion.CurrentAction);
        Assert.False(h.Motion.Unknown);
        Assert.Equal(1, h.Plc.StartCommands);
    }

    [Fact]
    public void MissingReadinessConnectionEpochOrCancelledControlCannotAuthorizeWork()
    {
        var observed = SemanticDeviceFixture.Ready(ClampState.Unconfirmed);
        Assert.True(StartupObservationPolicy.IsCurrentReady(observed, observed.ConnectionEpoch, false));
        Assert.False(StartupObservationPolicy.IsCurrentReady(observed with { Connection = DeviceConnection.Disconnected }, observed.ConnectionEpoch, false));
        Assert.False(StartupObservationPolicy.IsCurrentReady(observed, observed.ConnectionEpoch + 1, false));
        Assert.False(StartupObservationPolicy.IsCurrentReady(observed, observed.ConnectionEpoch, true));
        Assert.False(StartupObservationPolicy.IsCurrentReady(observed with { Readiness = DeviceReadiness.NotReady }, observed.ConnectionEpoch, false));
    }

    [Fact]
    public async Task StartSavesRealReadinessWithoutInventingClampOrPhysicalButton()
    {
        await using var h = Station01StepHarness.Create();
        var evidence = await h.CompleteStartAsync();
        Assert.Equal(1, h.Plc.StartCommands);
        Assert.Equal(0, h.Plc.MoveCommands);
        Assert.True(evidence.Accepted);
        Assert.True(evidence.DeviceReady);
        Assert.NotEqual(ClampState.Secured, evidence.Observation.Clamp);
        Assert.Equal(WriteKind.StartIntent, h.Writer.Batches[0].Kind);
        Assert.Contains(h.Writer.Batches, x => x.Kind == WriteKind.ActionFact &&
            x.PayloadJson.Contains("StartReadyObserved", StringComparison.Ordinal));
        Assert.DoesNotContain(h.Writer.Batches, x => x.PayloadJson.Contains("ClampCompleted", StringComparison.Ordinal));
        Assert.Equal(0, h.Capture.TriggerCount(CaptureRole.ThreeD));
    }

    [Fact]
    public async Task ReadinessAcceptanceHoldTimesOutWithoutStartingMotion()
    {
        var (_, _, source) = TestConfiguration.Normal();
        var profile = source with { Stages = source.Stages with {
            PlcAcceptance = source.Stages.PlcAcceptance with { Outcome = "Hold" } } };
        await using var h = Station01StepHarness.Create(simulationOverride: profile);
        var task = h.Start.ExecuteAsync(h.Run, h.Control, _ => Task.CompletedTask, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.DriveAsync(task));
        Assert.Equal(0, h.Plc.MoveCommands);
        Assert.True(h.Motion.Unknown);
    }
}
