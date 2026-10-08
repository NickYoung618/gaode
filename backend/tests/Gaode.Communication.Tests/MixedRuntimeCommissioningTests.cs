using System.Text.Json;
using Gaode.Application.Configuration;
using Gaode.Domain.Configuration;
using Gaode.Infrastructure.Configuration;
using Gaode.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Gaode.Communication.Tests;

public sealed class MixedRuntimeCommissioningTests
{
    [Fact]
    public void CommissioningUsesExplicitCostBudgetAndNeverFallsBackToTestConstants()
    {
        var publicConfig = ReviewBusinessData.Motion() with { Purpose = RuntimePurposes.RealDeviceCommissioning };
        var budget = ReviewBusinessData.Budget() with { Purpose = publicConfig.Purpose,
            RecipeExecution = new(123, 234, 345, 456, 567, 678) };
        var frozen = new FrozenConfiguration(publicConfig, budget, null!, "", "", "", "", "budget-fixture", "",
            new Dictionary<string, string>(), "fixture");
        var provider = new ApprovedExecutionCostProvider();
        var cost = provider.Resolve(frozen);
        Assert.Equal(123, cost.CaptureMs); Assert.Equal(234, cost.AlgorithmMs);
        Assert.Equal(345, cost.AcquisitionReleaseMs); Assert.Equal(456, cost.CaptureWaitMs);
        Assert.Equal(567, cost.AlgorithmWaitMs); Assert.Equal(678, cost.InputReleaseWaitMs);
        Assert.Equal(budget.Source, cost.Source); Assert.Equal("budget-fixture", cost.BudgetDigest);
        Assert.Throws<InvalidOperationException>(() => provider.Resolve(frozen with { Budget = budget with { RecipeExecution = null } }));
        Assert.Throws<InvalidOperationException>(() => provider.Resolve(frozen with { Budget = budget with { RecipeExecution = budget.RecipeExecution with { CaptureMs = 0 } } }));
        Assert.Throws<InvalidOperationException>(() => provider.Resolve(frozen with { Public = publicConfig with { Purpose = "Test" } }));
        var test = provider.Resolve(frozen with { Public = publicConfig with { Purpose = "Test" }, Budget = budget with { Purpose = "Test", RecipeExecution = null } });
        Assert.Equal(5000, test.CaptureMs); Assert.Equal(10000, test.AlgorithmMs);
        var json = JsonSerializer.Serialize(budget, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.DoesNotContain("isValid", json);
    }

    [Fact]
    public async Task StoreManifestPurposeCannotBeMixedWithLegacyTestRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "gaode-mixed-store-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var options = new DbContextOptionsBuilder<Station01DbContext>()
                .UseSqlite($"Data Source={Path.Combine(root, "station01.test.db")};Pooling=False").Options;
            await using (var db = new Station01DbContext(options))
            {
                await db.Database.MigrateAsync();
                db.Manifests.Add(new() { StoreId = Guid.NewGuid(), SchemaVersion = "s01-store/3",
                    Profile = RuntimePurposes.RealDeviceCommissioning, PrepareOperationId = Guid.NewGuid(), PreparedUtc = DateTimeOffset.UtcNow });
                await db.SaveChangesAsync();
            }
            Assert.True(StoreCompatibilityProbe.Inspect(root, RuntimePurposes.RealDeviceCommissioning).Compatible);
            Assert.False(StoreCompatibilityProbe.Inspect(root).Compatible);
            Assert.Throws<InvalidOperationException>(() => StoreCompatibilityProbe.ReadWriteConnectionString(root));
            Assert.Contains("ReadWrite", StoreCompatibilityProbe.ReadWriteConnectionString(root, RuntimePurposes.RealDeviceCommissioning));
            Assert.False(StoreCompatibilityProbe.Inspect(root, "Unknown").Compatible);
        }
        finally { Directory.Delete(root, true); }
    }
}
