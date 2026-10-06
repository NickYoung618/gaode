using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Domain.Station01;
using Microsoft.EntityFrameworkCore;

namespace Gaode.Infrastructure.Persistence;

// Finite persisted binding facts and receipt observations. This reader never authorizes an action.
public static class RecipeApplicationHistoryReader
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private sealed record Fact(Guid Id, string RecordKind, long Revision, DateTimeOffset CommittedAtUtc, string Payload);
    private sealed record ObservedReceipt(string BindingId, ActionCorrelation Correlation,
        bool WasCompletedInWindow, IReadOnlyList<RequiredCommitEvidence> RequiredCommits,
        RequiredCommitEvidence? IntentCommit, RequiredCommitEvidence? RequiredEvidenceCommit, long ReceivedTick);
    public sealed record BindingHistory(Guid WriteId, DateTimeOffset CommittedAtUtc, RecipeApplicationView View, string RecordNature);

    public static async Task<IReadOnlyList<BindingHistory>> ReadAsync(Station01DbContext db, Guid runId, CancellationToken token)
    {
        var writes = await db.Writes.AsNoTracking().Where(w => w.RunId == runId).ToArrayAsync(token);
        var stages = await db.StageEvents.AsNoTracking().Where(w => w.RunId == runId && w.Stage == "RecipeApplication").ToArrayAsync(token);
        var facts = writes.Select(w => new Fact(w.WriteId, "RunWrite", w.Revision, w.CommittedUtc, w.PayloadJson))
            .Concat(stages.Select(w => new Fact(w.EventId, "StageEvent", w.Sequence, w.PersistedUtc, w.PayloadJson))).ToArray();
        var receipts = facts.Where(f => Kind(f.Payload) == "RecipeApplicationReceiptObserved")
            .Select(f => ReadReceipt(Root(f.Payload).GetProperty("receipt")))
            .Where(r => r is not null && r.Correlation.RunId == runId).ToArray();
        var result = new List<BindingHistory>();
        foreach (var bound in facts.Where(f => Kind(f.Payload) == "RecipePlanBound"))
        {
            var saved = Root(bound.Payload);
            var id = saved.TryGetProperty("bindingId", out var b) ? b.ToString() : null;
            if (id is null) continue;
            var registration = Read<RecipeApplicationRegistration>(saved, "registration");
            var source = Read<RecipeApplicationBudgetSource>(saved, "source");
            var intent = Read<RequiredCommitEvidence>(saved, "intentCommit");
            var evidence = Read<DeviceActionEvidence>(saved, "deviceEvidence");
            var receipt = receipts.SingleOrDefault(r => r!.BindingId == id && r.RequiredCommits.Any(c => c.WriteId == bound.Id && c.RecordKind.ToString() == bound.RecordKind));
            // WriteId/table/revision identify the committed row. Its insertion timestamp
            // and the writer's post-COMMIT observation are different real measurements;
            // equality would reject valid receipts. Never infer a receipt from either time.
            bool Matches(RequiredCommitEvidence c) => c.Correlation.RunId == runId && c.CommittedUtc is not null &&
                facts.Any(f => f.Id == c.WriteId && f.RecordKind == c.RecordKind.ToString() && f.Revision == c.PersistedRevision);
            var observed = receipt is { WasCompletedInWindow: true } && receipt.RequiredCommits.All(Matches) &&
                receipt.IntentCommit is { } observedIntent && Matches(observedIntent) ? receipt : null;
            var commits = new List<RecipeCommitProjection>();
            void Add(Fact actual, string purpose, RequiredCommitEvidence? seen)
            {
                commits.Add(new(actual.Id, purpose, "Committed", seen?.Validity.ToString(), actual.CommittedAtUtc,
                    RecipeApplicationProjection.Tick(seen?.ReceivedTick), actual.RecordKind));
            }
            if (intent is not null && facts.SingleOrDefault(f => f.Id == intent.WriteId && f.RecordKind == intent.RecordKind.ToString()) is { } intentRow)
                Add(intentRow, "BindingIntent", observed?.IntentCommit);
            Add(bound, "RecipePlanBound", observed?.RequiredCommits.SingleOrDefault(c => c.WriteId == bound.Id));
            // The committed handoff is a fact even when its success receipt was lost.
            foreach (var handoff in facts.Where(f => f.RecordKind == "RunWrite" &&
                Root(f.Payload).TryGetProperty("recipeBindingReference", out var r) && r.GetString() == "recipe-binding://" + id))
                Add(handoff, "Handoff", observed?.RequiredCommits.SingleOrDefault(c => c.WriteId == handoff.Id));
            var view = new RecipeApplicationView(id, observed is not null ? "Completed" : "AwaitingRequiredBusinessCommits",
                source is null ? null : RecipeApplicationProjection.Budget(source), registration,
                evidence is { Meaning: DeviceCompletionMeaning.DeviceRecipeApplied } ? true : null,
                RecipeApplicationProjection.Tick(observed?.ReceivedTick), commits,
                evidence?.DiagnosticEvidenceReferences ?? []);
            if (evidence is not null) view = await WithCommunicationAsync(db, view, evidence.Correlation, token,
                observed?.RequiredEvidenceCommit ?? Read<RequiredCommitEvidence>(saved, "requiredEvidenceCommit"));
            result.Add(new(bound.Id, bound.CommittedAtUtc, view,
                saved.TryGetProperty("schemaVersion", out var s) && s.GetString() is "device-semantics/1" or "recipe-binding/1" ? "Derived" : "LegacyRecordedClaim"));
        }
        return result;
    }

    public static async Task<RecipeApplicationView> WithCommunicationAsync(Station01DbContext db, RecipeApplicationView view,
        ActionCorrelation correlation, CancellationToken token, RequiredCommitEvidence? observed = null)
    {
        var manifest = await db.Manifests.AsNoTracking().SingleAsync(token);
        var options = new DbContextOptionsBuilder<Station01DbContext>().UseSqlite(db.Database.GetConnectionString()!).Options;
        var reader = new CommunicationEvidenceReader(options, manifest.StoreId);
        var commits = view.RequiredCommits.ToList(); var references = new List<DiagnosticEvidenceReference>();
        foreach (var reference in view.DiagnosticEvidenceReferences.Distinct())
        {
            if (reference.StoreId != manifest.StoreId) continue;
            var record = await reader.ReadAsync(reference.EvidenceId, token);
            if (record is null || record.RunId != correlation.RunId || record.OperationId != correlation.OperationId ||
                record.ActionId != correlation.ActionId || record.ConnectionEpoch != correlation.ConnectionEpoch) continue;
            var batch = JsonSerializer.Deserialize<CommunicationEvidenceBatch>(record.RawPayloadJson, Json)!;
            references.Add(reference);
            // Reading a row proves commit. Receipt time/validity require an actual,
            // previously observed save receipt with this exact stored identity.
            var matching = observed?.WriteId == batch.WriteId && observed.Correlation == correlation &&
                observed.RecordKind == BusinessCommitRecordKind.CommunicationEvidence ? observed : null;
            commits.Add(new(batch.WriteId, "RequiredCommunicationEvidence", "Committed", matching?.Validity.ToString(), record.PersistedAtUtc,
                RecipeApplicationProjection.Tick(matching?.ReceivedTick), "CommunicationEvidence"));
        }
        return view with { RequiredCommits = commits, DiagnosticEvidenceReferences = references };
    }
    private static ObservedReceipt? ReadReceipt(JsonElement root)
    {
        if (root.TryGetProperty("definitionDigest", out _))
        {
            var current = root.Deserialize<RecipeBindingReceipt>(Json);
            return current is null ? null : new(current.BindingId, current.Correlation, current.WasCompletedInWindow,
                current.RequiredCommits, current.IntentCommit, null, current.ReceivedTick);
        }
        var legacy = root.Deserialize<RecipeApplicationReceipt>(Json);
        return legacy is null ? null : new(legacy.BindingId, legacy.Correlation, legacy.WasCompletedInWindow,
            legacy.RequiredCommits, legacy.IntentCommit, legacy.RequiredEvidenceCommit, legacy.ReceivedTick);
    }
    private static JsonElement Root(string payload) { using var d = JsonDocument.Parse(payload); return d.RootElement.Clone(); }
    private static string? Kind(string payload) => Root(payload).TryGetProperty("kind", out var k) ? k.GetString() : null;
    private static T? Read<T>(JsonElement root, string field) where T : class => root.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.Object ? value.Deserialize<T>(Json) : null;
}
