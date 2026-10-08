using Gaode.Plc.Protocol;
using System.Net;

namespace Gaode.Infrastructure.Devices.Plc;

public sealed class PlcRuntimeOptions
{
    public string Provider { get; set; } = "Virtual";
    public string? Purpose { get; set; }
    public string ConfigurationPurpose => Purpose ?? (Provider == "Real" ? "Production" : "Test");
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 1502;
    public byte UnitId { get; set; } = 1;
    public int IoTimeoutMs { get; set; } = 200;
    public int HeartbeatTimeoutMs { get; set; } = 3000;
    public string AddressConvention { get; set; } = "HexOneBased";
    public Float32ByteOrder Float32ByteOrder { get; set; } = Float32ByteOrder.Abcd;
    public bool ActionsEnabled { get; set; } = true;
    public PlcPoseProgram[] PosePrograms { get; set; } = [];
    public PlcGrabSafetyPosition? SortingSafePosition { get; set; }
    public Gaode.Application.Ports.RotationExecutionBasis? RotationBasis { get; set; }
    public PlcPositionBasis? PositionBasis { get; set; }
    public PlcSiteOperations? SiteOperations { get; set; }
    public ProtocolDefinition? Definition { get; set; }
    internal void LoadFieldProfile(string? path)
    {
        if (path is null) return;
        if (Provider != "Real") throw new InvalidOperationException("FieldProfileRequiresRealProvider");
        Definition = FieldAddressProfile.LoadDefinition(path);
        Float32ByteOrder = Definition.ByteOrder;
    }
    public void Validate()
    {
        if (SiteOperations is { } operations && (!operations.IsValid || Definition is not { IsSiteLayout: true }))
            throw new InvalidOperationException("SiteOperationsConfirmationInvalid");
        if (Provider == "Virtual" && ConfigurationPurpose != "Test" ||
            Provider == "Real" && ConfigurationPurpose is not ("Production" or Gaode.Domain.Configuration.RuntimePurposes.RealDeviceCommissioning))
            throw new InvalidOperationException("PlcProviderPurposeMismatch");
        if (PositionBasis is { } basis && (!basis.IsValid || basis.Purpose != ConfigurationPurpose) ||
            ConfigurationPurpose == Gaode.Domain.Configuration.RuntimePurposes.RealDeviceCommissioning && PositionBasis is null)
            throw new InvalidOperationException("PlcPositionBasisMissingOrMismatched");
        if (Provider == "Real" && (Definition is not { Purpose: "Production" } || string.IsNullOrWhiteSpace(Definition.SourceReference)))
            throw new InvalidOperationException("FormalProtocolDefinitionMissing");
        if (Provider is not ("Virtual" or "Real") || Port is < 1 or > 65535 ||
            UnitId == 0 || IoTimeoutMs is < 1 or > 1000 ||
            HeartbeatTimeoutMs < 500 || IoTimeoutMs >= HeartbeatTimeoutMs ||
            AddressConvention != "HexOneBased" || !Enum.IsDefined(Float32ByteOrder) ||
            !IPAddress.TryParse(Host, out var address)) throw new InvalidOperationException("InvalidPlcConfiguration");
        if (Provider == "Virtual" && !IPAddress.IsLoopback(address))
            throw new InvalidOperationException("VirtualEndpointMustBeLoopback");
    }
}

// Fixed, source-bound semantics confirmed by the PLC/user; absent means unconfirmed.
public sealed record PlcSiteOperations(string SchemaVersion, string SourceReference)
{
    public bool RestoresWorkpieceAndMechanisms { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsValid => SchemaVersion == "plc-site-operations/1" && !string.IsNullOrWhiteSpace(SourceReference);
}

public sealed record PlcGrabSafetyPosition(double GrabZ, string Unit, string Frame, string Purpose, string SourceReference);
public sealed record PlcPositionBasis(string Frame, string Unit, string Purpose, string SourceReference)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsValid => !string.IsNullOrWhiteSpace(Frame) && Unit == "mm" && !string.IsNullOrWhiteSpace(SourceReference);
}
