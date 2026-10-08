using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Application.Station01;
using Gaode.Domain.Station01;

namespace Gaode.Contracts.Tests.Support;

// Declared persisted inputs for common-component tests; not a storage or device proof.
internal sealed class SemanticHandoffInputs : ITraceQuery
{
    public PublicPreparationHandoffV2 Handoff { get; }
    public RecipeBindingReceipt Receipt { get; }
    public TrayObservation Observation { get; }
    public List<PersistedWrite> Writes { get; } = [];
    public SemanticHandoffInputs(PublicPreparationHandoffV2 handoff, RecipeRunPlan plan, long epoch = 1)
    {
        var run = handoff.Identity.RunId;
        var capture = Guid.NewGuid(); var call = Guid.NewGuid(); var media = Guid.NewGuid();
        var sourceWrite = Guid.NewGuid(); var intentWrite = Guid.NewGuid();
        var observationWrite = Guid.NewGuid();
        var observationMedia = Guid.NewGuid();
        Observation = new(Guid.NewGuid(), run, handoff.Identity.TrayId, Guid.NewGuid(), Guid.NewGuid(), handoff.PersistedAt,
            TrayObservationPurpose.InitialPreparation, 1, null,
            plan.Steps.Where(s => s.PhysicalSlotIndex is not null).Select(s => s.PhysicalSlotIndex!.Value).Distinct()
                .Select(i => new TraySlotObservation(i, TrayPresence.Present, TrayPose.Normal)).ToArray(),
            new(12, 24, "mm", "test-frame", "Component-only observation"),
            new(handoff.Source, "DeclaredUnitAlgorithm/1", "DeclaredUnit"), ["Component-only media"]);
        Handoff = handoff with { FMediaReferences = [$"media://{media:D}"],
            ThreeDMediaReferences = [$"media://{observationMedia:D}"],
            ResultReferences = [..handoff.ResultReferences, $"algorithm-call://{call:D}", $"algorithm-call://{Observation.CallId:D}"],
            EvidenceReferences = [..handoff.EvidenceReferences, $"write://{sourceWrite:D}", $"write://{observationWrite:D}"],
            CommittedRevision = 10, PayloadDigest = "" };
        Handoff = Handoff with { PayloadDigest = PublicPreparationHandoffV2.ComputePayloadDigest(Handoff) };
        Receipt = SemanticRecipeReceiptFixture.For(Handoff, epoch);
        Receipt = Receipt with { RecipeId = plan.RecipeId, RecipeVersion = plan.RecipeVersion, DefinitionDigest = plan.DefinitionDigest,
            IntentCommit = new(intentWrite, Receipt.Correlation,
            ActualCommitState.Committed, ReceiptValidity.ValidCurrent, 5, handoff.PersistedAt, 1, null)
            { RecordKind = BusinessCommitRecordKind.RunWrite, SavePurpose = "BindingIntent" } };
        var frozen = new FrozenExecutionInputs(FrozenExecutionInputs.CurrentSchema, run, handoff.Identity.TrayId,
            handoff.PlanRevision, plan, new Dictionary<string, BoundCapability>(),
            new("unit-cost", "1", "Test", "unit", "unit/1", "unit-digest", 5000, 10000, 5000, 24100, 23000) { CaptureWaitMs = 8000, AlgorithmWaitMs = 15000, InputReleaseWaitMs = 2000 }, "");
        frozen = frozen with { SemanticDigest = frozen.ComputeDigest() };
        Add(Guid.NewGuid(), 1, WriteKind.Media, new MediaRef(observationMedia, run, Observation.CaptureId,
            "Image", "unit/3d.png", 1, "png", "DeclaredUnit", "none", "1", "FileCompleted"));
        Add(observationWrite, 2, WriteKind.AlgorithmFact, new AlgorithmFactPayload(Observation.CallId, AlgorithmState.Success,
            true, JsonSerializer.Serialize(Observation), "DeclaredComponentObservation", "Accepted")
            { RunId = run, CaptureId = Observation.CaptureId, Origin = Observation.Source });
        Add(Guid.NewGuid(), 3, WriteKind.Media, new MediaRef(media, run, capture, "Image", "unit/f.png", 1,
            "png", "DeclaredUnit", "none", "1", "FileCompleted"));
        Add(sourceWrite, 4, WriteKind.AlgorithmFact, new AlgorithmFactPayload(call, AlgorithmState.Success,
            true, "[]", "DeclaredUnit", "Accepted")
            { RunId = run, CaptureId = capture, Origin = new(handoff.Source, "DeclaredUnitAlgorithm/1", "DeclaredUnit") });
        Add(intentWrite, 5, WriteKind.ActionIntent, new { kind = "RecipePlanAndBindingIntent", frozenExecutionInputs = frozen });
        void Add(Guid id, long revision, WriteKind kind, object payload)
        {
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            Writes.Add(new(id, run, revision, kind, json, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))), CommitState.Committed));
        }
    }
    public Task<StartReceipt?> GetStartReceiptAsync(string subject, string requestId, CancellationToken token) =>
        throw new NotSupportedException("This handoff fixture does not contain persisted start requests.");
    public Task<PersistedRun?> GetRunAsync(Guid runId, CancellationToken token) => Task.FromResult<PersistedRun?>(null);
    public Task<IReadOnlyList<PersistedWrite>> GetWritesAsync(Guid runId, CancellationToken token) =>
        Task.FromResult<IReadOnlyList<PersistedWrite>>(Writes.Where(w => w.RunId == runId).ToArray());
    public Task<PersistedWrite?> GetWriteAsync(Guid writeId, CancellationToken token) => Task.FromResult(Writes.SingleOrDefault(w => w.WriteId == writeId));
    public Task<IReadOnlyList<PersistedRun>> GetUnfinishedRunsAsync(CancellationToken token) => Task.FromResult<IReadOnlyList<PersistedRun>>([]);
}
