namespace Gaode.Station01.Desktop;

public sealed record HostConfiguration(string ApiBaseUrl, string SignalRUrl, string Mode, string ResourceVersion, string PrototypeSha256)
{
    public static HostConfiguration FromEnvironment() => new(
        Environment.GetEnvironmentVariable("GAODE_API_BASE_URL") ?? "https://localhost:5001",
        Environment.GetEnvironmentVariable("GAODE_SIGNALR_URL") ?? "https://localhost:5001/hubs/station01",
        Environment.GetEnvironmentVariable("GAODE_MODE") ?? "Test",
        Environment.GetEnvironmentVariable("GAODE_RESOURCE_VERSION") ?? "dev",
        "3DC791C1F8AB5EEDFA037F5DBAE450B2D20522FED654F86EA700C0284945E1E0");

    public void Validate()
    {
        if (!Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out var api) || api.Scheme is not ("http" or "https")) throw new InvalidOperationException("API base URL must be HTTP(S).");
        if (!Uri.TryCreate(SignalRUrl, UriKind.Absolute, out var hub) || hub.Scheme is not ("http" or "https")) throw new InvalidOperationException("SignalR URL must be HTTP(S).");
        if (Mode is not ("Test" or "Simulation" or "Production")) throw new InvalidOperationException("Unsupported runtime mode.");
        if (PrototypeSha256 != "3DC791C1F8AB5EEDFA037F5DBAE450B2D20522FED654F86EA700C0284945E1E0") throw new InvalidOperationException("Prototype hash does not match approved baseline.");
        if (ApiBaseUrl.Contains("PLC", StringComparison.OrdinalIgnoreCase) || ApiBaseUrl.Contains("database", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Device and database endpoints are not valid frontend configuration.");
        if (Mode == "Production" && (api.Host is "localhost" or "127.0.0.1" || hub.Host is "localhost" or "127.0.0.1")) throw new InvalidOperationException("Production configuration cannot target a local host.");
    }
}
