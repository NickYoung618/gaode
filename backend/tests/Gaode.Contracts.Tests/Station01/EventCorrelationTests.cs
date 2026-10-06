using Gaode.Application.Ports;
using Gaode.Application.Station01;
using Gaode.Domain.Configuration;
using Xunit;
using Gaode.Domain.Station01;
using Gaode.Contracts.Tests.Support;

namespace Gaode.Contracts.Tests.Station01;

public sealed class EventCorrelationTests
{
    [Fact]
    public void DuplicateAndLateDeviceEvidenceNeverBecomeNewAcceptance()
    {
        var envelope = new PortEnvelope(Guid.NewGuid(), Guid.NewGuid(), 1, Guid.NewGuid(),
            "snapshot", "1", "Test", 1, 10, "clock");
        var eventValue = new DeviceEvent(envelope, DeviceEventKind.Completed, Guid.NewGuid(), 2);
        var reducer = new OperationEvidenceReducer();
        Assert.Equal(EvidenceDisposition.Accepted, reducer.Observe(eventValue, envelope, 2, false));
        Assert.Equal(EvidenceDisposition.Duplicate, reducer.Observe(eventValue, envelope, 2, false));
        Assert.Equal(EvidenceDisposition.Late, reducer.Observe(
            eventValue with { Kind = DeviceEventKind.Failed }, envelope, 2, true));
    }

    [Fact]
    public void PhysicalUnknownAndSafetyFaultsAreNotReportedAsReady()
    {
        var disconnected = PhysicalFaultPolicy.Evaluate(SemanticDeviceFixture.Ready() with { Connection = DeviceConnection.Disconnected, Reliability = DeviceReliability.Unavailable });
        Assert.True(disconnected.Unknown);
        Assert.False(disconnected.Allowed);
        var unsafeState = PhysicalFaultPolicy.Evaluate(SemanticDeviceFixture.Ready() with { SafetyAssessment = SafetyAssessment.ExplicitUnsafe });
        Assert.False(unsafeState.Allowed);
        Assert.False(unsafeState.Unknown);
    }
}
