using Antlr4.Runtime.Tree;
using AppRefiner.PeopleCode;
using System.Linq;
using static AppRefiner.PeopleCode.PeopleCodeParser; // Import context types directly

namespace AppRefiner.Stylers
{
    /// <summary>
    /// Identifies calls to the Find() function where the second parameter is a string literal,
    /// as the parameters might be reversed from the expected (field, value).
    /// </summary>
    public class FindFunctionParameterStyler : BaseStyler
    {
        // Light Green color for the squiggle indicator (ARGB)
        private const uint LightGreen = 0x32FF32FF;

        public FindFunctionParameterStyler()
        {
            Description = "Find() function calls where the second parameter is a string literal (parameters might be reversed)";
            Active = true; // Assuming it should be active by default
        }

        public override void EnterSimpleFunctionCall(SimpleFunctionCallContext context)
        {
            // Check if the function name is "Find" (case-insensitive)
            // Function name is under genericID in simpleFunctionCall rule
            string functionName = context.genericID().GetText();
            if (!string.Equals(functionName, "Find", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Access arguments via functionCallArguments -> expression
            var args = context.functionCallArguments()?.expression();
            if (args == null || args.Length < 2)
            {
                // Need at least two arguments
                return;
            }

            // Check if the second argument is a string literal expression
            var secondArg = args[1];
            // Check if the expression context is a LiteralExprContext
            if (secondArg is LiteralExprContext literalExpr &&
                literalExpr.literal()?.StringLiteral() != null) // Check if the literal is a StringLiteral
            {
                // Found the pattern: Find(..., "string")
                Indicators ??= []; // Ensure Indicators list is initialized
                Indicators.Add(new Indicator
                {
                    Start = context.Start.StartIndex,
                    Length = context.Stop.StopIndex - context.Start.StartIndex + 1,
                    Color = LightGreen,
                    Tooltip = "Parameters may be backwards for Find() function. Expected Find(&needle, &haystack).",
                    Type = IndicatorType.SQUIGGLE
                });
            }

            // Call the correct base method
            base.EnterSimpleFunctionCall(context);
        }
    }
} 