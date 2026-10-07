namespace Gaode.Domain.Station01;

public enum ControlledRecoveryAction
{
    ReDetect,
    Scrap
}

public sealed record ControlledRecoveryDecision
{
    private ControlledRecoveryDecision(Guid decisionId, string requestId, long expectedRevision,
        string originalTaskId, Guid runId, Guid trayId, Guid? originalOperationId,
        string originalStage, ControlledRecoveryAction decision, string actorId,
        string actorRole, DateTimeOffset decidedAt, string reason,
        IReadOnlyList<string> evidenceReferences)
    {
        DecisionId = decisionId;
        RequestId = requestId;
        ExpectedRevision = expectedRevision;
        OriginalTaskId = originalTaskId;
        RunId = runId;
        TrayId = trayId;
        OriginalOperationId = originalOperationId;
        OriginalStage = originalStage;
        Decision = decision;
        ActorId = actorId;
        ActorRole = actorRole;
        DecidedAt = decidedAt;
        Reason = reason;
        EvidenceReferences = evidenceReferences;
    }

    public Guid DecisionId { get; }
    public string RequestId { get; }
    public long ExpectedRevision { get; }
    public string OriginalTaskId { get; }
    public Guid RunId { get; }
    public Guid TrayId { get; }
    public Guid? OriginalOperationId { get; }
    public string OriginalStage { get; }
    public ControlledRecoveryAction Decision { get; }
    public string ActorId { get; }
    public string ActorRole { get; }
    public DateTimeOffset DecidedAt { get; }
    public string Reason { get; }
    public IReadOnlyList<string> EvidenceReferences { get; }

    public static ControlledRecoveryDecision Create(Guid decisionId, string requestId,
        long expectedRevision, string originalTaskId, Guid runId, Guid trayId,
        Guid? originalOperationId, string originalStage, ControlledRecoveryAction decision,
        string actorId, string actorRole, DateTimeOffset decidedAt, string reason,
        IEnumerable<string> evidenceReferences)
    {
        if (decisionId == Guid.Empty) throw new ArgumentException("DecisionId is required.", nameof(decisionId));
        if (runId == Guid.Empty) throw new ArgumentException("RunId is required.", nameof(runId));
        if (trayId == Guid.Empty) throw new ArgumentException("TrayId is required.", nameof(trayId));
        if (expectedRevision < 0) throw new ArgumentOutOfRangeException(nameof(expectedRevision));

        Require(requestId, nameof(requestId));
        Require(originalTaskId, nameof(originalTaskId));
        Require(originalStage, nameof(originalStage));
        Require(actorId, nameof(actorId));
        Require(actorRole, nameof(actorRole));
        Require(reason, nameof(reason));

        var references = evidenceReferences?.Select(value => value?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];
        if (references.Length == 0)
            throw new ArgumentException("At least one evidence reference is required.", nameof(evidenceReferences));

        return new ControlledRecoveryDecision(decisionId, requestId.Trim(), expectedRevision,
            originalTaskId.Trim(), runId, trayId, originalOperationId, originalStage.Trim(),
            decision, actorId.Trim(), actorRole.Trim(), decidedAt, reason.Trim(),
            Array.AsReadOnly(references));
    }

    private static void Require(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{parameterName} is required.", parameterName);
    }
}
