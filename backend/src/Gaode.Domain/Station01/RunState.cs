namespace Gaode.Domain.Station01;

public enum RunState
{
    Created, Preparing, ConfigurationBlocked, WaitingStartAcceptance, WaitingSafety,
    WaitingPhysicalStart, WaitingClamp, Running3D, RunningF, SavingHandoff,
    HandoffReady, Detection, Sorting, UnloadPreparation, ReadyForUnlock,
    ObservedUnlocked, AwaitingManualRemoval, FinalUnloadCompleted,
    PauseRequested, Paused, Blocked, StopPending, RecoveryRequired,
    CancelRequested, Cancelled, Restricted, Completed, CompletedWithExceptions, ReadyForRemoval
}

public enum TerminalOutcome { None, Completed, CompletedWithExceptions, Cancelled }
public enum ActionState { NotRequested, IntentPending, IntentCommitted, Dispatched, Accepted, Executing, Completed, Failed, Unknown }
public enum CaptureState { NotRequested, Reserved, Requested, Capturing, Ended, MediaTaken, ContentUnavailable, Failed, Unknown }
public enum AlgorithmState { NotRequested, Queued, Running, Success, Error, TimedOut, NotConfigured, NotIntegrated, NotReady, NoResult, InvalidResult, DependencyFailed, Cancelled }
public enum SaveState { NotQueued, Queued, Writing, Committed, Failed, CommitUnknown, ConditionRejected }
public enum HandoffState { NotReady, Saving, Ready, ReadyWithLimitations }
public enum StepCompleteness { NotStarted, InProgress, ValidEnd, AllowedExceptionEnd, Blocked }

public static class RunStateRules
{
    public static bool IsTerminal(RunState state) => state is RunState.Cancelled or RunState.Completed or RunState.CompletedWithExceptions;
    public static bool IsCompatible(RunState state, TerminalOutcome outcome) => outcome switch
    {
        TerminalOutcome.None => !IsTerminal(state),
        TerminalOutcome.Cancelled => state == RunState.Cancelled,
        TerminalOutcome.Completed => state == RunState.Completed,
        TerminalOutcome.CompletedWithExceptions => state == RunState.CompletedWithExceptions,
        _ => false
    };
}
