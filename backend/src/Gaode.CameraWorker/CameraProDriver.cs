using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Infrastructure.Devices.Cameras;

namespace Gaode.CameraWorker;

internal sealed class CameraProDriver(CameraBinding binding, Guid session, string stateRoot) : CameraDriver(binding, session, stateRoot)
{
    private Camera? camera;
    private CameraInfoVector? devices;
    private CameraInfo? info;
    private bool opened, changed;
    private int oldMode;
    private ulong? previousIndex;
    private long packetLimit;
    private Dictionary<string, string>? imagingBefore;
    private readonly Dictionary<ParamType, int> changedImaging = [];
    public override long MaxBytes => packetLimit;
    private static void Check(int code, string operation)
    { if (code != CameraPro.AC_OK) throw new IOException($"CameraPro {operation}: {code}"); }

    public override void Open()
    {
        camera = CameraPro.CreateCamera(CamType.CamPro) ?? throw new IOException("CameraProCreateFailed");
        devices = new CameraInfoVector();
        var discoveryCode = camera.DiscoverCameras(devices, 6000);
        Save("discovery.json", new { utc = DateTimeOffset.UtcNow, Binding.Serial, Binding.ExpectedNicMac, timeoutMs = 6000, code = discoveryCode,
            devices = devices.Cast<CameraInfo>().Select(x => new { serial = x.serialNum, ip = x.cameraIP,
                nicIp = x.userIP, nicMac = (string?)null, access = "SDK does not expose discovery access status", mac = x.macAddr, x.model }).ToArray() });
        Check(discoveryCode, "DiscoverCameras");
        var matches = devices.Cast<CameraInfo>().Where(x => x.serialNum == Binding.Serial).ToArray();
        if (matches.Length != 1) throw new InvalidDataException($"SerialDiscoveryNotUnique: serial={Binding.Serial}, matches={matches.Length}, discovered={devices.Count}; see discovery.json");
        info = matches[0]; ValidateNic(info.userIP, info.cameraIP);
        Check(camera.Open(info, 15000), "Open"); opened = true;
        foreach (var param in new[] { ParamType.Capture_WorkMode, ParamType.Capture_Delay, ParamType.IR_Exposure,
            ParamType.IR_Gain, ParamType.IR_HDRCnt, ParamType.IR_PixelType, ParamType.Algo_DepthMapType })
        {
            var value = 0; Check(camera.GetValue(info, param, ref value), "Get " + param);
            Parameters[param.ToString()] = value.ToString();
        }
        var p = info.camParam;
        var pixels = checked((long)p.irWidth * p.irHeight);
        var depthPixels = p.depthType switch { 1 => checked((long)p.textureWidth * p.textureHeight), 2 => pixels, _ => throw new InvalidDataException("DepthTypeUnsupported") };
        var groups = p.reconstructionType switch { 0 => 2, 2 => 1, _ => throw new InvalidDataException("ReconstructionTypeUnsupported") };
        if (pixels <= 0 || depthPixels <= 0 || p.pixelBytes is not (1 or 2)) throw new InvalidDataException("CameraProDimensionsInvalid");
        packetLimit = checked(pixels * 12 + depthPixels * 4 + pixels * p.pixelBytes * groups * 2 + 1024 * 1024);
        if (packetLimit > CameraWorkerProtocol.MaxPayloadBytes) throw new InvalidDataException("CameraProCapacityExceeded");
        Width = p.irWidth; Height = p.irHeight; PixelFormat = "CameraPro XYZ-f32 Depth-f32 IR-u" + p.pixelBytes * 8;
        Parameters["irWidth"] = p.irWidth.ToString(); Parameters["irHeight"] = p.irHeight.ToString();
        Parameters["textureWidth"] = p.textureWidth.ToString(); Parameters["textureHeight"] = p.textureHeight.ToString();
        Parameters["pixelBytes"] = p.pixelBytes.ToString(); Parameters["depthType"] = p.depthType.ToString();
        Parameters["reconstructionType"] = p.reconstructionType.ToString(); Parameters["sdkVersion"] = info.sdkVersion;
        Parameters["cameraSystemVersion"] = info.cameraSystemVersion;
        imagingBefore = ReadImagingParameters();
        Save("imaging-before.json", imagingBefore);
        Save("parameters-before.json", Parameters);
        Check(camera.GetValue(info, ParamType.Capture_WorkMode, ref oldMode), "ReadWorkMode");
        Save("settings-to-restore.json", new { Capture_WorkMode = oldMode });
        if (oldMode != (int)CameraWorkMode.Camera_SoftTrigger)
        {
            changed = true; Check(camera.SetValue(info, ParamType.Capture_WorkMode, (int)CameraWorkMode.Camera_SoftTrigger), "SetWorkMode");
            var readback = -1; Check(camera.GetValue(info, ParamType.Capture_WorkMode, ref readback), "ReadBackWorkMode");
            if (readback != (int)CameraWorkMode.Camera_SoftTrigger) throw new InvalidDataException("WorkModeReadbackMismatch");
        }
        Parameters["Capture_WorkMode"] = ((int)CameraWorkMode.Camera_SoftTrigger).ToString();
        info.outputSettings.sendPoint3D = true; info.outputSettings.sendDepthmap = true; info.outputSettings.sendRemapTexture = true;
        info.outputSettings.sendTexture = false; info.outputSettings.sendPointUV = false; info.outputSettings.sendNormals = false;
        info.outputSettings.sendTriangleIndices = false; info.outputSettings.sendPointColor = false;
    }
    public override ActualCameraSettings ApplySettings(CameraImagingSettings settings)
    {
        if (!opened || camera is null || info is null) throw new InvalidOperationException("CameraNotReady");
        // Vendor Camera.cs specifies IR_Exposure in integer ms, range [1,100].
        if (settings.ExposureUs < 1000 || settings.ExposureUs > 100000 || settings.ExposureUs % 1000 != 0)
            throw new NotSupportedException("CameraProExposureRequiresIntegerMilliseconds1To100");
        if (settings.Gain is { } gain && (!double.IsFinite(gain) || gain < 0 || gain > 15 || gain != Math.Truncate(gain)))
            throw new NotSupportedException("CameraProGainRequiresInteger0To15");
        if (settings.RoiPixels is { } roi && !roi.SequenceEqual(new[] { 0, 0, Width, Height }))
            throw new NotSupportedException("CameraProNonFullFrameRoiUnsupported");
        void Apply(ParamType param, int value)
        {
            var original = 0; Check(camera.GetValue(info, param, ref original), "ReadBeforeSet " + param);
            changedImaging.TryAdd(param, original);
            Check(camera.SetValue(info, param, value), "Set " + param);
            var actual = -1; Check(camera.GetValue(info, param, ref actual), "ReadBack " + param);
            if (actual != value) throw new InvalidDataException("ParameterReadbackMismatch:" + param);
            Parameters[param.ToString()] = actual.ToString();
        }
        Apply(ParamType.IR_Exposure, settings.ExposureUs / 1000);
        if (settings.Gain is { } requestedGain) Apply(ParamType.IR_Gain, checked((int)requestedGain));
        var actualGain = -1; Check(camera.GetValue(info, ParamType.IR_Gain, ref actualGain), "ReadActualGain");
        Save("imaging-settings-to-restore.json", changedImaging);
        return new(settings.ExposureUs, actualGain, Width, Height, 0, 0);
    }
    public override unsafe WorkerFrame Capture()
    {
        if (!opened || camera is null || info is null) throw new InvalidOperationException("CameraNotReady");
        foreach (var param in new[] { ParamType.Capture_WorkMode, ParamType.IR_Exposure, ParamType.IR_Gain, ParamType.IR_HDRCnt })
        {
            var value = -1; Check(camera.GetValue(info, param, ref value), "ReadCurrent " + param);
            if (param == ParamType.Capture_WorkMode && value != (int)CameraWorkMode.Camera_SoftTrigger)
                throw new InvalidOperationException("SoftwareTriggerModeChanged");
            Parameters[param.ToString()] = value.ToString();
        }
        var triggered = DateTimeOffset.UtcNow; var sequence = ++Sequence;
        using var frame = new FrameData(); Check(camera.Capture(info, frame), "Capture");
        var fi = frame.frameInfo;
        if (previousIndex.HasValue && fi.frameIndex <= previousIndex.Value) throw new InvalidDataException("CameraProFrameNotNew");
        if (!double.IsFinite(fi.frameTimestamp) || fi.frameTimestamp <= 0) throw new InvalidDataException("CameraProTimestampInvalid");
        var p = info.camParam;
        var pixels = checked((long)p.irWidth * p.irHeight);
        var depthPixels = p.depthType switch { 1 => checked((long)p.textureWidth * p.textureHeight), 2 => pixels, _ => throw new InvalidDataException("DepthTypeUnsupported") };
        var groups = p.reconstructionType switch { 0 => 2, 2 => 1, _ => throw new InvalidDataException("ReconstructionTypeUnsupported") };
        if (pixels <= 0 || depthPixels <= 0 || p.pixelBytes is not (1 or 2) || frame.point3DSize != pixels * 3
            || frame.pointCount != pixels || frame.depthmapSize != depthPixels)
            throw new InvalidDataException("CameraProPayloadShapeMismatch");
        var irImageBytes = checked(pixels * p.pixelBytes);
        if (frame.remapTextureSize == 0 || frame.remapTextureSize % checked(irImageBytes * groups) != 0)
            throw new InvalidDataException("CameraProIrShapeMismatch");
        var irImagesPerCamera = frame.remapTextureSize / (irImageBytes * groups);
        // SDK documents one bright and one normal image for each IR camera.
        if (irImagesPerCamera != 2) throw new InvalidDataException("CameraProIrPlaneCountMismatch");
        var payloads = new List<FramePayload>();
        using var memory = new MemoryStream();
        long total = 0;
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
        {
            void Write(string name, IntPtr pointer, long elements, int elementBytes)
            {
                var size = checked(elements * elementBytes); total = checked(total + size);
                if (pointer == IntPtr.Zero || size <= 0 || total > MaxBytes - 1024 * 1024) throw new InvalidDataException("CameraProPayloadInvalid");
                var bytes = new byte[checked((int)size)]; Marshal.Copy(pointer, bytes, 0, bytes.Length);
                using (var output = zip.CreateEntry(name, CompressionLevel.Fastest).Open()) output.Write(bytes);
                payloads.Add(Payload(name, bytes, elements, elementBytes));
            }
            Write("points.xyz.f32", (IntPtr)frame.point3D, frame.point3DSize, 4);
            Write("depth.f32", (IntPtr)frame.depthmap, frame.depthmapSize, 4);
            Write("ir.bytes", (IntPtr)frame.remapTexture, frame.remapTextureSize / p.pixelBytes, p.pixelBytes);
            Parameters["irImageBytes"] = irImageBytes.ToString(); Parameters["irImagesPerCamera"] = irImagesPerCamera.ToString();
            Parameters["irCameraGroups"] = groups.ToString(); Parameters["irPlaneLayout"] = "left ordinal planes, then right ordinal planes; bright/normal order retained as SDK ordinals";
            Parameters["byteOrder"] = "little-endian";
            using var writer = new StreamWriter(zip.CreateEntry("metadata.json").Open(), new UTF8Encoding(false));
            // The bundle manifest describes its uncompressed entries. The wire metadata separately
            // records the final ZIP byte length, which cannot be self-referentially stored in a ZIP entry.
            writer.Write(JsonSerializer.Serialize(new
            {
                schema = "gaode.camera-pro.bundle.v1", Binding.Role, Binding.Serial,
                workerSessionId = Session, fi.frameIndex, fi.frameTimestamp, triggerSequence = sequence,
                triggeredUtc = triggered, receivedUtc = DateTimeOffset.UtcNow,
                rawPayloadBytes = total, actualParameters = Parameters, payloads
            }, CameraWorkerProtocol.Json));
        }
        previousIndex = fi.frameIndex;
        var packet = memory.ToArray();
        if (packet.LongLength > MaxBytes) throw new InvalidDataException("CameraProPacketTooLarge");
        return new(packet, "CameraProFrameZipV1", "application/zip", Metadata(fi.frameIndex, fi.frameTimestamp, sequence, triggered, packet.LongLength, payloads));
    }
    private Dictionary<string, string> ReadImagingParameters()
    {
        var result = new Dictionary<string, string>();
        foreach (var param in new[] { ParamType.IR_Exposure, ParamType.IR_Gain, ParamType.IR_HDRCnt, ParamType.IR_PixelType, ParamType.Algo_DepthMapType })
        {
            var value = 0; Check(camera!.GetValue(info!, param, ref value), "ReadImaging " + param);
            result[param.ToString()] = value.ToString();
        }
        var p = info!.camParam;
        result["irWidth"] = p.irWidth.ToString(); result["irHeight"] = p.irHeight.ToString();
        result["textureWidth"] = p.textureWidth.ToString(); result["textureHeight"] = p.textureHeight.ToString();
        result["pixelBytes"] = p.pixelBytes.ToString(); result["depthType"] = p.depthType.ToString();
        result["dimensionsSource"] = "CameraInfo.camParam SDK cached values; not an independent device dimension readback";
        return result;
    }
    public override void Close()
    {
        var errors = new List<string>();
        if (opened && info is not null && camera is not null)
        {
            foreach (var pair in changedImaging)
            {
                try
                {
                    Check(camera.SetValue(info, pair.Key, pair.Value), "Restore " + pair.Key);
                    var actual = -1; Check(camera.GetValue(info, pair.Key, ref actual), "ReadRestored " + pair.Key);
                    if (actual != pair.Value) throw new InvalidDataException("RestoredParameterMismatch:" + pair.Key);
                }
                catch (Exception error) { errors.Add(error.Message); }
            }
            if (changed)
            {
                try
                {
                    Check(camera.SetValue(info, ParamType.Capture_WorkMode, oldMode), "RestoreWorkMode");
                    var readback = -1; Check(camera.GetValue(info, ParamType.Capture_WorkMode, ref readback), "ReadRestoredWorkMode");
                    if (readback != oldMode) throw new InvalidDataException("RestoredWorkModeMismatch");
                }
                catch (Exception error) { errors.Add(error.Message); }
            }
            Dictionary<string, string>? imagingAfter = null;
            var restoredMode = -1;
            try
            {
                Check(camera.GetValue(info, ParamType.Capture_WorkMode, ref restoredMode), "FinalReadWorkMode");
                if (restoredMode != oldMode) throw new InvalidDataException("FinalWorkModeDiffersFromOriginal");
                Check(camera.CameraGetStatus(info), "StatusBeforeClose");
                imagingAfter = ReadImagingParameters();
            }
            catch (Exception error) { errors.Add(error.Message); }
            var comparisons = imagingBefore?.Select(x => new { parameter = x.Key, before = x.Value,
                after = imagingAfter?.GetValueOrDefault(x.Key), equal = imagingAfter?.GetValueOrDefault(x.Key) == x.Value }).ToArray();
            Save("restoration.json", new { restored = errors.Count == 0, errors, originalMode = oldMode, restoredMode,
                imagingBefore, imagingAfter, comparisons, imagingUnchanged = comparisons is not null && comparisons.All(x => x.equal),
                changedImaging,
                imagingComparisonNote = "Only requested IR imaging parameters are restored. CameraInfo dimensions remain SDK cached values." });
            try { camera.Close(info); } catch (Exception error) { errors.Add(error.Message); }
            opened = false;
        }
        devices?.Dispose(); devices = null;
        if (camera is not null) { CameraPro.DestoryCamera(camera); camera = null; }
        if (errors.Count != 0) throw new IOException("CameraProCleanupFailed: " + string.Join(";", errors));
    }
}
