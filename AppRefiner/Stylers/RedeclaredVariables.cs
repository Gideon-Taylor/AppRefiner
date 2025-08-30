using AppRefiner.Stylers;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

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
    private const uint REDECLARATION_COLOR = 0x0000FFA0; // Harsh red color with high alpha
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
    /// Handles local variable declarations and checks for redeclarations
    /// </summary>
    public override void VisitLocalVariableDeclaration(LocalVariableDeclarationNode node)
    {
        foreach(var name in node.VariableNameInfos)
        {
            CheckForVariable(name);
        }

        base.VisitLocalVariableDeclaration(node);
    }

    private void CheckForVariable(VariableNameInfo varInfo)
    {
        var currentScope = GetCurrentScope();
        var varName = varInfo.Name;

        /* Try to get the variable in scope with the name "varName" */
        var variablesInScope = GetVariablesInScope(currentScope).Where(var => var.Name.Equals(varName, StringComparison.OrdinalIgnoreCase));

        
        if (variablesInScope.Any())
        {
            var declaredVar = variablesInScope.First();
            switch (declaredVar.Kind)
            {
                case VariableKind.Parameter:
                    AddIndicator(varInfo.SourceSpan, IndicatorType.HIGHLIGHTER, REDECLARATION_COLOR,
                        $"Variable '{varName}' already declared as parameter in this scope");
                    break;
                case VariableKind.Instance:
                    AddIndicator(varInfo.SourceSpan, IndicatorType.HIGHLIGHTER, REDECLARATION_COLOR,
                        $"Variable '{varName}' shadows instance variable '{declaredVar.Name}'");
                    break;
                case VariableKind.Property:
                    AddIndicator(varInfo.SourceSpan, IndicatorType.HIGHLIGHTER, REDECLARATION_COLOR,
                        $"Variable '{varName}' shadows property '{declaredVar.Name}'");
                    break;
                case VariableKind.Local:
                    AddIndicator(varInfo.SourceSpan, IndicatorType.HIGHLIGHTER, REDECLARATION_COLOR,
                        $"Variable '{varName}' already declared in this scope");
                    break;
                default:
                    // Unknown kind - treat as local
                    break;
            }
        }
        
    }

    /// <summary>
    /// Handles local variable declarations with assignment and checks for redeclarations
    /// </summary>
    public override void VisitLocalVariableDeclarationWithAssignment(LocalVariableDeclarationWithAssignmentNode node)
    {
        CheckForVariable(node.VariableNameInfo);
        base.VisitLocalVariableDeclarationWithAssignment(node);
    }

    #endregion

    #region Helper Methods

    #endregion

    #region Scope Event Overrides

    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Resets the styler to its initial state
    /// </summary>
    protected override void OnReset()
    {
    }

    #endregion
}