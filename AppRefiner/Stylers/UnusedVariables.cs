using AppRefiner.Refactors.QuickFixes;
using PeopleCodeParser.SelfHosted.Nodes;
using PeopleCodeParser.SelfHosted.Visitors.Models;

namespace AppRefiner.Stylers;

/// <summary>
/// Visitor that identifies unused variables, parameters, and instance variables in PeopleCode.
/// This is a self-hosted equivalent to the AppRefiner's UnusedLocalVariableStyler.
/// </summary>
public class UnusedVariables : BaseStyler
{
    private const uint HIGHLIGHT_COLOR = 0x73737380; // Light gray text (no alpha)

    public UnusedVariables()
    {
    }

    public override string Description => "Unused variables";


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




    #endregion

    #region Event Handlers
    // Variable declaration is now handled automatically by base class
    #endregion

    #region Helper Methods

    /// <summary>
    /// Generates indicators for all unused variables
    /// </summary>
    private void GenerateIndicatorsForUnusedVariables()
    {
        foreach (var variable in GetUnusedVariables().Where(v => v.Kind == VariableKind.Local || v.Kind == VariableKind.Instance || v.Kind == VariableKind.Parameter))
        {
            string tooltip = GetTooltipForVariable(variable);

            AddIndicator(variable.VariableNameInfo.SourceSpan, IndicatorType.TEXTCOLOR, HIGHLIGHT_COLOR, tooltip, [(typeof(DeleteUnusedVariable), variable.Kind == VariableKind.Parameter ? "Delete unused parameter" : "Delete unused variable declaration")]);
        }
    }

    /// <summary>
    /// Gets an appropriate tooltip for a variable based on its type and scope
    /// </summary>
    private string GetTooltipForVariable(VariableInfo variable)
    {
        switch (variable.Kind)
        {
            case VariableKind.Parameter:
                return $"Unused parameter: {variable.Name}";
            case VariableKind.Instance:
                return $"Unused instance variable: {variable.Name}";
            case VariableKind.Property:
                return $"Unused property: {variable.Name}";
            case VariableKind.Global:
                return $"Unused global variable: {variable.Name}";
            default:
                var scopePrefix = GetTooltipPrefixForScope(variable.DeclarationScope);
                return $"{scopePrefix}: {variable.Name}";
        }
    }

    /// <summary>
    /// Gets the appropriate tooltip prefix based on the scope type
    /// </summary>
    private string GetTooltipPrefixForScope(ScopeContext scopeInfo)
    {

        return scopeInfo.Type switch
        {
            EnhancedScopeType.Method => "Unused method variable",
            EnhancedScopeType.Function => "Unused function variable",
            EnhancedScopeType.Property => "Unused property variable",
            //EnhancedScopeType.Getter => "Unused getter variable",
            //EnhancedScopeType.Setter => "Unused setter variable",
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
        // Base class handles VariableTracker.Reset() automatically
    }

    #endregion
}