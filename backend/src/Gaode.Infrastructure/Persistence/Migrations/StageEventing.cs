using Gaode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Gaode.Infrastructure.Persistence.Migrations;

[DbContext(typeof(Station01DbContext))]
[Migration("202609220001_StageEventing")]
public sealed class StageEventing : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable("StageEvents", columns: t => new
        {
            EventId = t.Column<Guid>("TEXT", nullable: false), RunId = t.Column<Guid>("TEXT", nullable: false),
            TrayId = t.Column<Guid>("TEXT", nullable: false), StationId = t.Column<string>("TEXT", nullable: false),
            LineId = t.Column<string>("TEXT", nullable: false), Stage = t.Column<string>("TEXT", nullable: false),
            OperationId = t.Column<Guid>("TEXT", nullable: false), Attempt = t.Column<int>("INTEGER", nullable: false),
            ConnectionEpoch = t.Column<long>("INTEGER", nullable: false), EventType = t.Column<string>("TEXT", nullable: false),
            OccurredUtc = t.Column<DateTimeOffset>("TEXT", nullable: false), PersistedUtc = t.Column<DateTimeOffset>("TEXT", nullable: false),
            Source = t.Column<string>("TEXT", nullable: false), Quality = t.Column<string>("TEXT", nullable: false),
            ErrorCode = t.Column<string>("TEXT", nullable: true), PayloadDigest = t.Column<string>("TEXT", nullable: false),
            PayloadJson = t.Column<string>("TEXT", nullable: false), IdempotencyKey = t.Column<string>("TEXT", nullable: false),
            Sequence = t.Column<long>("INTEGER", nullable: false), RetainUntilUtc = t.Column<DateTimeOffset>("TEXT", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_StageEvents", x => x.EventId);
            t.ForeignKey("FK_StageEvents_Runs_RunId", x => x.RunId, "Runs", "RunId", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_StageEvents_RunTrayStageSequence", "StageEvents", new[] { "RunId", "TrayId", "Stage", "Sequence" }, unique: true);
        m.CreateIndex("IX_StageEvents_IdempotencyKey", "StageEvents", "IdempotencyKey", unique: true);

        m.CreateTable("StageProjections", columns: t => new
        {
            ProjectionId = t.Column<Guid>("TEXT", nullable: false), RunId = t.Column<Guid>("TEXT", nullable: false),
            TrayId = t.Column<Guid>("TEXT", nullable: false), StationId = t.Column<string>("TEXT", nullable: false),
            LineId = t.Column<string>("TEXT", nullable: false), Stage = t.Column<string>("TEXT", nullable: false),
            Revision = t.Column<long>("INTEGER", nullable: false), Status = t.Column<string>("TEXT", nullable: false),
            CurrentOperationId = t.Column<Guid>("TEXT", nullable: true), ConnectionEpoch = t.Column<long>("INTEGER", nullable: false),
            DeviceHeld = t.Column<bool>("INTEGER", nullable: false), NeedsManualReview = t.Column<bool>("INTEGER", nullable: false),
            LastEventId = t.Column<Guid>("TEXT", nullable: false), UpdatedUtc = t.Column<DateTimeOffset>("TEXT", nullable: false),
            RetainUntilUtc = t.Column<DateTimeOffset>("TEXT", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_StageProjections", x => x.ProjectionId);
            t.ForeignKey("FK_StageProjections_Runs_RunId", x => x.RunId, "Runs", "RunId", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_StageProjections_RunTrayStage", "StageProjections", new[] { "RunId", "TrayId", "Stage" }, unique: true);

        m.CreateTable("StageIdempotencies", columns: t => new
        {
            IdempotencyKey = t.Column<string>("TEXT", nullable: false), PayloadDigest = t.Column<string>("TEXT", nullable: false),
            RunId = t.Column<Guid>("TEXT", nullable: false), TrayId = t.Column<Guid>("TEXT", nullable: false),
            Stage = t.Column<string>("TEXT", nullable: false), OperationId = t.Column<Guid>("TEXT", nullable: false),
            EventId = t.Column<Guid>("TEXT", nullable: false), CreatedUtc = t.Column<DateTimeOffset>("TEXT", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_StageIdempotencies", x => x.IdempotencyKey);
            t.ForeignKey("FK_StageIdempotencies_Runs_RunId", x => x.RunId, "Runs", "RunId", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_StageIdempotencies_EventId", "StageIdempotencies", "EventId", unique: true);
        m.CreateIndex("IX_StageIdempotencies_RunId", "StageIdempotencies", "RunId");
    }

    protected override void Down(MigrationBuilder m)
    {
        m.DropTable("StageIdempotencies");
        m.DropTable("StageProjections");
        m.DropTable("StageEvents");
    }
}
