using Gaode.Domain.Station01;

namespace Gaode.Application.Workflow;

public enum RecoveryAuthorizationTrigger
{
    AuthenticatedManualDecision,
    HostRestart,
    PlcReconnect,
    OrdinaryReset
}

public sealed record ControlledRecoveryAuthorization(
    bool IsAuthorized,
    ControlledRecoveryAction? AuthorizedAction,
    Guid? DecisionId,
    string Reason)
{
    public bool CreatesAlgorithmFact => false;
    public bool CreatesPlcFact => false;
    public bool CreatesCompletionFact => false;
}

public static class ControlledRecoveryPolicy
{
    public static ControlledRecoveryAuthorization Authorize(
        ControlledRecoveryDecision? decision,
        RecoveryAuthorizationTrigger trigger,
        bool actorAuthorizedByHost,
        Guid runId,
        Guid trayId,
        string originalTaskId,
        long currentRevision)
    {
        if (trigger != RecoveryAuthorizationTrigger.AuthenticatedManualDecision)
            return Rejected("AutomaticRecoveryDecisionForbidden");
        if (decision is null)
            return Rejected("ControlledRecoveryDecisionRequired");
        if (!actorAuthorizedByHost)
            return Rejected("RecoveryActorNotAuthorized");
        if (decision.RunId != runId || decision.TrayId != trayId ||
            !StringComparer.Ordinal.Equals(decision.OriginalTaskId, originalTaskId))
            return Rejected("RecoveryIdentityMismatch");
        if (decision.ExpectedRevision != currentRevision)
            return Rejected("RecoveryRevisionConflict");

        return new ControlledRecoveryAuthorization(true, decision.Decision,
            decision.DecisionId, "ControlledDecisionAuthorized");
    }

    private static ControlledRecoveryAuthorization Rejected(string reason) =>
        new(false, null, null, reason);
}
