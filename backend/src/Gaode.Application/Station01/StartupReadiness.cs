using Gaode.Domain.Station01;
using Gaode.Application.Motion;
using Gaode.Application.Ports;
using Gaode.Domain.Configuration;

namespace Gaode.Application.Station01;

public sealed record ReadinessResult(bool Ready, IReadOnlyList<string> Reasons,
    DeviceObservation Observation, string SafetyAssessment);

public sealed class StartupReadiness(MotionCoordinator motion)
{
    public DeviceObservation Observe() => motion.Observe();

    public ReadinessResult Check(PublicConfiguration config, ControlLatch control)
    {
        var errors = new List<string>();
        var state = motion.Observe();
        if (control.AdmissionClosed) errors.Add("ControlAdmissionClosed");
        if (!state.HasReliableObservation) errors.Add(state.ReasonCodes.FirstOrDefault() ?? "DeviceObservationUnavailable");
        if (state.HasReliableObservation && state.OperatingMode != OperatingMode.Automatic) errors.Add("PlcNotAutomatic");
        if (state.HasReliableObservation && state.SafetyAssessment != SafetyAssessment.Clear) errors.Add("SafetyInterlockDenied");
        if (motion.Unknown) errors.Add("PreviousMotionUnknown");
        if (config.Purpose == "Test" && config.Bindings.Any(x => x.Provider == "Real"))
            errors.Add("TestConfigCannotDriveRealDevice");
        // 算法未就绪不参与本阶段启动门。
        return new(errors.Count == 0, errors, state,
            !state.HasReliableObservation ? "Unconfirmed" : state.SafetyAssessment != SafetyAssessment.Clear ? "ExplicitUnsafe" : "Other");
    }
}
