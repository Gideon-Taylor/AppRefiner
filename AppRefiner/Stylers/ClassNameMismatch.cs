using AppRefiner.Database;
using PeopleCodeParser.SelfHosted.Nodes;
using System;
using System.Linq;

namespace AppRefiner.Stylers;

/// <summary>
/// Visitor that identifies class names that do not match the expected name from the editor's class path.
/// This is a self-hosted equivalent to the AppRefiner's ClassNameMismatch styler.
/// </summary>
public class ClassNameMismatch : BaseStyler
{
    private const uint ERROR_COLOR = 0x0000FFFF; // Red color for class name mismatch

    public override string Description => "Class name mismatch";

    /// <summary>
    /// Processes the entire program and resets state
    /// </summary>
    public override void VisitProgram(ProgramNode node)
    {
        Reset();
        
        // Visit the program
        base.VisitProgram(node);
    }

    /// <summary>
    /// Handles application class definitions and validates the class name
    /// </summary>
    public override void VisitAppClass(AppClassNode node)
    {
        if (Editor == null)
        {
            base.VisitAppClass(node);
            return;
        }

        // Get the class name from the AST
        string className = node.Name;
        
        // Get the expected class name from the editor's class path
        string expectedName = Editor.ClassPath.Split(':').LastOrDefault() ?? string.Empty;
        
        // Check if the class name matches the expected name (case-insensitive)
        if (!string.Equals(className, expectedName, StringComparison.OrdinalIgnoreCase))
        {
            string tooltip = $"Class name '{className}' does not match expected name '{expectedName}'.";
            AddIndicator(node.NameToken.SourceSpan, IndicatorType.SQUIGGLE, ERROR_COLOR, tooltip);
        }

        base.VisitAppClass(node);
    }
}