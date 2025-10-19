using PeopleCodeParser.SelfHosted.Nodes;

namespace AppRefiner.Linters
{
    public class ReusedForIterator : BaseLintRule
    {
        public override string LINTER_ID => "REUSED_FOR_ITER";

        private Stack<string> forIterators = new();

        public ReusedForIterator()
        {
            Description = "Detect the re-use of for loop iterators in nested for loops.";
            Type = ReportType.Error;
            Active = true;
        }

        public override void VisitFor(ForStatementNode node)
        {
            // Get the iterator variable from the for statement
            var iterator = node.IteratorName;

            // Check if the iterator is already in use
            if (forIterators.Contains(iterator))
            {
                // Report the re-use of the iterator
                AddReport(
                    1,
                    $"Re-use of for loop iterator '{iterator}' in nested for loop.",
                    Type,
                    node.SourceSpan.Start.Line,
                    node.SourceSpan
                );
            }
            else
            {
                // Push the iterator onto the stack
                forIterators.Push(iterator);
            }

            // Visit the for loop body
            base.VisitFor(node);

            // Pop the iterator off the stack when exiting the for statement
            if (forIterators.Count > 0 && forIterators.Peek() == iterator)
            {
                forIterators.Pop();
            }
        }

        public override void Reset()
        {
            forIterators.Clear();
            base.Reset();
        }
    }
}
