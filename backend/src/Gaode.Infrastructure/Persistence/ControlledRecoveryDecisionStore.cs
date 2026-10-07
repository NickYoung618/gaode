using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Workflow;
using Gaode.Domain.Station01;
using Microsoft.EntityFrameworkCore;

namespace Gaode.Infrastructure.Persistence;

public sealed class ControlledRecoveryDecisionStore(
    DbContextOptions<Station01DbContext> options) : IControlledRecoveryDecisionStore
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<ControlledRecoveryDecision> SaveAsync(ControlledRecoveryDecision decision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decision);
        var digest = Digest(decision);
        await using var db = new Station01DbContext(options);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var existing = await db.ControlledRecoveryDecisions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.RunId == decision.RunId &&
                x.RequestId == decision.RequestId, cancellationToken);
        if (existing is not null)
        {
            await tx.CommitAsync(cancellationToken);
            if (existing.PayloadDigest != digest)
                throw new InvalidOperationException("ControlledRecoveryDecisionConflict");
            return ToDomain(existing);
        }

        var run = await db.Runs.SingleOrDefaultAsync(x => x.RunId == decision.RunId,
            cancellationToken) ?? throw new InvalidOperationException("RecoveryRunNotPersisted");
        if (run.Revision != decision.ExpectedRevision)
            throw new InvalidOperationException("RecoveryRevisionConflict");
        var entity = new ControlledRecoveryDecisionEntity
        {
            DecisionId = decision.DecisionId,
            RequestId = decision.RequestId,
            ExpectedRevision = decision.ExpectedRevision,
            OriginalTaskId = decision.OriginalTaskId,
            RunId = decision.RunId,
            TrayId = decision.TrayId,
            OriginalOperationId = decision.OriginalOperationId,
            OriginalStage = decision.OriginalStage,
            Decision = decision.Decision.ToString(),
            ActorId = decision.ActorId,
            ActorRole = decision.ActorRole,
            DecidedAtUtc = decision.DecidedAt,
            Reason = decision.Reason,
            EvidenceReferencesJson = JsonSerializer.Serialize(decision.EvidenceReferences,
                JsonOptions),
            PayloadDigest = digest,
            RetainUntilUtc = StageEventRetentionPolicy.RetainUntil(decision.DecidedAt)
        };
        db.ControlledRecoveryDecisions.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return decision;
    }

    public async Task<ControlledRecoveryDecision?> GetAsync(Guid runId, Guid trayId,
        string originalTaskId, CancellationToken cancellationToken = default)
    {
        await using var db = new Station01DbContext(options);
        var candidates = await db.ControlledRecoveryDecisions.AsNoTracking().Where(x =>
            x.RunId == runId && x.TrayId == trayId &&
            x.OriginalTaskId == originalTaskId).ToListAsync(cancellationToken);
        var entity = candidates.OrderByDescending(x => x.DecidedAtUtc).FirstOrDefault();
        return entity is null ? null : ToDomain(entity);
    }

    private static ControlledRecoveryDecision ToDomain(ControlledRecoveryDecisionEntity entity) =>
        ControlledRecoveryDecision.Create(entity.DecisionId, entity.RequestId,
            entity.ExpectedRevision, entity.OriginalTaskId, entity.RunId, entity.TrayId,
            entity.OriginalOperationId, entity.OriginalStage,
            Enum.Parse<ControlledRecoveryAction>(entity.Decision), entity.ActorId,
            entity.ActorRole, entity.DecidedAtUtc, entity.Reason,
            JsonSerializer.Deserialize<string[]>(entity.EvidenceReferencesJson, JsonOptions) ?? []);

    private static string Digest(ControlledRecoveryDecision decision) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new
            {
                decision.RequestId, decision.ExpectedRevision, decision.OriginalTaskId,
                decision.RunId, decision.TrayId, decision.OriginalOperationId,
                decision.OriginalStage, decision.Decision, decision.ActorId,
                decision.ActorRole, decision.Reason, decision.EvidenceReferences
            }, JsonOptions))));
}
