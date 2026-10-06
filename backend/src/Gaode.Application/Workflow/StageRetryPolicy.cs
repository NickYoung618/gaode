using Gaode.Application.Ports;

namespace Gaode.Application.Workflow;

public enum StageRetryDisposition { Retry, Pending, Failed, UnknownHeld }

public sealed record StageRetryContext(
    StageFailureClass Failure,
    int Attempt,
    DateTimeOffset StageStartedAt,
    DateTimeOffset StageDeadlineAt,
    DateTimeOffset Now,
    string ErrorCode,
    Guid OperationId);

public sealed record StageRetryDecision(
    StageRetryDisposition Disposition,
    int Attempt,
    int TotalAttemptLimit,
    TimeSpan? Delay,
    DateTimeOffset? PlannedAt,
    DateTimeOffset StageDeadlineAt,
    string ErrorCode,
    Guid OperationId,
    bool AutomaticRetryAllowed);

public static class StageRetryPolicy
{
    public const int CommunicationTotalAttempts = 4;
    public const int AlgorithmTimeoutTotalAttempts = 3;
    public static readonly TimeSpan StageDuration = TimeSpan.FromSeconds(120);
    private static readonly TimeSpan[] CommunicationBackoff =
        [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)];
    private static readonly TimeSpan[] AlgorithmBackoff =
        [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5)];

    public static DateTimeOffset FreezeDeadline(DateTimeOffset stageStartedAt,
        DateTimeOffset? persistedDeadline = null) => persistedDeadline ?? stageStartedAt + StageDuration;

    public static StageRetryDecision Decide(StageRetryContext context)
    {
        if (context.Attempt < 1) throw new ArgumentOutOfRangeException(nameof(context));
        if (context.OperationId == Guid.Empty) throw new ArgumentException("OperationId is required.", nameof(context));
        if (string.IsNullOrWhiteSpace(context.ErrorCode))
            throw new ArgumentException("ErrorCode is required.", nameof(context));
        if (context.StageDeadlineAt <= context.StageStartedAt)
            throw new ArgumentException("Stage deadline is invalid.", nameof(context));

        if (context.Failure == StageFailureClass.PlcPhysicalDispatchUnknown)
            return Final(context, StageRetryDisposition.UnknownHeld, 1, context.ErrorCode, false);

        var detection = context.Failure is StageFailureClass.DetectionCommunication or
            StageFailureClass.DetectionAlgorithmTimeout;
        var limit = context.Failure == StageFailureClass.DetectionAlgorithmTimeout
            ? AlgorithmTimeoutTotalAttempts
            : CommunicationTotalAttempts;
        var backoff = context.Failure == StageFailureClass.DetectionAlgorithmTimeout
            ? AlgorithmBackoff
            : CommunicationBackoff;
        var exhaustedDisposition = detection ? StageRetryDisposition.Pending : StageRetryDisposition.Failed;

        if (context.Now >= context.StageDeadlineAt)
            return Final(context, exhaustedDisposition, limit, "StageDeadlineExceeded", false);
        if (context.Attempt >= limit)
            return Final(context, exhaustedDisposition, limit, context.ErrorCode, false);

        var delay = backoff[context.Attempt - 1];
        var plannedAt = context.Now + delay;
        if (plannedAt >= context.StageDeadlineAt)
            return Final(context, exhaustedDisposition, limit, "StageDeadlineWouldBeExceeded", false);

        return new(StageRetryDisposition.Retry, context.Attempt, limit, delay, plannedAt,
            context.StageDeadlineAt, context.ErrorCode, context.OperationId, true);
    }

    private static StageRetryDecision Final(StageRetryContext context,
        StageRetryDisposition disposition, int limit, string error, bool canRetry) =>
        new(disposition, context.Attempt, limit, null, null, context.StageDeadlineAt,
            error, context.OperationId, canRetry);
}
