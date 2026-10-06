using Gaode.Application.Ports;
using Gaode.Domain.Station01;
using Xunit;

namespace Gaode.Contracts.Tests.Persistence;

public sealed class ReceiptContractTests
{
    [Fact]
    public void QueuedReceiptDoesNotClaimCommitAndTerminalPayloadsAreConstrained()
    {
        var write = Guid.NewGuid();
        var run = Guid.NewGuid();
        var queued = new CommitReceipt(write, run, CommitState.Queued, null,
            TerminalOutcome.None, null);
        Assert.Null(queued.CommittedRevision);
        var invalid = new WriteBatch(write, run, 4, WriteKind.Complete, "{}", "digest",
            CandidateTerminal: TerminalOutcome.Completed);
        Assert.False(PersistenceContract.IsValid(invalid));
        var valid = invalid with { HandoffJson = "{}", HandoffId = Guid.NewGuid() };
        Assert.True(PersistenceContract.IsValid(valid));
        Assert.False(PersistenceContract.IsValid(valid with { Kind = WriteKind.Cancel }));
    }
}
