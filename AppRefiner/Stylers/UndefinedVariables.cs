using System;
using System.Collections.Generic;
using System.Linq;
using AppRefiner.Stylers;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted.Visitors.Models;

namespace AppRefiner.Stylers;

/// <summary>
/// Highlights variables that are referenced but not defined in any accessible scope.
/// This is a self-hosted equivalent to the ANTLR-based UndefinedVariableStyler.
/// </summary>
public class UndefinedVariables : ScopedStyler
{
    private const uint HIGHLIGHT_COLOR = 0x0000FFA0; // Harsh red color with high alpha
    private readonly IVariableUsageTracker usageTracker;
    private readonly HashSet<string> instanceVariables = new(StringComparer.InvariantCultureIgnoreCase);
    private readonly HashSet<string> classProperties = new(StringComparer.InvariantCultureIgnoreCase);

    public UndefinedVariables()
    {
        usageTracker = new VariableUsageTracker();
    }

    public override string Description => "Highlights undefined variables";

    #region AST Visitor Overrides

    /// <summary>
    /// Processes the entire program and checks for undefined variable references
    /// </summary>
    public override void VisitProgram(ProgramNode node)
    {
        Reset();
        
        // Process the program first to collect all declarations
        base.VisitProgram(node);
        
        // Generate indicators for all undefined variable references
        GenerateIndicatorsForUndefinedVariables();
    }

    /// <summary>
    /// Handles app class declarations and collects instance variables and properties
    /// </summary>
    public override void VisitAppClass(AppClassNode node)
    {
        // Collect instance variables (private members)
        foreach (var instanceVar in node.InstanceVariables)
        {
            instanceVariables.Add(instanceVar.Name);
        }
        
        // Collect class properties (public/protected properties)
        foreach (var property in node.Properties)
        {
            if (property.Visibility == VisibilityModifier.Public || 
                property.Visibility == VisibilityModifier.Protected)
            {
                // Add both with and without & prefix for property access patterns
                classProperties.Add(property.Name);
                classProperties.Add($"&{property.Name}");
            }
        }
        
        base.VisitAppClass(node);
    }

    /// <summary>
    /// Handles identifier references and checks for undefined variables
    /// </summary>
    public override void VisitIdentifier(IdentifierNode node)
    {
        // Only check user variables and generic identifiers that could be variables
        if (node.IdentifierType == IdentifierType.UserVariable || 
            node.IdentifierType == IdentifierType.Generic)
        {
            string varName = node.Name;
            
            // Skip special system variables
            if (IsSpecialVariable(varName))
            {
                base.VisitIdentifier(node);
                return;
            }
            
            // Check if variable is defined in any accessible scope
            if (!IsVariableDefined(varName))
            {
                // Track this as an undefined reference
                usageTracker.TrackUndefinedReference(varName, node.SourceSpan, GetCurrentScopeInfo());
            }
            else
            {
                // Mark as used if it's defined
                usageTracker.MarkAsUsed(varName, GetCurrentScopeInfo());
            }
        }
        
        base.VisitIdentifier(node);
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Called when a variable is declared in any scope - register it with the tracker
    /// </summary>
    protected override void OnVariableDeclared(VariableInfo varInfo, ScopeInfo scope)
    {
        usageTracker.RegisterVariable(varInfo, scope);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Checks if a variable name represents a special system variable that should not be flagged
    /// </summary>
    private static bool IsSpecialVariable(string varName)
    {
        // System variables starting with %
        if (varName.StartsWith("%"))
            return true;
        
        return false;
    }

    /// <summary>
    /// Comprehensive check to determine if a variable is defined in any accessible context
    /// </summary>
    private bool IsVariableDefined(string varName)
    {
        var currentScope = GetCurrentScopeInfo();
        
        // 1. Check if defined in any scope (parameters, locals, etc.)
        if (usageTracker.IsVariableDefined(varName, currentScope))
            return true;
        
        // 2. Check instance variables (for class contexts)
        if (instanceVariables.Contains(varName))
            return true;
            
        // 3. Check class properties (both direct name and &-prefixed)
        if (classProperties.Contains(varName))
            return true;
        
        // 4. For &-prefixed variables, also check the unprefixed property name
        if (varName.StartsWith("&"))
        {
            string propertyName = varName.Substring(1);
            if (classProperties.Contains(propertyName))
                return true;
        }
        
        // 5. For non-prefixed names, check if there's a matching &-prefixed property
        else
        {
            string prefixedName = $"&{varName}";
            if (classProperties.Contains(prefixedName))
                return true;
        }
        
        return false;
    }

    /// <summary>
    /// Generates indicators for all undefined variable references
    /// </summary>
    private void GenerateIndicatorsForUndefinedVariables()
    {
        foreach (var (name, location, scope) in usageTracker.GetUndefinedReferences())
        {
            string tooltip = $"Undefined variable: {name}";
            AddIndicator(location, IndicatorType.BACKGROUND, HIGHLIGHT_COLOR, tooltip);
        }
    }

    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Resets the styler to its initial state
    /// </summary>
    protected override void OnReset()
    {
        usageTracker.Reset();
        instanceVariables.Clear();
        classProperties.Clear();
    }

    #endregion
}