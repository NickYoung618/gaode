using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Contracts.Tests.Support;
using Gaode.Domain.Station01;
using Xunit;

namespace Gaode.Contracts.Tests.Station01;

public sealed class ThreeDStepTests
{
    [Fact]
    public async Task OrdinaryPauseDuringTrayObservationCompletesOriginalInspectionReset()
    {
        await using var h = Station01StepHarness.Create();
        await h.CompleteStartAsync();
        var operation = h.ThreeD.ExecuteAsync(h.Run, h.Control, default);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        while (!h.Writer.Batches.Any(x => x.Kind == WriteKind.AlgorithmIntent))
        {
            h.Clock.Advance(TimeSpan.FromMilliseconds(10));
            await Task.Delay(1, timeout.Token);
        }
        h.Control.RequestStop();
        await h.DriveAsync(operation);
        Assert.Equal(new[] { AcquisitionState.CaptureAllowed, AcquisitionState.Released }, h.Plc.AcquisitionTransitions);
        Assert.True(h.Control.PauseRequested);
        Assert.Null(h.Motion.CurrentAction);
        Assert.False(h.Motion.Unknown);
        Assert.Equal(1, h.Capture.TriggerCount(CaptureRole.ThreeD));
        Assert.Equal(1, h.Algorithm.CallCount(AlgorithmRole.TrayPose));
    }

    [Fact]
    public async Task WholeTrayCapturePreservesObservationsAndPersistsIntentBeforeAlgorithmFact()
    {
        await using var harness = Station01StepHarness.Create();
        await harness.CompleteStartAsync();
        var evidence = await harness.DriveAsync(harness.ThreeD.ExecuteAsync(harness.Run, harness.Control,
            CancellationToken.None));
        Assert.True(evidence.Observation.IsValid);
        Assert.Equal(harness.Run.RunId, evidence.Observation.RunId);
        Assert.Equal(new[] { 1, 3 }, evidence.Observation.Slots.Select(s => s.PhysicalSlotIndex));
        Assert.Equal(TrayPose.Abnormal, evidence.Observation.Slots[1].Pose);
        Assert.Equal(205, evidence.Observation.FLocation!.X);
        Assert.Equal(evidence.Observation, harness.Run.InitialObservation);
        Assert.NotNull(harness.Run.InitialObservationWriteId);
        Assert.Equal(1, harness.Capture.TriggerCount(CaptureRole.ThreeD));
        Assert.Equal(1, harness.Plc.MoveCommands);
        Assert.Equal(1, harness.Algorithm.CallCount(AlgorithmRole.TrayPose));
        var batches = harness.Writer.Batches;
        var intent = batches.Single(x => x.Kind == WriteKind.AlgorithmIntent);
        var fact = batches.Single(x => x.Kind == WriteKind.AlgorithmFact);
        Assert.True(Array.IndexOf(batches.ToArray(), intent) < Array.IndexOf(batches.ToArray(), fact));
        var payload = JsonSerializer.Deserialize<AlgorithmIntentPayload>(intent.PayloadJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal(evidence.Media.CaptureId, payload?.CaptureId);
        Assert.Contains(evidence.Media.MediaId, payload?.InputMediaIds ?? []);
        Assert.DoesNotContain("Part", intent.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Face", intent.PayloadJson, StringComparison.OrdinalIgnoreCase);
    }
}
