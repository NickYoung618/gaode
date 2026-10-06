namespace Gaode.Domain.Station01;

public enum OperationKind { PlcStart, ClampObservation, Move3D, Capture3D, Height, MoveF, CaptureF, FDecode, Stop, Save }
public enum OperationPhase { Acceptance, Completion, Result, CriticalCommit }
public enum IngressOutcome { Accepted, TimedOut, Late, Duplicate, Unmatched, Failed }

public sealed record OperationKey(Guid RunId, Guid OperationId, int Attempt, OperationPhase Phase)
{
    public bool IsValid => RunId != Guid.Empty && OperationId != Guid.Empty && Attempt >= 1;
}
