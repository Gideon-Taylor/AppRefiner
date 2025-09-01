using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;

namespace PeopleCodeParser.SelfHosted.Visitors;

/// <summary>
/// Example implementation showing how to use the EnhancedScopedAstVisitor
/// for comprehensive variable analysis and refactoring support.
/// </summary>
public class VariableAnalysisVisitor : ScopedAstVisitor<string>
{
    private readonly List<string> analysisLog = new();

    /// <summary>
    /// Gets the complete analysis log after visiting the AST
    /// </summary>
    public IReadOnlyList<string> GetAnalysisLog() => analysisLog.AsReadOnly();

    protected override void OnEnterGlobalScope(ScopeContext scope, ProgramNode node)
    {
        analysisLog.Add($"=== GLOBAL SCOPE ANALYSIS ===");
        AddToCurrentScope("analysis_start", DateTime.Now.ToString());
    }

    protected override void OnExitGlobalScope(ScopeContext scope, ProgramNode node, Dictionary<string, string> customData)
    {
        var globalVars = GetVariablesInScope(scope);
        var functions = GetAllScopes().Where(s => s.Type == EnhancedScopeType.Function);
        var methods = GetAllScopes().Where(s => s.Type == EnhancedScopeType.Method);

        analysisLog.Add($"Global Variables: {globalVars.Count()}");
        analysisLog.Add($"Functions: {functions.Count()}");
        analysisLog.Add($"Methods: {methods.Count()}");
        
        // Example: Find all unused variables
        var unusedVars = GetUnusedVariables();
        if (unusedVars.Any())
        {
            analysisLog.Add("UNUSED VARIABLES FOUND:");
            foreach (var unused in unusedVars)
            {
                analysisLog.Add($"  - {unused.Name} ({unused.Kind}) in {unused.DeclarationScope.Name}");
            }
        }

        // Example: Find variables safe for refactoring
        var safeVars = GetAllVariables().Where(v => v.IsSafeToRefactor);
        analysisLog.Add($"Variables safe for refactoring: {safeVars.Count()}");
        
        // CRITICAL: GetCurrentScope() works correctly here because OnExit is called BEFORE pop
        var currentScope = GetCurrentScope();
        analysisLog.Add($"Current scope during OnExit: {currentScope.Name} (should be 'Global')");
    }

    protected override void OnEnterMethodScope(ScopeContext scope, MethodNode node)
    {
        analysisLog.Add($"Method '{node.Name}' has {node.Parameters.Count} parameters");
        AddToCurrentScope("method_complexity", CalculateMethodComplexity(node).ToString());
    }

    protected override void OnExitMethodScope(ScopeContext scope, MethodNode node, Dictionary<string, string> customData)
    {
        var localVars = GetVariablesInScope(scope).Where(v => v.Kind == VariableKind.Local);
        var parameters = GetVariablesInScope(scope).Where(v => v.Kind == VariableKind.Parameter);
        
        analysisLog.Add($"Method '{node.Name}' analysis:");
        analysisLog.Add($"  Parameters: {parameters.Count()}");
        analysisLog.Add($"  Local variables: {localVars.Count()}");
        
        // Check for unused parameters (potential code smell)
        var unusedParams = parameters.Where(p => p.IsUnused);
        if (unusedParams.Any())
        {
            analysisLog.Add($"  WARNING: Unused parameters: {string.Join(", ", unusedParams.Select(p => p.Name))}");
        }
    }

    protected override void OnVariableDeclared(VariableInfo variable)
    {
        analysisLog.Add($"Variable declared: {variable.Kind} {variable.Type} {variable.Name} " +
                       $"(Safe to refactor: {variable.IsSafeToRefactor})");
    }

    protected override void OnVariableReferenced(string variableName, VariableReference reference)
    {
        if (reference.ReferenceType == ReferenceType.Read)
        {
            // Only log significant references to avoid spam
            if (reference.Context?.Contains("parameter") == true)
            {
                analysisLog.Add($"Parameter reference: {variableName} at {reference.Line}:{reference.Column}");
            }
        }
    }

    /// <summary>
    /// Example: Find all references to a specific variable across all scopes
    /// </summary>
    public List<VariableReference> FindAllReferencesToVariable(string variableName)
    {
        var allReferences = new List<VariableReference>();
        
        foreach (var variable in GetAllVariables())
        {
            if (variable.Name.Equals(variableName, StringComparison.OrdinalIgnoreCase))
            {
                allReferences.AddRange(variable.References);
            }
        }
        
        return allReferences.OrderBy(r => r.Line).ThenBy(r => r.Column).ToList();
    }

    /// <summary>
    /// Example: Check if it's safe to rename a variable
    /// </summary>
    public RenameAnalysis AnalyzeVariableRename(string variableName, ScopeContext scope)
    {
        var variable = VariableRegistry.FindVariableInScope(variableName, scope);
        if (variable == null)
        {
            return new RenameAnalysis
            {
                CanRename = false,
                Reason = "Variable not found",
                References = new List<VariableReference>()
            };
        }

        // Check if variable is safe to refactor
        if (!variable.IsSafeToRefactor)
        {
            return new RenameAnalysis
            {
                CanRename = false,
                Reason = $"Variable '{variableName}' is {variable.Kind} and may be accessed from outside the program",
                References = variable.References.ToList()
            };
        }

        // Check for potential naming conflicts
        var scopeChain = scope.GetScopeChain();
        foreach (var checkScope in scopeChain)
        {
            // Implementation would check for naming conflicts...
        }

        return new RenameAnalysis
        {
            CanRename = true,
            Reason = "Variable is safe to rename",
            References = variable.References.ToList()
        };
    }

    /// <summary>
    /// Example: Get all variables accessible from a specific scope
    /// </summary>
    public List<VariableInfo> GetAccessibleVariablesExample(ScopeContext scope)
    {
        return GetAccessibleVariables(scope).OrderBy(v => v.Name).ToList();
    }

    private int CalculateMethodComplexity(MethodNode method)
    {
        // Simple complexity calculation based on method body
        // In real implementation, this would analyze the method body more thoroughly
        return method.Body?.Statements?.Count ?? 0;
    }
}

/// <summary>
/// Result of analyzing whether a variable can be safely renamed
/// </summary>
public class RenameAnalysis
{
    public bool CanRename { get; set; }
    public string Reason { get; set; } = string.Empty;
    public List<VariableReference> References { get; set; } = new();
}