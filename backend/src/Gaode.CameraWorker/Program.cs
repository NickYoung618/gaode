using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text.Json;
using Gaode.Infrastructure.Devices.Cameras;

namespace Gaode.CameraWorker;

internal static class Program
{
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetDllDirectory(string path);

    public static async Task<int> Main(string[] args)
    {
        var options = new Dictionary<string, string>();
        for (var i = 0; i < args.Length; i += 2)
        {
            if (i + 1 == args.Length) throw new ArgumentException("Worker argument missing value");
            options.Add(args[i], args[i + 1]);
        }
        var session = Guid.Parse(options["--session"]);
        var stateRoot = Path.Combine(Path.GetFullPath(options["--state-root"]), session.ToString("N"));
        Directory.CreateDirectory(stateRoot);
        void Log(string stage, object detail) => File.AppendAllText(Path.Combine(stateRoot, "diagnostics.jsonl"),
            JsonSerializer.Serialize(new { utc = DateTimeOffset.UtcNow, session, stage, detail }, CameraWorkerProtocol.Json) + Environment.NewLine);
        using var pipe = new NamedPipeClientStream(".", options["--pipe"], PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(15000);
        CameraDriver? driver = null;
        CameraWireMessage? request = null;
        try
        {
            request = (await CameraWorkerProtocol.ReadAsync(pipe, 0, CancellationToken.None)).Header;
            if (request.SessionId != session || request.Kind != "init" || request.Binding is null)
                throw new InvalidDataException("WorkerInitRequired");
            var binding = request.Binding;
            var native = Path.GetFullPath(options[binding.Kind == "2D" ? "--galaxy-sdk" : "--camerapro-sdk"]);
            if (!Directory.Exists(native) || !SetDllDirectory(native)) throw new IOException("SdkDirectoryInvalid: " + native);
            Environment.SetEnvironmentVariable("PATH", native + ";" + Environment.GetEnvironmentVariable("PATH"));
            Log("initialize", new { binding, native, process = Environment.ProcessId });
            driver = binding.Kind switch
            {
                "2D" => new GalaxyDriver(binding, session, stateRoot),
                "3D" => new CameraProDriver(binding, session, stateRoot),
                _ => throw new InvalidDataException("UnknownCameraKind")
            };
            driver.Open();
            var nativeFile = Path.Combine(native, binding.Kind == "2D" ? "GxIAPI.dll" : "CameraPro.dll");
            var version = System.Diagnostics.FileVersionInfo.GetVersionInfo(nativeFile);
            driver.Parameters["SdkNativeFileVersion"] = version.FileVersion ?? "NoFileVersionResource";
            driver.Parameters["SdkNativeSha256"] = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(nativeFile)));
            driver.Parameters["SdkNativeDirectory"] = native;
            Log("ready", driver.Parameters);
            await CameraWorkerProtocol.WriteAsync(pipe, new("ready", session, request.RequestId)
                { MaxBytes = driver.MaxBytes, Metadata = driver.Metadata(0, 0, 0, DateTimeOffset.UtcNow, 0, []) }, ReadOnlyMemory<byte>.Empty, CancellationToken.None);
            while (true)
            {
                request = (await CameraWorkerProtocol.ReadAsync(pipe, 0, CancellationToken.None)).Header;
                if (request.SessionId != session) throw new InvalidDataException("WorkerSessionMismatch");
                Log(request.Kind, new { request.RequestId });
                if (request.Kind == "close")
                {
                    driver.Close();
                    driver = null;
                    Log("closed", new { restored = true });
                    await CameraWorkerProtocol.WriteAsync(pipe, new("closed", session, request.RequestId), ReadOnlyMemory<byte>.Empty, CancellationToken.None);
                    return 0;
                }
                if (request.Kind != "capture") throw new InvalidDataException("WorkerCommandInvalid");
                var frame = driver.Capture();
                await CameraWorkerProtocol.WriteAsync(pipe, new("frame", session, request.RequestId)
                    { Metadata = frame.Metadata, Format = frame.Format, ContentType = frame.ContentType }, frame.Bytes, CancellationToken.None);
                Log("frame", new { request.RequestId, frame.Metadata.FrameId, frame.Metadata.PayloadBytes });
            }
        }
        catch (Exception error)
        {
            Log("error", new { error = error.ToString(), request = request?.RequestId });
            try { await CameraWorkerProtocol.WriteAsync(pipe, new("error", session, request?.RequestId ?? Guid.Empty) { Error = error.Message }, ReadOnlyMemory<byte>.Empty, CancellationToken.None); }
            catch (IOException) { }
            return 2;
        }
        finally
        {
            if (driver is not null)
            {
                try { driver.Close(); Log("cleanup", new { restored = true }); }
                catch (Exception error) { Log("restore-failed", error.ToString()); }
            }
        }
    }
}
