using Gaode.Infrastructure.Configuration;
using Gaode.Domain.Configuration;
using Gaode.Application.Ports;
using Gaode.Application.Recipes;
using Gaode.Infrastructure.Recipes;

namespace Gaode.Contracts.Tests.Support;

public static class TestConfiguration
{
    public static string Workspace()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "global.json"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("工作区不存在");
    }
    public static IPublicConfiguration Loader()
    {
        var feature = Path.Combine(Workspace(), "specs", "001-station01-public-preparation");
        return new ConfigurationLoader(Path.Combine(feature, "examples"), Path.Combine(feature, "contracts"));
    }
    public static IPublicConfiguration Loader(string directory,string contracts) => new ConfigurationLoader(directory,contracts);
    // Actual file provider at the fixture boundary; business assertions consume
    // the existing semantic catalog. No protocol definitions or values enter it.
    public static IRecipeCatalog RecipeCatalog(string path) => new JsonRecipeCatalog(path);
    public static IRecipeCatalog FileRecipeCatalog(string path) => RecipeCatalogFactory.Create(
        new RecipeCatalogOptions { Provider = "File", CatalogPath = path }, Workspace());
    public static (PublicConfiguration Public, BusinessBudget Budget, SimulationProfile Simulation) Normal()
    {
        var loader = Loader();
        return (loader.LoadPublic(new("s01-public-dev", "1.0.0")).Value,
            loader.LoadBudget(new("s01-budget-dev", "3.0.0")).Value,
            loader.LoadSimulation(new("s01-sim-normal", "3.0.0")).Value);
    }
}
