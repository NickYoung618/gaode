namespace Gaode.Infrastructure.Devices.Plc;

/// <summary>Transport boundary used by the PLC pump; it never retries writes.</summary>
public interface IPlcTransport
{
    Task<ushort[]> ReadRegistersAsync(ushort offset, ushort count, CancellationToken cancellationToken = default);
    Task<bool[]> ReadCoilsAsync(ushort offset, ushort count, CancellationToken cancellationToken = default);
    Task WriteCoilAsync(ushort offset, bool value, CancellationToken cancellationToken = default);
    Task WriteRegisterAsync(ushort offset, ushort value, CancellationToken cancellationToken = default);
    Task WriteRegistersAsync(ushort offset, ushort[] values, CancellationToken cancellationToken = default);
}
