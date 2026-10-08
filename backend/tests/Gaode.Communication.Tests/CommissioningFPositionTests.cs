using System.Diagnostics;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Domain.Configuration;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Media;
using Gaode.Infrastructure.Simulation;
using Xunit;
namespace Gaode.Communication.Tests;
public sealed class CommissioningFPositionTests
{
    [Fact]
    public async Task SavedVersionLocationIsFrozenPerRunAndOutOfTravelIsRejected()
    {
        using var fixture = new ControlledCommissioningTests.Inputs();
        var media = new MediaStore(fixture.Root, new(65536, 0, 65536, 65536), new(), 1);
        var algorithm = new CommissioningAlgorithm(fixture.Loaded, media);
        var first = fixture.Config.FLocation!;
        var edited = first with { X = first.X + 1, Y = first.Y + 1, SourceReference = "OFFLINE:edited-saved-recipe/2" };
        var tray = Guid.NewGuid(); var a = Guid.NewGuid(); var b = Guid.NewGuid();
        algorithm.FreezeRun(a, tray, fixture.Config.ExpectedRecipe.ScenarioId, fixture.Public, fLocation:first);
        algorithm.FreezeRun(b, tray, fixture.Config.ExpectedRecipe.ScenarioId, fixture.Public, fLocation:edited);
        var observedA = await Observe(a); var observedB = await Observe(b);
        Assert.Equal(first, observedA.FLocation); Assert.Equal(edited, observedB.FLocation);
        Assert.Equal(first, (await Observe(a)).FLocation);
        Assert.Throws<InvalidOperationException>(() => algorithm.FreezeRun(Guid.NewGuid(), tray,
            fixture.Config.ExpectedRecipe.ScenarioId, fixture.Public,
            fLocation:edited with { X = fixture.Public.Motion.Limits.XMax + 1 }));
        Assert.Equal(0, media.ActiveLeases);
        var evidence = ControlledCommissioningTests.Inputs.EvidencePath("F-position-run-isolation.json");
        File.WriteAllText(evidence, JsonSerializer.Serialize(new { scope="OFFLINE:actual-media-read;no-device-motion", observedA, observedB,
            oldRunStillUsesOldLocation=true, outsideTravelRejected=true }, new JsonSerializerOptions(JsonSerializerDefaults.Web){WriteIndented=true}));
        async Task<TrayObservation> Observe(Guid run)
        {
            var capture = Guid.NewGuid();
            using var reservation = media.ReserveCapture(capture, "3D", 1024);
            var image = await media.SaveAsync(run, capture, "3D", "1", "1", [3,5,7], "bin", "OFFLINE", default);
            await media.MarkCommittedAsync(image, default);
            var tick = Stopwatch.GetTimestamp();
            var request = new AlgorithmRequest(new(run, Guid.NewGuid(), 1, Guid.NewGuid(), "OFFLINE", "1", fixture.Config.Purpose,
                tick, tick+Stopwatch.Frequency, "OFFLINE"), Guid.NewGuid(), capture, AlgorithmRole.TrayPose,
                [image], "1", "tray.observation", "1.0", Guid.NewGuid(), "OFFLINE")
                { ObservationContext = new(tray, TrayObservationPurpose.InitialPreparation, 1, null) };
            var events = new List<AlgorithmEvent>();
            var dispatch = await algorithm.RequestAsync(request, events.Add, default); await dispatch.Exited;
            return Assert.Single(events,e=>e.Kind==AlgorithmEventKind.Result).Observation!;
        }
    }
}
