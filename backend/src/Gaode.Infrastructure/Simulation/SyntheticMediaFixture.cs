using System.Text;

namespace Gaode.Infrastructure.Simulation;

public static class SyntheticMediaFixture
{
    public static byte[] Create(string role, Guid runId, Guid captureId, string scopeVersion) =>
        Encoding.UTF8.GetBytes($"SIMULATED|{role}|{runId:D}|{captureId:D}|{scopeVersion}|SyntheticTestFixture");
}
