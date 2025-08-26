using System;
using System.Linq;
using PeopleCodeParser.SelfHosted.Nodes;

namespace AppRefiner.Stylers;

/// <summary>
/// Identifies calls to the Find() function where the second parameter is a string literal,
/// as the parameters might be reversed from the expected (field, value).
/// This is a self-hosted equivalent to the ANTLR-based FindFunctionParameterStyler.
/// </summary>
public class FindFunctionParameterStyler : BaseStyler
{
    // Light Green color for the squiggle indicator (ARGB format)
    private const uint LIGHT_GREEN_COLOR = 0x32FF32FF;

    public override string Description => "Find() parameter order";

    /// <summary>
    /// Processes the entire program to find Find() function calls
    /// </summary>
    public override void VisitProgram(ProgramNode node)
    {
        Reset();
        base.VisitProgram(node);
    }

    /// <summary>
    /// Processes function calls to detect Find() with potentially reversed parameters
    /// </summary>
    public override void VisitFunctionCall(FunctionCallNode node)
    {
        // Check if this is a call to the Find() function
        if (IsFindFunction(node))
        {
            // Validate parameter count (need at least 2 arguments)
            if (node.Arguments.Count >= 2)
            {
                // Check if the second argument is a string literal
                var secondArg = node.Arguments[1];
                if (IsStringLiteral(secondArg))
                {
                    // Found the pattern: Find(..., "string") - parameters might be reversed
                    AddIndicator(
                        node.SourceSpan,
                        IndicatorType.SQUIGGLE,
                        LIGHT_GREEN_COLOR,
                        "Parameters may be backwards for Find() function. Expected Find(&needle, &haystack)."
                    );
                }
            }
        }

        // Continue visiting child nodes
        base.VisitFunctionCall(node);
    }

    /// <summary>
    /// Determines if a function call node represents a call to the Find() function
    /// </summary>
    private static bool IsFindFunction(FunctionCallNode node)
    {
        // The function name should be an identifier
        if (node.Function is IdentifierNode identifier)
        {
            return string.Equals(identifier.Name, "Find", StringComparison.OrdinalIgnoreCase);
        }
        
        return false;
    }

    /// <summary>
    /// Determines if an expression node is a string literal
    /// </summary>
    private static bool IsStringLiteral(ExpressionNode expression)
    {
        return expression is LiteralNode literal && 
               literal.LiteralType == LiteralType.String;
    }
}