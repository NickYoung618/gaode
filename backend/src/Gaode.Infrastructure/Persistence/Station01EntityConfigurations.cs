using Gaode.Domain.Station01;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Gaode.Infrastructure.Persistence;

internal static class Station01EntityConfigurations
{
    public static void Configure(ModelBuilder m)
    {
        Properties<RunEntity>(m, "RunId", "RequestId", "SubjectId", "ContextJson", "State", "Terminal",
            "Revision", "TerminalRevision", "CancelRequested", "CreatedUtc");
        Properties<CommandEntity>(m, "CommandId", "RunId", "SubjectId", "RequestId", "Kind", "Scope",
            "PayloadDigest", "ReceiptState", "Applied");
        Properties<WriteEntity>(m, "WriteId", "RunId", "Revision", "Kind", "PayloadJson",
            "PayloadDigest", "CommittedUtc");
        Properties<OperationEntity>(m, "OperationId", "RunId", "Kind", "Attempt", "State",
            "IntentWriteId", "EvidenceJson");
        Properties<AlgorithmCallEntity>(m, "CallId", "RunId", "CaptureId", "OperationId", "Attempt",
            "IntentWriteId", "InputMediaIdsJson", "PublicVersion", "ScopeVersion", "ParametersVersion",
            "CapabilityId", "CapabilityVersion", "ExpectedComponentVersion", "SessionId", "ClockId",
            "StartTick", "DueTick", "BudgetMs", "InvocationBasis", "DispatchEvidence", "TechnicalState");
        Properties<MediaEntity>(m, "MediaId", "RunId", "CaptureId", "RelativeKey", "ByteLength",
            "Format", "Source", "State");
        Properties<HandoffEntity>(m, "HandoffId", "RunId", "PayloadJson", "Terminal", "Revision", "WriteId");
        Properties<PublicPreparationHandoffV2Entity>(m, "HandoffId", "RunId", "TrayId", "WriteId",
            "Revision", "PayloadJson", "PayloadDigest", "PersistedUtc");
        Properties<StoreManifestEntity>(m, "StoreId", "SchemaVersion", "Profile", "PrepareOperationId", "PreparedUtc");
        Properties<StageEventEntity>(m, "EventId", "RunId", "TrayId", "StationId", "LineId", "Stage",
            "OperationId", "Attempt", "PlanRevision", "StageStartedUtc", "StageDeadlineUtc",
            "ConnectionEpoch", "EventType", "OccurredUtc", "PersistedUtc", "Source",
            "Quality", "ErrorCode", "PayloadDigest", "PayloadJson", "IdempotencyKey", "Sequence", "RetainUntilUtc");
        Properties<StageProjectionEntity>(m, "ProjectionId", "RunId", "TrayId", "StationId", "LineId", "Stage",
            "Revision", "Status", "CurrentOperationId", "ConnectionEpoch", "DeviceHeld", "NeedsManualReview",
            "LastEventId", "UpdatedUtc", "RetainUntilUtc");
        Properties<StageIdempotencyEntity>(m, "IdempotencyKey", "PayloadDigest", "RunId", "TrayId", "Stage",
            "OperationId", "EventId", "CreatedUtc");
        Properties<ComponentEvidenceMatrixEntity>(m, "MatrixId", "SchemaVersion", "RunId", "TrayId",
            "PlanRevision", "Milestone", "ComponentsJson", "MatrixDigest", "Scope", "CreatedUtc", "RetainUntilUtc");
        Properties<ControlledRecoveryDecisionEntity>(m, "DecisionId", "RequestId", "ExpectedRevision",
            "OriginalTaskId", "RunId", "TrayId", "OriginalOperationId", "OriginalStage", "Decision",
            "ActorId", "ActorRole", "DecidedAtUtc", "Reason", "EvidenceReferencesJson", "PayloadDigest",
            "RetainUntilUtc");
        Properties<WholeTrayCompletionEntity>(m, "WholeTrayCompletionId", "RunId", "TrayId", "PlanRevision",
            "DetectionCompletedEventId", "SortingCompletedEventId", "UnloadPreparationCompletedEventId",
            "SourceMatrixId", "PersistedRevision", "CreatedUtc", "RetainUntilUtc");
        Properties<CommunicationEvidenceEntity>(m, "EvidenceId", "StoreId", "ObservationId", "RunId", "OperationId", "ActionId",
            "ConnectionEpoch", "ObservedStartUtc", "ObservedEndUtc", "PersistedUtc", "PayloadSchema", "PayloadDigest", "RawPayloadJson");
        m.Entity<CommunicationEvidenceEntity>(b =>
        {
            b.ToTable("PlcCommunicationEvidence");
            b.HasKey(x => x.EvidenceId);
            b.HasIndex(x => new { x.RunId, x.OperationId });
            b.HasIndex(x => new { x.ActionId, x.ConnectionEpoch });
            b.HasIndex(x => x.ObservationId);
            Immutable(b, nameof(CommunicationEvidenceEntity.StoreId), nameof(CommunicationEvidenceEntity.ObservationId),
                nameof(CommunicationEvidenceEntity.RunId), nameof(CommunicationEvidenceEntity.OperationId), nameof(CommunicationEvidenceEntity.ActionId),
                nameof(CommunicationEvidenceEntity.ConnectionEpoch), nameof(CommunicationEvidenceEntity.ObservedStartUtc),
                nameof(CommunicationEvidenceEntity.ObservedEndUtc), nameof(CommunicationEvidenceEntity.PersistedUtc),
                nameof(CommunicationEvidenceEntity.PayloadSchema), nameof(CommunicationEvidenceEntity.PayloadDigest), nameof(CommunicationEvidenceEntity.RawPayloadJson));
        });
        m.Entity<RunEntity>(b =>
        {
            b.ToTable("Runs", table => table.HasCheckConstraint("CK_Runs_TerminalState",
                "(Terminal = 'None' AND State NOT IN ('Completed','CompletedWithExceptions','Cancelled') AND TerminalRevision IS NULL) OR " +
                "(Terminal = 'Completed' AND State = 'Completed' AND TerminalRevision IS NOT NULL) OR " +
                "(Terminal = 'CompletedWithExceptions' AND State = 'CompletedWithExceptions' AND TerminalRevision IS NOT NULL) OR " +
                "(Terminal = 'Cancelled' AND State = 'Cancelled' AND TerminalRevision IS NOT NULL)"));
            b.HasKey(x => x.RunId);
            b.Property(x => x.State).HasConversion<string>().IsRequired();
            b.Property(x => x.Terminal).HasConversion<string>().IsRequired();
            b.Property(x => x.RequestId).IsRequired();
            b.Property(x => x.SubjectId).IsRequired();
            b.Property(x => x.ContextJson).IsRequired();
            b.HasIndex(x => new { x.RunId, x.TerminalRevision, x.Terminal }).IsUnique();
        });
        m.Entity<CommandEntity>(b =>
        {
            b.ToTable("Commands");
            b.HasKey(x => x.CommandId);
            b.HasIndex(x => new { x.SubjectId, x.RequestId, x.Kind, x.Scope }).IsUnique();
            b.HasIndex(x => x.RunId);
        });
        m.Entity<WriteEntity>(b =>
        {
            b.ToTable("Writes");
            b.HasKey(x => x.WriteId);
            b.HasIndex(x => new { x.RunId, x.Revision }).IsUnique();
            b.HasOne<RunEntity>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<OperationEntity>(b =>
        {
            b.ToTable("Operations");
            b.HasKey(x => x.OperationId);
            b.HasIndex(x => new { x.RunId, x.Kind, x.Attempt });
            b.HasOne<RunEntity>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<AlgorithmCallEntity>(b =>
        {
            b.ToTable("AlgorithmCalls");
            b.HasKey(x => x.CallId);
            b.HasIndex(x => new { x.RunId, x.CaptureId, x.Attempt });
            b.HasOne<RunEntity>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<MediaEntity>(b =>
        {
            b.ToTable("Media");
            b.HasKey(x => x.MediaId);
            b.HasIndex(x => new { x.RunId, x.CaptureId });
            b.HasOne<RunEntity>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<HandoffEntity>(b =>
        {
            b.ToTable("Handoffs", table => table.HasCheckConstraint("CK_Handoff_Terminal",
                "Terminal IN ('Completed','CompletedWithExceptions')"));
            b.HasKey(x => x.HandoffId);
            b.HasIndex(x => x.RunId).IsUnique();
            b.Property(x => x.Terminal).HasConversion<string>();
            b.HasOne<RunEntity>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<PublicPreparationHandoffV2Entity>(b =>
        {
            b.ToTable("PublicPreparationHandoffsV2");
            b.HasKey(x => x.HandoffId);
            b.HasIndex(x => new { x.RunId, x.TrayId }).IsUnique();
            b.HasIndex(x => x.WriteId).IsUnique();
            b.HasOne<RunEntity>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<WriteEntity>().WithMany().HasForeignKey(x => x.WriteId).OnDelete(DeleteBehavior.Restrict);
            Immutable(b, nameof(PublicPreparationHandoffV2Entity.RunId), nameof(PublicPreparationHandoffV2Entity.TrayId),
                nameof(PublicPreparationHandoffV2Entity.WriteId), nameof(PublicPreparationHandoffV2Entity.Revision),
                nameof(PublicPreparationHandoffV2Entity.PayloadJson), nameof(PublicPreparationHandoffV2Entity.PayloadDigest));
        });
        m.Entity<StoreManifestEntity>(b => { b.ToTable("Manifests"); b.HasKey(x => x.StoreId); });
        m.Entity<StageEventEntity>(b =>
        {
            b.ToTable("StageEvents"); b.HasKey(x => x.EventId);
            b.Property(x => x.ErrorCode).IsRequired(false);
            b.HasIndex(x => new { x.RunId, x.TrayId, x.Stage, x.Sequence }).IsUnique();
            b.HasIndex(x => x.IdempotencyKey).IsUnique();
            b.HasOne<RunEntity>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<StageProjectionEntity>(b =>
        {
            b.ToTable("StageProjections"); b.HasKey(x => x.ProjectionId);
            b.HasIndex(x => new { x.RunId, x.TrayId, x.Stage }).IsUnique();
            b.HasOne<RunEntity>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<StageIdempotencyEntity>(b =>
        {
            b.ToTable("StageIdempotencies"); b.HasKey(x => x.IdempotencyKey);
            b.HasIndex(x => x.EventId).IsUnique();
            b.HasIndex(x => x.RunId);
            b.HasOne<RunEntity>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
        });
        m.Entity<ComponentEvidenceMatrixEntity>(b =>
        {
            b.ToTable("ComponentEvidenceMatrices", table => table.HasCheckConstraint(
                "CK_ComponentEvidenceMatrices_Retention",
                "julianday(RetainUntilUtc) >= julianday(CreatedUtc, '+7 years')"));
            b.HasKey(x => x.MatrixId);
            b.HasIndex(x => new { x.RunId, x.TrayId, x.Milestone }).IsUnique();
            b.HasIndex(x => x.MatrixDigest).IsUnique();
            b.HasOne<RunEntity>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
            Immutable(b, nameof(ComponentEvidenceMatrixEntity.RunId), nameof(ComponentEvidenceMatrixEntity.TrayId),
                nameof(ComponentEvidenceMatrixEntity.PlanRevision), nameof(ComponentEvidenceMatrixEntity.ComponentsJson),
                nameof(ComponentEvidenceMatrixEntity.MatrixDigest));
        });
        m.Entity<ControlledRecoveryDecisionEntity>(b =>
        {
            b.ToTable("ControlledRecoveryDecisions", table => table.HasCheckConstraint(
                "CK_ControlledRecoveryDecisions_Retention",
                "julianday(RetainUntilUtc) >= julianday(DecidedAtUtc, '+7 years')"));
            b.HasKey(x => x.DecisionId);
            b.HasIndex(x => new { x.RunId, x.RequestId }).IsUnique();
            b.HasIndex(x => new { x.RunId, x.TrayId, x.OriginalTaskId });
            b.HasOne<RunEntity>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
            Immutable(b, nameof(ControlledRecoveryDecisionEntity.RunId), nameof(ControlledRecoveryDecisionEntity.TrayId),
                nameof(ControlledRecoveryDecisionEntity.OriginalTaskId), nameof(ControlledRecoveryDecisionEntity.Decision),
                nameof(ControlledRecoveryDecisionEntity.ActorId), nameof(ControlledRecoveryDecisionEntity.DecidedAtUtc),
                nameof(ControlledRecoveryDecisionEntity.EvidenceReferencesJson));
        });
        m.Entity<WholeTrayCompletionEntity>(b =>
        {
            b.ToTable("WholeTrayCompletions", table => table.HasCheckConstraint(
                "CK_WholeTrayCompletions_Retention",
                "julianday(RetainUntilUtc) >= julianday(CreatedUtc, '+7 years')"));
            b.HasKey(x => x.WholeTrayCompletionId);
            b.HasIndex(x => new { x.RunId, x.TrayId }).IsUnique().HasDatabaseName("IX_WholeTrayCompletions_RunTray");
            b.HasIndex(x => new { x.RunId, x.PersistedRevision }).IsUnique().HasDatabaseName("IX_WholeTrayCompletions_RunRevision");
            b.HasIndex(x => x.DetectionCompletedEventId);
            b.HasIndex(x => x.SortingCompletedEventId);
            b.HasIndex(x => x.UnloadPreparationCompletedEventId);
            b.HasIndex(x => x.SourceMatrixId);
            b.HasOne<RunEntity>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<StageEventEntity>().WithMany().HasForeignKey(x => x.DetectionCompletedEventId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<StageEventEntity>().WithMany().HasForeignKey(x => x.SortingCompletedEventId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<StageEventEntity>().WithMany().HasForeignKey(x => x.UnloadPreparationCompletedEventId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<ComponentEvidenceMatrixEntity>().WithMany().HasForeignKey(x => x.SourceMatrixId).OnDelete(DeleteBehavior.Restrict);
            Immutable(b, nameof(WholeTrayCompletionEntity.RunId), nameof(WholeTrayCompletionEntity.TrayId),
                nameof(WholeTrayCompletionEntity.PlanRevision), nameof(WholeTrayCompletionEntity.DetectionCompletedEventId),
                nameof(WholeTrayCompletionEntity.SortingCompletedEventId),
                nameof(WholeTrayCompletionEntity.UnloadPreparationCompletedEventId),
                nameof(WholeTrayCompletionEntity.SourceMatrixId), nameof(WholeTrayCompletionEntity.PersistedRevision));
        });
    }

    private static void Properties<T>(ModelBuilder model, params string[] names) where T : class
    {
        var entity = model.Entity<T>();
        foreach (var name in names)
        {
            var property = typeof(T).GetProperty(name) ?? throw new InvalidOperationException("模型属性缺失:" + name);
            var mapping = entity.Property(property.PropertyType, name);
            if (property.PropertyType == typeof(string)) mapping.IsRequired();
        }
    }

    private static void Immutable<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> entity,
        params string[] names) where T : class
    {
        foreach (var name in names)
            entity.Property(name).Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Throw);
    }
}
