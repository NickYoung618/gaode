using System.Text.Json;
using Gaode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Gaode.Integration.Tests.Support;

// Actual persisted readback only; business assertions remain in the other file.
public static partial class Station01Evidence
{
    public static async Task<string> SaveNormalAsync(string root, Guid runId,
        object counts, CancellationToken cancellationToken = default)
    {
        var options = new DbContextOptionsBuilder<Station01DbContext>()
            .UseSqlite(StoreCompatibilityProbe.ReadOnlyConnectionString(root)).Options;
        await using var db = new Station01DbContext(options);
        var run = await db.Runs.AsNoTracking().SingleAsync(x => x.RunId == runId, cancellationToken);
        var writes = await db.Writes.AsNoTracking().Where(x => x.RunId == runId)
            .OrderBy(x => x.Revision).Select(x => new { x.Revision, x.Kind, x.WriteId })
            .ToArrayAsync(cancellationToken);
        var media = await db.Media.AsNoTracking().Where(x => x.RunId == runId)
            .Select(x => new { x.MediaId, x.RelativeKey, x.ByteLength, x.State })
            .ToArrayAsync(cancellationToken);
        var calls = await db.AlgorithmCalls.AsNoTracking().Where(x => x.RunId == runId)
            .Select(x => new { x.CallId, x.CaptureId, x.IntentWriteId, x.TechnicalState,
                x.DispatchEvidence, x.StartTick, x.DueTick })
            .ToArrayAsync(cancellationToken);
        var handoff = await db.PublicPreparationHandoffsV2.AsNoTracking()
            .SingleAsync(x => x.RunId == runId, cancellationToken);
        var evidence = new { generatedUtc = DateTimeOffset.UtcNow, runId, run.State, run.Terminal,
            run.Revision, run.TerminalRevision, handoffRevision = handoff.Revision, handoff.WriteId,
            writes, media, calls, counts,
            publicConfig = "s01-public-dev/1.0.0", budget = "s01-budget-dev/2.0.0",
            simulation = "s01-sim-normal/2.0.0", scope = "Test", source = "actual-SQLite-and-media" };
        var file = Path.Combine(root, "T038-normal-evidence.json");
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(evidence,
            new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        return file;
    }
}
