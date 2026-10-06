using Gaode.Application.Ports;
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
            Assert.Equal($"camera:{exposure}:{gain}", calls[1]);
            Assert.Equal($"light:{brightness}", calls[2]);
            Assert.True(calls.IndexOf("trigger") > calls.IndexOf($"light:{brightness}"));
            var fact = Assert.Single(events, e => e.Kind == CaptureEventKind.MediaTaken).Fact!;
            Assert.Equal(CaptureApplicationState.Unknown, fact.ApplicationState);
        }
    }
    private sealed class Camera(List<string> calls) : ICameraSdkGateway
    {
        public long ConnectionEpoch => 1;
        public Task OpenAsync(string binding, CancellationToken cancellationToken = default) { calls.Add("open"); return Task.CompletedTask; }
        public Task ConfigureAsync(string binding, int exposure, double gain, CancellationToken cancellationToken = default)
        { calls.Add($"camera:{exposure}:{gain}"); return Task.CompletedTask; }
        public Task<CameraFrame> TriggerAsync(string binding, string version, CancellationToken cancellationToken = default)
        { calls.Add("trigger"); return Task.FromResult(new CameraFrame([1, 2], "img", "application/octet-stream", 1)); }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class Light(List<string> calls) : ILightGateway
    {
        public Task SetBrightnessAsync(string binding, string channel, int brightness, CancellationToken cancellationToken = default)
        { Assert.Equal("configured-channel", channel); calls.Add($"light:{brightness}"); return Task.CompletedTask; }
        public Task SetAsync(string binding, bool enabled, CancellationToken cancellationToken = default)
        { calls.Add(enabled ? "on" : "off"); return Task.CompletedTask; }
    }
}
