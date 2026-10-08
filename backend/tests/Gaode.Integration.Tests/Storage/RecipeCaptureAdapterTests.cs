using Gaode.Application.Ports;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Devices.Cameras;
using Xunit;

namespace Gaode.Integration.Tests.Storage;

// Gateway doubles prove request/configure/trigger ordering, never a physical device Applied fact.
public sealed class RecipeCaptureAdapterTests
{
    [Fact]
    public async Task CameraAndLightConfigureTheActualRequestBeforeEachTrigger()
    {
        var calls = new List<string>();
        var camera = new Camera(calls); var light = new Light(calls);
        var adapter = new CameraCaptureAdapter(camera, light);
        var envelope = new PortEnvelope(Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(), "snapshot", "1", "Test", 10, 20, "clock");
        foreach (var (role, exposure, gain, brightness) in new[] { (CaptureRole.Detection, 120, 1.2, 23), (CaptureRole.E, 370, 2.6, 68) })
        {
            calls.Clear(); var events = new List<CaptureEvent>();
            var request = new CaptureRequest(envelope, Guid.NewGuid(), role, "actual-point", "1", "object", "1",
                role.ToString(), "configured-light", Guid.NewGuid(), 1024)
            { DetectionSettings = new("local-profile", exposure, gain, [0, 0, 4, 4], "configured-channel", brightness, 0) };
            await adapter.RequestCaptureAsync(request, events.Add, CancellationToken.None);
            Assert.DoesNotContain("open", calls);
            Assert.Contains($"camera:{exposure}:{gain}", calls);
            Assert.Contains($"light:{brightness}", calls);
            Assert.True(calls.IndexOf("trigger") > calls.IndexOf($"camera:{exposure}:{gain}"));
            Assert.True(calls.IndexOf("trigger") > calls.IndexOf($"light:{brightness}"));
            var fact = Assert.Single(events, e => e.Kind == CaptureEventKind.MediaTaken).Fact!;
            Assert.Equal(CaptureApplicationState.ConfiguredOnly, fact.ApplicationState);
            Assert.Equal(ComponentEvidenceSource.Simulated, fact.LightOrigin.Source);
            Assert.False(fact.PhysicalLightApplied);
        }
    }
    private sealed class Camera(List<string> calls) : ICameraSdkGateway
    {
        public long ConnectionEpoch => 1;
        public Task OpenAsync(string binding, CancellationToken cancellationToken = default) { calls.Add("open"); return Task.CompletedTask; }
        public Task ConfigureAsync(string binding, int exposure, double gain, CancellationToken cancellationToken = default)
        { calls.Add($"camera:{exposure}:{gain}"); return Task.CompletedTask; }
        public Task<CameraFrame> TriggerAsync(string binding, string version, CancellationToken cancellationToken = default)
        {
            calls.Add("trigger");
            var now = DateTimeOffset.UtcNow;
            // Declared gateway output for the ordering test, not an SDK/hardware observation.
            return Task.FromResult(new CameraFrame([1, 2], "img", "application/octet-stream", 1)
            {
                Metadata = new("camera-frame/1", binding, "DeclaredUnitCamera", "", "", "",
                    Guid.NewGuid(), 1, 0, 1, now, now, 2, 1, "Mono8", 2,
                    new Dictionary<string, string>(), [])
            });
        }
        public async Task<CameraFrame> CaptureConfiguredAsync(string binding, string version,
            CameraImagingSettings settings, string settingsDigest, CancellationToken cancellationToken = default)
        {
            await ConfigureAsync(binding, settings.ExposureUs, settings.Gain!.Value, cancellationToken);
            return await TriggerAsync(binding, version, cancellationToken);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class Light(List<string> calls) : ILightGateway
    {
        public ComponentExecutionOrigin Origin => new(ComponentEvidenceSource.Simulated, "DeclaredUnitLight", "ordering-test/1");
        public Task SetBrightnessAsync(string binding, string channel, int brightness, CancellationToken cancellationToken = default)
        { Assert.Equal("configured-channel", channel); calls.Add($"light:{brightness}"); return Task.CompletedTask; }
        public Task SetAsync(string binding, bool enabled, CancellationToken cancellationToken = default)
        { calls.Add(enabled ? "on" : "off"); return Task.CompletedTask; }
    }
}
