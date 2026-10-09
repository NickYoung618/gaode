using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Gaode.Infrastructure.Devices.Cameras;

public sealed record CameraHostInterface(string Mac, string Address, string Mask, int InterfaceIndex);
public static class CameraNetworkBinding
{
    [DllImport("iphlpapi.dll")]
    private static extern uint GetBestInterface(uint destination, out uint interfaceIndex);
    public static CameraHostInterface ResolveLoopback(string expectedMac, string cameraIp)
    {
        var camera = IPAddress.Parse(cameraIp);
        if (camera.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(camera))
            throw new InvalidDataException("CameraAddressInvalid");
        var code = GetBestInterface(BitConverter.ToUInt32(camera.GetAddressBytes()), out var best);
        if (code != 0) throw new IOException("CameraRouteLookupFailed:" + code);
        var local = NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses.Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => new CameraHostInterface(n.GetPhysicalAddress().ToString(), a.Address.ToString(), a.IPv4Mask.ToString(),
                    n.GetIPProperties().GetIPv4Properties().Index))).ToArray();
        return Resolve(expectedMac, cameraIp, local, checked((int)best));
    }
    public static CameraHostInterface Resolve(string expectedMac, string cameraIp, IReadOnlyList<CameraHostInterface> local, int bestInterface)
    {
        static string Mac(string value) => new(value.Where(Uri.IsHexDigit).Select(char.ToUpperInvariant).ToArray());
        var camera = IPAddress.Parse(cameraIp);
        if (camera.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(camera) || camera.Equals(IPAddress.Any))
            throw new InvalidDataException("CameraAddressInvalid");
        var candidates = local.Where(n => Mac(n.Mac) == Mac(expectedMac)).Where(n => {
            if (!IPAddress.TryParse(n.Address, out var host) || host.AddressFamily != AddressFamily.InterNetwork ||
                IPAddress.IsLoopback(host) || host.Equals(IPAddress.Any) || host.Equals(camera) ||
                !IPAddress.TryParse(n.Mask, out var mask) || mask.AddressFamily != AddressFamily.InterNetwork || mask.Equals(IPAddress.Any)) return false;
            var h = host.GetAddressBytes(); var c = camera.GetAddressBytes(); var m = mask.GetAddressBytes();
            return Enumerable.Range(0, 4).All(i => (h[i] & m[i]) == (c[i] & m[i]));
        }).ToArray();
        if (candidates.Length != 1) throw new InvalidDataException("CameraBoundHostNotUnique:" + candidates.Length);
        if (candidates[0].InterfaceIndex != bestInterface) throw new InvalidDataException("CameraRouteBindingMismatch");
        return candidates[0];
    }
}
