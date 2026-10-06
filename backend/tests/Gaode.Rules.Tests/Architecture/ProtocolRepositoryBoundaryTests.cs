using System.Text.Json;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Gaode.Rules.Tests.Architecture;

public sealed class ProtocolRepositoryBoundaryTests
{
    private static readonly List<(BoundaryInput Input, SemanticModel Model)> sourceModels = [];
    private static readonly Lazy<IReadOnlyList<BoundaryViolation>> Findings = new(Scan);
    internal static IReadOnlyList<(BoundaryInput Input, SemanticModel Model)> SourceModels { get { _ = Findings.Value; return sourceModels; } }
    internal static IReadOnlyList<BoundaryViolation> CoverageFailures => Findings.Value.Where(v => v.RuleId.StartsWith("CHECKER", StringComparison.Ordinal)).ToArray();

    [Fact]
    public void ProductionBusinessSourceBoundary() => AssertClean("A01", "A03", "A04", "A05");
    [Fact]
    public void PublicPortShapesAreReviewed() => AssertClean("A02");
    [Fact]
    public void BusinessContractsAndTestsDoNotRequireWireDetails() => AssertClean("A07");
    [Fact]
    public void FormalAdaptersUseNamedSignalAccess() => AssertClean("A06");

    private static void AssertClean(params string[] rules)
    {
        var failures = Findings.Value.Where(x => rules.Contains(x.RuleId) || x.RuleId.StartsWith("CHECKER", StringComparison.Ordinal));
        Assert.True(!failures.Any(), string.Join(Environment.NewLine,
            failures.Take(25).Select(x => $"{x.RuleId} {x.Path}:{x.Line}:{x.Column} {x.Message}")) +
            $"\nTotal findings in this group: {failures.Count()}; full findings are in csharp-boundary.json.");
    }

    private static IReadOnlyList<BoundaryViolation> Scan()
    {
        var workspace = FindWorkspace();
        var directory = Path.Combine(workspace, "backend/tests/Gaode.Rules.Tests/Architecture");
        using var inventory = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "009-boundary-inventory.json")));
        var entries = inventory.RootElement.GetProperty("files").EnumerateArray()
            .ToDictionary(x => x.GetProperty("path").GetString()!, x => x.GetProperty("role").GetString()!);
        var checker = new ProtocolBoundaryChecker(ProtocolBoundaryChecker.LoadShapes(Path.Combine(directory, "009-public-shapes.json")));
        var result = new List<BoundaryViolation>();
        var scanned = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var contract in Directory.EnumerateFiles(Path.Combine(workspace, "specs"), "*.md", SearchOption.AllDirectories)
                     .Where(x => Path.GetDirectoryName(x)!.EndsWith("contracts", StringComparison.Ordinal) &&
                         !Path.GetRelativePath(workspace, x).Split(Path.DirectorySeparatorChar).Contains("deliveries")))
        {
            var relative = Path.GetRelativePath(workspace, contract).Replace('\\', '/');
            if (!entries.ContainsKey(relative)) result.Add(new("CHECKER-INVENTORY", relative, 1, 1, "New contract is unclassified."));
        }
        var references = ProtocolBoundaryChecker.RuntimeReferences().GroupBy(x => Path.GetFileName(x.Display)!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var assemblyRoots = new[] { Path.Combine(workspace, "backend"), Path.Combine(workspace, "VirtualPlc") };
        foreach (var root in assemblyRoots)
            foreach (var dll in Directory.EnumerateFiles(root, "*.dll", SearchOption.AllDirectories)
                         .Where(x => x.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}Debug{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                                     !x.EndsWith(".resources.dll", StringComparison.OrdinalIgnoreCase) && !x.Contains($"{Path.DirectorySeparatorChar}runtimes{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
                references.TryAdd(Path.GetFileName(dll), MetadataReference.CreateFromFile(dll));
        var dotnetRoot = Path.GetDirectoryName(Environment.ProcessPath)!;
        // The executable can be testhost; locate reference packs from the runtime installation.
        for (var parent = new DirectoryInfo(Path.GetDirectoryName(typeof(object).Assembly.Location)!); parent is not null; parent = parent.Parent)
            if (Directory.Exists(Path.Combine(parent.FullName, "packs"))) { dotnetRoot = parent.FullName; break; }
        var aspPack = Path.Combine(dotnetRoot, "packs", "Microsoft.AspNetCore.App.Ref");
        if (!Directory.Exists(aspPack)) throw new InvalidOperationException("Pinned ASP.NET reference pack unavailable");
        var aspVersion = Directory.GetDirectories(aspPack).OrderDescending().First();
        foreach (var dll in Directory.GetFiles(Path.Combine(aspVersion, "ref", "net10.0"), "*.dll"))
            references.TryAdd(Path.GetFileName(dll), MetadataReference.CreateFromFile(dll));

        var compilations = new Dictionary<string, CSharpCompilation>(StringComparer.OrdinalIgnoreCase);
        var sourceSets = new Dictionary<string, List<SyntaxTree>>(StringComparer.OrdinalIgnoreCase);
        var projectReferences = new Dictionary<string, List<(string Path, string? Alias)>>(StringComparer.OrdinalIgnoreCase);
        CSharpCompilation Compile(string project)
        {
            project = Path.GetFullPath(project);
            if (compilations.TryGetValue(project, out var existing)) return existing;
            var projectRoot = Path.GetDirectoryName(project)!;
            var document = XDocument.Load(project);
            var sources = Directory.EnumerateFiles(projectRoot, "*.cs", SearchOption.AllDirectories).Where(x => !Generated(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var linked in document.Descendants("Compile").Select(x => (string?)x.Attribute("Include")).OfType<string>())
                sources.Add(Path.GetFullPath(Path.Combine(projectRoot, linked)));
            var trees = sources.Select(path => (SyntaxTree)CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path)).ToList();
            var web = ((string?)document.Root?.Attribute("Sdk"))?.Contains(".Web", StringComparison.Ordinal) == true;
            trees.Add(CSharpSyntaxTree.ParseText("global using System; global using System.IO; global using System.Linq; global using System.Collections.Generic; global using System.Threading; global using System.Threading.Tasks; global using System.Net.Http;" +
                (web ? "global using Microsoft.AspNetCore.Builder; global using Microsoft.AspNetCore.Hosting; global using Microsoft.AspNetCore.Http; global using Microsoft.AspNetCore.Routing; global using Microsoft.Extensions.Configuration; global using Microsoft.Extensions.DependencyInjection; global using Microsoft.Extensions.Hosting; global using Microsoft.Extensions.Logging;" : ""), path: "__implicit_usings.cs"));
            // MSBuild emits these real project items into AssemblyInfo. Preserve
            // them in the source compilation, otherwise legitimate internal test
            // access binds differently from the actual project. Boundary rules
            // still scan every accessing expression and never exempt friend code.
            trees.Add(CSharpSyntaxTree.ParseText(string.Join(Environment.NewLine,
                document.Descendants("InternalsVisibleTo").Select(x =>
                    "[assembly: System.Runtime.CompilerServices.InternalsVisibleTo(" +
                    JsonSerializer.Serialize((string)x.Attribute("Include")!) + ")]")), path: "__project_attributes.cs"));
            var ownName = Path.GetFileNameWithoutExtension(project);
            projectReferences[project] = document.Descendants("ProjectReference").Select(x =>
                (Path.GetFullPath(Path.Combine(projectRoot, (string)x.Attribute("Include")!)),
                 (string?)x.Attribute("Aliases") ?? (string?)x.Element("Aliases"))).ToList();
            var refs = references.Where(x => !x.Key.StartsWith("Gaode.", StringComparison.Ordinal) && x.Key != "VirtualPlc.dll")
                .Select(x => x.Value).ToList();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            void AddProject(string path, string? alias)
            {
                if (!visited.Add(path)) return;
                refs.Add(Compile(path).ToMetadataReference(aliases: alias is null ? default : [alias]));
                foreach (var dependency in projectReferences[path]) AddProject(dependency.Path, dependency.Alias);
            }
            foreach (var dependency in projectReferences[project]) AddProject(dependency.Path, dependency.Alias);
            var compilation = CSharpCompilation.Create(ownName, trees,
                refs,
                new CSharpCompilationOptions(sources.Any(x => Path.GetFileName(x) == "Program.cs") ? OutputKind.ConsoleApplication : OutputKind.DynamicallyLinkedLibrary));
            compilations[project] = compilation;
            sourceSets[project] = trees;
            return compilation;
        }
        foreach (var project in Directory.EnumerateFiles(Path.Combine(workspace, "backend"), "*.csproj", SearchOption.AllDirectories).Where(x => !Generated(x)))
        {
            var compilation = Compile(project);
            var trees = sourceSets[Path.GetFullPath(project)];
            var relativeProject = Path.GetRelativePath(workspace, project).Replace('\\', '/');
            if (!entries.ContainsKey(relativeProject)) result.Add(new("CHECKER-INVENTORY", relativeProject, 1, 1, "New project is unclassified."));
            result.AddRange(ProtocolBoundaryChecker.CheckProject(Path.GetRelativePath(workspace, project),
                Path.GetFileNameWithoutExtension(project), projectReferences[Path.GetFullPath(project)]
                    .Select(x => Path.GetFileNameWithoutExtension(x.Path)),
                XDocument.Load(project).Descendants("PackageReference").Select(x => (string)x.Attribute("Include")!)));
            foreach (var tree in trees.Where(x => x.FilePath is not ("__implicit_usings.cs" or "__project_attributes.cs")))
            {
                var relative = Path.GetRelativePath(workspace, tree.FilePath).Replace('\\', '/');
                var projectDocument = XDocument.Load(project);
                var testProject = projectDocument.Descendants("IsTestProject").Any(x => x.Value == "true") &&
                    projectDocument.Descendants("PackageReference").Any(x => (string?)x.Attribute("Include") == "Microsoft.NET.Test.Sdk");
                sourceModels.Add((new(relative, entries.GetValueOrDefault(relative, "unclassified"), tree, testProject), compilation.GetSemanticModel(tree)));
                if (!entries.TryGetValue(relative, out var role))
                {
                    result.Add(new("CHECKER-INVENTORY", relative, 1, 1, "Source file not classified; cannot omit it from the boundary scan."));
                    continue;
                }
                var registeredRole = role;
                // Mixed tests are protected until their original assertions are explicitly split.
                if (role is "mixed-test-pending-split" or "integration-test-pending-split") role = "business-test";
                role = ProtocolBoundaryChecker.ProtectedRole(relative, role);
                scanned[relative] = new { path = relative, registeredRole, effectiveRole = role,
                    sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(tree.FilePath))) };
                result.AddRange(checker.Check(new(relative, role, tree), compilation.GetSemanticModel(tree)));
            }
        }
        foreach (var (path, role) in entries.Where(x => x.Value == "normative-contract"))
        {
            scanned[path] = new { path, registeredRole = role, effectiveRole = role,
                sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(workspace, path)))) };
            result.AddRange(checker.CheckContract(path, File.ReadAllText(Path.Combine(workspace, path))));
        }
        var evidenceRoot = Environment.GetEnvironmentVariable("GAODE_009_EVIDENCE_ROOT");
        if (!string.IsNullOrEmpty(evidenceRoot))
        {
            var allowed = Path.GetFullPath(Path.Combine(workspace, "artifacts/recipe-execution-008/009-isolation")) + Path.DirectorySeparatorChar;
            evidenceRoot = Path.GetFullPath(evidenceRoot);
            if (!evidenceRoot.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) &&
                !evidenceRoot.StartsWith(Path.GetFullPath(Path.Combine(workspace, "artifacts/recipe-execution-010")) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Evidence outside controlled root");
            Directory.CreateDirectory(evidenceRoot);
            var output = Path.Combine(evidenceRoot, "csharp-boundary.json");
            using var file = new FileStream(output, FileMode.CreateNew, FileAccess.Write);
            JsonSerializer.Serialize(file, new { schemaVersion = "009-csharp-boundary/1", ruleSetVersion = ProtocolBoundaryChecker.Version,
                runId = Environment.GetEnvironmentVariable("GAODE_009_RUN_ID"), files = scanned.Values,
                result = result.Count == 0 ? "Passed" : "Rejected", violations = result }, new JsonSerializerOptions { WriteIndented = true });
        }
        return result;
    }

    private static bool Generated(string path) => path.Split(Path.DirectorySeparatorChar).Any(x => x is "obj" or "bin");
    private static string FindWorkspace()
    {
        for (var path = new DirectoryInfo(AppContext.BaseDirectory); path is not null; path = path.Parent)
            if (File.Exists(Path.Combine(path.FullName, "global.json"))) return path.FullName;
        throw new InvalidOperationException("Workspace not found");
    }
}
