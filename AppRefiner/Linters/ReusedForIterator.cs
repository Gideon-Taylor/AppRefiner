using Antlr4.Runtime.Misc;
using AppRefiner.PeopleCode;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AppRefiner.Linters
{
    public class ReusedForIterator : BaseLintRule
    {
        public override string LINTER_ID => "REUSED_FOR_ITER";

        private Stack<string> forIterators = new Stack<string>();

        public ReusedForIterator()
        {
            Description = "Detect the re-use of for loop iterators in nested for loops.";
            Type = ReportType.Error;
            Active = true;
        }

        public override void EnterForStatement([NotNull] PeopleCode.PeopleCodeParser.ForStatementContext context)
        {
            // Get the iterator variable from the for statement
            var iterator = context.USER_VARIABLE().GetText();
            // Check if the iterator is already in use
            if (forIterators.Contains(iterator))
            {
                // Report the re-use of the iterator
                AddReport(
                    1,
                    $"Re-use of for loop iterator '{iterator}' in nested for loop.",
                    Type,
                    context.Start.Line - 1,
                    context
                );
            }
            else
            {
                // Push the iterator onto the stack
                forIterators.Push(iterator);
            }
        }
        public override void ExitForStatement([NotNull] PeopleCode.PeopleCodeParser.ForStatementContext context)
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
