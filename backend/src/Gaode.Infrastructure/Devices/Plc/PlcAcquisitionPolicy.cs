namespace Gaode.Infrastructure.Devices.Plc;

internal static class PlcAcquisitionPolicy
{
    internal const string Identity = "plc-acquisition/013-1";
    internal const int HeartbeatMs = 300, ActiveBaseMs = 200, IdleBaseMs = 500,
        FeedbackMs = 200, MovingPositionMs = 500, StaticPositionMs = 1000, FirstStateMs = 50;
}
