using Gaode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Gaode.Infrastructure.Persistence.Migrations;

[DbContext(typeof(Station01DbContext))]
[Migration("202609230001_Station01MainFlow")]
public sealed class Station01MainFlow : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<string>("PlanRevision", "StageEvents", "TEXT", nullable: false, defaultValue: "");
        m.AddColumn<DateTimeOffset>("StageStartedUtc", "StageEvents", "TEXT", nullable: true);
        m.AddColumn<DateTimeOffset>("StageDeadlineUtc", "StageEvents", "TEXT", nullable: true);

        m.CreateTable("PublicPreparationHandoffsV2", columns: t => new
        {
            HandoffId = t.Column<Guid>("TEXT", nullable: false), RunId = t.Column<Guid>("TEXT", nullable: false),
            TrayId = t.Column<Guid>("TEXT", nullable: false), WriteId = t.Column<Guid>("TEXT", nullable: false),
            Revision = t.Column<long>("INTEGER", nullable: false), PayloadJson = t.Column<string>("TEXT", nullable: false),
            PayloadDigest = t.Column<string>("TEXT", nullable: false), PersistedUtc = t.Column<DateTimeOffset>("TEXT", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_PublicPreparationHandoffsV2", x => x.HandoffId);
            t.ForeignKey("FK_PublicPreparationHandoffsV2_Runs_RunId", x => x.RunId, "Runs", "RunId", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_PublicPreparationHandoffsV2_Writes_WriteId", x => x.WriteId, "Writes", "WriteId", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_PublicPreparationHandoffsV2_RunId_TrayId", "PublicPreparationHandoffsV2",
            new[] { "RunId", "TrayId" }, unique: true);
        m.CreateIndex("IX_PublicPreparationHandoffsV2_WriteId", "PublicPreparationHandoffsV2", "WriteId", unique: true);

        m.CreateTable("ComponentEvidenceMatrices", columns: t => new
        {
            MatrixId = t.Column<Guid>("TEXT", nullable: false),
            SchemaVersion = t.Column<string>("TEXT", nullable: false),
            RunId = t.Column<Guid>("TEXT", nullable: false), TrayId = t.Column<Guid>("TEXT", nullable: false),
            PlanRevision = t.Column<string>("TEXT", nullable: false), Milestone = t.Column<string>("TEXT", nullable: false),
            ComponentsJson = t.Column<string>("TEXT", nullable: false), MatrixDigest = t.Column<string>("TEXT", nullable: false),
            Scope = t.Column<string>("TEXT", nullable: false), CreatedUtc = t.Column<DateTimeOffset>("TEXT", nullable: false),
            RetainUntilUtc = t.Column<DateTimeOffset>("TEXT", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_ComponentEvidenceMatrices", x => x.MatrixId);
            t.ForeignKey("FK_ComponentEvidenceMatrices_Runs_RunId", x => x.RunId, "Runs", "RunId", onDelete: ReferentialAction.Restrict);
            t.CheckConstraint("CK_ComponentEvidenceMatrices_Retention", "julianday(RetainUntilUtc) >= julianday(CreatedUtc, '+7 years')");
        });
        m.CreateIndex("IX_ComponentEvidenceMatrices_RunTrayMilestone", "ComponentEvidenceMatrices",
            new[] { "RunId", "TrayId", "Milestone" }, unique: true);
        m.CreateIndex("IX_ComponentEvidenceMatrices_MatrixDigest", "ComponentEvidenceMatrices", "MatrixDigest", unique: true);

        m.CreateTable("ControlledRecoveryDecisions", columns: t => new
        {
            DecisionId = t.Column<Guid>("TEXT", nullable: false), RequestId = t.Column<string>("TEXT", nullable: false),
            ExpectedRevision = t.Column<long>("INTEGER", nullable: false), OriginalTaskId = t.Column<string>("TEXT", nullable: false),
            RunId = t.Column<Guid>("TEXT", nullable: false), TrayId = t.Column<Guid>("TEXT", nullable: false),
            OriginalOperationId = t.Column<Guid>("TEXT", nullable: true), OriginalStage = t.Column<string>("TEXT", nullable: false),
            Decision = t.Column<string>("TEXT", nullable: false), ActorId = t.Column<string>("TEXT", nullable: false),
            ActorRole = t.Column<string>("TEXT", nullable: false), DecidedAtUtc = t.Column<DateTimeOffset>("TEXT", nullable: false),
            Reason = t.Column<string>("TEXT", nullable: false), EvidenceReferencesJson = t.Column<string>("TEXT", nullable: false),
            PayloadDigest = t.Column<string>("TEXT", nullable: false), RetainUntilUtc = t.Column<DateTimeOffset>("TEXT", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_ControlledRecoveryDecisions", x => x.DecisionId);
            t.ForeignKey("FK_ControlledRecoveryDecisions_Runs_RunId", x => x.RunId, "Runs", "RunId", onDelete: ReferentialAction.Restrict);
            t.CheckConstraint("CK_ControlledRecoveryDecisions_Retention", "julianday(RetainUntilUtc) >= julianday(DecidedAtUtc, '+7 years')");
        });
        m.CreateIndex("IX_ControlledRecoveryDecisions_RunRequest", "ControlledRecoveryDecisions",
            new[] { "RunId", "RequestId" }, unique: true);
        m.CreateIndex("IX_ControlledRecoveryDecisions_RunTrayTask", "ControlledRecoveryDecisions",
            new[] { "RunId", "TrayId", "OriginalTaskId" });

        m.CreateTable("WholeTrayCompletions", columns: t => new
        {
            WholeTrayCompletionId = t.Column<Guid>("TEXT", nullable: false), RunId = t.Column<Guid>("TEXT", nullable: false),
            TrayId = t.Column<Guid>("TEXT", nullable: false), PlanRevision = t.Column<string>("TEXT", nullable: false),
            DetectionCompletedEventId = t.Column<Guid>("TEXT", nullable: false), SortingCompletedEventId = t.Column<Guid>("TEXT", nullable: false),
            UnloadPreparationCompletedEventId = t.Column<Guid>("TEXT", nullable: false), SourceMatrixId = t.Column<Guid>("TEXT", nullable: false),
            PersistedRevision = t.Column<long>("INTEGER", nullable: false), CreatedUtc = t.Column<DateTimeOffset>("TEXT", nullable: false),
            RetainUntilUtc = t.Column<DateTimeOffset>("TEXT", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_WholeTrayCompletions", x => x.WholeTrayCompletionId);
            t.ForeignKey("FK_WholeTrayCompletions_Runs_RunId", x => x.RunId, "Runs", "RunId", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_WholeTrayCompletions_StageEvents_Detection", x => x.DetectionCompletedEventId, "StageEvents", "EventId", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_WholeTrayCompletions_StageEvents_Sorting", x => x.SortingCompletedEventId, "StageEvents", "EventId", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_WholeTrayCompletions_StageEvents_Unload", x => x.UnloadPreparationCompletedEventId, "StageEvents", "EventId", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_WholeTrayCompletions_ComponentEvidenceMatrices", x => x.SourceMatrixId, "ComponentEvidenceMatrices", "MatrixId", onDelete: ReferentialAction.Restrict);
            t.CheckConstraint("CK_WholeTrayCompletions_Retention", "julianday(RetainUntilUtc) >= julianday(CreatedUtc, '+7 years')");
        });
        m.CreateIndex("IX_WholeTrayCompletions_RunTray", "WholeTrayCompletions", new[] { "RunId", "TrayId" }, unique: true);
        m.CreateIndex("IX_WholeTrayCompletions_RunRevision", "WholeTrayCompletions", new[] { "RunId", "PersistedRevision" }, unique: true);
        m.CreateIndex("IX_WholeTrayCompletions_DetectionCompletedEventId", "WholeTrayCompletions", "DetectionCompletedEventId");
        m.CreateIndex("IX_WholeTrayCompletions_SortingCompletedEventId", "WholeTrayCompletions", "SortingCompletedEventId");
        m.CreateIndex("IX_WholeTrayCompletions_UnloadPreparationCompletedEventId", "WholeTrayCompletions", "UnloadPreparationCompletedEventId");
        m.CreateIndex("IX_WholeTrayCompletions_SourceMatrixId", "WholeTrayCompletions", "SourceMatrixId");
    }

    protected override void Down(MigrationBuilder m)
    {
        m.DropTable("WholeTrayCompletions");
        m.DropTable("ControlledRecoveryDecisions");
        m.DropTable("ComponentEvidenceMatrices");
        m.DropTable("PublicPreparationHandoffsV2");
        m.DropColumn("PlanRevision", "StageEvents");
        m.DropColumn("StageStartedUtc", "StageEvents");
        m.DropColumn("StageDeadlineUtc", "StageEvents");
    }
}
