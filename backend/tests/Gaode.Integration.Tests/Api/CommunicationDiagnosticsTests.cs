using System.Net;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Domain.Station01;
using Gaode.Infrastructure.Persistence;
using Gaode.Integration.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Gaode.Integration.Tests.Api;

// Diagnostic route/storage tests; fixture traffic is explicitly not a real TCP run.
public sealed class CommunicationDiagnosticsTests
{
    [Fact]
    public async Task EvidenceEtagChangesWhenOnlyFailureDiagnosticsBecomeDurable()
    {
        var runId = Guid.NewGuid();
        await using var host = await Station01HostFixture.CreateAsync(afterStorePrepared: async root => {
            var options = new DbContextOptionsBuilder<Station01DbContext>().UseSqlite(StoreCompatibilityProbe.ReadWriteConnectionString(root)).Options;
            await using var db = new Station01DbContext(options);
            db.Runs.Add(new() { RunId = runId, RequestId = runId.ToString(), SubjectId = "DiagnosticEtagFixture",
                State = RunState.Blocked, CreatedUtc = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        });
        var path = $"/api/v1/station01/runs/{runId:D}/evidence";
        using var before = await host.Client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        var options = host.Host.Services.GetRequiredService<DbContextOptions<Station01DbContext>>();
        await using var db = new Station01DbContext(options);
        var manifest = await db.Manifests.AsNoTracking().SingleAsync();
        var clock = TimeProvider.System; var utc = clock.GetUtcNow(); var tick = clock.GetTimestamp();
        var batch = new CommunicationEvidenceBatch(Guid.NewGuid(), manifest.StoreId, Guid.NewGuid(), runId, Guid.NewGuid(), Guid.NewGuid(), 1,
            new(DeviceProvider.Virtual, "DiagnosticEtagFixture/1", EvidenceQuality.Derived), "fixture-only/1", "Abcd", utc, utc,
            [new(Guid.NewGuid(), "component-fixture", 1, 1, 3, 0, 1, utc, utc, "000100000006010300000001", null, "TimeoutException")],
            "Explicit stored timeout fixture, no TCP acceptance claim", false, Guid.NewGuid());
        await using (var writer = new TraceWriter(options, clock, 4))
        {
            var receipt = await new CommunicationEvidenceStore(writer, clock).SaveAsync(batch,
                new(tick, tick + clock.TimestampFrequency * 10, "system", utc, utc.AddSeconds(10)), CancellationToken.None);
            Assert.Equal(ReceiptValidity.ValidCurrent, receipt.Validity);
        }
        using var conditional = new HttpRequestMessage(HttpMethod.Get, path);
        conditional.Headers.IfNoneMatch.Add(before.Headers.ETag!);
        using var after = await host.Client.SendAsync(conditional);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.NotEqual(before.Headers.ETag, after.Headers.ETag);
        using var document = JsonDocument.Parse(await after.Content.ReadAsStringAsync());
        Assert.Equal(batch.EvidenceId, Assert.Single(document.RootElement.GetProperty("diagnosticEvidenceReferences").EnumerateArray())
            .GetProperty("evidenceId").GetGuid());
        Assert.Empty(await db.Writes.ToArrayAsync()); Assert.Empty(await db.StageEvents.ToArrayAsync());
        Assert.Equal(0, host.Plc.StartCommands);
    }

    [Fact]
    public async Task HistoricalRunEvidenceProjectsRecordedClaimWithoutChangingPayloadOrGrantingActions()
    {
        var runId = Guid.NewGuid(); var eventId = Guid.NewGuid();
        var bindingId = Guid.NewGuid(); var bindingWriteId = Guid.NewGuid();
        var oldBinding = JsonSerializer.Serialize(new { kind = "RecipePlanBound", bindingId, source = new { configurationId = "old-partial" } });
        const string payload = """
        {"kind":"SortingAssignmentInTransit","protocolStatus":2,"positionEvidence":[
          {"Phase":"PickCompleted","Status":2,"TargetX":10,"TargetY":20,"TargetZ":30,
           "ActualX":10,"ActualY":20,"ActualZ":35,"Tolerance":0.1,"ObservedAtUtc":"2026-09-27T00:00:00Z"}]}
        """;
        var digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload)));
        await using var host = await Station01HostFixture.CreateAsync(afterStorePrepared: async root => {
            var options = new DbContextOptionsBuilder<Station01DbContext>().UseSqlite(StoreCompatibilityProbe.ReadWriteConnectionString(root)).Options;
            await using var db = new Station01DbContext(options);
            db.Runs.Add(new() { RunId = runId, RequestId = runId.ToString(), SubjectId = "HistoricalFixture",
                State = RunState.Blocked, CreatedUtc = DateTimeOffset.UtcNow });
            db.StageEvents.Add(new() { EventId = eventId, RunId = runId, TrayId = Guid.NewGuid(), OperationId = Guid.NewGuid(),
                Stage = "Sorting", EventType = "Executing", PlanRevision = "old-plan", Attempt = 1, ConnectionEpoch = 1,
                Source = "Real", Quality = "Derived", Sequence = 1, IdempotencyKey = eventId.ToString(),
                PayloadJson = payload, PayloadDigest = digest, OccurredUtc = DateTimeOffset.UtcNow, PersistedUtc = DateTimeOffset.UtcNow,
                RetainUntilUtc = DateTimeOffset.UtcNow.AddDays(1) });
            db.Writes.Add(new() { WriteId = bindingWriteId, RunId = runId, Revision = 1, Kind = "ActionFact",
                PayloadJson = oldBinding, PayloadDigest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(oldBinding))),
                CommittedUtc = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        });
        using var evidence = JsonDocument.Parse(await host.Client.GetStringAsync($"/api/v1/station01/runs/{runId:D}/evidence"));
        Assert.Equal("device-semantics/1", evidence.RootElement.GetProperty("deviceSchemaVersion").GetString());
        var stage = Assert.Single(evidence.RootElement.GetProperty("stages").EnumerateArray());
        Assert.Equal("LegacyRecordedClaim", stage.GetProperty("recordNature").GetString());
        Assert.Equal("Real", stage.GetProperty("source").GetString());
        var position = Assert.Single(stage.GetProperty("positionEvidence").EnumerateArray());
        Assert.Equal("PickObserved", position.GetProperty("kind").GetString());
        Assert.Equal(35, position.GetProperty("actual").GetProperty("z").GetDouble());
        Assert.Equal(JsonValueKind.Null, position.GetProperty("observationId").ValueKind);
        Assert.Empty(evidence.RootElement.GetProperty("diagnosticEvidenceReferences").EnumerateArray());
        var binding = Assert.Single(evidence.RootElement.GetProperty("motionEvidence").EnumerateArray()).GetProperty("facts");
        Assert.Equal("LegacyRecordedClaim", binding.GetProperty("recordNature").GetString());
        var application = binding.GetProperty("recipeApplication");
        Assert.Equal(JsonValueKind.Null, application.GetProperty("budgetReference").ValueKind);
        Assert.Equal(JsonValueKind.Null, application.GetProperty("window").ValueKind);
        Assert.Equal(JsonValueKind.Null, application.GetProperty("hostValidatedTick").ValueKind);
        Assert.Equal(JsonValueKind.Null, application.GetProperty("deviceApplied").ValueKind);
        using var run = JsonDocument.Parse(await host.Client.GetStringAsync($"/api/v1/station01/runs/{runId:D}"));
        Assert.Empty(run.RootElement.GetProperty("allowedActions").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, run.RootElement.GetProperty("startupDiagnostic").ValueKind);
        await using var reopened = new Station01DbContext(host.Host.Services.GetRequiredService<DbContextOptions<Station01DbContext>>());
        var original = await reopened.StageEvents.AsNoTracking().SingleAsync();
        Assert.Equal(payload, original.PayloadJson); Assert.Equal(digest, original.PayloadDigest);
        Assert.Equal(oldBinding, (await reopened.Writes.AsNoTracking().SingleAsync()).PayloadJson);
        Assert.Empty(await reopened.PlcCommunicationEvidence.ToArrayAsync());
    }

    [Fact]
    public async Task DiagnosticRouteReadsOnlyCommittedLocalEvidenceAndRequiresReadPermission()
    {
        Guid evidenceId = Guid.Empty;
        await using var host = await Station01HostFixture.CreateAsync(afterStorePrepared: async root =>
        {
            var options = new DbContextOptionsBuilder<Station01DbContext>()
                .UseSqlite(StoreCompatibilityProbe.ReadWriteConnectionString(root)).Options;
            await using var db = new Station01DbContext(options);
            var store = await db.Manifests.AsNoTracking().SingleAsync();
            var now = DateTimeOffset.UtcNow;
            var tick = TimeProvider.System.GetTimestamp();
            var window = new ActionWindow(tick, tick + TimeProvider.System.TimestampFrequency * 10, "system", now, now.AddSeconds(10));
            var batch = new CommunicationEvidenceBatch(Guid.NewGuid(), store.StoreId, Guid.NewGuid(), null, null, null, 1,
                new(DeviceProvider.Virtual, "DiagnosticApiFixture/1", EvidenceQuality.Derived), "fixture-only/1", "Abcd", now, now,
                [new(Guid.NewGuid(), "diagnostic-api-fixture", 1, 1, 3, 0, 1, now, now,
                    "000100000006010300000001", null, "TimeoutException")], "Explicit fixture; not formal TCP proof", false, Guid.NewGuid());
            await using var writer = new TraceWriter(options, TimeProvider.System, 4);
            var saved = await new CommunicationEvidenceStore(writer, TimeProvider.System).SaveAsync(batch, window, CancellationToken.None);
            Assert.Equal(ReceiptValidity.ValidCurrent, saved.Validity);
            evidenceId = batch.EvidenceId;
        });
        var path = $"/api/v1/station01/diagnostics/communication/{evidenceId:D}";
        using var response = await host.Client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Headers.ETag);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = document.RootElement;
        Assert.Equal("plc-evidence/1", body.GetProperty("schemaVersion").GetString());
        Assert.Equal(evidenceId, body.GetProperty("evidenceId").GetGuid());
        await using var db = new Station01DbContext(host.Host.Services.GetRequiredService<DbContextOptions<Station01DbContext>>());
        var row = await db.PlcCommunicationEvidence.AsNoTracking().SingleAsync();
        Assert.Equal(row.RawPayloadJson, body.GetProperty("rawPayloadJson").GetString());
        Assert.Equal(row.PayloadDigest, body.GetProperty("payloadDigest").GetString());
        using var conditional = new HttpRequestMessage(HttpMethod.Get, path);
        conditional.Headers.IfNoneMatch.Add(response.Headers.ETag!);
        Assert.Equal(HttpStatusCode.NotModified, (await host.Client.SendAsync(conditional)).StatusCode);
        using var anonymous = new HttpClient(host.Host.Server.CreateHandler()) { BaseAddress = host.Client.BaseAddress };
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync($"/api/v1/station01/diagnostics/communication/{Guid.NewGuid():D}")).StatusCode);
        Assert.Equal(0, host.Plc.StartCommands);
        Assert.Empty(await db.Writes.ToListAsync());
    }
}
