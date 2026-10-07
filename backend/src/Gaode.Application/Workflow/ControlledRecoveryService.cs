using Gaode.Domain.Station01;

namespace Gaode.Application.Workflow;

public interface IControlledRecoveryDecisionStore
{
    Task<ControlledRecoveryDecision> SaveAsync(ControlledRecoveryDecision decision,
        CancellationToken cancellationToken = default);

    Task<ControlledRecoveryDecision?> GetAsync(Guid runId, Guid trayId,
        string originalTaskId, CancellationToken cancellationToken = default);
}

public sealed class ControlledRecoveryService(
    IControlledRecoveryDecisionStore store,
    TimeProvider? clock = null)
{
    private readonly TimeProvider clock = clock ?? TimeProvider.System;

    public async Task<ControlledRecoveryDecision> RecordDecisionAsync(
        string requestId,
        long expectedRevision,
        string originalTaskId,
        Guid runId,
        Guid trayId,
        Guid? originalOperationId,
        string originalStage,
        ControlledRecoveryAction action,
        string actorId,
        string actorRole,
        string reason,
        IReadOnlyList<string> evidenceReferences,
        CancellationToken cancellationToken = default)
    {
        var decision = ControlledRecoveryDecision.Create(Guid.NewGuid(), requestId,
            expectedRevision, originalTaskId, runId, trayId, originalOperationId,
            originalStage, action, actorId, actorRole, clock.GetUtcNow(), reason,
            evidenceReferences);
        return await store.SaveAsync(decision, cancellationToken);
    }

    public async Task<ControlledRecoveryAuthorization> AuthorizeAsync(Guid runId,
        Guid trayId, string originalTaskId, long currentRevision,
        RecoveryAuthorizationTrigger trigger, bool actorAuthorizedByHost,
        CancellationToken cancellationToken = default)
    {
        var decision = trigger == RecoveryAuthorizationTrigger.AuthenticatedManualDecision
            ? await store.GetAsync(runId, trayId, originalTaskId, cancellationToken)
            : null;
        return ControlledRecoveryPolicy.Authorize(decision, trigger, actorAuthorizedByHost,
            runId, trayId, originalTaskId, currentRevision);
    }
}
