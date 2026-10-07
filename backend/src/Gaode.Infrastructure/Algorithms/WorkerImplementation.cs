using System.Security.Cryptography;
using System.Text.Json;

namespace Gaode.Infrastructure.Algorithms;

// Immutable launch binding. Readiness independently checks the running process's reported identity.
public sealed record WorkerImplementation(string Name, string ScriptPath, string ConfigPath,
    string ScriptSha256, string ConfigSha256)
{
    public string Reference => $"{Name}|script:{ScriptSha256}|config:{ConfigSha256}";
    public static WorkerImplementation Read(string script, string config)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(config));
        var name = document.RootElement.TryGetProperty("implementation", out var value)
            ? value.GetString()! : "PythonWorkerAdapter/1";
        if (name is not ("PythonWorkerAdapter/1" or "ContentSampleWorker/1"))
            throw new InvalidDataException("WorkerImplementationNotApproved");
        return new(name, Path.GetFullPath(script), Path.GetFullPath(config), Digest(script), Digest(config));
    }
    public void VerifyFiles()
    {
        if (Digest(ScriptPath) != ScriptSha256 || Digest(ConfigPath) != ConfigSha256)
            throw new InvalidDataException("WorkerLaunchBindingChanged");
    }
    public void VerifyReady(string? reason, int processId)
    {
        using var document = JsonDocument.Parse(reason ?? throw new InvalidDataException("WorkerImplementationIdentityMissing"));
        var r = document.RootElement;
        if (r.GetProperty("implementation").GetString() != Name || r.GetProperty("pid").GetInt32() != processId ||
            r.GetProperty("scriptSha256").GetString() != ScriptSha256 || r.GetProperty("configSha256").GetString() != ConfigSha256)
            throw new InvalidDataException("WorkerImplementationIdentityMismatch");
    }
    private static string Digest(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
