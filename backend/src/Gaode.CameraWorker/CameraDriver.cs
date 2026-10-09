using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Infrastructure.Devices.Cameras;

namespace Gaode.CameraWorker;

internal sealed record WorkerFrame(byte[] Bytes, string Format, string ContentType, CaptureFrameMetadata Metadata);

internal abstract class CameraDriver(CameraBinding binding, Guid session, string stateRoot)
{
    protected CameraBinding Binding { get; } = binding;
    protected Guid Session { get; } = session;
    protected string StateRoot { get; } = stateRoot;
    protected string CameraIp = "", HostIp = "", NicMac = "", PixelFormat = "";
    protected int Width, Height;
    protected long Sequence;
    public Dictionary<string, string> Parameters { get; } = [];
    public virtual long MaxBytes => CameraWorkerProtocol.MaxPayloadBytes;
    public bool DeviceOpenAttempted { get; protected set; }
    public abstract void Open();
    public abstract WorkerFrame Capture();
    public abstract ActualCameraSettings ApplySettings(CameraImagingSettings settings);
    public abstract void Close();
    protected void Save(string file, object value) => File.WriteAllText(Path.Combine(StateRoot, file), JsonSerializer.Serialize(value, CameraWorkerProtocol.Json));
    protected static string Mac(string value) => new(value.Where(Uri.IsHexDigit).Select(char.ToUpperInvariant).ToArray());
    protected void ValidateNic(string hostIp, string cameraIp, string? reportedMac = null)
    {
        if (!IPAddress.TryParse(hostIp, out var host) || host.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(host) || host.Equals(IPAddress.Any))
            throw new InvalidDataException("DiscoveredHostAddressInvalid: " + hostIp);
        if (!IPAddress.TryParse(cameraIp, out var camera) || camera.AddressFamily != AddressFamily.InterNetwork || camera.Equals(host))
            throw new InvalidDataException("DiscoveredCameraAddressInvalid");
        var matches = NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses.Where(a => a.Address.Equals(host)).Select(a => (Nic: n, Address: a))).ToArray();
        if (matches.Length != 1 || Mac(matches[0].Nic.GetPhysicalAddress().ToString()) != Mac(Binding.ExpectedNicMac)
            || (reportedMac is not null && Mac(reportedMac) != Mac(Binding.ExpectedNicMac)))
            throw new InvalidDataException("PhysicalNicBindingMismatch");
        var mask = matches[0].Address.IPv4Mask.GetAddressBytes();
        var h = host.GetAddressBytes(); var c = camera.GetAddressBytes();
        if (!h.Zip(c, (left, right) => (left, right)).Select((pair, i) => (pair.left & mask[i]) == (pair.right & mask[i])).All(x => x))
            throw new InvalidDataException("CameraHostSubnetMismatch");
        HostIp = hostIp; CameraIp = cameraIp; NicMac = matches[0].Nic.GetPhysicalAddress().ToString();
        Save("binding.json", new { Binding, CameraIp, HostIp, NicMac, session = Session });
    }
    public CaptureFrameMetadata Metadata(ulong frameId, double timestamp, long sequence, DateTimeOffset triggered, long bytes, IReadOnlyList<FramePayload> payloads) =>
        new("gaode.capture.frame.v1", Binding.Role, Binding.Serial, NicMac, CameraIp, HostIp, Session, frameId, timestamp,
            sequence, triggered, DateTimeOffset.UtcNow, Width, Height, PixelFormat, bytes, new Dictionary<string, string>(Parameters), payloads);
    protected static FramePayload Payload(string name, byte[] bytes, long elements, int elementBytes) =>
        new(name, elements, elementBytes, bytes.LongLength, Convert.ToHexString(SHA256.HashData(bytes)));
}
