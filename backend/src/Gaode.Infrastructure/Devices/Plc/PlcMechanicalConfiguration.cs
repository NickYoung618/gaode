using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gaode.Infrastructure.Devices.Plc;

// Communication configuration, frozen at device construction; never a recipe/body DTO.
public sealed record PlcMechanicalConfiguration
{
    public required string SchemaVersion { get; init; }
    public required string Purpose { get; init; }
    public required string SourceReference { get; init; }
    public required PlcPoseProgram[] PosePrograms { get; init; }
    public required PlcGrabSafetyPosition? SortingSafePosition { get; init; }

    public Gaode.Application.Ports.RotationExecutionBasis? RotationBasis { get; init; }
    public PlcPositionBasis? PositionBasis { get; init; }
    public PlcSiteOperations? SiteOperations { get; init; }

    public static void Apply(PlcRuntimeOptions options, string path)
    {
        var configuration = JsonSerializer.Deserialize<PlcMechanicalConfiguration>(File.ReadAllText(path),
            new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
            }) ?? throw new InvalidDataException("PlcMechanicsConfigurationEmpty");
        var purpose = options.ConfigurationPurpose;
        if (configuration.SchemaVersion != "plc-mechanics/1" || configuration.Purpose != purpose ||
            string.IsNullOrWhiteSpace(configuration.SourceReference) || configuration.PosePrograms is null ||
            configuration.PosePrograms.Any(p => p.Purpose != purpose || string.IsNullOrWhiteSpace(p.SourceReference)) ||
            configuration.SortingSafePosition is { } safe &&
                (safe.Purpose != purpose || string.IsNullOrWhiteSpace(safe.SourceReference)))
            throw new InvalidDataException("PlcMechanicsConfigurationSourceMismatch");
        // Existing movement admission validates the exact model/pose and required position.
        // An empty set or null safety position does not fabricate configuration or grant motion.
        if (configuration.RotationBasis is { } rotation &&
            (rotation.Purpose != purpose || string.IsNullOrWhiteSpace(rotation.SourceReference) ||
             !double.IsFinite(rotation.AngleToleranceDeg) || rotation.AngleToleranceDeg < 0))
            throw new InvalidDataException("RotationMechanicalBasisInvalid");
        options.RotationBasis = configuration.RotationBasis;
        if (configuration.PositionBasis is { } basis && (!basis.IsValid || basis.Purpose != purpose))
            throw new InvalidDataException("PlcPositionBasisSourceMismatch");
        options.PositionBasis = configuration.PositionBasis;
        if (configuration.SiteOperations is { IsValid: false })
            throw new InvalidDataException("SiteOperationsConfirmationInvalid");
        options.SiteOperations = configuration.SiteOperations;
        options.PosePrograms = configuration.PosePrograms;
        options.SortingSafePosition = configuration.SortingSafePosition;
    }
}
