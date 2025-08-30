using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;

namespace PeopleCodeParser.SelfHosted.Visitors;

/// <summary>
/// Test implementation of EnhancedScopedAstVisitor for validation and demonstration
/// </summary>
public class TestEnhancedScopedVisitor : EnhancedScopedAstVisitor<string>
{
    private readonly List<string> events = new();
    
    /// <summary>
    /// Gets all events that occurred during AST traversal
    /// </summary>
    public IReadOnlyList<string> Events => events.AsReadOnly();
    
    protected override void OnEnterGlobalScope(ScopeContext scope, ProgramNode node)
    {
        events.Add($"ENTER: Global scope '{scope.Name}'");
        AddToCurrentScope("global_data", "This is global scope data");
    }
    
    protected override void OnExitGlobalScope(ScopeContext scope, ProgramNode node, Dictionary<string, string> customData)
    {
        events.Add($"EXIT: Global scope '{scope.Name}' with {GetVariablesDeclaredInScope(scope).Count()} declared variables");
        
        // Validate that GetCurrentScope() returns the correct scope
        var current = GetCurrentScope();
        if (current.Id == scope.Id)
        {
            events.Add("✓ GetCurrentScope() works correctly in OnExitGlobalScope");
        }
        else
        {
            events.Add("✗ GetCurrentScope() FAILED in OnExitGlobalScope");
        }
    }
    
    protected override void OnEnterClassScope(ScopeContext scope, AppClassNode node)
    {
        events.Add($"ENTER: Class scope '{scope.Name}'");
        AddToCurrentScope("class_data", $"Class {node.Name} data");
    }
    
    protected override void OnExitClassScope(ScopeContext scope, AppClassNode node, Dictionary<string, string> customData)
    {
        events.Add($"EXIT: Class scope '{scope.Name}' with {GetVariablesDeclaredInScope(scope).Count()} declared variables");
    }
    
    protected override void OnEnterMethodScope(ScopeContext scope, MethodNode node)
    {
        events.Add($"ENTER: Method scope '{scope.Name}' with {node.Parameters.Count} parameters");
        AddToCurrentScope("method_data", $"Method {node.Name} data");
    }
    
    protected override void OnExitMethodScope(ScopeContext scope, MethodNode node, Dictionary<string, string> customData)
    {
        var declaredVars = GetVariablesDeclaredInScope(scope);
        var accessibleVars = GetVariablesInScope(scope);
        var parameters = declaredVars.Where(v => v.Kind == VariableKind.Parameter);
        var locals = declaredVars.Where(v => v.Kind == VariableKind.Local);
        
        events.Add($"EXIT: Method scope '{scope.Name}' - {parameters.Count()} parameters, {locals.Count()} locals, {accessibleVars.Count()} accessible total");
    }
    
    protected override void OnEnterFunctionScope(ScopeContext scope, FunctionNode node)
    {
        events.Add($"ENTER: Function scope '{scope.Name}' with {node.Parameters.Count} parameters");
        AddToCurrentScope("function_data", $"Function {node.Name} data");
    }
    
    protected override void OnExitFunctionScope(ScopeContext scope, FunctionNode node, Dictionary<string, string> customData)
    {
        var accessibleVars = GetVariablesInScope(scope);
        events.Add($"EXIT: Function scope '{scope.Name}' with {accessibleVars.Count()} accessible variables");
    }
    
    protected override void OnEnterPropertyScope(ScopeContext scope, PropertyNode node)
    {
        events.Add($"ENTER: Property scope '{scope.Name}'");
        AddToCurrentScope("property_data", $"Property {node.Name} data");
    }
    
    protected override void OnExitPropertyScope(ScopeContext scope, PropertyNode node, Dictionary<string, string> customData)
    {
        events.Add($"EXIT: Property scope '{scope.Name}'");
    }
    
    protected override void OnVariableDeclared(EnhancedVariableInfo variable)
    {
        events.Add($"VARIABLE DECLARED: {variable.Kind} {variable.Type} {variable.Name} in {variable.DeclarationScope.Name} (Safe: {variable.IsSafeToRefactor})");
    }
    
    protected override void OnVariableReferenced(string variableName, VariableReference reference)
    {
        events.Add($"VARIABLE REFERENCED: {variableName} [{reference.ReferenceType}] at {reference.Line}:{reference.Column} in {reference.Scope.Name}");
    }
    
    /// <summary>
    /// Gets a summary of the analysis after visiting the AST
    /// </summary>
    public AnalysisSummary GetAnalysisSummary()
    {
        var allScopes = GetAllScopes().ToList();
        var allVariables = GetAllVariables().ToList();
        
        return new AnalysisSummary
        {
            TotalScopes = allScopes.Count,
            TotalVariables = allVariables.Count,
            ScopesByType = allScopes.GroupBy(s => s.Type).ToDictionary(g => g.Key, g => g.Count()),
            VariablesByKind = allVariables.GroupBy(v => v.Kind).ToDictionary(g => g.Key, g => g.Count()),
            UnusedVariables = allVariables.Where(v => v.IsUnused).ToList(),
            SafeToRefactorVariables = allVariables.Where(v => v.IsSafeToRefactor).ToList(),
            TotalReferences = allVariables.Sum(v => v.References.Count),
            Events = Events.ToList()
        };
    }
    
    /// <summary>
    /// Validates the scope hierarchy and variable accessibility rules
    /// </summary>
    public ValidationResult ValidateHierarchy()
    {
        var result = new ValidationResult();
        var allScopes = GetAllScopes().ToList();
        var globalScope = allScopes.FirstOrDefault(s => s.Type == EnhancedScopeType.Global);
        
        if (globalScope == null)
        {
            result.Errors.Add("No global scope found");
            return result;
        }
        
        // Validate scope hierarchy
        foreach (var scope in allScopes)
        {
            if (scope.Type != EnhancedScopeType.Global && scope.Parent == null)
            {
                result.Errors.Add($"Non-global scope '{scope.Name}' has no parent");
            }
            
            // Check that all children have this scope as parent
            foreach (var child in scope.Children)
            {
                if (child.Parent?.Id != scope.Id)
                {
                    result.Errors.Add($"Child scope '{child.Name}' has incorrect parent reference");
                }
            }
        }
        
        // Validate variable accessibility
        foreach (var variable in GetAllVariables())
        {
            var accessibleScopes = allScopes.Where(s => variable.IsAccessibleFrom(s)).ToList();
            
            foreach (var reference in variable.References.Where(r => r.ReferenceType != ReferenceType.Declaration))
            {
                if (!variable.IsAccessibleFrom(reference.Scope))
                {
                    result.Warnings.Add($"Variable '{variable.Name}' referenced from inaccessible scope '{reference.Scope.Name}'");
                }
            }
        }
        
        result.IsValid = result.Errors.Count == 0;
        return result;
    }
}

/// <summary>
/// Summary of analysis results
/// </summary>
public class AnalysisSummary
{
    public int TotalScopes { get; init; }
    public int TotalVariables { get; init; }
    public Dictionary<EnhancedScopeType, int> ScopesByType { get; init; } = new();
    public Dictionary<VariableKind, int> VariablesByKind { get; init; } = new();
    public List<EnhancedVariableInfo> UnusedVariables { get; init; } = new();
    public List<EnhancedVariableInfo> SafeToRefactorVariables { get; init; } = new();
    public int TotalReferences { get; init; }
    public List<string> Events { get; init; } = new();
}

/// <summary>
/// Result of hierarchy validation
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
}