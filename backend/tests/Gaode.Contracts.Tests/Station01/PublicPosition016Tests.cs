using Gaode.Application.Configuration;
using Gaode.Contracts.Tests.Support;
using Xunit;

namespace Gaode.Contracts.Tests.Station01;

public sealed class PublicPosition016Tests
{
    [Fact]
    public void CompleteDocumentSaveRereadAndFrozenRunKeepActualCoordinates()
    {
        var repo=TestConfiguration.Workspace();
        var source=Path.Combine(repo,"specs/016-public-preparation-tray-check-unload/examples/mixed/config");
        var root=Path.Combine(repo,"artifacts/016-public-tray-flow","public-config-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        foreach(var input in Directory.EnumerateFiles(source,"*.json"))File.Copy(input,Path.Combine(root,Path.GetFileName(input)));
        var schema=Path.Combine(repo,"specs/001-station01-public-preparation/contracts");
        var loader=TestConfiguration.Loader(root,schema);
        var current=loader.LoadPublic(new("s01-public-011-joint","1"));
        var frozen=ConfigurationFreezer.Freeze(current,loader.LoadBudget(new("s01-budget-011-joint","2")),
            loader.LoadSimulation(new("s01-sim-011-joint","2")),new Dictionary<string,string>());
        var saved=loader.SavePublicPositions(new("s01-public-011-joint","1"),current.Value.Motion.Points.ThreeD with {X=101},
            current.Value.Motion.Points.Unload! with {X=180},current.Digest);
        var reread=TestConfiguration.Loader(root,schema).LoadPublic(new("s01-public-011-joint","1"));
        Assert.Equal(saved.Digest,reread.Digest);Assert.Equal(180,reread.Value.Motion.Points.Unload!.X);
        Assert.Equal(current.Value.Motion.Points.F,reread.Value.Motion.Points.F);
        Assert.Equal(100,frozen.Public.Motion.Points.ThreeD.X);
        Assert.Equal(current.Digest,frozen.PublicDigest);Assert.NotEqual(current.Digest,saved.Digest);
        Assert.DoesNotContain("height",saved.CanonicalJson);
        Assert.Throws<ConfigurationException>(()=>loader.SavePublicPositions(new("s01-public-011-joint","1"),
            saved.Value.Motion.Points.ThreeD,saved.Value.Motion.Points.Unload!,current.Digest));
    }
}
