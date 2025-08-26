using System;
using System.Collections.Generic;
using System.Linq;
using AppRefiner.Stylers;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted.Visitors.Models;

namespace AppRefiner.Stylers;

/// <summary>
/// Information about a member that can be shadowed by a local variable
/// </summary>
public class ShadowInfo
{
    public string ActualName { get; set; } = string.Empty;  // Preserves original casing
    public string MemberType { get; set; } = string.Empty;  // "property", "instance variable", etc.
    public ScopeInfo? Scope { get; set; }   // Which scope this member belongs to (for parameters)
    
    public ShadowInfo(string actualName, string memberType, ScopeInfo? scope = null)
    {
        ActualName = actualName;
        MemberType = memberType;
        Scope = scope;
    }
}

/// <summary>
/// Highlights variables that are declared multiple times in the same scope.
/// Detects redeclarations in local variable declarations and property shadowing.
/// </summary>
public class RedeclaredVariables : ScopedStyler
{
    private const uint REDECLARATION_COLOR = 0x0066CCFF; // Orange warning color
    private const uint PROPERTY_SHADOW_COLOR = 0x0099FFFF; // Yellow warning color
    
    private readonly Dictionary<ScopeInfo, HashSet<string>> declaredVariablesPerScope = new();
    private readonly Dictionary<string, ShadowInfo> shadowableMembers = new(StringComparer.InvariantCultureIgnoreCase);

    public override string Description => "Redeclared variables";

    #region AST Visitor Overrides

    /// <summary>
    /// Processes the entire program and resets state
    /// </summary>
    public override void VisitProgram(ProgramNode node)
    {
        Reset();
        base.VisitProgram(node);
    }

    /// <summary>
    /// Handles app class declarations and collects properties and instance variables for shadowing detection
    /// </summary>
    public override void VisitAppClass(AppClassNode node)
    {
        // Collect class properties for shadowing detection
        foreach (var property in node.Properties)
        {
            if (property.Visibility == VisibilityModifier.Public || 
                property.Visibility == VisibilityModifier.Protected)
            {
                shadowableMembers[property.Name] = new ShadowInfo(property.Name, "property");
            }
        }
        
        // Collect instance variables for shadowing detection
        foreach (var instanceVar in node.InstanceVariables)
        {
            shadowableMembers[instanceVar.Name] = new ShadowInfo(instanceVar.Name, "instance variable");
        }
        
        base.VisitAppClass(node);
    }

    /// <summary>
    /// Handles method declarations and collects parameters for shadowing detection
    /// </summary>
    public override void VisitMethod(MethodNode node)
    {
        var currentScope = GetCurrentScopeInfo();
        
        // Remove previous method/function parameters from this scope
        RemoveParametersFromScope(currentScope);
        
        // Add method parameters for shadowing detection
        foreach (var parameter in node.Parameters)
        {
            shadowableMembers[parameter.Name] = new ShadowInfo(parameter.Name, "method parameter", currentScope);
        }
        
        base.VisitMethod(node);
    }

    /// <summary>
    /// Handles function declarations and collects parameters for shadowing detection
    /// </summary>
    public override void VisitFunction(FunctionNode node)
    {
        var currentScope = GetCurrentScopeInfo();
        
        // Remove previous method/function parameters from this scope
        RemoveParametersFromScope(currentScope);
        
        // Add function parameters for shadowing detection
        foreach (var parameter in node.Parameters)
        {
            shadowableMembers[parameter.Name] = new ShadowInfo(parameter.Name, "function parameter", currentScope);
        }
        
        base.VisitFunction(node);
    }

    /// <summary>
    /// Handles local variable declarations and checks for redeclarations
    /// </summary>
    public override void VisitLocalVariableDeclaration(LocalVariableDeclarationNode node)
    {
        base.VisitLocalVariableDeclaration(node);

        var currentScope = GetCurrentScopeInfo();
        EnsureScopeTracker(currentScope);
        var declaredInScope = declaredVariablesPerScope[currentScope];

        // Check each variable name in the declaration
        foreach (var variableNameInfo in node.VariableNameInfos)
        {
            string varName = variableNameInfo.Name;
            
            // Check for redeclaration in current scope
            if (declaredInScope.Contains(varName))
            {
                AddIndicator(variableNameInfo.SourceSpan, IndicatorType.SQUIGGLE, REDECLARATION_COLOR, 
                    $"Variable '{varName}' already declared in this scope");
            }
            // Check for member shadowing (property, instance variable, or method parameter)
            else if (GetShadowInfo(varName) is ShadowInfo shadowInfo)
            {
                AddIndicator(variableNameInfo.SourceSpan, IndicatorType.SQUIGGLE, PROPERTY_SHADOW_COLOR, 
                    $"Variable '{varName}' shadows {shadowInfo.MemberType} '{shadowInfo.ActualName}'");
            }
            else
            {
                // Track this variable as declared
                declaredInScope.Add(varName);
            }
        }
    }

    /// <summary>
    /// Handles local variable declarations with assignment and checks for redeclarations
    /// </summary>
    public override void VisitLocalVariableDeclarationWithAssignment(LocalVariableDeclarationWithAssignmentNode node)
    {
        var currentScope = GetCurrentScopeInfo();
        EnsureScopeTracker(currentScope);
        var declaredInScope = declaredVariablesPerScope[currentScope];

        string varName = node.VariableNameInfo.Name;
        
        // Check for redeclaration in current scope
        if (declaredInScope.Contains(varName))
        {
            AddIndicator(node.VariableNameInfo.SourceSpan, IndicatorType.SQUIGGLE, REDECLARATION_COLOR, 
                $"Variable '{varName}' already declared in this scope");
        }
        // Check for member shadowing (property, instance variable, or method parameter)
        else if (GetShadowInfo(varName) is ShadowInfo shadowInfo)
        {
            AddIndicator(node.VariableNameInfo.SourceSpan, IndicatorType.SQUIGGLE, PROPERTY_SHADOW_COLOR, 
                $"Variable '{varName}' shadows {shadowInfo.MemberType} '{shadowInfo.ActualName}'");
        }
        else
        {
            // Track this variable as declared
            declaredInScope.Add(varName);
        }

        base.VisitLocalVariableDeclarationWithAssignment(node);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Ensures that a scope tracker exists for the given scope
    /// </summary>
    private void EnsureScopeTracker(ScopeInfo scope)
    {
        if (!declaredVariablesPerScope.ContainsKey(scope))
        {
            declaredVariablesPerScope[scope] = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        }
    }

    /// <summary>
    /// Gets shadow information for a variable name if it shadows any member
    /// </summary>
    private ShadowInfo? GetShadowInfo(string varName)
    {
        // Direct lookup for exact match (handles instance variables, parameters)
        if (shadowableMembers.TryGetValue(varName, out var directMatch))
        {
            return directMatch;
        }
        
        // Check for &-prefixed variable shadowing non-prefixed property
        if (varName.StartsWith("&"))
        {
            string unprefixedName = varName.Substring(1);
            if (shadowableMembers.TryGetValue(unprefixedName, out var shadowMatch))
            {
                // Only allow shadowing properties with &-prefixed variables
                if (shadowMatch.MemberType == "property")
                {
                    return shadowMatch;
                }
            }
        }
        
        return null;
    }

    /// <summary>
    /// Removes parameters from the specified scope when entering a new method/function
    /// </summary>
    private void RemoveParametersFromScope(ScopeInfo scope)
    {
        var keysToRemove = shadowableMembers
            .Where(kvp => kvp.Value.Scope == scope && 
                         (kvp.Value.MemberType == "method parameter" || kvp.Value.MemberType == "function parameter"))
            .Select(kvp => kvp.Key)
            .ToList();
            
        foreach (var key in keysToRemove)
        {
            shadowableMembers.Remove(key);
        }
    }

    #endregion

    #region Scope Event Overrides

    /// <summary>
    /// Called when entering a new scope - prepare tracking for the scope
    /// </summary>
    protected override void OnEnterScope(ScopeInfo scope)
    {
        EnsureScopeTracker(scope);
        base.OnEnterScope(scope);
    }

    /// <summary>
    /// Called when exiting a scope - clean up tracking data
    /// </summary>
    protected override void OnExitScope(ScopeInfo scopeInfo, Dictionary<string, VariableInfo> variableScope, Dictionary<string, object> customData)
    {
        // Keep scope data for potential future reference, but could clean up here if memory is a concern
        base.OnExitScope(scopeInfo, variableScope, customData);
    }

    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Resets the styler to its initial state
    /// </summary>
    protected override void OnReset()
    {
        declaredVariablesPerScope.Clear();
        shadowableMembers.Clear();
    }

    #endregion
}