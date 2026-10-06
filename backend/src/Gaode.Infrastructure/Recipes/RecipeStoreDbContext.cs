using Microsoft.EntityFrameworkCore;

namespace Gaode.Infrastructure.Recipes;

// Persistence rows only. RecipeDefinition remains the Application-owned business model.
public sealed class RecipeStoreDbContext(DbContextOptions<RecipeStoreDbContext> options) : DbContext(options)
{
    public DbSet<RecipeHeadRow> Heads => Set<RecipeHeadRow>();
    public DbSet<RecipeSavedContentRow> Contents => Set<RecipeSavedContentRow>();
    protected override void OnModelCreating(ModelBuilder modelBuilder) => ConfigureModel(modelBuilder);
    internal static void ConfigureModel(ModelBuilder modelBuilder)
    {
        var content = modelBuilder.Entity<RecipeSavedContentRow>();
        content.ToTable("RecipeSavedContent");
        content.HasKey(x => new { x.RecipeId, x.Version });
        content.Property(x => x.DefinitionDigest).IsRequired();
        content.Property(x => x.ContractVersion).IsRequired();
        content.Property(x => x.DefinitionJson).IsRequired();
        content.Property(x => x.SavedUtc);
        content.Property(x => x.ActorId).IsRequired();
        content.Property(x => x.RequestId).IsRequired();
        content.Property(x => x.RecipeId).UseCollation("BINARY");
        content.Property(x => x.Version).UseCollation("BINARY");
        var head = modelBuilder.Entity<RecipeHeadRow>();
        head.ToTable("RecipeHead");
        head.HasKey(x => x.RecipeId);
        head.Property(x => x.RecipeId).UseCollation("BINARY");
        head.Property(x => x.CurrentVersion).UseCollation("BINARY").IsRequired();
        head.Property(x => x.FCode).UseCollation("BINARY").IsRequired();
        head.HasIndex(x => x.FCode).IsUnique();
        head.HasIndex(x => new { x.RecipeId, x.CurrentVersion });
        head.HasOne<RecipeSavedContentRow>().WithMany()
            .HasForeignKey(x => new { x.RecipeId, x.CurrentVersion })
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class RecipeHeadRow
{
    public string RecipeId { get; set; } = "";
    public string FCode { get; set; } = "";
    public string CurrentVersion { get; set; } = "";
}

public sealed class RecipeSavedContentRow
{
    public string RecipeId { get; set; } = "";
    public string Version { get; set; } = "";
    public string DefinitionDigest { get; set; } = "";
    public string ContractVersion { get; set; } = "";
    public string DefinitionJson { get; set; } = "";
    public DateTimeOffset SavedUtc { get; set; }
    public string ActorId { get; set; } = "";
    public string RequestId { get; set; } = "";
}
