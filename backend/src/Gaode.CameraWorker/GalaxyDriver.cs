using System.Runtime.InteropServices;
using GxIAPINET;
using Gaode.Infrastructure.Devices.Cameras;

namespace Gaode.CameraWorker;

internal sealed class GalaxyDriver(CameraBinding binding, Guid session, string stateRoot) : CameraDriver(binding, session, stateRoot)
{
    private IGXFactory? factory;
    private IGXDevice? device;
    private IGXFeatureControl? features;
    private IGXStream? stream;
    private bool initialized, grabbing, acquisitionStarted, triggerSnapshot;
    private string? acquisitionMode, selector, triggerMode, triggerSource;
    private ulong? previousFrame;
    private long payloadLimit;
    private Dictionary<string, string>? imagingBefore;
    public override long MaxBytes => payloadLimit;
    private string Enum(string name) => features!.GetEnumFeature(name).GetValue();
    private void Set(string name, string value)
    {
        features!.GetEnumFeature(name).SetValue(value);
        if (Enum(name) != value) throw new InvalidDataException("ParameterReadbackMismatch: " + name);
    }

    public override void Open()
    {
        factory = IGXFactory.GetInstance(); factory.Init(); initialized = true;
        var list = new List<IGXDeviceInfo>(); factory.UpdateAllDeviceList(1500, list);
        Save("discovery.json", new
        {
            utc = DateTimeOffset.UtcNow, Binding.Serial, Binding.ExpectedNicMac, timeoutMs = 1500,
            devices = list.Select(x => new { serial = x.GetSN(), ip = x.GetIP(), nicMac = x.GetNICMAC(), nicIp = x.GetNICIP(),
                access = x.GetAccessStatus().ToString(), model = x.GetModelName(), mac = x.GetMAC() }).ToArray()
        });
        var matches = list.Where(x => x.GetSN() == Binding.Serial).ToArray();
        if (matches.Length != 1) throw new InvalidDataException($"SerialDiscoveryNotUnique: serial={Binding.Serial}, matches={matches.Length}, discovered={list.Count}; see discovery.json");
        var info = matches[0]; ValidateNic(info.GetNICIP(), info.GetIP(), info.GetNICMAC());
        device = factory.OpenDeviceBySN(Binding.Serial, GX_ACCESS_MODE.GX_ACCESS_CONTROL);
        features = device.GetRemoteFeatureControl();
        foreach (var name in new[] { "AcquisitionMode", "TriggerSelector", "TriggerMode", "TriggerSource", "PixelFormat", "ExposureAuto", "GainAuto" })
            Read(name, () => Enum(name));
        foreach (var name in new[] { "ExposureTime", "Gain", "AcquisitionFrameRate" })
            Read(name, () => features.GetFloatFeature(name).GetValue().ToString(System.Globalization.CultureInfo.InvariantCulture));
        foreach (var name in new[] { "Width", "Height", "OffsetX", "OffsetY", "PayloadSize" })
            Read(name, () => features.GetIntFeature(name).GetValue().ToString());
        Width = checked((int)features.GetIntFeature("Width").GetValue());
        Height = checked((int)features.GetIntFeature("Height").GetValue());
        payloadLimit = features.GetIntFeature("PayloadSize").GetValue();
        if (Width <= 0 || Height <= 0 || payloadLimit <= 0 || payloadLimit > CameraWorkerProtocol.MaxPayloadBytes)
            throw new InvalidDataException("GalaxyPayloadCapacityInvalid");
        PixelFormat = Enum("PixelFormat");
        imagingBefore = ReadImagingParameters();
        Save("imaging-before.json", imagingBefore);
        Save("parameters-before.json", Parameters);
        // Export is diagnostic backup only. Restoration below changes only the trigger fields.
        device.ExportConfigFileW(Path.Combine(StateRoot, "galaxy-before.txt"));
        acquisitionMode = Enum("AcquisitionMode");
        if (features.IsImplemented("TriggerSelector"))
        {
            selector = Enum("TriggerSelector");
            Save("settings-to-restore.json", new { acquisitionMode, selector });
            Set("TriggerSelector", "FrameStart");
        }
        triggerMode = Enum("TriggerMode"); triggerSource = Enum("TriggerSource"); triggerSnapshot = true;
        Save("settings-to-restore.json", new { acquisitionMode, selector, triggerMode, triggerSource, selectedContext = "FrameStart" });
        Set("AcquisitionMode", "Continuous");
        Set("TriggerMode", "Off"); Set("TriggerSource", "Software"); Set("TriggerMode", "On");
        stream = device.OpenStream(0); stream.SetAcqusitionBufferNumber(3);
        stream.StartGrab(); grabbing = true; stream.FlushQueue();
        acquisitionStarted = true; features.GetCommandFeature("AcquisitionStart").Execute();
        Parameters["TriggerMode"] = "On"; Parameters["TriggerSource"] = "Software";
        Parameters["AcquisitionMode"] = "Continuous"; Parameters["TriggerSelector"] = "FrameStart";
    }
    private void Read(string name, Func<string> read)
    {
        if (features!.IsImplemented(name) && features.IsReadable(name)) Parameters[name] = read();
    }
    private Dictionary<string, string> ReadImagingParameters()
    {
        var result = new Dictionary<string, string>();
        foreach (var name in new[] { "PixelFormat", "ExposureAuto", "GainAuto" })
            if (features!.IsImplemented(name) && features.IsReadable(name)) result[name] = Enum(name);
        foreach (var name in new[] { "ExposureTime", "Gain", "AcquisitionFrameRate" })
            if (features!.IsImplemented(name) && features.IsReadable(name)) result[name] = features.GetFloatFeature(name).GetValue().ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        foreach (var name in new[] { "Width", "Height", "OffsetX", "OffsetY" })
            if (features!.IsImplemented(name) && features.IsReadable(name)) result[name] = features.GetIntFeature(name).GetValue().ToString();
        return result;
    }
    public override WorkerFrame Capture()
    {
        if (stream is null || features is null) throw new InvalidOperationException("CameraNotReady");
        if (Enum("TriggerMode") != "On" || Enum("TriggerSource") != "Software") throw new InvalidOperationException("SoftwareTriggerModeChanged");
        stream.FlushQueue();
        var triggered = DateTimeOffset.UtcNow; var sequence = ++Sequence;
        features.GetCommandFeature("TriggerSoftware").Execute();
        IImageData? image = null;
        try
        {
            image = stream.GetImage(10000);
            if (image.GetStatus() != GX_FRAME_STATUS_LIST.GX_FRAME_STATUS_SUCCESS) throw new InvalidDataException("IncompleteGalaxyFrame");
            var frameId = image.GetFrameID(); var size = image.GetPayloadSize();
            if (previousFrame.HasValue && frameId <= previousFrame.Value) throw new InvalidDataException("GalaxyFrameNotNew");
            if (size == 0 || size > (ulong)MaxBytes || image.GetBuffer() == IntPtr.Zero || image.GetWidth() == 0 || image.GetHeight() == 0)
                throw new InvalidDataException("GalaxyPayloadInvalid");
            Width = checked((int)image.GetWidth()); Height = checked((int)image.GetHeight()); PixelFormat = image.GetPixelFormat().ToString();
            var bytes = new byte[checked((int)size)]; Marshal.Copy(image.GetBuffer(), bytes, 0, bytes.Length);
            previousFrame = frameId;
            Read("ExposureTime", () => features.GetFloatFeature("ExposureTime").GetValue().ToString(System.Globalization.CultureInfo.InvariantCulture));
            Read("Gain", () => features.GetFloatFeature("Gain").GetValue().ToString(System.Globalization.CultureInfo.InvariantCulture));
            var metadata = Metadata(frameId, image.GetTimeStamp(), sequence, triggered, bytes.LongLength, [Payload("frame.raw", bytes, bytes.LongLength, 1)]);
            return new(bytes, "GalaxyRaw", "application/octet-stream", metadata);
        }
        finally { image?.Destroy(); }
    }
    public override void Close()
    {
        var errors = new List<string>();
        void Attempt(Action action) { try { action(); } catch (Exception error) { errors.Add(error.Message); } }
        if (acquisitionStarted) { acquisitionStarted = false; Attempt(() => features!.GetCommandFeature("AcquisitionStop").Execute()); }
        if (grabbing) { grabbing = false; Attempt(() => stream!.StopGrab()); }
        if (features is not null)
        {
            var triggerReadback = new Dictionary<string, string>();
            if (triggerSnapshot)
            {
                Attempt(() => Set("TriggerMode", "Off"));
                Attempt(() => Set("TriggerSource", triggerSource!));
                Attempt(() => Set("TriggerMode", triggerMode!));
                Attempt(() => triggerReadback["FrameStart.TriggerMode"] = Enum("TriggerMode"));
                Attempt(() => triggerReadback["FrameStart.TriggerSource"] = Enum("TriggerSource"));
            }
            if (selector is not null) Attempt(() => Set("TriggerSelector", selector));
            if (acquisitionMode is not null) Attempt(() => Set("AcquisitionMode", acquisitionMode));
            Attempt(() => triggerReadback["AcquisitionMode"] = Enum("AcquisitionMode"));
            if (selector is not null) Attempt(() => triggerReadback["TriggerSelector"] = Enum("TriggerSelector"));
            Dictionary<string, string>? imagingAfter = null;
            Attempt(() => imagingAfter = ReadImagingParameters());
            var comparisons = imagingBefore?.Select(x => new { parameter = x.Key, before = x.Value,
                after = imagingAfter?.GetValueOrDefault(x.Key), equal = imagingAfter?.GetValueOrDefault(x.Key) == x.Value }).ToArray();
            Save("restoration.json", new { restored = errors.Count == 0, errors,
                triggerReadback, imagingBefore, imagingAfter, comparisons,
                imagingUnchanged = comparisons is not null && comparisons.All(x => x.equal),
                imagingComparisonNote = "Read-only comparison; automatic exposure/gain may evolve while acquisition runs. No imaging parameter is restored or written." });
        }
        if (stream is not null) { Attempt(stream.Close); stream = null; }
        if (device is not null) { Attempt(device.Close); device = null; }
        if (initialized) { initialized = false; Attempt(() => factory!.Uninit()); }
        if (errors.Count != 0) throw new IOException("GalaxyCleanupFailed: " + string.Join(";", errors));
    }
}
