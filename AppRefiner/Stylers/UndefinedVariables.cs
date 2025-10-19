using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;
using AppRefiner.Refactors.QuickFixes;

namespace AppRefiner.Stylers;

/// <summary>
/// Highlights variables that are referenced but not defined in any accessible scope.
/// This is a self-hosted equivalent to the ANTLR-based UndefinedVariableStyler.
/// </summary>
public class UndefinedVariables : BaseStyler
{
    private const uint HIGHLIGHT_COLOR = 0x0000FFA0; // Harsh red color with high alpha
    private HashSet<(string Name, SourceSpan Location)> undefinedVars = new();
    private HashSet<(string Name, SourceSpan Location)> forLoopIterators = new();

    public UndefinedVariables()
    {
    }

    public override string Description => "Undefined variables";

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
    /// Handles identifier references and checks for undefined variables
    /// </summary>
    public override void VisitIdentifier(IdentifierNode node)
    {

        // Only check user variables and generic identifiers that could be variables
        if (node.IdentifierType == IdentifierType.UserVariable)
        {
            string varName = node.Name;

            // Check if variable is defined in any accessible scope
            var curScope = GetCurrentScope();
            var varsInScope = GetVariablesInScope(curScope);
            if (!varsInScope.Any(v => v.Name.Equals(varName) ||
                (varName.StartsWith('&') && v.Name.Equals(varName.Substring(1)) && v.Kind == VariableKind.Property)))
            {
                // Track this as an undefined reference
                undefinedVars.Add((varName, node.SourceSpan));
            }
        }

        base.VisitIdentifier(node);
    }

    /// <summary>
    /// Handles for loops and checks if the iterator variable is undefined
    /// </summary>
    public override void VisitFor(ForStatementNode node)
    {
        string varName = node.IteratorName;

        // Check if variable is defined in any accessible scope
        var curScope = GetCurrentScope();
        var varsInScope = GetVariablesInScope(curScope);

        // Normalize variable name (remove & prefix for comparison)
        string normalizedVarName = varName.StartsWith('&') ? varName.Substring(1) : varName;

        if (!varsInScope.Any(v => v.Name.Equals(normalizedVarName) || v.Name.Equals(varName)))
        {
            // Track this as an undefined for loop iterator
            forLoopIterators.Add((varName, node.IteratorToken.SourceSpan));
        }

        base.VisitFor(node);
    }

    #endregion

    #region Event Handlers
    // Variable declaration is now handled automatically by base class
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
    /// Generates indicators for all undefined variable references
    /// </summary>
    private void GenerateIndicatorsForUndefinedVariables()
    {
        // First, handle for loop iterators with quick fixes
        foreach (var (name, location) in forLoopIterators)
        {
            string tooltip = $"Undefined for loop iterator: {name}";
            var quickFixes = new List<(Type RefactorClass, string Description)>
            {
                (typeof(DeclareForLoopIterator), "Declare iterator")
            };
            AddIndicator(location, IndicatorType.HIGHLIGHTER, HIGHLIGHT_COLOR, tooltip, quickFixes);
        }

        // Then handle other undefined variables (excluding those already handled as for loop iterators)
        foreach (var (name, location) in undefinedVars)
        {
            // Skip if this is already handled as a for loop iterator
            if (forLoopIterators.Contains((name, location)))
                continue;

            string tooltip = $"Undefined variable: {name}";
            AddIndicator(location, IndicatorType.HIGHLIGHTER, HIGHLIGHT_COLOR, tooltip);
        }
    }

    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Resets the styler to its initial state
    /// </summary>
    protected override void OnReset()
    {
        // Base class handles VariableTracker.Reset() automatically
        undefinedVars.Clear();
        forLoopIterators.Clear();
    }

    #endregion
}