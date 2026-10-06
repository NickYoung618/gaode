using Gaode.Domain.Station01;
using Microsoft.EntityFrameworkCore;

namespace Gaode.Infrastructure.Persistence;

public sealed class Station01DbContext(DbContextOptions<Station01DbContext> options) : DbContext(options)
{
    public DbSet<RunEntity> Runs => Set<RunEntity>();
    public DbSet<CommandEntity> Commands => Set<CommandEntity>();
    public DbSet<WriteEntity> Writes => Set<WriteEntity>();
    public DbSet<OperationEntity> Operations => Set<OperationEntity>();
    public DbSet<AlgorithmCallEntity> AlgorithmCalls => Set<AlgorithmCallEntity>();
    public DbSet<MediaEntity> Media => Set<MediaEntity>();
    public DbSet<HandoffEntity> Handoffs => Set<HandoffEntity>();
    public DbSet<PublicPreparationHandoffV2Entity> PublicPreparationHandoffsV2 => Set<PublicPreparationHandoffV2Entity>();
    public DbSet<StoreManifestEntity> Manifests => Set<StoreManifestEntity>();
    public DbSet<StageEventEntity> StageEvents => Set<StageEventEntity>();
    public DbSet<StageProjectionEntity> StageProjections => Set<StageProjectionEntity>();
    public DbSet<StageIdempotencyEntity> StageIdempotencies => Set<StageIdempotencyEntity>();
    public DbSet<ComponentEvidenceMatrixEntity> ComponentEvidenceMatrices => Set<ComponentEvidenceMatrixEntity>();
    public DbSet<ControlledRecoveryDecisionEntity> ControlledRecoveryDecisions => Set<ControlledRecoveryDecisionEntity>();
    public DbSet<WholeTrayCompletionEntity> WholeTrayCompletions => Set<WholeTrayCompletionEntity>();
    internal DbSet<CommunicationEvidenceEntity> PlcCommunicationEvidence => Set<CommunicationEvidenceEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) => ConfigureModel(modelBuilder);
    internal static void ConfigureModel(ModelBuilder modelBuilder) => Station01EntityConfigurations.Configure(modelBuilder);
}

internal sealed class CommunicationEvidenceEntity
{
    public Guid EvidenceId { get; set; }
    public Guid StoreId { get; set; }
    public Guid ObservationId { get; set; }
    public Guid? RunId { get; set; }
    public Guid? OperationId { get; set; }
    public Guid? ActionId { get; set; }
    public long ConnectionEpoch { get; set; }
    public DateTimeOffset ObservedStartUtc { get; set; }
    public DateTimeOffset ObservedEndUtc { get; set; }
    public DateTimeOffset PersistedUtc { get; set; }
    public string PayloadSchema { get; set; } = "";
    public string PayloadDigest { get; set; } = "";
    public string RawPayloadJson { get; set; } = "";
}

public sealed class RunEntity
{
    public Guid RunId { get; set; }
    public string RequestId { get; set; } = "";
    public string SubjectId { get; set; } = "";
    public string ContextJson { get; set; } = "{}";
    public RunState State { get; set; } = RunState.Created;
    public TerminalOutcome Terminal { get; set; } = TerminalOutcome.None;
    public long Revision { get; set; }
    public long? TerminalRevision { get; set; }
    public bool CancelRequested { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
}

public sealed class CommandEntity
{
    public Guid CommandId { get; set; }
    public Guid RunId { get; set; }
    public string SubjectId { get; set; } = "";
    public string RequestId { get; set; } = "";
    public string Kind { get; set; } = "Start";
    public string Scope { get; set; } = "Station01";
    public string PayloadDigest { get; set; } = "";
    public string ReceiptState { get; set; } = "Pending";
    public bool? Applied { get; set; }
}

public sealed class WriteEntity
{
    public Guid WriteId { get; set; }
    public Guid RunId { get; set; }
    public long Revision { get; set; }
    public string Kind { get; set; } = "";
    public string PayloadJson { get; set; } = "";
    public string PayloadDigest { get; set; } = "";
    public DateTimeOffset CommittedUtc { get; set; }
}

public sealed class OperationEntity
{
    public Guid OperationId { get; set; }
    public Guid RunId { get; set; }
    public string Kind { get; set; } = "";
    public int Attempt { get; set; } = 1;
    public string State { get; set; } = "IntentCommitted";
    public Guid IntentWriteId { get; set; }
    public string EvidenceJson { get; set; } = "";
}

public sealed class AlgorithmCallEntity
{
    public Guid CallId { get; set; }
    public Guid RunId { get; set; }
    public Guid CaptureId { get; set; }
    public Guid OperationId { get; set; }
    public int Attempt { get; set; }
    public Guid IntentWriteId { get; set; }
    public string InputMediaIdsJson { get; set; } = "[]";
    public string PublicVersion { get; set; } = "";
    public string ScopeVersion { get; set; } = "";
    public string ParametersVersion { get; set; } = "";
    public string CapabilityId { get; set; } = "";
    public string CapabilityVersion { get; set; } = "";
    public string ExpectedComponentVersion { get; set; } = "";
    public Guid SessionId { get; set; }
    public string ClockId { get; set; } = "";
    public long StartTick { get; set; }
    public long DueTick { get; set; }
    public int BudgetMs { get; set; }
    public string InvocationBasis { get; set; } = "";
    public string DispatchEvidence { get; set; } = "Unknown";
    public string TechnicalState { get; set; } = "Queued";
}

public sealed class MediaEntity
{
    public Guid MediaId { get; set; }
    public Guid RunId { get; set; }
    public Guid CaptureId { get; set; }
    public string RelativeKey { get; set; } = "";
    public long ByteLength { get; set; }
    public string Format { get; set; } = "";
    public string Source { get; set; } = "";
    public string State { get; set; } = "Ready";
}

public sealed class HandoffEntity
{
    public Guid HandoffId { get; set; }
    public Guid RunId { get; set; }
    public string PayloadJson { get; set; } = "";
    public TerminalOutcome Terminal { get; set; }
    public long Revision { get; set; }
    public Guid WriteId { get; set; }
}

public sealed class PublicPreparationHandoffV2Entity
{
    public Guid HandoffId { get; set; }
    public Guid RunId { get; set; }
    public Guid TrayId { get; set; }
    public Guid WriteId { get; set; }
    public long Revision { get; set; }
    public string PayloadJson { get; set; } = "";
    public string PayloadDigest { get; set; } = "";
    public DateTimeOffset PersistedUtc { get; set; }
}

public sealed class StoreManifestEntity
{
    public Guid StoreId { get; set; }
    public string SchemaVersion { get; set; } = "s01-store/3";
    public string Profile { get; set; } = "Test";
    public Guid PrepareOperationId { get; set; }
    public DateTimeOffset PreparedUtc { get; set; }
}

public sealed class StageEventEntity
{
    public Guid EventId { get; set; }
    public Guid RunId { get; set; }
    public Guid TrayId { get; set; }
    public string StationId { get; set; } = "";
    public string LineId { get; set; } = "";
    public string Stage { get; set; } = "";
    public Guid OperationId { get; set; }
    public int Attempt { get; set; }
    public string PlanRevision { get; set; } = "";
    public DateTimeOffset? StageStartedUtc { get; set; }
    public DateTimeOffset? StageDeadlineUtc { get; set; }
    public long ConnectionEpoch { get; set; }
    public string EventType { get; set; } = "";
    public DateTimeOffset OccurredUtc { get; set; }
    public DateTimeOffset PersistedUtc { get; set; }
    public string Source { get; set; } = "";
    public string Quality { get; set; } = "";
    public string? ErrorCode { get; set; }
    public string PayloadDigest { get; set; } = "";
    public string PayloadJson { get; set; } = "{}";
    public string IdempotencyKey { get; set; } = "";
    public long Sequence { get; set; }
    public DateTimeOffset RetainUntilUtc { get; set; }
}

public sealed class StageProjectionEntity
{
    public Guid ProjectionId { get; set; }
    public Guid RunId { get; set; }
    public Guid TrayId { get; set; }
    public string StationId { get; set; } = "";
    public string LineId { get; set; } = "";
    public string Stage { get; set; } = "";
    public long Revision { get; set; }
    public string Status { get; set; } = "";
    public Guid? CurrentOperationId { get; set; }
    public long ConnectionEpoch { get; set; }
    public bool DeviceHeld { get; set; }
    public bool NeedsManualReview { get; set; }
    public Guid LastEventId { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public DateTimeOffset RetainUntilUtc { get; set; }
}

public sealed class StageIdempotencyEntity
{
    public string IdempotencyKey { get; set; } = "";
    public string PayloadDigest { get; set; } = "";
    public Guid RunId { get; set; }
    public Guid TrayId { get; set; }
    public string Stage { get; set; } = "";
    public Guid OperationId { get; set; }
    public Guid EventId { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
}

public sealed class ComponentEvidenceMatrixEntity
{
    public Guid MatrixId { get; set; }
    public string SchemaVersion { get; set; } = "station01-component-source-matrix/1.0";
    public Guid RunId { get; set; }
    public Guid TrayId { get; set; }
    public string PlanRevision { get; set; } = "";
    public string Milestone { get; set; } = "";
    public string ComponentsJson { get; set; } = "[]";
    public string MatrixDigest { get; set; } = "";
    public string Scope { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset RetainUntilUtc { get; set; }
}

public sealed class ControlledRecoveryDecisionEntity
{
    public Guid DecisionId { get; set; }
    public string RequestId { get; set; } = "";
    public long ExpectedRevision { get; set; }
    public string OriginalTaskId { get; set; } = "";
    public Guid RunId { get; set; }
    public Guid TrayId { get; set; }
    public Guid? OriginalOperationId { get; set; }
    public string OriginalStage { get; set; } = "";
    public string Decision { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string ActorRole { get; set; } = "";
    public DateTimeOffset DecidedAtUtc { get; set; }
    public string Reason { get; set; } = "";
    public string EvidenceReferencesJson { get; set; } = "[]";
    public string PayloadDigest { get; set; } = "";
    public DateTimeOffset RetainUntilUtc { get; set; }
}

public sealed class WholeTrayCompletionEntity
{
    public Guid WholeTrayCompletionId { get; set; }
    public Guid RunId { get; set; }
    public Guid TrayId { get; set; }
    public string PlanRevision { get; set; } = "";
    public Guid? DetectionCompletedEventId { get; set; }
    public Guid? SortingCompletedEventId { get; set; }
    public Guid UnloadPreparationCompletedEventId { get; set; }
    public Guid SourceMatrixId { get; set; }
    public long PersistedRevision { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset RetainUntilUtc { get; set; }
}
