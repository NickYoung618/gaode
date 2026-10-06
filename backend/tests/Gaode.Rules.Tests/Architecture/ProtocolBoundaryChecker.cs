using System.Collections.Immutable;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Gaode.Rules.Tests.Architecture;

// A finite project rule set. The same semantic walk is used for repository trees and fixtures.
public sealed record BoundaryViolation(string RuleId, string Path, int Line, int Column, string Message);
public sealed record BoundaryInput(string Path, string Role, SyntaxTree Tree, bool TestProject = false);

public sealed class ProtocolBoundaryChecker
{
    public const string Version = "009-boundary/6";
    private static readonly HashSet<string> DiagnosticTypes = new(StringComparer.Ordinal)
    {
        "Gaode.Infrastructure.Persistence.RawExchange",
        "Gaode.Infrastructure.Persistence.RawHttpExchange",
        "Gaode.Infrastructure.Persistence.CommunicationEvidenceBatch",
        "Gaode.Infrastructure.Persistence.CommunicationEvidenceReceipt",
        "Gaode.Infrastructure.Persistence.EvidenceCommitResult",
        "Gaode.Infrastructure.Persistence.CommunicationEvidenceStore",
        "Gaode.Infrastructure.Persistence.CommunicationEvidenceEntity",
        "Gaode.Infrastructure.Persistence.CommunicationEvidenceReader",
        "Gaode.Infrastructure.Persistence.CommunicationEvidenceDocument",
        "Gaode.Application.Ports.PlcStageProtocolContract",
        "Gaode.Application.Ports.InspectionHandshake"
    };
    private static readonly HashSet<string> RawMembers = new(StringComparer.OrdinalIgnoreCase)
    {
        "AlarmBits", "AlarmSeverity", "InspectionStatus", "ZResetStatus", "FlipStatus",
        "PalletLockStatus", "ProtocolStatus", "SortingExecutionStatus", "SortingAckCleared",
        "ReliableFeedback", "RawBytes", "RawWords", "Registers", "Coils", "DocumentNumber",
        "PduOffset", "RegisterAddress", "AckCleared", "FailureOrigin", "PlcSystemFault",
        "AcceptedWriteCount"
    };
    private static readonly HashSet<string> TransportCalls = new(StringComparer.Ordinal)
    {
        "ReadRegistersAsync", "ReadHoldingRegistersAsync", "WriteRegisterAsync", "WriteRegistersAsync",
        "ReadCoilsAsync", "WriteCoilAsync", "WriteCoilsAsync", "ReadHoldingRegisters", "ReadRegisters"
    };
    private readonly IReadOnlyDictionary<string, HashSet<string>> publicShapes;
    // Only these formal DI/lifecycle types are needed by Host composition. Their raw
    // members and arbitrary calls are still rejected; this is not a namespace exemption.
    private static readonly HashSet<string> WiringTypes = new(StringComparer.Ordinal) {
        "Gaode.Infrastructure.Devices.Plc.LatestProtocolPlcDevice",
        "Gaode.Infrastructure.Devices.Plc.LatestProtocolStageActionAdapter",
        "Gaode.Infrastructure.Devices.Plc.PlcRuntimeOptions",
        "Gaode.Infrastructure.Devices.Plc.CommunicationEvidenceRecorder"
    };
    private static readonly HashSet<string> RuntimeOptionAssignments = new(StringComparer.Ordinal) {
        "Provider", "Host", "Port", "UnitId", "NgCapacity", "PendingCapacity", "ZResetTimeoutMs",
        "IoTimeoutMs", "HeartbeatTimeoutMs", "ActionsEnabled"
    };

    public ProtocolBoundaryChecker(IReadOnlyDictionary<string, HashSet<string>> shapes) => publicShapes = shapes;

    // Actual business roots and direct semantic consumers cannot opt out by
    // changing a registry label. The one diagnostic route remains read-only.
    public static string ProtectedRole(string path, string registeredRole)
    {
        path = path.Replace('\\', '/');
        if (path.StartsWith("backend/src/Gaode.Application/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("backend/src/Gaode.Domain/", StringComparison.OrdinalIgnoreCase)) return "business";
        if (path.StartsWith("backend/src/Gaode.Host/", StringComparison.OrdinalIgnoreCase))
            return path == "backend/src/Gaode.Host/Api/CommunicationDiagnosticEndpoints.cs"
                ? "communication-diagnostics" : "composition-api";
        if (path.StartsWith("backend/src/Gaode.Infrastructure/Recipes/", StringComparison.OrdinalIgnoreCase) ||
            path is "backend/src/Gaode.Infrastructure/Simulation/IntegratedDetectionPort.cs" or
                "backend/src/Gaode.Infrastructure/Simulation/SimulatedDeviceState.cs" or
                "backend/src/Gaode.Infrastructure/Integrations/NotIntegratedStagePorts.cs") return "business-orchestration";
        return registeredRole;
    }

    public IReadOnlyList<BoundaryViolation> Check(BoundaryInput input, SemanticModel model)
    {
        var violations = new List<BoundaryViolation>();
        var seen = new HashSet<(string, int)>();
        var root = input.Tree.GetRoot();
        var business = input.Role is "business" or "business-test" or "business-orchestration" or "composition-api";
        var communication = input.Role == "communication-implementation";
        if (!business && !communication) return violations;
        void Reject(string rule, SyntaxNode node, string message)
        {
            if (!seen.Add((rule, node.SpanStart))) return;
            var position = input.Tree.GetLineSpan(node.Span).StartLinePosition;
            violations.Add(new(rule, input.Path, position.Line + 1, position.Character + 1, message));
        }
        foreach (var error in model.GetDiagnostics().Where(x => x.Severity == DiagnosticSeverity.Error))
        {
            var node = root.FindNode(error.Location.SourceSpan);
            Reject("CHECKER-BIND", node, error.ToString());
        }
        var tainted = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var symbols = new Dictionary<SyntaxNode, ISymbol?>();
        ISymbol? Symbol(SyntaxNode node)
        {
            if (!symbols.TryGetValue(node, out var symbol)) symbols[node] = symbol = model.GetSymbolInfo(node).Symbol;
            return symbol;
        }
        bool IsRawSymbol(ISymbol? symbol)
        {
            if (symbol is null) return false;
            if (tainted.Contains(symbol) || RawMembers.Contains(symbol.Name)) return true;
            var valueType = symbol switch
            {
                IParameterSymbol parameter => parameter.Type,
                ILocalSymbol local => local.Type,
                IPropertySymbol property => property.Type,
                IFieldSymbol field => field.Type,
                IMethodSymbol method => method.ReturnType,
                INamedTypeSymbol named => named,
                _ => null
            };
            return RawType(valueType) || RawType(symbol.ContainingType);
        }
        bool RawType(ITypeSymbol? type)
        {
            if (type is IArrayTypeSymbol array) return RawType(array.ElementType);
            if (type is not INamedTypeSymbol named) return false;
            var ns = named.ContainingNamespace.ToDisplayString();
            return ns.StartsWith("Gaode.Plc.Protocol", StringComparison.Ordinal) ||
                ns.StartsWith("Gaode.Infrastructure.Devices.Plc", StringComparison.Ordinal) ||
                ns.StartsWith("VirtualPlc", StringComparison.Ordinal) ||
                DiagnosticTypes.Contains(named.OriginalDefinition.ToDisplayString()) ||
                named.TypeArguments.Any(RawType) || RawType(named.ContainingType);
        }
        bool CompositionMethod(IMethodSymbol method) => input.Role == "composition-api" &&
            (method.ContainingNamespace.ToDisplayString() == "Microsoft.Extensions.DependencyInjection" &&
                method.Name is "AddSingleton" or "GetRequiredService" or "AddHostedService" ||
             method.ContainingType.ToDisplayString() == "Gaode.Infrastructure.Devices.Plc.LatestProtocolPlcDevice" &&
                method.Name is "StartAsync" or "DisposeAsync");
        bool WiringUse(SyntaxNode node)
        {
            if (input.Role != "composition-api" || Symbol(node) is not { } symbol || RawMembers.Contains(symbol.Name)) return false;
            if (symbol is IMethodSymbol method) return CompositionMethod(method);
            if (symbol is INamedTypeSymbol named && WiringTypes.Contains(named.ToDisplayString()))
            {
                if (node.Ancestors().OfType<ParameterSyntax>().Any(p => p.Type?.Span.Contains(node.Span) == true)) return true;
                if (node.Ancestors().OfType<ObjectCreationExpressionSyntax>().Any(c => c.Type.Span.Contains(node.Span))) return true;
                if (node.Ancestors().OfType<TypeArgumentListSyntax>().Any(a => a.Parent is GenericNameSyntax generic &&
                    Symbol(generic) is IMethodSymbol genericMethod && CompositionMethod(genericMethod))) return true;
                if (node.Parent is VariableDeclarationSyntax declaration && declaration.Type == node &&
                    declaration.Variables.All(v => v.Initializer?.Value is ObjectCreationExpressionSyntax c &&
                        model.GetTypeInfo(c).Type is { } t && WiringTypes.Contains(t.ToDisplayString()))) return true;
            }
            if (symbol is IPropertySymbol option && option.ContainingType.ToDisplayString() == "Gaode.Infrastructure.Devices.Plc.PlcRuntimeOptions" &&
                RuntimeOptionAssignments.Contains(option.Name) && node.Parent is AssignmentExpressionSyntax assignment && assignment.Left == node &&
                assignment.Parent is InitializerExpressionSyntax) return true;
            if (node.Parent is MemberAccessExpressionSyntax access && access.Expression == node &&
                Symbol(access) is IMethodSymbol call && CompositionMethod(call)) return true;
            if (node.Parent is ArgumentSyntax { Parent: ArgumentListSyntax { Parent: ObjectCreationExpressionSyntax creation } } &&
                model.GetTypeInfo(creation).Type is { } target && WiringTypes.Contains(target.ToDisplayString())) return true;
            return false;
        }
        bool Raw(SyntaxNode? node) => node is not null && node.DescendantNodesAndSelf().Any(n =>
            n is ExpressionSyntax && IsRawSymbol(Symbol(n)) && !WiringUse(n));

        void CheckShape(ITypeSymbol value, SyntaxNode origin, HashSet<ITypeSymbol> visited, string? memberPath = null)
        {
            if (value is IArrayTypeSymbol array)
            {
                var imagePayload = memberPath is "Gaode.Application.Ports.CaptureEvent.Buffer" or
                    "Gaode.Application.Ports.IMediaStore.SaveAsync.buffer";
                if (!imagePayload && (array.ElementType.SpecialType is SpecialType.System_Byte or SpecialType.System_UInt16))
                    Reject("A02", origin, "Wire byte/word array is not a semantic device contract.");
                CheckShape(array.ElementType, origin, visited);
                return;
            }
            if (!visited.Add(value)) return;
            var key = value.WithNullableAnnotation(NullableAnnotation.None).ToDisplayString();
            if (value.SpecialType == SpecialType.System_Object || value.TypeKind == TypeKind.Dynamic ||
                key is "System.Text.Json.JsonElement" or "System.Text.Json.JsonDocument")
                Reject("A02", origin, "Unbounded raw payload escape in the reachable device shape.");
            if (value is not INamedTypeSymbol named) return;
            foreach (var argument in named.TypeArguments) CheckShape(argument, origin, visited);
            var ns = named.ContainingNamespace?.ToDisplayString() ?? "";
            if (!ns.StartsWith("Gaode.", StringComparison.Ordinal)) return;
            if (IsRawSymbol(named)) Reject("A02", origin, "Protocol type reachable through a semantic contract.");
            if (!publicShapes.TryGetValue(key, out var allowed) &&
                !(named.IsGenericType && publicShapes.TryGetValue(named.OriginalDefinition.ToDisplayString(), out allowed)))
            {
                Reject("A02", origin, $"Unreviewed reachable public type: {key}");
                return;
            }
            foreach (var member in named.GetMembers().Where(x => x.DeclaredAccessibility == Accessibility.Public))
            {
                if (member is IMethodSymbol { MethodKind: MethodKind.Ordinary, IsImplicitlyDeclared: false } method)
                {
                    if (!allowed.Contains(member.Name)) Reject("A02", origin, $"Unreviewed reachable method: {key}.{member.Name}");
                    CheckShape(method.ReturnType, origin, visited);
                    foreach (var parameter in method.Parameters) CheckShape(parameter.Type, origin, visited, key+"."+method.Name+"."+parameter.Name);
                    continue;
                }
                ITypeSymbol? memberType = member switch
                {
                    IPropertySymbol property => property.Type,
                    IFieldSymbol field when !field.IsImplicitlyDeclared => field.Type,
                    _ => null
                };
                if (memberType is null) continue;
                if (RawMembers.Contains(member.Name) || !allowed.Contains(member.Name))
                    Reject("A02", origin, $"Unreviewed reachable member: {key}.{member.Name}");
                CheckShape(memberType, origin, visited, key+"."+member.Name);
            }
        }

        // Fixed point across locals, members, parameters and returns within this file.
        // Unknown calls receiving tainted data are rejected below, so they cannot hide the provenance.
        bool changed;
        do
        {
            changed = false;
            foreach (var declaration in root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
                if (Raw(declaration.Initializer?.Value) && model.GetDeclaredSymbol(declaration) is { } symbol)
                    changed |= tainted.Add(symbol);
            foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
                if (Raw(assignment.Right) && model.GetSymbolInfo(assignment.Left).Symbol is { } symbol)
                    changed |= tainted.Add(symbol);
            foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                if ((Raw(method.ExpressionBody?.Expression) || method.DescendantNodes().OfType<ReturnStatementSyntax>().Any(x => Raw(x.Expression))) &&
                    model.GetDeclaredSymbol(method) is { } symbol)
                    changed |= tainted.Add(symbol);
            foreach (var call in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
                if (model.GetSymbolInfo(call).Symbol is IMethodSymbol target)
                    for (var i = 0; i < Math.Min(target.Parameters.Length, call.ArgumentList.Arguments.Count); i++)
                        if (Raw(call.ArgumentList.Arguments[i].Expression)) changed |= tainted.Add(target.Parameters[i]);
        } while (changed);

        foreach (var node in root.DescendantNodes())
        {
            // Finite repository wire identities, evaluated as C# constants (including
            // constant concatenation), not a text search or a ban on business strings.
            // Actual producer versions may pass through an opaque semantic property.
            if (business && node is ExpressionSyntax constant && model.GetConstantValue(constant) is { HasValue: true, Value: string value } &&
                (value.StartsWith("plc-upper-", StringComparison.Ordinal) || value.StartsWith("modbus://", StringComparison.Ordinal) ||
                 value is "FlipAckCleared" or "ManualFlipCompletionCleared" or "ManualCompletionAndClear" or
                    "DetectionResetConfirmed" or "RescanResetConfirmed" or "ThreeDHandshakeIncomplete" or "FHandshakeIncomplete"))
                Reject(input.Role == "business-test" ? "A07" : "A04", constant,
                    "Business source hardcodes a wire identity, raw transport reference or internal handshake fact.");
            if (business && node is UsingDirectiveSyntax use && use.Name is { } name)
            {
                var ns = model.GetSymbolInfo(name).Symbol?.ToDisplayString() ?? name.ToString();
                if (ns.StartsWith("Gaode.Plc.Protocol", StringComparison.Ordinal) || ns.StartsWith("VirtualPlc", StringComparison.Ordinal) ||
                    (input.Role == "business-test" && ns.StartsWith("Gaode.Infrastructure", StringComparison.Ordinal)))
                    Reject(input.Role == "business-test" ? "A07" : "A01", use, "Business source depends on communication implementation.");
            }
            if (business && node is IdentifierNameSyntax identifier && IsRawSymbol(model.GetSymbolInfo(identifier).Symbol) && !WiringUse(identifier))
                Reject(identifier.Identifier.ValueText.Contains("Evidence", StringComparison.Ordinal) ? "A05" : "A04",
                    identifier, "Raw protocol/diagnostic symbol reaches protected business source.");
            if (business && node is BinaryExpressionSyntax binary && Raw(binary))
                Reject("A03", binary, "Raw provenance used in comparison, arithmetic or bit operation.");
            if (business && node is CastExpressionSyntax cast && Raw(cast.Expression))
                Reject("A03", cast, "Protocol value cast does not create business semantics.");
            if (business && node is ElementAccessExpressionSyntax element && Raw(element.Expression))
                Reject("A03", element, "Raw layout/offset is interpreted by business code.");
            if (business && node is SwitchStatementSyntax statement && Raw(statement.Expression))
                Reject("A03", statement, "Raw value drives business switch.");
            if (business && node is SwitchExpressionSyntax expression && Raw(expression.GoverningExpression))
                Reject("A03", expression, "Raw value drives business switch.");
            if (node is InvocationExpressionSyntax invocation && model.GetSymbolInfo(invocation).Symbol is IMethodSymbol called)
            {
                if (business && (TransportCalls.Contains(called.Name) || called.ContainingType.Name.Contains("ProtocolContract", StringComparison.Ordinal)))
                    Reject("A04", invocation, "Business must not access wire signals.");
                if (communication && TransportCalls.Contains(called.Name) &&
                    !input.Path.EndsWith("/PlcSignalAccessor.cs", StringComparison.Ordinal) &&
                    !input.Path.EndsWith("/ModbusTcpClient.cs", StringComparison.Ordinal))
                    Reject("A06", invocation, "Formal adapter bypasses named signal accessor.");
                if (business && !CompositionMethod(called) && invocation.ArgumentList.Arguments.Any(x => Raw(x.Expression)))
                    Reject(called.ContainingType.Name.Contains("Json", StringComparison.Ordinal) ? "A05" : "A03",
                        invocation, "Raw data passed through helper/call; wrapping does not remove its provenance.");
                if (business && (called.Name is "GetProperty" or "TryGetProperty" or "GetValueOrDefault") &&
                    invocation.ArgumentList.Arguments.Any(x => x.Expression is LiteralExpressionSyntax literal && RawMembers.Contains(literal.Token.ValueText)))
                    Reject("A05", invocation, "Business reads a raw diagnostic field from a payload.");
                if (business && (called.Name is "Parse" or "Deserialize") &&
                    called.ContainingNamespace.ToDisplayString().StartsWith("System.Text.Json", StringComparison.Ordinal) &&
                    invocation.ArgumentList.Arguments.Any(x => x.DescendantNodes().OfType<IdentifierNameSyntax>()
                        .Any(y => y.Identifier.ValueText.Contains("EvidenceReference", StringComparison.OrdinalIgnoreCase))))
                    Reject("A05", invocation, "An opaque diagnostic reference cannot be decoded by business code.");
                if (communication && called.ContainingType.ToDisplayString() == "System.BitConverter" &&
                    called.Name is "Int32BitsToSingle" or "SingleToInt32Bits")
                    Reject("A06", invocation, "Float wire representation must use the single protocol codec.");
            }
            if (business && node is BaseTypeDeclarationSyntax declaration && model.GetDeclaredSymbol(declaration) is INamedTypeSymbol type)
            {
                var key = type.ToDisplayString();
                var deviceShape = type.ContainingNamespace.ToDisplayString() == "Gaode.Application.Ports" ||
                    type.Name is "StartupDiagnostic" or "StartupReliableFeedback";
                if (!deviceShape) continue;
                if (!publicShapes.TryGetValue(key, out var allowed))
                {
                    Reject("A02", declaration, $"Unreviewed public port type: {key}");
                    continue;
                }
                CheckShape(type, declaration, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));
                foreach (var member in type.GetMembers().Where(x => !x.IsImplicitlyDeclared && x.DeclaredAccessibility == Accessibility.Public))
                {
                    if (member is IMethodSymbol { MethodKind: not MethodKind.Ordinary }) continue;
                    if (member.Kind is not (SymbolKind.Property or SymbolKind.Field or SymbolKind.Method)) continue;
                    if (RawMembers.Contains(member.Name) || !allowed.Contains(member.Name))
                        Reject("A02", member.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() ?? declaration,
                            $"Unreviewed or raw public field/member: {key}.{member.Name}");
                    if (member is IMethodSymbol method)
                    {
                        CheckShape(method.ReturnType, declaration, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));
                        foreach (var parameter in method.Parameters)
                            CheckShape(parameter.Type, declaration, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default), key+"."+method.Name+"."+parameter.Name);
                    }
                }
                // Positional record properties are implicit in Roslyn, but still public contract fields.
                if (declaration is RecordDeclarationSyntax record && record.ParameterList is { } parameters)
                    foreach (var parameter in parameters.Parameters)
                        if (RawMembers.Contains(parameter.Identifier.ValueText) || !allowed.Contains(parameter.Identifier.ValueText))
                            Reject("A02", parameter, $"Unreviewed or raw record field: {key}.{parameter.Identifier.ValueText}");
            }
        }
        return violations;
    }

    public static IReadOnlyDictionary<string, HashSet<string>> LoadShapes(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("allowedPublicShapes").EnumerateObject()
            .ToDictionary(x => x.Name, x => x.Value.EnumerateArray().Select(v => v.GetString()!).ToHashSet(StringComparer.Ordinal));
    }

    public IReadOnlyList<BoundaryViolation> CheckContract(string path, string text)
    {
        var found = new List<BoundaryViolation>();
        var businessInterface = false;
        var sample = false;
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.StartsWith('#'))
                businessInterface = line.Contains("业务接口", StringComparison.Ordinal) ||
                    line.Contains("业务端口字段", StringComparison.Ordinal) || line.Contains("可见数据", StringComparison.Ordinal) ||
                    line.Contains("端口职责", StringComparison.Ordinal) || line.Contains("语义字段", StringComparison.Ordinal);
            if (businessInterface && line.StartsWith("```", StringComparison.Ordinal)) { sample = !sample; continue; }
            if (businessInterface && sample)
            {
                // Only normative request/type examples. Historical/prohibition prose is not scanned as an interface.
                var tokens = SyntaxFactory.ParseTokens(line);
                if (tokens.Any(t => t.IsKind(SyntaxKind.IdentifierToken) && RawMembers.Contains(t.ValueText) ||
                                    t.IsKind(SyntaxKind.StringLiteralToken) && RawMembers.Contains(t.ValueText)))
                    found.Add(new("A07", path, i + 1, 1, "Normative business request/type example exposes a raw field."));
            }
            if (!businessInterface || !line.StartsWith('|')) continue;
            var columns = line.Split('|', StringSplitOptions.TrimEntries);
            if (columns.Length > 2 && RawMembers.Contains(columns[1].Trim('`')))
                found.Add(new("A07", path, i + 1, 1, "Normative business interface exposes a raw protocol field."));
        }
        return found;
    }

    public static IReadOnlyList<BoundaryViolation> CheckProject(string path, string project, IEnumerable<string> references,
        IEnumerable<string> packages)
    {
        var expected = project switch
        {
            "Gaode.Domain" => Array.Empty<string>(),
            "Gaode.Application" => ["Gaode.Domain"],
            "Gaode.Infrastructure" => ["Gaode.Application", "Gaode.Plc.Protocol"],
            "Gaode.Host" => ["Gaode.Application", "Gaode.Infrastructure"],
            "VirtualPlc" => ["Gaode.Plc.Protocol"],
            "Gaode.Plc.Protocol" => Array.Empty<string>(),
            _ => null
        };
        if (expected is null) return [];
        var actual = references.Order(StringComparer.Ordinal).ToArray();
        var violations = new List<BoundaryViolation>();
        if (!actual.SequenceEqual(expected.Order(StringComparer.Ordinal)))
            violations.Add(new("A01", path, 1, 1, $"Expected direct dependencies [{string.Join(',', expected)}], observed [{string.Join(',', actual)}]."));
        if (project == "Gaode.Plc.Protocol" && packages.Any())
            violations.Add(new("A01", path, 1, 1, "Pure protocol definitions cannot have package dependencies."));
        return violations;
    }

    public static ImmutableArray<MetadataReference> RuntimeReferences() =>
        ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? throw new InvalidOperationException("Reference assemblies unavailable"))
        .Split(Path.PathSeparator).Distinct(StringComparer.OrdinalIgnoreCase)
        .Select(x => MetadataReference.CreateFromFile(x)).ToImmutableArray<MetadataReference>();
}
