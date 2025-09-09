using PeopleCodeParser.SelfHosted;
using PeopleCodeParser.SelfHosted.Nodes;


namespace AppRefiner.Stylers;

/// <summary>
/// Visitor that identifies meaningless variable names in PeopleCode.
/// This is a self-hosted equivalent to the AppRefiner's MeaninglessVariableNameStyler.
/// </summary>
public class MeaninglessVariableNameStyler : BaseStyler
{
    private const uint HIGHLIGHT_COLOR = 0xD9D6A560; // Yellow highlight color from original
    private readonly HashSet<string> meaninglessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "b", "c", "d", "e", "f", "g", "h", "j", "k", "l", "m", "n", "o", "p", "q", "r", "s", "t", "u", "v", "w",
        "aa", "bb", "cc", "dd", "ee", "ff", "gg", "hh", "ii", "jj", "kk", "ll", "mm", "nn", "oo", "pp", "qq", "rr", "ss", "tt", "uu", "vv", "ww", "xx", "yy", "zz",
        "var", "var1", "var2", "var3", "temp", "tmp", "temp1", "tmp1", "temp2", "tmp2", "foo", "bar", "baz",
        "obj", "object", "str", "string", "num", "number", "int", "integer", "flt", "float", "bool", "boolean",
        "arr", "array", "lst", "list", "val", "value", "res", "result", "ret", "return"
    };

    public override string Description => "Meaningless variable names";

    #region AST Visitor Overrides

    /// <summary>
    /// Handles local variable declarations
    /// </summary>
    public override void VisitLocalVariableDeclaration(LocalVariableDeclarationNode node)
    {
        CheckVariableNames(node.VariableNames);
        base.VisitLocalVariableDeclaration(node);
    }

    /// <summary>
    /// Handles local variable declarations with assignment
    /// </summary>
    public override void VisitLocalVariableDeclarationWithAssignment(LocalVariableDeclarationWithAssignmentNode node)
    {
        CheckVariableName(node.VariableName, node.SourceSpan);
        base.VisitLocalVariableDeclarationWithAssignment(node);
    }

    /// <summary>
    /// Handles method declarations to check parameter names
    /// </summary>
    public override void VisitMethod(MethodNode node)
    {
        // Check parameter names before visiting the method body
        foreach (var parameter in node.Parameters)
        {
            CheckVariableName(parameter.Name, parameter.SourceSpan);
        }

        base.VisitMethod(node);
    }

    /// <summary>
    /// Handles instance variable declarations
    /// </summary>
    public override void VisitVariable(VariableNode node)
    {
        CheckVariableName(node.Name, node.NameToken.SourceSpan);
        base.VisitVariable(node);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Checks multiple variable names from a collection
    /// </summary>
    private void CheckVariableNames(IEnumerable<string> variableNames)
    {
        foreach (var variableName in variableNames)
        {
            // For local variable declarations, we need to find the source span
            // This is a simplified approach - in the real implementation we'd need
            // more precise span information for each variable
            CheckVariableName(variableName, default);
        }
    }

    /// <summary>
    /// Checks a single variable name and adds indicator if it's meaningless
    /// </summary>
    private void CheckVariableName(string variableName, SourceSpan span)
    {
        // Remove & if it's at the start of the variable name (PeopleCode instance variable prefix)
        string cleanName = variableName.StartsWith("&") ? variableName.Substring(1) : variableName;

        // Check if the variable name is in our list of meaningless names
        if (meaninglessNames.Contains(cleanName))
        {
            // Add indicator for this meaningless variable name
            // Note: In the simplified model, we don't have QuickFixes support yet
            AddIndicator(
                span,
                IndicatorType.HIGHLIGHTER,
                HIGHLIGHT_COLOR,
                "Meaningless variable name"
            );
        }
    }

    #endregion
}