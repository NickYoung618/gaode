using Gaode.Application.Ports;
using Gaode.Infrastructure.Devices.Plc;

namespace Gaode.Infrastructure.Configuration;

// Device configuration stays at the infrastructure boundary. No motion or polling occurs here.
public static class RotationExecutionConfigurationFactory
{
    public static RotationExecutionConfiguration FromDevice(LatestProtocolPlcDevice device) =>
        new(device.RotationBasis);
}
