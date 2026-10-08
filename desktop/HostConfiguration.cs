using System.IO;
using System.Text.Json;

namespace Gaode.Station01.Desktop;

public sealed record HostConfiguration(string ApiBaseUrl, string SignalRUrl, string Mode, string ResourceVersion, string PrototypeSha256)
{
    public DesktopCommissioningProfile? CommissioningProfile { get; init; }
    public static HostConfiguration FromEnvironment()
    {
        var configuration = new HostConfiguration(
        Environment.GetEnvironmentVariable("GAODE_API_BASE_URL") ?? "https://localhost:5001",
        Environment.GetEnvironmentVariable("GAODE_SIGNALR_URL") ?? "https://localhost:5001/hubs/station01",
        Environment.GetEnvironmentVariable("GAODE_MODE") ?? "Test",
        Environment.GetEnvironmentVariable("GAODE_RESOURCE_VERSION") ?? "dev",
        "3DC791C1F8AB5EEDFA037F5DBAE450B2D20522FED654F86EA700C0284945E1E0");
        if (configuration.Mode == "RealDeviceCommissioning")
        {
            var path = Environment.GetEnvironmentVariable("GAODE_COMMISSIONING_PROFILE_PATH");
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) throw new InvalidOperationException("CommissioningProfileAbsolutePathRequired");
            var profile = JsonSerializer.Deserialize<DesktopCommissioningProfile>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))
                ?? throw new InvalidOperationException("CommissioningProfileRequired");
            profile.Validate(); configuration = configuration with { CommissioningProfile = profile };
        }
        return configuration;
    }

    public void Validate()
    {
        if (Mode == "RealDeviceCommissioning") (CommissioningProfile ?? throw new InvalidOperationException("CommissioningProfileRequired")).Validate();
        if (!Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out var api) || api.Scheme is not ("http" or "https")) throw new InvalidOperationException("API base URL must be HTTP(S).");
        if (!Uri.TryCreate(SignalRUrl, UriKind.Absolute, out var hub) || hub.Scheme is not ("http" or "https")) throw new InvalidOperationException("SignalR URL must be HTTP(S).");
        if (Mode is not ("Test" or "Simulation" or "Production" or "RealDeviceCommissioning")) throw new InvalidOperationException("Unsupported runtime mode.");
        if (PrototypeSha256 != "3DC791C1F8AB5EEDFA037F5DBAE450B2D20522FED654F86EA700C0284945E1E0") throw new InvalidOperationException("Prototype hash does not match approved baseline.");
        if (ApiBaseUrl.Contains("PLC", StringComparison.OrdinalIgnoreCase) || ApiBaseUrl.Contains("database", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Device and database endpoints are not valid frontend configuration.");
        if (Mode == "Production" && (api.Host is "localhost" or "127.0.0.1" || hub.Host is "localhost" or "127.0.0.1")) throw new InvalidOperationException("Production configuration cannot target a local host.");
    }
}

public sealed record DesktopCommissioningProfile(string SchemaVersion, string ProfileId, string ExpectedSubjectId,
    string ExpectedRole, string Mode, string CredentialEnvironmentVariable, string PreparedTemplatePath, string LogRoot)
{
    public void Validate()
    {
        if (SchemaVersion != "commissioning-desktop-profile/1" || Mode != "RealDeviceCommissioning" ||
            string.IsNullOrWhiteSpace(ProfileId) || string.IsNullOrWhiteSpace(ExpectedSubjectId) ||
            ExpectedSubjectId.StartsWith("test:", StringComparison.OrdinalIgnoreCase) || ExpectedRole is not ("Operator" or "ProcessEngineer") ||
            string.IsNullOrWhiteSpace(CredentialEnvironmentVariable) || CredentialEnvironmentVariable.StartsWith("GAODE_TEST_", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(CredentialEnvironmentVariable)) ||
            !Path.IsPathFullyQualified(PreparedTemplatePath ?? "") || !Path.IsPathFullyQualified(LogRoot ?? ""))
            throw new InvalidOperationException("CommissioningProfileIncompleteOrMismatched");
    }
}
