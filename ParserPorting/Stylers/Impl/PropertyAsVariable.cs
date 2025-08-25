using System;
using System.Collections.Generic;
using System.Linq;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;

namespace ParserPorting.Stylers.Impl;

/// <summary>
/// Highlights properties used as variables (&PropertyName) outside of constructors.
/// This is a self-hosted equivalent to the ANTLR-based PropertyAsVariable styler.
/// </summary>
public class PropertyAsVariable : ScopedStyler
{
    private const uint HIGHLIGHT_COLOR = 0x4DB7FF80;
    private readonly HashSet<string> publicProperties = new();
    private string? currentClassName;

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
    /// Handles app class declarations and collects public/protected properties
    /// </summary>
    public override void VisitAppClass(AppClassNode node)
    {
        currentClassName = node.Name;
        
        // Collect all public and protected properties
        foreach (var property in node.Properties)
        {
            if (property.Visibility == VisibilityModifier.Public || 
                property.Visibility == VisibilityModifier.Protected)
            {
                publicProperties.Add(property.Name);
            }
        }
        
        base.VisitAppClass(node);
    }

    /// <summary>
    /// Handles identifier references and highlights property references used as variables
    /// </summary>
    public override void VisitIdentifier(IdentifierNode node)
    {
        // Only process user variables (those starting with &)
        if (node.IdentifierType == IdentifierType.UserVariable)
        {
            // Extract property name by removing the & prefix
            string varName = node.Name.TrimStart('&');
            
            // Check if this variable name matches a public/protected property
            if (publicProperties.Contains(varName))
            {
                // Only highlight if we're not in a constructor
                var currentMethod = GetCurrentScopeInfo();
                if (currentMethod != null && !IsInConstructor(currentMethod))
                {
                    AddIndicator(node.SourceSpan, Indicator.IndicatorType.HIGHLIGHTER, HIGHLIGHT_COLOR,
                        "Property used as variable outside constructor");
                }
            }
        }
        
        base.VisitIdentifier(node);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Determines if the current scope is within a constructor
    /// </summary>
    private bool IsInConstructor(ScopeInfo scopeInfo)
    {
        // Walk up the scope chain to find a method scope
        var current = scopeInfo;
        while (current != null)
        {
            if (current.Type == ScopeType.Method)
            {
                // Check if method name matches class name (constructor pattern)
                return string.Equals(current.Name, currentClassName, StringComparison.OrdinalIgnoreCase);
            }
            current = current.Parent;
        }
        
        return false;
    }

    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Resets the styler to its initial state
    /// </summary>
    protected override void OnReset()
    {
        publicProperties.Clear();
        currentClassName = null;
    }

    #endregion
}