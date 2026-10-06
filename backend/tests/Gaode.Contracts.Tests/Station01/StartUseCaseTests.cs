using Gaode.Domain.Station01;
using Gaode.Application.Motion;
using Gaode.Application.Ports;
using Gaode.Application.Station01;
using Gaode.Contracts.Tests.Support;
using Xunit;

namespace Gaode.Contracts.Tests.Station01;

public sealed class StartUseCaseTests
{
    [Fact]
    public void RequestIdentityIsIdempotentAndChangedPayloadConflicts()
    {
        var registry = new CommandRegistry();
        var first = registry.Register("test:Operator", "request-1", "{\"value\":1}");
        var repeated = registry.Register("test:Operator", "request-1", "{\"value\":1}");
        Assert.Equal(first, repeated);
        Assert.Throws<InvalidOperationException>(() =>
            registry.Register("test:Operator", "request-1", "{\"value\":2}"));
        Assert.Throws<InvalidOperationException>(() =>
            registry.Register("test:Operator", "request-2", "{\"value\":1}"));
    }

    [Fact]
    public void StartupReadinessUsesSafetyAndControlFactsWithoutAlgorithmReadiness()
    {
        var (config, _, _) = TestConfiguration.Normal();
        var device = new ReadyDevice();
        var readiness = new StartupReadiness(new MotionCoordinator(device, device, device, device,
            new ResourceLease()));
        Assert.True(readiness.Check(config, new ControlLatch()).Ready);
        device.Observation = device.Observation with { SafetyAssessment = SafetyAssessment.ExplicitUnsafe };
        Assert.Contains("SafetyInterlockDenied", readiness.Check(config, new ControlLatch()).Reasons);
        device.Observation = device.Observation with { SafetyAssessment = SafetyAssessment.Clear };
        var control = new ControlLatch();
        control.RequestStop();
        Assert.Contains("ControlAdmissionClosed", readiness.Check(config, control).Reasons);
    }

    [Fact]
    public void DisconnectedObservationIsUnconfirmedNotExplicitUnsafe()
    {
        var (config, _, _) = TestConfiguration.Normal();
        var device = new ReadyDevice();
        var readiness = new StartupReadiness(new MotionCoordinator(device, device, device, device,
            new ResourceLease()));
        device.Observation = device.Observation with
        {
            Connection = DeviceConnection.Disconnected, OperatingMode = OperatingMode.Unconfirmed,
            SafetyAssessment = SafetyAssessment.Unconfirmed, ReasonCodes = ["PlcHeartbeatLost"]
        };
        var unknown = readiness.Check(config, new ControlLatch());
        Assert.Equal("Unconfirmed", unknown.SafetyAssessment);
        Assert.Contains("PlcHeartbeatLost", unknown.Reasons);
        Assert.DoesNotContain("SafetyInterlockDenied", unknown.Reasons);
        device.Observation = device.Observation with
        {
            Connection = DeviceConnection.Connected, OperatingMode = OperatingMode.Automatic,
            SafetyAssessment = SafetyAssessment.ExplicitUnsafe, ReasonCodes = []
        };
        var unsafeResult = readiness.Check(config, new ControlLatch());
        Assert.Equal("ExplicitUnsafe", unsafeResult.SafetyAssessment);
        Assert.Contains("SafetyInterlockDenied", unsafeResult.Reasons);
    }

    private sealed class ReadyDevice : IPlcStatePort, IPlcActionPort, IMotionPort, IAcquisitionCyclePort
    {
        public ValueTask<AcquisitionSession> OpenCaptureWindowAsync(CaptureWindowRequest request, CancellationToken token) =>
            throw new InvalidOperationException("Readiness test must not acquire a capture window");
        public Task<CaptureCycleResult> FinishCaptureWindowAsync(AcquisitionSession session, CaptureWorkCommit work, ActionWindow window, CancellationToken token) =>
            throw new InvalidOperationException("Readiness test must not release a capture window");
        public Task<CaptureCycleResult> CloseFailedCaptureWindowAsync(AcquisitionSession session, string reason, ActionWindow window, CancellationToken token) =>
            throw new InvalidOperationException("Readiness test must not perform device cleanup");
        public DeviceObservation Observation { get; set; } = SemanticDeviceFixture.Ready(ClampState.Released);
        public DeviceObservation Observe() => Observation;
        public ValueTask RequestStartAsync(PortEnvelope envelope, Guid actionId,
            Guid intentWriteId, Action<DeviceEvent> onEvent, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public ValueTask RequestStopAsync(PortEnvelope envelope, Action<DeviceEvent> onEvent,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask RequestMoveAsync(MoveRequest request, Action<DeviceEvent> onEvent,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
