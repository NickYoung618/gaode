using System.Security.Cryptography;
using System.Text;
using Gaode.Application.Ports;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Gaode.Communication.Tests.Persistence;

public sealed class CommunicationEvidenceStoreTests
{
    [Fact]
    public async Task DurableReferenceFollowsActualCommitAndSurvivesReopen()
    {
        var fixture = await CreateAsync();
        await using var writer = new TraceWriter(fixture.Options, TimeProvider.System, 4);
        var receipt = await new CommunicationEvidenceStore(writer, TimeProvider.System)
            .SaveAsync(fixture.Batch, Window(), CancellationToken.None);
        Assert.Equal(ActualCommitState.Committed, receipt.ActualCommit);
        Assert.Equal(ReceiptValidity.ValidCurrent, receipt.Validity);
        Assert.Equal(new(fixture.StoreId, fixture.Batch.EvidenceId), receipt.Reference);
        await using var reopened = new Station01DbContext(fixture.Options);
        var actual = await reopened.PlcCommunicationEvidence.AsNoTracking().SingleAsync();
        Assert.Equal(fixture.Batch.ObservationId, actual.ObservationId);
        Assert.Contains("SQLiteComponentFixture", actual.RawPayloadJson);
        Assert.Contains(fixture.Batch.Exchanges[0].RequestHex!, actual.RawPayloadJson);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(actual.RawPayloadJson))), actual.PayloadDigest);
        Assert.Empty(await reopened.Writes.ToListAsync()); // raw never enters a business payload.
    }

    [Fact]
    public async Task RealSqliteInsertFailureConfirmsRollbackAndIssuesNoReference()
    {
        var fixture = await CreateAsync();
        await using (var connection = new SqliteConnection(fixture.ConnectionString))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "CREATE TRIGGER reject_component_evidence BEFORE INSERT ON PlcCommunicationEvidence BEGIN SELECT RAISE(ABORT, 'controlled evidence insert rejection'); END";
            await command.ExecuteNonQueryAsync();
        }
        await using var writer = new TraceWriter(fixture.Options, TimeProvider.System, 4);
        var result = await new CommunicationEvidenceStore(writer, TimeProvider.System).SaveAsync(fixture.Batch, Window(), CancellationToken.None);
        Assert.Equal(ActualCommitState.ConfirmedRolledBack, result.ActualCommit);
        Assert.NotEqual(ReceiptValidity.ValidCurrent, result.Validity);
        Assert.Null(result.Reference);
        await using var reopened = new Station01DbContext(fixture.Options);
        Assert.Empty(await reopened.PlcCommunicationEvidence.ToListAsync());
    }

    [Fact]
    public async Task CommittedRowAndLostReceiptRemainDifferentFacts()
    {
        var fixture = await CreateAsync();
        var committed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseReceipt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var writer = new TraceWriter(fixture.Options, TimeProvider.System, 4)
        {
            AfterCommunicationCommitBeforeReceipt = async (_, actual, _) =>
            {
                Assert.Equal(ActualCommitState.Committed, actual.ActualCommit);
                committed.TrySetResult();
                await releaseReceipt.Task;
            }
        };
        using var cancel = new CancellationTokenSource();
        try
        {
            var pending = new CommunicationEvidenceStore(writer, TimeProvider.System).SaveAsync(fixture.Batch, Window(), cancel.Token);
            await committed.Task.WaitAsync(TimeSpan.FromSeconds(5)); // component watchdog, not a business budget
            await using (var reopened = new Station01DbContext(fixture.Options))
                Assert.Equal(fixture.Batch.EvidenceId, (await reopened.PlcCommunicationEvidence.SingleAsync()).EvidenceId);
            cancel.Cancel();
            var receipt = await pending;
            Assert.Equal(ActualCommitState.Unknown, receipt.ActualCommit); // caller has no valid receipt
            Assert.Equal(ReceiptValidity.None, receipt.Validity);
            Assert.Null(receipt.Reference);
        }
        finally { releaseReceipt.TrySetResult(); }
        await writer.WaitForIdleAsync(CancellationToken.None);
        await using var after = new Station01DbContext(fixture.Options);
        Assert.Single(await after.PlcCommunicationEvidence.ToListAsync());
        // No second save/physical action is issued to "recover" the invalid receipt.
    }

    [Fact]
    public async Task LockedDatabaseMakesOutcomeUnknownUntilReadOnlyReconciliationIsPossible()
    {
        var fixture = await CreateAsync();
        await using var blocker = new SqliteConnection(fixture.ConnectionString);
        await blocker.OpenAsync();
        using (var mode = blocker.CreateCommand())
        {
            mode.CommandText = "PRAGMA journal_mode=DELETE";
            Assert.Equal("delete", Convert.ToString(mode.ExecuteScalar()));
            mode.CommandText = "PRAGMA locking_mode=EXCLUSIVE";
            Assert.Equal("exclusive", Convert.ToString(mode.ExecuteScalar()));
        }
        using (var lockCommand = blocker.CreateCommand())
        {
            lockCommand.CommandText = "BEGIN EXCLUSIVE";
            lockCommand.ExecuteNonQuery();
            lockCommand.CommandText = "UPDATE Manifests SET Profile=Profile";
            lockCommand.ExecuteNonQuery();
        }
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var writer = new TraceWriter(fixture.Options, TimeProvider.System, 4)
        { BeforeCommunicationCommit = (_, _) => { started.TrySetResult(); return Task.CompletedTask; } };
        using var cancel = new CancellationTokenSource();
        var task = new CommunicationEvidenceStore(writer, TimeProvider.System).SaveAsync(fixture.Batch, Window(), cancel.Token);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancel.Cancel();
            var receipt = await task;
            Assert.Equal(ActualCommitState.Unknown, receipt.ActualCommit);
            Assert.Null(receipt.Reference);
            Assert.NotEqual(ReceiptValidity.ValidCurrent, receipt.Validity);
            var readOnly = new SqliteConnectionStringBuilder(fixture.ConnectionString)
            { Mode = SqliteOpenMode.ReadOnly, DefaultTimeout = 1 };
            using var reader = new SqliteConnection(readOnly.ToString());
            var unavailable = Assert.Throws<SqliteException>(() =>
            {
                reader.Open();
                using var query = reader.CreateCommand();
                query.CommandText = "SELECT count(*) FROM PlcCommunicationEvidence";
                query.CommandTimeout = 1;
                query.ExecuteScalar();
            });
            Assert.Equal(5, unavailable.SqliteErrorCode); // actual SQLITE_BUSY, not a callback exception
        }
        finally
        {
            using var rollback = blocker.CreateCommand();
            rollback.CommandText = "ROLLBACK";
            rollback.ExecuteNonQuery();
            blocker.Close();
        }
        await writer.WaitForIdleAsync(CancellationToken.None);
        await using var actual = new Station01DbContext(fixture.Options);
        Assert.Empty(await actual.PlcCommunicationEvidence.ToListAsync());
        // Only after the lock ends can this test establish the actual absence of a committed row.
    }

    internal sealed record Fixture(Guid StoreId, string ConnectionString,
        DbContextOptions<Station01DbContext> Options, CommunicationEvidenceBatch Batch);
    internal static async Task<Fixture> CreateAsync(bool hostStore = false)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "global.json"))) root = root.Parent;
        Assert.NotNull(root);
        var approved = Gaode.Testing.ApprovedTestRoot.Resolve(root.FullName);
        var directory = Path.Combine(approved, "009-evidence-component-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        if (hostStore) Directory.CreateDirectory(Path.Combine(directory, "media-root"));
        var connection = new SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, hostStore ? "station01.test.db" : "component.test.db"),
            ForeignKeys = true, Pooling = false }.ToString();
        var options = new DbContextOptionsBuilder<Station01DbContext>().UseSqlite(connection).Options;
        await using var ctx = new Station01DbContext(options);
        await ctx.Database.MigrateAsync();
        var storeId = Guid.NewGuid();
        ctx.Manifests.Add(new() { StoreId = storeId, SchemaVersion = "s01-store/2", Profile = "Test",
            PrepareOperationId = Guid.NewGuid(), PreparedUtc = DateTimeOffset.UtcNow });
        await ctx.SaveChangesAsync();
        var now = DateTimeOffset.UtcNow;
        var batch = new CommunicationEvidenceBatch(Guid.NewGuid(), storeId, Guid.NewGuid(), null, null, null, 1,
            new(DeviceProvider.Virtual, "SQLiteComponentFixture/1", EvidenceQuality.Derived), "component-only/1", "ABCD",
            now, now, [new(Guid.NewGuid(), "component-fixture", 1, 1, 3, 0, 1, now, now,
                "000100000006010300000001", "0001000000050103020000", null)], "Fixture persistence only; not real TCP proof", false, Guid.NewGuid());
        return new(storeId, connection, options, batch);
    }
    private static ActionWindow Window()
    {
        var clock = TimeProvider.System;
        var start = clock.GetTimestamp();
        return new(start, start + clock.TimestampFrequency * 10, "system", clock.GetUtcNow(), clock.GetUtcNow().AddSeconds(10));
    }
}
