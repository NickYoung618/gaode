using Gaode.Application.Ports;
using Gaode.Application.Station01;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Gaode.Infrastructure.Persistence;

public sealed class TraceQuery : ITraceQuery, IStageHandoffQuery
{
    public async Task<IReadOnlyList<AlgorithmResourceState>> GetUnreclaimedResourcesAsync(int offset, int limit,
        CancellationToken cancellationToken)
    {
        using var budget = Budget(cancellationToken);
        return await new StageEventStore(options, clock).GetUnreclaimedResourcesAsync(offset, limit, budget.Token);
    }
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly DbContextOptions<Station01DbContext> options;
    private readonly TimeProvider clock;
    private readonly int queryBudgetMs;

    public TraceQuery(DbContextOptions<Station01DbContext> options,
        TimeProvider? clock = null, int queryBudgetMs = 1000)
    {
        if (queryBudgetMs <= 0) throw new ArgumentOutOfRangeException(nameof(queryBudgetMs));
        this.options = options;
        this.clock = clock ?? TimeProvider.System;
        this.queryBudgetMs = queryBudgetMs;
    }

    public async Task<StartReceipt?> GetStartReceiptAsync(string subject, string requestId, CancellationToken cancellationToken)
    {
        using var budget = Budget(cancellationToken);
        await using var ctx = new Station01DbContext(options);
        var rows = await ctx.Commands.AsNoTracking().Where(x => x.SubjectId == subject && x.RequestId == requestId &&
            x.Kind == "Start" && x.Scope == "Station01").Take(2).ToListAsync(budget.Token);
        if (rows.Count > 1) throw new InvalidOperationException("StartRequestRecordsInconsistent");
        var row = rows.SingleOrDefault();
        return row is null ? null : new(row.CommandId, row.RunId, row.ReceiptState,
            $"/api/v1/station01/runs/{row.RunId:D}", true);
    }
    public async Task<IReadOnlyList<PersistedRun>> GetUnfinishedRunsAsync(CancellationToken cancellationToken)
    {
        using var budget = Budget(cancellationToken);
        await using var ctx = new Station01DbContext(options);
        var rows = await ctx.Runs.AsNoTracking().Where(x => x.Terminal == Gaode.Domain.Station01.TerminalOutcome.None)
            .ToListAsync(budget.Token);
        return rows.Select(x => new PersistedRun(x.RunId, x.RequestId, x.SubjectId,
            x.ContextJson, x.State, x.Revision, x.Terminal, x.TerminalRevision,
            x.CancelRequested)).ToArray();
    }
    public async Task<PersistedRun?> GetRunAsync(Guid runId, CancellationToken cancellationToken)
    {
        using var budget = Budget(cancellationToken);
        await using var ctx = new Station01DbContext(options);
        var row = await ctx.Runs.AsNoTracking().SingleOrDefaultAsync(x => x.RunId == runId, budget.Token);
        return row is null ? null : new(row.RunId, row.RequestId, row.SubjectId,
            row.ContextJson, row.State, row.Revision, row.Terminal, row.TerminalRevision,
            row.CancelRequested);
    }

    public async Task<IReadOnlyList<PersistedWrite>> GetWritesAsync(Guid runId, CancellationToken cancellationToken)
    {
        using var budget = Budget(cancellationToken);
        await using var ctx = new Station01DbContext(options);
        var rows = await ctx.Writes.AsNoTracking().Where(x => x.RunId == runId)
            .OrderBy(x => x.Revision).ToListAsync(budget.Token);
        return rows.Select(x => new PersistedWrite(x.WriteId, x.RunId, x.Revision,
            Enum.Parse<WriteKind>(x.Kind), x.PayloadJson, x.PayloadDigest, CommitState.Committed)).ToArray();
    }

    public async Task<PersistedWrite?> GetWriteAsync(Guid writeId, CancellationToken cancellationToken)
    {
        using var budget = Budget(cancellationToken);
        await using var ctx = new Station01DbContext(options);
        var x = await ctx.Writes.AsNoTracking().SingleOrDefaultAsync(x => x.WriteId == writeId, budget.Token);
        return x is null ? null : new(x.WriteId, x.RunId, x.Revision,
            Enum.Parse<WriteKind>(x.Kind), x.PayloadJson, x.PayloadDigest, CommitState.Committed);
    }

    public async Task<PersistedHandoff?> GetHandoffAsync(Guid runId, CancellationToken cancellationToken)
    {
        using var budget = Budget(cancellationToken);
        await using var ctx = new Station01DbContext(options);
        var x = await ctx.Handoffs.AsNoTracking().SingleOrDefaultAsync(x => x.RunId == runId, budget.Token);
        return x is null ? null : new(x.HandoffId, x.RunId, x.PayloadJson, x.Revision,
            x.Terminal, x.WriteId);
    }

    public async Task<CommittedPublicPreparationHandoffV2?> GetCommittedV2Async(
        Guid runId, CancellationToken cancellationToken)
    {
        using var budget = Budget(cancellationToken);
        await using var ctx = new Station01DbContext(options);
        var row = await ctx.PublicPreparationHandoffsV2.AsNoTracking().SingleOrDefaultAsync(
            x => x.RunId == runId, budget.Token);
        if (row is null) return null;
        var write = await ctx.Writes.AsNoTracking().SingleOrDefaultAsync(
            x => x.WriteId == row.WriteId && x.RunId == row.RunId && x.Revision == row.Revision,
            budget.Token);
        if (write is null) return null;

        PublicPreparationHandoffV2? handoff;
        try { handoff = JsonSerializer.Deserialize<PublicPreparationHandoffV2>(row.PayloadJson, JsonOptions); }
        catch (JsonException) { return null; }
        if (handoff is null || !handoff.IsComplete || handoff.Identity.RunId != runId ||
            handoff.Identity.TrayId != row.TrayId) return null;
        var digest = row.PayloadDigest;
        if (!StringComparer.Ordinal.Equals(write.PayloadDigest, digest)) return null;
        if (!StringComparer.Ordinal.Equals(digest, handoff.PayloadDigest)) return null;
        return new CommittedPublicPreparationHandoffV2(handoff, row.HandoffId, row.WriteId,
            row.Revision, digest);
    }

    private QueryBudget Budget(CancellationToken caller) =>
        new(caller, TimeSpan.FromMilliseconds(queryBudgetMs), clock);

    private sealed class QueryBudget : IDisposable
    {
        private readonly CancellationTokenSource deadline;
        private readonly CancellationTokenRegistration callerRegistration;
        public QueryBudget(CancellationToken caller, TimeSpan duration, TimeProvider clock)
        {
            deadline = new(duration, clock);
            callerRegistration = caller.CanBeCanceled
                ? caller.Register(static state => ((CancellationTokenSource)state!).Cancel(), deadline)
                : default;
        }
        public CancellationToken Token => deadline.Token;
        public void Dispose()
        {
            callerRegistration.Dispose();
            deadline.Dispose();
        }
    }
}
