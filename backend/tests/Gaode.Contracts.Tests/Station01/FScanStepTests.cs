using Gaode.Application.Ports;
using Gaode.Contracts.Tests.Support;
using Gaode.Domain.Station01;
using Xunit;
using System.Text.Json;

namespace Gaode.Contracts.Tests.Station01;

public sealed class FScanStepTests
{
    [Fact]
    public async Task FUsesOneTriggerOneImageAndDoesNotPerformRecipeOperation()
    {
        var actualOrigin = new ComponentExecutionOrigin(ComponentEvidenceSource.Virtual, "DeclaredFProducer/23", "DeclaredUnit");
        await using var harness = Station01StepHarness.Create(algorithmOrigin: actualOrigin);
        await harness.CompleteStartAsync();
        await harness.DriveAsync(harness.ThreeD.ExecuteAsync(harness.Run, harness.Control,
            CancellationToken.None));
        var evidence = await harness.DriveAsync(harness.FScan.ExecuteAsync(harness.Run, harness.Control,
            CancellationToken.None));
        var fact = harness.Writer.Batches.Where(x => x.Kind == WriteKind.AlgorithmFact)
            .Select(x => (Write: x, Fact: JsonSerializer.Deserialize<AlgorithmFactPayload>(x.PayloadJson,
                new JsonSerializerOptions(JsonSerializerDefaults.Web))!))
            .Single(x => x.Fact.CallId == evidence.Algorithm.CallId);
        Assert.Equal(actualOrigin, fact.Fact.Origin);
        Assert.Equal(harness.Run.RunId, fact.Fact.RunId);
        Assert.Equal(evidence.Media.CaptureId, fact.Fact.CaptureId);
        Assert.Equal(fact.Write.WriteId, harness.Run.CommittedFSource!.WriteId);
        Assert.Equal(fact.Fact.Origin, harness.Run.CommittedFSource.Origin);
        var intent = harness.Writer.Batches.Where(x => x.Kind == WriteKind.AlgorithmIntent)
            .Select(x => JsonSerializer.Deserialize<AlgorithmIntentPayload>(x.PayloadJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!)
            .Single(x => x.CallId == evidence.Algorithm.CallId);
        Assert.NotEqual(intent.ExpectedComponentVersion, fact.Fact.Origin.VersionRef);
        Assert.NotEqual(evidence.Algorithm.WorkerSessionId?.ToString(), fact.Fact.Origin.VersionRef);
        Assert.Equal(1, harness.Capture.TriggerCount(CaptureRole.F));
        Assert.Equal(2, harness.Plc.MoveCommands);
        Assert.Equal(1, harness.Algorithm.CallCount(AlgorithmRole.FDecode));
        Assert.Equal(FRecognitionState.Unique, evidence.Code.RecognitionState);
        Assert.Equal("TEST-TRAY-0001", evidence.Code.PrimaryCode);
        Assert.Equal(new[] { AcquisitionState.CaptureAllowed, AcquisitionState.Released },
            harness.Plc.AcquisitionTransitions.TakeLast(2));
        var fCaptureIntents = harness.Writer.Batches.Count(x => x.Kind == WriteKind.CaptureIntent &&
            x.PayloadJson.Contains("CaptureF", StringComparison.Ordinal));
        Assert.Equal(1, fCaptureIntents);
        Assert.DoesNotContain(harness.Writer.Batches,
            x => x.Kind.ToString().Contains("Recipe", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FailedAlgorithmFactSaveDoesNotSubmitFCompletion()
    {
        await using var harness = Station01StepHarness.Create();
        await harness.CompleteStartAsync();
        await harness.DriveAsync(harness.ThreeD.ExecuteAsync(harness.Run, harness.Control,
            CancellationToken.None));
        var before = harness.Plc.AcquisitionTransitions.Count;
        harness.Writer.FailOnKind = WriteKind.AlgorithmFact;
        await Assert.ThrowsAsync<Gaode.Application.Station01.SaveGateException>(async () =>
            await harness.DriveAsync(harness.FScan.ExecuteAsync(harness.Run, harness.Control,
                CancellationToken.None)));
        Assert.Null(harness.Run.CommittedFSource);
        Assert.Equal(1, harness.Capture.TriggerCount(CaptureRole.F));
        Assert.Contains(harness.Plc.AcquisitionTransitions.Skip(before), value => value == AcquisitionState.CaptureAllowed);
        Assert.DoesNotContain(harness.Plc.AcquisitionTransitions.Skip(before), value => value == AcquisitionState.Released);
    }
}
