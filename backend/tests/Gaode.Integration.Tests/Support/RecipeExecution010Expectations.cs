using System.Text.Json;
using System.Net.Http.Json;
using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Application.Station01;
using Gaode.Domain.Station01;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Gaode.Integration.Tests.Support;

// One joint driver; expected counts/coordinates are declared before execution in the Test input.
// No planner-derived oracle, test-number business branch or protocol assertion here.
public static class RecipeExecution010Expectations
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static async Task VerifyAsync(RecipeExecution010RunHarness driver, Guid runId)
    {
        var expected = driver.Fixture.GetProperty("expected");
        int Count(string name) => expected.GetProperty(name).GetInt32();
        var connection = new SqliteConnectionStringBuilder { DataSource = Path.Combine(driver.StoreRoot, "station01.test.db"),
            Mode = SqliteOpenMode.ReadOnly }.ToString();
        await using var db = new Gaode.Infrastructure.Persistence.Station01DbContext(
            new DbContextOptionsBuilder<Gaode.Infrastructure.Persistence.Station01DbContext>().UseSqlite(connection).Options);
        var writes = await db.Writes.AsNoTracking().Where(w => w.RunId == runId).OrderBy(w => w.Revision).ToArrayAsync();
        var facts = writes.Where(w => w.Kind == "AlgorithmFact").Select(w =>
            (Write: w, Fact: JsonSerializer.Deserialize<AlgorithmFactPayload>(w.PayloadJson, Json)!)).ToArray();
        Assert.Equal(Count("algorithmFactCount"), facts.Length);
        Assert.Equal(facts.Length, facts.Select(f => f.Fact.CallId).Distinct().Count());
        var implementation = expected.GetProperty("workerVersion").GetString();
        Assert.All(facts, f => {
            Assert.Equal(runId, f.Fact.RunId); Assert.NotEqual(Guid.Empty, f.Fact.CaptureId);
            Assert.Equal(AlgorithmState.Success, f.Fact.State); Assert.True(f.Fact.Origin.IsKnown);
            Assert.Equal(ComponentEvidenceSource.Test, f.Fact.Origin.Source); Assert.Equal(implementation, f.Fact.Origin.VersionRef);
        });
        var media = await db.Media.AsNoTracking().Where(m => m.RunId == runId).ToArrayAsync();
        Assert.Equal(Count("totalMediaCount"), media.Length);
        Assert.All(media, m => Assert.Equal("FileCompleted", m.State));
        var captures = writes.Where(w => w.Kind == "CaptureFact").Select(w => JsonSerializer.Deserialize<JsonElement>(w.PayloadJson)).ToArray();
        Assert.Equal(media.Length, captures.Length);
        foreach (var capture in captures)
        {
            var actual = Field(capture, "captureFact").Deserialize<CorrelatedCaptureFact>(Json)!;
            Assert.Equal(runId, actual.RunId); Assert.True(actual.Replayed);
            Assert.Equal(CaptureApplicationState.ConfiguredOnly, actual.ApplicationState); Assert.Null(actual.ActualSettings);
            Assert.Equal(ComponentEvidenceSource.Test, actual.CameraOrigin.Source);
            Assert.Equal("ConfiguredOnly", actual.LightOrigin.Quality);
            Assert.Single(media, m => m.CaptureId == actual.CaptureId && m.MediaId == Field(capture, "mediaId").GetGuid());
        }
        var handoffRow = await db.PublicPreparationHandoffsV2.AsNoTracking().SingleAsync(h => h.RunId == runId);
        var handoff = JsonSerializer.Deserialize<PublicPreparationHandoffV2>(handoffRow.PayloadJson, Json)!;
        Assert.True(handoff.IsComplete); Assert.Equal(handoffRow.PayloadDigest, handoff.PayloadDigest);
        var f = Assert.Single(facts, f => f.Fact.DecodedCode is not null);
        Assert.Equal(driver.Fixture.GetProperty("fCode").GetString(), f.Fact.DecodedCode!.RawCode);
        Assert.Contains($"algorithm-call://{f.Fact.CallId:D}", handoff.ResultReferences);
        Assert.Contains($"write://{f.Write.WriteId:D}", handoff.EvidenceReferences);
        Assert.True(f.Write.Revision < handoff.CommittedRevision);
        Assert.Contains(f.Fact.CaptureId, media.Where(m => handoff.FMediaReferences.Contains($"media://{m.MediaId:D}")).Select(m => m.CaptureId));
        var firstMedia = Assert.Single(media, m => handoff.ThreeDMediaReferences.Contains($"media://{m.MediaId:D}"));
        var firstPose = Assert.Single(facts, fact => fact.Fact.CaptureId == firstMedia.CaptureId);
        var observed = JsonSerializer.Deserialize<TrayObservation>(firstPose.Fact.RawResultJson, Json)!;
        Assert.True(observed.IsValid); Assert.Equal(runId, observed.RunId); Assert.Equal(handoff.Identity.TrayId, observed.TrayId);
        Assert.Equal(TrayObservationPurpose.InitialPreparation, observed.Purpose);
        Assert.Equal(expected.GetProperty("fLocation").GetProperty("x").GetDouble(), observed.FLocation!.X);
        Assert.Equal(expected.GetProperty("fLocation").GetProperty("y").GetDouble(), observed.FLocation.Y);
        Assert.NotEqual(firstMedia.CaptureId, f.Fact.CaptureId);
        var intent = Assert.Single(writes, w => Kind(w.PayloadJson) == "RecipePlanAndBindingIntent");
        var bound = Assert.Single(writes, w => Kind(w.PayloadJson) == "RecipePlanBound");
        Assert.True(intent.Revision < bound.Revision && bound.Revision <= handoff.CommittedRevision);
        var frozen = Field(JsonSerializer.Deserialize<JsonElement>(intent.PayloadJson), "frozenExecutionInputs").Deserialize<FrozenExecutionInputs>(Json)!;
        Assert.True(frozen.IsValid); Assert.Equal("Test", frozen.Plan.Approval.Purpose);
        Assert.Equal(handoff.PlanRevision, frozen.PlanRevision);
        Assert.Equal(driver.Fixture.GetProperty("fCode").GetString(), frozen.Plan.FCode);
        Assert.All(frozen.Plan.ExecutionPositions.Values, position => Assert.Single(position.PhysicalEntity.Coordinates.Select(c => c.PhysicalSlotIndex).Distinct()));
        var stages = (await db.StageEvents.AsNoTracking().Where(e => e.RunId == runId).ToArrayAsync()).OrderBy(e => e.PersistedUtc).ToArray();
        var images = stages.Where(e => Kind(e.PayloadJson) == "DetectionImageCommitted").Select(e => JsonSerializer.Deserialize<JsonElement>(e.PayloadJson)).ToArray();
        Assert.Equal(Count("productMediaCount"), images.Length);
        Assert.Equal(expected.GetProperty("cameraOrder").EnumerateArray().Select(x => x.GetString()), images.Select(i => Field(i, "camera").GetString()));
        Assert.Equal(expected.GetProperty("cameraDispositions").EnumerateArray().Select(x => x.GetString()),
            images.Select(i => Field(i, "detectionDisposition").GetString()));
        var movement = writes.Where(w => Kind(w.PayloadJson) == "DetectionMoveConfirmed")
            .Select(w => JsonSerializer.Deserialize<JsonElement>(w.PayloadJson)).ToArray();
        var targets = expected.GetProperty("detectionTargets").EnumerateArray().ToArray();
        Assert.Equal(targets.Length, movement.Length);
        for (var index = 0; index < targets.Length; index++)
        {
            var point = Field(movement[index], "target");
            foreach (var axis in new[] { "x", "y", "z" })
                Assert.Equal(targets[index].GetProperty(axis).GetDouble(), Field(point, axis).GetDouble());
            var evidence = Field(movement[index], "positionEvidence").Deserialize<DeviceActionEvidence>(Json)!;
            Assert.True(evidence.IsCorrelated); Assert.Equal(runId, evidence.Correlation.RunId);
            Assert.True(Assert.Single(evidence.Positions).Matched);
        }
        var fusions = stages.Where(e => Kind(e.PayloadJson) == "FaceFusionCommitted").Select(e => JsonSerializer.Deserialize<JsonElement>(e.PayloadJson)).ToArray();
        Assert.Equal(Count("productMediaCount") / 2, fusions.Length);
        Assert.Equal(expected.GetProperty("faceDispositions").EnumerateArray().Select(x => x.GetString()),
            fusions.Select(fusion => Field(fusion, "disposition").GetString()));
        Assert.Equal(Count("flipCount"), writes.Count(w => Kind(w.PayloadJson) == "FlipPickCompleted"));
        Assert.Equal(Count("putBackCount"), writes.Count(w => Kind(w.PayloadJson) == "FlipPutBackCompleted"));
        Assert.Equal(Count("poseObservationCount") - 1, stages.Count(e => Kind(e.PayloadJson) == "TrayObservationCommitted"));
        var completion = await db.WholeTrayCompletions.AsNoTracking().SingleAsync(c => c.RunId == runId);
        Assert.All(stages, e => Assert.Equal(completion.TrayId, e.TrayId));
        Assert.All(stages.Where(e => !string.IsNullOrWhiteSpace(e.PlanRevision)), e => Assert.Equal(completion.PlanRevision, e.PlanRevision));
        foreach (var id in new[] { completion.DetectionCompletedEventId, completion.SortingCompletedEventId, completion.UnloadPreparationCompletedEventId })
            Assert.Contains(stages, e => e.EventId == id && e.EventType == "Completed");
        var sorting = Assert.Single(stages, e => e.EventId == completion.SortingCompletedEventId);
        var unloadStart = Assert.Single(stages, e => e.Stage == "UnloadPreparation" && e.EventType == "Started");
        Assert.True(sorting.PersistedUtc <= unloadStart.OccurredUtc);
        Assert.Single(stages, e => e.EventType == "ManualRemovalAllowed");
        Assert.DoesNotContain(stages, e => e.EventType is "UnlockRequested" or "ObservedUnlocked");
        Assert.Single(stages, e => e.EventType == "ManualTrayRemovalConfirmed");
        Assert.Single(stages, e => e.EventType == "FinalUnloadCompleted");
        Assert.Equal(2, await db.ComponentEvidenceMatrices.CountAsync(m => m.RunId == runId));
        var savedRun = await db.Runs.AsNoTracking().SingleAsync(r => r.RunId == runId);
        Assert.Equal(RunState.Completed, savedRun.State); Assert.Equal(TerminalOutcome.Completed, savedRun.Terminal);
        var displayed = await driver.Client.GetFromJsonAsync<JsonElement>($"/api/v1/station01/runs/{runId:D}");
        Assert.Equal("Completed", displayed.GetProperty("sortingState").GetString());
        await driver.SaveAsync("sorting-state-reconciled.json", new { runId, sortingState = "Completed",
            committedSortingEvent = sorting.EventId, source = "Actual HTTP matched committed sorting completion" });
        var audit = (await File.ReadAllLinesAsync(Path.Combine(driver.StoreRoot, "media-root", "worker-protocol.jsonl")))
            .Select(line => JsonSerializer.Deserialize<JsonElement>(line)).ToArray();
        var results = audit.Where(r => r.GetProperty("event").GetString() == "Result").ToArray();
        Assert.Equal(facts.Length, results.Length);
        var inputReadCount = Count("totalMediaCount") + Count("productMediaCount");
        Assert.Equal(inputReadCount, audit.Count(r => r.GetProperty("event").GetString() == "InputReleased"));
        Assert.Equal(inputReadCount, audit.Count(r => r.GetProperty("event").GetString() == "MediaRead"));
        foreach (var fact in facts) Assert.Single(results, r => r.GetProperty("callId").GetGuid() == fact.Fact.CallId &&
            r.GetProperty("workerSessionId").GetGuid() == fact.Fact.WorkerSessionId);
        Assert.All(results, r => Assert.Equal(implementation, r.GetProperty("implementation").GetString()));
        if(frozen.Plan.InspectionKind==RecipeInspectionKind.SpecialRotation)
        {
            Assert.Equal(2,frozen.Plan.OriginalSlots!.Count);
            Assert.Equal(8,images.Length);Assert.Equal(8,images.Select(i=>Field(i,"captureId").GetGuid()).Distinct().Count());
            Assert.Equal(2,frozen.Plan.RotationLoadingGripperId);Assert.Equal(1,frozen.Plan.SortingGripperId);
            var declaredSettings=expected.GetProperty("captureParameters").EnumerateArray().ToArray();
            var captureSteps=frozen.Plan.Steps.Where(step=>step.Kind==RecipeStepKind.Capture).ToArray();
            Assert.Equal(images.Length,declaredSettings.Length);Assert.Equal(images.Length,captureSteps.Length);
            for(var index=0;index<images.Length;index++)
            {
                var settings=Field(images[index],"requestedCaptureSettings");
                var original=JsonSerializer.SerializeToElement(frozen.Plan.CaptureProfiles[captureSteps[index].CaptureProfile!].Settings,Json);
                foreach(var parameter in new[]{"exposureUs","gain","brightnessPercent"})
                {
                    Assert.Equal(declaredSettings[index].GetProperty(parameter).GetDouble(),Field(settings,parameter).GetDouble());
                    Assert.Equal(Field(original,parameter).GetDouble(),Field(settings,parameter).GetDouble());
                }
            }
            var occupied=stages.Where(e=>Kind(e.PayloadJson)=="SortingAssignmentOccupied").ToArray();
            var cycles=stages.Where(e=>Kind(e.PayloadJson)=="UnitCycleCompleted").ToArray();
            Assert.Equal(2,cycles.Length);Assert.Equal(4,occupied.Length);
            DateTimeOffset? priorReturnTime=null;
            foreach(var origin in frozen.Plan.OriginalSlots.Values.OrderBy(o=>o.Row).ThenBy(o=>o.Column))
            {
                var byObject=images.Where(i=>Field(i,"objectId").GetString()==origin.EntityId).ToArray();
                Assert.Equal(new[]{"stage:1:A","stage:1:B","stage:2:A","stage:2:B"},byObject.Select(i=>Field(i,"stageId").GetString()+":"+Field(i,"camera").GetString()));
                Assert.Equal(4,byObject.Select(i=>Field(Field(i,"requestedCaptureSettings"),"exposureUs").GetDouble()).Distinct().Count());
                var actual=occupied.Where(e=>Field(Field(JsonSerializer.Deserialize<JsonElement>(e.PayloadJson),"assignment"),"objectId").GetString()==origin.EntityId).ToArray();
                Assert.Equal(2,actual.Length);
                var loaded=Assert.Single(actual,e=>Field(JsonSerializer.Deserialize<JsonElement>(e.PayloadJson),"transferPurpose").GetString()=="RotationLoading");
                var returned=Assert.Single(actual,e=>Field(JsonSerializer.Deserialize<JsonElement>(e.PayloadJson),"transferPurpose").GetString()=="ReturnToOrigin");
                var returnedBody=JsonSerializer.Deserialize<JsonElement>(returned.PayloadJson);
                var proof=Field(returnedBody,"evidence").Deserialize<DeviceActionEvidence>(Json)!;
                Assert.True(proof.SafeReached?.Matched);Assert.Equal(DeviceCompletionMeaning.MaterialTransferred,proof.Meaning);
                Assert.Contains(proof.Positions,p=>p.Target==frozen.Plan.ExecutionPositions[origin.SlotId].PhysicalEntity.OriginPutBack!.Point&&p.Matched);
                Assert.True(loaded.PersistedUtc<returned.PersistedUtc);
                if(priorReturnTime is not null)Assert.True(priorReturnTime.Value<loaded.OccurredUtc,"Next loading must follow previous actual placed/safe commit");
                priorReturnTime=returned.PersistedUtc;
                Assert.Contains(cycles,c=>Field(Field(JsonSerializer.Deserialize<JsonElement>(c.PayloadJson),"origin"),"cellId").GetString()==origin.CellId&&returned.PersistedUtc<=c.PersistedUtc);
            }
            Assert.True(cycles.Max(c=>c.PersistedUtc)<=unloadStart.OccurredUtc);
            Assert.Equal(4,stages.Count(e=>Kind(e.PayloadJson)=="SortingAssignmentInTransit"));
            Assert.Equal(4,writes.Count(w=>Kind(w.PayloadJson)=="RotationReached"));
            var actualQuery=await driver.Client.GetFromJsonAsync<JsonElement>($"/api/v1/station01/runs/{runId:D}");
            Assert.Equal(4,actualQuery.GetProperty("results").EnumerateArray().Count(r=>r.GetProperty("kind").GetString()=="Face"));
            await driver.SaveAsync("special-origin-stage-proof.json",new {runId,origins=frozen.Plan.OriginalSlots,unitCycles=cycles.Select(c=>c.EventId),
                occupied=occupied.Select(c=>new{c.EventId,c.OperationId,c.OccurredUtc,c.PersistedUtc,c.PayloadJson}),
                captureRequests=images.Select(i=>new { captureId=Field(i,"captureId").GetGuid(),settings=Field(i,"requestedCaptureSettings"),stageId=Field(i,"stageId").GetString(),objectId=Field(i,"objectId").GetString() }),captureIds=images.Select(i=>Field(i,"captureId").GetGuid()),stageIds=images.Select(i=>Field(i,"stageId").GetString()),hardwareApplied="NotVerified"});
        }
        else if(frozen.Plan.InspectionKind==RecipeInspectionKind.Ordinary&&expected.GetProperty("disposition").GetString()=="OK")
        {
            Assert.DoesNotContain(stages,e=>Kind(e.PayloadJson)=="SortingAssignmentInTransit"||Kind(e.PayloadJson)=="SortingAssignmentOccupied");
            Assert.Contains(stages,e=>Kind(e.PayloadJson)=="NoAdditionalSortingRequired");
            await driver.SaveAsync("ordinary-ok-retention.json",new{runId,extraSortingMotion=false,source="Actual committed facts; detection movements retained"});
        }
        await driver.SaveAsync("obligations.json", new { runId, evidenceLevel = "FullRun", implementation,
            source = "011 declared Test input arithmetic; no production approval", algorithmFacts = facts.Length, mediaCount = media.Length,
            handoff.WriteId, handoff.CommittedRevision, frozen.PlanRevision, softwareObligationsComplete = true,
            jointAcceptance = "Awaiting012PageEvidence",
            pageEvidence = "SeparatelyRequiredFrom012", saveSnapshotEvidence = "binding-isolation.json",
            normalRestartEvidence = "normal-restart-reread.json" });
    }
    private static JsonElement Field(JsonElement value, string name) => Assert.Single(value.EnumerateObject(), p =>
        string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)).Value;
    private static string? Kind(string json)
    {
        var value = JsonSerializer.Deserialize<JsonElement>(json);
        return value.ValueKind == JsonValueKind.Object && value.TryGetProperty("kind", out var kind) ? kind.GetString() : null;
    }
}
