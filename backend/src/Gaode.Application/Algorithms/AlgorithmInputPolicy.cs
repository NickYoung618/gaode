using Gaode.Application.Configuration;
using Gaode.Application.Ports;

namespace Gaode.Application.Algorithms;

// Business checks declared/frozen capability and format. All encoding stays in IMediaStore.
internal static class AlgorithmInputPolicy
{
    public static AlgorithmModuleReference RequireModule(RealAlgorithmConfiguration configuration, AlgorithmRole role,
        int count, string parameters, string capability, string version)
    {
        var name = role switch { AlgorithmRole.TrayPose => "TrayPose",AlgorithmRole.FDecode => "FDecode",
            AlgorithmRole.EDecode => "EDecode",AlgorithmRole.Detection when count==2 => "DefectFusion",
            AlgorithmRole.Detection => "DefectSingle",_ => throw new InvalidOperationException("RealAlgorithmRoleUnsupported") };
        var module = configuration.Modules.SingleOrDefault(m => m.Module==name);
        if(module is null || module.InputCount!=count || module.ParametersVersion!=parameters || module.CapabilityId!=capability || module.CapabilityVersion!=version)
            throw new InvalidOperationException("RealAlgorithmFrozenCapabilityMismatch:"+name);
        if(role==AlgorithmRole.TrayPose)
        {
            if(configuration.Inputs.PlyEncoding!="binary_little_endian" || configuration.Inputs.PlyRgbRequired!=false ||
                configuration.Inputs.TrayPosePng.Required!=false || configuration.LayoutFile is null || configuration.CalibrationFile is null)
                throw new InvalidOperationException("RealTrayPoseInputOrCalibrationUnconfirmed");
        }
        else if(!configuration.Inputs.PngBitDepths.Contains(8) || !configuration.Inputs.PngColorTypes.Contains("Gray"))
            throw new InvalidOperationException("RealAlgorithmMono8InputNotDeclared");
        return module;
    }
}
