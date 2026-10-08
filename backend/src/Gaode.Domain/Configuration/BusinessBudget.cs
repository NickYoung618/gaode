namespace Gaode.Domain.Configuration;

public sealed record BusinessDurations(int PlcAcceptance, int ClampCompletion, int XyCompletion,
    int Capture3d, int? HeightAlgorithm, int CaptureF, int? FDecode, int SafetyReady,
    int StopAcceptance, int StopCompletion, int CriticalSave, int WorkerReleaseGrace,
    int HeartbeatFlip, int HeartbeatDisconnect, int Query, int PlcIo,
    int? RecipeApplication = null, int? FlipCompletion = null,
    int? PutBackCompletion = null, int? TrayPoseAlgorithm = null);

public sealed record CapacityLimits(int FlowNormal, int FlowControl, int TerminalReservations,
    int Writer, int MediaJobs, int AlgorithmQueuePerRole, int WorkerPerRole,
    int NotificationsPerClient, int NotificationClients, int QueryConcurrency,
    int QueryPageMax, long MediaMemoryBytes, long RunMediaQuotaBytes,
    long DataQuotaBytes, long MinFreeDiskBytes, long FReservedMemoryBytes,
    int LateEvidencePerOperation, int DuplicateSummariesPerOperation,
    int MaxIpcMessageBytes, int MaxPendingTimerEvents);

public sealed record BusinessBudget(string SchemaVersion, string Id, string Version, string Purpose,
    string Source, BusinessDurations BusinessMs, CapacityLimits Limits,
    string PhysicalButtonWait, string TimeoutBoundary)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public RecipeExecutionAllowances? RecipeExecution { get; init; }
}

public sealed record RecipeExecutionAllowances(int CaptureMs, int AlgorithmMs, int AcquisitionReleaseMs,
    int CaptureWaitMs, int AlgorithmWaitMs, int InputReleaseWaitMs)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsValid => CaptureMs > 0 && AlgorithmMs > 0 && AcquisitionReleaseMs > 0 &&
        CaptureWaitMs > 0 && AlgorithmWaitMs > 0 && InputReleaseWaitMs > 0;
}
