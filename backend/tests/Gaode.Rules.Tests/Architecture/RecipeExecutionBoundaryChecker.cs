using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Gaode.Rules.Tests.Architecture;

// Finite 010 rules over the same project/Compile-link semantic models used by 009.
// Role and directory labels are evidence only: symbol reachability determines business helpers.
public sealed class RecipeExecutionBoundaryChecker
{
    public const string Version = "010-boundary/2";
    private static readonly HashSet<string> RootNames = new(StringComparer.Ordinal) {
        "RunEndpoints", "RecipeEndpoints", "CommittedRecipePlanReader", "StartPublicPreparation", "RecipeDetectionExecutor", "RecipeRunPlanner", "RecipeDefinitionValidator",
        "CoordinateResolver", "RecipeExecutionCoordinator", "ThreeStageWorkflowExecutor", "WholeTrayWorkflowOrchestrator",
        "RecipeSortingMapper", "FaceResultAggregator", "RecipeExecutionBudget", "StageHandoffBuilder",
        "PublicPreparationHandoffV2Consumer", "FScanStep", "RecipeApplicationCoordinator", "IndependentRecipeApplication"
    };
    private static readonly HashSet<string> BoundaryNames = new(StringComparer.Ordinal) {
        "JsonRecipeCatalog", "RecipeEnvironmentDecoder", "SemanticRecipeInputProvider", "RecipeCatalogFactory",
        "ApprovedExecutionCostProvider", "CapabilityRegistration", "PublicConfigurationValidator", "ConfigurationFreezer",
        "StartRunContext", "CapabilityRegistry", "RecipeAdmission",
        // Read-only projections/history decode persisted contracts, never select a stage.
        "QueryEndpoints", "CommittedResultProjection", "RecipeApplicationHistoryReader"
    };
    private static readonly HashSet<string> LeafPorts = new(StringComparer.Ordinal) {
        "ICapturePort", "IAlgorithmPort", "IMediaStore", "ITraceWriter", "ITraceQuery", "IStageHandoffQuery",
        "IRecipeCatalog", "IRecipeStore", "IPlcStatePort", "IPlcActionPort", "IMotionPort", "IAcquisitionCyclePort", "IPhysicalHandlingPort",
        "IPlcStageActionPort", "IPlcResetPort", "IStageEventStore", "IWholeTrayCompletionStore"
    };
    private static readonly HashSet<string> ShapeRoots = new(StringComparer.Ordinal) {
        "DetectionRequest", "DetectionPortResult", "RecipeDefinition", "RecipeRunPlan", "FrozenExecutionInputs",
        "CoordinateDefinition", "DetectionStepTarget", "AuxiliaryActionRequest"
    };
    private static readonly HashSet<string> ContractFields = new(StringComparer.OrdinalIgnoreCase) {
        "kind", "frozenExecutionInputs", "schemaVersion", "executionOrigin", "AlgorithmOrigin", "CaptureFacts",
        "correlation", "actionId", "assignments", "objectId", "result", "bindingId", "operationId", "planRevision",
        "snapshotId", "capabilityVersions", "routeDeadlines", "state", "events", "receipt", "publicJson", "budgetJson", "simulationJson",
        "publicDigest", "budgetDigest", "simulationDigest",
        // 012 HTTP thin envelope; the definition body still uses the unique common decoder.
        "requestId", "definition"
    };
    private static readonly HashSet<string> ForbiddenMembers = new(StringComparer.OrdinalIgnoreCase) {
        "TestSourceReference", "WorkerScriptPath", "WorkerManifestPath", "WorkerExecutablePath", "FixtureJson",
        "FixturePayload", "PositionPayloads", "ProfilePayloads", "TestEligibleSlots", "StrictRecipeExecution", "DetectionTestMode"
    };
    private sealed record TypeSource(INamedTypeSymbol Symbol, TypeDeclarationSyntax Node, BoundaryInput Input, SemanticModel Model);

    public (IReadOnlyList<BoundaryViolation> Violations, IReadOnlyList<string> Closure) Check(
        IReadOnlyList<(BoundaryInput Input, SemanticModel Model)> sources)
    {
        var findings = new List<BoundaryViolation>();
        var seen = new HashSet<(string, string, int)>();
        void Reject(string rule, BoundaryInput input, SyntaxNode node, string message)
        {
            if (!seen.Add((rule, input.Path, node.SpanStart))) return;
            var at = input.Tree.GetLineSpan(node.Span).StartLinePosition;
            findings.Add(new(rule, input.Path, at.Line + 1, at.Character + 1, message));
        }
        var types = sources.SelectMany(s => s.Input.Tree.GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>()
            .Select(node => (s, node, symbol: s.Model.GetDeclaredSymbol(node) as INamedTypeSymbol))
            .Where(x => x.symbol is not null)
            .Select(x => new TypeSource(x.symbol!, x.node, x.s.Input, x.s.Model))).ToArray();
        var byName = types.GroupBy(t => t.Symbol.ToDisplayString()).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
        var progressMethods = new HashSet<string>(StringComparer.Ordinal);
        bool Progress(IMethodSymbol? method) => Advances(method) || method is not null && progressMethods.Contains(method.ToDisplayString());
        bool progressChanged;
        do
        {
            progressChanged = false;
            foreach (var t in types)
                foreach (var method in t.Node.Members.OfType<MethodDeclarationSyntax>())
                    if (method.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(call =>
                        Progress(t.Model.GetSymbolInfo(call).Symbol as IMethodSymbol)) && t.Model.GetDeclaredSymbol(method) is { } symbol)
                        progressChanged |= progressMethods.Add(symbol.ToDisplayString());
        } while (progressChanged);
        var closure = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>(types.Where(t => RootNames.Contains(t.Symbol.Name) ||
            t.Symbol.ContainingNamespace.ToDisplayString() == "Gaode.Application.Recipes")
            .Select(t => t.Symbol.ToDisplayString()));
        while (pending.TryDequeue(out var key))
        {
            if (!closure.Add(key) || !byName.TryGetValue(key, out var declarations)) continue;
            foreach (var t in declarations)
            {
                if (IsBoundary(t.Symbol))
                {
                    if (t.Symbol.TypeKind == TypeKind.Interface)
                        foreach (var implementation in types.Where(candidate => candidate.Symbol.AllInterfaces.Any(i =>
                            i.OriginalDefinition.ToDisplayString() == t.Symbol.OriginalDefinition.ToDisplayString())))
                            pending.Enqueue(implementation.Symbol.ToDisplayString());
                    // A leaf may decode inputs or reject admission, never take over workflow progression.
                    foreach (var call in t.Node.DescendantNodes().OfType<InvocationExpressionSyntax>())
                        if (Progress(t.Model.GetSymbolInfo(call).Symbol as IMethodSymbol))
                            Reject("B01", t.Input, call, "An input/device boundary takes over a business stage.");
                    continue;
                }
                foreach (var node in t.Node.DescendantNodes().OfType<SimpleNameSyntax>())
                {
                    var symbol = t.Model.GetSymbolInfo(node).Symbol;
                    var target = symbol as INamedTypeSymbol ?? symbol?.ContainingType;
                    if (target is not null && byName.ContainsKey(target.OriginalDefinition.ToDisplayString()))
                        pending.Enqueue(target.OriginalDefinition.ToDisplayString());
                }
            }
        }
        var business = types.Where(t => closure.Contains(t.Symbol.ToDisplayString()) && !IsBoundary(t.Symbol)).ToArray();
        var environmentMethods = new HashSet<string>(StringComparer.Ordinal);
        var environmentValues = new HashSet<string>(StringComparer.Ordinal);
        static string SymbolKey(ISymbol symbol) => symbol is ILocalSymbol or IParameterSymbol
            ? symbol.ContainingSymbol.ToDisplayString() + ":" + symbol.Name : symbol.ToDisplayString();
        static bool Scalar(ITypeSymbol? type) => type?.SpecialType is SpecialType.System_Boolean or SpecialType.System_String ||
            type?.TypeKind == TypeKind.Enum && type.Name is not ("FlipMode" or "WholeTrayWorkflowStage" or "RunState");
        bool EnvironmentValue(SyntaxNode node, SemanticModel model) => node.DescendantNodesAndSelf().OfType<ExpressionSyntax>().Any(n => {
            var symbol = model.GetSymbolInfo(n).Symbol;
            return symbol is not null && Scalar(model.GetTypeInfo(n).Type) &&
                (EnvironmentMember(symbol.Name) || environmentValues.Contains(SymbolKey(symbol)) ||
                 symbol is IMethodSymbol m && environmentMethods.Contains(m.ToDisplayString()));
        });
        bool changed;
        do
        {
            changed = false;
            foreach (var t in types)
            {
                foreach (var variable in t.Node.DescendantNodes().OfType<VariableDeclaratorSyntax>())
                    if (variable.Initializer is { } initializer && Scalar(t.Model.GetTypeInfo(initializer.Value).Type) && EnvironmentValue(initializer.Value, t.Model) &&
                        t.Model.GetDeclaredSymbol(variable) is { } local)
                        changed |= environmentValues.Add(SymbolKey(local));
                foreach (var assignment in t.Node.DescendantNodes().OfType<AssignmentExpressionSyntax>())
                    if (Scalar(t.Model.GetTypeInfo(assignment.Right).Type) && EnvironmentValue(assignment.Right, t.Model) && t.Model.GetSymbolInfo(assignment.Left).Symbol is { } target)
                        changed |= environmentValues.Add(SymbolKey(target));
            }
            foreach (var t in types)
                foreach (var method in t.Node.Members.OfType<MethodDeclarationSyntax>())
                    if (method.ReturnType.ToString() == "bool" &&
                        (method.ExpressionBody is { } expression && EnvironmentValue(expression.Expression, t.Model) ||
                         method.DescendantNodes().OfType<ReturnStatementSyntax>().Any(r => r.Expression is { } e && EnvironmentValue(e, t.Model))) &&
                        t.Model.GetDeclaredSymbol(method) is { } symbol)
                        changed |= environmentMethods.Add(symbol.ToDisplayString());
        } while (changed);
        foreach (var t in business)
        {
            foreach (var error in t.Model.GetDiagnostics(t.Node.Span).Where(d => d.Severity == DiagnosticSeverity.Error))
                Reject("B05", t.Input, t.Node, "Business semantic binding failed: " + error);
            foreach (var node in t.Node.DescendantNodes().OfType<SimpleNameSyntax>())
            {
                var symbol = t.Model.GetSymbolInfo(node).Symbol;
                var type = symbol as INamedTypeSymbol ?? symbol?.ContainingType;
                if (type is not null && (type.ContainingNamespace.ToDisplayString().StartsWith("Gaode.Infrastructure.Simulation", StringComparison.Ordinal) ||
                    type.Name is "PythonWorkerAdapter" or "WorkerProcessSupervisor" or "ContentSampleWorker"))
                    Reject("B01", t.Input, node, "Business depends on a concrete environment implementation: " + type.ToDisplayString());
                if (symbol is not null && ForbiddenMembers.Contains(symbol.Name))
                    Reject("B01", t.Input, node, "Business consumes an environment-specific field: " + symbol.Name);
            }
            foreach (var literal in t.Node.DescendantNodes().OfType<LiteralExpressionSyntax>().Where(n => n.IsKind(SyntaxKind.StringLiteralExpression)))
            {
                var value = literal.Token.ValueText;
                if (value.StartsWith("TEST-TRAY-", StringComparison.Ordinal) ||
                    System.Text.RegularExpressions.Regex.IsMatch(value, "^(R00[0-9]-|Q[0-9]{2}$|SIM_CAPTURE_|SIM_ALGORITHM)"))
                    Reject("B01", t.Input, literal, "Fixed environment recipe/capability identity in business.");
            }
            foreach (var call in t.Node.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var method = t.Model.GetSymbolInfo(call).Symbol as IMethodSymbol;
                if (method?.ContainingNamespace.ToDisplayString() == "System.Text.Json" && method.Name is "GetProperty" or "TryGetProperty")
                {
                    var field = call.ArgumentList.Arguments.FirstOrDefault()?.Expression;
                    if (field is LiteralExpressionSyntax literal && !ContractFields.Contains(literal.Token.ValueText))
                        Reject("B01", t.Input, call, "Business decodes a non-contract input field: " + literal.Token.ValueText);
                    else if (field is not LiteralExpressionSyntax && t.Symbol.Name != "IndependentRecipeApplication")
                        Reject("B01", t.Input, call, "Unbounded business JSON field decoding.");
                }
                if (method?.ContainingType.Name is "Activator" or "Assembly" && method.Name is "CreateInstance" or "Load" or "LoadFrom")
                    Reject("B05", t.Input, call, "Dynamic business helper/implementation cannot be enumerated.");
            }
            foreach (var branch in t.Node.DescendantNodes().OfType<IfStatementSyntax>())
                if (EnvironmentValue(branch.Condition, t.Model) && branch.DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .Any(call => Progress(t.Model.GetSymbolInfo(call).Symbol as IMethodSymbol)))
                    Reject("B02", t.Input, branch.Condition, "Environment identity controls business progression.");
            // The stage may be outside the if. Compare its two CFG successors,
            // rather than treating a silent normal return as an admission refusal.
            foreach (var method in t.Node.Members.OfType<MethodDeclarationSyntax>())
            {
                var conditions = method.DescendantNodes().OfType<IfStatementSyntax>()
                    .Where(b => EnvironmentValue(b.Condition, t.Model)).Select(b => b.Condition).ToArray();
                if (conditions.Length == 0 || !method.DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .Any(call => Progress(t.Model.GetSymbolInfo(call).Symbol as IMethodSymbol))) continue;
                var graph = ControlFlowGraph.Create(method, t.Model);
                if (graph is null)
                {
                    Reject("B05", t.Input, method, "Required environmental control flow could not be bound.");
                    continue;
                }
                (bool NormalExit, bool Progress) Follow(ControlFlowBranch? edge)
                {
                    var normal = false;
                    var advances = false;
                    var visited = new HashSet<int>();
                    var edges = new Queue<ControlFlowBranch>();
                    if (edge is not null) edges.Enqueue(edge);
                    while (edges.TryDequeue(out var next))
                    {
                        if (next.Semantics is ControlFlowBranchSemantics.Throw or ControlFlowBranchSemantics.Rethrow or
                            ControlFlowBranchSemantics.ProgramTermination or ControlFlowBranchSemantics.Error) continue;
                        if (next.Semantics == ControlFlowBranchSemantics.Return && ExplicitRefusal(next.Source.BranchValue)) continue;
                        var block = next.Destination;
                        if (block is null || !visited.Add(block.Ordinal)) continue;
                        if (block.Kind == BasicBlockKind.Exit) { normal = true; continue; }
                        if (block.Operations.Concat(block.BranchValue is null ? [] : new[] { block.BranchValue })
                            .Any(operation => operation.DescendantsAndSelf().OfType<IInvocationOperation>().Any(call => Progress(call.TargetMethod))))
                        { advances = true; continue; }
                        if (block.FallThroughSuccessor is { } fall) edges.Enqueue(fall);
                        if (block.ConditionalSuccessor is { } conditional) edges.Enqueue(conditional);
                    }
                    return (normal, advances);
                }
                foreach (var block in graph.Blocks.Where(b => b.IsReachable && b.ConditionKind != ControlFlowConditionKind.None && b.BranchValue is not null))
                {
                    var condition = conditions.FirstOrDefault(c => c.Span.Contains(block.BranchValue!.Syntax.Span));
                    if (condition is null || !EnvironmentValue(block.BranchValue!.Syntax, t.Model)) continue;
                    var first = Follow(block.FallThroughSuccessor);
                    var second = Follow(block.ConditionalSuccessor);
                    if (first.NormalExit && second.Progress || second.NormalExit && first.Progress)
                        Reject("B02", t.Input, condition, "Environment identity silently exits before a required business stage.");
                }
            }
            foreach (var branch in t.Node.DescendantNodes().OfType<ConditionalExpressionSyntax>())
                if (EnvironmentValue(branch.Condition, t.Model) && branch.DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .Any(call => Progress(t.Model.GetSymbolInfo(call).Symbol as IMethodSymbol)))
                    Reject("B02", t.Input, branch.Condition, "Environment identity selects a business branch.");
            foreach (var branch in t.Node.DescendantNodes().OfType<SwitchStatementSyntax>())
                if (EnvironmentValue(branch.Expression, t.Model) && branch.DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .Any(call => Progress(t.Model.GetSymbolInfo(call).Symbol as IMethodSymbol)))
                    Reject("B02", t.Input, branch.Expression, "Environment identity selects a business stage.");
            foreach (var branch in t.Node.DescendantNodes().OfType<SwitchExpressionSyntax>())
                if (EnvironmentValue(branch.GoverningExpression, t.Model) && branch.DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .Any(call => Progress(t.Model.GetSymbolInfo(call).Symbol as IMethodSymbol)))
                    Reject("B02", t.Input, branch.GoverningExpression, "Environment identity selects a business stage.");
        }
        foreach (var t in types.Where(t => ShapeRoots.Contains(t.Symbol.Name)))
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            void Shape(ITypeSymbol type, SyntaxNode origin)
            {
                var name = type.ToDisplayString();
                if (!visited.Add(name)) return;
                if (type.SpecialType == SpecialType.System_Object || type.TypeKind == TypeKind.Dynamic ||
                    name.TrimEnd('?') is "System.Text.Json.JsonElement" or "System.Text.Json.JsonDocument")
                    Reject("B03", t.Input, origin, "Unbounded fixture payload in shared execution shape: " + name);
                if (type is IArrayTypeSymbol array) { Shape(array.ElementType, origin); return; }
                if (type is not INamedTypeSymbol named) return;
                foreach (var argument in named.TypeArguments) Shape(argument, origin);
                if (!byName.TryGetValue(named.OriginalDefinition.ToDisplayString(), out var own)) return;
                foreach (var property in named.GetMembers().OfType<IPropertySymbol>().Where(p => !p.IsStatic))
                {
                    if (ForbiddenMembers.Contains(property.Name)) Reject("B03", t.Input, origin, "Test implementation knowledge in shared shape: " + property.Name);
                    Shape(property.Type, origin);
                }
            }
            Shape(t.Symbol, t.Node);
        }
        var formalBindings = 0;
        foreach (var (input, model) in sources)
        {
            foreach (var error in input.Tree.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))
                Reject("B05", input, input.Tree.GetRoot(), "Source parsing failed: " + error);
            foreach (var call in input.Tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (model.GetSymbolInfo(call).Symbol is not IMethodSymbol method || method.Name is not ("AddSingleton" or "AddScoped" or "AddTransient")) continue;
                if (!method.TypeArguments.Any(a => a.Name == "IDetectionPort")) continue;
                // A Compile-linked source remains production even when located under tests/.
                // Test assembly metadata, not the file path or inventory Role, identifies UpperIsolation.
                if (input.TestProject && model.Compilation.AssemblyName != "Gaode.Host") continue;
                var implementations = method.TypeArguments.Where(a => a.Name != "IDetectionPort").ToArray();
                if (implementations.Length != 1 || implementations[0].Name != "RecipeDetectionExecutor")
                    Reject("B04", input, call, "Formal detection DI must select the one common execution implementation.");
                else if (model.Compilation.AssemblyName == "Gaode.Host") formalBindings++;
            }
        }
        if (sources.Any(s => s.Model.Compilation.AssemblyName == "Gaode.Host") && formalBindings != 1)
        {
            var host = sources.First(s => s.Model.Compilation.AssemblyName == "Gaode.Host");
            Reject("B04", host.Input, host.Input.Tree.GetRoot(), "Formal Host must have exactly one common detection registration.");
        }
        return (findings, closure.Order(StringComparer.Ordinal).ToArray());
    }

    private static bool IsBoundary(INamedTypeSymbol type) => BoundaryNames.Contains(type.Name) || LeafPorts.Contains(type.Name) ||
        type.AllInterfaces.Any(i => LeafPorts.Contains(i.Name));
    private static bool EnvironmentMember(string name) => name.ToLowerInvariant() is "purpose" or "source" or "mode" or "provider" or "istest" or
        "issimulation" or "externalvirtualplc" or "isvirtual" or "detectiontestmode";
    private static bool Advances(IMethodSymbol? method) => method is not null &&
        (method.ContainingType.Name is "IDetectionPort" or "RecipeDetectionExecutor" or "ThreeStageWorkflowExecutor" or "WholeTrayWorkflowOrchestrator" &&
            method.Name is "ExecuteAsync" or "ExecuteCoreAsync" or "ExecuteThreeStagesAsync" or "AllowManualRemovalAsync" or "ConfirmManualRemovalAsync" ||
         method.Name is "ExecutePostFlipComponentAsync" or "ContinueAsync" or "RunNextStageAsync" or "CompleteWorkflowAsync") &&
        method.MethodKind != MethodKind.Constructor;

    private static bool ExplicitRefusal(IOperation? value)
    {
        while (value is IConversionOperation conversion) value = conversion.Operand;
        if (value is not IObjectCreationOperation creation) return false;
        bool Flag(string parameter, bool expected) => creation.Arguments.Any(a => a.Parameter?.Name == parameter &&
            a.Value.ConstantValue is { HasValue: true, Value: bool actual } && actual == expected);
        bool StageRefusal() => creation.Arguments.Any(a=>a.Parameter?.Name=="Status"&&
            a.Value.DescendantsAndSelf().OfType<IFieldReferenceOperation>().Any(f=>
                f.Field.ContainingType.ToDisplayString()=="Gaode.Application.Workflow.ThreeStageExecutionStatus"&&
                f.Field.Name is "Failed" or "TimedOut" or "Disconnected" or "PausedForManualReview" or "UnknownHeld"));
        // Exact typed refusal statuses only; UnitCompleted/StagesCompleted and arbitrary factories still fail.
        // Existing business rejection contracts, bound by type/constructor semantics;
        // neither an arbitrary returned object nor a factory named Rejected is proof.
        return creation.Type?.ToDisplayString() switch {
            "Gaode.Application.Station01.ThreeDAndFRecipeGateDecision" => Flag("CanLoadAndBind", false) && Flag("MustLockAndStop", true),
            "Gaode.Application.Workflow.ControlledRecoveryAuthorization" => Flag("IsAuthorized", false),
            "Gaode.Application.Workflow.ThreeStageExecutionResult" => StageRefusal(),
            _ => false
        };
    }
}
