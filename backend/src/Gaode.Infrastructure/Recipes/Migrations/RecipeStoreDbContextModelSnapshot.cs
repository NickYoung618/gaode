using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Gaode.Infrastructure.Recipes.Migrations;

[DbContext(typeof(RecipeStoreDbContext))]
public sealed class RecipeStoreDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
        => BuildInitialModel(modelBuilder);

    // Frozen schema, independent of later CLR properties/current ConfigureModel changes.
    internal static void BuildInitialModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.12")
            .HasAnnotation("Relational:MaxIdentifierLength", 128);
        modelBuilder.Entity("Gaode.Infrastructure.Recipes.RecipeSavedContentRow", content =>
        {
            content.Property<string>("RecipeId").HasColumnType("TEXT").UseCollation("BINARY");
            content.Property<string>("Version").HasColumnType("TEXT").UseCollation("BINARY");
            content.Property<string>("DefinitionDigest").IsRequired().HasColumnType("TEXT");
            content.Property<string>("ContractVersion").IsRequired().HasColumnType("TEXT");
            content.Property<string>("DefinitionJson").IsRequired().HasColumnType("TEXT");
            content.Property<DateTimeOffset>("SavedUtc").HasColumnType("TEXT");
            content.Property<string>("ActorId").IsRequired().HasColumnType("TEXT");
            content.Property<string>("RequestId").IsRequired().HasColumnType("TEXT");
            content.HasKey("RecipeId", "Version");
            content.ToTable("RecipeSavedContent");
        });
        modelBuilder.Entity("Gaode.Infrastructure.Recipes.RecipeHeadRow", head =>
        {
            head.Property<string>("RecipeId").HasColumnType("TEXT").UseCollation("BINARY");
            head.Property<string>("CurrentVersion").IsRequired().HasColumnType("TEXT").UseCollation("BINARY");
            head.Property<string>("FCode").IsRequired().HasColumnType("TEXT").UseCollation("BINARY");
            head.HasKey("RecipeId");
            head.HasIndex("FCode").IsUnique();
            head.HasIndex("RecipeId", "CurrentVersion");
            head.HasOne("Gaode.Infrastructure.Recipes.RecipeSavedContentRow", null).WithMany()
                .HasForeignKey("RecipeId", "CurrentVersion").OnDelete(DeleteBehavior.Restrict).IsRequired();
            head.ToTable("RecipeHead");
        });
    }
}
