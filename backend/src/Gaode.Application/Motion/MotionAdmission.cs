using Gaode.Domain.Station01;
using Gaode.Application.Ports;
using Gaode.Domain.Configuration;

namespace Gaode.Application.Motion;

public static class MotionAdmission
{
    public static string? CheckStart(PublicConfiguration config, DeviceObservation observation,
        bool controlClosed, Guid committedIntentId)
    {
        if (controlClosed) return "ControlClosed";
        if (committedIntentId == Guid.Empty) return "IntentNotCommitted";
        if (config.Purpose == "Test" && config.Bindings.Any(b => b.Provider == "Real")) return "TestConfigRealActionDenied";
        if (!observation.HasReliableObservation || observation.OperatingMode != OperatingMode.Automatic || observation.SafetyAssessment != SafetyAssessment.Clear) return "PlcNotReady";
        return null;
    }

    public static string? CheckMove(PublicConfiguration config, DeviceObservation observation,
        FixedPoint point, bool controlClosed, Guid committedIntentId)
    {
        var start = CheckStart(config, observation, controlClosed, committedIntentId);
        if (start is not null) return start;
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || !double.IsFinite(point.Z) ||
            point.Z < config.Motion.Limits.ZMin || point.Z > config.Motion.Limits.ZMax ||
            point.X < config.Motion.Limits.XMin || point.X > config.Motion.Limits.XMax ||
            point.Y < config.Motion.Limits.YMin || point.Y > config.Motion.Limits.YMax ||
            point.Unit != config.Motion.Unit || point.Frame != config.Motion.Frame) return "MotionTargetInvalid";
        if (observation.Readiness != DeviceReadiness.Ready) return "DeviceNotReady";
        return null;
    }
}
