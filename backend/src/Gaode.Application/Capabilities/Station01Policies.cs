namespace Gaode.Application.Capabilities;

public static class Station01Policies
{
    public static CapabilityRegistry Create()
    {
        var registry = new CapabilityRegistry();
        var purposes = new HashSet<string>(StringComparer.Ordinal) { "Test", "Production", Gaode.Domain.Configuration.RuntimePurposes.RealDeviceCommissioning };
        registry.Register(new FixedCapabilityPolicy("xyz.fixed", "1.0", "Motion", purposes));
        registry.Register(new FixedCapabilityPolicy("xy.fixed", "1.0", "Motion", purposes));
        registry.Register(new FixedCapabilityPolicy("capture.whole-tray", "1.0", "Capture3D", purposes));
        registry.Register(new FixedCapabilityPolicy("capture.single-frame", "1.0", "CaptureF", purposes));
        registry.Register(new FixedCapabilityPolicy("height.values", "1.0", "Height", purposes));
        registry.Register(new FixedCapabilityPolicy("tray.observation", "1.0", "TrayPose", purposes));
        registry.Register(new FixedCapabilityPolicy("code.raw-candidates", "1.0", "FDecode", purposes));
        return registry;
    }
}
