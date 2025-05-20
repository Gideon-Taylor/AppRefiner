
    using AppRefiner.Linters;
    using Antlr4.Runtime.Tree;
    using static AppRefiner.PeopleCode.PeopleCodeParser;
    using AppRefiner.Database;
    using AppRefiner.PeopleCode; // Added for Report
    using AppRefiner.Linters.Models; // Added for ReportType enum and Report class
    using System.Linq; // Added for Where clause
    using System.Collections.Generic; // Added for List
    using global::AppRefiner.Database;
    using global::AppRefiner.Linters;
    using global::AppRefiner.PeopleCode;
    using static global::AppRefiner.PeopleCode.PeopleCodeParser;
using Antlr4.Runtime.Misc;
using static SqlParser.Ast.Statement;
using System.Net.Mime;

namespace AppRefiner.Stylers
{
    public class ReusedForIterator : BaseStyler
    {
        // Corrected BGRA format (BBGGRRAA)
        private const uint ErrorColor = 0x0000FFFF;   // Opaque Red
        private const uint WarningColor = 0x00FFFF00; // Opaque Yellow

        private Stack<string> forIterators = new Stack<string>();

        public ReusedForIterator()
        {
            Description = "Highlights for loops that re-use an outer for's iterator.";
            Active = true; // Set to true to enable by default, or manage externally
        }

        public override DataManagerRequirement DatabaseRequirement => DataManagerRequirement.Optional;

        public override void EnterForStatement([NotNull] PeopleCodeParser.ForStatementContext context)
        {
            // Get the iterator variable from the for statement
            var iterator = context.USER_VARIABLE().GetText();
            // Check if the iterator is already in use
            if (forIterators.Contains(iterator))
            {
                var startIndex = context.FOR().Symbol.StartIndex;
                var endIndex = context.USER_VARIABLE().Symbol.StopIndex;

                var expressions = context.expression();
                if (expressions.Length > 0)
                {
                    endIndex = expressions[0].Stop.StartIndex;
                }
                if (expressions.Length > 1)
                {
                    endIndex = expressions[1].Stop.StartIndex;
                }

                if (Indicators != null)
                {
                    Indicators.Add(new Indicator
                    {
                        Start = startIndex,
                        Length = endIndex - startIndex + 1,
                        Color = ErrorColor,
                        Tooltip = $"For loop re-uses iterator {iterator} which is used by an outer for loop.",
                        Type = IndicatorType.SQUIGGLE
                    });
                }

            }
            else
            {
                // Push the iterator onto the stack
                forIterators.Push(iterator);
            }
        }
        public override void ExitForStatement([NotNull] PeopleCodeParser.ForStatementContext context)
        {
            // Pop the iterator off the stack when exiting the for statement
            var iterator = context.USER_VARIABLE().GetText();
            if (forIterators.Count > 0 && forIterators.Peek() == iterator)
            {
                forIterators.Pop();
            }
        }
    }
}
