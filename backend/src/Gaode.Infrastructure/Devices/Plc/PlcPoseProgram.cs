namespace Gaode.Infrastructure.Devices.Plc;

// This configuration belongs to communication. Neither payload words nor wire
// addresses escape into recipe business values or public status DTOs.
public sealed record PlcPoseProgram(string Model, string ProfileId, string ProfileVersion,
    string PoseKey, ushort TargetFaceWord, ushort[] ModelWords,
    string SourceReference, string Purpose)
{
    public double? ModelNumber { get; init; }
}
