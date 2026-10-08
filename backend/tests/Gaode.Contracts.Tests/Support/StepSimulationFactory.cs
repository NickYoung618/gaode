using Gaode.Application.Configuration;
using Gaode.Infrastructure.Devices.Plc;
using Gaode.Infrastructure.Persistence;
using Gaode.Infrastructure.Simulation;

namespace Gaode.Contracts.Tests.Support;

// Assembly-only communication evidence wiring for the existing step fixture.
// No business assertion, protocol value or raw evidence is exposed to its caller.
internal static class StepSimulationFactory
{
    internal static SimulatedPlc Create(FrozenConfiguration config, SimulationEventScheduler scheduler,
        TraceWriter writer, Guid storeId, TimeProvider clock) => new(
            config.Simulation ?? throw new InvalidOperationException("TestSimulationRequired"), scheduler,
            evidenceRecorder: new CommunicationEvidenceRecorder(writer, storeId, clock,
                config.Budget.BusinessMs.CriticalSave));
}
