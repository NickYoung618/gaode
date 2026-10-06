using Gaode.Application.Workflow;
using Gaode.Domain.Station01;
using Xunit;

namespace Gaode.Rules.Tests.Station01;

public sealed class ControlledRecoveryDecisionTests
{
    [Theory]
    [InlineData(ControlledRecoveryAction.ReDetect)]
    [InlineData(ControlledRecoveryAction.Scrap)]
    public void ManualDecisionPreservesEveryAuditFieldAndOnlyAuthorizesLaterUse(
        ControlledRecoveryAction action)
    {
        var runId = Guid.NewGuid();
        var trayId = Guid.NewGuid();
        var operationId = Guid.NewGuid();
        var decidedAt = new DateTimeOffset(2026, 9, 23, 3, 45, 0, TimeSpan.FromHours(8));
        var decision = ControlledRecoveryDecision.Create(Guid.NewGuid(), "request-17", 42,
            "old-task-9", runId, trayId, operationId, "Detection", action,
            "operator-3", "AuthorizedOperator", decidedAt, "人工核对原始证据后决定",
            ["evidence://stage/8", "evidence://media/12"]);

        var authorization = ControlledRecoveryPolicy.Authorize(decision,
            RecoveryAuthorizationTrigger.AuthenticatedManualDecision, true,
            runId, trayId, "old-task-9", 42);

        Assert.Equal("request-17", decision.RequestId);
        Assert.Equal(42, decision.ExpectedRevision);
        Assert.Equal("old-task-9", decision.OriginalTaskId);
        Assert.Equal(operationId, decision.OriginalOperationId);
        Assert.Equal("operator-3", decision.ActorId);
        Assert.Equal("AuthorizedOperator", decision.ActorRole);
        Assert.Equal(decidedAt, decision.DecidedAt);
        Assert.Equal("人工核对原始证据后决定", decision.Reason);
        Assert.Equal(2, decision.EvidenceReferences.Count);
        Assert.True(authorization.IsAuthorized);
        Assert.Equal(action, authorization.AuthorizedAction);
        Assert.False(authorization.CreatesAlgorithmFact);
        Assert.False(authorization.CreatesPlcFact);
        Assert.False(authorization.CreatesCompletionFact);
    }

    [Theory]
    [InlineData(RecoveryAuthorizationTrigger.HostRestart)]
    [InlineData(RecoveryAuthorizationTrigger.PlcReconnect)]
    [InlineData(RecoveryAuthorizationTrigger.OrdinaryReset)]
    public void AutomaticTriggersCannotCreateOrAuthorizeRecoveryDecision(
        RecoveryAuthorizationTrigger trigger)
    {
        var authorization = ControlledRecoveryPolicy.Authorize(null, trigger, false,
            Guid.NewGuid(), Guid.NewGuid(), "old-task", 3);

        Assert.False(authorization.IsAuthorized);
        Assert.Null(authorization.DecisionId);
        Assert.Equal("AutomaticRecoveryDecisionForbidden", authorization.Reason);
    }

    [Fact]
    public void MissingDecisionUnauthorizedActorOrRevisionConflictIsRejected()
    {
        var runId = Guid.NewGuid();
        var trayId = Guid.NewGuid();
        var decision = ValidDecision(runId, trayId);

        Assert.Equal("ControlledRecoveryDecisionRequired",
            ControlledRecoveryPolicy.Authorize(null,
                RecoveryAuthorizationTrigger.AuthenticatedManualDecision, true,
                runId, trayId, "old-task", 8).Reason);
        Assert.Equal("RecoveryActorNotAuthorized",
            ControlledRecoveryPolicy.Authorize(decision,
                RecoveryAuthorizationTrigger.AuthenticatedManualDecision, false,
                runId, trayId, "old-task", 8).Reason);
        Assert.Equal("RecoveryRevisionConflict",
            ControlledRecoveryPolicy.Authorize(decision,
                RecoveryAuthorizationTrigger.AuthenticatedManualDecision, true,
                runId, trayId, "old-task", 9).Reason);
    }

    [Fact]
    public void MissingAuditEvidenceCannotConstructDecision()
    {
        Assert.Throws<ArgumentException>(() => ControlledRecoveryDecision.Create(
            Guid.NewGuid(), "request", 1, "old-task", Guid.NewGuid(), Guid.NewGuid(),
            null, "Detection", ControlledRecoveryAction.ReDetect, "actor", "role",
            DateTimeOffset.UtcNow, "reason", []));
    }

    private static ControlledRecoveryDecision ValidDecision(Guid runId, Guid trayId) =>
        ControlledRecoveryDecision.Create(Guid.NewGuid(), "request", 8, "old-task",
            runId, trayId, null, "Detection", ControlledRecoveryAction.ReDetect,
            "actor", "role", DateTimeOffset.UtcNow, "reason", ["evidence://stage/1"]);
}
