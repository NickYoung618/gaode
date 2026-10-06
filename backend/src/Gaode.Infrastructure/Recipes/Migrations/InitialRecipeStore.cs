using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Gaode.Infrastructure.Recipes.Migrations;

[DbContext(typeof(RecipeStoreDbContext))]
[Migration(MigrationId)]
public sealed class InitialRecipeStore : Migration
{
    public const string MigrationId = "202610030001_InitialRecipeStore";
    protected override void BuildTargetModel(ModelBuilder modelBuilder) => RecipeStoreDbContextModelSnapshot.BuildInitialModel(modelBuilder);
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable("RecipeSavedContent", columns: table => new
        {
            RecipeId = table.Column<string>("TEXT", nullable: false, collation: "BINARY"),
            Version = table.Column<string>("TEXT", nullable: false, collation: "BINARY"),
            DefinitionDigest = table.Column<string>("TEXT", nullable: false),
            ContractVersion = table.Column<string>("TEXT", nullable: false),
            DefinitionJson = table.Column<string>("TEXT", nullable: false),
            SavedUtc = table.Column<DateTimeOffset>("TEXT", nullable: false),
            ActorId = table.Column<string>("TEXT", nullable: false),
            RequestId = table.Column<string>("TEXT", nullable: false)
        }, constraints: table => table.PrimaryKey("PK_RecipeSavedContent", row => new { row.RecipeId, row.Version }));
        migrationBuilder.CreateTable("RecipeHead", columns: table => new
        {
            RecipeId = table.Column<string>("TEXT", nullable: false, collation: "BINARY"),
            FCode = table.Column<string>("TEXT", nullable: false, collation: "BINARY"),
            CurrentVersion = table.Column<string>("TEXT", nullable: false, collation: "BINARY")
        }, constraints: table =>
        {
            table.PrimaryKey("PK_RecipeHead", row => row.RecipeId);
            table.ForeignKey("FK_RecipeHead_RecipeSavedContent_RecipeId_CurrentVersion",
                row => new { row.RecipeId, row.CurrentVersion }, "RecipeSavedContent", new[] { "RecipeId", "Version" },
                onDelete: ReferentialAction.Restrict);
        });
        migrationBuilder.CreateIndex("IX_RecipeHead_FCode", "RecipeHead", "FCode", unique: true);
        migrationBuilder.CreateIndex("IX_RecipeHead_RecipeId_CurrentVersion", "RecipeHead", new[] { "RecipeId", "CurrentVersion" });
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("RecipeHead");
        migrationBuilder.DropTable("RecipeSavedContent");
    }
}
