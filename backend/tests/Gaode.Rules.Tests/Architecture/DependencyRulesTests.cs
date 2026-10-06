using System.Xml.Linq;
using Xunit;

namespace Gaode.Rules.Tests.Architecture;

public sealed class DependencyRulesTests
{
    [Fact]
    public void ProjectReferencesFollowTheFourLayerBoundary()
    {
        var root = FindWorkspace();
        AssertReferences(root, "Gaode.Domain", []);
        AssertReferences(root, "Gaode.Application", ["Gaode.Domain"]);
        AssertReferences(root, "Gaode.Infrastructure", ["Gaode.Application", "Gaode.Plc.Protocol"]);
        AssertReferences(root, "Gaode.Host", ["Gaode.Application", "Gaode.Infrastructure"]);
        AssertReferences(root, "Gaode.Plc.Protocol", []);
        var virtualProject = Path.Combine(root, "VirtualPlc", "VirtualPlc.csproj");
        Assert.Empty(ProtocolBoundaryChecker.CheckProject(virtualProject, "VirtualPlc",
            XDocument.Load(virtualProject).Descendants("ProjectReference")
                .Select(x => Path.GetFileNameWithoutExtension((string)x.Attribute("Include")!)), []));
        Assert.Single(Directory.EnumerateFiles(Path.Combine(root, "backend", "src"),
            "Gaode.Host.csproj", SearchOption.AllDirectories));
    }

    [Fact]
    public void DomainDoesNotReferenceTransportPersistenceOrDeviceFrameworks()
    {
        var root = Path.Combine(FindWorkspace(), "backend", "src", "Gaode.Domain");
        var forbidden = new[] { "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore",
            "Gaode.Infrastructure", "Gaode.Application", "Gaode.Plc.Protocol", "System.IO.Ports" };
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
                     .Where(x => !x.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            var source = File.ReadAllText(file);
            foreach (var value in forbidden)
                Assert.DoesNotContain(value, source, StringComparison.Ordinal);
        }
    }

    private static void AssertReferences(string root, string project, string[] expected)
    {
        var path = Path.Combine(root, "backend", "src", project, project + ".csproj");
        Assert.True(File.Exists(path), "Required project is missing: " + path);
        var actual = XDocument.Load(path).Descendants("ProjectReference")
            .Select(x => Path.GetFileNameWithoutExtension((string)x.Attribute("Include")!))
            .Order().ToArray();
        Assert.Equal(expected.Order().ToArray(), actual);
        Assert.Empty(ProtocolBoundaryChecker.CheckProject(path, project, actual,
            XDocument.Load(path).Descendants("PackageReference").Select(x => (string)x.Attribute("Include")!)));
    }

    private static string FindWorkspace()
    {
        for (var path = new DirectoryInfo(AppContext.BaseDirectory); path is not null; path = path.Parent)
            if (File.Exists(Path.Combine(path.FullName, "global.json"))) return path.FullName;
        throw new InvalidOperationException("Workspace not found");
    }
}
