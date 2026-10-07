using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Gaode.Infrastructure.Persistence.Migrations;

[DbContext(typeof(Station01DbContext))]
[Migration("202610010001_CommunicationEvidence")]
public sealed class CommunicationEvidence : Migration
{
    public const string MigrationId = "202610010001_CommunicationEvidence";
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable("PlcCommunicationEvidence", columns: t => new
        {
            EvidenceId = t.Column<Guid>("TEXT", nullable: false),
            StoreId = t.Column<Guid>("TEXT", nullable: false),
            ObservationId = t.Column<Guid>("TEXT", nullable: false),
            RunId = t.Column<Guid>("TEXT", nullable: true),
            OperationId = t.Column<Guid>("TEXT", nullable: true),
            ActionId = t.Column<Guid>("TEXT", nullable: true),
            ConnectionEpoch = t.Column<long>("INTEGER", nullable: false),
            ObservedStartUtc = t.Column<DateTimeOffset>("TEXT", nullable: false),
            ObservedEndUtc = t.Column<DateTimeOffset>("TEXT", nullable: false),
            PersistedUtc = t.Column<DateTimeOffset>("TEXT", nullable: false),
            PayloadSchema = t.Column<string>("TEXT", nullable: false),
            PayloadDigest = t.Column<string>("TEXT", nullable: false),
            RawPayloadJson = t.Column<string>("TEXT", nullable: false)
        }, constraints: t => t.PrimaryKey("PK_PlcCommunicationEvidence", e => e.EvidenceId));
        m.CreateIndex("IX_PlcCommunicationEvidence_RunId_OperationId", "PlcCommunicationEvidence", new[] { "RunId", "OperationId" });
        m.CreateIndex("IX_PlcCommunicationEvidence_ActionId_ConnectionEpoch", "PlcCommunicationEvidence", new[] { "ActionId", "ConnectionEpoch" });
        m.CreateIndex("IX_PlcCommunicationEvidence_ObservationId", "PlcCommunicationEvidence", "ObservationId");
    }
    protected override void Down(MigrationBuilder m) => m.DropTable("PlcCommunicationEvidence");
    protected override void BuildTargetModel(ModelBuilder modelBuilder) => Station01DbContext.ConfigureModel(modelBuilder);
}
