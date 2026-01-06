using PeopleCodeParser.SelfHosted.Nodes;

namespace AppRefiner.Stylers;

/// <summary>
/// Visitor that detects the re-use of for loop iterators in nested for loops.
/// This is a self-hosted equivalent to the AppRefiner's ReusedForIterator linter.
/// </summary>
public class ReusedForIterator : BaseStyler
{
    private const uint ERROR_COLOR = 0x0000FF85; // Red color for errors
    private readonly Stack<string> forIterators = new();

    public override string Description => "Reused For iterators";

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
    /// Handles FOR statements and checks for iterator reuse
    /// </summary>
    public override void VisitFor(ForStatementNode node)
    {
        // Get the iterator variable from the for statement
        var iterator = node.IteratorName;

        // Check if the iterator is already in use
        if (forIterators.Contains(iterator))
        {
            // Report the re-use of the iterator
            AddIndicator(
                node.IteratorToken.SourceSpan,
                IndicatorType.HIGHLIGHTER,
                ERROR_COLOR,
                $"Re-use of for loop iterator '{iterator}' in nested for loop."
            );
        }
        else
        {
            // Push the iterator onto the stack
            forIterators.Push(iterator);
        }

        // Visit the for loop body (where nested for loops might be)
        base.VisitFor(node);

        // Pop the iterator off the stack when exiting the for statement
        // Only pop if this iterator is on top of the stack (defensive programming)
        if (forIterators.Count > 0 && forIterators.Peek() == iterator)
        {
            forIterators.Pop();
        }
    }

    #endregion

    #region Lifecycle Methods

    /// <summary>
    /// Resets the styler to its initial state
    /// </summary>
    public new void Reset()
    {
        base.Reset();
        forIterators.Clear();
    }

    #endregion
}