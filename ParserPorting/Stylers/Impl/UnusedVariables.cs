using System;
using System.Collections.Generic;
using System.Linq;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using PeopleCodeParser.SelfHosted.Visitors.Utilities;

namespace ParserPorting.Stylers.Impl;

/// <summary>
/// Visitor that identifies unused variables, parameters, and instance variables in PeopleCode.
/// This is a self-hosted equivalent to the AppRefiner's UnusedLocalVariableStyler.
/// </summary>
public class UnusedVariables : ScopedStyler
{
    private const uint HIGHLIGHT_COLOR = 0x73737380; // Light gray text (no alpha)
    private readonly IVariableUsageTracker usageTracker;

    public UnusedVariables()
    {
        usageTracker = new VariableUsageTracker();
    }


    #region AST Visitor Overrides

    /// <summary>
    /// Processes the entire program and checks for unused variables in all scopes
    /// </summary>
    public override void VisitProgram(ProgramNode node)
    {
        Reset();
        
        // Process the program
        base.VisitProgram(node);
        
        // Generate indicators for all unused variables
        GenerateIndicatorsForUnusedVariables();
    }

    /// <summary>
    /// Handles member access expressions, particularly for %This references
    /// </summary>
    public override void VisitMemberAccess(MemberAccessNode node)
    {
        // Check for %This dot access to properties and instance variables
        var target = node.Target;
        if (target is IdentifierNode identNode && identNode.Name.Equals("%THIS", StringComparison.OrdinalIgnoreCase))
        {
            var memberName = node.MemberName;
            
            // Mark the property as used
            usageTracker.MarkAsUsed(memberName, GetCurrentScopeInfo());
            
            // Also check for instance variable with & prefix
            string varNameWithPrefix = $"&{memberName}";
            usageTracker.MarkAsUsed(varNameWithPrefix, GetCurrentScopeInfo());
        }
        
        base.VisitMemberAccess(node);
    }

    /// <summary>
    /// Handles FOR statements and marks iterator variables as used
    /// </summary>
    public override void VisitFor(ForStatementNode node)
    {
        // Mark the iterator variable as used
        string iteratorName = node.Variable;
        usageTracker.MarkAsUsed(iteratorName, GetCurrentScopeInfo());
        
        // Also check with/without & prefix
        if (iteratorName.StartsWith("&"))
        {
            var nameWithoutPrefix = iteratorName.Substring(1);
            usageTracker.MarkAsUsed(nameWithoutPrefix, GetCurrentScopeInfo());
        }
        else
        {
            var nameWithPrefix = $"&{iteratorName}";
            usageTracker.MarkAsUsed(nameWithPrefix, GetCurrentScopeInfo());
        }
        
        base.VisitFor(node);
    }

    /// <summary>
    /// Handles identifier references and marks variables as used
    /// </summary>
    public override void VisitIdentifier(IdentifierNode node)
    {
        // Mark the variable as used
        usageTracker.MarkAsUsed(node.Name, GetCurrentScopeInfo());
        
        // If this is a property accessed with & prefix, also mark the property as used
        if (node.Name.StartsWith("&"))
        {
            var propertyName = node.Name.Substring(1); // Remove the & prefix
            usageTracker.MarkAsUsed(propertyName, GetCurrentScopeInfo());
        }
        
        base.VisitIdentifier(node);
    }

    public override void VisitFunctionCall(FunctionCallNode node)
    {
        if (node.Function is MemberAccessNode member && member.Target is IdentifierNode ident)
        {
            if (ident.Name.StartsWith('&'))
            {
                usageTracker.MarkAsUsed(ident.Name, GetCurrentScopeInfo());
            }
        }
        base.VisitFunctionCall(node);
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Called when a variable is declared in any scope
    /// </summary>
    protected override void OnVariableDeclared(VariableInfo varInfo, ScopeInfo scope)
    {
        // Register the variable with the usage tracker
        usageTracker.RegisterVariable(varInfo, scope);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Generates indicators for all unused variables
    /// </summary>
    private void GenerateIndicatorsForUnusedVariables()
    {
        foreach (var (variable, scope) in usageTracker.GetUnusedVariables())
        {
            string tooltip = GetTooltipForVariable(variable, scope);
            AddIndicator(variable.Span, Indicator.IndicatorType.TEXTCOLOR, HIGHLIGHT_COLOR, tooltip);
        }
    }

    /// <summary>
    /// Gets an appropriate tooltip for a variable based on its type and scope
    /// </summary>
    private string GetTooltipForVariable(VariableInfo variable, ScopeInfo scope)
    {
        switch (variable.VariableType)
        {
            case VariableType.Parameter:
                return $"Unused parameter: {variable.Name}";
            case VariableType.Instance:
                return $"Unused instance variable: {variable.Name}";
            case VariableType.Property:
                return $"Unused property: {variable.Name}";
            case VariableType.Global:
                return $"Unused global variable: {variable.Name}";
            default:
                var scopePrefix = GetTooltipPrefixForScope(scope);
                return $"{scopePrefix}: {variable.Name}";
        }
    }

    /// <summary>
    /// Gets the appropriate tooltip prefix based on the scope type
    /// </summary>
    private string GetTooltipPrefixForScope(ScopeInfo scopeInfo)
    {
        return scopeInfo.Type switch
        {
            ScopeType.Method => "Unused method variable",
            ScopeType.Function => "Unused function variable",
            ScopeType.Property => "Unused property variable",
            ScopeType.Getter => "Unused getter variable",
            ScopeType.Setter => "Unused setter variable",
            _ => "Unused local variable"
        };
    }

    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Resets the visitor to its initial state
    /// </summary>
    protected override void OnReset()
    {

        usageTracker.Reset();
    }

    #endregion
}