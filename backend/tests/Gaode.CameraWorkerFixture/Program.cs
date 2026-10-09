using System.IO.Compression;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Infrastructure.Devices.Cameras;

// Offline process fixture only: never loads a vendor SDK or starts a product HTTP Host.
var options = Enumerable.Range(0, args.Length / 2).ToDictionary(i => args[i * 2], i => args[i * 2 + 1]);
var session = Guid.Parse(options["--session"]);
var root = Path.GetFullPath(Path.Combine(options["--state-root"], "../../.."));
string Mode() => File.Exists(Path.Combine(root, "mode.txt")) ? File.ReadAllText(Path.Combine(root, "mode.txt")) : "normal";
using var pipe = new NamedPipeClientStream(".", options["--pipe"], PipeDirection.InOut, PipeOptions.Asynchronous);
await pipe.ConnectAsync(5000);
var init = (await CameraWorkerProtocol.ReadAsync(pipe, 0, default)).Header;
var binding = init.Binding!;
var previousSessionPath=Path.Combine(root,"previous-session.txt");
var previousSession=File.Exists(previousSessionPath)?Guid.Parse(File.ReadAllText(previousSessionPath)):Guid.NewGuid();
if (Mode() is "init-fail" or "init-before-open-fail")
{
    await CameraWorkerProtocol.WriteAsync(pipe, new("error", session, init.RequestId) { Error = "InjectedOpenFailure", DeviceOpenAttempted = Mode() == "init-before-open-fail" ? false : null }, ReadOnlyMemory<byte>.Empty, default);
    return;
}
var parameters = new Dictionary<string, string> { ["Width"] = "2", ["Height"] = "2", ["PayloadSize"] = "4", ["PixelFormat"] = "Mono8",
    ["irWidth"]="2", ["irHeight"]="2", ["textureWidth"]="2", ["textureHeight"]="2", ["pixelBytes"]="1", ["depthType"]="2",
    ["reconstructionType"]="2", ["irImagesPerCamera"]="2", ["irCameraGroups"]="1", ["irImageBytes"]="4", ["byteOrder"]="little-endian" };
CaptureFrameMetadata Metadata(long sequence, long length, IReadOnlyList<FramePayload> payloads) =>
    new("gaode.capture.frame.v1", binding.Role, binding.Serial, binding.ExpectedNicMac, "192.0.2.2", "192.0.2.1", session,
        (ulong)sequence, 123 + sequence, sequence, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 2, 2,
        binding.Role == "3D" ? "CameraPro XYZ-f32 Depth-f32 IR-u8" : "GX_PIXEL_FORMAT_MONO8", length, parameters, payloads);
await CameraWorkerProtocol.WriteAsync(pipe, new("ready", session, init.RequestId) { MaxBytes = 4096, Metadata = Metadata(0,0,[]) }, ReadOnlyMemory<byte>.Empty, default);
File.WriteAllText(previousSessionPath,session.ToString());
_ = Task.Run(async () => { while (true) { if (File.Exists(Path.Combine(root, "exit"))) Environment.Exit(17); await Task.Delay(10); } });
long sequence = 0;
ActualCameraSettings? actualSettings = null;
while (true)
{
    var request = (await CameraWorkerProtocol.ReadAsync(pipe, 0, default)).Header;
    if (request.Kind == "close")
    {
        File.WriteAllText(Path.Combine(root, "restoration.json"), JsonSerializer.Serialize(new { restored = true, modified = actualSettings is not null }));
        await CameraWorkerProtocol.WriteAsync(pipe, new("closed", session, request.RequestId), ReadOnlyMemory<byte>.Empty, default);
        return;
    }
    if (request.Kind == "capture-configured")
    {
        var settings = request.Settings ?? throw new InvalidDataException("FixtureSettingsRequired");
        if (Mode() is "settings-unsupported" or "settings-readback-fail" ||
            settings.RoiPixels is { } roi && !roi.SequenceEqual(new[] { 0, 0, 2, 2 }))
        {
            await CameraWorkerProtocol.WriteAsync(pipe, new("error", session, request.RequestId)
                { Error = Mode() == "settings-readback-fail" ? "ParameterReadbackMismatch:ExposureTime" : "CameraParameterUnsupported" },
                ReadOnlyMemory<byte>.Empty, default);
            continue;
        }
        actualSettings = new(settings.ExposureUs, settings.Gain ?? 1, 2, 2, 0, 0);
        parameters["ExposureTime"] = settings.ExposureUs.ToString();
        parameters["Gain"] = actualSettings.Gain.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
    else if (request.Kind != "capture") throw new InvalidDataException("FixtureCommandInvalid");
    File.AppendAllText(Path.Combine(root, "triggers.txt"), session + Environment.NewLine);
    sequence++;
    if (Mode() == "hold") await Task.Delay(1500);
    byte[] bytes = [1,2,3,4];
    var payloads = new List<FramePayload>();
    var format = binding.Role == "3D" ? "CameraProFrameZipV1" : "GalaxyRaw";
    FramePayload Payload(string name, byte[] data, int size) => new(name, data.Length / size, size, data.Length, Convert.ToHexString(SHA256.HashData(data)));
    if (binding.Role == "3D")
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
        {
            void Add(string name, byte[] data, int size)
            {
                using var stream = zip.CreateEntry(name).Open(); stream.Write(data); payloads.Add(Payload(name, data, size));
            }
            Add("points.xyz.f32", new byte[48], 4); Add("depth.f32", new byte[16], 4);
            if (Mode() != "missing-ir") Add("ir.bytes", new byte[8], 1);
            using var manifest = zip.CreateEntry("metadata.json").Open();
            JsonSerializer.Serialize(manifest, new { role=binding.Role, serial=binding.Serial, workerSessionId=session,
                frameIndex=(ulong)sequence, frameTimestamp=123+sequence, triggerSequence=sequence, rawPayloadBytes=payloads.Sum(x=>x.ByteLength),
                actualParameters=parameters, payloads }, CameraWorkerProtocol.Json);
        }
        bytes = memory.ToArray();
    }
    else payloads.Add(Payload("frame.raw", bytes, 1));
    var metadata = Metadata(sequence, bytes.Length, payloads);
    if (Mode() == "bad-role") metadata = metadata with { Role = "B" };
    if (Mode() == "bad-size") metadata = metadata with { Width = 3 };
    if (Mode() == "old-session") metadata = metadata with { WorkerSessionId = previousSession };
    await CameraWorkerProtocol.WriteAsync(pipe, new("frame", Mode()=="old-session"?previousSession:session, request.RequestId)
        { Metadata=metadata,Format=format, SettingsDigest = request.SettingsDigest,
            ActualSettings = request.Settings is null ? null : actualSettings }, bytes, default);
}
