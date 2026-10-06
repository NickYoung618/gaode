using Gaode.Domain.Station01;

namespace Gaode.Application.Ports;

public interface IPlcStatePort
{
    DeviceObservation Observe();
}
