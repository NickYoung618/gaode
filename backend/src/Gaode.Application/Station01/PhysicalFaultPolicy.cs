using Gaode.Domain.Station01;
using Gaode.Application.Ports;

namespace Gaode.Application.Station01;

public sealed record PhysicalRestriction(bool Allowed, bool Unknown, string Code, string Message);

public static class PhysicalFaultPolicy
{
    public static PhysicalRestriction Evaluate(DeviceObservation observation,
        bool heartbeatLocked, bool recoveryExplicitlyAuthorized)
    {
        if (heartbeatLocked)
            return new(false, true, "HeartbeatTimeoutRestricted",
                "连续3秒无有效心跳，物理动作已锁定并等待人工恢复核对");
        if (recoveryExplicitlyAuthorized)
            return new(false, false, "ManualRestartRequired",
                "恢复核对不自动续跑，必须由新命令重新准入");
        return Evaluate(observation);
    }

    public static PhysicalRestriction Evaluate(DeviceObservation observation)
    {
        if (!observation.HasReliableObservation)
            return new(false, true, "PlcDisconnected", "PLC连接状态未知，禁止新的物理动作");
        if (observation.OperatingMode != OperatingMode.Automatic)
            return new(false, true, "ManualMode", "PLC未处于自动模式，禁止新的物理动作");
        if (observation.SafetyAssessment != SafetyAssessment.Clear)
            return new(false, false, "SafetyOrPlcFault", "安全互锁或PLC故障未清除");
        if (observation.MotionAvailability == MotionAvailability.HeldUnknown)
            return new(false, true, "MotionUnknown", "运动完成状态未知，需人工核对");
        return new(true, false, "Ready", "物理状态允许继续");
    }
}
