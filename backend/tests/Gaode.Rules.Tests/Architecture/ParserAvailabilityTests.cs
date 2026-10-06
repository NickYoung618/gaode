using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Gaode.Rules.Tests.Architecture;

public sealed class ParserAvailabilityTests
{
    [Fact]
    public void PinnedRoslynParsesAndBindsRealSymbols()
    {
        Assert.Equal(new Version(5, 9, 0, 0), typeof(Compilation).Assembly.GetName().Version);
        Assert.Equal(new Version(5, 9, 0, 0), typeof(CSharpCompilation).Assembly.GetName().Version);
        var tree = CSharpSyntaxTree.ParseText("public class Sample { public int Slot => 3; }");
        var compilation = CSharpCompilation.Create("parser-proof", [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.Empty(compilation.GetDiagnostics().Where(x => x.Severity == DiagnosticSeverity.Error));
        Assert.NotNull(compilation.GetTypeByMetadataName("Sample")?.GetMembers("Slot").Single());
    }
}
