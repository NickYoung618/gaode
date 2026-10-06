using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Gaode.Infrastructure.Persistence.Migrations;

[DbContext(typeof(Station01DbContext))]
[Migration(MigrationId)]
public sealed class PublicTrayEnd : Migration
{
    public const string MigrationId = "202610060001_PublicTrayEnd";
    protected override void Up(MigrationBuilder m)
    {
        m.AlterColumn<Guid>("DetectionCompletedEventId", "WholeTrayCompletions", "TEXT", nullable: true,
            oldClrType: typeof(Guid), oldType: "TEXT");
        m.AlterColumn<Guid>("SortingCompletedEventId", "WholeTrayCompletions", "TEXT", nullable: true,
            oldClrType: typeof(Guid), oldType: "TEXT");
        m.Sql("UPDATE Manifests SET SchemaVersion='s01-store/3' WHERE SchemaVersion='s01-store/2';");
    }
    protected override void Down(MigrationBuilder m) =>
        throw new NotSupportedException("PublicTrayEnd requires preserved-source restoration; early-end rows cannot gain fabricated stage references.");
    protected override void BuildTargetModel(ModelBuilder builder) => Station01DbContext.ConfigureModel(builder);
}
