using Gaode.Application.Acquisition;
using Gaode.Application.Ports;
using Xunit;

namespace Gaode.Contracts.Tests.Acquisition;

public sealed class CaptureCoordinationTests
{
    [Theory]
    [InlineData("run")]
    [InlineData("capture")]
    [InlineData("operation")]
    [InlineData("epoch")]
    [InlineData("settings")]
    public void CaptureFactMustMatchCurrentRequestAndFirstOwnedBuffer(string mismatch)
    {
        var request = Request();
        var fact = new CorrelatedCaptureFact(request.Envelope.RunId, request.CaptureId, request.Envelope.OperationId,
            1, AcquisitionContract.RequestedSettingsDigest(request), "DeclaredFixture",
            Gaode.Domain.Station01.ComponentExecutionOrigin.Unknown, Gaode.Domain.Station01.ComponentExecutionOrigin.Unknown,
            CaptureApplicationState.Unknown, null, false, []);
        var wrong = mismatch switch {
            "run" => fact with { RunId = Guid.NewGuid() }, "capture" => fact with { CaptureId = Guid.NewGuid() },
            "operation" => fact with { OperationId = Guid.NewGuid() }, "epoch" => fact with { ConnectionEpoch = 2 },
            _ => fact with { RequestedSettingsDigest = "other-settings" }
        };
        var gate = new CaptureEvidenceGate();
        Assert.False(gate.Observe(new(request, CaptureEventKind.Ended, 1)));
        Assert.False(gate.Observe(new(request, CaptureEventKind.MediaTaken, 1, [9], "png") { Fact = wrong }));
        Assert.Throws<InvalidOperationException>(() => gate.TakeFact(request, 1));
        Assert.True(gate.Observe(new(request, CaptureEventKind.MediaTaken, 1, [1, 2], "png") { Fact = fact }));
        Assert.False(gate.Observe(new(request, CaptureEventKind.MediaTaken, 1, [3], "png") { Fact = fact }));
        Assert.Equal(new byte[] { 1, 2 }, gate.Take().Buffer);
        Assert.Equal(fact, gate.TakeFact(request, 1));
        Assert.False(gate.TakeFact(request, 1).CameraOrigin.IsKnown);
        Assert.Null(gate.TakeFact(request, 1).ActualSettings);
    }

    [Fact]
    public void MissingAdapterFactCannotBeFilledFromRequestedSettings()
    {
        var request = Request();
        var gate = new CaptureEvidenceGate();
        gate.Observe(new(request, CaptureEventKind.Ended, 1));
        gate.Observe(new(request, CaptureEventKind.MediaTaken, 1, [1], "png"));
        Assert.Throws<InvalidOperationException>(() => gate.TakeFact(request, 1));
    }

    [Fact]
    public void EndedAndMediaTakenAreIndependentAndFirstOwnedBufferWins()
    {
        var request = Request();
        var gate = new CaptureEvidenceGate();
        Assert.False(gate.Observe(new(request, CaptureEventKind.MediaTaken, 1, [1, 2], "img")));
        Assert.False(gate.Observe(new(request, CaptureEventKind.MediaTaken, 1, [9], "img")));
        Assert.True(gate.Observe(new(request, CaptureEventKind.Ended, 1)));
        var owned = gate.Take();
        Assert.Equal(new byte[] { 1, 2 }, owned.Buffer);
        Assert.Equal("img", owned.Format);
    }

    [Fact]
    public void FrameWithoutEndedAndEndedWithoutContentCannotCrossCompletionGate()
    {
        var frameOnly = new CaptureEvidenceGate();
        frameOnly.Observe(new(Request(), CaptureEventKind.MediaTaken, 1, [1], "img"));
        Assert.Throws<InvalidOperationException>(() => frameOnly.Take());
        var endedOnly = new CaptureEvidenceGate();
        endedOnly.Observe(new(Request(), CaptureEventKind.Ended, 1));
        Assert.Throws<InvalidOperationException>(() => endedOnly.Take());
    }

    [Fact]
    public async Task ActualUnknownMediaSourceIsNotFilledFromSimulationFixture()
    {
        await using var harness = Gaode.Contracts.Tests.Support.Station01StepHarness.Create(mediaSourceOverride: "Unknown");
        await harness.CompleteStartAsync();
        var captured = await harness.DriveAsync(harness.ThreeD.ExecuteAsync(harness.Run, harness.Control, default));
        Assert.Equal("Unknown", captured.Media.Source);
        var saved = harness.Writer.Batches.Single(x => x.Kind == WriteKind.CaptureFact);
        var fact = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(saved.PayloadJson).GetProperty("captureFact");
        Assert.Equal("Unknown", fact.GetProperty("mediaSource").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, fact.GetProperty("actualSettings").ValueKind);
        Assert.NotEqual(harness.Config.Simulation.Fixtures.MediaSource, captured.Media.Source);
    }

    private static CaptureRequest Request()
    {
        var envelope = new PortEnvelope(Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(),
            "snapshot", "1.0.0", "Test", 1, 100, "clock");
        return new(envelope, Guid.NewGuid(), CaptureRole.F, "f", "1", null, null,
            "camera", "light", Guid.NewGuid(), 1024);
    }
}
