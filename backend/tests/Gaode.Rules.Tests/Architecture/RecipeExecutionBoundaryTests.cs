using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using Xunit.Abstractions;

namespace Gaode.Rules.Tests.Architecture;

public sealed class RecipeExecutionBoundaryTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("N02-return-direct")]
    [InlineData("N02-return-helper-alias")]
    public void EnvironmentalEarlyReturnIsRejected(string sample)
    {
        var condition = sample == "N02-return-direct" ? "purpose == \"Production\"" : "selected";
        var source = $$"""
            interface IDetectionPort { void ExecuteAsync(); }
            class RecipeDetectionExecutor
            {
                static bool RequiresApproval(string purpose) => purpose == "Production";
                void Run(string purpose, IDetectionPort detection)
                {
                    var selected = RequiresApproval(purpose);
                    if ({{condition}})
                        return;
                    detection.ExecuteAsync();
                }
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(source, path: "backend/src/execution.cs");
        var compilation = CSharpCompilation.Create("010EarlyReturn", [tree], ProtocolBoundaryChecker.RuntimeReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.True(errors.Length == 0, string.Join(Environment.NewLine, errors.Select(d => d.ToString())));
        var model = compilation.GetSemanticModel(tree);
        var calls = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().ToArray();
        var symbols = calls.Select(c => model.GetSymbolInfo(c).Symbol as IMethodSymbol).ToArray();
        Assert.Contains(symbols, s => s?.Name == "ExecuteAsync" && s.ContainingType.Name == "IDetectionPort");
        Assert.Contains(symbols, s => s?.Name == "RequiresApproval" && s.ReturnType.SpecialType == SpecialType.System_Boolean);
        Assert.DoesNotContain(symbols, s => s is null);
        var findings = new RecipeExecutionBoundaryChecker().Check([
            (new BoundaryInput(tree.FilePath, "unclassified", tree), model)]).Violations;
        output.WriteLine($"sample={sample}; compilationErrors=0; boundCalls={string.Join(",", symbols.Select(s => s!.ToDisplayString()))}; rules={string.Join(",", findings.Select(f => f.RuleId))}; locations={string.Join(",", findings.Select(f => $"{f.Path}:{f.Line}:{f.Column}"))}");
        var at = tree.GetLineSpan(tree.GetRoot().DescendantNodes().OfType<IfStatementSyntax>().Single().Condition.Span).StartLinePosition;
        Assert.True(findings.Any(f => f.RuleId == "B02" && f.Path == tree.FilePath && f.Line == at.Line + 1 &&
            f.Column == at.Character + 1 && !string.IsNullOrWhiteSpace(f.Message)), "Expected B02 at the bound environmental condition; sample compiled and all calls bound successfully.");
    }

    [Theory]
    [InlineData("P-return-throw")]
    [InlineData("P-return-rejected-result")]
    [InlineData("P-return-origin-log")]
    [InlineData("P-return-business")]
    public void LegitimateEarlyReturnAndOriginUseRemainAllowed(string sample)
    {
        var body = sample switch {
            "P-return-throw" => "if (purpose == \"Production\") throw new System.InvalidOperationException(\"ApprovalMissing\"); detection.ExecuteAsync();",
            "P-return-origin-log" => "if (purpose == \"Production\") System.Console.WriteLine(purpose); detection.ExecuteAsync();",
            "P-return-business" => "if (isComplete) return; detection.ExecuteAsync();",
            "P-return-rejected-result" => "if (purpose == \"Production\") return new(false, true, \"ApprovalMissing\"); detection.ExecuteAsync(); return new(true, false, \"Admitted\");",
            _ => throw new ArgumentOutOfRangeException(nameof(sample))
        };
        var resultType = sample == "P-return-rejected-result"
            ? "Gaode.Application.Station01.ThreeDAndFRecipeGateDecision" : "void";
        Assert.Empty(Check(("backend/src/execution.cs", "unclassified",
            $"interface IDetectionPort {{ void ExecuteAsync(); }} class RecipeDetectionExecutor {{ {resultType} Run(string purpose, bool isComplete, IDetectionPort detection) {{ {body} }} }}")));
    }

    [Theory]
    [InlineData("PausedForManualReview",false)]
    [InlineData("StagesCompleted",true)]
    [InlineData("UnitCompleted",true)]
    public void TypedStageRefusalRemainsDistinctFromSuccessfulEarlyExit(string status,bool rejected)
    {
        var source=$$"""
            interface IDetectionPort { void ExecuteAsync(); }
            class RecipeDetectionExecutor {
              Gaode.Application.Workflow.ThreeStageExecutionResult Run(string purpose,IDetectionPort detection) {
                if(purpose=="Production") return new(Gaode.Application.Workflow.ThreeStageExecutionStatus.{{status}},
                  Gaode.Application.Ports.WholeTrayWorkflowStage.Detection,"MechanicalBasisMissing",
                  System.Array.Empty<Gaode.Application.Workflow.PlcActionAssociation>(),null);
                detection.ExecuteAsync();
                return new(Gaode.Application.Workflow.ThreeStageExecutionStatus.StagesCompleted,
                  Gaode.Application.Ports.WholeTrayWorkflowStage.Detection,null,
                  System.Array.Empty<Gaode.Application.Workflow.PlcActionAssociation>(),null);
              }
            }
            """;
        var findings=Check(("backend/src/execution.cs","unclassified",source));
        if(rejected)Assert.Contains(findings,f=>f.RuleId=="B02");else Assert.Empty(findings);
    }

    private static readonly Lazy<(IReadOnlyList<BoundaryViolation> Violations, IReadOnlyList<string> Closure)> Repository = new(() => {
        var sources = ProtocolRepositoryBoundaryTests.SourceModels;
        var result = new RecipeExecutionBoundaryChecker().Check(sources);
        var errors = result.Violations.Concat(ProtocolRepositoryBoundaryTests.CoverageFailures).ToList();
        foreach (var name in new[] { "RecipeDetectionExecutor", "StartPublicPreparation", "RecipeRunPlanner", "ThreeStageWorkflowExecutor", "CoordinateResolver",
            "RecipeEndpoints", "CommittedRecipePlanReader", "RecipeDefinitionSerialization",
            "RecipeDefinitionIdentity", "RecipeDefinitionValidator", "RecipeMatcher", "SqliteRecipeStore" })
            if (!result.Closure.Any(x => x.EndsWith("." + name, StringComparison.Ordinal)))
                errors.Add(new("B05", "repository", 1, 1, "Required business root missing: " + name));
        if (sources.Count == 0) errors.Add(new("B05", "repository", 1, 1, "No source enumerated"));
        if (Environment.GetEnvironmentVariable("GAODE_010_EVIDENCE_ROOT") is { Length: > 0 } evidence)
        {
            var root = Path.GetFullPath(evidence);
            var workspace = FindWorkspace();
            if (!root.StartsWith(Path.Combine(workspace, "artifacts", "recipe-execution-010") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("010 evidence root required");
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "recipe-boundary.json"), JsonSerializer.Serialize(new {
                schemaVersion = "010-recipe-boundary/1", checker = RecipeExecutionBoundaryChecker.Version,
                runId = Environment.GetEnvironmentVariable("GAODE_010_ATTEMPT"),
                files = sources.Select(s => s.Input.Path).Distinct().Order(), closure = result.Closure,
                violations = errors, result = errors.Count == 0 ? "Passed" : "Rejected" }, new JsonSerializerOptions { WriteIndented = true }));
        }
        return (errors, result.Closure);
    });

    [Theory]
    [InlineData("B01")]
    [InlineData("B02")]
    [InlineData("B03")]
    [InlineData("B04")]
    [InlineData("B05")]
    public void CurrentBusinessClosureSatisfiesRule(string rule)
    {
        var failures = Repository.Value.Violations.Where(v => v.RuleId == rule || v.RuleId.StartsWith("CHECKER", StringComparison.Ordinal)).ToArray();
        Assert.True(failures.Length == 0, string.Join(Environment.NewLine, failures.Take(30)) + $"; total={failures.Length}");
    }

    [Theory]
    [InlineData("N01a", "B01")]
    [InlineData("N01b", "B01")]
    [InlineData("N01c", "B01")]
    [InlineData("N02", "B02")]
    [InlineData("N03a", "B03")]
    [InlineData("N03b", "B03")]
    [InlineData("N04", "B04")]
    [InlineData("N05a", "B01")]
    [InlineData("N05b", "B02")]
    public void IllegalArchitectureIsRejectedByTheRepositoryChecker(string sample, string requiredRule)
    {
        var main = sample switch {
            "N01a" => "class RecipeDetectionExecutor { void Execute() => new Gaode.Infrastructure.Simulation.Camera().Read(); } namespace Gaode.Infrastructure.Simulation { class Camera { public void Read() {} } }",
            "N01b" => "class RecipeDetectionExecutor { bool Required(string recipeId) => recipeId == \"Q02\"; }",
            "N01c" or "N05a" => "class RecipeDetectionExecutor { public int Run(System.Text.Json.JsonElement input) => Moved.Read(input); }",
            "N02" or "N05b" => "interface IDetectionPort { void ExecuteAsync(); } class RecipeDetectionExecutor { void Run(IDetectionPort d) { var selected=Moved.UseEnvironment(); if (selected) Proceed(d); } void Proceed(IDetectionPort d) => d.ExecuteAsync(); }",
            "N03a" => "public record DetectionRequest(string WorkerScriptPath, string TestSourceReference);",
            "N03b" => "public record Wrapper(System.Collections.Generic.Dictionary<string,System.Text.Json.JsonElement> Values); public record DetectionRequest(Wrapper Input);",
            "N04" => "interface IDetectionPort {} class SimulatedSuccess : IDetectionPort {} static class Registry { public static void AddSingleton<T,U>() {} } class Wiring { void Register() { Registry.AddSingleton<IDetectionPort,SimulatedSuccess>(); } }",
            _ => throw new ArgumentOutOfRangeException(nameof(sample))
        };
        var helper = sample is "N01c" or "N05a"
            ? "public static class Moved { public static int Read(System.Text.Json.JsonElement input) => input.GetProperty(\"geometryNodesV2\").GetArrayLength(); }"
            : "static class Moved { public static string mode = \"Virtual\"; public static bool UseEnvironment() => mode == \"Virtual\"; }";
        var findings = CheckCore(sample == "N05a", ("backend/src/main.cs", "business", main),
            (sample == "N05a" ? "linked/project/Elsewhere.cs" : "backend/src/helper.cs", sample == "N05b" ? "adapter" : "unclassified", helper));
        Assert.Contains(findings, f => f.RuleId == requiredRule && f.Line > 0 && f.Column > 0 && !string.IsNullOrWhiteSpace(f.Message));
    }

    [Theory]
    [InlineData("P01")]
    [InlineData("P02")]
    [InlineData("P03")]
    public void LegitimateOriginDecodingAndAdmissionRemainAllowed(string sample)
    {
        var source = sample switch {
            "P01" => "public enum Source { Unknown, Test, Virtual, Simulated, Real } public record DetectionRequest(Source Source, string EvidenceReference); class RecipeDetectionExecutor { public Source Observe(DetectionRequest r) => r.Source; }",
            "P02" => "interface ICapturePort { void Capture(); } class RecipeDetectionExecutor { void Run(ICapturePort camera) => camera.Capture(); } class FileImages : ICapturePort { public void Capture() { var input=System.Text.Json.JsonDocument.Parse(\"{}\"); input.RootElement.GetProperty(\"fixtureData\"); } }",
            "P03" => "class RecipeDetectionExecutor { public void Run(string purpose) { if (purpose == \"Production\") throw new System.InvalidOperationException(\"ApprovalMissing\"); } }",
            _ => throw new ArgumentOutOfRangeException(nameof(sample))
        };
        Assert.Empty(Check(("backend/src/legitimate.cs", "adapter", source)));
    }

    [Theory]
    [InlineData("store-stage", "B01")]
    [InlineData("serializer-test-identity", "B01")]
    [InlineData("authoring-unregistered-field", "B01")]
    [InlineData("recipe-unbounded-input", "B03")]
    public void CurrentSaveAndSerializationPathsCannotCreateAnotherExecutor(string sample, string rule)
    {
        var source = sample switch
        {
            "store-stage" => "interface IRecipeStore { void Save(); } interface IDetectionPort { void ExecuteAsync(); } class RecipeEndpoints { void Save(IRecipeStore store) => store.Save(); } class Store(IDetectionPort detection) : IRecipeStore { public void Save() => detection.ExecuteAsync(); }",
            "serializer-test-identity" => "namespace Gaode.Application.Recipes { class RecipeDefinitionSerialization { public bool Decode(string id) => id == \"Q02\"; } }",
            "authoring-unregistered-field" => "class RecipeEndpoints { public int ReadCandidate(System.Text.Json.JsonElement body) => body.GetProperty(\"fixtureData\").GetInt32(); }",
            "recipe-unbounded-input" => "public record RecipeDefinition(System.Collections.Generic.Dictionary<string,System.Text.Json.JsonElement> Extra);",
            _ => throw new ArgumentOutOfRangeException(nameof(sample))
        };
        var findings = Check(("backend/src/save.cs", "business-orchestration", source));
        Assert.Contains(findings, f => f.RuleId == rule && f.Line > 0 && f.Column > 0);
    }

    private static IReadOnlyList<BoundaryViolation> Check(params (string Path, string Role, string Source)[] files)
        => CheckCore(false, files);

    private static IReadOnlyList<BoundaryViolation> CheckCore(bool linkedProject, params (string Path, string Role, string Source)[] files)
    {
        var trees = files.Select(f => CSharpSyntaxTree.ParseText(f.Source, path: f.Path)).ToArray();
        if (linkedProject)
        {
            // A separate referenced compilation, not just a renamed path in one project.
            var linked = CSharpCompilation.Create("010LinkedHelper", [trees[1]], ProtocolBoundaryChecker.RuntimeReferences(),
                new CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));
            var main = CSharpCompilation.Create("010Fixture", [trees[0]],
                ProtocolBoundaryChecker.RuntimeReferences().Append(linked.ToMetadataReference()),
                new CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));
            Assert.DoesNotContain(linked.GetDiagnostics().Concat(main.GetDiagnostics()), d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
            return new RecipeExecutionBoundaryChecker().Check([
                (new BoundaryInput(files[0].Path, files[0].Role, trees[0]), main.GetSemanticModel(trees[0])),
                (new BoundaryInput(files[1].Path, files[1].Role, trees[1]), linked.GetSemanticModel(trees[1]))]).Violations;
        }
        var compilation = CSharpCompilation.Create("010Fixture", trees, ProtocolBoundaryChecker.RuntimeReferences(),
            new CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));
        Assert.Empty(compilation.GetDiagnostics().Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
        return new RecipeExecutionBoundaryChecker().Check(files.Select((f, i) =>
            (new BoundaryInput(f.Path, f.Role, trees[i]), compilation.GetSemanticModel(trees[i]))).ToArray()).Violations;
    }
    private static string FindWorkspace()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "global.json"))) return directory.FullName;
        throw new InvalidOperationException("Workspace required");
    }
}
