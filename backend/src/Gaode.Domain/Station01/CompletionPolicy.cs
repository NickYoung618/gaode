namespace Gaode.Domain.Station01;

public sealed record CompletionEvidence(bool StartAccepted, bool DeviceReady,
    bool Move3DCompleted, bool Capture3DCompleted, bool HeightTerminalSaved,
    bool MoveFCompleted, bool CaptureFCompleted, bool FTerminalSaved,
    bool MediaSaved, bool SafetyClear, bool MotionKnown, bool AllNecessarySaved);

public static class CompletionPolicy
{
    public static bool CanComplete(CompletionEvidence e) =>
        e.StartAccepted && e.DeviceReady &&
        e.Move3DCompleted && e.Capture3DCompleted && e.HeightTerminalSaved &&
        e.MoveFCompleted && e.CaptureFCompleted && e.FTerminalSaved &&
        e.MediaSaved && e.SafetyClear && e.MotionKnown && e.AllNecessarySaved;
}
