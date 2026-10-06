using Gaode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Gaode.Infrastructure.Persistence.Migrations;

[DbContext(typeof(Station01DbContext))]
[Migration("202609210001_InitialStation01")]
public sealed class InitialStation01 : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable("Runs", columns: t => new
        {
            RunId = t.Column<Guid>("TEXT", nullable: false),
            RequestId = t.Column<string>("TEXT", nullable: false),
            SubjectId = t.Column<string>("TEXT", nullable: false),
            ContextJson = t.Column<string>("TEXT", nullable: false),
            State = t.Column<string>("TEXT", nullable: false),
            Terminal = t.Column<string>("TEXT", nullable: false),
            Revision = t.Column<long>("INTEGER", nullable: false),
            TerminalRevision = t.Column<long>("INTEGER", nullable: true),
            CancelRequested = t.Column<bool>("INTEGER", nullable: false),
            CreatedUtc = t.Column<DateTimeOffset>("TEXT", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_Runs", x => x.RunId);
            t.CheckConstraint("CK_Runs_TerminalState",
                "(Terminal = 'None' AND State NOT IN ('Completed','CompletedWithExceptions','Cancelled') AND TerminalRevision IS NULL) OR " +
                "(Terminal = 'Completed' AND State = 'Completed' AND TerminalRevision IS NOT NULL) OR " +
                "(Terminal = 'CompletedWithExceptions' AND State = 'CompletedWithExceptions' AND TerminalRevision IS NOT NULL) OR " +
                "(Terminal = 'Cancelled' AND State = 'Cancelled' AND TerminalRevision IS NOT NULL)");
        });
        m.CreateIndex("IX_Runs_TerminalIdentity", "Runs", new[] { "RunId", "TerminalRevision", "Terminal" }, unique: true);

        m.CreateTable("Commands", columns: t => new
        {
            CommandId = t.Column<Guid>("TEXT", nullable: false),
            RunId = t.Column<Guid>("TEXT", nullable: false),
            SubjectId = t.Column<string>("TEXT", nullable: false),
            RequestId = t.Column<string>("TEXT", nullable: false),
            Kind = t.Column<string>("TEXT", nullable: false),
            Scope = t.Column<string>("TEXT", nullable: false),
            PayloadDigest = t.Column<string>("TEXT", nullable: false),
            ReceiptState = t.Column<string>("TEXT", nullable: false),
            Applied = t.Column<bool>("INTEGER", nullable: true)
        }, constraints: t => t.PrimaryKey("PK_Commands", x => x.CommandId));
        m.CreateIndex("IX_Commands_Identity", "Commands", new[] { "SubjectId", "RequestId", "Kind", "Scope" }, unique: true);
        m.CreateIndex("IX_Commands_RunId", "Commands", "RunId");

        m.CreateTable("Writes", columns: t => new
        {
            WriteId = t.Column<Guid>("TEXT", nullable: false),
            RunId = t.Column<Guid>("TEXT", nullable: false),
            Revision = t.Column<long>("INTEGER", nullable: false),
            Kind = t.Column<string>("TEXT", nullable: false),
            PayloadJson = t.Column<string>("TEXT", nullable: false),
            PayloadDigest = t.Column<string>("TEXT", nullable: false),
            CommittedUtc = t.Column<DateTimeOffset>("TEXT", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_Writes", x => x.WriteId);
            t.ForeignKey("FK_Writes_Runs_RunId", x => x.RunId, "Runs", "RunId", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_Writes_RunRevision", "Writes", new[] { "RunId", "Revision" }, unique: true);

        m.CreateTable("Operations", columns: t => new
        {
            OperationId = t.Column<Guid>("TEXT", nullable: false),
            RunId = t.Column<Guid>("TEXT", nullable: false),
            Kind = t.Column<string>("TEXT", nullable: false),
            Attempt = t.Column<int>("INTEGER", nullable: false),
            State = t.Column<string>("TEXT", nullable: false),
            IntentWriteId = t.Column<Guid>("TEXT", nullable: false),
            EvidenceJson = t.Column<string>("TEXT", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_Operations", x => x.OperationId);
            t.ForeignKey("FK_Operations_Runs_RunId", x => x.RunId, "Runs", "RunId", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_Operations_RunKindAttempt", "Operations", new[] { "RunId", "Kind", "Attempt" });

        m.CreateTable("AlgorithmCalls", columns: t => new
        {
            CallId = t.Column<Guid>("TEXT", nullable: false),
            RunId = t.Column<Guid>("TEXT", nullable: false),
            CaptureId = t.Column<Guid>("TEXT", nullable: false),
            OperationId = t.Column<Guid>("TEXT", nullable: false),
            Attempt = t.Column<int>("INTEGER", nullable: false),
            IntentWriteId = t.Column<Guid>("TEXT", nullable: false),
            InputMediaIdsJson = t.Column<string>("TEXT", nullable: false),
            PublicVersion = t.Column<string>("TEXT", nullable: false),
            ScopeVersion = t.Column<string>("TEXT", nullable: false),
            ParametersVersion = t.Column<string>("TEXT", nullable: false),
            CapabilityId = t.Column<string>("TEXT", nullable: false),
            CapabilityVersion = t.Column<string>("TEXT", nullable: false),
            ExpectedComponentVersion = t.Column<string>("TEXT", nullable: false),
            SessionId = t.Column<Guid>("TEXT", nullable: false),
            ClockId = t.Column<string>("TEXT", nullable: false),
            StartTick = t.Column<long>("INTEGER", nullable: false),
            DueTick = t.Column<long>("INTEGER", nullable: false),
            BudgetMs = t.Column<int>("INTEGER", nullable: false),
            InvocationBasis = t.Column<string>("TEXT", nullable: false),
            DispatchEvidence = t.Column<string>("TEXT", nullable: false),
            TechnicalState = t.Column<string>("TEXT", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_AlgorithmCalls", x => x.CallId);
            t.ForeignKey("FK_AlgorithmCalls_Runs_RunId", x => x.RunId, "Runs", "RunId", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_AlgorithmCalls_RunCaptureAttempt", "AlgorithmCalls", new[] { "RunId", "CaptureId", "Attempt" });

        m.CreateTable("Media", columns: t => new
        {
            MediaId = t.Column<Guid>("TEXT", nullable: false),
            RunId = t.Column<Guid>("TEXT", nullable: false),
            CaptureId = t.Column<Guid>("TEXT", nullable: false),
            RelativeKey = t.Column<string>("TEXT", nullable: false),
            ByteLength = t.Column<long>("INTEGER", nullable: false),
            Format = t.Column<string>("TEXT", nullable: false),
            Source = t.Column<string>("TEXT", nullable: false),
            State = t.Column<string>("TEXT", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_Media", x => x.MediaId);
            t.ForeignKey("FK_Media_Runs_RunId", x => x.RunId, "Runs", "RunId", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_Media_RunCapture", "Media", new[] { "RunId", "CaptureId" });

        m.CreateTable("Handoffs", columns: t => new
        {
            HandoffId = t.Column<Guid>("TEXT", nullable: false),
            RunId = t.Column<Guid>("TEXT", nullable: false),
            PayloadJson = t.Column<string>("TEXT", nullable: false),
            Terminal = t.Column<string>("TEXT", nullable: false),
            Revision = t.Column<long>("INTEGER", nullable: false),
            WriteId = t.Column<Guid>("TEXT", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_Handoffs", x => x.HandoffId);
            t.CheckConstraint("CK_Handoff_Terminal", "Terminal IN ('Completed','CompletedWithExceptions')");
            t.ForeignKey("FK_Handoffs_Runs_RunId", x => x.RunId, "Runs", "RunId", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_Handoffs_RunId", "Handoffs", "RunId", unique: true);
        m.Sql("CREATE TRIGGER TR_Handoff_MatchesRun BEFORE INSERT ON Handoffs BEGIN SELECT CASE WHEN NOT EXISTS (SELECT 1 FROM Runs WHERE RunId = NEW.RunId AND Terminal = NEW.Terminal AND TerminalRevision = NEW.Revision) THEN RAISE(ABORT, 'HandoffRunMismatch') END; END;");
        m.Sql("CREATE TRIGGER TR_Run_TerminalImmutable BEFORE UPDATE ON Runs WHEN OLD.Terminal <> 'None' AND (NEW.Terminal <> OLD.Terminal OR NEW.State <> OLD.State OR NEW.TerminalRevision <> OLD.TerminalRevision OR NEW.Revision <> OLD.Revision) BEGIN SELECT RAISE(ABORT, 'TerminalImmutable'); END;");

        m.CreateTable("Manifests", columns: t => new
        {
            StoreId = t.Column<Guid>("TEXT", nullable: false),
            SchemaVersion = t.Column<string>("TEXT", nullable: false),
            Profile = t.Column<string>("TEXT", nullable: false),
            PrepareOperationId = t.Column<Guid>("TEXT", nullable: false),
            PreparedUtc = t.Column<DateTimeOffset>("TEXT", nullable: false)
        }, constraints: t => t.PrimaryKey("PK_Manifests", x => x.StoreId));
    }

    protected override void Down(MigrationBuilder m)
    {
        m.Sql("DROP TRIGGER IF EXISTS TR_Run_TerminalImmutable;");
        m.Sql("DROP TRIGGER IF EXISTS TR_Handoff_MatchesRun;");
        foreach (var table in new[] { "Handoffs", "Media", "AlgorithmCalls", "Operations", "Writes", "Commands", "Manifests", "Runs" })
            m.DropTable(table);
    }
}
