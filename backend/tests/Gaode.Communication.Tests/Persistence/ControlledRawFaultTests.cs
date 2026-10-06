using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Gaode.Communication.Tests.Persistence;

public sealed class ControlledRawFaultTests
{
    [Theory]
    [InlineData("FAULT-SEAM/raw-before-commit", "F06-A")]
    [InlineData("FAULT-SEAM/raw-after-commit", "F06-B")]
    [InlineData("FAULT-SEAM/raw-unavailable", "F06-C")]
    public async Task SameHostWriterSeamDistinguishesActualStorageAndReference(string caseId, string faultCase)
    {
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        var sourceFixture = await CommunicationEvidenceStoreTests.CreateAsync(hostStore: true);
        var root = Path.GetDirectoryName(new SqliteConnectionStringBuilder(sourceFixture.ConnectionString).DataSource)!;
        var parent = Path.GetDirectoryName(root)!;
        using var guard = StoreAccessGuard.Acquire(root, parent);
        using var fault = new ControlledTestPersistenceFault(root, faultCase);
        var options = new DbContextOptionsBuilder<Station01DbContext>().UseSqlite(StoreCompatibilityProbe.ReadWriteConnectionString(root));
        fault.Configure(options);
        await using var writer = fault.CreateWriter(options.Options, TimeProvider.System, 8);
        var run = Guid.NewGuid(); var nonce = Guid.NewGuid();
        await PublishAsync("009-fault-arm.json", JsonSerializer.Serialize(new { caseId = faultCase, runId = run, nonce }), watchdog.Token);
        var batch = sourceFixture.Batch with { StoreId = StoreCompatibilityProbe.Inspect(root).StoreId!.Value,
            RunId = run, Interpretation = "MaterialPicked" };
        var clock = TimeProvider.System; var tick = clock.GetTimestamp();
        var window = new ActionWindow(tick, tick + clock.TimestampFrequency * 10, "system", clock.GetUtcNow(), clock.GetUtcNow().AddSeconds(10));
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(watchdog.Token);
        var saving = new CommunicationEvidenceStore(writer, clock).SaveAsync(batch, window, caller.Token);
        if (faultCase == "F06-A") {
            var receipt = await saving;
            Assert.Equal(ActualCommitState.ConfirmedRolledBack, receipt.ActualCommit);
            Assert.Null(receipt.Reference);
        }
        else {
            var log = Path.Combine(root, "009-fault-events.jsonl");
            while (!File.Exists(log) || !(await File.ReadAllTextAsync(log, watchdog.Token)).Contains("ReceiptHeld"))
                await Task.Delay(20, watchdog.Token);
            if (faultCase == "F06-B") Assert.Equal(1, await CountAsync());
            else {
                using var reader = new SqliteConnection(new SqliteConnectionStringBuilder {
                    DataSource = Path.Combine(root, "station01.test.db"), Pooling = false, Mode = SqliteOpenMode.ReadOnly, DefaultTimeout = 1 }.ToString());
                var error = Assert.Throws<SqliteException>(() => {
                    reader.Open(); using var sql = reader.CreateCommand(); sql.CommandText = "SELECT count(*) FROM PlcCommunicationEvidence";
                    sql.CommandTimeout = 1; sql.ExecuteScalar(); });
                Assert.Equal(5, error.SqliteErrorCode);
            }
            caller.Cancel();
            var receipt = await saving;
            Assert.Equal(ActualCommitState.Unknown, receipt.ActualCommit);
            Assert.Equal(ReceiptValidity.None, receipt.Validity);
            Assert.Null(receipt.Reference);
            await PublishAsync("009-fault-release.json", JsonSerializer.Serialize(new { runId = run, nonce }), watchdog.Token);
        }
        await writer.WaitForIdleAsync(watchdog.Token);
        Assert.Equal(faultCase == "F06-B" ? 1 : 0, await CountAsync());
        var evidence = Environment.GetEnvironmentVariable("GAODE_009_EVIDENCE_ROOT") ?? throw new InvalidOperationException("EvidenceRootRequired");
        Directory.CreateDirectory(evidence);
        await File.WriteAllTextAsync(Path.Combine(evidence, caseId.Replace('/', '-') + "-" + run + ".json"),
            JsonSerializer.Serialize(new { caseId, runId = run, root, batch.EvidenceId, actualRows = await CountAsync(),
                scope = "ActualSQLiteHostWriterSeam;LiteralPayloadFixtureNotTcpOrPhysicalPickProof",
                events = await File.ReadAllLinesAsync(Path.Combine(root, "009-fault-events.jsonl")) }), watchdog.Token);

        async Task PublishAsync(string name, string payload, CancellationToken token) {
            var destination = Path.Combine(root, name);
            var temporary = destination + ".pending";
            await File.WriteAllTextAsync(temporary, payload, token);
            File.Move(temporary, destination); // Publish complete, closed input exactly once.
        }
        async Task<int> CountAsync() {
            await using var db = new Station01DbContext(new DbContextOptionsBuilder<Station01DbContext>()
                .UseSqlite(StoreCompatibilityProbe.ReadOnlyConnectionString(root)).Options);
            return await db.PlcCommunicationEvidence.CountAsync(e => e.EvidenceId == batch.EvidenceId, watchdog.Token);
        }
    }
}
