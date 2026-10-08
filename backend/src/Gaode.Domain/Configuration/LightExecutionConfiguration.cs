namespace Gaode.Domain.Configuration;

// Absence denotes the original version's semantics; an explicit mode is versioned.
public sealed record LightExecutionConfiguration(string SchemaVersion, string Mode)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsValid => SchemaVersion == "light-execution/1" && Mode is "Simulated" or "Real";
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsSimulated => IsValid && Mode == "Simulated";
}
