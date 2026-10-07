using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Gaode.Infrastructure.Persistence.Migrations;

[DbContext(typeof(Station01DbContext))]
public sealed class Station01DbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.12")
            .HasAnnotation("Relational:MaxIdentifierLength", 128);
        Station01DbContext.ConfigureModel(modelBuilder);
    }
}
